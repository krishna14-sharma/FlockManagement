/* =====================================================================
   FLOCK WEIGHT MANAGEMENT & ANALYSIS SYSTEM
   Script 02 : Phase 1 stored procedures (security, audit, flock master)
   Business-rule violations are raised with THROW 50000-50999 so the
   application can show the message to the user as a validation error.
   ===================================================================== */
USE FlockManagementDB;
GO
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

/* ---------------------------------------------------------------------
   AUDIT
   --------------------------------------------------------------------- */
CREATE OR ALTER PROCEDURE dbo.sp_AuditLog_Insert
    @UserID     INT,
    @UserName   NVARCHAR(50),
    @ActionType NVARCHAR(20),
    @TableName  NVARCHAR(60),
    @RecordID   NVARCHAR(50),
    @OldValue   NVARCHAR(MAX) = NULL,
    @NewValue   NVARCHAR(MAX) = NULL,
    @IPAddress  NVARCHAR(50)  = NULL
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO dbo.tblAuditLog (UserID, UserName, ActionType, TableName, RecordID, OldValue, NewValue, IPAddress)
    VALUES (@UserID, @UserName, @ActionType, @TableName, @RecordID, @OldValue, @NewValue, @IPAddress);
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_AuditLog_Search
    @FromDate   DATE = NULL,
    @ToDate     DATE = NULL,
    @TableName  NVARCHAR(60) = NULL,
    @UserName   NVARCHAR(50) = NULL,
    @PageNo     INT = 1,
    @PageSize   INT = 25
AS
BEGIN
    SET NOCOUNT ON;
    SELECT  a.AuditLogID, a.UserID, a.UserName, a.ActionDate, a.ActionType, a.TableName,
            a.RecordID, a.OldValue, a.NewValue, a.IPAddress,
            COUNT(*) OVER () AS TotalCount
    FROM    dbo.tblAuditLog a
    WHERE   (@FromDate IS NULL OR a.ActionDate >= @FromDate)
      AND   (@ToDate   IS NULL OR a.ActionDate <  DATEADD(DAY, 1, @ToDate))
      AND   (@TableName IS NULL OR a.TableName = @TableName)
      AND   (@UserName  IS NULL OR a.UserName LIKE N'%' + @UserName + N'%')
    ORDER BY a.ActionDate DESC, a.AuditLogID DESC
    OFFSET (@PageNo - 1) * @PageSize ROWS FETCH NEXT @PageSize ROWS ONLY;
END
GO

/* ---------------------------------------------------------------------
   AUTHENTICATION
   --------------------------------------------------------------------- */
CREATE OR ALTER PROCEDURE dbo.sp_User_GetForLogin
    @UserName NVARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT  u.UserID, u.UserName, u.FullName, u.Email, u.PasswordHash, u.RoleID, r.RoleName,
            u.FarmID, u.IsActive, u.IsLocked, u.FailedLoginCount, u.MustChangePassword,
            r.IsActive AS RoleIsActive
    FROM    dbo.tblUsers u
    INNER JOIN dbo.tblRoles r ON r.RoleID = u.RoleID
    WHERE   u.UserName = @UserName;
END
GO

