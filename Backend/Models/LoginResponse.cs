namespace Backend.Models;

public class LoginResponse
{
    public string AccessToken { get; set; } = string.Empty;
    public int ExpiresInSeconds { get; set; }
    public UserInfo User { get; set; } = null!;
}

public class UserInfo
{
    public string Name { get; set; } = string.Empty;
    public IReadOnlyList<string> Roles { get; set; } = Array.Empty<string>();
}
