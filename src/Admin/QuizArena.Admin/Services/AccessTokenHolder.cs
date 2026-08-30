namespace QuizArena.Admin.Services;

/// <summary>
/// Scoped holder for the current user's access_token.
/// Captured once per HTTP request (via middleware) and reused for the Blazor circuit
/// where HttpContext is not available (InteractiveServer render mode).
/// EduCoreWeb BFF pattern: token is stored in cookie + Redis, but the circuit needs it.
/// </summary>
public sealed class AccessTokenHolder
{
    public string? Token { get; set; }
    public string? Sid { get; set; }
}
