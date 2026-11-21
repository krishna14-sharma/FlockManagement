/* =====================================================================
   FLOCK WEIGHT MANAGEMENT & ANALYSIS SYSTEM
   Script 03 : Seed / reference data (Phase 1)
   Default logins (all must change password at first login except admin):
       admin        / Admin@123   (Administrator)
       manager      / Demo@123    (Farm Manager)
       supervisor   / Demo@123    (Farm Supervisor)
       nutritionist / Demo@123    (Nutritionist)
       vet          / Demo@123    (Veterinarian)
       operator     / Demo@123    (Data Entry Operator)
       viewer       / Demo@123    (Management / Viewer)
   CHANGE THESE PASSWORDS BEFORE GOING LIVE.
   ===================================================================== */
USE FlockManagementDB;
GO
SET NOCOUNT ON;
SET XACT_ABORT ON;
GO
IF EXISTS (SELECT 1 FROM dbo.tblRoles)
BEGIN
    RAISERROR(N'Seed data already present. Script stopped (no changes made).', 16, 1);
    SET NOEXEC ON;
END
GO
BEGIN TRANSACTION;

/* ---------- Roles ---------- */
INSERT INTO dbo.tblRoles (RoleName, Description, IsSystem) VALUES
 (N'Administrator',       N'Full system access including users, roles and configuration', 1),
 (N'Farm Manager',        N'Manages farms, flocks, standards and approves adjustments', 1),
 (N'Farm Supervisor',     N'Supervises daily/weekly flock operations', 1),
 (N'Nutritionist',        N'Manages feed, formulas and nutritional standards', 1),
 (N'Veterinarian',        N'Reviews mortality, health and alerts', 1),
 (N'Data Entry Operator', N'Enters weekly flock data', 1),
 (N'Management / Viewer', N'Read-only access to dashboards, analysis and reports', 1);

/* ---------- Permissions (Phase 1). Later phase scripts add their own rows. ---------- */
INSERT INTO dbo.tblPermissions (PermissionCode, PermissionName, MenuGroup, MenuText, ControllerName, ActionName, IconClass, IsMenu, SortOrder) VALUES
 (N'DASHBOARD_VIEW', N'View dashboard',              N'Dashboard',      N'Dashboard',        N'Home',      N'Index', N'bi-speedometer2',     1, 10),
 (N'FARM_VIEW',      N'View farms',                  N'Master Data',    N'Farms',            N'Farm',      N'Index', N'bi-geo-alt',          1, 110),
 (N'FARM_EDIT',      N'Create / edit farms',         N'Master Data',    NULL,                NULL,         NULL,     NULL,                   0, 111),
 (N'HOUSE_VIEW',     N'View houses / sheds',         N'Master Data',    N'Houses / Sheds',   N'House',     N'Index', N'bi-house-door',       1, 120),
 (N'HOUSE_EDIT',     N'Create / edit houses',        N'Master Data',    NULL,                NULL,         NULL,     NULL,                   0, 121),
 (N'BIRDTYPE_VIEW',  N'View bird types',             N'Master Data',    N'Bird Types',       N'BirdType',  N'Index', N'bi-tags',             1, 130),
 (N'BIRDTYPE_EDIT',  N'Create / edit bird types',    N'Master Data',    NULL,                NULL,         NULL,     NULL,                   0, 131),
 (N'BREED_VIEW',     N'View breeds',                 N'Master Data',    N'Breeds',           N'Breed',     N'Index', N'bi-diagram-3',        1, 140),
 (N'BREED_EDIT',     N'Create / edit breeds',        N'Master Data',    NULL,                NULL,         NULL,     NULL,                   0, 141),
 (N'LOT_VIEW',       N'View lots',                   N'Master Data',    N'Lots',             N'Lot',       N'Index', N'bi-collection',       1, 150),
 (N'LOT_EDIT',       N'Create / edit lots',          N'Master Data',    NULL,                NULL,         NULL,     NULL,                   0, 151),
 (N'FEEDTYPE_VIEW',  N'View feed types',             N'Master Data',    N'Feed Types',       N'FeedType',  N'Index', N'bi-basket',           1, 160),
 (N'FEEDTYPE_EDIT',  N'Create / edit feed types',    N'Master Data',    NULL,                NULL,         NULL,     NULL,                   0, 161),
 (N'FLOCK_VIEW',     N'View flocks',                 N'Master Data',    N'Flocks',           N'Flock',     N'Index', N'bi-egg',              1, 170),
 (N'FLOCK_EDIT',     N'Create / edit flocks',        N'Master Data',    NULL,                NULL,         NULL,     NULL,                   0, 171),
 (N'FLOCK_STATUS',   N'Close / deplete flocks',      N'Master Data',    NULL,                NULL,         NULL,     NULL,                   0, 172),
 (N'USER_VIEW',      N'View users',                  N'Administration', N'Users',            N'User',      N'Index', N'bi-people',           1, 910),
 (N'USER_EDIT',      N'Create / edit users',         N'Administration', NULL,                NULL,         NULL,     NULL,                   0, 911),
 (N'ROLE_VIEW',      N'View roles & permissions',    N'Administration', N'Roles & Permissions', N'Role',   N'Index', N'bi-shield-lock',      1, 920),
 (N'ROLE_EDIT',      N'Edit roles & permissions',    N'Administration', NULL,                NULL,         NULL,     NULL,                   0, 921),
 (N'AUDIT_VIEW',     N'View audit trail',            N'Administration', N'Audit Trail',      N'AuditLog',  N'Index', N'bi-clock-history',    1, 930);

