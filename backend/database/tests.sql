/*
  Database tests. Every test runs inside a transaction that is rolled back,
  so no data is left behind. Any failure raises an error (THROW 51xxx).
  Run after 02_schema.sql, as sa or any admin login.
*/
USE CommerceDb;
GO

SET NOCOUNT ON;

-- Test 1: create 10 rows, process them, 5 go to quarantine
BEGIN TRY
    BEGIN TRANSACTION;

    DECLARE @rows1 dbo.CommerceTableType;
    DECLARE @result1 TABLE (value INT);

    INSERT INTO @rows1 (pc_processdate, pc_codcomred, pc_nomcomred, pc_tipdoc, pc_numdoc) VALUES
        ('2099-01-01', N'1001', N'Ferreteria El Puerto', N'RUC', N'0912345678001'),
        ('2099-01-01', N'1002', N'Farmacia Central',     N'CED', N'0923456789'),
        ('2099-01-01', N'1003', N'Panaderia La Espiga',  N'RUC', N'0934567890001'),
        ('2099-01-01', N'1004', N'Libreria Sur',         N'CED', N'0945678901'),
        ('2099-01-01', N'1005', N'Optica Vision',        N'RUC', N'0956789012001'),
        ('2099-01-01', N'1006', NULL,                    N'CED', N'0967890123'),
        ('2099-01-01', N'1007', N'Bazar Norte',          N'CED', NULL),
        ('2099-01-01', N'1008', N'Tienda Aurora',        N'CED', N'09A7890123'),
        ('2099-01-01', N'1009', N'Comercial Rios',       N'RUC', N'0912-345678'),
        ('2099-01-01', N'1010', NULL,                    N'RUC', N'12A-45');

    INSERT INTO @result1 EXEC dbo.sp_create_commerce @SourceFile = N'commerce_01012099.csv', @Rows = @rows1;
    IF (SELECT TOP (1) value FROM @result1) <> 10
        THROW 51001, 'Test 1 failed: sp_create_commerce should insert 10 rows.', 1;

    DELETE FROM @result1;
    INSERT INTO @result1 EXEC dbo.sp_process_commerce @ProcessDate = '2099-01-01';
    IF (SELECT TOP (1) value FROM @result1) <> 5
        THROW 51002, 'Test 1 failed: sp_process_commerce should quarantine 5 rows.', 1;

    IF (SELECT COUNT(*) FROM dbo.commerce WHERE pc_processdate = '2099-01-01') <> 5
        THROW 51003, 'Test 1 failed: 5 valid rows should stay in commerce.', 1;

    IF (SELECT COUNT(*) FROM dbo.commerce_quarantine WHERE pc_processdate = '2099-01-01' AND motivo <> N'') <> 5
        THROW 51004, 'Test 1 failed: every quarantined row should have a motivo.', 1;

    -- Row 1010 has three problems, so motivo has three parts (two separators of two characters)
    IF (SELECT LEN(motivo) - LEN(REPLACE(motivo, N'; ', N''))
        FROM dbo.commerce_quarantine
        WHERE pc_processdate = '2099-01-01' AND pc_codcomred = N'1010') <> 4
        THROW 51005, 'Test 1 failed: row 1010 should have three motivos.', 1;

    DELETE FROM @result1;
    INSERT INTO @result1 EXEC dbo.sp_process_commerce @ProcessDate = '2099-01-01';
    IF (SELECT TOP (1) value FROM @result1) <> 0
        THROW 51006, 'Test 1 failed: a second process should quarantine 0 rows.', 1;

    ROLLBACK TRANSACTION;
    PRINT 'Test 1 passed: create and process';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO

