using Lab4_1.Data;
using Lab4_1.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Extensions.ManagedClient;
using MQTTnet.Packets;
using MQTTnet.Protocol;
using System.Text;

public class MqttClientService : IHostedService
{
    private readonly IManagedMqttClient _mqttClient;
    private readonly IHubContext<DashboardHub> _hubContext;
    private readonly IServiceProvider _serviceProvider;
    private readonly MqttSettings _mqttSettings;

    private static readonly HashSet<string> SubscribedTopics = new();

    private class StatusPayload
    {
        public string? Status { get; set; }
    }

    public MqttClientService(
        IHubContext<DashboardHub> hubContext,
        IServiceProvider serviceProvider,
        IOptions<MqttSettings> mqttSettingsOptions)
    {
        _hubContext = hubContext;
        _serviceProvider = serviceProvider;
        _mqttSettings = mqttSettingsOptions.Value;

        _mqttClient = new MqttFactory().CreateManagedMqttClient();
        _mqttClient.ApplicationMessageReceivedAsync += OnMessageReceived;
        _mqttClient.ConnectedAsync += OnConnected;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var options = new ManagedMqttClientOptionsBuilder()
            .WithAutoReconnectDelay(TimeSpan.FromSeconds(5))
            .WithClientOptions(new MqttClientOptionsBuilder()
                .WithClientId(_mqttSettings.ClientId)
                .WithTcpServer(_mqttSettings.Host, _mqttSettings.Port)
                .WithCredentials(_mqttSettings.Username, _mqttSettings.Password)
                .WithTls(new MqttClientOptionsBuilderTlsParameters
                {
                    UseTls = _mqttSettings.UseTls,
                    AllowUntrustedCertificates = false,
                    IgnoreCertificateChainErrors = false,
                    IgnoreCertificateRevocationErrors = false
                })
                .Build())
            .Build();

        await _mqttClient.StartAsync(options);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await _mqttClient.StopAsync();
    }

    private async Task OnConnected(MqttClientConnectedEventArgs e)
    {
        Console.WriteLine("[MQTT Service] Connected to MQTT Broker.");
        await RestoreSubscriptionsFromDb();
        await _mqttClient.SubscribeAsync("iot/device/+/status");
        Console.WriteLine("[MQTT Service] Subscribed to wildcard status topic: iot/device/+/status");
    }

    private async Task RestoreSubscriptionsFromDb()
    {
        using var scope = _serviceProvider.CreateScope();
        var deviceRepo = scope.ServiceProvider.GetRequiredService<DeviceRepository>();

        var topicsFromDb = await deviceRepo.GetAllTopicsAsync();

        if (topicsFromDb.Any())
        {
            var topicFilters = topicsFromDb.Select(t => new MqttTopicFilter { Topic = t }).ToList();
            await _mqttClient.SubscribeAsync(topicFilters);

            foreach (var topic in topicsFromDb)
                SubscribedTopics.Add(topic);

            Console.WriteLine($"[MQTT Service] Re-subscribed to {topicsFromDb.Count} topics: {string.Join(", ", topicsFromDb)}");
        }
        else
        {
            Console.WriteLine("[MQTT Service] No previous topics found in database.");
        }
    }

    private async Task OnMessageReceived(MqttApplicationMessageReceivedEventArgs e)
    {
        if (e.ApplicationMessage.Retain)
        {
            Console.WriteLine($"[MQTT] Ignored retained message: {e.ApplicationMessage.Topic}");
            return; // không xử lý retained
        }

        var topic = e.ApplicationMessage.Topic;
        var payload = Encoding.UTF8.GetString(e.ApplicationMessage.PayloadSegment);
        Console.WriteLine($"[MQTT] Topic={topic}, Payload={payload}");

        if (topic.EndsWith("/status"))
            await HandleStatusMessage(topic, payload);
        else
            await HandleTelemetryMessage(topic, payload);
    }


    private async Task HandleTelemetryMessage(string topic, string payload)
    {
        using var scope = _serviceProvider.CreateScope();
        var deviceRepo = scope.ServiceProvider.GetRequiredService<DeviceRepository>();
        var telemetryRepo = scope.ServiceProvider.GetRequiredService<TelemetryRepository>();

        var device = await deviceRepo.GetByTopicAsync(topic);
        if (device == null)
        {
            Console.WriteLine($"[MQTT] New device detected: {topic}");
            device = new Device { Name = topic, Topic = topic, Description = "Auto-registered device" };
            await deviceRepo.AddAsync(device);
        }

        var telemetry = new Telemetry { Payload = payload, Timestamp = DateTime.UtcNow, DeviceId = device.Id };
        await telemetryRepo.AddAsync(telemetry);

        await _hubContext.Clients.All.SendAsync("ReceiveMessage", topic, payload);
    }

    private async Task HandleStatusMessage(string topic, string payload)
    {
        Console.WriteLine($"[STATUS] Topic={topic}, Payload={payload}");
        string deviceBaseTopic = topic.Substring(0, topic.LastIndexOf('/'));
        string telemetryTopic = $"{deviceBaseTopic}/telemetry";
        await _hubContext.Clients.All.SendAsync("UpdateDeviceStatus", telemetryTopic, payload);
    }

    public async Task SubscribeToTopic(string topic)
    {
        if (SubscribedTopics.Add(topic))
        {
            var filter = new MqttTopicFilterBuilder().WithTopic(topic).Build();
            await _mqttClient.SubscribeAsync(new[] { filter });
        }
    }

    public async Task UnsubscribeFromTopic(string topic)
    {
        if (SubscribedTopics.Remove(topic))
            await _mqttClient.UnsubscribeAsync(topic);
    }

    public async Task PublishAsync(string topic, string payload, bool retain = false, MqttQualityOfServiceLevel qos = MqttQualityOfServiceLevel.AtLeastOnce)
    {
        var msg = new MqttApplicationMessageBuilder()
            .WithTopic(topic)
            .WithPayload(payload)
            .WithQualityOfServiceLevel(qos)
            .WithRetainFlag(retain)
            .Build();

        if (_mqttClient != null)
            await _mqttClient.EnqueueAsync(msg);
    }

    public List<string> GetSubscribedTopics() => SubscribedTopics.ToList();
}
