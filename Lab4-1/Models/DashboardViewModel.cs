namespace Lab4_1.Models
{
    public class DashboardViewModel
    {
        public List<DeviceInfo> Devices { get; set; } = new();
    }

    public class DeviceInfo
    {
        public string Topic { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Message { get; set; } = "Waiting for data...";
    }
}
