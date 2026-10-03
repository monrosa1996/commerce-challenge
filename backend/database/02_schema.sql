/*
  Step 2 of 2. Run it after step 1. Safe to run more than once.
  Sections: 2 tables, 3 motivo column, 4 table type and validation function,
            5 procedures, 6 application user, 7 users and audit (login).
  This file is ASCII on purpose: any client reads it the same way.
*/

USE CommerceDb;
GO

-- 2. Tables (commerce_quarantine is created without motivo, see section 3)
IF OBJECT_ID(N'dbo.commerce', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.commerce (
        id             BIGINT IDENTITY(1, 1) NOT NULL CONSTRAINT PK_commerce PRIMARY KEY,
        source_file    NVARCHAR(100) NOT NULL,
        pc_processdate DATE          NOT NULL,
        pc_codcomred   NVARCHAR(20)  NOT NULL,
        pc_nomcomred   NVARCHAR(150) NULL,
        pc_tipdoc      NVARCHAR(10)  NULL,
        pc_numdoc      NVARCHAR(30)  NULL,
        created_at     DATETIME2(0)  NOT NULL CONSTRAINT DF_commerce_created_at DEFAULT SYSUTCDATETIME()
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_commerce_key' AND object_id = OBJECT_ID(N'dbo.commerce'))
    CREATE UNIQUE INDEX UX_commerce_key ON dbo.commerce (pc_processdate, pc_codcomred);
GO

IF OBJECT_ID(N'dbo.commerce_quarantine', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.commerce_quarantine (
        id             BIGINT IDENTITY(1, 1) NOT NULL CONSTRAINT PK_commerce_quarantine PRIMARY KEY,
        source_file    NVARCHAR(100) NOT NULL,
        pc_processdate DATE          NOT NULL,
        pc_codcomred   NVARCHAR(20)  NOT NULL,
        pc_nomcomred   NVARCHAR(150) NULL,
        pc_tipdoc      NVARCHAR(10)  NULL,
        pc_numdoc      NVARCHAR(30)  NULL,
        quarantined_at DATETIME2(0)  NOT NULL CONSTRAINT DF_quarantine_quarantined_at DEFAULT SYSUTCDATETIME()
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_quarantine_processdate' AND object_id = OBJECT_ID(N'dbo.commerce_quarantine'))
    CREATE INDEX IX_quarantine_processdate ON dbo.commerce_quarantine (pc_processdate);
GO

-- 3. Requested change: add the motivo column to commerce_quarantine
IF COL_LENGTH(N'dbo.commerce_quarantine', N'motivo') IS NULL
    ALTER TABLE dbo.commerce_quarantine
        ADD motivo NVARCHAR(500) NOT NULL CONSTRAINT DF_quarantine_motivo DEFAULT (N'');
GO

-- 4. Table type for bulk insert and the validation rule
IF TYPE_ID(N'dbo.CommerceTableType') IS NULL
    CREATE TYPE dbo.CommerceTableType AS TABLE (
        pc_processdate DATE          NOT NULL,
        pc_codcomred   NVARCHAR(20)  NOT NULL,
        pc_nomcomred   NVARCHAR(150) NULL,
        pc_tipdoc      NVARCHAR(10)  NULL,
        pc_numdoc      NVARCHAR(30)  NULL
    );
GO

-- Returns motivo (NULL when the row is valid). NCHAR(237) is i with accent, NCHAR(250) is u with accent.
CREATE OR ALTER FUNCTION dbo.fn_commerce_validation (@nomcomred NVARCHAR(150), @numdoc NVARCHAR(30))
RETURNS TABLE
AS
RETURN
    SELECT NULLIF(CONCAT_WS(N'; ',
        CASE WHEN TRIM(ISNULL(@nomcomred, N'')) = N''
             THEN N'El nombre del comercio (nomcomred) se encuentra vac' + NCHAR(237) + N'o' END,
        CASE WHEN TRIM(ISNULL(@numdoc, N'')) = N''
             THEN N'El n' + NCHAR(250) + N'mero (numdoc) se encuentra vac' + NCHAR(237) + N'o' END,
        CASE WHEN @numdoc LIKE N'%[A-Za-z]%'
             THEN N'El n' + NCHAR(250) + N'mero (numdoc) contiene letras' END,
        CASE WHEN TRIM(@numdoc) LIKE N'%[^0-9A-Za-z]%'
             THEN N'El n' + NCHAR(250) + N'mero (numdoc) contiene caracteres especiales' END
    ), N'') AS motivo;
GO

-- 5. Procedures
-- Inserts all rows or none. If a key already exists it fails with 50001 (the API answers 409).
CREATE OR ALTER PROCEDURE dbo.sp_create_commerce
    @SourceFile NVARCHAR(100),
    @Rows       dbo.CommerceTableType READONLY
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF EXISTS (
        SELECT 1
        FROM @Rows AS r
        INNER JOIN dbo.commerce AS c
            ON c.pc_processdate = r.pc_processdate AND c.pc_codcomred = r.pc_codcomred
    )
    BEGIN
        DECLARE @Message NVARCHAR(2048);

        SELECT @Message = CONCAT(N'Hay registros que ya existen en el sistema (fecha|c', NCHAR(243), N'digo): ', STRING_AGG(k.existing_key, N', '))
        FROM (
            SELECT TOP (20) CONCAT(CONVERT(CHAR(10), r.pc_processdate, 23), N'|', r.pc_codcomred) AS existing_key
            FROM @Rows AS r
            INNER JOIN dbo.commerce AS c
                ON c.pc_processdate = r.pc_processdate AND c.pc_codcomred = r.pc_codcomred
            ORDER BY r.pc_processdate, r.pc_codcomred
        ) AS k;

        THROW 50001, @Message, 1;
    END

    INSERT INTO dbo.commerce (source_file, pc_processdate, pc_codcomred, pc_nomcomred, pc_tipdoc, pc_numdoc)
    SELECT @SourceFile, r.pc_processdate, r.pc_codcomred, r.pc_nomcomred, r.pc_tipdoc, r.pc_numdoc
    FROM @Rows AS r;

    SELECT @@ROWCOUNT AS InsertedCount;
END
GO

-- Validates the records of one date with fn_commerce_validation and moves the ones that fail to
-- commerce_quarantine, with their motivo, in a single statement. Returns how many were moved.
CREATE OR ALTER PROCEDURE dbo.sp_process_commerce
    @ProcessDate DATE
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DELETE c
    OUTPUT deleted.source_file, deleted.pc_processdate, deleted.pc_codcomred,
           deleted.pc_nomcomred, deleted.pc_tipdoc, deleted.pc_numdoc, v.motivo
    INTO dbo.commerce_quarantine (source_file, pc_processdate, pc_codcomred,
           pc_nomcomred, pc_tipdoc, pc_numdoc, motivo)
    FROM dbo.commerce AS c
    CROSS APPLY dbo.fn_commerce_validation(c.pc_nomcomred, c.pc_numdoc) AS v
    WHERE c.pc_processdate = @ProcessDate
      AND v.motivo IS NOT NULL;

    SELECT @@ROWCOUNT AS QuarantinedCount;
END
GO

-- Lists the records in commerce. Before a date is processed it holds everything that was loaded;
-- after processing, only the records that passed the validation remain.
CREATE OR ALTER PROCEDURE dbo.sp_list_commerce
    @ProcessDate DATE = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    SELECT TOP (5000)
        id             AS Id,
        source_file    AS SourceFile,
        pc_processdate AS ProcessDate,
        pc_codcomred   AS CodComRed,
        pc_nomcomred   AS NomComRed,
        pc_tipdoc      AS TipDoc,
        pc_numdoc      AS NumDoc,
        created_at     AS CreatedAt
    FROM dbo.commerce
    WHERE @ProcessDate IS NULL OR pc_processdate = @ProcessDate
    ORDER BY pc_processdate DESC, pc_codcomred
    OPTION (RECOMPILE);
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_list_commerce_quarantine
    @ProcessDate DATE = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    SELECT TOP (5000)
        id             AS Id,
        source_file    AS SourceFile,
        pc_processdate AS ProcessDate,
        pc_codcomred   AS CodComRed,
        pc_nomcomred   AS NomComRed,
        pc_tipdoc      AS TipDoc,
        pc_numdoc      AS NumDoc,
        motivo         AS Motivo,
        quarantined_at AS QuarantinedAt
    FROM dbo.commerce_quarantine
    WHERE @ProcessDate IS NULL OR pc_processdate = @ProcessDate
    ORDER BY quarantined_at DESC, id DESC
    OPTION (RECOMPILE);
END
GO

-- 6. Application user: EXECUTE only, no table access
IF DATABASE_PRINCIPAL_ID(N'commerce_app') IS NULL
    CREATE USER commerce_app FOR LOGIN commerce_app;
GO

GRANT EXECUTE ON SCHEMA::dbo TO commerce_app;
GRANT EXECUTE ON TYPE::dbo.CommerceTableType TO commerce_app;
GO

-- 7. Users and audit. app_user holds the people who can log in; audit_log holds what they do
-- (login, logout, file upload, process date) with the request and the response of each action.
-- Passwords are never stored: password_hash is PBKDF2-SHA256 (format PBKDF2-SHA256$iterations$salt$hash).
IF OBJECT_ID(N'dbo.app_user', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.app_user (
        id            INT IDENTITY(1, 1) NOT NULL CONSTRAINT PK_app_user PRIMARY KEY,
        username      NVARCHAR(50)  NOT NULL,
        display_name  NVARCHAR(100) NOT NULL,
        password_hash NVARCHAR(200) NOT NULL,
        is_active     BIT           NOT NULL CONSTRAINT DF_app_user_is_active DEFAULT (1),
        created_at    DATETIME2(0)  NOT NULL CONSTRAINT DF_app_user_created_at DEFAULT SYSUTCDATETIME()
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_app_user_username' AND object_id = OBJECT_ID(N'dbo.app_user'))
    CREATE UNIQUE INDEX UX_app_user_username ON dbo.app_user (username);
GO

IF OBJECT_ID(N'dbo.audit_log', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.audit_log (
        id               BIGINT IDENTITY(1, 1) NOT NULL CONSTRAINT PK_audit_log PRIMARY KEY,
        occurred_at      DATETIME2(3)  NOT NULL CONSTRAINT DF_audit_log_occurred_at DEFAULT SYSUTCDATETIME(),
        user_id          INT           NULL CONSTRAINT FK_audit_log_user REFERENCES dbo.app_user (id),
        username         NVARCHAR(50)  NOT NULL,
        action           NVARCHAR(20)  NOT NULL
            CONSTRAINT CK_audit_log_action CHECK (action IN (N'Login', N'LoginFailed', N'Logout', N'Upload', N'Process')),
        status_code      INT           NOT NULL,
        ip_address       NVARCHAR(45)  NULL,
        request_payload  NVARCHAR(MAX) NULL,
        response_payload NVARCHAR(MAX) NULL
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_audit_log_occurred_at' AND object_id = OBJECT_ID(N'dbo.audit_log'))
    CREATE INDEX IX_audit_log_occurred_at ON dbo.audit_log (occurred_at);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_audit_log_user' AND object_id = OBJECT_ID(N'dbo.audit_log'))
    CREATE INDEX IX_audit_log_user ON dbo.audit_log (user_id, occurred_at);
GO

-- Returns the user to check the login. The API compares the password hash.
CREATE OR ALTER PROCEDURE dbo.sp_auth_get_user
    @Username NVARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    SELECT
        id            AS Id,
        username      AS Username,
        display_name  AS DisplayName,
        password_hash AS PasswordHash,
        is_active     AS IsActive
    FROM dbo.app_user
    WHERE username = @Username;
END
GO

-- Adds one audit entry. @UserId is NULL when the user is unknown (a failed login with a wrong username).
CREATE OR ALTER PROCEDURE dbo.sp_audit_add
    @UserId          INT = NULL,
    @Username        NVARCHAR(50),
    @Action          NVARCHAR(20),
    @StatusCode      INT,
    @IpAddress       NVARCHAR(45) = NULL,
    @RequestPayload  NVARCHAR(MAX) = NULL,
    @ResponsePayload NVARCHAR(MAX) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    INSERT INTO dbo.audit_log (user_id, username, action, status_code, ip_address, request_payload, response_payload)
    VALUES (@UserId, @Username, @Action, @StatusCode, @IpAddress, @RequestPayload, @ResponsePayload);
END
GO

-- Development users (passwords: see SECURITY.md). Change or remove them outside development.
IF NOT EXISTS (SELECT 1 FROM dbo.app_user WHERE username = N'admin')
    INSERT INTO dbo.app_user (username, display_name, password_hash)
    VALUES (N'admin', N'Administrador', N'PBKDF2-SHA256$600000$H6CqPoHuFajzOtpH9Whd1w==$9mFvoWfKIe2BkMRW6h8rEem6921Ss/55K42xoj+o/wY=');
GO

IF NOT EXISTS (SELECT 1 FROM dbo.app_user WHERE username = N'analista')
    INSERT INTO dbo.app_user (username, display_name, password_hash)
    VALUES (N'analista', N'Analista de comercios', N'PBKDF2-SHA256$600000$A2347wM0OsdTF5tkPfMABw==$XO/3aCLmRUt0XFVttigc/d/7CNbg5NpFIcam/xjC+dA=');
GO
