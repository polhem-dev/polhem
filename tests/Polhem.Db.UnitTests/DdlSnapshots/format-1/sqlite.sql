-- path: create, mode: Create
-- stage: CreateTable
-- Create table idem_gate_test
CREATE TABLE "idem_gate_test" (
  "sys_no" INTEGER PRIMARY KEY AUTOINCREMENT NOT NULL,
  "s" VARCHAR(30) COLLATE NOCASE NOT NULL DEFAULT '',
  "s_def" VARCHAR(30) COLLATE NOCASE NOT NULL DEFAULT 'x',
  "s_null" VARCHAR(30) COLLATE NOCASE NULL,
  "code" VARCHAR(10) COLLATE NOCASE NOT NULL DEFAULT '',
  "t" TEXT COLLATE NOCASE NOT NULL DEFAULT '',
  "t_null" TEXT COLLATE NOCASE NULL,
  "b" BOOLEAN NOT NULL DEFAULT 0,
  "b_def" BOOLEAN NOT NULL DEFAULT 1,
  "sh" SMALLINT NOT NULL DEFAULT 0,
  "i" INTEGER NOT NULL DEFAULT 0,
  "i_def" INTEGER NOT NULL DEFAULT 7,
  "i_null" INTEGER NULL,
  "l" BIGINT NOT NULL DEFAULT 0,
  "d" NUMERIC(18,4) NOT NULL DEFAULT 0,
  "d_def" NUMERIC(10,2) NOT NULL DEFAULT 1.5,
  "c" NUMERIC(19,4) NOT NULL DEFAULT 0,
  "dt" DATE NOT NULL DEFAULT (datetime('now', 'start of day')),
  "dt_null" DATE NULL,
  "dtm" DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  "tm" VARCHAR(5) NOT NULL DEFAULT '',
  "g" UUID COLLATE NOCASE NOT NULL DEFAULT (hex(randomblob(16))),
  "g_null" UUID COLLATE NOCASE NULL,
  "bin" BLOB NOT NULL
);
CREATE INDEX "ix_idem_gate_test_s" ON "idem_gate_test" ("s" ASC);
CREATE UNIQUE INDEX "uk_idem_gate_test_i_def" ON "idem_gate_test" ("i_def" ASC, "sys_no" ASC);

-- path: add columns, mode: Rebuild
-- stage: Rebuild
-- Rebuild table idem_gate_test
-- Drop temporary table
DROP TABLE IF EXISTS "tmp_idem_gate_test";
-- Create temporary table
-- Create table tmp_idem_gate_test
CREATE TABLE "tmp_idem_gate_test" (
  "sys_no" INTEGER PRIMARY KEY AUTOINCREMENT NOT NULL,
  "s" VARCHAR(30) COLLATE NOCASE NOT NULL DEFAULT '',
  "s_def" VARCHAR(30) COLLATE NOCASE NOT NULL DEFAULT 'x',
  "s_null" VARCHAR(30) COLLATE NOCASE NULL,
  "code" VARCHAR(10) COLLATE NOCASE NOT NULL DEFAULT '',
  "t" TEXT COLLATE NOCASE NOT NULL DEFAULT '',
  "t_null" TEXT COLLATE NOCASE NULL,
  "b" BOOLEAN NOT NULL DEFAULT 0,
  "b_def" BOOLEAN NOT NULL DEFAULT 1,
  "sh" SMALLINT NOT NULL DEFAULT 0,
  "i" INTEGER NOT NULL DEFAULT 0,
  "i_def" INTEGER NOT NULL DEFAULT 7,
  "i_null" INTEGER NULL,
  "l" BIGINT NOT NULL DEFAULT 0,
  "d" NUMERIC(18,4) NOT NULL DEFAULT 0,
  "d_def" NUMERIC(10,2) NOT NULL DEFAULT 1.5,
  "c" NUMERIC(19,4) NOT NULL DEFAULT 0,
  "dt" DATE NOT NULL DEFAULT (datetime('now', 'start of day')),
  "dt_null" DATE NULL,
  "dtm" DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  "tm" VARCHAR(5) NOT NULL DEFAULT '',
  "g" UUID COLLATE NOCASE NOT NULL DEFAULT (hex(randomblob(16))),
  "g_null" UUID COLLATE NOCASE NULL,
  "bin" BLOB NOT NULL
);
-- Move data
INSERT INTO "tmp_idem_gate_test" ("sys_no", "s", "s_null", "code", "t_null", "b", "sh", "i", "l", "d", "c", "bin")
SELECT "sys_no", "s", "s_null", "code", "t_null", "b", "sh", "i", "l", "d", "c", "bin" FROM "idem_gate_test";
INSERT INTO sqlite_sequence (name, seq) SELECT 'tmp_idem_gate_test', seq FROM sqlite_sequence WHERE name = 'idem_gate_test' AND NOT EXISTS (SELECT 1 FROM sqlite_sequence WHERE name = 'tmp_idem_gate_test');
UPDATE sqlite_sequence SET seq = MAX(seq, COALESCE((SELECT seq FROM sqlite_sequence WHERE name = 'idem_gate_test'), 0)) WHERE name = 'tmp_idem_gate_test';
-- Drop old table
DROP TABLE IF EXISTS "idem_gate_test";
-- Rename temporary table
ALTER TABLE "tmp_idem_gate_test" RENAME TO "idem_gate_test";
CREATE INDEX "ix_idem_gate_test_s" ON "idem_gate_test" ("s" ASC);
CREATE UNIQUE INDEX "uk_idem_gate_test_i_def" ON "idem_gate_test" ("i_def" ASC, "sys_no" ASC);

