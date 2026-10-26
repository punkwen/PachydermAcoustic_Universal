using RayTraceTests;

// Shared deterministic-assertion harness (ticket 01). Register one line per suite here;
// suite logic lives in its own file so parallel tickets don't edit the same code.
// PoolInitSuite runs first so T1 exercises cold (first-ever) pool access.
PoolInitSuite.Register();
RayTraceDeterminismSuite.Register();
RadAccumulationSuite.Register();
FilterContractSuite.Register();
ConvergenceLifecycleSuite.Register();

return await Harness.RunAll();
