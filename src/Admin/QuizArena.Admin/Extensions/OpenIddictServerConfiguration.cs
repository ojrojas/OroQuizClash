using OpenIddict.Client;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace QuizArena.Admin.Extensions;

public static class OpenIddictServerConfiguration
{
    public static TBuilder AddIdentityServerOpenIddict<TBuilder>(this TBuilder builder, string identityAuthority, string webClientId) where TBuilder : IHostApplicationBuilder
    {
        builder.Services.AddOpenIddict()
            .AddClient(options =>
            {
                options.AllowAuthorizationCodeFlow()
                       .AllowRefreshTokenFlow();

                options.DisableTokenStorage();

                options.AddDevelopmentEncryptionCertificate()
                       .AddDevelopmentSigningCertificate();

                options.AddRegistration(new OpenIddictClientRegistration
                {
                    RegistrationId = "OroIdentityServer",
                    ProviderName = "OroIdentityServer",
                    ProviderDisplayName = "OroIdentityServer",
                    Issuer = new Uri(identityAuthority, UriKind.Absolute),
                    ClientId = webClientId,
                    ClientType = ClientTypes.Public,
                    GrantTypes = { GrantTypes.AuthorizationCode, GrantTypes.RefreshToken },
                    ResponseTypes = { ResponseTypes.Code },
                    Scopes = { Scopes.OpenId, Scopes.Email, Scopes.Profile, Scopes.Roles, Scopes.OfflineAccess, "admin" },
                    CodeChallengeMethods = { CodeChallengeMethods.Sha256 },
                    RedirectUri = new Uri("callback", UriKind.Relative),
                    PostLogoutRedirectUri = new Uri("logout-callback", UriKind.Relative)
                });

                options.UseSystemNetHttp()
                       .SetProductInformation(typeof(Program).Assembly);

                options.UseAspNetCore()
                       .EnableRedirectionEndpointPassthrough()
                       .EnablePostLogoutRedirectionEndpointPassthrough()
                       .EnableErrorPassthrough()
                       .DisableTransportSecurityRequirement();
            });

        return builder;
    }
}
