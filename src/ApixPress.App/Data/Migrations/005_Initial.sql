-- 历史面板按项目取最近记录（where project_id = @ProjectId order by timestamp desc limit @Limit），
-- 单列 timestamp 索引无法命中 project_id 过滤；补复合索引避免多项目下近乎全表扫描。重复执行无副作用
CREATE INDEX IF NOT EXISTS ix_request_history_project_timestamp ON request_history(project_id, timestamp DESC);
