using System.Diagnostics;
using System.Net.Sockets;
using Microsoft.Data.SqlClient;
using Testcontainers.MsSql;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace testing_strategies.Infrastructure;

/// <summary>
/// One SQL Server container for the whole test run, started before the first SqlServer_ test and removed
/// after the last. Each test then creates its own database inside it.
/// </summary>
public sealed class SqlServerContainerFixture(IMessageSink messages) : IAsyncLifetime
{
    // The same image as the repo's sql2025 container. Pull it before a talk so the first run doesn't download it:
    //   docker pull mcr.microsoft.com/mssql/server:2025-latest
    public const string Image = "mcr.microsoft.com/mssql/server:2025-latest";

    private MsSqlContainer? _container;

    public string ConnectionStringFor(string database) =>
        new SqlConnectionStringBuilder(_container!.GetConnectionString()) { InitialCatalog = database }.ConnectionString;

    public async Task InitializeAsync()
    {
        if (!Docker.IsAvailable) return; // the SqlServer_ facts skip themselves

        var stopwatch = Stopwatch.StartNew();
        _container = new MsSqlBuilder(Image).Build();
        await _container.StartAsync();
        messages.OnMessage(new DiagnosticMessage(
            $"SQL Server container ({Image}) started in {stopwatch.Elapsed.TotalSeconds:0.0} s"));
    }

    public async Task DisposeAsync()
    {
        if (_container is not null) await _container.DisposeAsync();
    }
}

[CollectionDefinition(Name)]
public sealed class SqlServerCollection : ICollectionFixture<SqlServerContainerFixture>
{
    public const string Name = "SQL Server container";
}

/// <summary>A fact that is skipped (not failed) when there is no Docker engine to start the container in.</summary>
public sealed class SqlServerContainerFactAttribute : FactAttribute
{
    public SqlServerContainerFactAttribute()
    {
        if (!Docker.IsAvailable)
        {
            Skip = "Docker is not reachable, so the Testcontainers SQL Server can't start.";
        }
    }
}

internal static class Docker
{
    private static readonly Lazy<bool> Reachable = new(() =>
    {
        var host = Environment.GetEnvironmentVariable("DOCKER_HOST");
        if (!string.IsNullOrEmpty(host) && !host.StartsWith("unix://", StringComparison.Ordinal))
        {
            return true; // tcp/npipe: let Testcontainers find out
        }

        var candidates = new List<string>();
        if (!string.IsNullOrEmpty(host)) candidates.Add(host["unix://".Length..]);
        candidates.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".docker/run/docker.sock"));
        candidates.Add("/var/run/docker.sock");

        foreach (var path in candidates.Where(File.Exists))
        {
            try
            {
                using var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
                socket.Connect(new UnixDomainSocketEndPoint(path));
                return true;
            }
            catch (SocketException)
            {
            }
        }

        return false;
    });

    public static bool IsAvailable => Reachable.Value;
}