/* ---------- Role -> permission mapping ---------- */
DECLARE @rAdmin INT = (SELECT RoleID FROM dbo.tblRoles WHERE RoleName = N'Administrator');
DECLARE @rMgr   INT = (SELECT RoleID FROM dbo.tblRoles WHERE RoleName = N'Farm Manager');
DECLARE @rSup   INT = (SELECT RoleID FROM dbo.tblRoles WHERE RoleName = N'Farm Supervisor');
DECLARE @rNut   INT = (SELECT RoleID FROM dbo.tblRoles WHERE RoleName = N'Nutritionist');
DECLARE @rVet   INT = (SELECT RoleID FROM dbo.tblRoles WHERE RoleName = N'Veterinarian');
DECLARE @rDEO   INT = (SELECT RoleID FROM dbo.tblRoles WHERE RoleName = N'Data Entry Operator');
DECLARE @rView  INT = (SELECT RoleID FROM dbo.tblRoles WHERE RoleName = N'Management / Viewer');

INSERT INTO dbo.tblRolePermissions (RoleID, PermissionID)
SELECT @rAdmin, PermissionID FROM dbo.tblPermissions;

INSERT INTO dbo.tblRolePermissions (RoleID, PermissionID)
SELECT @rMgr, PermissionID FROM dbo.tblPermissions
WHERE PermissionCode NOT IN (N'USER_EDIT', N'ROLE_EDIT', N'ROLE_VIEW');

INSERT INTO dbo.tblRolePermissions (RoleID, PermissionID)
SELECT @rSup, PermissionID FROM dbo.tblPermissions
WHERE PermissionCode LIKE N'%[_]VIEW' AND PermissionCode NOT IN (N'USER_VIEW', N'ROLE_VIEW', N'AUDIT_VIEW')
   OR PermissionCode IN (N'FLOCK_EDIT', N'LOT_EDIT');

INSERT INTO dbo.tblRolePermissions (RoleID, PermissionID)
SELECT @rNut, PermissionID FROM dbo.tblPermissions
WHERE PermissionCode LIKE N'%[_]VIEW' AND PermissionCode NOT IN (N'USER_VIEW', N'ROLE_VIEW', N'AUDIT_VIEW')
   OR PermissionCode IN (N'FEEDTYPE_EDIT');

INSERT INTO dbo.tblRolePermissions (RoleID, PermissionID)
SELECT @rVet, PermissionID FROM dbo.tblPermissions
WHERE PermissionCode LIKE N'%[_]VIEW' AND PermissionCode NOT IN (N'USER_VIEW', N'ROLE_VIEW', N'AUDIT_VIEW');

INSERT INTO dbo.tblRolePermissions (RoleID, PermissionID)
SELECT @rDEO, PermissionID FROM dbo.tblPermissions
WHERE PermissionCode IN (N'DASHBOARD_VIEW', N'FLOCK_VIEW', N'HOUSE_VIEW', N'FARM_VIEW', N'LOT_VIEW', N'FEEDTYPE_VIEW');

