namespace PulseBoard.Api.Auth;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "pulseboard";
    public string Audience { get; set; } = "pulseboard";

    /// <summary>HMAC signing key. Must be at least 32 bytes. Override via configuration in any real deployment.</summary>
    public string Key { get; set; } = string.Empty;

    public int LifetimeMinutes { get; set; } = 480;
}
