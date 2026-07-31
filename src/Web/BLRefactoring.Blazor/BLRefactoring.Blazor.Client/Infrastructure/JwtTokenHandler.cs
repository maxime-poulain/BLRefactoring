using System.Net.Http.Headers;

namespace BLRefactoring.Blazor.Client.Infrastructure;

public class JwtTokenHandler(IJwtTokenService jwtTokenService) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        // No guard against prerendering here: it is off by design, so this handler only ever
        // runs once the application is interactive and JavaScript interop is available.
        // Swallowing InvalidOperationException would now hide genuine interop failures.
        var token = await jwtTokenService.GetTokenAsync();

        if (!string.IsNullOrEmpty(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
