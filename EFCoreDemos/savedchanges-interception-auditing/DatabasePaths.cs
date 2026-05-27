using System;
using System.IO;

namespace savedchanges_interception_auditing;

internal static class DatabasePaths
{
    public static string BlogsConnectionString =>
        $"Data Source={Path.Combine(AppContext.BaseDirectory, "chinook.db")}";

    public static string AuditConnectionString =>
        $"Data Source={Path.Combine(AppContext.BaseDirectory, "audit.db")}";
}
