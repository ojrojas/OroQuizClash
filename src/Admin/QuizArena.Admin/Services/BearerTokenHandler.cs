using System.Net.Http.Headers;
using Microsoft.AspNetCore.Authentication;

namespace QuizArena.Admin.Services;

/// <summary>
/// Attaches the OIDC cookie's access_token as a Bearer header to outgoing HttpClient requests
/// made by the Server*Service implementations (InteractiveServer render mode). The token is
/// resolved per-request from the ambient HttpContext — it never leaves the server.
/// </summary>
public sealed class BearerTokenHandler(IHttpContextAccessor httpContextAccessor) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var httpContext = httpContextAccessor.HttpContext;
        if (httpContext is not null && request.Headers.Authorization is null)
        {
            var accessToken = await httpContext.GetTokenAsync("access_token");
            if (!string.IsNullOrEmpty(accessToken))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            }
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
