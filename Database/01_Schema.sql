/* =====================================================================
   FLOCK WEIGHT MANAGEMENT & ANALYSIS SYSTEM
   Script 01 : Database + complete normalized schema (all phases)
   Target    : SQL Server 2016 or later (uses FOR JSON, STRING_SPLIT)
   Run order : 01_Schema.sql -> 02_StoredProcedures_Phase1.sql -> 03_SeedData.sql
   ===================================================================== */
IF DB_ID(N'FlockManagementDB') IS NULL
    CREATE DATABASE FlockManagementDB;
GO
USE FlockManagementDB;
GO
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO
/* Safety guard: this script builds an empty database. Stop if it has already been run. */
IF OBJECT_ID(N'dbo.tblFlock', N'U') IS NOT NULL
BEGIN
    RAISERROR(N'Schema already exists in FlockManagementDB. Script stopped (no changes made).', 16, 1);
    SET NOEXEC ON;
END
GO

/* =====================================================================
   SECTION A : SECURITY
   ===================================================================== */
CREATE TABLE dbo.tblRoles
(
    RoleID          INT IDENTITY(1,1)   NOT NULL CONSTRAINT PK_tblRoles PRIMARY KEY,
    RoleName        NVARCHAR(50)        NOT NULL,
    Description     NVARCHAR(250)       NULL,
    IsSystem        BIT                 NOT NULL CONSTRAINT DF_tblRoles_IsSystem DEFAULT (0),
    IsActive        BIT                 NOT NULL CONSTRAINT DF_tblRoles_IsActive DEFAULT (1),
    CreatedBy       INT                 NULL,
    CreatedDate     DATETIME2(0)        NOT NULL CONSTRAINT DF_tblRoles_CreatedDate DEFAULT (SYSDATETIME()),
    ModifiedBy      INT                 NULL,
    ModifiedDate    DATETIME2(0)        NULL,
    CONSTRAINT UQ_tblRoles_RoleName UNIQUE (RoleName)
);

/* A permission is a securable action. Rows with IsMenu = 1 also drive the sidebar menu. */
CREATE TABLE dbo.tblPermissions
(
    PermissionID    INT IDENTITY(1,1)   NOT NULL CONSTRAINT PK_tblPermissions PRIMARY KEY,
    PermissionCode  NVARCHAR(60)        NOT NULL,
    PermissionName  NVARCHAR(120)       NOT NULL,
    MenuGroup       NVARCHAR(50)        NOT NULL,       -- Master Data / Transactions / Analysis / Reports / Administration
    MenuText        NVARCHAR(80)        NULL,
    ControllerName  NVARCHAR(60)        NULL,
    ActionName      NVARCHAR(60)        NULL,
    IconClass       NVARCHAR(60)        NULL,
    IsMenu          BIT                 NOT NULL CONSTRAINT DF_tblPermissions_IsMenu DEFAULT (0),
    SortOrder       INT                 NOT NULL CONSTRAINT DF_tblPermissions_SortOrder DEFAULT (0),
    IsActive        BIT                 NOT NULL CONSTRAINT DF_tblPermissions_IsActive DEFAULT (1),
    CONSTRAINT UQ_tblPermissions_Code UNIQUE (PermissionCode)
);

CREATE TABLE dbo.tblRolePermissions
(
    RoleID          INT NOT NULL CONSTRAINT FK_tblRolePermissions_Role REFERENCES dbo.tblRoles(RoleID) ON DELETE CASCADE,
    PermissionID    INT NOT NULL CONSTRAINT FK_tblRolePermissions_Permission REFERENCES dbo.tblPermissions(PermissionID) ON DELETE CASCADE,
    CONSTRAINT PK_tblRolePermissions PRIMARY KEY (RoleID, PermissionID)
);

/* =====================================================================
   SECTION B : ORGANISATION MASTERS
   ===================================================================== */
CREATE TABLE dbo.tblFarm
(
    FarmID          INT IDENTITY(1,1)   NOT NULL CONSTRAINT PK_tblFarm PRIMARY KEY,
    FarmCode        NVARCHAR(20)        NOT NULL,
    FarmName        NVARCHAR(100)       NOT NULL,
    Location        NVARCHAR(100)       NULL,
    Address         NVARCHAR(300)       NULL,
    ContactPerson   NVARCHAR(100)       NULL,
    ContactNumber   NVARCHAR(30)        NULL,
    IsActive        BIT                 NOT NULL CONSTRAINT DF_tblFarm_IsActive DEFAULT (1),
    CreatedBy       INT                 NULL,
    CreatedDate     DATETIME2(0)        NOT NULL CONSTRAINT DF_tblFarm_CreatedDate DEFAULT (SYSDATETIME()),
    ModifiedBy      INT                 NULL,
    ModifiedDate    DATETIME2(0)        NULL,
    CONSTRAINT UQ_tblFarm_FarmCode UNIQUE (FarmCode)
);

CREATE TABLE dbo.tblUsers
(
    UserID              INT IDENTITY(1,1)   NOT NULL CONSTRAINT PK_tblUsers PRIMARY KEY,
    UserName            NVARCHAR(50)        NOT NULL,
    FullName            NVARCHAR(100)       NOT NULL,
    Email               NVARCHAR(150)       NULL,
    MobileNumber        NVARCHAR(20)        NULL,
    PasswordHash        NVARCHAR(256)       NOT NULL,
    RoleID              INT                 NOT NULL CONSTRAINT FK_tblUsers_Role REFERENCES dbo.tblRoles(RoleID),
    FarmID              INT                 NULL CONSTRAINT FK_tblUsers_Farm REFERENCES dbo.tblFarm(FarmID), -- NULL = all farms
    IsActive            BIT                 NOT NULL CONSTRAINT DF_tblUsers_IsActive DEFAULT (1),
    IsLocked            BIT                 NOT NULL CONSTRAINT DF_tblUsers_IsLocked DEFAULT (0),
    FailedLoginCount    INT                 NOT NULL CONSTRAINT DF_tblUsers_Failed DEFAULT (0),
    MustChangePassword  BIT                 NOT NULL CONSTRAINT DF_tblUsers_MustChange DEFAULT (0),
    LastLoginDate       DATETIME2(0)        NULL,
    PasswordChangedDate DATETIME2(0)        NULL,
    CreatedBy           INT                 NULL,
    CreatedDate         DATETIME2(0)        NOT NULL CONSTRAINT DF_tblUsers_CreatedDate DEFAULT (SYSDATETIME()),
    ModifiedBy          INT                 NULL,
    ModifiedDate        DATETIME2(0)        NULL,
    CONSTRAINT UQ_tblUsers_UserName UNIQUE (UserName)
);
CREATE INDEX IX_tblUsers_RoleID ON dbo.tblUsers(RoleID);

CREATE TABLE dbo.tblLoginHistory
(
    LoginHistoryID  BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_tblLoginHistory PRIMARY KEY,
    UserID          INT                 NULL CONSTRAINT FK_tblLoginHistory_User REFERENCES dbo.tblUsers(UserID),
    UserName        NVARCHAR(50)        NOT NULL,
    AttemptDate     DATETIME2(0)        NOT NULL CONSTRAINT DF_tblLoginHistory_Date DEFAULT (SYSDATETIME()),
    IsSuccess       BIT                 NOT NULL,
    IPAddress       NVARCHAR(50)        NULL,
    Remarks         NVARCHAR(200)       NULL
);
CREATE INDEX IX_tblLoginHistory_User ON dbo.tblLoginHistory(UserID, AttemptDate DESC);

