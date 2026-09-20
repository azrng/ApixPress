-- 用例列表按项目过滤后按类型、目录和更新时间排序，避免大项目出现额外临时排序。
CREATE INDEX IF NOT EXISTS ix_request_cases_project_listing
ON request_cases(project_id, entry_type, folder_path, updated_at DESC);
