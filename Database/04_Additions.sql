USE mori;
GO
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

CREATE OR ALTER PROCEDURE dbo.usp_User_List
AS
BEGIN
    SET NOCOUNT ON;

    SELECT UserId, Username, FullName, RoleId, IsActive, LastLoginAt
    FROM dbo.Users
    ORDER BY Username;
END
GO

CREATE OR ALTER PROCEDURE dbo.usp_User_Create
    @Username     NVARCHAR(50),
    @PasswordHash VARCHAR(64),
    @Salt         VARCHAR(32),
    @FullName     NVARCHAR(100),
    @RoleId       INT
AS
BEGIN
    SET NOCOUNT ON;

    INSERT dbo.Users (Username, PasswordHash, Salt, FullName, RoleId)
    VALUES (@Username, @PasswordHash, @Salt, @FullName, @RoleId);

    SELECT CAST(SCOPE_IDENTITY() AS INT) AS Id;
END
GO

CREATE OR ALTER PROCEDURE dbo.usp_User_Update
    @UserId   INT,
    @FullName NVARCHAR(100),
    @RoleId   INT,
    @IsActive BIT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRAN;

    IF NOT EXISTS (SELECT 1 FROM dbo.Users WHERE UserId = @UserId)
        THROW 50030, N'User not found.', 1;

    IF EXISTS (SELECT 1 FROM dbo.Users WHERE UserId = @UserId AND RoleId = 1 AND IsActive = 1)
       AND (@RoleId <> 1 OR @IsActive = 0)
       AND NOT EXISTS (SELECT 1 FROM dbo.Users WITH (UPDLOCK, HOLDLOCK) WHERE UserId <> @UserId AND RoleId = 1 AND IsActive = 1)
        THROW 50031, N'At least one active admin must remain.', 1;

    UPDATE dbo.Users
    SET FullName = @FullName, RoleId = @RoleId, IsActive = @IsActive
    WHERE UserId = @UserId;

    COMMIT;
END
GO

CREATE OR ALTER PROCEDURE dbo.usp_User_ResetPassword
    @UserId       INT,
    @PasswordHash VARCHAR(64),
    @Salt         VARCHAR(32)
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE dbo.Users
    SET PasswordHash = @PasswordHash, Salt = @Salt
    WHERE UserId = @UserId;

    IF @@ROWCOUNT = 0
        THROW 50030, N'User not found.', 1;
END
GO
