using Npgsql;

namespace SchoolAdmission.Api.Data;

/// <summary>
/// Resolves the PostgreSQL connection string from configuration / environment.
/// Accepts either the conventional .NET key (ConnectionStrings__Default) or a single
/// DATABASE_URL which may be a key=value Npgsql string or a postgres:// URL.
/// </summary>
public static class ConnectionStrings
{
    public static string Resolve(IConfiguration configuration)
    {
        var raw = configuration["DATABASE_URL"];
        if (string.IsNullOrWhiteSpace(raw))
        {
            raw = configuration.GetConnectionString("Default");
        }
        if (string.IsNullOrWhiteSpace(raw))
        {
            throw new InvalidOperationException(
                "No database connection configured. Set DATABASE_URL (postgres://user:pass@host:5432/db) " +
                "or ConnectionStrings__Default.");
        }
        return Normalize(raw);
    }

    /// <summary>
    /// Default pool cap when the connection string does not set one. PostgreSQL ships with
    /// max_connections = 100; Npgsql's default of 100 per process would let a single busy backend
    /// (or two backends sharing the database) exhaust it. Operators can still override it explicitly.
    /// </summary>
    public const int DefaultMaxPoolSize = 40;

    public static string Normalize(string raw)
    {
        raw = raw.Trim();
        if (!raw.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) &&
            !raw.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
        {
            return ApplyPoolDefault(new NpgsqlConnectionStringBuilder(raw));
        }

        var uri = new Uri(raw);
        var userInfo = uri.UserInfo.Split(':', 2);
        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.IsDefaultPort ? 5432 : uri.Port,
            Database = uri.AbsolutePath.Trim('/'),
            Username = Uri.UnescapeDataString(userInfo[0]),
            Password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : null,
        };

        // Allow ?sslmode=...&timeout=... style extras.
        var query = uri.Query.TrimStart('?');
        if (!string.IsNullOrEmpty(query))
        {
            foreach (var pair in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                var kv = pair.Split('=', 2);
                if (kv.Length == 2)
                {
                    builder[Uri.UnescapeDataString(kv[0])] = Uri.UnescapeDataString(kv[1]);
                }
            }
        }
        return ApplyPoolDefault(builder);
    }

    private static string ApplyPoolDefault(NpgsqlConnectionStringBuilder builder)
    {
        if (!builder.ContainsKey("Maximum Pool Size") && !builder.ContainsKey("Max Pool Size"))
        {
            builder.MaxPoolSize = DefaultMaxPoolSize;
        }
        return builder.ConnectionString;
    }
}
