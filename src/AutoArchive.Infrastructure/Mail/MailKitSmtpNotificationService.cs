using AutoArchive.Core.Abstractions;
using AutoArchive.Core.Options;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace AutoArchive.Infrastructure.Mail;

/// <summary>Sends the new-folder-created notification via SMTP. Always sent to
/// Notifications:RecipientEmail, never to the polled mailbox - enforced separately at startup validation.</summary>
public sealed class MailKitSmtpNotificationService(
    IOptions<SmtpOptions> smtpOptions,
    IOptions<NotificationOptions> notificationOptions) : INotificationService
{
    public async Task SendNewFolderCreatedNotificationAsync(string folderRelativePath, string reasoning, CancellationToken cancellationToken)
    {
        var smtp = smtpOptions.Value;

        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse(smtp.FromAddress));
        message.To.Add(MailboxAddress.Parse(notificationOptions.Value.RecipientEmail));
        message.Subject = $"AutoArchive created a new folder: {folderRelativePath}";
        message.Body = new TextPart("plain")
        {
            Text = $"""
                AutoArchive created a new folder because no existing folder matched an incoming email.

                Folder: {folderRelativePath}
                Reasoning: {reasoning}

                You may want to review/rename it or edit its information.md.
                """,
        };

        using var client = new SmtpClient();
        var socketOptions = smtp.UseStartTls ? SecureSocketOptions.StartTls : SecureSocketOptions.Auto;
        await client.ConnectAsync(smtp.Host, smtp.Port, socketOptions, cancellationToken);
        await client.AuthenticateAsync(smtp.Username, smtp.Password, cancellationToken);
        await client.SendAsync(message, cancellationToken);
        await client.DisconnectAsync(quit: true, cancellationToken);
    }
}
