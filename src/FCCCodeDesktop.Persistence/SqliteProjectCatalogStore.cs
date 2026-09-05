using System.Globalization;
using FCCCodeDesktop.Application.Projects;
using FCCCodeDesktop.Core.State;
using Microsoft.Data.Sqlite;

namespace FCCCodeDesktop.Persistence;

public sealed class SqliteProjectCatalogStore : IProjectCatalogStore
{
    private readonly SqliteDatabaseOptions _options;

    public SqliteProjectCatalogStore(SqliteDatabaseOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
    }

    public async Task<PersistedProject?> FindProjectByRootPathAsync(
        string rootPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
        var normalizedRootPath = Path.GetFullPath(rootPath);

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT Id, RootPath, DisplayName, CreatedUtc, UpdatedUtc
            FROM Projects
            WHERE RootPath = $rootPath COLLATE NOCASE
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("$rootPath", normalizedRootPath);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false)
            ? ReadProject(reader)
            : null;
    }

    public async Task<IReadOnlyList<PersistedProject>> ListRecentProjectsAsync(
        int maximumCount,
        CancellationToken cancellationToken = default)
    {
        if (maximumCount < 1 || maximumCount > ProjectCatalogService.MaximumRecentProjectCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumCount),
                maximumCount,
                $"Recent project count must be between 1 and {ProjectCatalogService.MaximumRecentProjectCount}.");
        }

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT Id, RootPath, DisplayName, CreatedUtc, UpdatedUtc
            FROM Projects
            ORDER BY UpdatedUtc DESC, DisplayName COLLATE NOCASE ASC, Id ASC
            LIMIT $maximumCount;
            """;
        command.Parameters.AddWithValue("$maximumCount", maximumCount);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var projects = new List<PersistedProject>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            projects.Add(ReadProject(reader));
        }

        return projects.AsReadOnly();
    }

    public async Task UpsertProjectAsync(
        PersistedProject project,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(project);
        ValidateProject(project);

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO Projects (Id, RootPath, DisplayName, CreatedUtc, UpdatedUtc)
            VALUES ($id, $rootPath, $displayName, $createdUtc, $updatedUtc)
            ON CONFLICT(Id) DO UPDATE SET
                RootPath = excluded.RootPath,
                DisplayName = excluded.DisplayName,
                UpdatedUtc = excluded.UpdatedUtc;
            """;
        command.Parameters.AddWithValue("$id", project.Id.ToString("D", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$rootPath", Path.GetFullPath(project.RootPath));
        command.Parameters.AddWithValue("$displayName", project.DisplayName.Trim());
        command.Parameters.AddWithValue("$createdUtc", FormatTimestamp(project.CreatedUtc));
        command.Parameters.AddWithValue("$updatedUtc", FormatTimestamp(project.UpdatedUtc));

        try
        {
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (SqliteException exception)
        {
            throw new InvalidOperationException("SQLite could not persist project catalog metadata.", exception);
        }
    }

    private async Task<SqliteConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = _options.DatabasePath,
            Mode = SqliteOpenMode.ReadWrite,
            Pooling = false,
        }.ToString();

        var connection = new SqliteConnection(connectionString);
        try
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            await using (var foreignKeysCommand = connection.CreateCommand())
            {
                foreignKeysCommand.CommandText = "PRAGMA foreign_keys = ON;";
                await foreignKeysCommand.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            var busyTimeoutMilliseconds = checked((int)_options.BusyTimeout.TotalMilliseconds);
            await using (var timeoutCommand = connection.CreateCommand())
            {
                timeoutCommand.CommandText = $"PRAGMA busy_timeout = {busyTimeoutMilliseconds.ToString(CultureInfo.InvariantCulture)};";
                await timeoutCommand.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            return connection;
        }
        catch
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    private static PersistedProject ReadProject(SqliteDataReader reader) =>
        new(
            Guid.Parse(reader.GetString(0)),
            reader.GetString(1),
            reader.GetString(2),
            ParseTimestamp(reader.GetString(3)),
            ParseTimestamp(reader.GetString(4)));

    private static string FormatTimestamp(DateTimeOffset value) =>
        value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);

    private static DateTimeOffset ParseTimestamp(string value) =>
        DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

    private static void ValidateProject(PersistedProject project)
    {
        if (project.Id == Guid.Empty)
        {
            throw new ArgumentException("Project identifier must not be empty.", nameof(project));
        }
        if (string.IsNullOrWhiteSpace(project.RootPath))
        {
            throw new ArgumentException("Project root path is required.", nameof(project));
        }
        if (string.IsNullOrWhiteSpace(project.DisplayName))
        {
            throw new ArgumentException("Project display name is required.", nameof(project));
        }
        if (project.UpdatedUtc < project.CreatedUtc)
        {
            throw new ArgumentException("Project UpdatedUtc cannot precede CreatedUtc.", nameof(project));
        }
    }
}
