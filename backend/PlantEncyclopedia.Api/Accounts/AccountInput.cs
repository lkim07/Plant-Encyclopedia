using System.Net.Mail;
using System.Text.Json;
using Microsoft.AspNetCore.Http.Features;
using PlantEncyclopedia.Api.Errors;
using PlantEncyclopedia.Infrastructure.Identity;

namespace PlantEncyclopedia.Api.Accounts;

public sealed record RegisterRequest(string? Email, string? Password, string? DisplayName);

public sealed record LoginRequest(string? Email, string? Password);

public sealed record ForgotPasswordRequest(string? Email);

public sealed record ResetPasswordRequest(string? Email, string? Token, string? Password);

/// <summary>Reading and checking account request bodies. Passwords are never logged or echoed back.</summary>
public static class AccountInput
{
    public const int MaxBodyBytes = 4 * 1024;
    public const int MaxEmailLength = 256;

    /// <summary>The JSON body, or the documented error for a missing, malformed or oversized body.</summary>
    public static async Task<(T? Body, IResult? Error)> ReadJsonAsync<T>(HttpRequest request, CancellationToken cancellationToken)
        where T : class
    {
        if (request.ContentLength > MaxBodyBytes) return (null, ApiErrors.PayloadTooLarge());
        if (request.HttpContext.Features.Get<IHttpMaxRequestBodySizeFeature>() is { IsReadOnly: false } size)
        {
            size.MaxRequestBodySize = MaxBodyBytes;
        }

        if (!request.HasJsonContentType()) return (null, ApiErrors.Validation("body", "Request body must be JSON."));

        try
        {
            var body = await request.ReadFromJsonAsync<T>(cancellationToken);
            return body is null ? (null, ApiErrors.Validation("body", "Request body must be valid JSON.")) : (body, null);
        }
        catch (JsonException)
        {
            return (null, ApiErrors.Validation("body", "Request body must be valid JSON."));
        }
        catch (BadHttpRequestException e) when (e.StatusCode == StatusCodes.Status413PayloadTooLarge)
        {
            return (null, ApiErrors.PayloadTooLarge());
        }
    }

    /// <summary>A trimmed email address, or an error message.</summary>
    public static (string? Email, string? Error) Email(string? value)
    {
        var email = value?.Trim();
        if (string.IsNullOrEmpty(email)) return (null, "Enter your email address.");
        if (email.Length > MaxEmailLength || email.Any(char.IsControl) || email.Contains(' ')
            || !MailAddress.TryCreate(email, out var parsed) || parsed.Address != email)
        {
            return (null, "Enter a valid email address.");
        }

        return (email, null);
    }

    /// <summary>A trimmed display name of 1–50 characters without control characters, or an error message.</summary>
    public static (string? Name, string? Error) DisplayName(string? value)
    {
        var name = value?.Trim();
        if (string.IsNullOrEmpty(name)) return (null, "Enter your name.");
        if (name.Length > ApplicationUser.DisplayNameMaxLength) return (null, $"Use at most {ApplicationUser.DisplayNameMaxLength} characters.");
        if (name.Any(char.IsControl)) return (null, "Use letters, numbers and punctuation only.");
        return (name, null);
    }

    /// <summary>A new password of 8–128 characters (Identity checks it again). Spaces are allowed and kept.</summary>
    public static string? NewPassword(string? value) => value switch
    {
        null or "" => "Enter a password.",
        { Length: < AccountSetup.MinPasswordLength } => $"Use at least {AccountSetup.MinPasswordLength} characters.",
        { Length: > AccountSetup.MaxPasswordLength } => $"Use at most {AccountSetup.MaxPasswordLength} characters.",
        _ => null
    };
}