/* Records a login attempt; locks the account after @MaxFailedAttempts consecutive failures */
CREATE OR ALTER PROCEDURE dbo.sp_User_RecordLogin
    @UserID             INT = NULL,
    @UserName           NVARCHAR(50),
    @IsSuccess          BIT,
    @IPAddress          NVARCHAR(50) = NULL,
    @MaxFailedAttempts  INT = 5,
    @Remarks            NVARCHAR(200) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    BEGIN TRANSACTION;

    INSERT INTO dbo.tblLoginHistory (UserID, UserName, IsSuccess, IPAddress, Remarks)
    VALUES (@UserID, @UserName, @IsSuccess, @IPAddress, @Remarks);

    IF @UserID IS NOT NULL
    BEGIN
        IF @IsSuccess = 1
            UPDATE dbo.tblUsers SET FailedLoginCount = 0, LastLoginDate = SYSDATETIME() WHERE UserID = @UserID;
        ELSE
            UPDATE dbo.tblUsers
               SET FailedLoginCount = FailedLoginCount + 1,
                   IsLocked = CASE WHEN FailedLoginCount + 1 >= @MaxFailedAttempts THEN 1 ELSE IsLocked END
             WHERE UserID = @UserID;
    END

    COMMIT TRANSACTION;

    SELECT IsLocked, FailedLoginCount FROM dbo.tblUsers WHERE UserID = @UserID;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_User_ChangePassword
    @UserID         INT,
    @PasswordHash   NVARCHAR(256),
    @ChangedBy      INT,
    @ChangedByName  NVARCHAR(50),
    @IPAddress      NVARCHAR(50) = NULL,
    @MustChange     BIT = 0
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    BEGIN TRANSACTION;
    UPDATE dbo.tblUsers
       SET PasswordHash = @PasswordHash, PasswordChangedDate = SYSDATETIME(), MustChangePassword = @MustChange,
           IsLocked = 0, FailedLoginCount = 0, ModifiedBy = @ChangedBy, ModifiedDate = SYSDATETIME()
     WHERE UserID = @UserID;
    IF @@ROWCOUNT = 0 THROW 50010, N'User not found.', 1;

    EXEC dbo.sp_AuditLog_Insert @ChangedBy, @ChangedByName, N'PASSWORD', N'tblUsers', @UserID,
         NULL, N'{"PasswordChanged":true}', @IPAddress;
    COMMIT TRANSACTION;
END
GO

/* ---------------------------------------------------------------------
   USERS
   --------------------------------------------------------------------- */
CREATE OR ALTER PROCEDURE dbo.sp_User_Search
    @Search     NVARCHAR(100) = NULL,
    @RoleID     INT = NULL,
    @PageNo     INT = 1,
    @PageSize   INT = 25
AS
BEGIN
    SET NOCOUNT ON;
    SELECT  u.UserID, u.UserName, u.FullName, u.Email, u.MobileNumber, u.RoleID, r.RoleName,
            u.FarmID, f.FarmName, u.IsActive, u.IsLocked, u.LastLoginDate, u.MustChangePassword,
            COUNT(*) OVER () AS TotalCount
    FROM    dbo.tblUsers u
    INNER JOIN dbo.tblRoles r ON r.RoleID = u.RoleID
    LEFT  JOIN dbo.tblFarm  f ON f.FarmID = u.FarmID
    WHERE   (@Search IS NULL OR u.UserName LIKE N'%' + @Search + N'%' OR u.FullName LIKE N'%' + @Search + N'%' OR u.Email LIKE N'%' + @Search + N'%')
      AND   (@RoleID IS NULL OR u.RoleID = @RoleID)
    ORDER BY u.UserName
    OFFSET (@PageNo - 1) * @PageSize ROWS FETCH NEXT @PageSize ROWS ONLY;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_User_Get
    @UserID INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT  u.UserID, u.UserName, u.FullName, u.Email, u.MobileNumber, u.RoleID, r.RoleName,
            u.FarmID, f.FarmName, u.IsActive, u.IsLocked, u.LastLoginDate, u.MustChangePassword,
            CAST(0 AS INT) AS TotalCount
    FROM    dbo.tblUsers u
    INNER JOIN dbo.tblRoles r ON r.RoleID = u.RoleID
    LEFT  JOIN dbo.tblFarm  f ON f.FarmID = u.FarmID
    WHERE   u.UserID = @UserID;
END
GO

