using System.Net;
using Microsoft.Extensions.Hosting;
using System.Net.Mail;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PeopleDesk.Domain.Common;
using PeopleDesk.Domain.Notifications;

namespace PeopleDesk.Infrastructure.BackgroundJobs;

public class EmailOptions
{
    public const string SectionName = "Email";
    public string Mode { get; set; } = "FileSink";   // "FileSink" (dev) or "Smtp"
    public string SinkPath { get; set; } = "logs/dev-emails";
    public string Host { get; set; } = "";
    public int Port { get; set; } = 587;
    public bool UseSsl { get; set; } = true;
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public string FromName { get; set; } = "PeopleDesk";
    public string FromAddress { get; set; } = "no-reply@peopledesk.local";
}

/// <summary>
/// Transactional outbox dispatcher. Polls EmailOutbox for pending rows and sends via SMTP
/// (production) or writes .eml files to a sink (development, so the app runs without SMTP).
/// API requests never block on email delivery.
/// </summary>
public class EmailDispatcherService(
    IServiceScopeFactory scopeFactory,
    IOptions<EmailOptions> options,
    ILogger<EmailDispatcherService> logger) : BackgroundService
{
    private readonly EmailOptions _opts = options.Value;
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(30);
    private static readonly int MaxAttempts = 5;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Email dispatcher started (mode: {Mode}).", _opts.Mode);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await DispatchPendingAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Email dispatcher cycle failed.");
            }

            await Task.Delay(PollInterval, stoppingToken);
        }
    }

    private async Task DispatchPendingAsync(CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PeopleDesk.Application.Common.IAppDbContext>();

        var pending = await db.EmailOutbox
            .Where(e => e.Status == EmailOutboxStatus.Pending && e.Attempts < MaxAttempts)
            .OrderBy(e => e.CreatedAtUtc)
            .Take(20)
            .ToListAsync(ct);

        foreach (var email in pending)
        {
            email.Attempts++;
            try
            {
                await SendAsync(email, ct);
                email.Status = EmailOutboxStatus.Sent;
                email.SentAtUtc = DateTime.UtcNow;
                logger.LogInformation("Email sent to {To}: {Subject}", email.ToEmail, email.Subject);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                email.LastError = ex.Message[..Math.Min(ex.Message.Length, 500)];
                email.Status = email.Attempts >= MaxAttempts ? EmailOutboxStatus.Failed : EmailOutboxStatus.Pending;
                logger.LogWarning(ex, "Email to {To} failed (attempt {Attempt}).", email.ToEmail, email.Attempts);
            }
        }

        await db.SaveChangesAsync(ct);
    }

    private async Task SendAsync(EmailOutbox email, CancellationToken ct)
    {
        if (_opts.Mode.Equals("Smtp", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(_opts.Host))
        {
            using var client = new SmtpClient(_opts.Host, _opts.Port)
            {
                EnableSsl = _opts.UseSsl,
                Credentials = string.IsNullOrWhiteSpace(_opts.Username)
                    ? CredentialCache.DefaultNetworkCredentials
                    : new NetworkCredential(_opts.Username, _opts.Password)
            };
            using var message = new MailMessage(_opts.FromAddress, email.ToEmail, email.Subject, email.BodyHtml)
            {
                IsBodyHtml = true,
                From = new MailAddress(_opts.FromAddress, _opts.FromName)
            };
            await client.SendMailAsync(message, ct);
        }
        else
        {
            // Development sink: write an .eml file so flows are testable without SMTP.
            var dir = Path.IsPathRooted(_opts.SinkPath) ? _opts.SinkPath : Path.Combine(AppContext.BaseDirectory, _opts.SinkPath);
            Directory.CreateDirectory(dir);
            var safeSubject = string.Join('_', email.Subject.Split(Path.GetInvalidFileNameChars()));
            var fileName = $"{DateTime.UtcNow:yyyyMMdd-HHmmss-fff}_{safeSubject[..Math.Min(safeSubject.Length, 60)]}.eml";
            var path = Path.Combine(dir, fileName);

            await File.WriteAllTextAsync(path,
                $"""
                From: {_opts.FromName} <{_opts.FromAddress}>
                To: {email.ToEmail}
                Subject: {email.Subject}
                Date: {DateTime.UtcNow:R}
                Content-Type: text/html; charset=utf-8

                {email.BodyHtml}
                """, ct);
        }
    }
}
