using Microsoft.AspNetCore.SignalR.Client;
using TimeBlock.Core.Contracts;

namespace TimeBlock.Web.Services;

public class TimelineClient(AuthStateProvider auth) : IAsyncDisposable
{
    private HubConnection? _connection;

    public event Action<BlockChangedEvent>? BlockChanged;

    public bool IsConnected => _connection?.State == HubConnectionState.Connected;

    public async Task EnsureStartedAsync()
    {
        await auth.InitializeAsync();
        if (_connection is not null || auth.Token is null)
        {
            return;
        }

        var token = auth.Token;
        _connection = new HubConnectionBuilder()
            .WithUrl("hubs/timeline", options => options.AccessTokenProvider = () => Task.FromResult<string?>(token))
            .WithAutomaticReconnect()
            .Build();

        _connection.On<BlockChangedEvent>("blockChanged", e => BlockChanged?.Invoke(e));
        try
        {
            await _connection.StartAsync();
        }
        catch (Exception)
        {
            _connection = null;
        }
    }

    public async Task SubscribeAsync(IReadOnlyList<Guid> groupIds)
    {
        if (_connection is null || groupIds.Count == 0)
        {
            return;
        }

        try
        {
            await _connection.InvokeAsync("Subscribe", groupIds);
        }
        catch (Exception)
        {
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection is not null)
        {
            await _connection.DisposeAsync();
            _connection = null;
        }
    }
}