INSERT INTO dbo.tblRolePermissions (RoleID, PermissionID)
SELECT @rView, PermissionID FROM dbo.tblPermissions
WHERE PermissionCode LIKE N'%[_]VIEW' AND PermissionCode NOT IN (N'USER_VIEW', N'ROLE_VIEW', N'AUDIT_VIEW');

/* ---------- Farms & houses ---------- */
INSERT INTO dbo.tblFarm (FarmCode, FarmName, Location, Address, ContactPerson, ContactNumber) VALUES
 (N'FRM01', N'Hosur Breeder Farm',    N'Hosur, Tamil Nadu',    N'Survey No. 112, Bagalur Road, Hosur', N'R. Kumar',  N'+91 98400 00001'),
 (N'FRM02', N'Namakkal Breeder Farm', N'Namakkal, Tamil Nadu', N'Mohanur Road, Namakkal',             N'S. Prakash', N'+91 98400 00002');

DECLARE @f1 INT = (SELECT FarmID FROM dbo.tblFarm WHERE FarmCode = N'FRM01');
DECLARE @f2 INT = (SELECT FarmID FROM dbo.tblFarm WHERE FarmCode = N'FRM02');

INSERT INTO dbo.tblHouse (FarmID, HouseCode, HouseName, HouseType, Capacity, AreaSqFt) VALUES
 (@f1, N'H01', N'House 1', N'Environment Controlled', 12000, 24000),
 (@f1, N'H02', N'House 2', N'Environment Controlled', 12000, 24000),
 (@f1, N'H03', N'House 3', N'Curtain Sided',          10000, 20000),
 (@f2, N'H01', N'House 1', N'Environment Controlled', 15000, 30000),
 (@f2, N'H02', N'House 2', N'Open Sided',             10000, 20000);

/* ---------- Users (password hashes: PBKDF2-SHA256, 100,000 iterations) ---------- */
INSERT INTO dbo.tblUsers (UserName, FullName, Email, PasswordHash, RoleID, FarmID, MustChangePassword) VALUES
 (N'admin',        N'System Administrator', N'admin@example.com',        N'PBKDF2-SHA256$100000$SQqOajIwi2v4IykpNgXj5g==$WLoj3hWr4K2YOkYa4QjD4tFPLW//O6m1sth0rXvrqSA=', @rAdmin, NULL, 0),
 (N'manager',      N'Farm Manager (Demo)',  N'manager@example.com',      N'PBKDF2-SHA256$100000$/+QoAfCTMx4N043rPwPlvw==$vggplNgxis1yq/AUPMnhF79Ec4JjEWovKAsw2401YlA=', @rMgr,   NULL, 1),
 (N'supervisor',   N'Farm Supervisor (Demo)', N'supervisor@example.com', N'PBKDF2-SHA256$100000$b22cVdZpz1jR39MVr7NHIw==$Xa0oPZpA8pLz/HFsTjzB0aE0pyZVzWwlQiIdh196TS8=', @rSup,   @f1,  1),
 (N'nutritionist', N'Nutritionist (Demo)',  N'nutrition@example.com',    N'PBKDF2-SHA256$100000$TT8Lp75fOatx71Z+FCfScw==$XChEx+o+7Hsv+Un/6aC1i7lvBGFpqufiN4T1xRWHbTY=', @rNut,   NULL, 1),
 (N'vet',          N'Veterinarian (Demo)',  N'vet@example.com',          N'PBKDF2-SHA256$100000$JlbTuNLmXUQwdyrZxnl23g==$WkQbCffYQNdlb2NwOTSG9kL3cGRgZem8V1fOi1he88U=', @rVet,   NULL, 1),
 (N'operator',     N'Data Entry (Demo)',    N'operator@example.com',     N'PBKDF2-SHA256$100000$eG22XlzLyoJu5aU+6bkyog==$yZNognuCiLvpfIU/fgymJAlBEv0yuEiP5qL9cuWm6fY=', @rDEO,   @f1,  1),
 (N'viewer',       N'Management (Demo)',    N'viewer@example.com',       N'PBKDF2-SHA256$100000$ayAXR0mltvv+Z51bu5o3mQ==$+aWv0p/Pfs5PasAh+DmHrRrpZCI89jEv1dQLC5DJmf8=', @rView,  NULL, 1);

DECLARE @admin INT = (SELECT UserID FROM dbo.tblUsers WHERE UserName = N'admin');

