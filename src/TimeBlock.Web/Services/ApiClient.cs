global using System.Net.Http.Json;
global using System.Text.Json;
global using TimeBlock.Core.Contracts;

namespace TimeBlock.Web.Services;

public class ApiClient(HttpClient http, TokenStorage storage)
{
    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public class ApiException(string message, int statusCode) : Exception(message)
    {
        public int StatusCode { get; } = statusCode;
    }

    private async Task<T?> SendAsync<T>(HttpMethod method, string url, object? body = null)
    {
        using var response = await RawAsync(method, url, body);
        if (!response.IsSuccessStatusCode)
        {
            throw new ApiException(await DescribeAsync(response), (int)response.StatusCode);
        }

        return await response.Content.ReadFromJsonAsync<T>(JsonOptions);
    }

    private async Task SendAsync(HttpMethod method, string url, object? body = null)
    {
        using var response = await RawAsync(method, url, body);
        if (!response.IsSuccessStatusCode)
        {
            throw new ApiException(await DescribeAsync(response), (int)response.StatusCode);
        }
    }

    private async Task<HttpResponseMessage> RawAsync(HttpMethod method, string url, object? body)
    {
        var request = new HttpRequestMessage(method, url);
        var token = await TryGetTokenAsync();
        if (!string.IsNullOrWhiteSpace(token))
        {
            request.Headers.Authorization = new("Bearer", token);
        }

        if (body is not null)
        {
            request.Content = JsonContent.Create(body, options: JsonOptions);
        }

        return await http.SendAsync(request);
    }

    private async Task<string?> TryGetTokenAsync()
    {
        try
        {
            return await storage.GetAsync();
        }
        catch (JSException)
        {
            return null;
        }
    }

    private static async Task<string> DescribeAsync(HttpResponseMessage response)
    {
        var text = await response.Content.ReadAsStringAsync();
        if (string.IsNullOrWhiteSpace(text))
        {
            return $"Request failed ({(int)response.StatusCode}).";
        }

        try
        {
            using var doc = JsonDocument.Parse(text);
            if (doc.RootElement.ValueKind == JsonValueKind.Object && doc.RootElement.TryGetProperty("errors", out var errors))
            {
                var messages = errors.EnumerateObject()
                    .SelectMany(p => p.Value.EnumerateArray().Select(e => e.GetString()))
                    .Where(m => !string.IsNullOrEmpty(m));
                return string.Join(" ", messages);
            }
        }
        catch (JsonException)
        {
        }

        return text.Length > 300 ? text[..300] : text;
    }

    public Task<AuthResponse?> RegisterAsync(RegisterRequest request) => SendAsync<AuthResponse>(HttpMethod.Post, "api/auth/register", request);
    public Task<AuthResponse?> LoginAsync(LoginRequest request) => SendAsync<AuthResponse>(HttpMethod.Post, "api/auth/login", request);
    public Task<UserDto?> MeAsync() => SendAsync<UserDto>(HttpMethod.Get, "api/auth/me");
    public Task<UserDto?> UpdateSettingsAsync(UpdateSettingsRequest request) => SendAsync<UserDto>(HttpMethod.Put, "api/me", request);

    public Task<GroupDto?> CreateGroupAsync(CreateGroupRequest request) => SendAsync<GroupDto>(HttpMethod.Post, "api/groups", request);
    public Task<GroupDto?> JoinGroupAsync(JoinGroupRequest request) => SendAsync<GroupDto>(HttpMethod.Post, "api/groups/join", request);
    public Task<List<GroupDto>?> MyGroupsAsync() => SendAsync<List<GroupDto>>(HttpMethod.Get, "api/groups/mine");
    public Task<string?> RegenerateInviteAsync(Guid groupId) => SendAsync<string>(HttpMethod.Post, $"api/groups/{groupId}/regenerate-invite");
    public Task<GroupTimelineDto?> GroupTimelineAsync(Guid groupId, DateOnly? weekStart = null) =>
        SendAsync<GroupTimelineDto>(HttpMethod.Get, weekStart.HasValue ? $"api/groups/{groupId}/timeline?weekStart={weekStart:yyyy-MM-dd}" : $"api/groups/{groupId}/timeline");

    public Task<List<BlockDto>?> MyBlocksAsync(DateOnly from, DateOnly to) =>
        SendAsync<List<BlockDto>>(HttpMethod.Get, $"api/blocks?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}");
    public Task<BlockDto?> CreateBlockAsync(CreateBlockRequest request) => SendAsync<BlockDto>(HttpMethod.Post, "api/blocks", request);
    public Task<BlockDto?> UpdateBlockStatusAsync(Guid id, BlockStatus status) =>
        SendAsync<BlockDto>(HttpMethod.Patch, $"api/blocks/{id}/status", new UpdateBlockStatusRequest(status));
    public Task DeleteBlockAsync(Guid id) => SendAsync(HttpMethod.Delete, $"api/blocks/{id}");
    public Task<List<DayLoad>?> OverloadAsync(DateOnly from, DateOnly to) =>
        SendAsync<List<DayLoad>>(HttpMethod.Get, $"api/blocks/overload?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}");

    public Task<List<TemplateDto>?> MyTemplatesAsync() => SendAsync<List<TemplateDto>>(HttpMethod.Get, "api/templates");
    public Task<TemplateDto?> CreateTemplateAsync(CreateTemplateRequest request) => SendAsync<TemplateDto>(HttpMethod.Post, "api/templates", request);
    public Task DeleteTemplateAsync(Guid id) => SendAsync(HttpMethod.Delete, $"api/templates/{id}");

    public Task<List<WeekFollowThroughDto>?> FollowThroughAsync(int weeks = 6) =>
        SendAsync<List<WeekFollowThroughDto>>(HttpMethod.Get, $"api/stats/follow-through?weeks={weeks}");

    public Task<List<NotificationDto>?> NotificationsAsync() => SendAsync<List<NotificationDto>>(HttpMethod.Get, "api/notifications");
    public Task MarkReadAsync(Guid id) => SendAsync(HttpMethod.Post, $"api/notifications/{id}/read");
    public Task MarkAllReadAsync() => SendAsync(HttpMethod.Post, "api/notifications/read-all");
}