CREATE TABLE dbo.tblHouse
(
    HouseID         INT IDENTITY(1,1)   NOT NULL CONSTRAINT PK_tblHouse PRIMARY KEY,
    FarmID          INT                 NOT NULL CONSTRAINT FK_tblHouse_Farm REFERENCES dbo.tblFarm(FarmID),
    HouseCode       NVARCHAR(20)        NOT NULL,
    HouseName       NVARCHAR(100)       NOT NULL,
    HouseType       NVARCHAR(50)        NULL,      -- Open / Environment Controlled / Curtain
    Capacity        INT                 NOT NULL CONSTRAINT DF_tblHouse_Capacity DEFAULT (0),
    AreaSqFt        DECIMAL(12,2)       NULL,
    IsActive        BIT                 NOT NULL CONSTRAINT DF_tblHouse_IsActive DEFAULT (1),
    CreatedBy       INT                 NULL,
    CreatedDate     DATETIME2(0)        NOT NULL CONSTRAINT DF_tblHouse_CreatedDate DEFAULT (SYSDATETIME()),
    ModifiedBy      INT                 NULL,
    ModifiedDate    DATETIME2(0)        NULL,
    CONSTRAINT UQ_tblHouse_Farm_Code UNIQUE (FarmID, HouseCode),
    CONSTRAINT CK_tblHouse_Capacity CHECK (Capacity >= 0)
);

/* =====================================================================
   SECTION C : BIRD / FEED / PARAMETER MASTERS
   ===================================================================== */
CREATE TABLE dbo.tblBirdType
(
    BirdTypeID      INT IDENTITY(1,1)   NOT NULL CONSTRAINT PK_tblBirdType PRIMARY KEY,
    BirdTypeCode    NVARCHAR(20)        NOT NULL,
    BirdTypeName    NVARCHAR(60)        NOT NULL,
    Description     NVARCHAR(250)       NULL,
    IsActive        BIT                 NOT NULL CONSTRAINT DF_tblBirdType_IsActive DEFAULT (1),
    CreatedBy       INT                 NULL,
    CreatedDate     DATETIME2(0)        NOT NULL CONSTRAINT DF_tblBirdType_CreatedDate DEFAULT (SYSDATETIME()),
    ModifiedBy      INT                 NULL,
    ModifiedDate    DATETIME2(0)        NULL,
    CONSTRAINT UQ_tblBirdType_Code UNIQUE (BirdTypeCode),
    CONSTRAINT UQ_tblBirdType_Name UNIQUE (BirdTypeName)
);

CREATE TABLE dbo.tblBreed
(
    BreedID         INT IDENTITY(1,1)   NOT NULL CONSTRAINT PK_tblBreed PRIMARY KEY,
    BreedCode       NVARCHAR(20)        NOT NULL,
    BreedName       NVARCHAR(100)       NOT NULL,
    Supplier        NVARCHAR(100)       NULL,
    Description     NVARCHAR(250)       NULL,
    IsActive        BIT                 NOT NULL CONSTRAINT DF_tblBreed_IsActive DEFAULT (1),
    CreatedBy       INT                 NULL,
    CreatedDate     DATETIME2(0)        NOT NULL CONSTRAINT DF_tblBreed_CreatedDate DEFAULT (SYSDATETIME()),
    ModifiedBy      INT                 NULL,
    ModifiedDate    DATETIME2(0)        NULL,
    CONSTRAINT UQ_tblBreed_Code UNIQUE (BreedCode)
);

CREATE TABLE dbo.tblLot
(
    LotID           INT IDENTITY(1,1)   NOT NULL CONSTRAINT PK_tblLot PRIMARY KEY,
    LotNumber       NVARCHAR(30)        NOT NULL,
    LotName         NVARCHAR(100)       NULL,
    FarmID          INT                 NULL CONSTRAINT FK_tblLot_Farm REFERENCES dbo.tblFarm(FarmID),
    Description     NVARCHAR(250)       NULL,
    IsActive        BIT                 NOT NULL CONSTRAINT DF_tblLot_IsActive DEFAULT (1),
    CreatedBy       INT                 NULL,
    CreatedDate     DATETIME2(0)        NOT NULL CONSTRAINT DF_tblLot_CreatedDate DEFAULT (SYSDATETIME()),
    ModifiedBy      INT                 NULL,
    ModifiedDate    DATETIME2(0)        NULL,
    CONSTRAINT UQ_tblLot_LotNumber UNIQUE (LotNumber)
);

/* Configurable units - never hard-coded in code */
CREATE TABLE dbo.tblUnit
(
    UnitID          INT IDENTITY(1,1)   NOT NULL CONSTRAINT PK_tblUnit PRIMARY KEY,
    UnitCode        NVARCHAR(20)        NOT NULL,
    UnitName        NVARCHAR(60)        NOT NULL,
    IsActive        BIT                 NOT NULL CONSTRAINT DF_tblUnit_IsActive DEFAULT (1),
    CONSTRAINT UQ_tblUnit_Code UNIQUE (UnitCode)
);

/* Every measurable item (body weight, feed intake, each nutrient, mortality, uniformity,
   egg production KPIs ...) is a row here, so new parameters need no schema change. */
CREATE TABLE dbo.tblParameter
(
    ParameterID     INT IDENTITY(1,1)   NOT NULL CONSTRAINT PK_tblParameter PRIMARY KEY,
    ParameterCode   NVARCHAR(30)        NOT NULL,
    ParameterName   NVARCHAR(100)       NOT NULL,
    ParameterGroup  NVARCHAR(30)        NOT NULL,   -- BODYWEIGHT, FEED, NUTRITION, MORTALITY, UNIFORMITY, PRODUCTION, FERTILITY, HATCH
    UnitID          INT                 NOT NULL CONSTRAINT FK_tblParameter_Unit REFERENCES dbo.tblUnit(UnitID),
    DecimalPlaces   TINYINT             NOT NULL CONSTRAINT DF_tblParameter_Dec DEFAULT (2),
    Direction       CHAR(1)             NOT NULL CONSTRAINT DF_tblParameter_Dir DEFAULT ('T'), -- T=target(both sides), H=higher is better, L=lower is better
    LogicalMin      DECIMAL(18,4)       NULL,       -- validation range (physically possible values)
    LogicalMax      DECIMAL(18,4)       NULL,
    SortOrder       INT                 NOT NULL CONSTRAINT DF_tblParameter_Sort DEFAULT (0),
    IsActive        BIT                 NOT NULL CONSTRAINT DF_tblParameter_IsActive DEFAULT (1),
    CONSTRAINT UQ_tblParameter_Code UNIQUE (ParameterCode),
    CONSTRAINT CK_tblParameter_Direction CHECK (Direction IN ('T','H','L')),
    CONSTRAINT CK_tblParameter_Group CHECK (ParameterGroup IN ('BODYWEIGHT','WEIGHTGAIN','FEED','NUTRITION','MORTALITY','UNIFORMITY','PRODUCTION','FERTILITY','HATCH'))
);

