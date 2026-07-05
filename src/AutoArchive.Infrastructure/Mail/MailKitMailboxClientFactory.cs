using AutoArchive.Core.Abstractions;
using AutoArchive.Core.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AutoArchive.Infrastructure.Mail;

public sealed class MailKitMailboxClientFactory(
    IOptions<ImapOptions> options,
    IEnumerable<ITextExtractor> textExtractors,
    ILoggerFactory loggerFactory) : IMailboxClientFactory
{
    public IMailboxClient Create() =>
        new MailKitMailboxClient(options, textExtractors, loggerFactory.CreateLogger<MailKitMailboxClient>());
}
