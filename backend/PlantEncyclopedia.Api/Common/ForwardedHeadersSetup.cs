using System.Net;
using Microsoft.AspNetCore.HttpOverrides;

namespace PlantEncyclopedia.Api.Common;

/// <summary>Settings section "ForwardedHeaders" (design 2026-10-27). Off unless turned on.</summary>
public sealed class TrustedProxySettings
{
    public const string SectionName = "ForwardedHeaders";

    /// <summary>Apply X-Forwarded-Proto and X-Forwarded-For from the proxy networks below.</summary>
    public bool Enabled { get; init; }

    /// <summary>The proxy networks in CIDR form, e.g. "172.30.0.0/16". Headers from any other address are ignored.</summary>
    public string[] KnownNetworks { get; init; } = [];
}

/// <summary>
/// Behind a reverse proxy (the website's nginx in Docker) the API sees plain HTTP from the proxy's address. When
/// turned on, the scheme and client address the proxy forwards are applied, but only for requests coming from a
/// configured proxy network, so a client can never claim HTTPS or another address by sending the headers itself.
/// </summary>
public static class ForwardedHeadersSetup
{
    public static IServiceCollection AddTrustedProxies(this IServiceCollection services, IConfiguration configuration)
    {
        var settings = configuration.GetSection(TrustedProxySettings.SectionName).Get<TrustedProxySettings>()
            ?? new TrustedProxySettings();
        if (!settings.Enabled) return services;

        // Parsed at startup, so a typo stops the app instead of silently trusting nobody.
        var networks = settings.KnownNetworks.Select(ParseNetwork).ToList();
        if (networks.Count == 0)
        {
            throw new InvalidOperationException("ForwardedHeaders is enabled but ForwardedHeaders:KnownNetworks is empty.");
        }

        services.AddSingleton(settings);
        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            options.ForwardLimit = 1;
            options.KnownProxies.Clear();
            options.KnownIPNetworks.Clear();
            foreach (var network in networks) options.KnownIPNetworks.Add(network);
        });
        return services;
    }

    /// <summary>Applies the forwarded headers when turned on. Must run before anything that reads the scheme or address.</summary>
    public static IApplicationBuilder UseTrustedProxies(this IApplicationBuilder app) =>
        app.ApplicationServices.GetService<TrustedProxySettings>() is null ? app : app.UseForwardedHeaders();

    private static System.Net.IPNetwork ParseNetwork(string value) =>
        System.Net.IPNetwork.TryParse(value, out var network)
            ? network
            : throw new InvalidOperationException($"ForwardedHeaders:KnownNetworks has an invalid network '{value}' (expected CIDR, e.g. 172.30.0.0/16).");
}
