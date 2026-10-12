using Microsoft.EntityFrameworkCore.Diagnostics;

namespace connection_resiliency.Faults;

/// <summary>
/// The two fault injectors, created once and registered on every context. Reusing the same interceptor instances
/// matters: EF builds (and caches) an internal service provider per distinct set of interceptor instances.
/// </summary>
public sealed class FaultInjection
{
    public CommandFaultInjector Commands { get; } = new();
    public CommitAckFaultInjector Commits { get; } = new();

    public IInterceptor[] Interceptors => [Commands, Commits];

    public Action<string>? Log
    {
        set
        {
            Commands.Log = value;
            Commits.Log = value;
        }
    }

    public void Disarm()
    {
        Commands.Disarm();
        Commits.Disarm();
    }
}
