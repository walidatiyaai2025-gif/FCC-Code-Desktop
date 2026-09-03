namespace FCCCodeDesktop.Application.Persistence;

public sealed record DatabaseIntegrityReport(
    bool IsHealthy,
    IReadOnlyList<string> Messages);

public sealed record DatabaseBackupArtifact(
    string BackupPath,
    DateTimeOffset CreatedUtc);

public interface IDatabaseMaintenanceService
{
    Task<DatabaseIntegrityReport> CheckIntegrityAsync(
        CancellationToken cancellationToken = default);

    Task<DatabaseBackupArtifact> CreateBackupAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DatabaseBackupArtifact>> ListBackupsAsync(
        CancellationToken cancellationToken = default);
}