/* Insert (@UserID = 0) or update. @PasswordHash is required for insert, ignored for update. */
CREATE OR ALTER PROCEDURE dbo.sp_User_Save
    @UserID         INT,
    @UserName       NVARCHAR(50),
    @FullName       NVARCHAR(100),
    @Email          NVARCHAR(150) = NULL,
    @MobileNumber   NVARCHAR(20) = NULL,
    @RoleID         INT,
    @FarmID         INT = NULL,
    @IsActive       BIT,
    @IsLocked       BIT = 0,
    @PasswordHash   NVARCHAR(256) = NULL,
    @ActionUserID   INT,
    @ActionUserName NVARCHAR(50),
    @IPAddress      NVARCHAR(50) = NULL,
    @NewUserID      INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF EXISTS (SELECT 1 FROM dbo.tblUsers WHERE UserName = @UserName AND UserID <> @UserID)
        THROW 50011, N'User name already exists.', 1;
    IF NOT EXISTS (SELECT 1 FROM dbo.tblRoles WHERE RoleID = @RoleID AND IsActive = 1)
        THROW 50012, N'Selected role is not valid or inactive.', 1;
    IF @UserID = @ActionUserID AND @IsActive = 0
        THROW 50013, N'You cannot deactivate your own account.', 1;

    DECLARE @Old NVARCHAR(MAX), @New NVARCHAR(MAX);

    BEGIN TRANSACTION;
    IF @UserID = 0
    BEGIN
        IF @PasswordHash IS NULL THROW 50014, N'Password is required for a new user.', 1;
        INSERT INTO dbo.tblUsers (UserName, FullName, Email, MobileNumber, PasswordHash, RoleID, FarmID, IsActive, IsLocked, MustChangePassword, CreatedBy)
        VALUES (@UserName, @FullName, @Email, @MobileNumber, @PasswordHash, @RoleID, @FarmID, @IsActive, 0, 1, @ActionUserID);
        SET @NewUserID = SCOPE_IDENTITY();

        SET @New = (SELECT UserID, UserName, FullName, Email, MobileNumber, RoleID, FarmID, IsActive
                    FROM dbo.tblUsers WHERE UserID = @NewUserID FOR JSON PATH, WITHOUT_ARRAY_WRAPPER);
        EXEC dbo.sp_AuditLog_Insert @ActionUserID, @ActionUserName, N'INSERT', N'tblUsers', @NewUserID, NULL, @New, @IPAddress;
    END
    ELSE
    BEGIN
        SET @Old = (SELECT UserID, UserName, FullName, Email, MobileNumber, RoleID, FarmID, IsActive, IsLocked
                    FROM dbo.tblUsers WHERE UserID = @UserID FOR JSON PATH, WITHOUT_ARRAY_WRAPPER);
        IF @Old IS NULL THROW 50010, N'User not found.', 1;

        UPDATE dbo.tblUsers
           SET UserName = @UserName, FullName = @FullName, Email = @Email, MobileNumber = @MobileNumber,
               RoleID = @RoleID, FarmID = @FarmID, IsActive = @IsActive, IsLocked = @IsLocked,
               FailedLoginCount = CASE WHEN @IsLocked = 0 THEN 0 ELSE FailedLoginCount END,
               ModifiedBy = @ActionUserID, ModifiedDate = SYSDATETIME()
         WHERE UserID = @UserID;
        SET @NewUserID = @UserID;

        SET @New = (SELECT UserID, UserName, FullName, Email, MobileNumber, RoleID, FarmID, IsActive, IsLocked
                    FROM dbo.tblUsers WHERE UserID = @UserID FOR JSON PATH, WITHOUT_ARRAY_WRAPPER);
        EXEC dbo.sp_AuditLog_Insert @ActionUserID, @ActionUserName, N'UPDATE', N'tblUsers', @UserID, @Old, @New, @IPAddress;
    END
    COMMIT TRANSACTION;
END
GO

/* ---------------------------------------------------------------------
   ROLES & PERMISSIONS
   --------------------------------------------------------------------- */
CREATE OR ALTER PROCEDURE dbo.sp_Permission_GetCodesByRole
    @RoleID INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT  p.PermissionCode
    FROM    dbo.tblRolePermissions rp
    INNER JOIN dbo.tblPermissions p ON p.PermissionID = rp.PermissionID
    INNER JOIN dbo.tblRoles r ON r.RoleID = rp.RoleID
    WHERE   rp.RoleID = @RoleID AND p.IsActive = 1 AND r.IsActive = 1;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_Menu_GetByRole
    @RoleID INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT  p.PermissionID, p.PermissionCode, p.MenuGroup, p.MenuText, p.ControllerName, p.ActionName, p.IconClass, p.SortOrder
    FROM    dbo.tblRolePermissions rp
    INNER JOIN dbo.tblPermissions p ON p.PermissionID = rp.PermissionID
    WHERE   rp.RoleID = @RoleID AND p.IsActive = 1 AND p.IsMenu = 1
    ORDER BY p.SortOrder, p.MenuText;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_Permission_GetMatrix
    @RoleID INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT  p.PermissionID, p.PermissionCode, p.PermissionName, p.MenuGroup, p.SortOrder,
            CAST(CASE WHEN rp.RoleID IS NULL THEN 0 ELSE 1 END AS BIT) AS IsGranted
    FROM    dbo.tblPermissions p
    LEFT JOIN dbo.tblRolePermissions rp ON rp.PermissionID = p.PermissionID AND rp.RoleID = @RoleID
    WHERE   p.IsActive = 1
    ORDER BY p.SortOrder, p.PermissionName;
