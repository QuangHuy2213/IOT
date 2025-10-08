namespace Lab4_1.Data
{
    public class MqttSettings
    {
        public string ClientId { get; set; } = string.Empty;
        public string Host { get; set; } = string.Empty;
        public int Port { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public bool UseTls { get; set; }
    }
}
