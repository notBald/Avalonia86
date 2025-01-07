CREATE TABLE FileInfo (
    Creator TEXT NOT NULL
               PRIMARY KEY,
    Version NUMERIC NOT NULL
);
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