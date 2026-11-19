-- path: create, mode: Create
-- stage: CreateTable
-- Create table idem_gate_test
CREATE TABLE [idem_gate_test] (
  [sys_no] [int] IDENTITY(1,1) NOT NULL,
  [s] [nvarchar](30) NOT NULL DEFAULT (N''),
  [s_def] [nvarchar](30) NOT NULL DEFAULT (N'x'),
  [s_null] [nvarchar](30) NULL,
  [code] [nvarchar](10) NOT NULL DEFAULT (N''),
  [t] [nvarchar](max) NOT NULL DEFAULT (N''),
  [t_null] [nvarchar](max) NULL,
  [b] [bit] NOT NULL DEFAULT (0),
  [b_def] [bit] NOT NULL DEFAULT (1),
  [sh] [smallint] NOT NULL DEFAULT (0),
  [i] [int] NOT NULL DEFAULT (0),
  [i_def] [int] NOT NULL DEFAULT (7),
  [i_null] [int] NULL,
  [l] [bigint] NOT NULL DEFAULT (0),
  [d] [decimal](18,4) NOT NULL DEFAULT (0),
  [d_def] [decimal](10,2) NOT NULL DEFAULT (1.5),
  [c] [decimal](19,4) NOT NULL DEFAULT (0),
  [dt] [date] NOT NULL DEFAULT (getutcdate()),
  [dt_null] [date] NULL,
  [dtm] [datetime2](7) NOT NULL DEFAULT (getutcdate()),
  [tm] [nchar](5) NOT NULL DEFAULT (N''),
  [sys_rowid] [uniqueidentifier] NOT NULL DEFAULT (newid()),
  [g] [uniqueidentifier] NOT NULL DEFAULT ('00000000-0000-0000-0000-000000000000'),
  [g_null] [uniqueidentifier] NULL,
  [bin] [varbinary](max) NOT NULL,
  CONSTRAINT [pk_idem_gate_test] PRIMARY KEY ([sys_no] ASC)
);
CREATE INDEX [ix_idem_gate_test_s] ON [idem_gate_test] ([s] ASC);
CREATE UNIQUE INDEX [uk_idem_gate_test_i_def] ON [idem_gate_test] ([i_def] ASC, [sys_no] ASC);
EXEC sp_addextendedproperty
  @name=N'MS_Description', @value=N'Idempotency gate',
  @level0type=N'SCHEMA', @level0name=N'dbo',
  @level1type=N'TABLE', @level1name=N'idem_gate_test';
EXEC sp_addextendedproperty
  @name=N'MS_Description', @value=N'Number',
  @level0type=N'SCHEMA', @level0name=N'dbo',
  @level1type=N'TABLE', @level1name=N'idem_gate_test',
  @level2type=N'COLUMN', @level2name=N'sys_no';
EXEC sp_addextendedproperty
  @name=N'MS_Description', @value=N'Text up to 30',
  @level0type=N'SCHEMA', @level0name=N'dbo',
  @level1type=N'TABLE', @level1name=N'idem_gate_test',
  @level2type=N'COLUMN', @level2name=N's';
EXEC sp_addextendedproperty
  @name=N'MS_Description', @value=N'Text with a default',
  @level0type=N'SCHEMA', @level0name=N'dbo',
  @level1type=N'TABLE', @level1name=N'idem_gate_test',
  @level2type=N'COLUMN', @level2name=N's_def';
EXEC sp_addextendedproperty
  @name=N'MS_Description', @value=N'Nullable text',
  @level0type=N'SCHEMA', @level0name=N'dbo',
  @level1type=N'TABLE', @level1name=N'idem_gate_test',
  @level2type=N'COLUMN', @level2name=N's_null';
EXEC sp_addextendedproperty
  @name=N'MS_Description', @value=N'Code',
  @level0type=N'SCHEMA', @level0name=N'dbo',
  @level1type=N'TABLE', @level1name=N'idem_gate_test',
  @level2type=N'COLUMN', @level2name=N'code';
EXEC sp_addextendedproperty
  @name=N'MS_Description', @value=N'Long text',
  @level0type=N'SCHEMA', @level0name=N'dbo',
  @level1type=N'TABLE', @level1name=N'idem_gate_test',
  @level2type=N'COLUMN', @level2name=N't';
