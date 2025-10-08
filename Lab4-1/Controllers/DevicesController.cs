using Lab4_1.Data;
using Lab4_1.Models;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace Lab4_1.Controllers;

[Route("api/devices")]
[ApiController]
public class DevicesApiController : ControllerBase
{
    private readonly DeviceRepository _deviceRepo;
    private readonly TelemetryRepository _telemetryRepo;
    private readonly MqttClientService _mqttClientService;

    public DevicesApiController(
        DeviceRepository deviceRepo,
        TelemetryRepository telemetryRepo,
        MqttClientService mqttClientService)
    {
        _deviceRepo = deviceRepo;
        _telemetryRepo = telemetryRepo;
        _mqttClientService = mqttClientService;
    }

    /// <summary>
    /// Lấy tin nhắn cuối cùng của tất cả các device.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<DeviceDataDto>>> GetLatestMessages()
    {
        var devices = await _deviceRepo.GetAllAsync();
        var result = new List<DeviceDataDto>();

        foreach (var device in devices)
        {
            var lastTelemetry = await _telemetryRepo.GetLastTelemetryByDeviceIdAsync(device.Id);
            result.Add(new DeviceDataDto
            {
                Topic = device.Topic,
                LastMessage = lastTelemetry?.Payload ?? "N/A"
            });
        }

        return result.Any() ? Ok(result) : NoContent();
    }

    /// <summary>
    /// Lấy tin nhắn cuối cùng của một device theo topic.
    /// </summary>
    [HttpGet("{*topic}")]
    public async Task<ActionResult<DeviceDataDto>> GetMessageByTopic(string topic)
    {
        var device = await _deviceRepo.GetByTopicAsync(topic);
        if (device == null)
            return NotFound(new { Error = $"Topic '{topic}' not found." });

        var lastTelemetry = await _telemetryRepo.GetLastTelemetryByDeviceIdAsync(device.Id);

        return Ok(new DeviceDataDto
        {
            Topic = topic,
            LastMessage = lastTelemetry?.Payload ?? "N/A"
        });
    }

    /// <summary>
    /// Lấy 20 bản ghi telemetry cuối cùng của một topic để vẽ biểu đồ.
    /// </summary>
    [HttpGet("telemetry")]
    public async Task<ActionResult<IEnumerable<TelemetryDto>>> GetTelemetryHistory([FromQuery] string topic)
    {
        if (string.IsNullOrEmpty(topic))
            return BadRequest("Topic parameter is required.");

        var device = await _deviceRepo.GetByTopicAsync(topic);
        if (device == null)
            return NotFound();

        var history = await _telemetryRepo.GetLastNByDeviceIdAsync(device.Id, 20);

        var telemetryDtos = history
            .OrderBy(t => t.Timestamp)
            .Select(t => new TelemetryDto { Timestamp = t.Timestamp, Payload = t.Payload })
            .ToList();

        return Ok(telemetryDtos);
    }

    [HttpGet("ping")]
    public IActionResult Ping() => Ok("pong");

    [HttpGet("stream")]
    public async Task Stream([FromQuery] string topic)
    {
        HttpContext.Response.ContentType = "text/event-stream";
        HttpContext.Response.Headers.Add("Cache-Control", "no-cache");
        HttpContext.Response.Headers.Add("Connection", "keep-alive");
        HttpContext.Response.Headers.Add("X-Accel-Buffering", "no");

        await HttpContext.Response.StartAsync();

        var device = await _deviceRepo.GetByTopicAsync(topic);
        if (device == null)
        {
            var errorData = JsonSerializer.Serialize(new { error = $"Topic '{topic}' not found." });
            await HttpContext.Response.WriteAsync($"data: {errorData}\n\n");
            await HttpContext.Response.Body.FlushAsync();
            return;
        }

        try
        {
            while (!HttpContext.RequestAborted.IsCancellationRequested)
            {
                var lastTelemetry = await _telemetryRepo.GetLastTelemetryByDeviceIdAsync(device.Id);

                object parsedPayload = null;
                if (!string.IsNullOrWhiteSpace(lastTelemetry?.Payload))
                {
                    try
                    {
                        parsedPayload = JsonSerializer.Deserialize<object>(lastTelemetry.Payload);
                    }
                    catch
                    {
                        parsedPayload = lastTelemetry.Payload;
                    }
                }

                var dataToSend = new
                {
                    Topic = device.Topic,
                    LastMessage = parsedPayload ?? new { },
                    Timestamp = DateTime.UtcNow
                };

                var message = JsonSerializer.Serialize(dataToSend);
                await HttpContext.Response.WriteAsync($"data: {message}\n\n");
                await HttpContext.Response.Body.FlushAsync();

                await Task.Delay(5000, HttpContext.RequestAborted);
            }
        }
        catch (TaskCanceledException)
        {
            // client closed
        }
    }
    /// <summary>
    /// Tạo và đăng ký một thiết bị mới từ form data.
    /// </summary>
    [HttpPost]
    // ✅ THAY ĐỔI: Nhận tham số trực tiếp từ form thay vì DTO
    public async Task<IActionResult> CreateDevice([FromForm] string name, [FromForm] string description)
    {
        // ✅ THAY ĐỔI: Tự kiểm tra validation vì không còn DTO
        if (string.IsNullOrWhiteSpace(name))
        {
            // Trả về lỗi 400 Bad Request nếu name bị trống
            return BadRequest(new { error = "Device name is required." });
        }

        try
        {
            var newDevice = new Device
            {
                // ✅ THAY ĐỔI: Sử dụng trực tiếp tham số name và description
                Name = name,
                Description = description,
                Topic = ""
            };

            await _deviceRepo.AddAsync(newDevice);

            string topic = $"iot/device/{newDevice.Id}/telemetry";
            newDevice.Topic = topic;

            await _deviceRepo.UpdateAsync(newDevice);
            await _mqttClientService.SubscribeToTopic(topic);

            return CreatedAtAction(nameof(GetDeviceById), new { id = newDevice.Id }, newDevice);
        }
        catch (Exception ex)
        {
            // Log lỗi
            return StatusCode(500, "An internal error occurred while creating the device.");
        }
    }

    /// <summary>
    /// Lấy thông tin một device theo Id.
    /// </summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetDeviceById(string id)
    {
        var device = await _deviceRepo.GetByIdAsync(id);
        if (device == null)
        {
            return NotFound();
        }
        return Ok(device);
    }
    /// <summary>
    /// Gửi lệnh điều khiển thiết bị (LED/FAN) qua MQTT.
    /// </summary>
    [HttpPost("control")]
    public async Task<IActionResult> ControlDevice([FromBody] DeviceControlRequest request)
    {
        if (string.IsNullOrEmpty(request.Topic) || string.IsNullOrEmpty(request.Command))
            return BadRequest(new { error = "Topic and command are required." });

        try
        {
            // Tạo payload JSON gửi đến MQTT
            var payload = JsonSerializer.Serialize(new { command = request.Command });

            // Thêm "/control" nếu topic là base
            string controlTopic = request.Topic.EndsWith("/control")
                ? request.Topic
                : $"{request.Topic}/control";

            await _mqttClientService.PublishAsync(controlTopic, payload);

            return Ok(new { message = "Command sent successfully", topic = controlTopic, payload });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] ControlDevice: {ex.Message}");
            return StatusCode(500, new { error = "Failed to send command to device." });
        }
    }

    public class DeviceControlRequest
    {
        public string Topic { get; set; } = string.Empty;
        public string Command { get; set; } = string.Empty;
    }

}
