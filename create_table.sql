CREATE TABLE IF NOT EXISTS UserApiSettings (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    UserId INTEGER NOT NULL,
    Provider TEXT NOT NULL,
    ApiKeyEncrypted TEXT NOT NULL,
    IsActive INTEGER NOT NULL,
    CreatedAtUtc TEXT NOT NULL,
    LastValidatedAtUtc TEXT,
    FOREIGN KEY (UserId) REFERENCES Users (Id) ON DELETE CASCADE
);

CREATE UNIQUE INDEX IF NOT EXISTS IX_UserApiSettings_UserId_Provider 
ON UserApiSettings (UserId, Provider);