using QuizArena.Admin.Services;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Server-side (InteractiveServer) service registrations for the admin BFF.
/// Server*Service implementations call QuizArena.Api directly via Aspire service discovery
/// (http://oroclash-api) attaching the operator's access_token from the OIDC cookie.
/// Typed client registrations are added per user story as Server*Services are implemented.
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAdminServerServices(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddTransient<BearerTokenHandler>();
        return services;
    }

    /// <summary>
    /// Registers a Server*Service typed HttpClient with the API base address and Bearer handler.
    /// </summary>
    public static IHttpClientBuilder AddAdminApiHttpClient<TClient, TImplementation>(this IServiceCollection services)
        where TClient : class
        where TImplementation : class, TClient =>
        services.AddHttpClient<TClient, TImplementation>(httpClient =>
        {
            httpClient.BaseAddress = new Uri(BffForwarderExtensions.ApiServiceName);
        }).AddHttpMessageHandler<BearerTokenHandler>();
}
