CREATE TABLE IF NOT EXISTS projects (
    id TEXT PRIMARY KEY,
    name TEXT NOT NULL,
    description TEXT NOT NULL DEFAULT '',
    is_default INTEGER NOT NULL DEFAULT 0,
    created_at TEXT NOT NULL,
    updated_at TEXT NOT NULL
);

CREATE UNIQUE INDEX IF NOT EXISTS ux_projects_name
ON projects(name);

CREATE UNIQUE INDEX IF NOT EXISTS ux_projects_default
ON projects(is_default)
WHERE is_default = 1;

CREATE TABLE IF NOT EXISTS project_environments (
    id TEXT PRIMARY KEY,
    project_id TEXT NOT NULL,
    name TEXT NOT NULL,
    base_url TEXT NOT NULL DEFAULT '',
    is_active INTEGER NOT NULL DEFAULT 0,
    sort_order INTEGER NOT NULL DEFAULT 0,
    created_at TEXT NOT NULL,
    updated_at TEXT NOT NULL,
    FOREIGN KEY(project_id) REFERENCES projects(id) ON DELETE CASCADE
);

CREATE UNIQUE INDEX IF NOT EXISTS ux_project_environments_project_name
ON project_environments(project_id, name);

CREATE UNIQUE INDEX IF NOT EXISTS ux_project_environments_project_active
ON project_environments(project_id, is_active)
WHERE is_active = 1;

CREATE TABLE IF NOT EXISTS api_documents (
    id TEXT PRIMARY KEY,
    project_id TEXT NOT NULL,
    name TEXT NOT NULL,
    source_type TEXT NOT NULL,
    source_value TEXT NOT NULL,
    base_url TEXT NOT NULL DEFAULT '',
    raw_json TEXT NOT NULL,
    imported_at TEXT NOT NULL,
    FOREIGN KEY(project_id) REFERENCES projects(id) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS api_endpoints (
    id TEXT PRIMARY KEY,
    document_id TEXT NOT NULL,
    group_name TEXT NOT NULL,
    name TEXT NOT NULL,
    method TEXT NOT NULL,
    path TEXT NOT NULL,
    description TEXT NOT NULL DEFAULT '',
    request_body_template TEXT NOT NULL DEFAULT '',
    FOREIGN KEY(document_id) REFERENCES api_documents(id) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS request_parameters (
    id TEXT PRIMARY KEY,
    endpoint_id TEXT NOT NULL,
    parameter_type TEXT NOT NULL,
    name TEXT NOT NULL,
    default_value TEXT NOT NULL DEFAULT '',
    description TEXT NOT NULL DEFAULT '',
    required INTEGER NOT NULL DEFAULT 0,
    FOREIGN KEY(endpoint_id) REFERENCES api_endpoints(id) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS request_cases (
    id TEXT PRIMARY KEY,
    project_id TEXT NOT NULL,
    entry_type TEXT NOT NULL DEFAULT 'quick-request',
    name TEXT NOT NULL,
    group_name TEXT NOT NULL,
    folder_path TEXT NOT NULL DEFAULT '',
    parent_id TEXT NOT NULL DEFAULT '',
    tags_json TEXT NOT NULL DEFAULT '[]',
    description TEXT NOT NULL DEFAULT '',
    request_snapshot_json TEXT NOT NULL,
    updated_at TEXT NOT NULL,
    FOREIGN KEY(project_id) REFERENCES projects(id) ON DELETE CASCADE
);

DROP INDEX IF EXISTS ux_request_cases_group_name;
DROP INDEX IF EXISTS ux_request_cases_project_group_name;

CREATE UNIQUE INDEX IF NOT EXISTS ux_request_cases_project_entry_scope_name
ON request_cases(project_id, entry_type, group_name, folder_path, parent_id, name);

CREATE TABLE IF NOT EXISTS environment_variables (
    id TEXT PRIMARY KEY,
    environment_id TEXT NOT NULL,
    environment_name TEXT NOT NULL DEFAULT '',
    key TEXT NOT NULL,
    value TEXT NOT NULL DEFAULT '',
    is_enabled INTEGER NOT NULL DEFAULT 1,
    FOREIGN KEY(environment_id) REFERENCES project_environments(id) ON DELETE CASCADE
);

DROP INDEX IF EXISTS ux_environment_variables_name_key;

CREATE UNIQUE INDEX IF NOT EXISTS ux_environment_variables_environment_key
ON environment_variables(environment_id, key);

CREATE TABLE IF NOT EXISTS request_history (
    id TEXT PRIMARY KEY,
    project_id TEXT NOT NULL,
    timestamp TEXT NOT NULL,
    request_snapshot_json TEXT NOT NULL,
    response_snapshot_json TEXT NOT NULL DEFAULT '{}',
    FOREIGN KEY(project_id) REFERENCES projects(id) ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS ix_request_history_timestamp
ON request_history(timestamp DESC);