-- path: rebuild, mode: Rebuild
-- stage: Rebuild
-- Rebuild table idem_gate_test
-- Drop temporary table
DROP TABLE IF EXISTS "tmp_idem_gate_test";
-- Create temporary table
-- Create table tmp_idem_gate_test
CREATE TABLE "tmp_idem_gate_test" (
  "sys_no" INTEGER PRIMARY KEY AUTOINCREMENT NOT NULL,
  "s" VARCHAR(30) COLLATE NOCASE NOT NULL DEFAULT '',
  "s_def" VARCHAR(30) COLLATE NOCASE NOT NULL DEFAULT 'x',
  "s_null" VARCHAR(30) COLLATE NOCASE NULL,
  "code" VARCHAR(10) COLLATE NOCASE NOT NULL DEFAULT '',
  "t" TEXT COLLATE NOCASE NOT NULL DEFAULT '',
  "t_null" TEXT COLLATE NOCASE NULL,
  "b" BOOLEAN NOT NULL DEFAULT 0,
  "b_def" BOOLEAN NOT NULL DEFAULT 1,
  "sh" SMALLINT NOT NULL DEFAULT 0,
  "i" INTEGER NOT NULL DEFAULT 0,
  "i_def" INTEGER NOT NULL DEFAULT 7,
  "i_null" INTEGER NULL,
  "l" BIGINT NOT NULL DEFAULT 0,
  "d" NUMERIC(18,4) NOT NULL DEFAULT 0,
  "d_def" NUMERIC(10,2) NOT NULL DEFAULT 1.5,
  "c" NUMERIC(19,4) NOT NULL DEFAULT 0,
  "dt" DATE NOT NULL DEFAULT (datetime('now', 'start of day')),
  "dt_null" DATE NULL,
  "dtm" DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  "tm" VARCHAR(5) NOT NULL DEFAULT '',
  "g" UUID COLLATE NOCASE NOT NULL DEFAULT (hex(randomblob(16))),
  "g_null" UUID COLLATE NOCASE NULL,
  "bin" BLOB NOT NULL
);
-- Move data
INSERT INTO "tmp_idem_gate_test" ("sys_no", "s", "s_def", "s_null", "code", "t", "t_null", "b", "b_def", "sh", "i", "i_def", "i_null", "l", "d", "d_def", "c", "dt", "dt_null", "dtm", "tm", "g", "g_null", "bin")
SELECT "sys_no", "s", "s_def", "s_null", "code", "t", "t_null", "b", "b_def", "sh", "i", "i_def", "i_null", "l", "d", "d_def", "c", "dt", "dt_null", "dtm", "tm", "g", "g_null", "bin" FROM "idem_gate_test";
INSERT INTO sqlite_sequence (name, seq) SELECT 'tmp_idem_gate_test', seq FROM sqlite_sequence WHERE name = 'idem_gate_test' AND NOT EXISTS (SELECT 1 FROM sqlite_sequence WHERE name = 'tmp_idem_gate_test');
UPDATE sqlite_sequence SET seq = MAX(seq, COALESCE((SELECT seq FROM sqlite_sequence WHERE name = 'idem_gate_test'), 0)) WHERE name = 'tmp_idem_gate_test';
-- Drop old table
DROP TABLE IF EXISTS "idem_gate_test";
-- Rename temporary table
ALTER TABLE "tmp_idem_gate_test" RENAME TO "idem_gate_test";
CREATE INDEX "ix_idem_gate_test_s" ON "idem_gate_test" ("s" ASC);
CREATE UNIQUE INDEX "uk_idem_gate_test_i_def" ON "idem_gate_test" ("i_def" ASC, "sys_no" ASC);

