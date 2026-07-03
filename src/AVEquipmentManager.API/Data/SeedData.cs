using System.Security.Cryptography;
using AVEquipmentManager.Shared.Enums;
using AVEquipmentManager.Shared.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AVEquipmentManager.API.Data;

/// <summary>
/// Seed data for initial database population.
///
/// Finding #2 / CWE-521 / CWE-798 patch (2026-06-16):
/// Default account passwords are no longer hard-coded. For each of the four
/// canonical accounts the seeder consults configuration first
/// (key: <c>Seeds:&lt;role&gt;:Password</c>) and falls back to a cryptographically
/// random 32-character password generated via <see cref="RandomNumberGenerator"/>.
///
/// Generated fallback passwords are written ONCE to the API console (warning
/// level) with a prominent rotate-now banner. Capture them on first boot.
///
/// Override examples (any one of the three is enough):
///   • user-secrets:  dotnet user-secrets set "Seeds:Admin:Password" "..."
///   • env var:       export Seeds__Admin__Password="..."
///   • appsettings.Development.json (NEVER commit production passwords there)
///
/// Roles per 2026-06-15 client agreement: Admin · Supervisor · AVStaff · ITSpecialist
/// plus the legacy "Staff" account preserved for backwards compatibility.
/// </summary>
public static class SeedData
{
    public static void Initialize(
        AppDbContext   context,
        IConfiguration config,
        ILogger        logger)
    {
        // ── Seed default users ───────────────────────────────────────────
        bool usersDirty = false;
        var  generated  = new List<(string Username, string Password)>();

        usersDirty |= EnsureUser(context, config, generated,
            username: "admin",      email: "admin@avams.edu",
            role:     "Admin",      configKey: "Seeds:Admin:Password");

        usersDirty |= EnsureUser(context, config, generated,
            username: "supervisor", email: "supervisor@avams.edu",
            role:     "Supervisor", configKey: "Seeds:Supervisor:Password");

        usersDirty |= EnsureUser(context, config, generated,
            username: "avstaff",    email: "avstaff@avams.edu",
            role:     "AVStaff",    configKey: "Seeds:AVStaff:Password");

        usersDirty |= EnsureUser(context, config, generated,
            username: "itspec",     email: "itspec@avams.edu",
            role:     "ITSpecialist", configKey: "Seeds:ITSpec:Password");

        // Legacy "staff" account preserved so older test data, screenshots, and
        // documentation continue to work. Roles.Normalize() maps "Staff" → AVStaff.
        usersDirty |= EnsureUser(context, config, generated,
            username: "staff",      email: "staff@avams.edu",
            role:     "Staff",      configKey: "Seeds:Staff:Password");

        if (usersDirty)
            context.SaveChanges();

        // ── Print generated fallback passwords ONCE to the operator ─────
        if (generated.Count > 0)
        {
            logger.LogWarning("================================================================");
            logger.LogWarning("AVAMS — generated seed credentials (rotate immediately!)");
            logger.LogWarning("These accounts had no configured password and were assigned a");
            logger.LogWarning("cryptographically random one. Capture them, log in once, and");
            logger.LogWarning("rotate via Admin → Users. They will NEVER be printed again.");
            logger.LogWarning("----------------------------------------------------------------");
            foreach (var (u, p) in generated)
                logger.LogWarning("  {Username,-12}  →  {Password}", u, p);
            logger.LogWarning("================================================================");
        }

        // ── Seed staff (teachers) ────────────────────────────────────────
        if (!context.Staff.Any())
        {
            context.Staff.AddRange(
                new Staff
                {
                    Name       = "Ms. Anna Reyes",
                    EmployeeId = "EMP-1001",
                    RoomNumber = "Room 1",
                    Department = "Mathematics",
                    Email      = "areyes@school.edu",
                    Phone      = "0917-111-2222",
                    CreatedAt  = DateTime.UtcNow
                },
                new Staff
                {
                    Name       = "Mr. Carlos Cruz",
                    EmployeeId = "EMP-1002",
                    RoomNumber = "Room 2",
                    Department = "Science",
                    Email      = "ccruz@school.edu",
                    Phone      = "0917-333-4444",
                    CreatedAt  = DateTime.UtcNow
                },
                new Staff
                {
                    Name       = "Mrs. Liza Tan",
                    EmployeeId = "EMP-1003",
                    RoomNumber = "Room 3",
                    Department = "English",
                    Email      = "ltan@school.edu",
                    Phone      = "0917-555-6666",
                    CreatedAt  = DateTime.UtcNow
                }
            );
            context.SaveChanges();
        }

        // ── Seed equipment ───────────────────────────────────────────────
        if (context.Equipment.Any())
            return;

        var equipment = new List<Equipment>
        {
            // Room 1
            new Equipment
            {
                Name = "Projector",
                SerialNumber = "AV-R1-001",
                RoomName = "Room 1",
                DateInstalled = new DateTime(2024, 1, 15, 0, 0, 0, DateTimeKind.Utc),
                ExpectedLifeInYears = 5,
                Status = EquipmentStatus.Active,
                CreatedAt = DateTime.UtcNow
            },
            new Equipment
            {
                Name = "Speaker System",
                SerialNumber = "AV-R1-002",
                RoomName = "Room 1",
                DateInstalled = new DateTime(2024, 2, 1, 0, 0, 0, DateTimeKind.Utc),
                ExpectedLifeInYears = 7,
                Status = EquipmentStatus.Active,
                CreatedAt = DateTime.UtcNow
            },
            new Equipment
            {
                Name = "Microphone",
                SerialNumber = "AV-R1-003",
                RoomName = "Room 1",
                DateInstalled = new DateTime(2023, 6, 10, 0, 0, 0, DateTimeKind.Utc),
                ExpectedLifeInYears = 3,
                Status = EquipmentStatus.UnderMaintenance,
                CreatedAt = DateTime.UtcNow
            },

            // Room 2
            new Equipment
            {
                Name = "LED Display",
                SerialNumber = "AV-R2-001",
                RoomName = "Room 2",
                DateInstalled = new DateTime(2023, 3, 20, 0, 0, 0, DateTimeKind.Utc),
                ExpectedLifeInYears = 8,
                Status = EquipmentStatus.Active,
                CreatedAt = DateTime.UtcNow
            },
            new Equipment
            {
                Name = "Amplifier",
                SerialNumber = "AV-R2-002",
                RoomName = "Room 2",
                DateInstalled = new DateTime(2022, 11, 5, 0, 0, 0, DateTimeKind.Utc),
                ExpectedLifeInYears = 6,
                Status = EquipmentStatus.Active,
                CreatedAt = DateTime.UtcNow
            },
            new Equipment
            {
                Name = "Webcam",
                SerialNumber = "AV-R2-003",
                RoomName = "Room 2",
                DateInstalled = new DateTime(2024, 4, 1, 0, 0, 0, DateTimeKind.Utc),
                ExpectedLifeInYears = 4,
                Status = EquipmentStatus.Retired,
                CreatedAt = DateTime.UtcNow
            },

            // Room 3
            new Equipment
            {
                Name = "Interactive Whiteboard",
                SerialNumber = "AV-R3-001",
                RoomName = "Room 3",
                DateInstalled = new DateTime(2023, 9, 15, 0, 0, 0, DateTimeKind.Utc),
                ExpectedLifeInYears = 10,
                Status = EquipmentStatus.Active,
                CreatedAt = DateTime.UtcNow
            },
            new Equipment
            {
                Name = "Soundbar",
                SerialNumber = "AV-R3-002",
                RoomName = "Room 3",
                DateInstalled = new DateTime(2024, 5, 20, 0, 0, 0, DateTimeKind.Utc),
                ExpectedLifeInYears = 5,
                Status = EquipmentStatus.Active,
                CreatedAt = DateTime.UtcNow
            }
        };

        context.Equipment.AddRange(equipment);
        context.SaveChanges();
    }