END
GO

/* Replaces the permission set of a role. @PermissionIDs = comma separated list. */
CREATE OR ALTER PROCEDURE dbo.sp_Role_SavePermissions
    @RoleID         INT,
    @PermissionIDs  NVARCHAR(MAX),
    @ActionUserID   INT,
    @ActionUserName NVARCHAR(50),
    @IPAddress      NVARCHAR(50) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF NOT EXISTS (SELECT 1 FROM dbo.tblRoles WHERE RoleID = @RoleID)
        THROW 50020, N'Role not found.', 1;

    DECLARE @Old NVARCHAR(MAX), @New NVARCHAR(MAX);
    SET @Old = (SELECT p.PermissionCode FROM dbo.tblRolePermissions rp
                INNER JOIN dbo.tblPermissions p ON p.PermissionID = rp.PermissionID
                WHERE rp.RoleID = @RoleID ORDER BY p.PermissionCode FOR JSON PATH);

    BEGIN TRANSACTION;
    DELETE FROM dbo.tblRolePermissions WHERE RoleID = @RoleID;

    INSERT INTO dbo.tblRolePermissions (RoleID, PermissionID)
    SELECT DISTINCT @RoleID, p.PermissionID
    FROM   STRING_SPLIT(ISNULL(@PermissionIDs, N''), N',') s
    INNER JOIN dbo.tblPermissions p ON p.PermissionID = TRY_CONVERT(INT, LTRIM(RTRIM(s.value)));

    /* The Administrator role must always keep role-management rights, to avoid lock-out */
    IF EXISTS (SELECT 1 FROM dbo.tblRoles WHERE RoleID = @RoleID AND IsSystem = 1 AND RoleName = N'Administrator')
    BEGIN
        INSERT INTO dbo.tblRolePermissions (RoleID, PermissionID)
        SELECT @RoleID, p.PermissionID FROM dbo.tblPermissions p
        WHERE p.PermissionCode IN (N'ROLE_VIEW', N'ROLE_EDIT', N'USER_VIEW', N'USER_EDIT')
          AND NOT EXISTS (SELECT 1 FROM dbo.tblRolePermissions x WHERE x.RoleID = @RoleID AND x.PermissionID = p.PermissionID);
    END

    SET @New = (SELECT p.PermissionCode FROM dbo.tblRolePermissions rp
                INNER JOIN dbo.tblPermissions p ON p.PermissionID = rp.PermissionID
                WHERE rp.RoleID = @RoleID ORDER BY p.PermissionCode FOR JSON PATH);

    EXEC dbo.sp_AuditLog_Insert @ActionUserID, @ActionUserName, N'PERMISSION', N'tblRolePermissions', @RoleID, @Old, @New, @IPAddress;
    COMMIT TRANSACTION;
END
GO

/* ---------------------------------------------------------------------
   FLOCK MASTER
   --------------------------------------------------------------------- */
