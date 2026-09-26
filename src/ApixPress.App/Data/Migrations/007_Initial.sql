-- 列由 DatabaseInitializer 的兼容升级逻辑补齐，索引在迁移中登记以便版本可追踪。
CREATE INDEX IF NOT EXISTS ix_request_cases_source
ON request_cases(source_project_id, source_id);

CREATE INDEX IF NOT EXISTS ix_project_environments_source
ON project_environments(source_project_id, source_id);
