PRAGMA foreign_keys = ON;

CREATE TABLE IF NOT EXISTS file (
    path_hash TEXT,
    path TEXT NOT NULL,
    date_modified TEXT NOT NULL,
    date_created TEXT NOT NULL,
    size INTEGER NOT NULL,
    extension TEXT NOT NULL,
    hash TEXT,
    attributes TEXT,
    extra_attributes TEXT
);

CREATE UNIQUE INDEX IF NOT EXISTS file_path_hash ON file(path_hash);
