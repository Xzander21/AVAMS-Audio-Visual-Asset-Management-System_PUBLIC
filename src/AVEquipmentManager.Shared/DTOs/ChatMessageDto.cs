namespace AVEquipmentManager.Shared.DTOs;

public class ChatMessageDto
{
    public string Message { get; set; } = string.Empty;
    public string Response { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public bool IsUser { get; set; }
}
