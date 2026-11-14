using Microsoft.JSInterop;

namespace TimeBlock.Web.Services;

public class TokenStorage(IJSRuntime js)
{
    public ValueTask<string?> GetAsync() => js.InvokeAsync<string?>("timeblock.getToken");
    public ValueTask SetAsync(string token) => js.InvokeVoidAsync("timeblock.setToken", token);
    public ValueTask ClearAsync() => js.InvokeVoidAsync("timeblock.clearToken");
}
