using Lab4_1.Data;
using MongoDB.Driver;

public class DeviceRepository
{
    private readonly IMongoCollection<Device> _devices;

    public DeviceRepository(IMongoDatabase database)
    {
        _devices = database.GetCollection<Device>("Devices");
    }

    public async Task<Device> GetByTopicAsync(string topic) =>
        await _devices.Find(d => d.Topic == topic).FirstOrDefaultAsync();

    public async Task<Device> GetByNameAsync(string name) =>
        await _devices.Find(d => d.Name == name).FirstOrDefaultAsync();

    public async Task<List<Device>> GetAllAsync() =>
        await _devices.Find(FilterDefinition<Device>.Empty).ToListAsync();

    public async Task AddAsync(Device device) =>
        await _devices.InsertOneAsync(device);

    public async Task UpdateAsync(Device device) =>
        await _devices.ReplaceOneAsync(d => d.Id == device.Id, device);

    public async Task DeleteAsync(string id)
    {
        await _devices.DeleteOneAsync(d => d.Id == id);
    }
    public async Task<Device?> GetByIdAsync(string id)
    {
        // Tìm device có Id khớp với id được cung cấp
        return await _devices.Find(d => d.Id == id).FirstOrDefaultAsync();
    }
    public async Task<List<string>> GetAllTopicsAsync() =>
        await _devices.Distinct<string>(nameof(Device.Topic), FilterDefinition<Device>.Empty).ToListAsync();
    public async Task DeleteByTopicAsync(string topic)
    {
        await _devices.DeleteOneAsync(d => d.Topic == topic);
    }

}