EXEC sp_addextendedproperty
  @name=N'MS_Description', @value=N'Nullable long text',
  @level0type=N'SCHEMA', @level0name=N'dbo',
  @level1type=N'TABLE', @level1name=N'idem_gate_test',
  @level2type=N'COLUMN', @level2name=N't_null';
EXEC sp_addextendedproperty
  @name=N'MS_Description', @value=N'Flag',
  @level0type=N'SCHEMA', @level0name=N'dbo',
  @level1type=N'TABLE', @level1name=N'idem_gate_test',
  @level2type=N'COLUMN', @level2name=N'b';
EXEC sp_addextendedproperty
  @name=N'MS_Description', @value=N'Flag set by default',
  @level0type=N'SCHEMA', @level0name=N'dbo',
  @level1type=N'TABLE', @level1name=N'idem_gate_test',
  @level2type=N'COLUMN', @level2name=N'b_def';
EXEC sp_addextendedproperty
  @name=N'MS_Description', @value=N'Short',
  @level0type=N'SCHEMA', @level0name=N'dbo',
  @level1type=N'TABLE', @level1name=N'idem_gate_test',
  @level2type=N'COLUMN', @level2name=N'sh';
EXEC sp_addextendedproperty
  @name=N'MS_Description', @value=N'Integer',
  @level0type=N'SCHEMA', @level0name=N'dbo',
  @level1type=N'TABLE', @level1name=N'idem_gate_test',
  @level2type=N'COLUMN', @level2name=N'i';
EXEC sp_addextendedproperty
  @name=N'MS_Description', @value=N'Integer with a default',
  @level0type=N'SCHEMA', @level0name=N'dbo',
  @level1type=N'TABLE', @level1name=N'idem_gate_test',
  @level2type=N'COLUMN', @level2name=N'i_def';
EXEC sp_addextendedproperty
  @name=N'MS_Description', @value=N'Nullable integer',
  @level0type=N'SCHEMA', @level0name=N'dbo',
  @level1type=N'TABLE', @level1name=N'idem_gate_test',
  @level2type=N'COLUMN', @level2name=N'i_null';
EXEC sp_addextendedproperty
  @name=N'MS_Description', @value=N'Long',
  @level0type=N'SCHEMA', @level0name=N'dbo',
  @level1type=N'TABLE', @level1name=N'idem_gate_test',
  @level2type=N'COLUMN', @level2name=N'l';
EXEC sp_addextendedproperty
  @name=N'MS_Description', @value=N'Decimal',
  @level0type=N'SCHEMA', @level0name=N'dbo',
  @level1type=N'TABLE', @level1name=N'idem_gate_test',
  @level2type=N'COLUMN', @level2name=N'd';
EXEC sp_addextendedproperty
  @name=N'MS_Description', @value=N'Decimal with a default',
  @level0type=N'SCHEMA', @level0name=N'dbo',
  @level1type=N'TABLE', @level1name=N'idem_gate_test',
  @level2type=N'COLUMN', @level2name=N'd_def';
EXEC sp_addextendedproperty
  @name=N'MS_Description', @value=N'Currency',
  @level0type=N'SCHEMA', @level0name=N'dbo',
  @level1type=N'TABLE', @level1name=N'idem_gate_test',
  @level2type=N'COLUMN', @level2name=N'c';
EXEC sp_addextendedproperty
  @name=N'MS_Description', @value=N'Date',
  @level0type=N'SCHEMA', @level0name=N'dbo',
  @level1type=N'TABLE', @level1name=N'idem_gate_test',
  @level2type=N'COLUMN', @level2name=N'dt';
EXEC sp_addextendedproperty
  @name=N'MS_Description', @value=N'Nullable date',
  @level0type=N'SCHEMA', @level0name=N'dbo',
  @level1type=N'TABLE', @level1name=N'idem_gate_test',
  @level2type=N'COLUMN', @level2name=N'dt_null';
EXEC sp_addextendedproperty
  @name=N'MS_Description', @value=N'Date and time',
  @level0type=N'SCHEMA', @level0name=N'dbo',
  @level1type=N'TABLE', @level1name=N'idem_gate_test',
  @level2type=N'COLUMN', @level2name=N'dtm';
