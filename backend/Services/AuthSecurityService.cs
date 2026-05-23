namespace backend.Services;

public sealed class AuthSecurityService
{
    private readonly IConfiguration _configuration;

    public AuthSecurityService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public int MaxFailedLoginAttempts => GetIntSetting(
        "Security:MaxFailedLoginAttempts",
        defaultValue: 5,
        min: 1,
        max: 20
    );

    public TimeSpan LockoutDuration => TimeSpan.FromMinutes(GetIntSetting(
        "Security:LockoutMinutes",
        defaultValue: 15,
        min: 1,
        max: 1440
    ));

    public string PasswordPolicySummary =>
        $"La contrasena debe tener al menos {GetPasswordMinLength()} caracteres, una mayuscula, una minuscula, un numero y un simbolo.";

    public IReadOnlyCollection<string> ValidatePassword(string password)
    {
        var errors = new List<string>();
        var minLength = GetPasswordMinLength();

        if (string.IsNullOrEmpty(password))
        {
            errors.Add("La contrasena es obligatoria.");
            return errors;
        }

        if (password.Length < minLength)
        {
            errors.Add($"La contrasena debe tener minimo {minLength} caracteres.");
        }

        if (!password.Any(char.IsUpper))
        {
            errors.Add("La contrasena debe incluir al menos una letra mayuscula.");
        }

        if (!password.Any(char.IsLower))
        {
            errors.Add("La contrasena debe incluir al menos una letra minuscula.");
        }

        if (!password.Any(char.IsDigit))
        {
            errors.Add("La contrasena debe incluir al menos un numero.");
        }

        if (!password.Any(ch => !char.IsLetterOrDigit(ch)))
        {
            errors.Add("La contrasena debe incluir al menos un simbolo.");
        }

        return errors;
    }

    private int GetPasswordMinLength()
    {
        return GetIntSetting(
            "Security:PasswordMinLength",
            defaultValue: 8,
            min: 8,
            max: 128
        );
    }

    private int GetIntSetting(string key, int defaultValue, int min, int max)
    {
        var raw = _configuration[key];

        if (!int.TryParse(raw, out var value))
        {
            return defaultValue;
        }

        if (value < min || value > max)
        {
            return defaultValue;
        }

        return value;
    }
}
