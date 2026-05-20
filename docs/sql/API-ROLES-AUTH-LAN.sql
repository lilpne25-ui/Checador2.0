/*
Prompt 3 - Roles basicos + auth API LAN
Objetivo:
 - Crear tablas: ApiUsers, ApiRoles, ApiUserRoles (idempotente)
 - Roles iniciales: consulta, admin
 - Semilla admin temporal con cambio obligatorio de password
 - No tocar Employees, PunchLogs, SyncState
*/

SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRAN;

    IF OBJECT_ID('dbo.ApiUsers', 'U') IS NULL
    BEGIN
        CREATE TABLE dbo.ApiUsers
        (
            UserId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ApiUsers PRIMARY KEY,
            Username NVARCHAR(100) NOT NULL,
            PasswordSalt VARBINARY(16) NOT NULL,
            PasswordHash VARBINARY(32) NOT NULL,
            MustChangePassword BIT NOT NULL CONSTRAINT DF_ApiUsers_MustChangePassword DEFAULT (1),
            IsActive BIT NOT NULL CONSTRAINT DF_ApiUsers_IsActive DEFAULT (1),
            LastLoginAt DATETIME2 NULL,
            CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_ApiUsers_CreatedAt DEFAULT SYSUTCDATETIME(),
            UpdatedAt DATETIME2 NOT NULL CONSTRAINT DF_ApiUsers_UpdatedAt DEFAULT SYSUTCDATETIME()
        );

        CREATE UNIQUE INDEX UX_ApiUsers_Username ON dbo.ApiUsers(Username);
    END;

    IF OBJECT_ID('dbo.ApiRoles', 'U') IS NULL
    BEGIN
        CREATE TABLE dbo.ApiRoles
        (
            RoleId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ApiRoles PRIMARY KEY,
            RoleName NVARCHAR(50) NOT NULL,
            IsActive BIT NOT NULL CONSTRAINT DF_ApiRoles_IsActive DEFAULT (1),
            CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_ApiRoles_CreatedAt DEFAULT SYSUTCDATETIME()
        );

        CREATE UNIQUE INDEX UX_ApiRoles_RoleName ON dbo.ApiRoles(RoleName);
    END;

    IF OBJECT_ID('dbo.ApiUserRoles', 'U') IS NULL
    BEGIN
        CREATE TABLE dbo.ApiUserRoles
        (
            UserId INT NOT NULL,
            RoleId INT NOT NULL,
            CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_ApiUserRoles_CreatedAt DEFAULT SYSUTCDATETIME(),
            CONSTRAINT PK_ApiUserRoles PRIMARY KEY (UserId, RoleId),
            CONSTRAINT FK_ApiUserRoles_ApiUsers FOREIGN KEY (UserId) REFERENCES dbo.ApiUsers(UserId),
            CONSTRAINT FK_ApiUserRoles_ApiRoles FOREIGN KEY (RoleId) REFERENCES dbo.ApiRoles(RoleId)
        );
    END;

    IF NOT EXISTS (SELECT 1 FROM dbo.ApiRoles WHERE RoleName = N'consulta')
    BEGIN
        INSERT INTO dbo.ApiRoles (RoleName, IsActive) VALUES (N'consulta', 1);
    END;

    IF NOT EXISTS (SELECT 1 FROM dbo.ApiRoles WHERE RoleName = N'admin')
    BEGIN
        INSERT INTO dbo.ApiRoles (RoleName, IsActive) VALUES (N'admin', 1);
    END;

    DECLARE @AdminUsername NVARCHAR(100) = N'admin';
    DECLARE @AdminTempPassword NVARCHAR(200) = N'CAMBIAR_ADMIN_TEMP';
    DECLARE @ResetAdminPassword BIT = 0; -- 1 = regenera hash temporal y obliga cambio
    DECLARE @AdminUserId INT;
    DECLARE @AdminRoleId INT = (SELECT TOP (1) RoleId FROM dbo.ApiRoles WHERE RoleName = N'admin');

    IF @AdminTempPassword = N'CAMBIAR_ADMIN_TEMP'
    BEGIN
        THROW 50010, 'Define @AdminTempPassword antes de ejecutar el script.', 1;
    END;

    IF @AdminRoleId IS NULL
    BEGIN
        THROW 50001, 'No existe rol admin en ApiRoles.', 1;
    END;

    SELECT @AdminUserId = UserId
    FROM dbo.ApiUsers
    WHERE Username = @AdminUsername;

    IF @AdminUserId IS NULL
    BEGIN
        DECLARE @Salt VARBINARY(16) = CRYPT_GEN_RANDOM(16);
        DECLARE @Hash VARBINARY(32) = HASHBYTES('SHA2_256', @Salt + CONVERT(VARBINARY(4000), @AdminTempPassword));

        INSERT INTO dbo.ApiUsers
        (
            Username,
            PasswordSalt,
            PasswordHash,
            MustChangePassword,
            IsActive
        )
        VALUES
        (
            @AdminUsername,
            @Salt,
            @Hash,
            1,
            1
        );

        SET @AdminUserId = SCOPE_IDENTITY();
    END;
    ELSE IF @ResetAdminPassword = 1
    BEGIN
        DECLARE @NewSalt VARBINARY(16) = CRYPT_GEN_RANDOM(16);
        DECLARE @NewHash VARBINARY(32) = HASHBYTES('SHA2_256', @NewSalt + CONVERT(VARBINARY(4000), @AdminTempPassword));

        UPDATE dbo.ApiUsers
        SET PasswordSalt = @NewSalt,
            PasswordHash = @NewHash,
            MustChangePassword = 1,
            IsActive = 1,
            UpdatedAt = SYSUTCDATETIME()
        WHERE UserId = @AdminUserId;
    END;

    IF NOT EXISTS
    (
        SELECT 1
        FROM dbo.ApiUserRoles
        WHERE UserId = @AdminUserId
          AND RoleId = @AdminRoleId
    )
    BEGIN
        INSERT INTO dbo.ApiUserRoles (UserId, RoleId)
        VALUES (@AdminUserId, @AdminRoleId);
    END;

    DECLARE @SeedConsultaUser BIT = 1; -- dejar en 0 si no quieres usuario demo
    IF @SeedConsultaUser = 1
    BEGIN
        DECLARE @ConsultaUsername NVARCHAR(100) = N'consulta_demo';
        DECLARE @ConsultaPassword NVARCHAR(200) = N'CAMBIAR_CONSULTA_TEMP';
        DECLARE @ConsultaUserId INT;
        DECLARE @ConsultaRoleId INT = (SELECT TOP (1) RoleId FROM dbo.ApiRoles WHERE RoleName = N'consulta');

        IF @ConsultaPassword = N'CAMBIAR_CONSULTA_TEMP'
        BEGIN
            THROW 50011, 'Define @ConsultaPassword antes de ejecutar el script.', 1;
        END;

        IF @ConsultaRoleId IS NULL
        BEGIN
            THROW 50002, 'No existe rol consulta en ApiRoles.', 1;
        END;

        SELECT @ConsultaUserId = UserId
        FROM dbo.ApiUsers
        WHERE Username = @ConsultaUsername;

        IF @ConsultaUserId IS NULL
        BEGIN
            DECLARE @ConsultaSalt VARBINARY(16) = CRYPT_GEN_RANDOM(16);
            DECLARE @ConsultaHash VARBINARY(32) = HASHBYTES('SHA2_256', @ConsultaSalt + CONVERT(VARBINARY(4000), @ConsultaPassword));

            INSERT INTO dbo.ApiUsers
            (
                Username,
                PasswordSalt,
                PasswordHash,
                MustChangePassword,
                IsActive
            )
            VALUES
            (
                @ConsultaUsername,
                @ConsultaSalt,
                @ConsultaHash,
                0,
                1
            );

            SET @ConsultaUserId = SCOPE_IDENTITY();
        END;

        IF NOT EXISTS
        (
            SELECT 1
            FROM dbo.ApiUserRoles
            WHERE UserId = @ConsultaUserId
              AND RoleId = @ConsultaRoleId
        )
        BEGIN
            INSERT INTO dbo.ApiUserRoles (UserId, RoleId)
            VALUES (@ConsultaUserId, @ConsultaRoleId);
        END;
    END;

    COMMIT TRAN;

    SELECT
        u.UserId,
        u.Username,
        u.MustChangePassword,
        u.IsActive,
        STRING_AGG(r.RoleName, ',') AS Roles
    FROM dbo.ApiUsers u
    LEFT JOIN dbo.ApiUserRoles ur ON ur.UserId = u.UserId
    LEFT JOIN dbo.ApiRoles r ON r.RoleId = ur.RoleId
    GROUP BY u.UserId, u.Username, u.MustChangePassword, u.IsActive
    ORDER BY u.UserId;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRAN;

    THROW;
END CATCH;

/*
Rollback manual (si necesitas revertir solo este modulo):
  DROP TABLE dbo.ApiUserRoles;
  DROP TABLE dbo.ApiRoles;
  DROP TABLE dbo.ApiUsers;
*/