EXEC sp_addextendedproperty
  @name=N'MS_Description', @value=N'Time of day',
  @level0type=N'SCHEMA', @level0name=N'dbo',
  @level1type=N'TABLE', @level1name=N'idem_gate_test',
  @level2type=N'COLUMN', @level2name=N'tm';
EXEC sp_addextendedproperty
  @name=N'MS_Description', @value=N'Row id',
  @level0type=N'SCHEMA', @level0name=N'dbo',
  @level1type=N'TABLE', @level1name=N'idem_gate_test',
  @level2type=N'COLUMN', @level2name=N'sys_rowid';
EXEC sp_addextendedproperty
  @name=N'MS_Description', @value=N'Guid',
  @level0type=N'SCHEMA', @level0name=N'dbo',
  @level1type=N'TABLE', @level1name=N'idem_gate_test',
  @level2type=N'COLUMN', @level2name=N'g';
EXEC sp_addextendedproperty
  @name=N'MS_Description', @value=N'Nullable guid',
  @level0type=N'SCHEMA', @level0name=N'dbo',
  @level1type=N'TABLE', @level1name=N'idem_gate_test',
  @level2type=N'COLUMN', @level2name=N'g_null';
EXEC sp_addextendedproperty
  @name=N'MS_Description', @value=N'Bytes',
  @level0type=N'SCHEMA', @level0name=N'dbo',
  @level1type=N'TABLE', @level1name=N'idem_gate_test',
  @level2type=N'COLUMN', @level2name=N'bin';

-- path: add columns, mode: Alter
-- stage: AlterColumns
DECLARE @df_name NVARCHAR(256);
SELECT @df_name = dc.name
FROM sys.default_constraints dc
INNER JOIN sys.columns c ON c.object_id = dc.parent_object_id AND c.column_id = dc.parent_column_id
WHERE dc.parent_object_id = OBJECT_ID(N'idem_gate_test') AND c.name = N's';
IF @df_name IS NOT NULL
    EXEC('ALTER TABLE [idem_gate_test] DROP CONSTRAINT [' + @df_name + ']');

ALTER TABLE [idem_gate_test] ALTER COLUMN [s] [nvarchar](30) NOT NULL;

ALTER TABLE [idem_gate_test] ADD CONSTRAINT [df_idem_gate_test_s] DEFAULT (N'') FOR [s];

-- stage: AddColumns
ALTER TABLE [idem_gate_test] ADD [s_def] [nvarchar](30) NOT NULL DEFAULT (N'x');

ALTER TABLE [idem_gate_test] ADD [t] [nvarchar](max) NOT NULL DEFAULT (N'');

ALTER TABLE [idem_gate_test] ADD [b_def] [bit] NOT NULL DEFAULT (1);

ALTER TABLE [idem_gate_test] ADD [i_def] [int] NOT NULL DEFAULT (7);

ALTER TABLE [idem_gate_test] ADD [i_null] [int] NULL;

ALTER TABLE [idem_gate_test] ADD [d_def] [decimal](10,2) NOT NULL DEFAULT (1.5);

ALTER TABLE [idem_gate_test] ADD [dt] [date] NOT NULL DEFAULT (getutcdate());

ALTER TABLE [idem_gate_test] ADD [dt_null] [date] NULL;

ALTER TABLE [idem_gate_test] ADD [dtm] [datetime2](7) NOT NULL DEFAULT (getutcdate());

ALTER TABLE [idem_gate_test] ADD [tm] [nchar](5) NOT NULL DEFAULT (N'');

ALTER TABLE [idem_gate_test] ADD [g] [uniqueidentifier] NOT NULL DEFAULT ('00000000-0000-0000-0000-000000000000');

ALTER TABLE [idem_gate_test] ADD [g_null] [uniqueidentifier] NULL;

-- stage: CreateIndexes
CREATE UNIQUE INDEX [uk_idem_gate_test_i_def] ON [idem_gate_test] ([i_def] ASC, [sys_no] ASC);

