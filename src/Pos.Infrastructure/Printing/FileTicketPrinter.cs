using System.Text;
using Microsoft.Extensions.Logging;
using Pos.Application.Abstractions;
using Pos.Application.Printing;
using Pos.Application.Printing.Ticket;

namespace Pos.Infrastructure.Printing;

/// <summary>
/// Impresora virtual (006, research §11): guarda el ticket como <c>tickets/&lt;fecha&gt;-&lt;folio&gt;.txt</c>
/// con el mismo texto que saldría en papel.
/// </summary>
public sealed partial class FileTicketPrinter
{
    public const string LogoPlaceholder = "[LOGOTIPO]";

    private readonly IAppPaths _paths;
    private readonly IClock _clock;
    private readonly ILogger<FileTicketPrinter> _logger;

    public FileTicketPrinter(IAppPaths paths, IClock clock, ILogger<FileTicketPrinter> logger)
    {
        _paths = paths;
        _clock = clock;
        _logger = logger;
    }

    public static string Render(TicketDocument ticket)
    {
        ArgumentNullException.ThrowIfNull(ticket);

        var builder = new StringBuilder();
        if (ticket.Logo is { Length: > 0 })
        {
            builder.AppendLine(TextWrap.Align(LogoPlaceholder, ticket.Columns, TicketAlignment.Center));
        }

        foreach (var line in ticket.Lines)
        {
            builder.AppendLine(TextWrap.Align(line.Text, ticket.Columns, line.Alignment).TrimEnd());
        }

        return builder.ToString();
    }

    public async Task<PrintOutcome> PrintAsync(TicketDocument ticket, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ticket);

        try
        {
            Directory.CreateDirectory(_paths.TicketsDirectory);
            var stamp = TimeZoneInfo.ConvertTimeFromUtc(_clock.UtcNow, TimeZoneInfo.Local).ToString("yyyyMMdd-HHmmss", System.Globalization.CultureInfo.InvariantCulture);
            var baseName = $"{stamp}-{SanitizeFolio(ticket.Folio)}";
            var text = Render(ticket);

            for (var attempt = 1; ; attempt++)
            {
                var path = Path.Combine(_paths.TicketsDirectory, attempt == 1 ? $"{baseName}.txt" : $"{baseName}-{attempt}.txt");
                try
                {
                    await using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                    await stream.WriteAsync(new UTF8Encoding(false).GetBytes(text), cancellationToken);
                    return PrintOutcome.Ok(path);
                }
                catch (IOException) when (File.Exists(path))
                {
                    // Mismo nombre en el mismo segundo (por ejemplo, una reimpresión): se numera.
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            LogFailed(ex, _paths.TicketsDirectory);
            return PrintOutcome.Fail(DeviceFailure.IoError, _paths.TicketsDirectory);
        }
    }

    internal static string SanitizeFolio(string? folio)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var clean = new string([.. (folio ?? string.Empty).Select(c => invalid.Contains(c) || char.IsWhiteSpace(c) ? '_' : c)]);
        return clean.Length == 0 ? "ticket" : clean;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "No se pudo guardar el ticket de la impresora virtual en {Directory}")]
    private partial void LogFailed(Exception exception, string directory);
}