CREATE TABLE dbo.tblFeedType
(
    FeedTypeID          INT IDENTITY(1,1)   NOT NULL CONSTRAINT PK_tblFeedType PRIMARY KEY,
    FeedTypeCode        NVARCHAR(20)        NOT NULL,
    FeedTypeName        NVARCHAR(100)       NOT NULL,
    TargetSex           CHAR(1)             NOT NULL CONSTRAINT DF_tblFeedType_Sex DEFAULT ('B'), -- F, M, B(oth)
    ApplicableFromWeek  INT                 NULL,
    ApplicableToWeek    INT                 NULL,
    Description         NVARCHAR(250)       NULL,
    IsActive            BIT                 NOT NULL CONSTRAINT DF_tblFeedType_IsActive DEFAULT (1),
    CreatedBy           INT                 NULL,
    CreatedDate         DATETIME2(0)        NOT NULL CONSTRAINT DF_tblFeedType_CreatedDate DEFAULT (SYSDATETIME()),
    ModifiedBy          INT                 NULL,
    ModifiedDate        DATETIME2(0)        NULL,
    CONSTRAINT UQ_tblFeedType_Code UNIQUE (FeedTypeCode),
    CONSTRAINT CK_tblFeedType_Sex CHECK (TargetSex IN ('F','M','B')),
    CONSTRAINT CK_tblFeedType_Weeks CHECK (ApplicableFromWeek IS NULL OR ApplicableToWeek IS NULL OR ApplicableFromWeek <= ApplicableToWeek)
);

CREATE TABLE dbo.tblFeedFormula
(
    FeedFormulaID   INT IDENTITY(1,1)   NOT NULL CONSTRAINT PK_tblFeedFormula PRIMARY KEY,
    FeedTypeID      INT                 NOT NULL CONSTRAINT FK_tblFeedFormula_FeedType REFERENCES dbo.tblFeedType(FeedTypeID),
    FormulaCode     NVARCHAR(30)        NOT NULL,
    FormulaName     NVARCHAR(100)       NOT NULL,
    EffectiveFrom   DATE                NOT NULL,
    EffectiveTo     DATE                NULL,
    Remarks         NVARCHAR(250)       NULL,
    IsActive        BIT                 NOT NULL CONSTRAINT DF_tblFeedFormula_IsActive DEFAULT (1),
    CreatedBy       INT                 NULL,
    CreatedDate     DATETIME2(0)        NOT NULL CONSTRAINT DF_tblFeedFormula_CreatedDate DEFAULT (SYSDATETIME()),
    ModifiedBy      INT                 NULL,
    ModifiedDate    DATETIME2(0)        NULL,
    CONSTRAINT UQ_tblFeedFormula_Code UNIQUE (FormulaCode),
    CONSTRAINT CK_tblFeedFormula_Dates CHECK (EffectiveTo IS NULL OR EffectiveTo >= EffectiveFrom)
);

/* Formulated nutrient values per formula (calculated / declared values) */
CREATE TABLE dbo.tblFeedFormulaDetail
(
    FeedFormulaDetailID INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_tblFeedFormulaDetail PRIMARY KEY,
    FeedFormulaID   INT                 NOT NULL CONSTRAINT FK_tblFeedFormulaDetail_Formula REFERENCES dbo.tblFeedFormula(FeedFormulaID) ON DELETE CASCADE,
    ParameterID     INT                 NOT NULL CONSTRAINT FK_tblFeedFormulaDetail_Param REFERENCES dbo.tblParameter(ParameterID),
    FormulatedValue DECIMAL(18,4)       NOT NULL,
    CONSTRAINT UQ_tblFeedFormulaDetail UNIQUE (FeedFormulaID, ParameterID)
);

CREATE TABLE dbo.tblMortalityReason
(
    MortalityReasonID INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_tblMortalityReason PRIMARY KEY,
    ReasonCode      NVARCHAR(20)        NOT NULL,
    ReasonName      NVARCHAR(100)       NOT NULL,
    Category        NVARCHAR(50)        NULL,       -- Disease / Management / Culling / Other
    IsActive        BIT                 NOT NULL CONSTRAINT DF_tblMortalityReason_IsActive DEFAULT (1),
    CreatedBy       INT                 NULL,
    CreatedDate     DATETIME2(0)        NOT NULL CONSTRAINT DF_tblMortalityReason_CreatedDate DEFAULT (SYSDATETIME()),
    ModifiedBy      INT                 NULL,
    ModifiedDate    DATETIME2(0)        NULL,
    CONSTRAINT UQ_tblMortalityReason_Code UNIQUE (ReasonCode)
);

/* =====================================================================
   SECTION D : STANDARDS & CONFIGURATION (nothing hard-coded)
   ===================================================================== */
/* Age/week standards. BreedID / FeedTypeID NULL = applies to all breeds / feed types.
   sp_GetFlockStandard picks the most specific row effective on the entry date. */
CREATE TABLE dbo.tblStandard
(
    StandardID      INT IDENTITY(1,1)   NOT NULL CONSTRAINT PK_tblStandard PRIMARY KEY,
    BirdTypeID      INT                 NOT NULL CONSTRAINT FK_tblStandard_BirdType REFERENCES dbo.tblBirdType(BirdTypeID),
    BreedID         INT                 NULL CONSTRAINT FK_tblStandard_Breed REFERENCES dbo.tblBreed(BreedID),
    FeedTypeID      INT                 NULL CONSTRAINT FK_tblStandard_FeedType REFERENCES dbo.tblFeedType(FeedTypeID),
    AgeWeek         INT                 NOT NULL,
    ParameterID     INT                 NOT NULL CONSTRAINT FK_tblStandard_Parameter REFERENCES dbo.tblParameter(ParameterID),
    StandardValue   DECIMAL(18,4)       NOT NULL,
    MinValue        DECIMAL(18,4)       NULL,
    MaxValue        DECIMAL(18,4)       NULL,
    CriticalMinValue DECIMAL(18,4)      NULL,       -- optional: explicit critical band; else tolerance % applies
    CriticalMaxValue DECIMAL(18,4)      NULL,
    UnitID          INT                 NOT NULL CONSTRAINT FK_tblStandard_Unit REFERENCES dbo.tblUnit(UnitID),
    EffectiveFrom   DATE                NOT NULL,
    EffectiveTo     DATE                NULL,
    IsActive        BIT                 NOT NULL CONSTRAINT DF_tblStandard_IsActive DEFAULT (1),
    CreatedBy       INT                 NULL,
    CreatedDate     DATETIME2(0)        NOT NULL CONSTRAINT DF_tblStandard_CreatedDate DEFAULT (SYSDATETIME()),
    ModifiedBy      INT                 NULL,
    ModifiedDate    DATETIME2(0)        NULL,
    CONSTRAINT CK_tblStandard_Age CHECK (AgeWeek BETWEEN 0 AND 120),
    CONSTRAINT CK_tblStandard_MinMax CHECK (MinValue IS NULL OR MaxValue IS NULL OR MinValue <= MaxValue),
    CONSTRAINT CK_tblStandard_Dates CHECK (EffectiveTo IS NULL OR EffectiveTo >= EffectiveFrom)
);
CREATE INDEX IX_tblStandard_Lookup ON dbo.tblStandard(BirdTypeID, AgeWeek, ParameterID, EffectiveFrom)
    INCLUDE (BreedID, FeedTypeID, StandardValue, MinValue, MaxValue, EffectiveTo, IsActive);

/* Tolerance used when a standard has no explicit Min/Max or Critical band.
   ParameterID NULL row = global default. */
