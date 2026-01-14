using Duende.IdentityServer;
using Duende.IdentityServer.Models;

namespace IdentityServer;

public static class Config
{
    public static IEnumerable<IdentityResource> IdentityResources =>
        new IdentityResource[]
        {
            new IdentityResources.OpenId(),
            new IdentityResources.Profile(),
            new IdentityResources.Email(),
        };

    public static IEnumerable<ApiScope> ApiScopes =>
        new ApiScope[]
        {
            new ApiScope("api1", "My API"),
            new ApiScope("firebase", "Firebase API"),
        };

    public static IEnumerable<Client> Clients =>
        new Client[]
        {
            // MVC Client using Authorization Code Flow
            new Client
            {
                ClientId = "mvc-client",
                ClientName = "MVC Client Application",
                ClientSecrets = { new Secret("mvc-client-secret".Sha256()) },

                AllowedGrantTypes = GrantTypes.Code,
                RequirePkce = true,

                // Where to redirect after login
                RedirectUris = { "https://localhost:5002/signin-oidc" },

                // Where to redirect after logout
                PostLogoutRedirectUris = { "https://localhost:5002/signout-callback-oidc" },

                AllowedScopes = new List<string>
                {
                    IdentityServerConstants.StandardScopes.OpenId,
                    IdentityServerConstants.StandardScopes.Profile,
                    IdentityServerConstants.StandardScopes.Email,
                    "api1"
                },

                AllowOfflineAccess = true,
            },

            // Firebase/SPA Client using Authorization Code Flow with PKCE
            new Client
            {
                ClientId = "firebase-client",
                ClientName = "Firebase Application",
                RequireClientSecret = false, // Public client (SPA)

                AllowedGrantTypes = GrantTypes.Code,
                RequirePkce = true,

                RedirectUris =
                {
                    "http://localhost:5002/firebase/callback.html",
                    "http://localhost:3000/callback.html",
                    "https://localhost:5002/firebase/callback.html",
                    "https://localhost:5002/api/firebase-auth/callback",
                },

                PostLogoutRedirectUris =
                {
                    "http://localhost:5002/firebase/index.html",
                    "http://localhost:3000/index.html",
                    "https://localhost:5002/firebase/index.html",
                },

                AllowedCorsOrigins =
                {
                    "http://localhost:5002",
                    "http://localhost:3000",
                    "https://localhost:5002",
                },

                AllowedScopes = new List<string>
                {
                    IdentityServerConstants.StandardScopes.OpenId,
                    IdentityServerConstants.StandardScopes.Profile,
                    IdentityServerConstants.StandardScopes.Email,
                    "firebase"
                },

                AllowOfflineAccess = true,
            }
        };
}
