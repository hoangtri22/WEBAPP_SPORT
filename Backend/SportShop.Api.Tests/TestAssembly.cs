using Xunit;

// Integration fixtures insert records; catalog counts must see a stable database snapshot between tests.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
