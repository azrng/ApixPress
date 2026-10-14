ALTER TABLE projects ADD COLUMN sort_order INTEGER NOT NULL DEFAULT 0;

UPDATE projects
SET sort_order = (
    SELECT COUNT(*) + 1
    FROM projects AS earlier
    WHERE earlier.is_default > projects.is_default
       OR (earlier.is_default = projects.is_default AND earlier.updated_at > projects.updated_at)
       OR (earlier.is_default = projects.is_default AND earlier.updated_at = projects.updated_at AND earlier.name < projects.name)
);
