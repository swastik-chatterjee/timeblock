namespace TimeBlock.Api.Services;

public static class EnvLoader
{
    public static void Load(string? path = null)
    {
        path ??= Path.Combine(Directory.GetCurrentDirectory(), ".env");
        if (!File.Exists(path))
        {
            return;
        }

        foreach (var rawLine in File.ReadAllLines(path))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            line = line.StartsWith("export ", StringComparison.Ordinal) ? line["export ".Length..].Trim() : line;
            var separator = line.IndexOf('=');
            if (separator <= 0)
            {
                continue;
            }

            var key = line[..separator].Trim();
            var value = line[(separator + 1)..].Trim();
            if (value.Length >= 2 && ((value.StartsWith('"') && value.EndsWith('"')) || (value.StartsWith('\'') && value.EndsWith('\''))))
            {
                value = value[1..^1];
            }

            if (!string.IsNullOrWhiteSpace(key) && Environment.GetEnvironmentVariable(key) is null)
            {
                Environment.SetEnvironmentVariable(key, value);
            }
        }
    }
}