CREATE TABLE dbo.tblToleranceConfig
(
    ToleranceID     INT IDENTITY(1,1)   NOT NULL CONSTRAINT PK_tblToleranceConfig PRIMARY KEY,
    ParameterID     INT                 NULL CONSTRAINT FK_tblToleranceConfig_Param REFERENCES dbo.tblParameter(ParameterID),
    WarningPct      DECIMAL(9,4)        NOT NULL,   -- |variance %| above this = Below/Above Standard
    CriticalPct     DECIMAL(9,4)        NOT NULL,   -- |variance %| above this = Critical
    IsActive        BIT                 NOT NULL CONSTRAINT DF_tblToleranceConfig_IsActive DEFAULT (1),
    ModifiedBy      INT                 NULL,
    ModifiedDate    DATETIME2(0)        NULL,
    CONSTRAINT CK_tblToleranceConfig CHECK (WarningPct >= 0 AND CriticalPct >= WarningPct)
);
CREATE UNIQUE INDEX UX_tblToleranceConfig_Param ON dbo.tblToleranceConfig(ParameterID) WHERE IsActive = 1;

/* Centralized status definitions (labels, Bootstrap classes, colours) */
CREATE TABLE dbo.tblStatusConfig
(
    StatusCode      NVARCHAR(20)        NOT NULL CONSTRAINT PK_tblStatusConfig PRIMARY KEY,
    StatusName      NVARCHAR(50)        NOT NULL,
    StatusGroup     NVARCHAR(20)        NOT NULL,   -- VARIANCE, UNIFORMITY, SCORE, ALERT
    BadgeCssClass   NVARCHAR(60)        NOT NULL,
    ColorHex        CHAR(7)             NOT NULL,
    SortOrder       INT                 NOT NULL CONSTRAINT DF_tblStatusConfig_Sort DEFAULT (0)
);

CREATE TABLE dbo.tblSystemSetting
(
    SettingKey      NVARCHAR(60)        NOT NULL CONSTRAINT PK_tblSystemSetting PRIMARY KEY,
    SettingValue    NVARCHAR(200)       NOT NULL,
    DataType        NVARCHAR(20)        NOT NULL CONSTRAINT DF_tblSystemSetting_Type DEFAULT ('STRING'),
    Description     NVARCHAR(250)       NULL,
    ModifiedBy      INT                 NULL,
    ModifiedDate    DATETIME2(0)        NULL
);

/* Performance score: weight of each component (configurable by management) */
CREATE TABLE dbo.tblScoreWeightage
(
    ScoreWeightageID INT IDENTITY(1,1)  NOT NULL CONSTRAINT PK_tblScoreWeightage PRIMARY KEY,
    ComponentCode   NVARCHAR(30)        NOT NULL,   -- BODYWEIGHT, WEIGHTGAIN, FEED, UNIFORMITY, MORTALITY, NUTRITION
    ComponentName   NVARCHAR(60)        NOT NULL,
    Weightage       DECIMAL(9,4)        NOT NULL,
    ZeroScoreDeviationPct DECIMAL(9,4)  NOT NULL CONSTRAINT DF_tblScoreWeightage_Zero DEFAULT (20), -- deviation % at which component scores 0
    EffectiveFrom   DATE                NOT NULL,
    EffectiveTo     DATE                NULL,
    IsActive        BIT                 NOT NULL CONSTRAINT DF_tblScoreWeightage_IsActive DEFAULT (1),
    ModifiedBy      INT                 NULL,
    ModifiedDate    DATETIME2(0)        NULL,
    CONSTRAINT CK_tblScoreWeightage CHECK (Weightage >= 0)
);

CREATE TABLE dbo.tblScoreBand
(
    ScoreBandID     INT IDENTITY(1,1)   NOT NULL CONSTRAINT PK_tblScoreBand PRIMARY KEY,
    BandName        NVARCHAR(30)        NOT NULL,
    MinScore        DECIMAL(9,4)        NOT NULL,
    MaxScore        DECIMAL(9,4)        NOT NULL,
    StatusCode      NVARCHAR(20)        NOT NULL CONSTRAINT FK_tblScoreBand_Status REFERENCES dbo.tblStatusConfig(StatusCode),
    CONSTRAINT CK_tblScoreBand CHECK (MinScore <= MaxScore AND MinScore >= 0 AND MaxScore <= 100)
);

/* Optional modules can be switched on without a schema change */
CREATE TABLE dbo.tblModule
(
    ModuleCode      NVARCHAR(30)        NOT NULL CONSTRAINT PK_tblModule PRIMARY KEY,
    ModuleName      NVARCHAR(100)       NOT NULL,
    IsEnabled       BIT                 NOT NULL CONSTRAINT DF_tblModule_IsEnabled DEFAULT (0),
    ModifiedBy      INT                 NULL,
    ModifiedDate    DATETIME2(0)        NULL
);

/* =====================================================================
   SECTION E : FLOCK
   ===================================================================== */
CREATE TABLE dbo.tblFlock
(
    FlockID             INT IDENTITY(1,1)   NOT NULL CONSTRAINT PK_tblFlock PRIMARY KEY,
    FlockNumber         NVARCHAR(30)        NOT NULL,
    LotID               INT                 NOT NULL CONSTRAINT FK_tblFlock_Lot REFERENCES dbo.tblLot(LotID),
    BirdTypeID          INT                 NOT NULL CONSTRAINT FK_tblFlock_BirdType REFERENCES dbo.tblBirdType(BirdTypeID),
    BreedID             INT                 NOT NULL CONSTRAINT FK_tblFlock_Breed REFERENCES dbo.tblBreed(BreedID),
    FarmID              INT                 NOT NULL CONSTRAINT FK_tblFlock_Farm REFERENCES dbo.tblFarm(FarmID),
    HouseID             INT                 NOT NULL CONSTRAINT FK_tblFlock_House REFERENCES dbo.tblHouse(HouseID),
    PlacementDate       DATE                NOT NULL,
    HatchDate           DATE                NOT NULL,
    InitialBirdCount    INT                 NOT NULL,       -- birds placed (male + female)
    MaleCount           INT                 NOT NULL,       -- males placed
    FemaleCount         INT                 NOT NULL,       -- females placed
    CurrentMaleCount    INT                 NOT NULL,       -- live population, maintained by weekly/mortality/transfer
    CurrentFemaleCount  INT                 NOT NULL,
    TotalBirdCount      AS (CurrentMaleCount + CurrentFemaleCount) PERSISTED,
    Source              NVARCHAR(100)       NULL,           -- hatchery / supplier
    CurrentAge          AS (DATEDIFF(DAY, HatchDate, CONVERT(DATE, GETDATE())) / 7),  -- completed weeks
    Status              NVARCHAR(20)        NOT NULL CONSTRAINT DF_tblFlock_Status DEFAULT (N'Active'),
    DepletionDate       DATE                NULL,
    Remarks             NVARCHAR(500)       NULL,
    CreatedBy           INT                 NULL,
    CreatedDate         DATETIME2(0)        NOT NULL CONSTRAINT DF_tblFlock_CreatedDate DEFAULT (SYSDATETIME()),
    ModifiedBy          INT                 NULL,
    ModifiedDate        DATETIME2(0)        NULL,
    RowVer              ROWVERSION          NOT NULL,
    CONSTRAINT UQ_tblFlock_FlockNumber UNIQUE (FlockNumber),
    CONSTRAINT CK_tblFlock_Counts CHECK (MaleCount >= 0 AND FemaleCount >= 0 AND InitialBirdCount = MaleCount + FemaleCount AND InitialBirdCount > 0),
    CONSTRAINT CK_tblFlock_Current CHECK (CurrentMaleCount >= 0 AND CurrentFemaleCount >= 0),
    CONSTRAINT CK_tblFlock_Dates CHECK (HatchDate <= PlacementDate),
    CONSTRAINT CK_tblFlock_Status CHECK (Status IN (N'Active', N'Closed', N'Depleted', N'Transferred'))
);
CREATE INDEX IX_tblFlock_FarmHouse ON dbo.tblFlock(FarmID, HouseID, Status);
CREATE INDEX IX_tblFlock_BirdType ON dbo.tblFlock(BirdTypeID, Status);
CREATE INDEX IX_tblFlock_Lot ON dbo.tblFlock(LotID);

