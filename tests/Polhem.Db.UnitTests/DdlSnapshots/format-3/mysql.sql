-- path: create, mode: Create
-- stage: CreateTable
-- Create table idem_gate_test
CREATE TABLE `idem_gate_test` (
  `sys_no` BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY COMMENT 'Number',
  `s` VARCHAR(30) NOT NULL DEFAULT '' COMMENT 'Text up to 30',
  `s_def` VARCHAR(30) NOT NULL DEFAULT 'x' COMMENT 'Text with a default',
  `s_null` VARCHAR(30) NULL COMMENT 'Nullable text',
  `code` VARCHAR(10) NOT NULL DEFAULT '' COMMENT 'Code',
  `t` LONGTEXT NOT NULL COMMENT 'Long text',
  `t_null` LONGTEXT NULL COMMENT 'Nullable long text',
  `b` TINYINT(1) NOT NULL DEFAULT 0 COMMENT 'Flag',
  `b_def` TINYINT(1) NOT NULL DEFAULT 1 COMMENT 'Flag set by default',
  `sh` SMALLINT NOT NULL DEFAULT 0 COMMENT 'Short',
  `i` INT NOT NULL DEFAULT 0 COMMENT 'Integer',
  `i_def` INT NOT NULL DEFAULT 7 COMMENT 'Integer with a default',
  `i_null` INT NULL COMMENT 'Nullable integer',
  `l` BIGINT NOT NULL DEFAULT 0 COMMENT 'Long',
  `d` DECIMAL(18,4) NOT NULL DEFAULT 0 COMMENT 'Decimal',
  `d_def` DECIMAL(10,2) NOT NULL DEFAULT 1.5 COMMENT 'Decimal with a default',
  `c` DECIMAL(19,4) NOT NULL DEFAULT 0 COMMENT 'Currency',
  `dt` DATE NOT NULL DEFAULT (UTC_DATE()) COMMENT 'Date',
  `dt_null` DATE NULL COMMENT 'Nullable date',
  `dtm` DATETIME(6) NOT NULL DEFAULT (UTC_TIMESTAMP(6)) COMMENT 'Date and time',
  `tm` CHAR(5) NOT NULL DEFAULT '' COMMENT 'Time of day',
  `sys_rowid` CHAR(36) NOT NULL DEFAULT (UUID()) COMMENT 'Row id',
  `g` CHAR(36) NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000' COMMENT 'Guid',
  `g_null` CHAR(36) NULL COMMENT 'Nullable guid',
  `bin` LONGBLOB NOT NULL COMMENT 'Bytes'
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci COMMENT='Idempotency gate';
CREATE INDEX `ix_idem_gate_test_s` ON `idem_gate_test` (`s` ASC);
CREATE UNIQUE INDEX `uk_idem_gate_test_i_def` ON `idem_gate_test` (`i_def` ASC, `sys_no` ASC);

-- path: add columns, mode: Alter
-- stage: AlterColumns
ALTER TABLE `idem_gate_test` MODIFY COLUMN `s` VARCHAR(30) NOT NULL DEFAULT '' COMMENT 'Text up to 30';

-- stage: AddColumns
ALTER TABLE `idem_gate_test` ADD COLUMN `s_def` VARCHAR(30) NOT NULL DEFAULT 'x' COMMENT 'Text with a default';

ALTER TABLE `idem_gate_test` ADD COLUMN `t` LONGTEXT NOT NULL COMMENT 'Long text';

ALTER TABLE `idem_gate_test` ADD COLUMN `b_def` TINYINT(1) NOT NULL DEFAULT 1 COMMENT 'Flag set by default';

ALTER TABLE `idem_gate_test` ADD COLUMN `i_def` INT NOT NULL DEFAULT 7 COMMENT 'Integer with a default';

ALTER TABLE `idem_gate_test` ADD COLUMN `i_null` INT NULL COMMENT 'Nullable integer';

ALTER TABLE `idem_gate_test` ADD COLUMN `d_def` DECIMAL(10,2) NOT NULL DEFAULT 1.5 COMMENT 'Decimal with a default';

ALTER TABLE `idem_gate_test` ADD COLUMN `dt` DATE NOT NULL DEFAULT (UTC_DATE()) COMMENT 'Date';

ALTER TABLE `idem_gate_test` ADD COLUMN `dt_null` DATE NULL COMMENT 'Nullable date';

ALTER TABLE `idem_gate_test` ADD COLUMN `dtm` DATETIME(6) NOT NULL DEFAULT (UTC_TIMESTAMP(6)) COMMENT 'Date and time';

ALTER TABLE `idem_gate_test` ADD COLUMN `tm` CHAR(5) NOT NULL DEFAULT '' COMMENT 'Time of day';

ALTER TABLE `idem_gate_test` ADD COLUMN `sys_rowid` CHAR(36) NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000' COMMENT 'Row id';

UPDATE `idem_gate_test` SET `sys_rowid` = UUID();

ALTER TABLE `idem_gate_test` ALTER COLUMN `sys_rowid` SET DEFAULT (UUID());

ALTER TABLE `idem_gate_test` ADD COLUMN `g` CHAR(36) NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000' COMMENT 'Guid';

ALTER TABLE `idem_gate_test` ADD COLUMN `g_null` CHAR(36) NULL COMMENT 'Nullable guid';

-- stage: CreateIndexes
CREATE UNIQUE INDEX `uk_idem_gate_test_i_def` ON `idem_gate_test` (`i_def` ASC, `sys_no` ASC);

-- path: rebuild, mode: Rebuild
-- stage: Rebuild
-- Rebuild table idem_gate_test
-- Drop temporary table
DROP TABLE IF EXISTS `tmp_idem_gate_test`;
-- Create temporary table
-- Create table tmp_idem_gate_test
CREATE TABLE `tmp_idem_gate_test` (
  `sys_no` BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY COMMENT 'Number',
  `s` VARCHAR(30) NOT NULL DEFAULT '' COMMENT 'Text up to 30',
  `s_def` VARCHAR(30) NOT NULL DEFAULT 'x' COMMENT 'Text with a default',
  `s_null` VARCHAR(30) NULL COMMENT 'Nullable text',
  `code` VARCHAR(10) NOT NULL DEFAULT '' COMMENT 'Code',
  `t` LONGTEXT NOT NULL COMMENT 'Long text',
  `t_null` LONGTEXT NULL COMMENT 'Nullable long text',
  `b` TINYINT(1) NOT NULL DEFAULT 0 COMMENT 'Flag',
  `b_def` TINYINT(1) NOT NULL DEFAULT 1 COMMENT 'Flag set by default',
  `sh` SMALLINT NOT NULL DEFAULT 0 COMMENT 'Short',
  `i` INT NOT NULL DEFAULT 0 COMMENT 'Integer',
  `i_def` INT NOT NULL DEFAULT 7 COMMENT 'Integer with a default',
  `i_null` INT NULL COMMENT 'Nullable integer',
  `l` BIGINT NOT NULL DEFAULT 0 COMMENT 'Long',
  `d` DECIMAL(18,4) NOT NULL DEFAULT 0 COMMENT 'Decimal',
  `d_def` DECIMAL(10,2) NOT NULL DEFAULT 1.5 COMMENT 'Decimal with a default',
  `c` DECIMAL(19,4) NOT NULL DEFAULT 0 COMMENT 'Currency',
  `dt` DATE NOT NULL DEFAULT (UTC_DATE()) COMMENT 'Date',
  `dt_null` DATE NULL COMMENT 'Nullable date',
  `dtm` DATETIME(6) NOT NULL DEFAULT (UTC_TIMESTAMP(6)) COMMENT 'Date and time',
  `tm` CHAR(5) NOT NULL DEFAULT '' COMMENT 'Time of day',
  `sys_rowid` CHAR(36) NOT NULL DEFAULT (UUID()) COMMENT 'Row id',
  `g` CHAR(36) NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000' COMMENT 'Guid',
  `g_null` CHAR(36) NULL COMMENT 'Nullable guid',
  `bin` LONGBLOB NOT NULL COMMENT 'Bytes'
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci COMMENT='Idempotency gate';
-- Move data
INSERT INTO `tmp_idem_gate_test` (`sys_no`, `s`, `s_def`, `s_null`, `code`, `t`, `t_null`, `b`, `b_def`, `sh`, `i`, `i_def`, `i_null`, `l`, `d`, `d_def`, `c`, `dt`, `dt_null`, `dtm`, `tm`, `sys_rowid`, `g`, `g_null`, `bin`)
SELECT `sys_no`, `s`, `s_def`, `s_null`, `code`, `t`, `t_null`, `b`, `b_def`, `sh`, `i`, `i_def`, `i_null`, `l`, `d`, `d_def`, `c`, `dt`, `dt_null`, `dtm`, `tm`, `sys_rowid`, `g`, `g_null`, `bin` FROM `idem_gate_test`;
-- Drop old table
DROP TABLE IF EXISTS `idem_gate_test`;
-- Rename temporary table
ALTER TABLE `tmp_idem_gate_test` RENAME TO `idem_gate_test`;
CREATE INDEX `ix_idem_gate_test_s` ON `idem_gate_test` (`s` ASC);
CREATE UNIQUE INDEX `uk_idem_gate_test_i_def` ON `idem_gate_test` (`i_def` ASC, `sys_no` ASC);

