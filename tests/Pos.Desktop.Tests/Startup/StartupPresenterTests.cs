using System.Globalization;
using Pos.Application.Startup;
using Pos.Desktop.Resources;
using Pos.Desktop.Startup;
using Pos.Desktop.Tests.TestSupport;

namespace Pos.Desktop.Tests.Startup;

public class StartupPresenterTests
{
    private readonly FakeDialogService _dialogs = new();
    private readonly CollectingSink _sink = new();

    private StartupPresenter Create(FakeDatabaseStartup startup) =>
        new(startup, _dialogs, new FakeAppPaths(), _sink.CreateLogger());

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Ready_ContinuaSinMensajes()
    {
        var ok = await Create(new FakeDatabaseStartup(new StartupResult.Ready())).RunAsync(Ct);

        Assert.True(ok);
        Assert.Empty(_dialogs.Messages);
    }

    public static TheoryData<StartupResult, string> ErrorResults => new()
    {
        { new StartupResult.NewerDatabase(), Strings.Startup_NewerDatabase },
        { new StartupResult.MigrationFailed(), Strings.Startup_MigrationFailed },
        { new StartupResult.InsufficientSpace(), Strings.Startup_InsufficientSpace },
        { new StartupResult.Inaccessible(DatabaseProblem.Locked), Strings.Startup_Locked },
        { new StartupResult.Inaccessible(DatabaseProblem.DiskFull), Strings.Startup_InsufficientSpace },
        {
            new StartupResult.Inaccessible(DatabaseProblem.PermissionDenied),
            string.Format(CultureInfo.CurrentCulture, Strings.Startup_PermissionDenied, "/datos/Pos")
        },
        { new StartupResult.Corrupted(null), Strings.Startup_CorruptedNoBackup },
    };

    [Theory]
    [MemberData(nameof(ErrorResults))]
    public async Task ResultadoDeError_MuestraSuMensajeYNoContinua(StartupResult result, string expected)
    {
        var ok = await Create(new FakeDatabaseStartup(result)).RunAsync(Ct);

        Assert.False(ok);
        Assert.Equal(expected, Assert.Single(_dialogs.Messages).Message);
    }

    [Fact]
    public async Task Corrupted_ConConfirmacion_RestauraYVuelveAEjecutarElArranque()
    {
        var backup = new BackupInfo("/b/pos.db", BackupKind.Automatic, new DateTime(2026, 9, 28, 20, 0, 0, DateTimeKind.Utc));
        var startup = new FakeDatabaseStartup(new StartupResult.Corrupted(backup), new StartupResult.Ready());
        _dialogs.ConfirmResult = true;

        var ok = await Create(startup).RunAsync(Ct);

        Assert.True(ok);
        Assert.Equal(backup, Assert.Single(startup.Recovered));
        Assert.Equal(2, startup.RunCount);
        var expectedDate = backup.CreatedAtUtc.ToLocalTime().ToString("g", CultureInfo.GetCultureInfo("es-MX"));
        Assert.Contains(expectedDate, Assert.Single(_dialogs.Confirmations), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Corrupted_SinConfirmacion_NoRestauraYNoContinua()
    {
        var backup = new BackupInfo("/b/pos.db", BackupKind.Automatic, DateTime.UtcNow);
        var startup = new FakeDatabaseStartup(new StartupResult.Corrupted(backup));
        _dialogs.ConfirmResult = false;

        var ok = await Create(startup).RunAsync(Ct);

        Assert.False(ok);
        Assert.Empty(startup.Recovered);
        Assert.Equal(Strings.Startup_ContactSupport, Assert.Single(_dialogs.Messages).Message);
    }

    [Fact]
    public async Task ExcepcionInesperada_SeRegistraYMuestraMensajeGenerico()
    {
        var startup = new FakeDatabaseStartup(new InvalidOperationException("falla"));

        var ok = await Create(startup).RunAsync(Ct);

        Assert.False(ok);
        Assert.Equal(Strings.Common_UnexpectedError, Assert.Single(_dialogs.Messages).Message);
        Assert.Contains(_sink.Events, e => e.Exception is InvalidOperationException);
    }
}

public sealed class FakeDatabaseStartup : IDatabaseStartup
{
    private readonly Queue<StartupResult> _results;
    private readonly Exception? _exception;

    public FakeDatabaseStartup(params StartupResult[] results) => _results = new Queue<StartupResult>(results);

    public FakeDatabaseStartup(Exception exception)
    {
        _results = new Queue<StartupResult>();
        _exception = exception;
    }

    public int RunCount { get; private set; }

    public List<BackupInfo> Recovered { get; } = [];

    public Task<StartupResult> RunAsync(CancellationToken cancellationToken)
    {
        RunCount++;
        return _exception is null ? Task.FromResult(_results.Dequeue()) : Task.FromException<StartupResult>(_exception);
    }

    public Task RecoverFromBackupAsync(BackupInfo backup, CancellationToken cancellationToken)
    {
        Recovered.Add(backup);
        return Task.CompletedTask;
    }

    public Task BackupOnCloseAsync() => Task.CompletedTask;
}
