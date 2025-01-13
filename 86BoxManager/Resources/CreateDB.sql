--§v1.0
--§Main
CREATE TABLE FileInfo (
    Creator TEXT NOT NULL PRIMARY KEY,
    Updater TEXT,
    Version NUMERIC NOT NULL
) WITHOUT ROWID;
--§
CREATE TABLE Window (
    "Top"     REAL NOT NULL,
    "Left"    REAL NOT NULL,
    Height    REAL NOT NULL,
    Width     REAL NOT NULL,
    Maximized BOOLEAN NOT NULL
);
--§
CREATE TABLE AppSettings (
    Field TEXT NOT NULL PRIMARY KEY,
    Value TEXT
);
--§
CREATE TABLE VMs (
    ID INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
    VMPath TEXT NOT NULL UNIQUE,
    Name TEXT NOT NULL,
    Created TEXT NOT NULL,
    Category TEXT,
    IconPath TEXT,
    LastRun TEXT,
    Uptime TEXT,
    RunCount INTEGER NOT NULL DEFAULT 0,
    Linked BOOLEAN NOT NULL DEFAULT FALSE 
);
--§
CREATE TABLE VMSettings (
    VMID INTEGER NOT NULL,
    Field TEXT NOT NULL,
    Value TEXT,
    CONSTRAINT vm_settings_pk PRIMARY KEY (
        VMID,
        Field
    ),
    FOREIGN KEY(VMID) REFERENCES VMs(ID)
);
--§Version
--§v1.1
CREATE TABLE FileInfo_new (
    Creator TEXT NOT NULL PRIMARY KEY,
    Updater TEXT,
    Version NUMERIC NOT NULL
) WITHOUT ROWID;
--§
INSERT INTO FileInfo_new (Creator, Version)
SELECT Creator, Version FROM FileInfo;
--§
DROP TABLE FileInfo;
--§
ALTER TABLE FileInfo_new RENAME TO FileInfo;
--§Main
CREATE TABLE Executables (
    ID INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
    IsDef BOOLEAN NOT NULL DEFAULT FALSE,
    Name TEXT,
    Path86 TEXT NOT NULL UNIQUE,
    Comment Text
);