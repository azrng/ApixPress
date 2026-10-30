-- 项目时间倒序分页的复合索引，支持稳定游标扫描并避免每页全表排序。
CREATE INDEX IF NOT EXISTS ix_request_history_project_timestamp_id
    ON request_history(project_id, timestamp DESC, id DESC);
