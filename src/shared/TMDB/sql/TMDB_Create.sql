CREATE TABLE IF NOT EXISTS tmdb_cache (
    url_hash PRIMARY KEY NOT NULL,
    response BLOB,
    response_type TEXT NOT NULL,
    url TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS genres (
    id INTEGER PRIMARY KEY,
    name TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS movie_details (
    id INTEGER PRIMARY KEY,
    title TEXT NOT NULL,
    details TEXT NOT NULL
);
