using AutoArchive.Core.Abstractions;
using AutoArchive.Core.Options;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace AutoArchive.Infrastructure.Mail;

/// <summary>Sends the message-filed notification via SMTP. Always sent to
/// Notifications:RecipientEmail, never to the polled mailbox - enforced separately at startup validation.</summary>
public sealed class MailKitSmtpNotificationService(
    IOptions<SmtpOptions> smtpOptions,
    IOptions<NotificationOptions> notificationOptions) : INotificationService
{
    public async Task SendMessageFiledNotificationAsync(
        string messageSubject,
        string folderRelativePath,
        bool isNewFolder,
        string reasoning,
        CancellationToken cancellationToken)
    {
        var smtp = smtpOptions.Value;

        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse(smtp.FromAddress));
        message.To.Add(MailboxAddress.Parse(notificationOptions.Value.RecipientEmail));
        message.Subject = $"AutoArchive filed: {messageSubject}";

        var newFolderNote = isNewFolder
            ? "\nThis is a new folder - no existing folder matched, so you may want to review/rename it or edit its information.md.\n"
            : string.Empty;

        message.Body = new TextPart("plain")
        {
            Text = $"""
                AutoArchive successfully filed an email.

                Subject: {messageSubject}
                Folder: {folderRelativePath}
                Reasoning: {reasoning}
                {newFolderNote}
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
