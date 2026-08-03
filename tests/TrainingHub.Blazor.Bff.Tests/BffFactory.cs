using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Yarp.ReverseProxy.Forwarder;

namespace TrainingHub.Blazor.Bff.Tests;

/// <summary>
/// Hosts the real BFF — the actual <c>Program.cs</c>, its pipeline order included — with the API
/// replaced by a handler that records what it was sent.
/// </summary>
/// <remarks>
/// Nothing about the security behaviour is stubbed: cookie authentication, the forgery guard, the
/// authorization on the proxied route and the token transform are the production ones. Only the far
/// side of the proxy is fake, because a test cannot assert on a credential it never sees.
/// <para>
/// The environment is <c>Development</c>, so the host's own <c>appsettings.Development.json</c>
/// supplies <c>Api:BaseAddress</c>. Its value is never dialled: both the login client and the proxy
/// are pointed at the handlers below.
/// </para>
/// </remarks>
public sealed class BffFactory : WebApplicationFactory<Program>
{
    /// <summary>The API as the BFF's own sign-in endpoint reaches it.</summary>
    public RecordingHandler LoginApi { get; } = new();

    /// <summary>The API as the proxy reaches it.</summary>
    public RecordingHandler ProxiedApi { get; } = new();

    /// <summary>
    /// Configure web host.
    /// </summary>
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureServices(services =>
        {
            services
                .AddHttpClient(BffEndpoints.ApiClientName)
                .ConfigurePrimaryHttpMessageHandler(() => LoginApi);

            services.AddSingleton<IForwarderHttpClientFactory>(
                new StubForwarderHttpClientFactory(ProxiedApi));
        });
    }

    /// <summary>
    /// A client that behaves like the browser: keeps cookies, follows no redirect, speaks HTTPS.
    /// </summary>
    /// <remarks>
    /// HTTPS is not decoration. The session cookie is <c>Secure</c> with the <c>__Host-</c> prefix,
    /// so over <c>http</c> the cookie container would drop it and every test would fail as though
    /// sign-in had not worked — which is exactly what happens to a developer who runs the
    /// application over plain HTTP, and why the <c>http</c> launch profile no longer exists.
    /// </remarks>
    public HttpClient CreateBrowser() => CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://localhost"),
        AllowAutoRedirect = false,
        HandleCookies = true
    });

    /// <summary>
    /// An access token shaped like the one the API issues — <see cref="ClaimTypes.Name"/> included,
    /// which the serializer shortens to <c>unique_name</c> on the way out.
    /// </summary>
    public static string IssueToken(string username = "alice", TimeSpan? lifetime = null)
    {
        var token = new JwtSecurityToken(
            issuer: "tests",
            audience: "tests",
            claims:
            [
                new Claim(ClaimTypes.Name, username),
                new Claim("trainer_id", "9d1f7f2e-0e4a-4a1e-9f5f-2b3c4d5e6f70")
            ],
            expires: DateTime.UtcNow.Add(lifetime ?? TimeSpan.FromMinutes(30)));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
