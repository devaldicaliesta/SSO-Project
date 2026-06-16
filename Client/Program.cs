using Client;
using Client.Services;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// =========================================================================
// BFF CLIENT SETUP
//
// The client no longer performs the OIDC flow and never handles tokens.
// It talks to its own origin (the BFF) using the HttpOnly session cookie,
// which the browser attaches automatically. Every request carries the
// BFF antiforgery header via AntiforgeryHandler.
// =========================================================================
builder.Services.AddTransient<AntiforgeryHandler>();

builder.Services.AddHttpClient("ServerAPI", client =>
        client.BaseAddress = new Uri(builder.HostEnvironment.BaseAddress))
    .AddHttpMessageHandler<AntiforgeryHandler>();

builder.Services.AddScoped(sp => sp.GetRequiredService<IHttpClientFactory>()
    .CreateClient("ServerAPI"));

// Authorization primitives + a BFF-backed authentication state (reads /bff/user).
builder.Services.AddAuthorizationCore();
builder.Services.AddScoped<BffAuthenticationStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(sp =>
    sp.GetRequiredService<BffAuthenticationStateProvider>());

await builder.Build().RunAsync();