/* ---------- Bird types / breeds / lots ---------- */
INSERT INTO dbo.tblBirdType (BirdTypeCode, BirdTypeName, Description, CreatedBy) VALUES
 (N'FB', N'Female Breeder', N'Female parent-stock birds', @admin),
 (N'MB', N'Male Breeder',   N'Male parent-stock birds',   @admin),
 (N'PS', N'Parent Stock',   N'Mixed-sex parent stock flock', @admin),
 (N'OT', N'Other',          N'Other bird categories',     @admin);

INSERT INTO dbo.tblBreed (BreedCode, BreedName, Supplier, CreatedBy) VALUES
 (N'COBB500', N'Cobb 500 FF',  N'Cobb-Vantress', @admin),
 (N'ROSS308', N'Ross 308 AP',  N'Aviagen',       @admin),
 (N'HUBCL',   N'Hubbard Classic', N'Hubbard',    @admin),
 (N'VEN400',  N'Vencobb 400',  N'Venky''s',      @admin);

INSERT INTO dbo.tblLot (LotNumber, LotName, FarmID, CreatedBy) VALUES
 (N'LOT-2026-01', N'Batch Q2-2026 A', @f1, @admin),
 (N'LOT-2026-02', N'Batch Q2-2026 B', @f1, @admin),
 (N'LOT-2026-03', N'Batch Q2-2026 C', @f2, @admin);

/* ---------- Units & parameters ---------- */
INSERT INTO dbo.tblUnit (UnitCode, UnitName) VALUES
 (N'g', N'Grams'), (N'kg', N'Kilograms'), (N'kcal/kg', N'Kilocalories per kg'), (N'%', N'Percent'),
 (N'g/kg', N'Grams per kg'), (N'mg/kg', N'Milligrams per kg'), (N'g/bird/day', N'Grams per bird per day'),
 (N'birds', N'Birds'), (N'eggs', N'Eggs'), (N'pts', N'Score points');

DECLARE @uG INT = (SELECT UnitID FROM dbo.tblUnit WHERE UnitCode = N'g');
DECLARE @uKcal INT = (SELECT UnitID FROM dbo.tblUnit WHERE UnitCode = N'kcal/kg');
DECLARE @uPct INT = (SELECT UnitID FROM dbo.tblUnit WHERE UnitCode = N'%');
DECLARE @uGbd INT = (SELECT UnitID FROM dbo.tblUnit WHERE UnitCode = N'g/bird/day');
DECLARE @uEgg INT = (SELECT UnitID FROM dbo.tblUnit WHERE UnitCode = N'eggs');
DECLARE @uKg  INT = (SELECT UnitID FROM dbo.tblUnit WHERE UnitCode = N'kg');

