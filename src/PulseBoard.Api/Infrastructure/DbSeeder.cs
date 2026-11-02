using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PulseBoard.Api.Domain;

namespace PulseBoard.Api.Infrastructure;

/// <summary>Creates the schema and loads demo devices and users on first start.</summary>
public static class DbSeeder
{
    public static readonly (string Id, string Name, string Site)[] DemoDevices =
    [
        ("edge-01", "Edge Gateway 1", "Lahore DC"),
        ("edge-02", "Edge Gateway 2", "Lahore DC"),
        ("cam-01", "Perimeter Camera A", "North Yard"),
        ("cam-02", "Perimeter Camera B", "North Yard"),
        ("plc-01", "Conveyor PLC", "Assembly Hall"),
        ("plc-02", "Packaging PLC", "Assembly Hall"),
        ("ups-01", "UPS Rack 1", "Lahore DC"),
        ("hvac-01", "HVAC Controller", "Assembly Hall"),
    ];

    /// <summary>Demo credentials. Every account uses the same password to keep the demo simple.</summary>
    public const string DemoPassword = "pulse123";

    public static readonly (string Username, string Role)[] DemoUsers =
    [
        ("viewer", Roles.Viewer),
        ("operator", Roles.Operator),
        ("admin", Roles.Admin),
    ];

    public static async Task SeedAsync(AppDbContext db, CancellationToken ct = default)
    {
        await db.Database.EnsureCreatedAsync(ct);

        if (!await db.Devices.AnyAsync(ct))
        {
            db.Devices.AddRange(DemoDevices.Select(d => new Device { Id = d.Id, Name = d.Name, Site = d.Site }));
        }

        if (!await db.Users.AnyAsync(ct))
        {
            var hasher = new PasswordHasher<User>();
            foreach (var (username, role) in DemoUsers)
            {
                var user = new User { Username = username, Role = role };
                user.PasswordHash = hasher.HashPassword(user, DemoPassword);
                db.Users.Add(user);
            }
        }

        await db.SaveChangesAsync(ct);
    }
}
