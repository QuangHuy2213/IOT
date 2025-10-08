using Lab4_1.Hubs;
using Lab4_1.Data;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

var builder = WebApplication.CreateBuilder(args);

// ===== MongoDB configuration =====
builder.Services.Configure<MongoDBSettings>(
    builder.Configuration.GetSection("MongoDB"));

// MongoClient singleton
builder.Services.AddSingleton<IMongoClient>(sp =>
{
    var settings = sp.GetRequiredService<IOptions<MongoDBSettings>>().Value;
    return new MongoClient(settings.ConnectionString);
});

// IMongoDatabase scoped
builder.Services.AddScoped(sp =>
{
    var settings = sp.GetRequiredService<IOptions<MongoDBSettings>>().Value;
    var client = sp.GetRequiredService<IMongoClient>();
    return client.GetDatabase(settings.DatabaseName);
});

// Repositories
builder.Services.AddScoped<DeviceRepository>();
builder.Services.AddScoped<TelemetryRepository>();

// ===== MVC + SignalR =====
builder.Services.AddControllersWithViews();
builder.Services.AddSignalR();

// ===== MQTT Client Service =====
builder.Services.AddSingleton<MqttClientService>();
builder.Services.AddHostedService(provider => provider.GetService<MqttClientService>());
builder.Services.Configure<MqttSettings>(builder.Configuration.GetSection("Mqtt"));
builder.Services.AddHttpClient();
var app = builder.Build();

// ===== HTTP pipeline =====
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

//app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();

app.MapHub<DashboardHub>("/dashboardHub");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Dashboard}/{action=Index}/{id?}");

app.Run();