-- stage: SyncDescriptions
-- Sync extended properties for idem_gate_test
EXEC sp_addextendedproperty
  @name=N'MS_Description', @value=N'Text with a default',
  @level0type=N'SCHEMA', @level0name=N'dbo',
  @level1type=N'TABLE', @level1name=N'idem_gate_test',
  @level2type=N'COLUMN', @level2name=N's_def';
EXEC sp_addextendedproperty
  @name=N'MS_Description', @value=N'Long text',
  @level0type=N'SCHEMA', @level0name=N'dbo',
  @level1type=N'TABLE', @level1name=N'idem_gate_test',
  @level2type=N'COLUMN', @level2name=N't';
EXEC sp_addextendedproperty
  @name=N'MS_Description', @value=N'Flag set by default',
  @level0type=N'SCHEMA', @level0name=N'dbo',
  @level1type=N'TABLE', @level1name=N'idem_gate_test',
  @level2type=N'COLUMN', @level2name=N'b_def';
EXEC sp_addextendedproperty
  @name=N'MS_Description', @value=N'Integer with a default',
  @level0type=N'SCHEMA', @level0name=N'dbo',
  @level1type=N'TABLE', @level1name=N'idem_gate_test',
  @level2type=N'COLUMN', @level2name=N'i_def';
EXEC sp_addextendedproperty
  @name=N'MS_Description', @value=N'Nullable integer',
  @level0type=N'SCHEMA', @level0name=N'dbo',
  @level1type=N'TABLE', @level1name=N'idem_gate_test',
  @level2type=N'COLUMN', @level2name=N'i_null';
EXEC sp_addextendedproperty
  @name=N'MS_Description', @value=N'Decimal with a default',
  @level0type=N'SCHEMA', @level0name=N'dbo',
  @level1type=N'TABLE', @level1name=N'idem_gate_test',
  @level2type=N'COLUMN', @level2name=N'd_def';
EXEC sp_addextendedproperty
  @name=N'MS_Description', @value=N'Date',
  @level0type=N'SCHEMA', @level0name=N'dbo',
  @level1type=N'TABLE', @level1name=N'idem_gate_test',
  @level2type=N'COLUMN', @level2name=N'dt';
EXEC sp_addextendedproperty
  @name=N'MS_Description', @value=N'Nullable date',
  @level0type=N'SCHEMA', @level0name=N'dbo',
  @level1type=N'TABLE', @level1name=N'idem_gate_test',
  @level2type=N'COLUMN', @level2name=N'dt_null';
EXEC sp_addextendedproperty
  @name=N'MS_Description', @value=N'Date and time',
  @level0type=N'SCHEMA', @level0name=N'dbo',
  @level1type=N'TABLE', @level1name=N'idem_gate_test',
  @level2type=N'COLUMN', @level2name=N'dtm';
EXEC sp_addextendedproperty
  @name=N'MS_Description', @value=N'Time of day',
  @level0type=N'SCHEMA', @level0name=N'dbo',
  @level1type=N'TABLE', @level1name=N'idem_gate_test',
  @level2type=N'COLUMN', @level2name=N'tm';
EXEC sp_addextendedproperty
  @name=N'MS_Description', @value=N'Guid',
  @level0type=N'SCHEMA', @level0name=N'dbo',
  @level1type=N'TABLE', @level1name=N'idem_gate_test',
  @level2type=N'COLUMN', @level2name=N'g';
EXEC sp_addextendedproperty
  @name=N'MS_Description', @value=N'Nullable guid',
  @level0type=N'SCHEMA', @level0name=N'dbo',
  @level1type=N'TABLE', @level1name=N'idem_gate_test',
  @level2type=N'COLUMN', @level2name=N'g_null';

-- path: rebuild, mode: Rebuild
-- stage: Rebuild
-- Rebuild table idem_gate_test
-- Drop temporary table
IF (SELECT COUNT(*) From sys.tables WHERE name=N'tmp_idem_gate_test')>0
  DROP TABLE [tmp_idem_gate_test];
