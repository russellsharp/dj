CREATE TABLE IF NOT EXISTS 'users' (
    display_name TEXT NOT NULL UNIQUE,
    client_id TEXT NOT NULL,
    scopes TEXT NOT NULL,
    password_hash TEXT NOT NULL,
    created_at DATETIME DEFAULT CURRENT_TIMESTAMP
);


CREATE UNIQUE INDEX IF NOT EXISTS user_name ON users(user_name);