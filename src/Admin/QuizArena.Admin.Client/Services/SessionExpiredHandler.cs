using System.Net;
using Microsoft.AspNetCore.Components;

namespace QuizArena.Admin.Client.Services;

/// <summary>
/// Client-side (WASM) 401 interceptor (FR-005): when the session cookie is no longer valid
/// the BFF answers 401; the operator is sent back to the OIDC challenge with a return URL.
/// Server-side (InteractiveServer) the standard OIDC challenge applies automatically.
/// </summary>
public sealed class SessionExpiredHandler(NavigationManager navigation) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = await base.SendAsync(request, cancellationToken);

        if (response.StatusCode == HttpStatusCode.Unauthorized &&
            !navigation.Uri.Contains("/authentication/", StringComparison.OrdinalIgnoreCase))
        {
            var returnUrl = Uri.EscapeDataString(navigation.Uri);
            navigation.NavigateTo($"authentication/login?returnUrl={returnUrl}", forceLoad: true);
        }

        return response;
    }
}
