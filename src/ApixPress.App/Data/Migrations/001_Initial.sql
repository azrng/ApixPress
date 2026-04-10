CREATE TABLE IF NOT EXISTS api_documents (
    id TEXT PRIMARY KEY,
    name TEXT NOT NULL,
    source_type TEXT NOT NULL,
    source_value TEXT NOT NULL,
    base_url TEXT NOT NULL DEFAULT '',
    raw_json TEXT NOT NULL,
    imported_at TEXT NOT NULL
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
    name TEXT NOT NULL,
    group_name TEXT NOT NULL,
    tags_json TEXT NOT NULL DEFAULT '[]',
    description TEXT NOT NULL DEFAULT '',
    request_snapshot_json TEXT NOT NULL,
    updated_at TEXT NOT NULL
);

CREATE UNIQUE INDEX IF NOT EXISTS ux_request_cases_group_name
ON request_cases(group_name, name);

CREATE TABLE IF NOT EXISTS environment_variables (
    id TEXT PRIMARY KEY,
    environment_name TEXT NOT NULL,
    key TEXT NOT NULL,
    value TEXT NOT NULL DEFAULT '',
    is_enabled INTEGER NOT NULL DEFAULT 1
);

CREATE UNIQUE INDEX IF NOT EXISTS ux_environment_variables_name_key
ON environment_variables(environment_name, key);

CREATE TABLE IF NOT EXISTS request_history (
    id TEXT PRIMARY KEY,
    timestamp TEXT NOT NULL,
    request_snapshot_json TEXT NOT NULL,
    response_snapshot_json TEXT NOT NULL DEFAULT '{}'
);

CREATE INDEX IF NOT EXISTS ix_request_history_timestamp
ON request_history(timestamp DESC);