    // ─────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Inserts the user if it doesn't already exist. Returns TRUE when a new
    /// row was added so the caller knows to call SaveChanges. If
    /// <paramref name="configKey"/> resolves to a configured password, that
    /// value is used; otherwise a cryptographically random one is generated
    /// and pushed into the <paramref name="generated"/> list for one-time
    /// console logging by the caller.
    /// </summary>
    private static bool EnsureUser(
        AppDbContext context,
        IConfiguration config,
        List<(string Username, string Password)> generated,
        string username, string email, string role, string configKey)
    {
        if (context.Users.Any(u => u.Username == username || u.Email == email))
            return false;

        var password = config[configKey];
        if (string.IsNullOrWhiteSpace(password))
        {
            password = GenerateRandomPassword(length: 32);
            generated.Add((username, password));
        }

        context.Users.Add(new User
        {
            Username     = username,
            Email        = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            Role         = role,
            CreatedAt    = DateTime.UtcNow
        });
        return true;
    }

    // RFC 4648 base64url alphabet — URL-safe and printable in any terminal.
    private static readonly char[] PwAlphabet =
        "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_".ToCharArray();

    private static string GenerateRandomPassword(int length)
    {
        var chars = new char[length];
        Span<byte> buf = stackalloc byte[length];
        RandomNumberGenerator.Fill(buf);
        for (int i = 0; i < length; i++)
            chars[i] = PwAlphabet[buf[i] % PwAlphabet.Length];
        return new string(chars);
    }
}
