using Xunit;

// 测试中多处依赖 Avalonia Dispatcher（线程亲和），并行执行会放大时序敏感的
// 集合/线程竞态（CloseInterface 等）。禁用并行后总时长仍在数秒内。
[assembly: CollectionBehavior(DisableTestParallelization = true)]