-- Create temporary table
-- Create table tmp_idem_gate_test
CREATE TABLE [tmp_idem_gate_test] (
  [sys_no] [int] IDENTITY(1,1) NOT NULL,
  [s] [nvarchar](30) NOT NULL DEFAULT (N''),
  [s_def] [nvarchar](30) NOT NULL DEFAULT (N'x'),
  [s_null] [nvarchar](30) NULL,
  [code] [nvarchar](10) NOT NULL DEFAULT (N''),
  [t] [nvarchar](max) NOT NULL DEFAULT (N''),
  [t_null] [nvarchar](max) NULL,
  [b] [bit] NOT NULL DEFAULT (0),
  [b_def] [bit] NOT NULL DEFAULT (1),
  [sh] [smallint] NOT NULL DEFAULT (0),
  [i] [int] NOT NULL DEFAULT (0),
  [i_def] [int] NOT NULL DEFAULT (7),
  [i_null] [int] NULL,
  [l] [bigint] NOT NULL DEFAULT (0),
  [d] [decimal](18,4) NOT NULL DEFAULT (0),
  [d_def] [decimal](10,2) NOT NULL DEFAULT (1.5),
  [c] [decimal](19,4) NOT NULL DEFAULT (0),
  [dt] [date] NOT NULL DEFAULT (getutcdate()),
  [dt_null] [date] NULL,
  [dtm] [datetime2](7) NOT NULL DEFAULT (getutcdate()),
  [tm] [nchar](5) NOT NULL DEFAULT (N''),
  [sys_rowid] [uniqueidentifier] NOT NULL DEFAULT (newid()),
  [g] [uniqueidentifier] NOT NULL DEFAULT ('00000000-0000-0000-0000-000000000000'),
  [g_null] [uniqueidentifier] NULL,
  [bin] [varbinary](max) NOT NULL,
  CONSTRAINT [pk_tmp_idem_gate_test] PRIMARY KEY ([sys_no] ASC)
);
CREATE INDEX [ix_tmp_idem_gate_test_s] ON [tmp_idem_gate_test] ([s] ASC);
CREATE UNIQUE INDEX [uk_tmp_idem_gate_test_i_def] ON [tmp_idem_gate_test] ([i_def] ASC, [sys_no] ASC);
EXEC sp_addextendedproperty
  @name=N'MS_Description', @value=N'Idempotency gate',
  @level0type=N'SCHEMA', @level0name=N'dbo',
  @level1type=N'TABLE', @level1name=N'tmp_idem_gate_test';
EXEC sp_addextendedproperty
  @name=N'MS_Description', @value=N'Number',
  @level0type=N'SCHEMA', @level0name=N'dbo',
  @level1type=N'TABLE', @level1name=N'tmp_idem_gate_test',
  @level2type=N'COLUMN', @level2name=N'sys_no';
EXEC sp_addextendedproperty
  @name=N'MS_Description', @value=N'Text up to 30',
  @level0type=N'SCHEMA', @level0name=N'dbo',
  @level1type=N'TABLE', @level1name=N'tmp_idem_gate_test',
  @level2type=N'COLUMN', @level2name=N's';
EXEC sp_addextendedproperty
  @name=N'MS_Description', @value=N'Text with a default',
  @level0type=N'SCHEMA', @level0name=N'dbo',
  @level1type=N'TABLE', @level1name=N'tmp_idem_gate_test',
  @level2type=N'COLUMN', @level2name=N's_def';
EXEC sp_addextendedproperty
  @name=N'MS_Description', @value=N'Nullable text',
  @level0type=N'SCHEMA', @level0name=N'dbo',
  @level1type=N'TABLE', @level1name=N'tmp_idem_gate_test',
  @level2type=N'COLUMN', @level2name=N's_null';
EXEC sp_addextendedproperty
  @name=N'MS_Description', @value=N'Code',
  @level0type=N'SCHEMA', @level0name=N'dbo',
  @level1type=N'TABLE', @level1name=N'tmp_idem_gate_test',
  @level2type=N'COLUMN', @level2name=N'code';
EXEC sp_addextendedproperty
  @name=N'MS_Description', @value=N'Long text',
  @level0type=N'SCHEMA', @level0name=N'dbo',
  @level1type=N'TABLE', @level1name=N'tmp_idem_gate_test',
  @level2type=N'COLUMN', @level2name=N't';