INSERT INTO dbo.tblParameter (ParameterCode, ParameterName, ParameterGroup, UnitID, DecimalPlaces, Direction, LogicalMin, LogicalMax, SortOrder) VALUES
 (N'FEED_INTAKE',   N'Feed Intake',        N'FEED',       @uGbd,  1, 'T', 0,    400,   10),
 (N'WEIGHT_GAIN',   N'Weight Gain',        N'WEIGHTGAIN', @uG,    0, 'T', -500, 1000,  20),
 (N'BODY_WEIGHT',   N'Body Weight',        N'BODYWEIGHT', @uG,    0, 'T', 0,    7000,  30),
 (N'ENERGY',        N'Energy',             N'NUTRITION',  @uKcal, 0, 'T', 1500, 3800,  40),
 (N'PROTEIN',       N'Protein',            N'NUTRITION',  @uPct,  2, 'T', 0,    40,    50),
 (N'METHIONINE',    N'Methionine',         N'NUTRITION',  @uPct,  3, 'T', 0,    2,     60),
 (N'LYSINE',        N'Lysine',             N'NUTRITION',  @uPct,  3, 'T', 0,    3,     70),
 (N'LINOLEIC_ACID', N'Linoleic Acid',      N'NUTRITION',  @uPct,  2, 'T', 0,    6,     80),
 (N'CALCIUM',       N'Calcium',            N'NUTRITION',  @uPct,  2, 'T', 0,    6,     90),
 (N'PHOSPHORUS',    N'Phosphorus (avail.)', N'NUTRITION', @uPct,  2, 'T', 0,    2,    100),
 (N'FAT',           N'Fat',                N'NUTRITION',  @uPct,  2, 'T', 0,    15,   110),
 (N'FIBRE',         N'Fibre',              N'NUTRITION',  @uPct,  2, 'T', 0,    15,   120),
 (N'SALT',          N'Salt',               N'NUTRITION',  @uPct,  3, 'T', 0,    1,    130),
 (N'MORTALITY',     N'Weekly Mortality',   N'MORTALITY',  @uPct,  2, 'L', 0,    100,  140),
 (N'UNIFORMITY',    N'Uniformity',         N'UNIFORMITY', @uPct,  1, 'H', 0,    100,  150),
 (N'MALE_BODY_WEIGHT', N'Male Body Weight', N'BODYWEIGHT', @uG,   0, 'T', 0,    8000, 160),
 (N'MALE_FEED_INTAKE', N'Male Feed Intake', N'FEED',      @uGbd,  1, 'T', 0,    400,  170),
 (N'MALE_UNIFORMITY',  N'Male Uniformity',  N'UNIFORMITY', @uPct, 1, 'H', 0,    100,  180),
 (N'MALE_MORTALITY',   N'Male Mortality',   N'MORTALITY',  @uPct, 2, 'L', 0,    100,  190),
 (N'HD_PRODUCTION',    N'Hen-Day Production', N'PRODUCTION', @uPct, 2, 'H', 0,  100,  200),
 (N'HH_EGGS',          N'Hen-Housed Eggs (cum.)', N'PRODUCTION', @uEgg, 1, 'H', 0, 300, 210),
 (N'HATCHING_EGG_PCT', N'Hatching Egg %',   N'PRODUCTION', @uPct, 2, 'H', 0,    100,  220),
 (N'FEED_PER_DOZEN',   N'Feed per Dozen Eggs', N'PRODUCTION', @uKg, 3, 'L', 0,  10,   230),
 (N'FERTILITY',        N'Fertility %',      N'FERTILITY',  @uPct, 2, 'H', 0,    100,  240),
 (N'HATCHABILITY',     N'Hatchability %',   N'HATCH',      @uPct, 2, 'H', 0,    100,  250);

/* ---------- Status configuration (centralized colours / CSS) ---------- */
INSERT INTO dbo.tblStatusConfig (StatusCode, StatusName, StatusGroup, BadgeCssClass, ColorHex, SortOrder) VALUES
 (N'WITHIN',    N'Within Standard', N'VARIANCE', N'status-within',   '#198754', 10),
 (N'BELOW',     N'Below Standard',  N'VARIANCE', N'status-warning',  '#e0a800', 20),
 (N'ABOVE',     N'Above Standard',  N'VARIANCE', N'status-warning',  '#e0a800', 30),
 (N'CRITICAL',  N'Critical',        N'VARIANCE', N'status-critical', '#dc3545', 40),
 (N'WARNING',   N'Warning',         N'GENERAL',  N'status-warning',  '#e0a800', 50),
 (N'INFO',      N'Information',     N'GENERAL',  N'status-info',     '#0d6efd', 60),
 (N'NODATA',    N'No Data',         N'GENERAL',  N'status-nodata',   '#6c757d', 70),
 (N'EXCELLENT', N'Excellent',       N'GRADE',    N'status-within',   '#198754', 80),
 (N'GOOD',      N'Good',            N'GRADE',    N'status-good',     '#20c997', 90),
 (N'AVERAGE',   N'Average',         N'GRADE',    N'status-info',     '#0d6efd', 100),
 (N'POOR',      N'Poor',            N'GRADE',    N'status-warning',  '#e0a800', 110);

/* ---------- Tolerances (|variance %| thresholds) ---------- */
INSERT INTO dbo.tblToleranceConfig (ParameterID, WarningPct, CriticalPct)
SELECT NULL, 5, 10
UNION ALL SELECT ParameterID, 5, 10 FROM dbo.tblParameter WHERE ParameterCode IN (N'BODY_WEIGHT', N'MALE_BODY_WEIGHT')
UNION ALL SELECT ParameterID, 8, 15 FROM dbo.tblParameter WHERE ParameterCode IN (N'FEED_INTAKE', N'MALE_FEED_INTAKE')
UNION ALL SELECT ParameterID, 10, 25 FROM dbo.tblParameter WHERE ParameterCode = N'WEIGHT_GAIN'
UNION ALL SELECT ParameterID, 3, 8 FROM dbo.tblParameter WHERE ParameterGroup = N'NUTRITION'
UNION ALL SELECT ParameterID, 50, 100 FROM dbo.tblParameter WHERE ParameterCode IN (N'MORTALITY', N'MALE_MORTALITY')
UNION ALL SELECT ParameterID, 5, 10 FROM dbo.tblParameter WHERE ParameterCode IN (N'UNIFORMITY', N'MALE_UNIFORMITY');

