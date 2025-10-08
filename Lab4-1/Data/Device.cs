using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Lab4_1.Data
{
    public class Device
    {
        [BsonId] // ID của MongoDB
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; }

        [BsonElement("name")]
        public required string Name { get; set; }

        [BsonElement("topic")]
        public required string Topic { get; set; } // Topic là định danh duy nhất

        [BsonElement("description")]
        public string? Description { get; set; }

        [BsonElement("createdAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Nếu muốn embed Telemetry trong Device (tùy trường hợp)
        //[BsonElement("telemetries")]
        //public List<Telemetry> Telemetries { get; set; } = new List<Telemetry>();
    }
}
