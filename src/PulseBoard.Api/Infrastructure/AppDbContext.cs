using Microsoft.EntityFrameworkCore;
using PulseBoard.Api.Domain;

namespace PulseBoard.Api.Infrastructure;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Device> Devices => Set<Device>();
    public DbSet<Reading> Readings => Set<Reading>();
    public DbSet<Alert> Alerts => Set<Alert>();
    public DbSet<User> Users => Set<User>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Device>(e =>
        {
            e.HasKey(d => d.Id);
            e.Property(d => d.Id).HasMaxLength(32);
            e.Property(d => d.Status).HasConversion<string>().HasMaxLength(16);
        });

        b.Entity<Reading>(e =>
        {
            e.HasKey(r => r.Id);
            e.HasIndex(r => new { r.DeviceId, r.Timestamp });
            e.HasOne<Device>().WithMany().HasForeignKey(r => r.DeviceId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<Alert>(e =>
        {
            e.HasKey(a => a.Id);
            e.HasIndex(a => new { a.DeviceId, a.Kind, a.ResolvedAt });
            e.Property(a => a.Severity).HasConversion<string>().HasMaxLength(16);
            e.Property(a => a.Kind).HasMaxLength(32);
            e.HasOne<Device>().WithMany().HasForeignKey(a => a.DeviceId).OnDelete(DeleteBehavior.Cascade);
            e.Ignore(a => a.IsOpen);
        });

        b.Entity<User>(e =>
        {
            e.HasKey(u => u.Id);
            e.HasIndex(u => u.Username).IsUnique();
            e.Property(u => u.Username).HasMaxLength(64);
            e.Property(u => u.Role).HasMaxLength(16);
        });
    }
}