CREATE TABLE dbo.tblFlockPlacement
(
    FlockPlacementID INT IDENTITY(1,1)  NOT NULL CONSTRAINT PK_tblFlockPlacement PRIMARY KEY,
    FlockID         INT                 NOT NULL CONSTRAINT FK_tblFlockPlacement_Flock REFERENCES dbo.tblFlock(FlockID),
    PlacementDate   DATE                NOT NULL,
    HouseID         INT                 NOT NULL CONSTRAINT FK_tblFlockPlacement_House REFERENCES dbo.tblHouse(HouseID),
    MaleCount       INT                 NOT NULL,
    FemaleCount     INT                 NOT NULL,
    Source          NVARCHAR(100)       NULL,
    ChickAvgWeight  DECIMAL(10,2)       NULL,       -- g, day-old
    IsInitial       BIT                 NOT NULL CONSTRAINT DF_tblFlockPlacement_IsInitial DEFAULT (0),
    Remarks         NVARCHAR(500)       NULL,
    CreatedBy       INT                 NULL,
    CreatedDate     DATETIME2(0)        NOT NULL CONSTRAINT DF_tblFlockPlacement_CreatedDate DEFAULT (SYSDATETIME()),
    ModifiedBy      INT                 NULL,
    ModifiedDate    DATETIME2(0)        NULL,
    CONSTRAINT CK_tblFlockPlacement_Counts CHECK (MaleCount >= 0 AND FemaleCount >= 0 AND MaleCount + FemaleCount > 0)
);
CREATE INDEX IX_tblFlockPlacement_Flock ON dbo.tblFlockPlacement(FlockID);

/* =====================================================================
   SECTION F : WEEKLY TRANSACTIONS
   ===================================================================== */
CREATE TABLE dbo.tblFlockWeekly
(
    FlockWeeklyID       INT IDENTITY(1,1)   NOT NULL CONSTRAINT PK_tblFlockWeekly PRIMARY KEY,
    FlockID             INT                 NOT NULL CONSTRAINT FK_tblFlockWeekly_Flock REFERENCES dbo.tblFlock(FlockID),
    WeekNo              INT                 NOT NULL,
    AgeInWeeks          INT                 NOT NULL,
    EntryDate           DATE                NOT NULL,
    HouseID             INT                 NOT NULL CONSTRAINT FK_tblFlockWeekly_House REFERENCES dbo.tblHouse(HouseID),
    -- population
    OpeningPopulation   INT                 NOT NULL,
    OpeningMale         INT                 NOT NULL CONSTRAINT DF_tblFlockWeekly_OpM DEFAULT (0),
    OpeningFemale       INT                 NOT NULL CONSTRAINT DF_tblFlockWeekly_OpF DEFAULT (0),
    BirdsReceived       INT                 NOT NULL CONSTRAINT DF_tblFlockWeekly_Recv DEFAULT (0),
    MaleMortality       INT                 NOT NULL CONSTRAINT DF_tblFlockWeekly_MM DEFAULT (0),
    FemaleMortality     INT                 NOT NULL CONSTRAINT DF_tblFlockWeekly_FM DEFAULT (0),
    TotalMortality      AS (MaleMortality + FemaleMortality) PERSISTED,
    MaleCulling         INT                 NOT NULL CONSTRAINT DF_tblFlockWeekly_MC DEFAULT (0),
    FemaleCulling       INT                 NOT NULL CONSTRAINT DF_tblFlockWeekly_FC DEFAULT (0),
    TransferOut         INT                 NOT NULL CONSTRAINT DF_tblFlockWeekly_TO DEFAULT (0),
    ClosingPopulation   INT                 NOT NULL,
    MortalityPercentage DECIMAL(9,4)        NULL,
    StandardMortalityPct DECIMAL(9,4)       NULL,
    -- feed
    FeedTypeID          INT                 NULL CONSTRAINT FK_tblFlockWeekly_FeedType REFERENCES dbo.tblFeedType(FeedTypeID),
    FeedFormulaID       INT                 NULL CONSTRAINT FK_tblFlockWeekly_FeedFormula REFERENCES dbo.tblFeedFormula(FeedFormulaID),
    TotalFeedKg         DECIMAL(14,3)       NULL,
    FeedWastageKg       DECIMAL(14,3)       NULL,
    StandardFeedIntake  DECIMAL(10,2)       NULL,   -- g/bird/day
    ActualFeedIntake    DECIMAL(10,2)       NULL,
    -- weight
    StandardWeightGain  DECIMAL(10,2)       NULL,   -- g/week
    ActualWeightGain    DECIMAL(10,2)       NULL,
    StandardBodyWeight  DECIMAL(10,2)       NULL,   -- g
    ActualBodyWeight    DECIMAL(10,2)       NULL,
    MinBodyWeight       DECIMAL(10,2)       NULL,
    MaxBodyWeight       DECIMAL(10,2)       NULL,
    StdDevBodyWeight    DECIMAL(10,3)       NULL,
    CVPercentage        DECIMAL(9,4)        NULL,
    SampleSize          INT                 NULL,
    -- nutrition (actual, as analysed / fed)
    Energy              DECIMAL(12,4)       NULL,
    Protein             DECIMAL(12,4)       NULL,
    Methionine          DECIMAL(12,4)       NULL,
    Lysine              DECIMAL(12,4)       NULL,
    LinoleicAcid        DECIMAL(12,4)       NULL,
    Calcium             DECIMAL(12,4)       NULL,
    Phosphorus          DECIMAL(12,4)       NULL,
    Fat                 DECIMAL(12,4)       NULL,
    Fibre               DECIMAL(12,4)       NULL,
    Salt                DECIMAL(12,4)       NULL,
    -- uniformity & score
    StandardUniformity  DECIMAL(9,4)        NULL,
    UniformityPercentage DECIMAL(9,4)       NULL,
    PerformanceScore    DECIMAL(9,4)        NULL,
    OverallStatusCode   NVARCHAR(20)        NULL CONSTRAINT FK_tblFlockWeekly_Status REFERENCES dbo.tblStatusConfig(StatusCode),
    IsLocked            BIT                 NOT NULL CONSTRAINT DF_tblFlockWeekly_IsLocked DEFAULT (0),
    Remarks             NVARCHAR(500)       NULL,
    CreatedBy           INT                 NULL,
    CreatedDate         DATETIME2(0)        NOT NULL CONSTRAINT DF_tblFlockWeekly_CreatedDate DEFAULT (SYSDATETIME()),
    ModifiedBy          INT                 NULL,
    ModifiedDate        DATETIME2(0)        NULL,
    RowVer              ROWVERSION          NOT NULL,
    CONSTRAINT UQ_tblFlockWeekly_FlockWeek UNIQUE (FlockID, WeekNo),         -- duplicate Flock + Week prevented
    CONSTRAINT CK_tblFlockWeekly_Week CHECK (WeekNo BETWEEN 0 AND 120 AND AgeInWeeks BETWEEN 0 AND 120),
    CONSTRAINT CK_tblFlockWeekly_NonNeg CHECK (OpeningPopulation >= 0 AND ClosingPopulation >= 0 AND BirdsReceived >= 0
              AND MaleMortality >= 0 AND FemaleMortality >= 0 AND MaleCulling >= 0 AND FemaleCulling >= 0 AND TransferOut >= 0),
    CONSTRAINT CK_tblFlockWeekly_MortVsOpening CHECK (MaleMortality + FemaleMortality <= OpeningPopulation + BirdsReceived),
    CONSTRAINT CK_tblFlockWeekly_Closing CHECK (ClosingPopulation = OpeningPopulation + BirdsReceived - MaleMortality - FemaleMortality - MaleCulling - FemaleCulling - TransferOut),
    CONSTRAINT CK_tblFlockWeekly_Weights CHECK (ISNULL(ActualBodyWeight,0) >= 0 AND ISNULL(ActualFeedIntake,0) >= 0 AND ISNULL(TotalFeedKg,0) >= 0)
);
CREATE INDEX IX_tblFlockWeekly_EntryDate ON dbo.tblFlockWeekly(EntryDate) INCLUDE (FlockID, WeekNo);