/* ---------- System settings ---------- */
INSERT INTO dbo.tblSystemSetting (SettingKey, SettingValue, DataType, Description) VALUES
 (N'UNIFORMITY_RANGE_PCT',     N'10',  N'DECIMAL', N'Birds within +/- this % of average weight count as uniform'),
 (N'UNIFORMITY_EXCELLENT_MIN', N'85',  N'DECIMAL', N'Uniformity % at or above = Excellent (fallback when no standard)'),
 (N'UNIFORMITY_GOOD_MIN',      N'80',  N'DECIMAL', N'Uniformity % at or above = Good'),
 (N'UNIFORMITY_WARNING_MIN',   N'70',  N'DECIMAL', N'Uniformity % at or above = Warning; below = Critical'),
 (N'MIN_WEIGHT_SAMPLE_SIZE',   N'50',  N'INT',     N'Recommended minimum birds weighed per sex per house'),
 (N'MAX_FAILED_LOGINS',        N'5',   N'INT',     N'Consecutive failed logins before the account is locked'),
 (N'MIN_PASSWORD_LENGTH',      N'8',   N'INT',     N'Minimum password length'),
 (N'DEFAULT_PAGE_SIZE',        N'25',  N'INT',     N'Rows per page in lists'),
 (N'APP_TITLE',                N'Flock Weight Management', N'STRING', N'Application title');

/* ---------- Performance score configuration ---------- */
INSERT INTO dbo.tblScoreWeightage (ComponentCode, ComponentName, Weightage, ZeroScoreDeviationPct, EffectiveFrom) VALUES
 (N'BODYWEIGHT', N'Body Weight',            30, 15, '2020-01-01'),
 (N'WEIGHTGAIN', N'Weight Gain',            15, 40, '2020-01-01'),
 (N'FEED',       N'Feed Intake',            15, 20, '2020-01-01'),
 (N'UNIFORMITY', N'Uniformity',             20, 25, '2020-01-01'),
 (N'MORTALITY',  N'Mortality',              15, 300, '2020-01-01'),
 (N'NUTRITION',  N'Nutritional Compliance',  5, 15, '2020-01-01');

INSERT INTO dbo.tblScoreBand (BandName, MinScore, MaxScore, StatusCode) VALUES
 (N'Excellent', 90, 100,     N'EXCELLENT'),
 (N'Good',      80, 89.9999, N'GOOD'),
 (N'Average',   70, 79.9999, N'AVERAGE'),
 (N'Poor',      60, 69.9999, N'POOR'),
 (N'Critical',  0,  59.9999, N'CRITICAL');

/* ---------- Optional modules ---------- */
INSERT INTO dbo.tblModule (ModuleCode, ModuleName, IsEnabled) VALUES
 (N'CORE',             N'Flock Weight Management (core)', 1),
 (N'MALE_PERFORMANCE', N'Male Performance',               0),
 (N'EGG_PRODUCTION',   N'Egg Production',                 0),
 (N'FERTILITY',        N'Fertility',                      0),
 (N'HATCHABILITY',     N'Hatchability',                   0);

/* ---------- Feed types ---------- */
INSERT INTO dbo.tblFeedType (FeedTypeCode, FeedTypeName, TargetSex, ApplicableFromWeek, ApplicableToWeek, Description, CreatedBy) VALUES
 (N'BST',  N'Breeder Starter',  'B', 0,  4,  N'Chick starter crumble', @admin),
 (N'DEV',  N'Developer',        'B', 5,  18, N'Grower / developer phase', @admin),
 (N'BPL',  N'Breeder Pre-Lay',  'F', 19, 23, N'Pre-lay transition feed', @admin),
 (N'BL1',  N'Breeder Layer',    'F', 24, 66, N'Production phase layer feed', @admin),
 (N'MALE', N'Male Feed',        'M', 19, 66, N'Male breeder maintenance feed', @admin),
 (N'FEM',  N'Female Feed',      'F', 19, 66, N'Female breeder feed (separate sex feeding)', @admin);

