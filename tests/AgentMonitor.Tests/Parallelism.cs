// The installer tests mutate the CLAUDE_CONFIG_DIR environment variable, which is
// process-global, so disable cross-test parallelism.
[assembly: Xunit.CollectionBehavior(DisableTestParallelization = true)]
