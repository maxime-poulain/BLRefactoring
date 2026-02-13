using System.Net.Http.Headers;

namespace BLRefactoring.Blazor.Client.Infrastructure;

public class JwtTokenHandler(IJwtTokenService jwtTokenService) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        try
        {
            var token = await jwtTokenService.GetTokenAsync();

            if (!string.IsNullOrEmpty(token))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }
        }
        catch (InvalidOperationException)
        {
            // JavaScript interop is not available during server-side prerendering
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
