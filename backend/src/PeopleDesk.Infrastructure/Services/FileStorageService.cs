using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;
using PeopleDesk.Application.Common;

namespace PeopleDesk.Infrastructure.Services;

/// <summary>
/// Disk-backed file storage with path-traversal protection. Keys look like:
/// {orgId}/{folder}/{random}.{ext}. In production, swap for blob storage behind this interface.
/// </summary>
public class FileStorageService(IConfiguration config) : IFileStorage
{
    private string Root => config["Storage:Root"] ?? Path.Combine(AppContext.BaseDirectory, "data", "files");

    private static readonly HashSet<string> AllowedExtensions =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ".pdf", ".png", ".jpg", ".jpeg", ".gif", ".webp", ".doc", ".docx", ".xls", ".xlsx", ".csv", ".txt"
        };

    public async Task<string> SaveAsync(Guid organizationId, string folder, Stream content, string fileExtension, CancellationToken ct = default)
    {
        if (!AllowedExtensions.Contains(fileExtension))
            throw new BadRequestException($"File type '{fileExtension}' is not allowed.");

        folder = Sanitize(folder);
        var name = $"{Guid.CreateVersion7():N}{fileExtension.ToLowerInvariant()}";
        var dir = Path.Combine(Root, organizationId.ToString("N"), folder);
        Directory.CreateDirectory(dir);

        var fullPath = Path.GetFullPath(Path.Combine(dir, name));
        if (!fullPath.StartsWith(Path.GetFullPath(dir), StringComparison.Ordinal))
            throw new BadRequestException("Invalid storage path.");

        await using var fs = File.Create(fullPath);
        await content.CopyToAsync(fs, ct);

        return $"{organizationId:N}/{folder}/{name}";
    }

    public Task<(Stream Content, string ContentType)?> OpenAsync(string storageKey, CancellationToken ct = default)
    {
        var fullPath = Resolve(storageKey);
        if (fullPath is null || !File.Exists(fullPath)) return Task.FromResult<(Stream, string)?>(null);

        var contentType = Path.GetExtension(fullPath).ToLowerInvariant() switch
        {
            ".pdf" => "application/pdf",
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".doc" => "application/msword",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".xls" => "application/vnd.ms-excel",
            ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            ".csv" => "text/csv",
            _ => "application/octet-stream"
        };

        return Task.FromResult<(Stream, string)?>((new FileStream(fullPath, FileMode.Open, FileAccess.Read), contentType));
    }

    public Task DeleteAsync(string storageKey, CancellationToken ct = default)
    {
        var fullPath = Resolve(storageKey);
        if (fullPath is not null && File.Exists(fullPath)) File.Delete(fullPath);
        return Task.CompletedTask;
    }

    private string? Resolve(string storageKey)
    {
        var root = Path.GetFullPath(Root);
        var parts = storageKey.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 3) return null;
        if (parts.Any(p => p.Contains("..") || p.Contains('/') || p.Contains('\\'))) return null;

        var candidate = Path.GetFullPath(Path.Combine(root, parts[0], parts[1], parts[2]));
        return candidate.StartsWith(root, StringComparison.Ordinal) ? candidate : null;
    }

    private static string Sanitize(string folder)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var c in folder.ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(c)) sb.Append(c);
            else if (c is '-' or '_') sb.Append(c);
        }
        return sb.Length == 0 ? "misc" : sb.ToString();
    }
}