-- Test 2: validation function
BEGIN TRY
    IF (SELECT motivo FROM dbo.fn_commerce_validation(N'Bazar', N'0912345678')) IS NOT NULL
        THROW 51010, 'Test 2 failed: a valid row should return NULL.', 1;

    IF (SELECT motivo FROM dbo.fn_commerce_validation(N'   ', N'123')) IS NULL
        THROW 51011, 'Test 2 failed: a blank name should return a motivo.', 1;

    IF (SELECT motivo FROM dbo.fn_commerce_validation(N'Bazar', N'12A')) IS NULL
        THROW 51012, 'Test 2 failed: letters in numdoc should return a motivo.', 1;

    IF (SELECT motivo FROM dbo.fn_commerce_validation(N'Bazar', N'12-34')) IS NULL
        THROW 51013, 'Test 2 failed: special characters in numdoc should return a motivo.', 1;

    PRINT 'Test 2 passed: validation function';
END TRY
BEGIN CATCH
    THROW;
END CATCH;
GO

-- Test 3: loading an existing key fails with 50001 and keeps the first load.
-- The failing call dooms the transaction, so this test ends with the rollback.
DECLARE @rows3 dbo.CommerceTableType;
DECLARE @result3 TABLE (value INT);
DECLARE @failed BIT = 0;

INSERT INTO @rows3 (pc_processdate, pc_codcomred, pc_nomcomred, pc_tipdoc, pc_numdoc) VALUES
    ('2099-01-02', N'2001', N'Bazar Sur', N'CED', N'0911111111');

BEGIN TRANSACTION;

INSERT INTO @result3 EXEC dbo.sp_create_commerce @SourceFile = N'commerce_02012099.csv', @Rows = @rows3;

BEGIN TRY
    EXEC dbo.sp_create_commerce @SourceFile = N'commerce_02012099.csv', @Rows = @rows3;
END TRY
BEGIN CATCH
    IF ERROR_NUMBER() = 50001 SET @failed = 1;
END CATCH;

IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;

IF @failed = 0
    THROW 51020, 'Test 3 failed: loading an existing key should fail with error 50001.', 1;

PRINT 'Test 3 passed: existing key is rejected';
GO

-- Test 4: motivo column exists
IF COL_LENGTH(N'dbo.commerce_quarantine', N'motivo') IS NULL
    THROW 51030, 'Test 4 failed: commerce_quarantine.motivo is missing.', 1;
PRINT 'Test 4 passed: motivo column';
GO

-- Test 5: sp_list_commerce lists what is in commerce, before and after processing
BEGIN TRY
    BEGIN TRANSACTION;

    DECLARE @rows5 dbo.CommerceTableType;
    DECLARE @created5 TABLE (value INT);
    DECLARE @processed5 TABLE (value INT);
    DECLARE @before5 TABLE (
        Id BIGINT, SourceFile NVARCHAR(100), ProcessDate DATE, CodComRed NVARCHAR(20),
        NomComRed NVARCHAR(150), TipDoc NVARCHAR(10), NumDoc NVARCHAR(30), CreatedAt DATETIME2(0));
    DECLARE @after5 TABLE (
        Id BIGINT, SourceFile NVARCHAR(100), ProcessDate DATE, CodComRed NVARCHAR(20),
        NomComRed NVARCHAR(150), TipDoc NVARCHAR(10), NumDoc NVARCHAR(30), CreatedAt DATETIME2(0));

    INSERT INTO @rows5 (pc_processdate, pc_codcomred, pc_nomcomred, pc_tipdoc, pc_numdoc) VALUES
        ('2099-01-03', N'3001', N'Bazar Sur', N'CED', N'0911111111'),
        ('2099-01-03', N'3002', NULL,         N'CED', N'12A');

    INSERT INTO @created5 EXEC dbo.sp_create_commerce @SourceFile = N'commerce_03012099.csv', @Rows = @rows5;
    INSERT INTO @before5 EXEC dbo.sp_list_commerce @ProcessDate = '2099-01-03';

    IF (SELECT COUNT(*) FROM @before5) <> 2
        THROW 51040, 'Test 5 failed: before processing, sp_list_commerce should return the 2 loaded rows.', 1;

    INSERT INTO @processed5 EXEC dbo.sp_process_commerce @ProcessDate = '2099-01-03';
    INSERT INTO @after5 EXEC dbo.sp_list_commerce @ProcessDate = '2099-01-03';

    IF (SELECT COUNT(*) FROM @after5) <> 1 OR (SELECT COUNT(*) FROM @after5 WHERE CodComRed = N'3001') <> 1
        THROW 51041, 'Test 5 failed: after processing, only the correct row should remain.', 1;

    ROLLBACK TRANSACTION;
    PRINT 'Test 5 passed: list commerce before and after processing';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO

