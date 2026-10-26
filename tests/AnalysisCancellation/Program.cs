using RayTraceTests;

// Shared deterministic-assertion harness (ticket 01). Register one line per suite here;
// suite logic lives in its own file so parallel tickets don't edit the same code.
RayTraceDeterminismSuite.Register();
FilterContractSuite.Register();

return await Harness.RunAll();
