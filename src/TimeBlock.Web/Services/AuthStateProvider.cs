using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.JSInterop;

namespace TimeBlock.Web.Services;

public class AuthStateProvider(TokenStorage storage, ApiClient api) : AuthenticationStateProvider
{
    private ClaimsPrincipal _user = new(new ClaimsIdentity());
    private string? _token;
    private bool _initialized;

    public string? Token => _token;

    public override Task<AuthenticationState> GetAuthenticationStateAsync() =>
        Task.FromResult(new AuthenticationState(_user));

    public async Task InitializeAsync()
    {
        if (_initialized)
        {
            return;
        }

        _initialized = true;
        try
        {
            _token = await storage.GetAsync();
        }
        catch (JSException)
        {
            _token = null;
        }

        if (_token is not null)
        {
            _user = CreatePrincipal(_token);
            Notify();
        }
    }

    public async Task<bool> LoginAsync(string email, string password)
    {
        var response = await api.LoginAsync(new LoginRequest(email, password));
        if (response is null)
        {
            return false;
        }

        await SetTokenAsync(response.Token);
        return true;
    }

    public async Task<bool> RegisterAsync(string email, string displayName, string password, string? inviteCode)
    {
        var response = await api.RegisterAsync(new RegisterRequest(email, displayName, password, inviteCode));
        if (response is null)
        {
            return false;
        }

        await SetTokenAsync(response.Token);
        return true;
    }

    public async Task LogoutAsync()
    {
        _token = null;
        _user = new ClaimsPrincipal(new ClaimsIdentity());
        await storage.ClearAsync();
        Notify();
    }

    private async Task SetTokenAsync(string token)
    {
        _token = token;
        _user = CreatePrincipal(token);
        await storage.SetAsync(token);
        Notify();
    }

    private void Notify() =>
        NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(_user)));

    private static ClaimsPrincipal CreatePrincipal(string token)
    {
        var payload = DecodePayload(token);
        if (payload is null)
        {
            return new ClaimsPrincipal(new ClaimsIdentity());
        }

        var claims = payload.Value.EnumerateObject()
            .Where(p => p.Value.ValueKind == JsonValueKind.String)
            .Select(p => new Claim(p.Name, p.Value.GetString() ?? ""))
            .ToList();

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "Jwt"));
    }

    private static JsonElement? DecodePayload(string token)
    {
        var parts = token.Split('.');
        if (parts.Length != 3)
        {
            return null;
        }

        try
        {
            var padded = parts[1].Replace('-', '+').Replace('_', '/');
            padded = padded.PadRight(padded.Length + ((4 - padded.Length % 4) % 4), '=');
            var json = Encoding.UTF8.GetString(Convert.FromBase64String(padded));
            return JsonDocument.Parse(json).RootElement.Clone();
        }
        catch (FormatException)
        {
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
