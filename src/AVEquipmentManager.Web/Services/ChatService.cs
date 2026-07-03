using System.Net.Http.Json;
using AVEquipmentManager.Shared.DTOs;

namespace AVEquipmentManager.Web.Services;

public class ChatService
{
    private readonly HttpClient _http;

    public ChatService(HttpClient http)
    {
        _http = http;
    }

    public async Task<(ChatMessageDto? Response, string? Error)> SendMessageAsync(string message)
    {
        var dto = new ChatMessageDto
        {
            Message = message,
            IsUser = true,
            Timestamp = DateTime.UtcNow
        };

        var response = await _http.PostAsJsonAsync("api/chat", dto);
        if (response.IsSuccessStatusCode)
            return (await response.Content.ReadFromJsonAsync<ChatMessageDto>(), null);

        var error = await response.Content.ReadAsStringAsync();
        return (null, error);
    }
}
