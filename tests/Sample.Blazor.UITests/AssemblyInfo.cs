// Pages share one served app but each test gets its own browser context, so tests may run in parallel.
// The limit keeps the WASM runtime boots from starving each other on a laptop.
[assembly: ParallelLimiter<Sample.Blazor.UITests.BrowserParallelism>]
