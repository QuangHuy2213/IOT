using Microsoft.EntityFrameworkCore;

namespace Lab4_1.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        // Đổi tên DbSet MqttMessages -> Telemetries
        public DbSet<Telemetry> Telemetries { get; set; }

        // Thêm DbSet cho Devices
        public DbSet<Device> Devices { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Đổi tên bảng cũ MqttMessages thành Telemetries
            modelBuilder.Entity<Telemetry>().ToTable("Telemetries");

            // Thiết lập cột Topic trong bảng Devices là duy nhất (unique)
            // để đảm bảo không có 2 thiết bị nào trùng topic
            modelBuilder.Entity<Device>()
                .HasIndex(d => d.Topic)
                .IsUnique();
        }
    }
}
