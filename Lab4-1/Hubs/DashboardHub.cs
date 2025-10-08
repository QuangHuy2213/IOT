// Hubs/DashboardHub.cs
using Microsoft.AspNetCore.SignalR;

namespace Lab4_1.Hubs
{
    public class DashboardHub : Hub
    {
        private readonly MqttClientService _mqttClientService;

        // Dùng Dependency Injection để inject MQTT service vào Hub
        public DashboardHub(MqttClientService mqttClientService)
        {
            _mqttClientService = mqttClientService;
        }

        // ✅ TẠO PHƯƠNG THỨC MỚI ĐỂ CLIENT GỌI
        public async Task PublishMessage(string topic, string payload)
        {
            // Gọi service để publish tin nhắn
            await _mqttClientService.PublishAsync(topic, payload);
        }
    }
}