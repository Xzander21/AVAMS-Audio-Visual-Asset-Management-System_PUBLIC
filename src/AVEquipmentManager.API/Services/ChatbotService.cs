using AVEquipmentManager.API.Data;
using AVEquipmentManager.Shared.DTOs;
using AVEquipmentManager.Shared.Enums;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Text.RegularExpressions;

namespace AVEquipmentManager.API.Services;

public class ChatbotService
{
    private readonly AppDbContext _context;

    public ChatbotService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<string> ProcessMessageAsync(string message)
    {
        var lower = message.ToLowerInvariant();

        // Summary / overview
        if (lower.Contains("summary") || lower.Contains("overview"))
            return await GetSummaryAsync();

        // Serial number lookup (e.g. AV-R1-001)
        var serialMatch = Regex.Match(message, @"AV-[A-Z0-9]+-[A-Z0-9]+", RegexOptions.IgnoreCase);
        if (serialMatch.Success)
            return await GetBySerialNumberAsync(serialMatch.Value.ToUpper());

        // Status queries
        if (lower.Contains("maintenance") || lower.Contains("under maintenance"))
            return await GetByStatusAsync(EquipmentStatus.UnderMaintenance, "Under Maintenance");

        if (lower.Contains("retired"))
            return await GetByStatusAsync(EquipmentStatus.Retired, "Retired");

        if (lower.Contains("decommission"))
            return await GetByStatusAsync(EquipmentStatus.Decommissioned, "Decommissioned");

        if (lower.Contains("active") && !lower.Contains("inactive"))
            return await GetByStatusAsync(EquipmentStatus.Active, "Active");

        // Room queries (e.g. "Room 1", "room 2")
        var roomMatch = Regex.Match(message, @"room\s+(\d+)", RegexOptions.IgnoreCase);
        if (roomMatch.Success)
            return await GetByRoomAsync($"Room {roomMatch.Groups[1].Value}");

        // Help / default
        return GetHelpMessage();
    }

    private async Task<string> GetByRoomAsync(string roomName)
    {
        var items = await _context.Equipment
            .Where(e => e.RoomName.ToLower() == roomName.ToLower())
            .OrderBy(e => e.Name)
            .ToListAsync();

        if (!items.Any())
            return $"No equipment found in {roomName}.";

        var sb = new StringBuilder();
        sb.AppendLine($"📍 Equipment in {roomName} ({items.Count} item(s)):");
        sb.AppendLine();
        foreach (var e in items)
            sb.AppendLine(FormatEquipment(e));

        return sb.ToString().TrimEnd();
    }

    private async Task<string> GetBySerialNumberAsync(string serialNumber)
    {
        var item = await _context.Equipment
            .FirstOrDefaultAsync(e => e.SerialNumber.ToUpper() == serialNumber);

        if (item == null)
            return $"No equipment found with serial number {serialNumber}.";

        return $"🔍 Equipment details for {serialNumber}:\n\n{FormatEquipment(item)}".TrimEnd();
    }

    private async Task<string> GetByStatusAsync(EquipmentStatus status, string statusLabel)
    {
        var items = await _context.Equipment
            .Where(e => e.Status == status)
            .OrderBy(e => e.RoomName)
            .ThenBy(e => e.Name)
            .ToListAsync();

        if (!items.Any())
            return $"No equipment with status: {statusLabel}.";

        var sb = new StringBuilder();
        sb.AppendLine($"🏷️ Equipment with status '{statusLabel}' ({items.Count} item(s)):");
        sb.AppendLine();
        foreach (var e in items)
            sb.AppendLine(FormatEquipment(e));

        return sb.ToString().TrimEnd();
    }

    private async Task<string> GetSummaryAsync()
    {
        var all = await _context.Equipment.ToListAsync();

        var byRoom = all.GroupBy(e => e.RoomName)
            .OrderBy(g => g.Key)
            .Select(g => $"  • {g.Key}: {g.Count()} item(s)");

        var byStatus = all.GroupBy(e => e.Status)
            .OrderBy(g => g.Key.ToString())
            .Select(g => $"  • {g.Key}: {g.Count()} item(s)");

        var sb = new StringBuilder();
        sb.AppendLine($"📊 AV Equipment Summary ({all.Count} total items)");
        sb.AppendLine();
        sb.AppendLine("By Room:");
        foreach (var line in byRoom)
            sb.AppendLine(line);
        sb.AppendLine();
        sb.AppendLine("By Status:");
        foreach (var line in byStatus)
            sb.AppendLine(line);

        return sb.ToString().TrimEnd();
    }

    private static string FormatEquipment(Shared.Models.Equipment e)
    {
        var endDate = e.DateInstalled.AddYears(e.ExpectedLifeInYears);
        var remaining = endDate - DateTime.UtcNow;
        string remainingLife;
        if (remaining.TotalDays <= 0)
            remainingLife = "⚠️ Expired";
        else
        {
            int years = (int)(remaining.TotalDays / 365);
            int months = (int)((remaining.TotalDays % 365) / 30);
            remainingLife = $"{years}y {months}m remaining";
        }

        return $"  📦 {e.Name}\n" +
               $"     Serial: {e.SerialNumber}\n" +
               $"     Room: {e.RoomName}\n" +
               $"     Installed: {e.DateInstalled:yyyy-MM-dd}\n" +
               $"     Expected Life: {e.ExpectedLifeInYears} year(s)\n" +
               $"     Remaining Life: {remainingLife}\n" +
               $"     Status: {e.Status}\n" +
               (string.IsNullOrWhiteSpace(e.Notes) ? "" : $"     Notes: {e.Notes}\n");
    }

    private static string GetHelpMessage()
    {
        return "👋 Hello! I'm your AV Equipment Assistant. Here's what I can help you with:\n\n" +
               "• **Room queries**: Type 'Room 1', 'Room 2', or 'Room 3' to see all equipment in that room\n" +
               "• **Serial number lookup**: Type a serial number like 'AV-R1-001' to get equipment details\n" +
               "• **Status filter**: Type 'active', 'maintenance', 'retired', or 'decommissioned'\n" +
               "• **Summary**: Type 'summary' or 'overview' for a count by room and status\n\n" +
               "Examples:\n" +
               "  - 'Show me Room 1'\n" +
               "  - 'AV-R2-003'\n" +
               "  - 'What equipment is under maintenance?'\n" +
               "  - 'Give me a summary'";
    }
}