CREATE OR ALTER PROCEDURE dbo.sp_Flock_Create
    @FlockNumber    NVARCHAR(30),
    @LotID          INT,
    @BirdTypeID     INT,
    @BreedID        INT,
    @FarmID         INT,
    @HouseID        INT,
    @PlacementDate  DATE,
    @HatchDate      DATE,
    @MaleCount      INT,
    @FemaleCount    INT,
    @Source         NVARCHAR(100) = NULL,
    @ChickAvgWeight DECIMAL(10,2) = NULL,
    @Remarks        NVARCHAR(500) = NULL,
    @UserID         INT,
    @UserName       NVARCHAR(50),
    @IPAddress      NVARCHAR(50) = NULL,
    @FlockID        INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    SET @FlockNumber = LTRIM(RTRIM(@FlockNumber));
    IF @FlockNumber IS NULL OR @FlockNumber = N''
        THROW 50100, N'Flock number is required.', 1;
    IF EXISTS (SELECT 1 FROM dbo.tblFlock WHERE FlockNumber = @FlockNumber)
        THROW 50101, N'A flock with this flock number already exists.', 1;
    IF @MaleCount < 0 OR @FemaleCount < 0 OR (@MaleCount + @FemaleCount) = 0
        THROW 50102, N'Male and female counts cannot be negative and the flock must contain birds.', 1;
    IF @HatchDate > @PlacementDate
        THROW 50103, N'Hatch date cannot be after placement date.', 1;
    IF @PlacementDate > DATEADD(DAY, 1, CONVERT(DATE, GETDATE()))
        THROW 50104, N'Placement date cannot be in the future.', 1;
    IF NOT EXISTS (SELECT 1 FROM dbo.tblHouse WHERE HouseID = @HouseID AND FarmID = @FarmID AND IsActive = 1)
        THROW 50105, N'Selected house does not belong to the selected farm or is inactive.', 1;
    IF NOT EXISTS (SELECT 1 FROM dbo.tblLot WHERE LotID = @LotID AND IsActive = 1)
        THROW 50106, N'Selected lot is not valid or inactive.', 1;
    IF NOT EXISTS (SELECT 1 FROM dbo.tblBirdType WHERE BirdTypeID = @BirdTypeID AND IsActive = 1)
        THROW 50107, N'Selected bird type is not valid or inactive.', 1;
    IF NOT EXISTS (SELECT 1 FROM dbo.tblBreed WHERE BreedID = @BreedID AND IsActive = 1)
        THROW 50108, N'Selected breed is not valid or inactive.', 1;

    DECLARE @Capacity INT = (SELECT Capacity FROM dbo.tblHouse WHERE HouseID = @HouseID);
    DECLARE @Occupied INT = (SELECT ISNULL(SUM(TotalBirdCount), 0) FROM dbo.tblFlock WHERE HouseID = @HouseID AND Status = N'Active');
    IF @Capacity > 0 AND @Occupied + @MaleCount + @FemaleCount > @Capacity
        THROW 50109, N'Placement exceeds the house capacity.', 1;

    BEGIN TRANSACTION;

    INSERT INTO dbo.tblFlock (FlockNumber, LotID, BirdTypeID, BreedID, FarmID, HouseID, PlacementDate, HatchDate,
                              InitialBirdCount, MaleCount, FemaleCount, CurrentMaleCount, CurrentFemaleCount,
                              Source, Status, Remarks, CreatedBy)
    VALUES (@FlockNumber, @LotID, @BirdTypeID, @BreedID, @FarmID, @HouseID, @PlacementDate, @HatchDate,
            @MaleCount + @FemaleCount, @MaleCount, @FemaleCount, @MaleCount, @FemaleCount,
            @Source, N'Active', @Remarks, @UserID);
    SET @FlockID = SCOPE_IDENTITY();

    INSERT INTO dbo.tblFlockPlacement (FlockID, PlacementDate, HouseID, MaleCount, FemaleCount, Source, ChickAvgWeight, IsInitial, Remarks, CreatedBy)
    VALUES (@FlockID, @PlacementDate, @HouseID, @MaleCount, @FemaleCount, @Source, @ChickAvgWeight, 1, N'Initial placement', @UserID);

    DECLARE @New NVARCHAR(MAX) = (
        SELECT FlockID, FlockNumber, LotID, BirdTypeID, BreedID, FarmID, HouseID, PlacementDate, HatchDate,
               InitialBirdCount, MaleCount, FemaleCount, Source, Status, Remarks
        FROM dbo.tblFlock WHERE FlockID = @FlockID FOR JSON PATH, WITHOUT_ARRAY_WRAPPER);
    EXEC dbo.sp_AuditLog_Insert @UserID, @UserName, N'INSERT', N'tblFlock', @FlockID, NULL, @New, @IPAddress;

    COMMIT TRANSACTION;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_Flock_Update
    @FlockID        INT,
    @FlockNumber    NVARCHAR(30),
    @LotID          INT,
    @BirdTypeID     INT,
    @BreedID        INT,
    @FarmID         INT,
    @HouseID        INT,
    @PlacementDate  DATE,
    @HatchDate      DATE,
    @MaleCount      INT,
    @FemaleCount    INT,
    @Source         NVARCHAR(100) = NULL,
    @Remarks        NVARCHAR(500) = NULL,
    @RowVer         BINARY(8),
    @UserID         INT,
    @UserName       NVARCHAR(50),
    @IPAddress      NVARCHAR(50) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    SET @FlockNumber = LTRIM(RTRIM(@FlockNumber));
    IF NOT EXISTS (SELECT 1 FROM dbo.tblFlock WHERE FlockID = @FlockID)
        THROW 50110, N'Flock not found.', 1;
    IF EXISTS (SELECT 1 FROM dbo.tblFlock WHERE FlockID = @FlockID AND RowVer <> @RowVer)
        THROW 50111, N'This flock was modified by another user. Reload the record and try again.', 1;
    IF @FlockNumber IS NULL OR @FlockNumber = N''
        THROW 50100, N'Flock number is required.', 1;
    IF EXISTS (SELECT 1 FROM dbo.tblFlock WHERE FlockNumber = @FlockNumber AND FlockID <> @FlockID)
        THROW 50101, N'A flock with this flock number already exists.', 1;
    IF @MaleCount < 0 OR @FemaleCount < 0 OR (@MaleCount + @FemaleCount) = 0
        THROW 50102, N'Male and female counts cannot be negative and the flock must contain birds.', 1;
    IF @HatchDate > @PlacementDate
        THROW 50103, N'Hatch date cannot be after placement date.', 1;
    IF NOT EXISTS (SELECT 1 FROM dbo.tblHouse WHERE HouseID = @HouseID AND FarmID = @FarmID)
        THROW 50105, N'Selected house does not belong to the selected farm.', 1;

    DECLARE @HasWeekly BIT = CASE WHEN EXISTS (SELECT 1 FROM dbo.tblFlockWeekly WHERE FlockID = @FlockID)
                                    OR EXISTS (SELECT 1 FROM dbo.tblMortality WHERE FlockID = @FlockID) THEN 1 ELSE 0 END;

    /* Once weekly data exists, placement figures drive the population chain and can only be
       corrected through Flock Adjustment (which is audited separately). */
    IF @HasWeekly = 1 AND EXISTS (
        SELECT 1 FROM dbo.tblFlock
        WHERE FlockID = @FlockID
          AND (MaleCount <> @MaleCount OR FemaleCount <> @FemaleCount OR HatchDate <> @HatchDate
               OR PlacementDate <> @PlacementDate OR BirdTypeID <> @BirdTypeID OR FarmID <> @FarmID))
        THROW 50112, N'Weekly data exists for this flock. Bird counts, dates, bird type and farm can no longer be changed here; use Flock Adjustment / Flock Transfer.', 1;

    DECLARE @Old NVARCHAR(MAX) = (
        SELECT FlockID, FlockNumber, LotID, BirdTypeID, BreedID, FarmID, HouseID, PlacementDate, HatchDate,
               InitialBirdCount, MaleCount, FemaleCount, Source, Status, Remarks
        FROM dbo.tblFlock WHERE FlockID = @FlockID FOR JSON PATH, WITHOUT_ARRAY_WRAPPER);

    BEGIN TRANSACTION;

    UPDATE dbo.tblFlock
       SET FlockNumber = @FlockNumber, LotID = @LotID, BirdTypeID = @BirdTypeID, BreedID = @BreedID,
           FarmID = @FarmID, HouseID = @HouseID, PlacementDate = @PlacementDate, HatchDate = @HatchDate,
           InitialBirdCount = @MaleCount + @FemaleCount, MaleCount = @MaleCount, FemaleCount = @FemaleCount,
           CurrentMaleCount   = CASE WHEN @HasWeekly = 0 THEN @MaleCount   ELSE CurrentMaleCount END,
           CurrentFemaleCount = CASE WHEN @HasWeekly = 0 THEN @FemaleCount ELSE CurrentFemaleCount END,
           Source = @Source, Remarks = @Remarks, ModifiedBy = @UserID, ModifiedDate = SYSDATETIME()
     WHERE FlockID = @FlockID AND RowVer = @RowVer;

    IF @@ROWCOUNT = 0
        THROW 50111, N'This flock was modified by another user. Reload the record and try again.', 1;

    IF @HasWeekly = 0
        UPDATE dbo.tblFlockPlacement
           SET PlacementDate = @PlacementDate, HouseID = @HouseID, MaleCount = @MaleCount, FemaleCount = @FemaleCount,
               Source = @Source, ModifiedBy = @UserID, ModifiedDate = SYSDATETIME()
         WHERE FlockID = @FlockID AND IsInitial = 1;

    DECLARE @New NVARCHAR(MAX) = (
        SELECT FlockID, FlockNumber, LotID, BirdTypeID, BreedID, FarmID, HouseID, PlacementDate, HatchDate,
               InitialBirdCount, MaleCount, FemaleCount, Source, Status, Remarks
        FROM dbo.tblFlock WHERE FlockID = @FlockID FOR JSON PATH, WITHOUT_ARRAY_WRAPPER);
    EXEC dbo.sp_AuditLog_Insert @UserID, @UserName, N'UPDATE', N'tblFlock', @FlockID, @Old, @New, @IPAddress;

    COMMIT TRANSACTION;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_Flock_SetStatus
    @FlockID        INT,
    @Status         NVARCHAR(20),
    @DepletionDate  DATE = NULL,
    @UserID         INT,
    @UserName       NVARCHAR(50),
    @IPAddress      NVARCHAR(50) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    IF @Status NOT IN (N'Active', N'Closed', N'Depleted', N'Transferred')
        THROW 50113, N'Invalid flock status.', 1;
    IF @Status IN (N'Depleted', N'Closed') AND @DepletionDate IS NULL
        SET @DepletionDate = CONVERT(DATE, GETDATE());

    DECLARE @Old NVARCHAR(MAX) = (SELECT Status, DepletionDate FROM dbo.tblFlock WHERE FlockID = @FlockID FOR JSON PATH, WITHOUT_ARRAY_WRAPPER);
    IF @Old IS NULL THROW 50110, N'Flock not found.', 1;

    BEGIN TRANSACTION;
    UPDATE dbo.tblFlock
       SET Status = @Status,
           DepletionDate = CASE WHEN @Status = N'Active' THEN NULL ELSE @DepletionDate END,
           ModifiedBy = @UserID, ModifiedDate = SYSDATETIME()
     WHERE FlockID = @FlockID;
    DECLARE @New NVARCHAR(MAX) = (SELECT Status, DepletionDate FROM dbo.tblFlock WHERE FlockID = @FlockID FOR JSON PATH, WITHOUT_ARRAY_WRAPPER);
    EXEC dbo.sp_AuditLog_Insert @UserID, @UserName, N'STATUS', N'tblFlock', @FlockID, @Old, @New, @IPAddress;
    COMMIT TRANSACTION;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_Flock_Get
    @FlockID INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT  f.FlockID, f.FlockNumber, f.LotID, l.LotNumber, f.BirdTypeID, bt.BirdTypeName, f.BreedID, b.BreedName,
            f.FarmID, fm.FarmName, f.HouseID, h.HouseName, f.PlacementDate, f.HatchDate,
            f.InitialBirdCount, f.MaleCount, f.FemaleCount, f.CurrentMaleCount, f.CurrentFemaleCount, f.TotalBirdCount,
            f.Source, f.CurrentAge, f.Status, f.DepletionDate, f.Remarks,
            f.CreatedBy, cu.UserName AS CreatedByName, f.CreatedDate, f.ModifiedBy, mu.UserName AS ModifiedByName, f.ModifiedDate,
            f.RowVer,
            CAST(CASE WHEN EXISTS (SELECT 1 FROM dbo.tblFlockWeekly w WHERE w.FlockID = f.FlockID) THEN 1 ELSE 0 END AS BIT) AS HasWeeklyData,
            CAST(0 AS INT) AS TotalCount
    FROM    dbo.tblFlock f
    INNER JOIN dbo.tblLot      l  ON l.LotID = f.LotID
    INNER JOIN dbo.tblBirdType bt ON bt.BirdTypeID = f.BirdTypeID
    INNER JOIN dbo.tblBreed    b  ON b.BreedID = f.BreedID
    INNER JOIN dbo.tblFarm     fm ON fm.FarmID = f.FarmID
    INNER JOIN dbo.tblHouse    h  ON h.HouseID = f.HouseID
    LEFT  JOIN dbo.tblUsers    cu ON cu.UserID = f.CreatedBy
    LEFT  JOIN dbo.tblUsers    mu ON mu.UserID = f.ModifiedBy
    WHERE   f.FlockID = @FlockID;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_Flock_Search
    @Search         NVARCHAR(50) = NULL,
    @FarmID         INT = NULL,
    @HouseID        INT = NULL,
    @BirdTypeID     INT = NULL,
    @LotID          INT = NULL,
    @Status         NVARCHAR(20) = NULL,
    @RestrictFarmID INT = NULL,     -- user's farm restriction (NULL = all farms)
    @PageNo         INT = 1,
    @PageSize       INT = 25