CREATE TABLE dbo.tblWeightSample
(
    WeightSampleID  BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_tblWeightSample PRIMARY KEY,
    FlockWeeklyID   INT                 NOT NULL CONSTRAINT FK_tblWeightSample_Weekly REFERENCES dbo.tblFlockWeekly(FlockWeeklyID) ON DELETE CASCADE,
    Sex             CHAR(1)             NOT NULL CONSTRAINT DF_tblWeightSample_Sex DEFAULT ('F'),
    SampleNo        INT                 NOT NULL,
    WeightGrams     DECIMAL(10,2)       NOT NULL,
    PenOrLocation   NVARCHAR(30)        NULL,
    CONSTRAINT UQ_tblWeightSample UNIQUE (FlockWeeklyID, Sex, SampleNo),
    CONSTRAINT CK_tblWeightSample_Sex CHECK (Sex IN ('F','M')),
    CONSTRAINT CK_tblWeightSample_Weight CHECK (WeightGrams > 0 AND WeightGrams < 10000)
);

/* Multiple feed types per week (e.g. male feed + female feed) */
CREATE TABLE dbo.tblFeedConsumption
(
    FeedConsumptionID INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_tblFeedConsumption PRIMARY KEY,
    FlockWeeklyID   INT                 NOT NULL CONSTRAINT FK_tblFeedConsumption_Weekly REFERENCES dbo.tblFlockWeekly(FlockWeeklyID) ON DELETE CASCADE,
    FeedTypeID      INT                 NOT NULL CONSTRAINT FK_tblFeedConsumption_FeedType REFERENCES dbo.tblFeedType(FeedTypeID),
    FeedFormulaID   INT                 NULL CONSTRAINT FK_tblFeedConsumption_Formula REFERENCES dbo.tblFeedFormula(FeedFormulaID),
    Sex             CHAR(1)             NOT NULL CONSTRAINT DF_tblFeedConsumption_Sex DEFAULT ('F'),
    QuantityKg      DECIMAL(14,3)       NOT NULL,
    WastageKg       DECIMAL(14,3)       NOT NULL CONSTRAINT DF_tblFeedConsumption_Waste DEFAULT (0),
    BirdDays        INT                 NOT NULL,   -- average birds x days fed
    IntakeGPerBirdDay AS (CASE WHEN BirdDays > 0 THEN CONVERT(DECIMAL(10,2), (QuantityKg - WastageKg) * 1000 / BirdDays) END) PERSISTED,
    StandardIntake  DECIMAL(10,2)       NULL,
    Remarks         NVARCHAR(250)       NULL,
    CONSTRAINT CK_tblFeedConsumption_Sex CHECK (Sex IN ('F','M','B')),
    CONSTRAINT CK_tblFeedConsumption_Qty CHECK (QuantityKg >= 0 AND WastageKg >= 0 AND WastageKg <= QuantityKg AND BirdDays >= 0)
);
CREATE INDEX IX_tblFeedConsumption_Weekly ON dbo.tblFeedConsumption(FlockWeeklyID);

/* Normalized nutrient analysis (any parameter in group NUTRITION) */
CREATE TABLE dbo.tblNutritionAnalysis
(
    NutritionAnalysisID INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_tblNutritionAnalysis PRIMARY KEY,
    FlockWeeklyID   INT                 NOT NULL CONSTRAINT FK_tblNutritionAnalysis_Weekly REFERENCES dbo.tblFlockWeekly(FlockWeeklyID) ON DELETE CASCADE,
    ParameterID     INT                 NOT NULL CONSTRAINT FK_tblNutritionAnalysis_Param REFERENCES dbo.tblParameter(ParameterID),
    StandardValue   DECIMAL(18,4)       NULL,
    MinValue        DECIMAL(18,4)       NULL,
    MaxValue        DECIMAL(18,4)       NULL,
    ActualValue     DECIMAL(18,4)       NOT NULL,
    UnitID          INT                 NOT NULL CONSTRAINT FK_tblNutritionAnalysis_Unit REFERENCES dbo.tblUnit(UnitID),
    Variance        AS (ActualValue - StandardValue) PERSISTED,
    StatusCode      NVARCHAR(20)        NULL CONSTRAINT FK_tblNutritionAnalysis_Status REFERENCES dbo.tblStatusConfig(StatusCode),
    SampleRef       NVARCHAR(50)        NULL,       -- lab sample reference
    AnalysisDate    DATE                NULL,
    CONSTRAINT UQ_tblNutritionAnalysis UNIQUE (FlockWeeklyID, ParameterID)
);

CREATE TABLE dbo.tblMortality
(
    MortalityID         INT IDENTITY(1,1)   NOT NULL CONSTRAINT PK_tblMortality PRIMARY KEY,
    FlockID             INT                 NOT NULL CONSTRAINT FK_tblMortality_Flock REFERENCES dbo.tblFlock(FlockID),
    FlockWeeklyID       INT                 NULL CONSTRAINT FK_tblMortality_Weekly REFERENCES dbo.tblFlockWeekly(FlockWeeklyID),
    WeekNo              INT                 NOT NULL,
    EntryDate           DATE                NOT NULL,
    OpeningPopulation   INT                 NOT NULL,
    MalePopulation      INT                 NOT NULL,
    FemalePopulation    INT                 NOT NULL,
    MaleMortality       INT                 NOT NULL CONSTRAINT DF_tblMortality_MM DEFAULT (0),
    FemaleMortality     INT                 NOT NULL CONSTRAINT DF_tblMortality_FM DEFAULT (0),
    TotalMortality      AS (MaleMortality + FemaleMortality) PERSISTED,
    MortalityPercentage AS (CASE WHEN OpeningPopulation > 0 THEN CONVERT(DECIMAL(9,4), (MaleMortality + FemaleMortality) * 100.0 / OpeningPopulation) END) PERSISTED,
    Culling             INT                 NOT NULL CONSTRAINT DF_tblMortality_Cull DEFAULT (0),
    TransferIn          INT                 NOT NULL CONSTRAINT DF_tblMortality_TI DEFAULT (0),
    TransferOut         INT                 NOT NULL CONSTRAINT DF_tblMortality_TO DEFAULT (0),
    ClosingPopulation   AS (OpeningPopulation + TransferIn - MaleMortality - FemaleMortality - Culling - TransferOut) PERSISTED,
    MortalityReasonID   INT                 NULL CONSTRAINT FK_tblMortality_Reason REFERENCES dbo.tblMortalityReason(MortalityReasonID),
    Remarks             NVARCHAR(500)       NULL,
    CreatedBy           INT                 NULL,
    CreatedDate         DATETIME2(0)        NOT NULL CONSTRAINT DF_tblMortality_CreatedDate DEFAULT (SYSDATETIME()),
    ModifiedBy          INT                 NULL,
    ModifiedDate        DATETIME2(0)        NULL,
    CONSTRAINT UQ_tblMortality_FlockWeek UNIQUE (FlockID, WeekNo),
    CONSTRAINT CK_tblMortality_Pop CHECK (OpeningPopulation = MalePopulation + FemalePopulation AND OpeningPopulation >= 0),
    CONSTRAINT CK_tblMortality_NonNeg CHECK (MaleMortality >= 0 AND FemaleMortality >= 0 AND Culling >= 0 AND TransferIn >= 0 AND TransferOut >= 0),
    CONSTRAINT CK_tblMortality_Limit CHECK (MaleMortality <= MalePopulation AND FemaleMortality <= FemalePopulation),
    CONSTRAINT CK_tblMortality_Closing CHECK (OpeningPopulation + TransferIn - MaleMortality - FemaleMortality - Culling - TransferOut >= 0)
);

