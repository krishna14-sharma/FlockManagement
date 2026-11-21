/* =====================================================================
   FLOCK WEIGHT MANAGEMENT & ANALYSIS SYSTEM
   Script 04 : Schema additions for Phases 2-5 (safe to re-run)
   Adds per-sex population movement columns to tblFlockWeekly and
   nutrition-source columns, production / environment columns.
   No existing data is changed.
   ===================================================================== */
USE FlockManagementDB;
GO
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

IF COL_LENGTH('dbo.tblFlockWeekly', 'ReceivedMale') IS NULL
BEGIN
    ALTER TABLE dbo.tblFlockWeekly ADD
        ReceivedMale        INT NOT NULL CONSTRAINT DF_tblFlockWeekly_RecM DEFAULT (0),
        ReceivedFemale      INT NOT NULL CONSTRAINT DF_tblFlockWeekly_RecF DEFAULT (0),
        TransferOutMale     INT NOT NULL CONSTRAINT DF_tblFlockWeekly_OutM DEFAULT (0),
        TransferOutFemale   INT NOT NULL CONSTRAINT DF_tblFlockWeekly_OutF DEFAULT (0),
        ClosingMale         INT NOT NULL CONSTRAINT DF_tblFlockWeekly_CloM DEFAULT (0),
        ClosingFemale       INT NOT NULL CONSTRAINT DF_tblFlockWeekly_CloF DEFAULT (0),
        ManualBodyWeight    DECIMAL(10,2) NULL,
        NutritionSource     CHAR(1) NULL,          -- L = lab analysis, F = feed formula values
        LabSampleRef        NVARCHAR(50) NULL,
        AnalysisDate        DATE NULL;
END
GO

IF OBJECT_ID(N'dbo.CK_tblFlockWeekly_Sex', N'C') IS NULL
    ALTER TABLE dbo.tblFlockWeekly ADD CONSTRAINT CK_tblFlockWeekly_Sex CHECK
        (ReceivedMale >= 0 AND ReceivedFemale >= 0 AND TransferOutMale >= 0 AND TransferOutFemale >= 0
         AND ClosingMale >= 0 AND ClosingFemale >= 0 AND (NutritionSource IS NULL OR NutritionSource IN ('L','F')));
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_tblStandard_Unique' AND object_id = OBJECT_ID(N'dbo.tblStandard'))
    CREATE UNIQUE INDEX IX_tblStandard_Unique
        ON dbo.tblStandard (BirdTypeID, BreedID, FeedTypeID, ParameterID, AgeWeek, EffectiveFrom)
        WHERE IsActive = 1;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_tblFlockPlacement_Date' AND object_id = OBJECT_ID(N'dbo.tblFlockPlacement'))
    CREATE INDEX IX_tblFlockPlacement_Date ON dbo.tblFlockPlacement (FlockID, PlacementDate) INCLUDE (MaleCount, FemaleCount, IsInitial);
GO

/* ---------- Production & environment (egg production, egg weight, lighting, temperature) ---------- */
IF COL_LENGTH('dbo.tblFlockWeekly', 'LightHours') IS NULL
BEGIN
    ALTER TABLE dbo.tblFlockWeekly ADD
        LightHours          DECIMAL(5,2)  NULL,    -- hours of light per day
        LightIntensity      DECIMAL(8,2)  NULL,    -- lux at bird level
        TempDay             DECIMAL(5,2)  NULL,    -- average day temperature, deg C
        TempNight           DECIMAL(5,2)  NULL;    -- average night temperature, deg C
END
GO

IF OBJECT_ID(N'dbo.CK_tblFlockWeekly_Env', N'C') IS NULL
    ALTER TABLE dbo.tblFlockWeekly ADD CONSTRAINT CK_tblFlockWeekly_Env CHECK
        ((LightHours IS NULL OR LightHours BETWEEN 0 AND 24) AND (LightIntensity IS NULL OR LightIntensity >= 0)
         AND (TempDay IS NULL OR TempDay BETWEEN -20 AND 60) AND (TempNight IS NULL OR TempNight BETWEEN -20 AND 60));
GO

/* parameter groups: add ENVIRONMENT (lighting, temperature) */
IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_tblParameter_Group'
           AND definition NOT LIKE N'%ENVIRONMENT%')
BEGIN
    ALTER TABLE dbo.tblParameter DROP CONSTRAINT CK_tblParameter_Group;
    ALTER TABLE dbo.tblParameter ADD CONSTRAINT CK_tblParameter_Group CHECK (ParameterGroup IN
        ('BODYWEIGHT','WEIGHTGAIN','FEED','NUTRITION','MORTALITY','UNIFORMITY','PRODUCTION','FERTILITY','HATCH','ENVIRONMENT'));
END
GO

/* hatch results are linked to the flock week the eggs were laid in */
IF COL_LENGTH('dbo.tblHatchResult', 'ModifiedBy') IS NULL
    ALTER TABLE dbo.tblHatchResult ADD ModifiedBy INT NULL, ModifiedDate DATETIME2(0) NULL;
GO

PRINT N'Phase 2-5 schema additions applied.';
GO
