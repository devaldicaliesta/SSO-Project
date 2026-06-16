namespace Client.Services;

/// <summary>
/// Duende.BFF protects the local API and the /bff/user endpoint with an
/// antiforgery requirement: every call from the SPA must carry a static
/// custom header. Browsers only send custom headers same-origin (and after a
/// CORS preflight cross-origin), so this header proves the request did not come
/// from a cross-site forgery.
/// </summary>
public class AntiforgeryHandler : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        request.Headers.Add("X-CSRF", "1");
        return base.SendAsync(request, cancellationToken);
    }
}