EXEC sp_addextendedproperty
  @name=N'MS_Description', @value=N'Nullable long text',
  @level0type=N'SCHEMA', @level0name=N'dbo',
  @level1type=N'TABLE', @level1name=N'tmp_idem_gate_test',
  @level2type=N'COLUMN', @level2name=N't_null';
EXEC sp_addextendedproperty
  @name=N'MS_Description', @value=N'Flag',
  @level0type=N'SCHEMA', @level0name=N'dbo',
  @level1type=N'TABLE', @level1name=N'tmp_idem_gate_test',
  @level2type=N'COLUMN', @level2name=N'b';
EXEC sp_addextendedproperty
  @name=N'MS_Description', @value=N'Flag set by default',
  @level0type=N'SCHEMA', @level0name=N'dbo',
  @level1type=N'TABLE', @level1name=N'tmp_idem_gate_test',
  @level2type=N'COLUMN', @level2name=N'b_def';
EXEC sp_addextendedproperty
  @name=N'MS_Description', @value=N'Short',
  @level0type=N'SCHEMA', @level0name=N'dbo',
  @level1type=N'TABLE', @level1name=N'tmp_idem_gate_test',
  @level2type=N'COLUMN', @level2name=N'sh';
EXEC sp_addextendedproperty
  @name=N'MS_Description', @value=N'Integer',
  @level0type=N'SCHEMA', @level0name=N'dbo',
  @level1type=N'TABLE', @level1name=N'tmp_idem_gate_test',
  @level2type=N'COLUMN', @level2name=N'i';
EXEC sp_addextendedproperty
  @name=N'MS_Description', @value=N'Integer with a default',
  @level0type=N'SCHEMA', @level0name=N'dbo',
  @level1type=N'TABLE', @level1name=N'tmp_idem_gate_test',
  @level2type=N'COLUMN', @level2name=N'i_def';
EXEC sp_addextendedproperty
  @name=N'MS_Description', @value=N'Nullable integer',
  @level0type=N'SCHEMA', @level0name=N'dbo',
  @level1type=N'TABLE', @level1name=N'tmp_idem_gate_test',
  @level2type=N'COLUMN', @level2name=N'i_null';
EXEC sp_addextendedproperty
  @name=N'MS_Description', @value=N'Long',
  @level0type=N'SCHEMA', @level0name=N'dbo',
  @level1type=N'TABLE', @level1name=N'tmp_idem_gate_test',
  @level2type=N'COLUMN', @level2name=N'l';
EXEC sp_addextendedproperty
  @name=N'MS_Description', @value=N'Decimal',
  @level0type=N'SCHEMA', @level0name=N'dbo',
  @level1type=N'TABLE', @level1name=N'tmp_idem_gate_test',
  @level2type=N'COLUMN', @level2name=N'd';
EXEC sp_addextendedproperty
  @name=N'MS_Description', @value=N'Decimal with a default',
  @level0type=N'SCHEMA', @level0name=N'dbo',
  @level1type=N'TABLE', @level1name=N'tmp_idem_gate_test',
  @level2type=N'COLUMN', @level2name=N'd_def';
EXEC sp_addextendedproperty
  @name=N'MS_Description', @value=N'Currency',
  @level0type=N'SCHEMA', @level0name=N'dbo',
  @level1type=N'TABLE', @level1name=N'tmp_idem_gate_test',
  @level2type=N'COLUMN', @level2name=N'c';
EXEC sp_addextendedproperty
  @name=N'MS_Description', @value=N'Date',
  @level0type=N'SCHEMA', @level0name=N'dbo',
  @level1type=N'TABLE', @level1name=N'tmp_idem_gate_test',
  @level2type=N'COLUMN', @level2name=N'dt';
EXEC sp_addextendedproperty
  @name=N'MS_Description', @value=N'Nullable date',
  @level0type=N'SCHEMA', @level0name=N'dbo',
  @level1type=N'TABLE', @level1name=N'tmp_idem_gate_test',
  @level2type=N'COLUMN', @level2name=N'dt_null';
