CREATE TABLE IF NOT EXISTS project_http_settings (
    project_id TEXT PRIMARY KEY,
    auth_mode TEXT NOT NULL DEFAULT 'none',
    bearer_token TEXT NOT NULL DEFAULT '',
    updated_at TEXT NOT NULL,
    FOREIGN KEY(project_id) REFERENCES projects(id) ON DELETE CASCADE
);
