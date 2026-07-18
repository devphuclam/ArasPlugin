using Xunit;

// Several tests share process-wide static state (MainViewModel.SharedUserName,
// MainViewModel.SharedArasCadClient). Disable assembly-wide parallelism so those
// shared fixtures cannot race between test classes.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
