namespace AVEquipmentManager.Shared.DTOs;

public class LoginResponseDto
{
    public string Token     { get; set; } = string.Empty;
    public string Username  { get; set; } = string.Empty;
    public string Email     { get; set; } = string.Empty;
    public string Role      { get; set; } = string.Empty;
    public DateTime Expiry  { get; set; }
}
