namespace TimeBlock.Api.Services;

public static class NpgsqlConnectionString
{
    public static string Normalize(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return connectionString;
        }

        var trimmed = connectionString.Trim();
        if (!trimmed.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase)
            && !trimmed.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
        {
            return connectionString;
        }

        var uri = new Uri(trimmed);
        var parts = new List<string> { $"Host={uri.Host}" };

        if (uri.Port > 0)
        {
            parts.Add($"Port={uri.Port}");
        }

        var database = uri.AbsolutePath.Trim('/');
        if (database.Length > 0)
        {
            parts.Add($"Database={Uri.UnescapeDataString(database)}");
        }

        if (!string.IsNullOrEmpty(uri.UserInfo))
        {
            var separatorIndex = uri.UserInfo.IndexOf(':');
            var username = separatorIndex < 0 ? uri.UserInfo : uri.UserInfo[..separatorIndex];
            parts.Add($"Username={Uri.UnescapeDataString(username)}");
            if (separatorIndex >= 0)
            {
                var password = uri.UserInfo[(separatorIndex + 1)..];
                if (password.Length > 0)
                {
                    parts.Add($"Password={Uri.UnescapeDataString(password)}");
                }
            }
        }

        var query = uri.Query.TrimStart('?');
        if (query.Length > 0)
        {
            foreach (var pair in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                var separatorIndex = pair.IndexOf('=');
                if (separatorIndex <= 0)
                {
                    continue;
                }

                var key = pair[..separatorIndex];
                var value = pair[(separatorIndex + 1)..];
                parts.Add($"{key}={Uri.UnescapeDataString(value)}");
            }
        }

        return string.Join(';', parts);
    }
}
