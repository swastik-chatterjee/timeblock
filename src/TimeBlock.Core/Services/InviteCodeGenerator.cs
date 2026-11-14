using System.Security.Cryptography;

namespace TimeBlock.Core.Services;

public static class InviteCodeGenerator
{
    public static string NewCode()
    {
        return Convert.ToHexString(RandomNumberGenerator.GetBytes(4));
    }
}
