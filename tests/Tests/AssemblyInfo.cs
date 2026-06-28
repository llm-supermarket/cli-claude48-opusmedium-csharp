using Xunit;

// CLI tests mutate process-global environment variables, so run serially.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
