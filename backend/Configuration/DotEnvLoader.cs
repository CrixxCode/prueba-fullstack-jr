namespace backend.Configuration;

public static class DotEnvLoader
{
    public static void LoadDefaultLocations(string baseDirectory)
    {
        if (string.IsNullOrWhiteSpace(baseDirectory))
        {
            return;
        }

        Load(Path.Combine(baseDirectory, ".env"));

        var parentDirectory = Directory.GetParent(baseDirectory)?.FullName;

        if (!string.IsNullOrWhiteSpace(parentDirectory))
        {
            Load(Path.Combine(parentDirectory, ".env"));
        }
    }

    public static void Load(string path, bool overwrite = false)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return;
        }

        foreach (var rawLine in File.ReadAllLines(path))
        {
            var line = rawLine.Trim();

            if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#'))
            {
                continue;
            }

            var separatorIndex = line.IndexOf('=');

            if (separatorIndex <= 0)
            {
                continue;
            }

            var key = line[..separatorIndex].Trim();

            if (key.StartsWith("export ", StringComparison.OrdinalIgnoreCase))
            {
                key = key["export ".Length..].Trim();
            }

            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            var value = line[(separatorIndex + 1)..].Trim();

            if (value.Length >= 2 &&
                ((value.StartsWith('"') && value.EndsWith('"')) ||
                 (value.StartsWith('\'') && value.EndsWith('\''))))
            {
                value = value[1..^1];
            }

            if (!overwrite && !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(key)))
            {
                continue;
            }

            Environment.SetEnvironmentVariable(key, value);
        }
    }
}
