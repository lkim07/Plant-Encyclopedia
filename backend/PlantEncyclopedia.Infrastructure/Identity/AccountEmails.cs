using System.Globalization;
using System.Text;

namespace PlantEncyclopedia.Infrastructure.Identity;

/// <summary>Account emails: address confirmation and password reset (D-070).</summary>
public interface IAccountEmailSender
{
    Task SendEmailConfirmationAsync(string email, string displayName, Uri confirmationLink, CancellationToken cancellationToken);

    Task SendPasswordResetAsync(string email, Uri resetLink, CancellationToken cancellationToken);
}

/// <summary>
/// Development and test stand-in for an email service: each email is written as a text file to a local folder
/// (git-ignored App_Data/emails in Development) instead of being sent. Links are never logged. Other environments
/// must configure a real email service before release (TODO).
/// </summary>
public sealed class FileAccountEmailSender(string folder, TimeProvider timeProvider) : IAccountEmailSender
{
    public string Folder { get; } = Path.GetFullPath(folder);

    public Task SendEmailConfirmationAsync(string email, string displayName, Uri confirmationLink, CancellationToken cancellationToken) =>
        WriteAsync("confirm-email", email, "Confirm your Plant Encyclopedia email address",
            $"Hello {displayName},\n\nConfirm your email address to save plants to your Favorites:\n\n{confirmationLink}\n\n" +
            "If you did not create an account, you can ignore this email.\n", cancellationToken);

    public Task SendPasswordResetAsync(string email, Uri resetLink, CancellationToken cancellationToken) =>
        WriteAsync("reset-password", email, "Reset your Plant Encyclopedia password",
            $"Hello,\n\nUse this link to choose a new password. It expires in 3 hours:\n\n{resetLink}\n\n" +
            "If you did not ask for this, you can ignore this email; your password stays the same.\n", cancellationToken);

    private async Task WriteAsync(string kind, string to, string subject, string body, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Folder);
        var stamp = timeProvider.GetUtcNow().ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture);
        var file = Path.Combine(Folder, $"{stamp}-{kind}-{Guid.NewGuid():N}.txt");
        var text = new StringBuilder()
            .Append("To: ").Append(to).Append('\n')
            .Append("Subject: ").Append(subject).Append("\n\n")
            .Append(body)
            .ToString();
        await File.WriteAllTextAsync(file, text, cancellationToken);
    }
}
