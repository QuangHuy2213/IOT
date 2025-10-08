using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Lab4_1.Data
{
    public class Telemetry
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; }

        [BsonElement("payload")]
        public required string Payload { get; set; }

        [BsonElement("timestamp")]
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        // Thay foreign key bằng ObjectId tham chiếu Device
        [BsonRepresentation(BsonType.ObjectId)]
        [BsonElement("deviceId")]
        public string DeviceId { get; set; }
    }
}
