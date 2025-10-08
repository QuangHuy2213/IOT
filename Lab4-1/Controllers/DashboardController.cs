using Lab4_1.Data;
using Lab4_1.Models;
using Microsoft.AspNetCore.Mvc;
using System.Net.Http;

public class DashboardController : Controller
{
    private readonly DeviceRepository _deviceRepo;
    private readonly TelemetryRepository _telemetryRepo;
    private readonly MqttClientService _mqttClientService;
    private readonly IHttpClientFactory _httpClientFactory;

    public DashboardController(
        DeviceRepository deviceRepo,
        TelemetryRepository telemetryRepo,
        MqttClientService mqttClientService,
        IHttpClientFactory httpClientFactory)
    {
        _deviceRepo = deviceRepo;
        _telemetryRepo = telemetryRepo;
        _mqttClientService = mqttClientService;
        _httpClientFactory = httpClientFactory;
        _httpClientFactory = httpClientFactory;
    }

    public async Task<IActionResult> Index()
    {
        List<Device> devicesWithLatestTelemetry = new();

        try
        {
            var devices = await _deviceRepo.GetAllAsync();
            devicesWithLatestTelemetry = devices.ToList();
        }
        catch
        {
            devicesWithLatestTelemetry = new List<Device>();
        }

        var model = new DashboardViewModel
        {
            Devices = new List<DeviceInfo>()
        };

        foreach (var d in devicesWithLatestTelemetry)
        {
            var lastTelemetry = await _telemetryRepo.GetLastTelemetryByDeviceIdAsync(d.Id);
            model.Devices.Add(new DeviceInfo
            {
                Topic = d.Topic,
                Name = d.Name,
                Message = lastTelemetry?.Payload ?? "Waiting for data..."
            });
        }

        return View(model);
    }

    [HttpPost]
    public async Task<IActionResult> Subscribe(string description, string name)
    {
        // Tạo một HTTP client từ factory
        var httpClient = _httpClientFactory.CreateClient();

        // ✅ THAY ĐỔI: Tạo nội dung dưới dạng Form-data thay vì JSON
        var formData = new FormUrlEncodedContent(new[]
        {
        new KeyValuePair<string, string>("name", name),
        new KeyValuePair<string, string>("description", description)
    });

        try
        {
            var baseUrl = $"{Request.Scheme}://{Request.Host}";
            var apiUrl = $"{baseUrl}/api/devices";

            // ✅ THAY ĐỔI: Dùng PostAsync và truyền formData vào
            HttpResponseMessage response = await httpClient.PostAsync(apiUrl, formData);

            // Kiểm tra xem API có trả về lỗi hay không
            response.EnsureSuccessStatusCode();
        }
        catch (HttpRequestException ex)
        {
            ModelState.AddModelError(string.Empty, "Có lỗi xảy ra khi cố gắng tạo thiết bị. Vui lòng thử lại.");
            Console.WriteLine($"Lỗi khi gọi API: {ex.Message}");
            // Bạn nên trả về View để người dùng thấy lỗi
            // return View("TênViewCủaForm"); 
        }

        // Nếu mọi thứ thành công, chuyển hướng về trang Index như cũ
        return RedirectToAction("Index");
    }

    [HttpPost]
    public async Task<IActionResult> Unsubscribe(string name)
    {
        if (string.IsNullOrEmpty(name))
            return RedirectToAction("Index");

        await _mqttClientService.UnsubscribeFromTopic(name);

        try
        {
            var deviceToRemove = await _deviceRepo.GetByNameAsync(name);
            if (deviceToRemove != null)
            {
                await _deviceRepo.DeleteAsync(deviceToRemove.Id);
            }
        }
        catch
        {
            // Ignore lỗi nếu DB chưa chạy
        }

        return RedirectToAction("Index");
    }

    [HttpPost]
    public async Task<IActionResult> DeleteDevice(string topic)
    {
        if (string.IsNullOrEmpty(topic))
            return BadRequest("Missing topic");
       
        await _deviceRepo.DeleteByTopicAsync(topic);
        await _mqttClientService.UnsubscribeFromTopic(topic);        // Hủy theo dõi MQTT

        return Ok(new { message = "Device deleted successfully" });
    }

}
