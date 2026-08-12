using System.Text;

namespace SportsPlatform.Api.Services;

public sealed class CsvUploadValidator
{
    public const long MaximumBytes = 2 * 1024 * 1024;
    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "text/csv", "application/csv", "application/vnd.ms-excel", "text/plain"
    };
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    public async Task<MemoryStream> ValidateAsync(
        IFormFile? file,
        IReadOnlyCollection<string> requiredHeaders,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
            throw new CsvUploadValidationException("No CSV file was uploaded.");
        if (file.Length > MaximumBytes)
            throw new CsvUploadValidationException("CSV files cannot exceed 2 MB.");
        if (file.FileName.IndexOfAny(new[] { '/', '\\' }) >= 0
            || !string.Equals(Path.GetExtension(file.FileName), ".csv", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(Path.GetFileName(file.FileName), file.FileName, StringComparison.Ordinal))
            throw new CsvUploadValidationException("The upload must use a safe .csv filename.");
        if (string.IsNullOrWhiteSpace(file.ContentType) || !AllowedContentTypes.Contains(file.ContentType))
            throw new CsvUploadValidationException("The upload content type is not an allowed CSV type.");

        var stream = new MemoryStream((int)file.Length);
        await file.CopyToAsync(stream, cancellationToken);
        if (stream.Length > MaximumBytes)
            throw new CsvUploadValidationException("CSV files cannot exceed 2 MB.");
        var bytes = stream.ToArray();
        if (bytes.Contains((byte)0))
            throw new CsvUploadValidationException("Binary or null-byte content is not allowed in CSV uploads.");

        string text;
        try { text = StrictUtf8.GetString(bytes); }
        catch (DecoderFallbackException)
        { throw new CsvUploadValidationException("CSV uploads must use valid UTF-8 text."); }
        var lines = text.Split('\n');
        if (lines.Length > 10_001)
            throw new CsvUploadValidationException("CSV uploads cannot contain more than 10,000 data rows.");
        if (lines.Any(line => line.Length > 32_768))
            throw new CsvUploadValidationException("A CSV row exceeds the 32 KB safety limit.");
        var headers = lines[0].TrimStart('\uFEFF').TrimEnd('\r')
            .Split(',').Select(value => value.Trim().Trim('"')).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (requiredHeaders.Any(required => !headers.Contains(required)))
            throw new CsvUploadValidationException($"CSV header must include: {string.Join(", ", requiredHeaders)}.");

        stream.Position = 0;
        return stream;
    }
}

public sealed class CsvUploadValidationException : Exception
{
    public CsvUploadValidationException(string message) : base(message) { }
}
