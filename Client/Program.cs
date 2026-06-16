using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.RootComponents.Add<Client.App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// HttpClient that automatically attaches the OIDC access token when calling the
// hosting server's API (BaseAddressAuthorizationMessageHandler reads the token
// acquired during login). Used by the dashboard to call GET /api/me.
builder.Services.AddHttpClient("ServerAPI", client =>
        client.BaseAddress = new Uri(builder.HostEnvironment.BaseAddress))
    .AddHttpMessageHandler<BaseAddressAuthorizationMessageHandler>();

builder.Services.AddScoped(sp => sp.GetRequiredService<IHttpClientFactory>()
    .CreateClient("ServerAPI"));

// =========================================================================
// OIDC AUTHENTICATION (Authorization Code Flow + PKCE)
//
// All provider values are read from wwwroot/appsettings.json -> "Oidc".
// The Client is a PUBLIC client: PKCE only, NEVER a client_secret.
//
// TODO: To switch to a production provider (Azure AD / Keycloak), only edit
//       wwwroot/appsettings.json (Authority, ClientId, RedirectUri, scopes).
//       No code changes are required here.
// =========================================================================
builder.Services.AddOidcAuthentication(options =>
{
    builder.Configuration.Bind("Oidc", options.ProviderOptions);

    // The library seeds DefaultScopes with {openid, profile}; binding the config
    // array appends to that list, so de-duplicate to keep the request clean.
    var scopes = options.ProviderOptions.DefaultScopes.Distinct().ToList();
    options.ProviderOptions.DefaultScopes.Clear();
    foreach (var scope in scopes)
    {
        options.ProviderOptions.DefaultScopes.Add(scope);
    }

    // Map the claim types issued by the IdP so the user's display name and role
    // resolve correctly on the client (and [Authorize(Roles = ...)] works).
    options.UserOptions.NameClaim = "name";
    options.UserOptions.RoleClaim = "role";
});

await builder.Build().RunAsync();
