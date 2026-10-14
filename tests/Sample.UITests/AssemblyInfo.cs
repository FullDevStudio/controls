// One app, one UI: tests drive it strictly one at a time.
[assembly: NotInParallel]

// Hard cap per test. Every wait in the suite is bounded, but a process stuck in a GC storm (Android's GREF
// limit: "N outstanding GREFs. Performing a full GC!" every ~0.5s) answers each agent call so slowly that one
// test once ran for 8+ minutes. Cut it off here; the next test's PrepareApp sees the GREF warning
// (AndroidAppHost.NeedsFreshProcessAsync) or a dead agent and relaunches. The slowest healthy test takes ~1 min.
[assembly: Timeout(5 * 60 * 1000)]