/* Break-down of mortality by reason (one week may have several causes) */
CREATE TABLE dbo.tblMortalityDetail
(
    MortalityDetailID INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_tblMortalityDetail PRIMARY KEY,
    MortalityID     INT                 NOT NULL CONSTRAINT FK_tblMortalityDetail_Mortality REFERENCES dbo.tblMortality(MortalityID) ON DELETE CASCADE,
    MortalityReasonID INT               NOT NULL CONSTRAINT FK_tblMortalityDetail_Reason REFERENCES dbo.tblMortalityReason(MortalityReasonID),
    Sex             CHAR(1)             NOT NULL,
    BirdCount       INT                 NOT NULL,
    CONSTRAINT CK_tblMortalityDetail CHECK (Sex IN ('F','M') AND BirdCount > 0)
);

CREATE TABLE dbo.tblFlockTransfer
(
    FlockTransferID INT IDENTITY(1,1)   NOT NULL CONSTRAINT PK_tblFlockTransfer PRIMARY KEY,
    FlockID         INT                 NOT NULL CONSTRAINT FK_tblFlockTransfer_Flock REFERENCES dbo.tblFlock(FlockID),
    TransferDate    DATE                NOT NULL,
    WeekNo          INT                 NULL,
    TransferType    NVARCHAR(10)        NOT NULL,   -- IN / OUT / HOUSE (house move)
    FromHouseID     INT                 NULL CONSTRAINT FK_tblFlockTransfer_FromHouse REFERENCES dbo.tblHouse(HouseID),
    ToHouseID       INT                 NULL CONSTRAINT FK_tblFlockTransfer_ToHouse REFERENCES dbo.tblHouse(HouseID),
    CounterFlockID  INT                 NULL CONSTRAINT FK_tblFlockTransfer_Counter REFERENCES dbo.tblFlock(FlockID),
    MaleCount       INT                 NOT NULL CONSTRAINT DF_tblFlockTransfer_M DEFAULT (0),
    FemaleCount     INT                 NOT NULL CONSTRAINT DF_tblFlockTransfer_F DEFAULT (0),
    Reason          NVARCHAR(250)       NULL,
    ApprovedBy      INT                 NULL CONSTRAINT FK_tblFlockTransfer_Approver REFERENCES dbo.tblUsers(UserID),
    CreatedBy       INT                 NULL,
    CreatedDate     DATETIME2(0)        NOT NULL CONSTRAINT DF_tblFlockTransfer_CreatedDate DEFAULT (SYSDATETIME()),
    CONSTRAINT CK_tblFlockTransfer_Type CHECK (TransferType IN (N'IN', N'OUT', N'HOUSE')),
    CONSTRAINT CK_tblFlockTransfer_Count CHECK (MaleCount >= 0 AND FemaleCount >= 0)
);
CREATE INDEX IX_tblFlockTransfer_Flock ON dbo.tblFlockTransfer(FlockID, TransferDate);

CREATE TABLE dbo.tblFlockAdjustment
(
    FlockAdjustmentID INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_tblFlockAdjustment PRIMARY KEY,
    FlockID         INT                 NOT NULL CONSTRAINT FK_tblFlockAdjustment_Flock REFERENCES dbo.tblFlock(FlockID),
    WeekNo          INT                 NULL,
    AdjustmentDate  DATE                NOT NULL,
    MaleAdjustment  INT                 NOT NULL CONSTRAINT DF_tblFlockAdjustment_M DEFAULT (0),   -- +/- birds
    FemaleAdjustment INT                NOT NULL CONSTRAINT DF_tblFlockAdjustment_F DEFAULT (0),
    Reason          NVARCHAR(250)       NOT NULL,
    ApprovedBy      INT                 NULL CONSTRAINT FK_tblFlockAdjustment_Approver REFERENCES dbo.tblUsers(UserID),
    CreatedBy       INT                 NULL,
    CreatedDate     DATETIME2(0)        NOT NULL CONSTRAINT DF_tblFlockAdjustment_CreatedDate DEFAULT (SYSDATETIME()),
    CONSTRAINT CK_tblFlockAdjustment_NonZero CHECK (MaleAdjustment <> 0 OR FemaleAdjustment <> 0)
);
CREATE INDEX IX_tblFlockAdjustment_Flock ON dbo.tblFlockAdjustment(FlockID, AdjustmentDate);

/* =====================================================================
   SECTION G : ANALYSIS / SUPPORT
   ===================================================================== */
CREATE TABLE dbo.tblAlert
(
    AlertID         BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_tblAlert PRIMARY KEY,
    FlockID         INT                 NOT NULL CONSTRAINT FK_tblAlert_Flock REFERENCES dbo.tblFlock(FlockID),
    FlockWeeklyID   INT                 NULL CONSTRAINT FK_tblAlert_Weekly REFERENCES dbo.tblFlockWeekly(FlockWeeklyID),
    WeekNo          INT                 NOT NULL,
    AlertDate       DATETIME2(0)        NOT NULL CONSTRAINT DF_tblAlert_Date DEFAULT (SYSDATETIME()),
    AlertType       NVARCHAR(30)        NOT NULL,   -- BODYWEIGHT, FEED, MORTALITY, UNIFORMITY, NUTRITION, SCORE
    ParameterID     INT                 NULL CONSTRAINT FK_tblAlert_Param REFERENCES dbo.tblParameter(ParameterID),
    StandardValue   DECIMAL(18,4)       NULL,
    ActualValue     DECIMAL(18,4)       NULL,
    Deviation       DECIMAL(18,4)       NULL,
    DeviationPct    DECIMAL(12,4)       NULL,
    Severity        NVARCHAR(20)        NOT NULL CONSTRAINT FK_tblAlert_Severity REFERENCES dbo.tblStatusConfig(StatusCode),
    Message         NVARCHAR(300)       NOT NULL,
    Status          NVARCHAR(20)        NOT NULL CONSTRAINT DF_tblAlert_Status DEFAULT (N'Open'),
    ActionTaken     NVARCHAR(500)       NULL,
    AcknowledgedBy  INT                 NULL CONSTRAINT FK_tblAlert_AckUser REFERENCES dbo.tblUsers(UserID),
    AcknowledgedDate DATETIME2(0)       NULL,
    CONSTRAINT CK_tblAlert_Status CHECK (Status IN (N'Open', N'Acknowledged', N'Closed'))
);
CREATE INDEX IX_tblAlert_Status ON dbo.tblAlert(Status, Severity, AlertDate DESC) INCLUDE (FlockID, WeekNo);
CREATE INDEX IX_tblAlert_Flock ON dbo.tblAlert(FlockID, WeekNo);

