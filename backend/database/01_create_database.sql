/*
  Step 1 of 2. Run it connected to the master database (or any database: it switches to master).
  Creates CommerceDb and the application login. Safe to run more than once.
  The login password is for development only, see SECURITY.md.
  This file is ASCII on purpose: any client reads it the same way.
*/

USE master;
GO

-- 1. Database
IF DB_ID(N'CommerceDb') IS NULL
    CREATE DATABASE CommerceDb;
GO

-- Application login: EXECUTE permission only (granted in step 2)
IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = N'commerce_app')
    CREATE LOGIN commerce_app WITH PASSWORD = N'Commerce_App#2026', CHECK_POLICY = OFF;
GO
