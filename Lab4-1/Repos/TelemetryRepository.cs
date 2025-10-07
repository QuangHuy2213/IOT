using Lab4_1.Data;
using MongoDB.Driver;

public class TelemetryRepository
{
    private readonly IMongoCollection<Telemetry> _telemetries;

    public TelemetryRepository(IMongoDatabase database)
    {
        _telemetries = database.GetCollection<Telemetry>("Telemetries");
    }

    public async Task AddAsync(Telemetry telemetry) =>
        await _telemetries.InsertOneAsync(telemetry);

    public async Task<Telemetry> GetLastTelemetryByDeviceIdAsync(string deviceId) =>
        await _telemetries
            .Find(t => t.DeviceId == deviceId)
            .SortByDescending(t => t.Timestamp)
            .FirstOrDefaultAsync();

    public async Task<List<Telemetry>> GetLastNByDeviceIdAsync(string deviceId, int n) =>
        await _telemetries
            .Find(t => t.DeviceId == deviceId)
            .SortByDescending(t => t.Timestamp)
            .Limit(n)
            .ToListAsync();
}