/* ---------- Mortality reasons ---------- */
INSERT INTO dbo.tblMortalityReason (ReasonCode, ReasonName, Category, CreatedBy) VALUES
 (N'NAT',  N'Natural / Unknown',          N'Other',      @admin),
 (N'RESP', N'Respiratory disease',        N'Disease',    @admin),
 (N'ENT',  N'Enteric disease',            N'Disease',    @admin),
 (N'LEG',  N'Leg problems',               N'Management', @admin),
 (N'HEAT', N'Heat stress',                N'Management', @admin),
 (N'PICK', N'Pecking / cannibalism',      N'Management', @admin),
 (N'PROL', N'Prolapse',                   N'Other',      @admin),
 (N'SDS',  N'Sudden death syndrome',      N'Disease',    @admin),
 (N'CULL', N'Culled - weak / runt',       N'Culling',    @admin),
 (N'CULS', N'Culled - sexing error',      N'Culling',    @admin);

/* ---------- Sample flocks (hatch dates relative to today so ages stay realistic) ---------- */
DECLARE @btF INT = (SELECT BirdTypeID FROM dbo.tblBirdType WHERE BirdTypeCode = N'FB');
DECLARE @btM INT = (SELECT BirdTypeID FROM dbo.tblBirdType WHERE BirdTypeCode = N'MB');
DECLARE @btP INT = (SELECT BirdTypeID FROM dbo.tblBirdType WHERE BirdTypeCode = N'PS');
DECLARE @brCobb INT = (SELECT BreedID FROM dbo.tblBreed WHERE BreedCode = N'COBB500');
DECLARE @brRoss INT = (SELECT BreedID FROM dbo.tblBreed WHERE BreedCode = N'ROSS308');
DECLARE @l1 INT = (SELECT LotID FROM dbo.tblLot WHERE LotNumber = N'LOT-2026-01');
DECLARE @l2 INT = (SELECT LotID FROM dbo.tblLot WHERE LotNumber = N'LOT-2026-02');
DECLARE @l3 INT = (SELECT LotID FROM dbo.tblLot WHERE LotNumber = N'LOT-2026-03');
DECLARE @h11 INT = (SELECT HouseID FROM dbo.tblHouse WHERE FarmID = @f1 AND HouseCode = N'H01');
DECLARE @h12 INT = (SELECT HouseID FROM dbo.tblHouse WHERE FarmID = @f1 AND HouseCode = N'H02');
DECLARE @h21 INT = (SELECT HouseID FROM dbo.tblHouse WHERE FarmID = @f2 AND HouseCode = N'H01');
DECLARE @today DATE = CONVERT(DATE, GETDATE());

INSERT INTO dbo.tblFlock (FlockNumber, LotID, BirdTypeID, BreedID, FarmID, HouseID, PlacementDate, HatchDate,
                          InitialBirdCount, MaleCount, FemaleCount, CurrentMaleCount, CurrentFemaleCount, Source, Status, CreatedBy)
VALUES
 (N'F001', @l1, @btF, @brCobb, @f1, @h11, DATEADD(DAY, 1, DATEADD(WEEK, -25, @today)), DATEADD(WEEK, -25, @today), 10000, 0,    10000, 0,    10000, N'Hosur Hatchery', N'Active', @admin),
 (N'F002', @l2, @btP, @brCobb, @f1, @h12, DATEADD(DAY, 1, DATEADD(WEEK, -25, @today)), DATEADD(WEEK, -25, @today), 11000, 1300, 9700,  1300, 9700,  N'Hosur Hatchery', N'Active', @admin),
 (N'F003', @l3, @btM, @brRoss, @f2, @h21, DATEADD(DAY, 1, DATEADD(WEEK, -32, @today)), DATEADD(WEEK, -32, @today), 1500,  1500, 0,     1500, 0,     N'Aviagen India', N'Active', @admin);

INSERT INTO dbo.tblFlockPlacement (FlockID, PlacementDate, HouseID, MaleCount, FemaleCount, Source, ChickAvgWeight, IsInitial, Remarks, CreatedBy)
SELECT FlockID, PlacementDate, HouseID, MaleCount, FemaleCount, Source, 40.00, 1, N'Initial placement (seed)', @admin
FROM dbo.tblFlock;

COMMIT TRANSACTION;
PRINT N'Seed data inserted.';
GO
SET NOEXEC OFF;
GO