-- Test 6: users and audit
BEGIN TRY
    BEGIN TRANSACTION;

    DECLARE @user6 TABLE (Id INT, Username NVARCHAR(50), DisplayName NVARCHAR(100), PasswordHash NVARCHAR(200), IsActive BIT);
    DECLARE @missing6 TABLE (Id INT, Username NVARCHAR(50), DisplayName NVARCHAR(100), PasswordHash NVARCHAR(200), IsActive BIT);
    DECLARE @adminId INT;

    INSERT INTO @user6 EXEC dbo.sp_auth_get_user @Username = N'admin';
    IF (SELECT COUNT(*) FROM @user6) <> 1 OR (SELECT TOP (1) PasswordHash FROM @user6) NOT LIKE N'PBKDF2-SHA256$%'
        THROW 51060, 'Test 6 failed: the development user admin should exist with a PBKDF2 hash.', 1;

    INSERT INTO @missing6 EXEC dbo.sp_auth_get_user @Username = N'nobody';
    IF EXISTS (SELECT 1 FROM @missing6)
        THROW 51061, 'Test 6 failed: an unknown user should return no rows.', 1;

    SELECT @adminId = Id FROM @user6;

    EXEC dbo.sp_audit_add @UserId = @adminId, @Username = N'admin', @Action = N'Upload', @StatusCode = 201,
        @IpAddress = N'127.0.0.1', @RequestPayload = N'{"fileName":"commerce_01012099.csv"}',
        @ResponsePayload = N'{"insertedCount":10}';
    EXEC dbo.sp_audit_add @UserId = NULL, @Username = N'nobody', @Action = N'LoginFailed', @StatusCode = 401,
        @RequestPayload = N'{"username":"nobody"}', @ResponsePayload = N'{"success":false}';

    IF (SELECT COUNT(*) FROM dbo.audit_log WHERE username IN (N'admin', N'nobody') AND occurred_at >= DATEADD(MINUTE, -5, SYSUTCDATETIME())) <> 2
        THROW 51062, 'Test 6 failed: the two audit entries should be saved.', 1;

    IF (SELECT COUNT(*) FROM dbo.audit_log WHERE action = N'Upload' AND response_payload = N'{"insertedCount":10}' AND user_id = @adminId) <> 1
        THROW 51063, 'Test 6 failed: the request and response payloads should be saved.', 1;

    ROLLBACK TRANSACTION;
    PRINT 'Test 6 passed: users and audit';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO

-- Test 7: an unknown audit action and a repeated username are rejected.
-- Each failing call dooms its transaction, so each part ends with the rollback.
DECLARE @action7 BIT = 0;
BEGIN TRANSACTION;
BEGIN TRY
    EXEC dbo.sp_audit_add @UserId = NULL, @Username = N'x', @Action = N'Other', @StatusCode = 200;
END TRY
BEGIN CATCH
    IF ERROR_NUMBER() = 547 SET @action7 = 1;
END CATCH;
IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
IF @action7 = 0
    THROW 51070, 'Test 7 failed: an unknown audit action should be rejected.', 1;

DECLARE @user7 BIT = 0;
BEGIN TRANSACTION;
BEGIN TRY
    INSERT INTO dbo.app_user (username, display_name, password_hash) VALUES (N'ADMIN', N'Duplicate', N'x');
END TRY
BEGIN CATCH
    IF ERROR_NUMBER() = 2601 SET @user7 = 1;
END CATCH;
IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
IF @user7 = 0
    THROW 51071, 'Test 7 failed: a repeated username should be rejected.', 1;

PRINT 'Test 7 passed: audit action and username are checked';
GO

PRINT 'All database tests passed.';
GO
