/*
  Loads the base file (commerce_15032024.csv) straight into dbo.commerce,
  so the process step can be tested without the API. Safe to run more than once.
  Do not use together with the upload of the same file: the upload would answer 409.
*/
USE CommerceDb;
GO

SET NOCOUNT ON;

INSERT INTO dbo.commerce (source_file, pc_processdate, pc_codcomred, pc_nomcomred, pc_tipdoc, pc_numdoc)
SELECT s.source_file, s.pc_processdate, s.pc_codcomred, s.pc_nomcomred, s.pc_tipdoc, s.pc_numdoc
FROM (VALUES
    (N'commerce_15032024.csv', CAST('2024-03-15' AS DATE), N'1001', N'Ferreteria El Puerto', N'RUC', N'0912345678001'),
    (N'commerce_15032024.csv', CAST('2024-03-15' AS DATE), N'1002', N'Farmacia Central',     N'CED', N'0923456789'),
    (N'commerce_15032024.csv', CAST('2024-03-15' AS DATE), N'1003', N'Panaderia La Espiga',   N'RUC', N'0934567890001'),
    (N'commerce_15032024.csv', CAST('2024-03-15' AS DATE), N'1004', N'Libreria Sur',          N'CED', N'0945678901'),
    (N'commerce_15032024.csv', CAST('2024-03-15' AS DATE), N'1005', N'Optica Vision',         N'RUC', N'0956789012001'),
    (N'commerce_15032024.csv', CAST('2024-03-15' AS DATE), N'1006', NULL,                     N'CED', N'0967890123'),
    (N'commerce_15032024.csv', CAST('2024-03-15' AS DATE), N'1007', N'Bazar Norte',           N'CED', NULL),
    (N'commerce_15032024.csv', CAST('2024-03-15' AS DATE), N'1008', N'Tienda Aurora',         N'CED', N'09A7890123'),
    (N'commerce_15032024.csv', CAST('2024-03-15' AS DATE), N'1009', N'Comercial Rios',        N'RUC', N'0912-345678'),
    (N'commerce_15032024.csv', CAST('2024-03-15' AS DATE), N'1010', NULL,                     N'RUC', N'12A-45')
) AS s (source_file, pc_processdate, pc_codcomred, pc_nomcomred, pc_tipdoc, pc_numdoc)
WHERE NOT EXISTS (
    SELECT 1
    FROM dbo.commerce AS c
    WHERE c.pc_processdate = s.pc_processdate AND c.pc_codcomred = s.pc_codcomred
);

SELECT COUNT(*) AS commerce_rows FROM dbo.commerce;
GO
