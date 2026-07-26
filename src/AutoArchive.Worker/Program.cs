using AutoArchive.Core.Abstractions;
using AutoArchive.Core.Classification;
using AutoArchive.Core.Options;
using AutoArchive.Infrastructure.Archive;
using AutoArchive.Infrastructure.FolderIndex;
using AutoArchive.Infrastructure.Mail;
using AutoArchive.Infrastructure.Ollama;
using AutoArchive.Infrastructure.Storage;
using AutoArchive.Infrastructure.TextExtraction;
using AutoArchive.Worker.HealthChecks;
using AutoArchive.Worker.HostedServices;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

// Loop-prevention guard: the new-folder notification must never go back to the mailbox being polled,
// or it would be re-ingested as a new message on the next cycle. Fail fast at startup, not mid-run.
var imapUsername = builder.Configuration[$"{ImapOptions.SectionName}:Username"];
var notificationRecipient = builder.Configuration[$"{NotificationOptions.SectionName}:RecipientEmail"];
if (!string.IsNullOrWhiteSpace(imapUsername) &&
    string.Equals(imapUsername, notificationRecipient, StringComparison.OrdinalIgnoreCase))
{
    throw new InvalidOperationException(
        $"{NotificationOptions.SectionName}:RecipientEmail must not equal {ImapOptions.SectionName}:Username " +
        "(sending the new-folder notification to the polled mailbox would cause it to be re-ingested).");
}

builder.Services.AddOptions<ImapOptions>()
    .Bind(builder.Configuration.GetSection(ImapOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddOptions<SmtpOptions>()
    .Bind(builder.Configuration.GetSection(SmtpOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddOptions<ArchiveOptions>()
    .Bind(builder.Configuration.GetSection(ArchiveOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddOptions<OllamaOptions>()
    .Bind(builder.Configuration.GetSection(OllamaOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddOptions<NotificationOptions>()
    .Bind(builder.Configuration.GetSection(NotificationOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddOptions<StorageOptions>()
    .Bind(builder.Configuration.GetSection(StorageOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddSingleton(TimeProvider.System);

builder.Services.AddHttpClient<IOllamaClient, OllamaHttpClient>()
    .AddStandardResilienceHandler();

builder.Services.AddSingleton<IFolderIndex, FilesystemFolderIndexScanner>();
builder.Services.AddSingleton<IProcessedMessageStore, SqliteProcessedMessageStore>();
builder.Services.AddSingleton<IArchiveWriter, FilesystemArchiveWriter>();
builder.Services.AddSingleton<INotificationService, MailKitSmtpNotificationService>();
builder.Services.AddSingleton<IMailboxClientFactory, MailKitMailboxClientFactory>();
builder.Services.AddSingleton<ITextExtractor, PdfAttachmentTextExtractor>();
builder.Services.AddSingleton<ITextExtractor, DocxAttachmentTextExtractor>();
builder.Services.AddSingleton<ITextExtractor, PlainTextAttachmentExtractor>();
builder.Services.AddSingleton<ClassificationService>();

builder.Services.AddHostedService<FolderIndexRefreshService>();
builder.Services.AddHostedService<MailProcessingService>();

builder.Services.AddHealthChecks()
    .AddCheck<OllamaHealthCheck>("ollama")
    .AddCheck<ArchiveRootHealthCheck>("archive-root");

var app = builder.Build();

// Ensure the SQLite schema and an initial folder-index snapshot exist before either hosted service's loop starts.
using (var scope = app.Services.CreateScope())
{
    await scope.ServiceProvider.GetRequiredService<IProcessedMessageStore>().InitializeAsync(CancellationToken.None);
    await scope.ServiceProvider.GetRequiredService<IFolderIndex>().RefreshAsync(CancellationToken.None);
}

app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready");

await app.RunAsync();
