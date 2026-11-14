using TimeBlock.Core.Contracts;
using TimeBlock.Core.Entities;

namespace TimeBlock.Api.Services;

public static class UserMapper
{
    public static UserDto ToUserDto(this User user) =>
        new(user.Id, user.Email, user.DisplayName, user.DailyCapacityHours, user.AccountabilityOptIn);
}
