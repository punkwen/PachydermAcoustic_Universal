using System.Reflection;
using System.Runtime.ExceptionServices;
using Pachyderm_Acoustic;
using Pachyderm_Acoustic.Numeric.TimeDomain;
using Pachyderm_Acoustic.Utilities;

int failures = 0;
await Check("pre-cancel never starts simulation", async () =>
{
    using var cts = new CancellationTokenSource();
    cts.Cancel();
    var simulation = new FakeSimulation();
    await ExpectCancellation(() => Run(simulation, cts.Token));
    Assert(simulation.Starts == 0, "pre-cancel started a simulation");
});
await Check("Esc stops all later stages and sources", async () =>
{
    using var cts = new CancellationTokenSource();
    int starts = 0, completedRuns = 0;
    var first = new FakeSimulation { Started = () => { starts++; cts.Cancel(); } };
    try
    {
        for (int source = 0; source < 2; source++)
        {
            await Run(source == 0 ? first : new FakeSimulation { Started = () => starts++ }, cts.Token);
            await Run(new FakeSimulation { Started = () => starts++ }, cts.Token);
            completedRuns++;
        }
    }
    catch (OperationCanceledException) { }
    Assert(starts == 1, $"Esc allowed {starts} stages to start");
    Assert(first.Combinations == 0, "cancelled partial results were combined");
    Assert(completedRuns == 0, "cancelled source was committed");
});
await Check("Esc stops inside FDTD time steps", () =>
{
    using var cts = new CancellationTokenSource();
    var simulation = new Acoustic_Compact_FDTD { Step = () => cts.Cancel() };
    bool cancelled = false;
    try { RunFdtd(simulation, cts.Token); }
    catch (OperationCanceledException) { cancelled = true; }
    Assert(cancelled, "FDTD reported success after cancellation");
    Assert(simulation.Steps == 1, $"FDTD continued to {simulation.Steps} steps");
    return Task.CompletedTask;
});
return failures == 0 ? 0 : 1;

async Task Check(string name, Func<Task> test)
{
    try { await test(); Console.WriteLine($"PASS {name}"); }
    catch (Exception error) { failures++; Console.WriteLine($"FAIL {name}: {error.Message}"); }
}
static void Assert(bool condition, string message)
{
    if (!condition) throw new Exception(message);
}
static async Task ExpectCancellation(Func<Task> action)
{
    try { await action(); }
    catch (OperationCanceledException) { return; }
    throw new Exception("expected cancellation, but computation completed");
}
static Task Run(Simulation_Type simulation, CancellationToken token)
{
    var method = typeof(RCPachTools).GetMethod("RunSimulation");
    object[] arguments = method.GetParameters().Length == 2
        ? new object[] { simulation, false }
        : new object[] { simulation, false, token };
    return (Task)method.Invoke(null, arguments);
}
static void RunFdtd(Acoustic_Compact_FDTD simulation, CancellationToken token)
{
    var method = typeof(Acoustic_Compact_FDTD).GetMethod("RuntoCompletion");
    try { method.Invoke(simulation, method.GetParameters().Length == 0 ? null : new object[] { token }); }
    catch (TargetInvocationException error) { ExceptionDispatchInfo.Capture(error.InnerException).Throw(); }
}

sealed class FakeSimulation : Simulation_Type
{
    public int Starts, Combinations;
    public Action Started;
    public override void Begin() { Starts++; Started?.Invoke(); }
    public override string Sim_Type() => "test";
    public override string ProgressMsg() => "test";
    public override System.Threading.ThreadState ThreadState() => System.Threading.ThreadState.Stopped;
    public override void Combine_ThreadLocal_Results() => Combinations++;
}

// Only the host and work units are fake; the runner and FDTD loop are linked production files.
namespace Pachyderm_Acoustic.Numeric.TimeDomain
{
    public partial class Acoustic_Compact_FDTD
    {
        public double tmax = 1000, dt = 0.01;
        double time_ms;
        public int Steps;
        public Action Step;
        public double Increment() { Steps++; Step?.Invoke(); return 0; }
    }
}
namespace Pachyderm_Acoustic.Environment { public class Scene { } }
namespace Pachyderm_Acoustic.UI
{
    public class PachydermAc_PlugIn
    {
        public static PachydermAc_PlugIn Instance = new();
        public int TaskPriority = 2;
    }
}
namespace Rhino
{
    public static class RhinoApp { public static void SetCommandPrompt(string message) { } }
    public class RhinoDoc
    {
        public static RhinoDoc ActiveDoc = new();
        public ViewTable Views = new();
    }
    public class ViewTable { public bool RedrawEnabled = true; }
}
namespace Rhino.ApplicationSettings
{
    public static class FileSettings { public static bool AutoSaveEnabled = true; }
}