EXEC sp_addextendedproperty
  @name=N'MS_Description', @value=N'Date and time',
  @level0type=N'SCHEMA', @level0name=N'dbo',
  @level1type=N'TABLE', @level1name=N'tmp_idem_gate_test',
  @level2type=N'COLUMN', @level2name=N'dtm';
EXEC sp_addextendedproperty
  @name=N'MS_Description', @value=N'Time of day',
  @level0type=N'SCHEMA', @level0name=N'dbo',
  @level1type=N'TABLE', @level1name=N'tmp_idem_gate_test',
  @level2type=N'COLUMN', @level2name=N'tm';
EXEC sp_addextendedproperty
  @name=N'MS_Description', @value=N'Row id',
  @level0type=N'SCHEMA', @level0name=N'dbo',
  @level1type=N'TABLE', @level1name=N'tmp_idem_gate_test',
  @level2type=N'COLUMN', @level2name=N'sys_rowid';
EXEC sp_addextendedproperty
  @name=N'MS_Description', @value=N'Guid',
  @level0type=N'SCHEMA', @level0name=N'dbo',
  @level1type=N'TABLE', @level1name=N'tmp_idem_gate_test',
  @level2type=N'COLUMN', @level2name=N'g';
EXEC sp_addextendedproperty
  @name=N'MS_Description', @value=N'Nullable guid',
  @level0type=N'SCHEMA', @level0name=N'dbo',
  @level1type=N'TABLE', @level1name=N'tmp_idem_gate_test',
  @level2type=N'COLUMN', @level2name=N'g_null';
EXEC sp_addextendedproperty
  @name=N'MS_Description', @value=N'Bytes',
  @level0type=N'SCHEMA', @level0name=N'dbo',
  @level1type=N'TABLE', @level1name=N'tmp_idem_gate_test',
  @level2type=N'COLUMN', @level2name=N'bin';
-- Move data
DECLARE @polhem_last_identity numeric(38, 0) = (SELECT CONVERT(numeric(38, 0), last_value) FROM sys.identity_columns WHERE object_id = OBJECT_ID(N'[idem_gate_test]') AND name = N'sys_no');
SET IDENTITY_INSERT [tmp_idem_gate_test] ON;
INSERT INTO [tmp_idem_gate_test] ([sys_no], [s], [s_def], [s_null], [code], [t], [t_null], [b], [b_def], [sh], [i], [i_def], [i_null], [l], [d], [d_def], [c], [dt], [dt_null], [dtm], [tm], [sys_rowid], [g], [g_null], [bin])
SELECT [sys_no], [s], [s_def], [s_null], [code], [t], [t_null], [b], [b_def], [sh], [i], [i_def], [i_null], [l], [d], [d_def], [c], [dt], [dt_null], [dtm], [tm], [sys_rowid], [g], [g_null], [bin] FROM [idem_gate_test];
SET IDENTITY_INSERT [tmp_idem_gate_test] OFF;
DECLARE @polhem_reseed nvarchar(200) = N'DBCC CHECKIDENT (N''[tmp_idem_gate_test]'', RESEED, ' + CAST(@polhem_last_identity + CASE WHEN EXISTS (SELECT 1 FROM [tmp_idem_gate_test]) THEN 0 ELSE 1 END AS nvarchar(40)) + N') WITH NO_INFOMSGS';
IF @polhem_last_identity IS NOT NULL AND @polhem_last_identity > ISNULL((SELECT MAX([sys_no]) FROM [tmp_idem_gate_test]), 0)
    EXEC (@polhem_reseed);
-- Drop old table
IF (SELECT COUNT(*) From sys.tables WHERE name=N'idem_gate_test')>0
  DROP TABLE [idem_gate_test];
-- Rename temporary table
EXEC sp_rename N'dbo.tmp_idem_gate_test.pk_tmp_idem_gate_test', N'pk_idem_gate_test', N'INDEX';
EXEC sp_rename N'dbo.tmp_idem_gate_test.ix_tmp_idem_gate_test_s', N'ix_idem_gate_test_s', N'INDEX';
EXEC sp_rename N'dbo.tmp_idem_gate_test.uk_tmp_idem_gate_test_i_def', N'uk_idem_gate_test_i_def', N'INDEX';
EXEC sp_rename N'tmp_idem_gate_test', N'idem_gate_test';

