using Xunit;

// Fixture tích hợp có ghi dữ liệu nên chạy tuần tự để số liệu catalog ổn định.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
