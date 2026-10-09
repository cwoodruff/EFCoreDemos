using System;
using System.IO;

namespace savedchanges_interception_auditing;

// Both databases are scratch files owned by this demo: Program.CreateDatabases()
// deletes and recreates them on every run, so they must never share a name with
// the Chinook sample database used by the other demos.
internal static class DatabasePaths
{
    public static string BlogsConnectionString =>
        $"Data Source={Path.Combine(AppContext.BaseDirectory, "auditing-blogs.db")}";

    public static string AuditConnectionString =>
        $"Data Source={Path.Combine(AppContext.BaseDirectory, "audit.db")}";
}