AS
BEGIN
    SET NOCOUNT ON;
    IF @PageNo < 1 SET @PageNo = 1;
    IF @PageSize NOT BETWEEN 1 AND 500 SET @PageSize = 25;

    SELECT  f.FlockID, f.FlockNumber, f.LotID, l.LotNumber, f.BirdTypeID, bt.BirdTypeName, f.BreedID, b.BreedName,
            f.FarmID, fm.FarmName, f.HouseID, h.HouseName, f.PlacementDate, f.HatchDate,
            f.InitialBirdCount, f.MaleCount, f.FemaleCount, f.CurrentMaleCount, f.CurrentFemaleCount, f.TotalBirdCount,
            f.Source, f.CurrentAge, f.Status, f.DepletionDate, f.Remarks,
            f.CreatedBy, CAST(NULL AS NVARCHAR(50)) AS CreatedByName, f.CreatedDate, f.ModifiedBy,
            CAST(NULL AS NVARCHAR(50)) AS ModifiedByName, f.ModifiedDate, f.RowVer,
            CAST(0 AS BIT) AS HasWeeklyData,
            COUNT(*) OVER () AS TotalCount
    FROM    dbo.tblFlock f
    INNER JOIN dbo.tblLot      l  ON l.LotID = f.LotID
    INNER JOIN dbo.tblBirdType bt ON bt.BirdTypeID = f.BirdTypeID
    INNER JOIN dbo.tblBreed    b  ON b.BreedID = f.BreedID
    INNER JOIN dbo.tblFarm     fm ON fm.FarmID = f.FarmID
    INNER JOIN dbo.tblHouse    h  ON h.HouseID = f.HouseID
    WHERE   (@Search IS NULL OR f.FlockNumber LIKE N'%' + @Search + N'%' OR l.LotNumber LIKE N'%' + @Search + N'%')
      AND   (@FarmID IS NULL OR f.FarmID = @FarmID)
      AND   (@HouseID IS NULL OR f.HouseID = @HouseID)
      AND   (@BirdTypeID IS NULL OR f.BirdTypeID = @BirdTypeID)
      AND   (@LotID IS NULL OR f.LotID = @LotID)
      AND   (@Status IS NULL OR f.Status = @Status)
      AND   (@RestrictFarmID IS NULL OR f.FarmID = @RestrictFarmID)
    ORDER BY CASE WHEN f.Status = N'Active' THEN 0 ELSE 1 END, f.PlacementDate DESC, f.FlockNumber
    OFFSET (@PageNo - 1) * @PageSize ROWS FETCH NEXT @PageSize ROWS ONLY
    OPTION (RECOMPILE);
END
GO

PRINT N'Phase 1 stored procedures created.';
GO
