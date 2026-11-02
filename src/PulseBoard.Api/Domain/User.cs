namespace PulseBoard.Api.Domain;

public static class Roles
{
    public const string Viewer = "Viewer";
    public const string Operator = "Operator";
    public const string Admin = "Admin";
}

public sealed class User
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Role { get; set; } = Roles.Viewer;
}
