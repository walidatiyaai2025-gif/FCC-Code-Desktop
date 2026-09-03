using System.Security.Cryptography;
using System.Text;

namespace FCCCodeDesktop.Persistence;

public sealed class SqliteMigration
{
    public SqliteMigration(int version, string name, string sql)
    {
        if (version <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(version), version, "Migration version must be positive.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);

        Version = version;
        Name = name.Trim();
        Sql = NormalizeSql(sql);
        Checksum = ComputeChecksum(Sql);
    }

    public int Version { get; }

    public string Name { get; }

    public string Sql { get; }

    public string Checksum { get; }

    private static string NormalizeSql(string sql) =>
        sql.Replace("\r\n", "\n", StringComparison.Ordinal).Trim();

    private static string ComputeChecksum(string sql) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sql)));
}