CREATE TABLE dbo.tblPerformanceScore
(
    PerformanceScoreID INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_tblPerformanceScore PRIMARY KEY,
    FlockWeeklyID   INT                 NOT NULL CONSTRAINT FK_tblPerformanceScore_Weekly REFERENCES dbo.tblFlockWeekly(FlockWeeklyID) ON DELETE CASCADE,
    FlockID         INT                 NOT NULL CONSTRAINT FK_tblPerformanceScore_Flock REFERENCES dbo.tblFlock(FlockID),
    WeekNo          INT                 NOT NULL,
    BodyWeightScore DECIMAL(9,4)        NULL,
    WeightGainScore DECIMAL(9,4)        NULL,
    FeedIntakeScore DECIMAL(9,4)        NULL,
    UniformityScore DECIMAL(9,4)        NULL,
    MortalityScore  DECIMAL(9,4)        NULL,
    NutritionScore  DECIMAL(9,4)        NULL,
    TotalScore      DECIMAL(9,4)        NOT NULL,
    ScoreBandID     INT                 NULL CONSTRAINT FK_tblPerformanceScore_Band REFERENCES dbo.tblScoreBand(ScoreBandID),
    CalculatedDate  DATETIME2(0)        NOT NULL CONSTRAINT DF_tblPerformanceScore_Date DEFAULT (SYSDATETIME()),
    CONSTRAINT UQ_tblPerformanceScore_Weekly UNIQUE (FlockWeeklyID),
    CONSTRAINT CK_tblPerformanceScore_Range CHECK (TotalScore BETWEEN 0 AND 100)
);
CREATE INDEX IX_tblPerformanceScore_Flock ON dbo.tblPerformanceScore(FlockID, WeekNo);

CREATE TABLE dbo.tblAuditLog
(
    AuditLogID      BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_tblAuditLog PRIMARY KEY,
    UserID          INT                 NULL,
    UserName        NVARCHAR(50)        NULL,
    ActionDate      DATETIME2(0)        NOT NULL CONSTRAINT DF_tblAuditLog_Date DEFAULT (SYSDATETIME()),
    ActionType      NVARCHAR(20)        NOT NULL,   -- INSERT / UPDATE / DELETE / STATUS / LOGIN / ADJUST
    TableName       NVARCHAR(60)        NOT NULL,
    RecordID        NVARCHAR(50)        NULL,
    OldValue        NVARCHAR(MAX)       NULL,       -- JSON
    NewValue        NVARCHAR(MAX)       NULL,       -- JSON
    IPAddress       NVARCHAR(50)        NULL
);
CREATE INDEX IX_tblAuditLog_Table ON dbo.tblAuditLog(TableName, RecordID, ActionDate DESC);
CREATE INDEX IX_tblAuditLog_Date ON dbo.tblAuditLog(ActionDate DESC) INCLUDE (UserName, ActionType, TableName);

/* =====================================================================
   SECTION H : OPTIONAL BREEDER MODULES (activate via tblModule)
   ===================================================================== */
CREATE TABLE dbo.tblEggProductionWeekly
(
    EggProductionID     INT IDENTITY(1,1)   NOT NULL CONSTRAINT PK_tblEggProductionWeekly PRIMARY KEY,
    FlockWeeklyID       INT                 NOT NULL CONSTRAINT FK_tblEggProduction_Weekly REFERENCES dbo.tblFlockWeekly(FlockWeeklyID) ON DELETE CASCADE,
    HenHousedCount      INT                 NOT NULL,   -- females housed at start of lay
    TotalEggs           INT                 NOT NULL CONSTRAINT DF_tblEggProduction_Total DEFAULT (0),
    HatchingEggs        INT                 NOT NULL CONSTRAINT DF_tblEggProduction_HE DEFAULT (0),
    FloorEggs           INT                 NOT NULL CONSTRAINT DF_tblEggProduction_Floor DEFAULT (0),
    DirtyEggs           INT                 NOT NULL CONSTRAINT DF_tblEggProduction_Dirty DEFAULT (0),
    BrokenEggs          INT                 NOT NULL CONSTRAINT DF_tblEggProduction_Broken DEFAULT (0),
    CrackedEggs         INT                 NOT NULL CONSTRAINT DF_tblEggProduction_Cracked DEFAULT (0),
    DoubleYolkEggs      INT                 NOT NULL CONSTRAINT DF_tblEggProduction_DY DEFAULT (0),
    AvgEggWeight        DECIMAL(8,2)        NULL,
    HenDayProductionPct DECIMAL(9,4)        NULL,
    HenHousedEggsCum    DECIMAL(12,4)       NULL,
    StandardProductionPct DECIMAL(9,4)      NULL,
    Remarks             NVARCHAR(250)       NULL,
    CONSTRAINT UQ_tblEggProduction_Weekly UNIQUE (FlockWeeklyID),
    CONSTRAINT CK_tblEggProduction CHECK (TotalEggs >= 0 AND HatchingEggs <= TotalEggs)
);

CREATE TABLE dbo.tblHatchResult
(
    HatchResultID   INT IDENTITY(1,1)   NOT NULL CONSTRAINT PK_tblHatchResult PRIMARY KEY,
    FlockID         INT                 NOT NULL CONSTRAINT FK_tblHatchResult_Flock REFERENCES dbo.tblFlock(FlockID),
    WeekNo          INT                 NOT NULL,
    SetDate         DATE                NOT NULL,
    HatchDate       DATE                NULL,
    SettableEggs    INT                 NOT NULL,
    InfertileEggs   INT                 NOT NULL CONSTRAINT DF_tblHatchResult_Inf DEFAULT (0),
    FertileEggs     AS (SettableEggs - InfertileEggs) PERSISTED,
    DeadInShell     INT                 NOT NULL CONSTRAINT DF_tblHatchResult_DIS DEFAULT (0),
    HatchedChicks   INT                 NOT NULL CONSTRAINT DF_tblHatchResult_Chicks DEFAULT (0),
    SaleableChicks  INT                 NULL,
    FertilityPct    AS (CASE WHEN SettableEggs > 0 THEN CONVERT(DECIMAL(9,4), (SettableEggs - InfertileEggs) * 100.0 / SettableEggs) END) PERSISTED,
    HatchabilityPct AS (CASE WHEN SettableEggs > 0 THEN CONVERT(DECIMAL(9,4), HatchedChicks * 100.0 / SettableEggs) END) PERSISTED,
    HatchOfFertilePct AS (CASE WHEN SettableEggs - InfertileEggs > 0 THEN CONVERT(DECIMAL(9,4), HatchedChicks * 100.0 / (SettableEggs - InfertileEggs)) END) PERSISTED,
    Remarks         NVARCHAR(250)       NULL,
    CreatedBy       INT                 NULL,
    CreatedDate     DATETIME2(0)        NOT NULL CONSTRAINT DF_tblHatchResult_CreatedDate DEFAULT (SYSDATETIME()),
    CONSTRAINT CK_tblHatchResult CHECK (SettableEggs >= 0 AND InfertileEggs <= SettableEggs AND HatchedChicks + DeadInShell <= SettableEggs - InfertileEggs)
);
CREATE INDEX IX_tblHatchResult_Flock ON dbo.tblHatchResult(FlockID, WeekNo);
GO

SET NOEXEC OFF;
GO
PRINT N'Schema created successfully.';
GO
