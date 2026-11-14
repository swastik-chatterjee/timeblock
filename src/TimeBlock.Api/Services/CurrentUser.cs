using System.Security.Claims;

namespace TimeBlock.Api.Services;

public class CurrentUser(IHttpContextAccessor accessor)
{
    public Guid UserId
    {
        get
        {
            var value = accessor.HttpContext?.User.FindFirstValue("sub");
            return Guid.TryParse(value, out var id) ? id : throw new UnauthorizedAccessException();
        }
    }
}
