-- T063 项目级导出偏好：四项备份范围、最近格式与最近输出目录
CREATE TABLE IF NOT EXISTS project_export_settings (
    project_id TEXT PRIMARY KEY,
    format TEXT NOT NULL DEFAULT 'backup',
    include_interfaces INTEGER NOT NULL DEFAULT 1,
    include_request_cases INTEGER NOT NULL DEFAULT 1,
    include_environments INTEGER NOT NULL DEFAULT 1,
    include_request_history INTEGER NOT NULL DEFAULT 0,
    last_output_directory TEXT NOT NULL DEFAULT '',
    updated_at TEXT NOT NULL,
    FOREIGN KEY(project_id) REFERENCES projects(id) ON DELETE CASCADE
);
