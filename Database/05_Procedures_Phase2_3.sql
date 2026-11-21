/* =====================================================================
   FLOCK WEIGHT MANAGEMENT & ANALYSIS SYSTEM
   Script 05 : Functions + procedures for Phase 2 and Phase 3
     - standard lookup (fn_FlockStandard, sp_GetFlockStandard)
     - central variance / status logic (fn_VarianceStatus)
     - Standard Master, Feed Formula, Placement, Transfer, Adjustment
     - Weekly Flock Entry (sp_FlockWeekly_Save) with weight samples,
       feed lines, nutrients and mortality reasons in ONE transaction
     - population chain (sp_Flock_RecalculatePopulation)
     - uniformity (sp_CalculateUniformity), sp_SaveMortality
   Run after 04. Safe to re-run (CREATE OR ALTER).
   sp_CalculateFlockPerformance is created by script 06; SQL Server
   resolves it when first executed (deferred name resolution).
   ===================================================================== */
USE FlockManagementDB;
GO
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

/* ---------------------------------------------------------------------
   FUNCTIONS
   --------------------------------------------------------------------- */

/* Week of age a date falls in. Hatch day = age 0; days 1-7 = week 1. */
CREATE OR ALTER FUNCTION dbo.fn_AgeWeek (@HatchDate DATE, @OnDate DATE)
RETURNS INT
WITH SCHEMABINDING
AS
BEGIN
    DECLARE @days INT = DATEDIFF(DAY, @HatchDate, @OnDate);
    RETURN CASE WHEN @days < 1 THEN 1 ELSE (@days + 6) / 7 END;
END
GO

/* Central Standard-vs-Actual status. Mirrors FlockManagement.Common.Helpers.VarianceCalculator.
   Order: explicit critical band -> min/max band -> tolerance %.
   Direction: T = both sides matter, H = higher is better, L = lower is better. */
CREATE OR ALTER FUNCTION dbo.fn_VarianceStatus
(
    @Std DECIMAL(18,4), @Act DECIMAL(18,4),
    @Min DECIMAL(18,4), @Max DECIMAL(18,4),
    @CritMin DECIMAL(18,4), @CritMax DECIMAL(18,4),
    @WarnPct DECIMAL(9,4), @CritPct DECIMAL(9,4),
    @Direction CHAR(1)
)
RETURNS NVARCHAR(20)
AS
BEGIN
    IF @Std IS NULL OR @Act IS NULL RETURN N'NODATA';

    DECLARE @pct DECIMAL(18,4) = CASE WHEN @Std <> 0 THEN (@Act - @Std) / @Std * 100 ELSE 0 END;
    DECLARE @abs DECIMAL(18,4) = ABS(@pct);
    DECLARE @low BIT = CASE WHEN @Act < @Std THEN 1 ELSE 0 END;
    DECLARE @fav BIT = CASE WHEN (@Direction = 'H' AND @low = 0) OR (@Direction = 'L' AND @low = 1) THEN 1 ELSE 0 END;

    IF @fav = 0 AND ((@CritMin IS NOT NULL AND @Act < @CritMin) OR (@CritMax IS NOT NULL AND @Act > @CritMax))
        RETURN N'CRITICAL';

    IF @Min IS NOT NULL OR @Max IS NOT NULL
    BEGIN
        IF (@Min IS NULL OR @Act >= @Min) AND (@Max IS NULL OR @Act <= @Max) RETURN N'WITHIN';
        IF @fav = 0 AND @CritMin IS NULL AND @CritMax IS NULL AND @abs > ISNULL(@CritPct, 10) RETURN N'CRITICAL';
        RETURN CASE WHEN @Min IS NOT NULL AND @Act < @Min THEN N'BELOW' ELSE N'ABOVE' END;
    END

    IF @abs <= ISNULL(@WarnPct, 5) RETURN N'WITHIN';
    IF @fav = 0 AND @abs > ISNULL(@CritPct, 10) RETURN N'CRITICAL';
    RETURN CASE WHEN @low = 1 THEN N'BELOW' ELSE N'ABOVE' END;
END
GO

/* Applicable standard for every active parameter of a flock at an age.
   Most specific wins: breed-specific over all-breeds, feed-specific over all-feeds,
   then the latest EffectiveFrom on or before @AsOf. Tolerance: parameter row, else global row. */
CREATE OR ALTER FUNCTION dbo.fn_FlockStandard (@FlockID INT, @AgeWeek INT, @FeedTypeID INT, @AsOf DATE)
RETURNS TABLE
AS
RETURN
(
    SELECT  p.ParameterID, p.ParameterCode, p.ParameterName, p.ParameterGroup, u.UnitCode, p.Direction,
            p.DecimalPlaces, p.SortOrder, p.LogicalMin, p.LogicalMax,
            s.StandardID, s.StandardValue, s.MinValue, s.MaxValue, s.CriticalMinValue, s.CriticalMaxValue,
            CAST(COALESCE(tp.WarningPct, tg.WarningPct, 5) AS DECIMAL(9,4))   AS WarningPct,
            CAST(COALESCE(tp.CriticalPct, tg.CriticalPct, 10) AS DECIMAL(9,4)) AS CriticalPct
    FROM    dbo.tblFlock f
    CROSS JOIN dbo.tblParameter p
    INNER JOIN dbo.tblUnit u ON u.UnitID = p.UnitID
    OUTER APPLY (
        SELECT TOP (1) st.StandardID, st.StandardValue, st.MinValue, st.MaxValue, st.CriticalMinValue, st.CriticalMaxValue
        FROM   dbo.tblStandard st
        WHERE  st.BirdTypeID = f.BirdTypeID
          AND  st.AgeWeek = @AgeWeek
          AND  st.ParameterID = p.ParameterID
          AND  st.IsActive = 1
          AND  (st.BreedID IS NULL OR st.BreedID = f.BreedID)
          AND  (st.FeedTypeID IS NULL OR st.FeedTypeID = @FeedTypeID)
          AND  st.EffectiveFrom <= ISNULL(@AsOf, CONVERT(DATE, GETDATE()))
          AND  (st.EffectiveTo IS NULL OR st.EffectiveTo >= ISNULL(@AsOf, CONVERT(DATE, GETDATE())))
        ORDER BY CASE WHEN st.BreedID IS NULL THEN 1 ELSE 0 END,
                 CASE WHEN st.FeedTypeID IS NULL THEN 1 ELSE 0 END,
                 st.EffectiveFrom DESC
    ) s
    OUTER APPLY (SELECT TOP (1) t.WarningPct, t.CriticalPct FROM dbo.tblToleranceConfig t WHERE t.ParameterID = p.ParameterID AND t.IsActive = 1) tp
    OUTER APPLY (SELECT TOP (1) t.WarningPct, t.CriticalPct FROM dbo.tblToleranceConfig t WHERE t.ParameterID IS NULL AND t.IsActive = 1) tg
    WHERE   f.FlockID = @FlockID
      AND   p.IsActive = 1
);
GO

/* Standard vs Actual for the analysed parameters of one weekly entry (the heart of every
   analysis screen, alert, score and report): weight, feed, nutrients, mortality, uniformity,
   egg production, egg weight, fertility, hatchability, lighting and temperature.
   Standard = value captured when the week was last calculated, else the live standard.
   Hen-day production uses the average females of the week:
     opening females = closing + mortality + culling + transferred out - received
     HD % = total eggs x 100 / ((opening + closing females) / 2 x 7)
   Fertility / hatchability come from the hatch results of eggs laid in this week
   (fertility once candled, hatchability only from settings that have hatched). */
CREATE OR ALTER FUNCTION dbo.fn_WeeklyParameters (@FlockWeeklyID INT)
RETURNS TABLE
AS
RETURN
(
    SELECT  w.FlockWeeklyID, w.FlockID, w.WeekNo, w.AgeInWeeks, w.EntryDate,
            s.ParameterID, s.ParameterCode, s.ParameterName, s.ParameterGroup, s.UnitCode, s.Direction,
            s.DecimalPlaces, s.SortOrder,
            v.StandardValue, s.MinValue, s.MaxValue, s.CriticalMinValue, s.CriticalMaxValue,
            s.WarningPct, s.CriticalPct,
            v.ActualValue,
            CAST(v.ActualValue - v.StandardValue AS DECIMAL(18,4)) AS Variance,
            CAST(CASE WHEN v.StandardValue IS NOT NULL AND v.StandardValue <> 0 AND v.ActualValue IS NOT NULL
                      THEN ROUND((v.ActualValue - v.StandardValue) / v.StandardValue * 100, 2) END AS DECIMAL(12,2)) AS VariancePct,
            dbo.fn_VarianceStatus(v.StandardValue, v.ActualValue, s.MinValue, s.MaxValue, s.CriticalMinValue,
                                  s.CriticalMaxValue, s.WarningPct, s.CriticalPct, s.Direction) AS StatusCode
    FROM    dbo.tblFlockWeekly w
    CROSS APPLY dbo.fn_FlockStandard(w.FlockID, w.AgeInWeeks, w.FeedTypeID, w.EntryDate) s
    OUTER APPLY (
        SELECT  ep.TotalEggs, ep.HatchingEggs, ep.AvgEggWeight,
                CAST((w.ClosingFemale + w.FemaleMortality + w.FemaleCulling + w.TransferOutFemale - w.ReceivedFemale + w.ClosingFemale) / 2.0 * 7
                     AS DECIMAL(18,4)) AS HenDays
        FROM    dbo.tblEggProductionWeekly ep
        WHERE   ep.FlockWeeklyID = w.FlockWeeklyID
    ) e
    OUTER APPLY (
        SELECT  SUM(hr.SettableEggs) AS Settable, SUM(hr.SettableEggs - hr.InfertileEggs) AS Fertile,
                SUM(CASE WHEN hr.HatchDate IS NOT NULL THEN hr.SettableEggs END) AS SettableHatched,
                SUM(CASE WHEN hr.HatchDate IS NOT NULL THEN hr.HatchedChicks END) AS Hatched
        FROM    dbo.tblHatchResult hr
        WHERE   hr.FlockID = w.FlockID AND hr.WeekNo = w.WeekNo
    ) h
    CROSS APPLY (
        SELECT
            CAST(CASE s.ParameterCode
                    WHEN N'BODY_WEIGHT' THEN COALESCE(w.StandardBodyWeight, s.StandardValue)
                    WHEN N'WEIGHT_GAIN' THEN COALESCE(w.StandardWeightGain, s.StandardValue)
                    WHEN N'FEED_INTAKE' THEN COALESCE(w.StandardFeedIntake, s.StandardValue)
                    WHEN N'MORTALITY'   THEN COALESCE(w.StandardMortalityPct, s.StandardValue)
                    WHEN N'UNIFORMITY'  THEN COALESCE(w.StandardUniformity, s.StandardValue)
                    ELSE s.StandardValue END AS DECIMAL(18,4)) AS StandardValue,
            CAST(CASE s.ParameterCode
                    WHEN N'BODY_WEIGHT'   THEN w.ActualBodyWeight
                    WHEN N'WEIGHT_GAIN'   THEN w.ActualWeightGain
                    WHEN N'FEED_INTAKE'   THEN w.ActualFeedIntake
                    WHEN N'MORTALITY'     THEN w.MortalityPercentage
                    WHEN N'UNIFORMITY'    THEN w.UniformityPercentage
                    WHEN N'ENERGY'        THEN w.Energy
                    WHEN N'PROTEIN'       THEN w.Protein
                    WHEN N'METHIONINE'    THEN w.Methionine
                    WHEN N'LYSINE'        THEN w.Lysine
                    WHEN N'LINOLEIC_ACID' THEN w.LinoleicAcid
                    WHEN N'CALCIUM'       THEN w.Calcium
                    WHEN N'PHOSPHORUS'    THEN w.Phosphorus
                    WHEN N'FAT'           THEN w.Fat
                    WHEN N'FIBRE'         THEN w.Fibre
                    WHEN N'SALT'          THEN w.Salt
                    WHEN N'HD_PRODUCTION'    THEN CASE WHEN e.HenDays > 0 THEN e.TotalEggs * 100.0 / e.HenDays END
                    WHEN N'HATCHING_EGG_PCT' THEN CASE WHEN e.TotalEggs > 0 THEN e.HatchingEggs * 100.0 / e.TotalEggs END
                    WHEN N'EGG_WEIGHT'       THEN e.AvgEggWeight
                    WHEN N'FERTILITY'        THEN CASE WHEN h.Settable > 0 THEN h.Fertile * 100.0 / h.Settable END
                    WHEN N'HATCHABILITY'     THEN CASE WHEN h.SettableHatched > 0 THEN h.Hatched * 100.0 / h.SettableHatched END
                    WHEN N'LIGHT_HOURS'      THEN w.LightHours
                    WHEN N'LIGHT_INTENSITY'  THEN w.LightIntensity
                    WHEN N'TEMP_DAY'         THEN w.TempDay
                    WHEN N'TEMP_NIGHT'       THEN w.TempNight
                 END AS DECIMAL(18,4)) AS ActualValue
    ) v
    WHERE   w.FlockWeeklyID = @FlockWeeklyID
      AND   s.ParameterCode IN (N'FEED_INTAKE', N'WEIGHT_GAIN', N'BODY_WEIGHT', N'ENERGY', N'PROTEIN', N'METHIONINE',
                                N'LYSINE', N'LINOLEIC_ACID', N'CALCIUM', N'PHOSPHORUS', N'FAT', N'FIBRE', N'SALT',
                                N'MORTALITY', N'UNIFORMITY',
                                N'HD_PRODUCTION', N'HATCHING_EGG_PCT', N'EGG_WEIGHT', N'FERTILITY', N'HATCHABILITY',
                                N'LIGHT_HOURS', N'LIGHT_INTENSITY', N'TEMP_DAY', N'TEMP_NIGHT')
);
GO

/* One-row pivot of fn_WeeklyParameters (used by dashboard, comparison and management reports). */
CREATE OR ALTER FUNCTION dbo.fn_WeeklySummary (@FlockWeeklyID INT)
RETURNS TABLE
AS
RETURN
(
    SELECT
        MAX(CASE WHEN ParameterCode = N'BODY_WEIGHT' THEN StandardValue END) AS StdBodyWeight,
        MAX(CASE WHEN ParameterCode = N'BODY_WEIGHT' THEN ActualValue END)   AS BodyWeight,
        MAX(CASE WHEN ParameterCode = N'BODY_WEIGHT' THEN VariancePct END)   AS BodyWeightVarPct,
        MAX(CASE WHEN ParameterCode = N'BODY_WEIGHT' THEN StatusCode END)    AS BodyWeightStatus,
        MAX(CASE WHEN ParameterCode = N'WEIGHT_GAIN' THEN StandardValue END) AS StdWeightGain,
        MAX(CASE WHEN ParameterCode = N'WEIGHT_GAIN' THEN ActualValue END)   AS WeightGain,
        MAX(CASE WHEN ParameterCode = N'WEIGHT_GAIN' THEN StatusCode END)    AS WeightGainStatus,
        MAX(CASE WHEN ParameterCode = N'FEED_INTAKE' THEN StandardValue END) AS StdFeedIntake,
        MAX(CASE WHEN ParameterCode = N'FEED_INTAKE' THEN ActualValue END)   AS FeedIntake,
        MAX(CASE WHEN ParameterCode = N'FEED_INTAKE' THEN VariancePct END)   AS FeedIntakeVarPct,
        MAX(CASE WHEN ParameterCode = N'FEED_INTAKE' THEN StatusCode END)    AS FeedIntakeStatus,
        MAX(CASE WHEN ParameterCode = N'MORTALITY' THEN StandardValue END)   AS StdMortality,
        MAX(CASE WHEN ParameterCode = N'MORTALITY' THEN ActualValue END)     AS Mortality,
        MAX(CASE WHEN ParameterCode = N'MORTALITY' THEN StatusCode END)      AS MortalityStatus,
        MAX(CASE WHEN ParameterCode = N'UNIFORMITY' THEN StandardValue END)  AS StdUniformity,
        MAX(CASE WHEN ParameterCode = N'UNIFORMITY' THEN ActualValue END)    AS Uniformity,
        MAX(CASE WHEN ParameterCode = N'UNIFORMITY' THEN StatusCode END)     AS UniformityStatus,
        SUM(CASE WHEN ParameterGroup = N'NUTRITION' AND StatusCode IN (N'BELOW', N'ABOVE', N'CRITICAL') THEN 1 ELSE 0 END) AS NutritionOut,
        SUM(CASE WHEN ParameterGroup = N'NUTRITION' AND StatusCode <> N'NODATA' THEN 1 ELSE 0 END) AS NutritionChecked,
        SUM(CASE WHEN StatusCode = N'CRITICAL' THEN 1 ELSE 0 END) AS CriticalCount,
        SUM(CASE WHEN StatusCode IN (N'BELOW', N'ABOVE', N'CRITICAL') THEN 1 ELSE 0 END) AS OutOfStandardCount
    FROM dbo.fn_WeeklyParameters(@FlockWeeklyID)
);
GO

/* Population movements per week of age (extra placements, transfers in/out, adjustments). */
CREATE OR ALTER FUNCTION dbo.fn_FlockMovements (@FlockID INT)
RETURNS TABLE
AS
RETURN
(
    SELECT  m.WeekNo, SUM(m.InM) AS InM, SUM(m.InF) AS InF, SUM(m.OutM) AS OutM, SUM(m.OutF) AS OutF
    FROM (
        SELECT dbo.fn_AgeWeek(f.HatchDate, p.PlacementDate) AS WeekNo, p.MaleCount AS InM, p.FemaleCount AS InF, 0 AS OutM, 0 AS OutF
        FROM dbo.tblFlockPlacement p INNER JOIN dbo.tblFlock f ON f.FlockID = p.FlockID
        WHERE p.FlockID = @FlockID AND p.IsInitial = 0
        UNION ALL
        SELECT COALESCE(t.WeekNo, dbo.fn_AgeWeek(f.HatchDate, t.TransferDate)),
               CASE WHEN t.TransferType = N'IN' THEN t.MaleCount ELSE 0 END,
               CASE WHEN t.TransferType = N'IN' THEN t.FemaleCount ELSE 0 END,
               CASE WHEN t.TransferType = N'OUT' THEN t.MaleCount ELSE 0 END,
               CASE WHEN t.TransferType = N'OUT' THEN t.FemaleCount ELSE 0 END
        FROM dbo.tblFlockTransfer t INNER JOIN dbo.tblFlock f ON f.FlockID = t.FlockID
        WHERE t.FlockID = @FlockID AND t.TransferType IN (N'IN', N'OUT')
        UNION ALL
        SELECT COALESCE(a.WeekNo, dbo.fn_AgeWeek(f.HatchDate, a.AdjustmentDate)),
               CASE WHEN a.MaleAdjustment > 0 THEN a.MaleAdjustment ELSE 0 END,
               CASE WHEN a.FemaleAdjustment > 0 THEN a.FemaleAdjustment ELSE 0 END,
               CASE WHEN a.MaleAdjustment < 0 THEN -a.MaleAdjustment ELSE 0 END,
               CASE WHEN a.FemaleAdjustment < 0 THEN -a.FemaleAdjustment ELSE 0 END
        FROM dbo.tblFlockAdjustment a INNER JOIN dbo.tblFlock f ON f.FlockID = a.FlockID
        WHERE a.FlockID = @FlockID
    ) m
    GROUP BY m.WeekNo
);
GO

/* Component score 0-100 from the unfavourable deviation %. */
CREATE OR ALTER FUNCTION dbo.fn_ComponentScore (@VariancePct DECIMAL(12,2), @Direction CHAR(1), @ZeroScorePct DECIMAL(9,4))
RETURNS DECIMAL(9,4)
AS
BEGIN
    IF @VariancePct IS NULL RETURN NULL;
    DECLARE @dev DECIMAL(18,4) =
        CASE @Direction
            WHEN 'H' THEN CASE WHEN @VariancePct < 0 THEN -@VariancePct ELSE 0 END
            WHEN 'L' THEN CASE WHEN @VariancePct > 0 THEN @VariancePct ELSE 0 END
            ELSE ABS(@VariancePct) END;
    IF ISNULL(@ZeroScorePct, 0) <= 0 RETURN CASE WHEN @dev = 0 THEN 100 ELSE 0 END;
    DECLARE @score DECIMAL(18,4) = 100 * (1 - @dev / @ZeroScorePct);
    RETURN CAST(CASE WHEN @score < 0 THEN 0 WHEN @score > 100 THEN 100 ELSE @score END AS DECIMAL(9,4));
END
GO

/* ---------------------------------------------------------------------
   STANDARD MASTER
   --------------------------------------------------------------------- */
CREATE OR ALTER PROCEDURE dbo.sp_GetFlockStandard
    @FlockID INT, @AgeWeek INT, @FeedTypeID INT = NULL, @AsOf DATE = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT * FROM dbo.fn_FlockStandard(@FlockID, @AgeWeek, @FeedTypeID, @AsOf) ORDER BY SortOrder;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_Standard_Search
    @BirdTypeID INT = NULL, @BreedID INT = NULL, @ParameterID INT = NULL, @ParameterGroup NVARCHAR(30) = NULL,
    @FromWeek INT = NULL, @ToWeek INT = NULL, @ActiveOnly BIT = 1, @PageNo INT = 1, @PageSize INT = 50
AS
BEGIN
    SET NOCOUNT ON;
    IF @PageNo < 1 SET @PageNo = 1;
    IF @PageSize NOT BETWEEN 1 AND 500 SET @PageSize = 50;
    SELECT  s.StandardID, s.BirdTypeID, bt.BirdTypeName, s.BreedID, b.BreedName, s.FeedTypeID, ft.FeedTypeName,
            s.AgeWeek, s.ParameterID, p.ParameterName, p.ParameterGroup, u.UnitCode, p.DecimalPlaces,
            s.StandardValue, s.MinValue, s.MaxValue, s.CriticalMinValue, s.CriticalMaxValue,
            s.EffectiveFrom, s.EffectiveTo, s.IsActive, COUNT(*) OVER () AS TotalCount
    FROM    dbo.tblStandard s
    INNER JOIN dbo.tblBirdType bt ON bt.BirdTypeID = s.BirdTypeID
    INNER JOIN dbo.tblParameter p ON p.ParameterID = s.ParameterID
    INNER JOIN dbo.tblUnit u ON u.UnitID = p.UnitID
    LEFT  JOIN dbo.tblBreed b ON b.BreedID = s.BreedID
    LEFT  JOIN dbo.tblFeedType ft ON ft.FeedTypeID = s.FeedTypeID
    WHERE   (@BirdTypeID IS NULL OR s.BirdTypeID = @BirdTypeID)
      AND   (@BreedID IS NULL OR s.BreedID = @BreedID)
      AND   (@ParameterID IS NULL OR s.ParameterID = @ParameterID)
      AND   (@ParameterGroup IS NULL OR p.ParameterGroup = @ParameterGroup)
      AND   (@FromWeek IS NULL OR s.AgeWeek >= @FromWeek)
      AND   (@ToWeek IS NULL OR s.AgeWeek <= @ToWeek)
      AND   (@ActiveOnly = 0 OR s.IsActive = 1)
    ORDER BY bt.BirdTypeName, p.SortOrder, s.AgeWeek, s.EffectiveFrom DESC
    OFFSET (@PageNo - 1) * @PageSize ROWS FETCH NEXT @PageSize ROWS ONLY
    OPTION (RECOMPILE);
END
GO

/* Week-by-week grid for one standard set (bird type + breed + feed type + parameter + effective from). */
CREATE OR ALTER PROCEDURE dbo.sp_Standard_GetGrid
    @BirdTypeID INT, @BreedID INT = NULL, @FeedTypeID INT = NULL, @ParameterID INT, @EffectiveFrom DATE
AS
BEGIN
    SET NOCOUNT ON;
    SELECT  s.StandardID, s.AgeWeek, s.StandardValue, s.MinValue, s.MaxValue, s.CriticalMinValue, s.CriticalMaxValue, s.EffectiveTo
    FROM    dbo.tblStandard s
    WHERE   s.BirdTypeID = @BirdTypeID
      AND   ((@BreedID IS NULL AND s.BreedID IS NULL) OR s.BreedID = @BreedID)
      AND   ((@FeedTypeID IS NULL AND s.FeedTypeID IS NULL) OR s.FeedTypeID = @FeedTypeID)
      AND   s.ParameterID = @ParameterID
      AND   s.EffectiveFrom = @EffectiveFrom
      AND   s.IsActive = 1
    ORDER BY s.AgeWeek;
END
GO

/* Saves a grid. @RowsJson = [{"week":20,"std":1800,"min":1700,"max":1900,"cmin":null,"cmax":null}, ...]
   A week sent with "std":null removes (deactivates) that week. */
CREATE OR ALTER PROCEDURE dbo.sp_Standard_SaveGrid
    @BirdTypeID INT, @BreedID INT = NULL, @FeedTypeID INT = NULL, @ParameterID INT,
    @EffectiveFrom DATE, @EffectiveTo DATE = NULL, @RowsJson NVARCHAR(MAX),
    @UserID INT, @UserName NVARCHAR(50), @IPAddress NVARCHAR(50) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    DECLARE @msg NVARCHAR(400);

    IF ISJSON(@RowsJson) = 0 THROW 50400, N'Invalid standard data.', 1;
    IF @EffectiveTo IS NOT NULL AND @EffectiveTo < @EffectiveFrom THROW 50401, N'Effective To cannot be before Effective From.', 1;
    IF NOT EXISTS (SELECT 1 FROM dbo.tblBirdType WHERE BirdTypeID = @BirdTypeID) THROW 50402, N'Bird type not found.', 1;

    DECLARE @UnitID INT, @LMin DECIMAL(18,4), @LMax DECIMAL(18,4), @PName NVARCHAR(100);
    SELECT @UnitID = UnitID, @LMin = LogicalMin, @LMax = LogicalMax, @PName = ParameterName FROM dbo.tblParameter WHERE ParameterID = @ParameterID;
    IF @UnitID IS NULL THROW 50403, N'Parameter not found.', 1;

    DECLARE @r TABLE (AgeWeek INT PRIMARY KEY, StdV DECIMAL(18,4), MinV DECIMAL(18,4), MaxV DECIMAL(18,4), CMinV DECIMAL(18,4), CMaxV DECIMAL(18,4));
    INSERT INTO @r (AgeWeek, StdV, MinV, MaxV, CMinV, CMaxV)
    SELECT TRY_CONVERT(INT, JSON_VALUE(j.value, '$.week')),
           TRY_CONVERT(DECIMAL(18,4), JSON_VALUE(j.value, '$.std')),
           TRY_CONVERT(DECIMAL(18,4), JSON_VALUE(j.value, '$.min')),
           TRY_CONVERT(DECIMAL(18,4), JSON_VALUE(j.value, '$.max')),
           TRY_CONVERT(DECIMAL(18,4), JSON_VALUE(j.value, '$.cmin')),
           TRY_CONVERT(DECIMAL(18,4), JSON_VALUE(j.value, '$.cmax'))
    FROM OPENJSON(@RowsJson) j
    WHERE TRY_CONVERT(INT, JSON_VALUE(j.value, '$.week')) IS NOT NULL;

    IF EXISTS (SELECT 1 FROM @r WHERE AgeWeek NOT BETWEEN 0 AND 120) THROW 50404, N'Week must be between 0 and 120.', 1;

    SELECT TOP (1) @msg = CONCAT(N'Week ', AgeWeek, N': minimum cannot be greater than maximum.')
    FROM @r WHERE MinV IS NOT NULL AND MaxV IS NOT NULL AND MinV > MaxV;
    IF @msg IS NOT NULL THROW 50405, @msg, 1;

    SELECT TOP (1) @msg = CONCAT(N'Week ', AgeWeek, N': the standard must lie between the minimum and maximum.')
    FROM @r WHERE StdV IS NOT NULL AND ((MinV IS NOT NULL AND StdV < MinV) OR (MaxV IS NOT NULL AND StdV > MaxV));
    IF @msg IS NOT NULL THROW 50406, @msg, 1;

    SELECT TOP (1) @msg = CONCAT(N'Week ', AgeWeek, N': ', @PName, N' must be between ', CONVERT(NVARCHAR(30), @LMin), N' and ', CONVERT(NVARCHAR(30), @LMax), N'.')
    FROM @r WHERE StdV IS NOT NULL AND ((@LMin IS NOT NULL AND StdV < @LMin) OR (@LMax IS NOT NULL AND StdV > @LMax));
    IF @msg IS NOT NULL THROW 50407, @msg, 1;

    DECLARE @Old NVARCHAR(MAX) = (
        SELECT s.AgeWeek, s.StandardValue, s.MinValue, s.MaxValue, s.CriticalMinValue, s.CriticalMaxValue
        FROM dbo.tblStandard s
        WHERE s.BirdTypeID = @BirdTypeID AND ((@BreedID IS NULL AND s.BreedID IS NULL) OR s.BreedID = @BreedID)
          AND ((@FeedTypeID IS NULL AND s.FeedTypeID IS NULL) OR s.FeedTypeID = @FeedTypeID)
          AND s.ParameterID = @ParameterID AND s.EffectiveFrom = @EffectiveFrom AND s.IsActive = 1
        ORDER BY s.AgeWeek FOR JSON PATH);

    BEGIN TRANSACTION;

    /* weeks sent without a standard value -> deactivate */
    UPDATE s SET IsActive = 0, ModifiedBy = @UserID, ModifiedDate = SYSDATETIME()
    FROM dbo.tblStandard s INNER JOIN @r r ON r.AgeWeek = s.AgeWeek AND r.StdV IS NULL
    WHERE s.BirdTypeID = @BirdTypeID AND ((@BreedID IS NULL AND s.BreedID IS NULL) OR s.BreedID = @BreedID)
      AND ((@FeedTypeID IS NULL AND s.FeedTypeID IS NULL) OR s.FeedTypeID = @FeedTypeID)
      AND s.ParameterID = @ParameterID AND s.EffectiveFrom = @EffectiveFrom AND s.IsActive = 1;

    UPDATE s SET StandardValue = r.StdV, MinValue = r.MinV, MaxValue = r.MaxV, CriticalMinValue = r.CMinV, CriticalMaxValue = r.CMaxV,
                 EffectiveTo = @EffectiveTo, UnitID = @UnitID, ModifiedBy = @UserID, ModifiedDate = SYSDATETIME()
    FROM dbo.tblStandard s INNER JOIN @r r ON r.AgeWeek = s.AgeWeek AND r.StdV IS NOT NULL
    WHERE s.BirdTypeID = @BirdTypeID AND ((@BreedID IS NULL AND s.BreedID IS NULL) OR s.BreedID = @BreedID)
      AND ((@FeedTypeID IS NULL AND s.FeedTypeID IS NULL) OR s.FeedTypeID = @FeedTypeID)
      AND s.ParameterID = @ParameterID AND s.EffectiveFrom = @EffectiveFrom AND s.IsActive = 1;

    INSERT INTO dbo.tblStandard (BirdTypeID, BreedID, FeedTypeID, AgeWeek, ParameterID, StandardValue, MinValue, MaxValue,
                                 CriticalMinValue, CriticalMaxValue, UnitID, EffectiveFrom, EffectiveTo, IsActive, CreatedBy)
    SELECT @BirdTypeID, @BreedID, @FeedTypeID, r.AgeWeek, @ParameterID, r.StdV, r.MinV, r.MaxV, r.CMinV, r.CMaxV,
           @UnitID, @EffectiveFrom, @EffectiveTo, 1, @UserID
    FROM @r r
    WHERE r.StdV IS NOT NULL
      AND NOT EXISTS (SELECT 1 FROM dbo.tblStandard s
                      WHERE s.BirdTypeID = @BirdTypeID AND ((@BreedID IS NULL AND s.BreedID IS NULL) OR s.BreedID = @BreedID)
                        AND ((@FeedTypeID IS NULL AND s.FeedTypeID IS NULL) OR s.FeedTypeID = @FeedTypeID)
                        AND s.ParameterID = @ParameterID AND s.EffectiveFrom = @EffectiveFrom AND s.AgeWeek = r.AgeWeek AND s.IsActive = 1);

    DECLARE @New NVARCHAR(MAX) = (
        SELECT s.AgeWeek, s.StandardValue, s.MinValue, s.MaxValue, s.CriticalMinValue, s.CriticalMaxValue
        FROM dbo.tblStandard s
        WHERE s.BirdTypeID = @BirdTypeID AND ((@BreedID IS NULL AND s.BreedID IS NULL) OR s.BreedID = @BreedID)
          AND ((@FeedTypeID IS NULL AND s.FeedTypeID IS NULL) OR s.FeedTypeID = @FeedTypeID)
          AND s.ParameterID = @ParameterID AND s.EffectiveFrom = @EffectiveFrom AND s.IsActive = 1
        ORDER BY s.AgeWeek FOR JSON PATH);

    DECLARE @Key NVARCHAR(50) = CONCAT(@BirdTypeID, N'/', ISNULL(CONVERT(NVARCHAR(10), @BreedID), N'*'), N'/',
                                       ISNULL(CONVERT(NVARCHAR(10), @FeedTypeID), N'*'), N'/', @ParameterID);
    EXEC dbo.sp_AuditLog_Insert @UserID, @UserName, N'UPDATE', N'tblStandard', @Key, @Old, @New, @IPAddress;

    COMMIT TRANSACTION;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_Standard_SetActive
    @StandardID INT, @IsActive BIT, @UserID INT, @UserName NVARCHAR(50), @IPAddress NVARCHAR(50) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    BEGIN TRANSACTION;
    UPDATE dbo.tblStandard SET IsActive = @IsActive, ModifiedBy = @UserID, ModifiedDate = SYSDATETIME() WHERE StandardID = @StandardID;
    IF @@ROWCOUNT = 0 THROW 50408, N'Standard not found.', 1;
    DECLARE @New NVARCHAR(MAX) = CONCAT(N'{"IsActive":', CASE WHEN @IsActive = 1 THEN N'true' ELSE N'false' END, N'}');
    EXEC dbo.sp_AuditLog_Insert @UserID, @UserName, N'STATUS', N'tblStandard', @StandardID, NULL, @New, @IPAddress;
    COMMIT TRANSACTION;
END
GO

/* ---------------------------------------------------------------------
   FEED FORMULA
   --------------------------------------------------------------------- */
/* @DetailsJson = [{"parameterId":4,"value":2790}, ...] */
CREATE OR ALTER PROCEDURE dbo.sp_FeedFormula_Save
    @FeedFormulaID INT, @FeedTypeID INT, @FormulaCode NVARCHAR(30), @FormulaName NVARCHAR(100),
    @EffectiveFrom DATE, @EffectiveTo DATE = NULL, @Remarks NVARCHAR(250) = NULL, @IsActive BIT = 1,
    @DetailsJson NVARCHAR(MAX) = NULL,
    @UserID INT, @UserName NVARCHAR(50), @IPAddress NVARCHAR(50) = NULL, @NewFeedFormulaID INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    DECLARE @msg NVARCHAR(400);

    SET @FormulaCode = UPPER(LTRIM(RTRIM(@FormulaCode)));
    IF ISNULL(@FormulaCode, N'') = N'' THROW 50420, N'Formula code is required.', 1;
    IF EXISTS (SELECT 1 FROM dbo.tblFeedFormula WHERE FormulaCode = @FormulaCode AND FeedFormulaID <> @FeedFormulaID)
        THROW 50421, N'A formula with this code already exists.', 1;
    IF @EffectiveTo IS NOT NULL AND @EffectiveTo < @EffectiveFrom THROW 50422, N'Effective To cannot be before Effective From.', 1;
    IF NOT EXISTS (SELECT 1 FROM dbo.tblFeedType WHERE FeedTypeID = @FeedTypeID) THROW 50423, N'Feed type not found.', 1;
    IF @DetailsJson IS NOT NULL AND ISJSON(@DetailsJson) = 0 THROW 50424, N'Invalid nutrient data.', 1;

    DECLARE @d TABLE (ParameterID INT PRIMARY KEY, Value DECIMAL(18,4));
    INSERT INTO @d (ParameterID, Value)
    SELECT DISTINCT TRY_CONVERT(INT, JSON_VALUE(j.value, '$.parameterId')), TRY_CONVERT(DECIMAL(18,4), JSON_VALUE(j.value, '$.value'))
    FROM OPENJSON(ISNULL(@DetailsJson, N'[]')) j
    WHERE TRY_CONVERT(INT, JSON_VALUE(j.value, '$.parameterId')) IS NOT NULL
      AND TRY_CONVERT(DECIMAL(18,4), JSON_VALUE(j.value, '$.value')) IS NOT NULL;

    SELECT TOP (1) @msg = CONCAT(p.ParameterName, N' must be between ', CONVERT(NVARCHAR(30), p.LogicalMin), N' and ', CONVERT(NVARCHAR(30), p.LogicalMax), N'.')
    FROM @d d INNER JOIN dbo.tblParameter p ON p.ParameterID = d.ParameterID
    WHERE (p.LogicalMin IS NOT NULL AND d.Value < p.LogicalMin) OR (p.LogicalMax IS NOT NULL AND d.Value > p.LogicalMax);
    IF @msg IS NOT NULL THROW 50425, @msg, 1;

    DECLARE @Old NVARCHAR(MAX), @New NVARCHAR(MAX);
    BEGIN TRANSACTION;

    IF @FeedFormulaID = 0
    BEGIN
        INSERT INTO dbo.tblFeedFormula (FeedTypeID, FormulaCode, FormulaName, EffectiveFrom, EffectiveTo, Remarks, IsActive, CreatedBy)
        VALUES (@FeedTypeID, @FormulaCode, @FormulaName, @EffectiveFrom, @EffectiveTo, @Remarks, @IsActive, @UserID);
        SET @FeedFormulaID = SCOPE_IDENTITY();
    END
    ELSE
    BEGIN
        SET @Old = (SELECT f.*, (SELECT d.ParameterID, d.FormulatedValue FROM dbo.tblFeedFormulaDetail d WHERE d.FeedFormulaID = f.FeedFormulaID FOR JSON PATH) AS Nutrients
                    FROM dbo.tblFeedFormula f WHERE f.FeedFormulaID = @FeedFormulaID FOR JSON PATH, WITHOUT_ARRAY_WRAPPER);
        IF @Old IS NULL THROW 50426, N'Feed formula not found.', 1;
        UPDATE dbo.tblFeedFormula
           SET FeedTypeID = @FeedTypeID, FormulaCode = @FormulaCode, FormulaName = @FormulaName, EffectiveFrom = @EffectiveFrom,
               EffectiveTo = @EffectiveTo, Remarks = @Remarks, IsActive = @IsActive, ModifiedBy = @UserID, ModifiedDate = SYSDATETIME()
         WHERE FeedFormulaID = @FeedFormulaID;
    END

    DELETE FROM dbo.tblFeedFormulaDetail WHERE FeedFormulaID = @FeedFormulaID;
    INSERT INTO dbo.tblFeedFormulaDetail (FeedFormulaID, ParameterID, FormulatedValue)
    SELECT @FeedFormulaID, d.ParameterID, d.Value FROM @d d INNER JOIN dbo.tblParameter p ON p.ParameterID = d.ParameterID;

    SET @New = (SELECT f.*, (SELECT d.ParameterID, d.FormulatedValue FROM dbo.tblFeedFormulaDetail d WHERE d.FeedFormulaID = f.FeedFormulaID FOR JSON PATH) AS Nutrients
                FROM dbo.tblFeedFormula f WHERE f.FeedFormulaID = @FeedFormulaID FOR JSON PATH, WITHOUT_ARRAY_WRAPPER);
    DECLARE @Action NVARCHAR(20) = CASE WHEN @Old IS NULL THEN N'INSERT' ELSE N'UPDATE' END;
    EXEC dbo.sp_AuditLog_Insert @UserID, @UserName, @Action, N'tblFeedFormula', @FeedFormulaID, @Old, @New, @IPAddress;

    COMMIT TRANSACTION;
    SET @NewFeedFormulaID = @FeedFormulaID;
END
GO

/* ---------------------------------------------------------------------
   UNIFORMITY  (Uniformity % = birds within +/- range % of the sample average)
   --------------------------------------------------------------------- */
CREATE OR ALTER PROCEDURE dbo.sp_CalculateUniformity
    @FlockWeeklyID INT, @ReturnResult BIT = 0
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @range DECIMAL(9,4) = ISNULL(TRY_CONVERT(DECIMAL(9,4),
            (SELECT SettingValue FROM dbo.tblSystemSetting WHERE SettingKey = N'UNIFORMITY_RANGE_PCT')), 10);
    DECLARE @sex CHAR(1) = CASE WHEN EXISTS (SELECT 1 FROM dbo.tblWeightSample WHERE FlockWeeklyID = @FlockWeeklyID AND Sex = 'F') THEN 'F' ELSE 'M' END;

    DECLARE @n INT, @avg DECIMAL(18,6), @min DECIMAL(10,2), @max DECIMAL(10,2), @sd FLOAT, @within INT;
    SELECT @n = COUNT(*), @avg = AVG(CAST(WeightGrams AS DECIMAL(18,6))), @min = MIN(WeightGrams), @max = MAX(WeightGrams), @sd = STDEV(WeightGrams)
    FROM dbo.tblWeightSample WHERE FlockWeeklyID = @FlockWeeklyID AND Sex = @sex;

    DECLARE @lower DECIMAL(18,6) = @avg * (1 - @range / 100), @upper DECIMAL(18,6) = @avg * (1 + @range / 100);
    SELECT @within = COUNT(*) FROM dbo.tblWeightSample
    WHERE FlockWeeklyID = @FlockWeeklyID AND Sex = @sex AND WeightGrams BETWEEN @lower AND @upper;

    UPDATE dbo.tblFlockWeekly
       SET SampleSize           = NULLIF(@n, 0),
           ActualBodyWeight     = CASE WHEN @n > 0 THEN CAST(ROUND(@avg, 2) AS DECIMAL(10,2)) ELSE ManualBodyWeight END,
           MinBodyWeight        = CASE WHEN @n > 0 THEN @min END,
           MaxBodyWeight        = CASE WHEN @n > 0 THEN @max END,
           StdDevBodyWeight     = CASE WHEN @n > 1 THEN CAST(ROUND(@sd, 3) AS DECIMAL(10,3)) END,
           CVPercentage         = CASE WHEN @n > 1 AND @avg > 0 THEN CAST(ROUND(@sd / CAST(@avg AS FLOAT) * 100, 4) AS DECIMAL(9,4)) END,
           UniformityPercentage = CASE WHEN @n > 0 THEN CAST(ROUND(@within * 100.0 / @n, 4) AS DECIMAL(9,4)) END
     WHERE FlockWeeklyID = @FlockWeeklyID;

    IF @ReturnResult = 1
        SELECT @n AS SampleSize, CAST(ROUND(@avg, 2) AS DECIMAL(10,2)) AS AverageWeight,
               CAST(ROUND(@lower, 2) AS DECIMAL(10,2)) AS LowerLimit, CAST(ROUND(@upper, 2) AS DECIMAL(10,2)) AS UpperLimit,
               @within AS BirdsWithinRange, @min AS MinWeight, @max AS MaxWeight,
               CAST(ROUND(@sd, 3) AS DECIMAL(10,3)) AS StdDev,
               CASE WHEN @n > 0 THEN CAST(ROUND(@within * 100.0 / @n, 2) AS DECIMAL(9,2)) END AS UniformityPct,
               @range AS RangePct, @sex AS Sex;
END
GO

/* ---------------------------------------------------------------------
   POPULATION CHAIN
   Opening(week) = Closing(previous week); first week opens with the initial placement.
   Closing = Opening + Received - Mortality - Culling - Transfer Out (per sex).
   Re-computes every saved week of the flock, the feed intake per bird, mortality %,
   weight gain, the mortality register, the score/alerts and the flock's live count.
   Expects to run inside the caller's transaction.
   --------------------------------------------------------------------- */
CREATE OR ALTER PROCEDURE dbo.sp_Flock_RecalculatePopulation
    @FlockID INT, @FromWeek INT = NULL, @UserID INT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    DECLARE @msg NVARCHAR(400);

    DECLARE @InitM INT, @InitF INT, @ChickWt DECIMAL(10,2);
    SELECT @InitM = MaleCount, @InitF = FemaleCount, @ChickWt = ChickAvgWeight
    FROM dbo.tblFlockPlacement WHERE FlockID = @FlockID AND IsInitial = 1;
    IF @InitM IS NULL
        SELECT @InitM = MaleCount, @InitF = FemaleCount FROM dbo.tblFlock WHERE FlockID = @FlockID;

    DECLARE @mv TABLE (WeekNo INT PRIMARY KEY, InM INT, InF INT, OutM INT, OutF INT);
    INSERT INTO @mv (WeekNo, InM, InF, OutM, OutF) SELECT WeekNo, InM, InF, OutM, OutF FROM dbo.fn_FlockMovements(@FlockID);

    DECLARE @openM INT = ISNULL(@InitM, 0), @openF INT = ISNULL(@InitF, 0), @prevBW DECIMAL(10,2) = @ChickWt,
            @prevWeek INT = NULL, @first BIT = 1;
    DECLARE @wid INT, @wk INT, @mm INT, @fm INT, @mc INT, @fc INT, @bw DECIMAL(10,2),
            @feedKg DECIMAL(14,3), @wasteKg DECIMAL(14,3), @entry DATE;
    DECLARE @rM INT, @rF INT, @oM INT, @oF INT, @cM INT, @cF INT, @open INT, @close INT;

    DECLARE wk CURSOR LOCAL FAST_FORWARD FOR
        SELECT FlockWeeklyID, WeekNo, MaleMortality, FemaleMortality, MaleCulling, FemaleCulling,
               ActualBodyWeight, TotalFeedKg, FeedWastageKg, EntryDate
        FROM dbo.tblFlockWeekly WHERE FlockID = @FlockID ORDER BY WeekNo;
    OPEN wk;
    FETCH NEXT FROM wk INTO @wid, @wk, @mm, @fm, @mc, @fc, @bw, @feedKg, @wasteKg, @entry;

    WHILE @@FETCH_STATUS = 0
    BEGIN
        /* movements: the first saved week absorbs everything dated up to it */
        SELECT @rM = ISNULL(SUM(InM), 0), @rF = ISNULL(SUM(InF), 0), @oM = ISNULL(SUM(OutM), 0), @oF = ISNULL(SUM(OutF), 0)
        FROM @mv
        WHERE (@first = 1 AND WeekNo <= @wk) OR (@first = 0 AND WeekNo > @prevWeek AND WeekNo <= @wk);

        IF @mm > @openM OR @fm > @openF
        BEGIN
            SET @msg = CONCAT(N'Week ', @wk, N': mortality cannot exceed the opening population (males ', @openM, N', females ', @openF, N').');
            THROW 50201, @msg, 1;
        END

        SET @cM = @openM + @rM - @mm - @mc - @oM;
        SET @cF = @openF + @rF - @fm - @fc - @oF;
        IF @cM < 0 OR @cF < 0
        BEGIN
            SET @msg = CONCAT(N'Week ', @wk, N': the closing population would become negative (males ', @cM, N', females ', @cF, N'). Check mortality, culling and transfers.');
            THROW 50202, @msg, 1;
        END

        SET @open = @openM + @openF;
        SET @close = @cM + @cF;

        IF EXISTS (SELECT 1 FROM dbo.tblFeedConsumption WHERE FlockWeeklyID = @wid)
        BEGIN
            UPDATE dbo.tblFeedConsumption
               SET BirdDays = CASE Sex WHEN 'M' THEN (@openM + @cM) * 7 / 2
                                       WHEN 'F' THEN (@openF + @cF) * 7 / 2
                                       ELSE (@open + @close) * 7 / 2 END
             WHERE FlockWeeklyID = @wid;
            SELECT @feedKg = SUM(QuantityKg), @wasteKg = SUM(WastageKg) FROM dbo.tblFeedConsumption WHERE FlockWeeklyID = @wid;
        END

        UPDATE dbo.tblFlockWeekly
           SET OpeningMale = @openM, OpeningFemale = @openF, OpeningPopulation = @open,
               ReceivedMale = @rM, ReceivedFemale = @rF, BirdsReceived = @rM + @rF,
               TransferOutMale = @oM, TransferOutFemale = @oF, TransferOut = @oM + @oF,
               ClosingMale = @cM, ClosingFemale = @cF, ClosingPopulation = @close,
               TotalFeedKg = @feedKg, FeedWastageKg = @wasteKg,
               MortalityPercentage = CASE WHEN @open > 0 THEN CAST(ROUND((@mm + @fm) * 100.0 / @open, 4) AS DECIMAL(9,4)) END,
               ActualFeedIntake = CASE WHEN @feedKg IS NOT NULL AND (@open + @close) > 0
                                       THEN CAST(ROUND((@feedKg - ISNULL(@wasteKg, 0)) * 1000.0 / ((@open + @close) / 2.0 * 7), 2) AS DECIMAL(10,2)) END,
               ActualWeightGain = CASE WHEN @bw IS NOT NULL AND @prevBW IS NOT NULL THEN @bw - @prevBW END
         WHERE FlockWeeklyID = @wid;

        /* mortality register (one row per flock-week) */
        IF EXISTS (SELECT 1 FROM dbo.tblMortality WHERE FlockID = @FlockID AND WeekNo = @wk)
            UPDATE dbo.tblMortality
               SET FlockWeeklyID = @wid, EntryDate = @entry, OpeningPopulation = @open, MalePopulation = @openM, FemalePopulation = @openF,
                   MaleMortality = @mm, FemaleMortality = @fm, Culling = @mc + @fc, TransferIn = @rM + @rF, TransferOut = @oM + @oF,
                   ModifiedBy = @UserID, ModifiedDate = SYSDATETIME()
             WHERE FlockID = @FlockID AND WeekNo = @wk;
        ELSE
            INSERT INTO dbo.tblMortality (FlockID, FlockWeeklyID, WeekNo, EntryDate, OpeningPopulation, MalePopulation, FemalePopulation,
                                          MaleMortality, FemaleMortality, Culling, TransferIn, TransferOut, CreatedBy)
            VALUES (@FlockID, @wid, @wk, @entry, @open, @openM, @openF, @mm, @fm, @mc + @fc, @rM + @rF, @oM + @oF, @UserID);

        IF @FromWeek IS NULL OR @wk >= @FromWeek
            EXEC dbo.sp_CalculateFlockPerformance @FlockWeeklyID = @wid;

        SELECT @openM = @cM, @openF = @cF, @prevBW = @bw, @prevWeek = @wk, @first = 0;
        FETCH NEXT FROM wk INTO @wid, @wk, @mm, @fm, @mc, @fc, @bw, @feedKg, @wasteKg, @entry;
    END
    CLOSE wk;
    DEALLOCATE wk;

    /* movements after the last saved week change the live count only */
    SELECT @rM = ISNULL(SUM(InM), 0), @rF = ISNULL(SUM(InF), 0), @oM = ISNULL(SUM(OutM), 0), @oF = ISNULL(SUM(OutF), 0)
    FROM @mv WHERE @first = 1 OR WeekNo > @prevWeek;

    SET @cM = @openM + @rM - @oM;
    SET @cF = @openF + @rF - @oF;
    IF @cM < 0 OR @cF < 0
        THROW 50203, N'The live bird count would become negative. Check transfers and adjustments.', 1;

    UPDATE dbo.tblFlock SET CurrentMaleCount = @cM, CurrentFemaleCount = @cF WHERE FlockID = @FlockID;
END
GO

/* ---------------------------------------------------------------------
   WEEKLY ENTRY - context for the entry screen
   --------------------------------------------------------------------- */
CREATE OR ALTER PROCEDURE dbo.sp_FlockWeekly_GetContext
    @FlockID INT, @WeekNo INT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @Hatch DATE, @Placement DATE, @First INT, @Last INT, @Wk INT, @Wid INT, @PrevW INT, @openM INT, @openF INT;
    SELECT @Hatch = HatchDate, @Placement = PlacementDate FROM dbo.tblFlock WHERE FlockID = @FlockID;
    IF @Hatch IS NULL RETURN;

    SET @First = dbo.fn_AgeWeek(@Hatch, @Placement);
    SELECT @Last = MAX(WeekNo) FROM dbo.tblFlockWeekly WHERE FlockID = @FlockID;
    SET @Wk = ISNULL(@WeekNo, ISNULL(@Last + 1, @First));
    SELECT @Wid = FlockWeeklyID FROM dbo.tblFlockWeekly WHERE FlockID = @FlockID AND WeekNo = @Wk;

    SELECT TOP (1) @PrevW = FlockWeeklyID, @openM = ClosingMale, @openF = ClosingFemale
    FROM dbo.tblFlockWeekly WHERE FlockID = @FlockID AND WeekNo < @Wk ORDER BY WeekNo DESC;
    IF @PrevW IS NULL
        SELECT @openM = MaleCount, @openF = FemaleCount FROM dbo.tblFlockPlacement WHERE FlockID = @FlockID AND IsInitial = 1;

    DECLARE @rM INT, @rF INT, @oM INT, @oF INT;
    SELECT @rM = ISNULL(SUM(InM), 0), @rF = ISNULL(SUM(InF), 0), @oM = ISNULL(SUM(OutM), 0), @oF = ISNULL(SUM(OutF), 0)
    FROM dbo.fn_FlockMovements(@FlockID)
    WHERE (@PrevW IS NULL AND WeekNo <= @Wk) OR (@PrevW IS NOT NULL AND WeekNo = @Wk);

    SELECT  f.FlockID, f.FlockNumber, l.LotNumber, f.BirdTypeID, bt.BirdTypeName, f.BreedID, b.BreedName,
            f.FarmID, fm.FarmName, f.HouseID, h.HouseName, f.HatchDate, f.PlacementDate, f.Status,
            f.MaleCount, f.FemaleCount,
            @First AS FirstWeek, @Last AS LastWeek, ISNULL(@Last + 1, @First) AS NextWeek, @Wk AS WeekNo,
            DATEADD(DAY, 7 * (@Wk - 1) + 1, f.HatchDate) AS WeekStart, DATEADD(DAY, 7 * @Wk, f.HatchDate) AS WeekEnd,
            @Wid AS FlockWeeklyID,
            ISNULL(@openM, 0) AS OpeningMale, ISNULL(@openF, 0) AS OpeningFemale,
            @rM AS ReceivedMale, @rF AS ReceivedFemale, @oM AS TransferOutMale, @oF AS TransferOutFemale,
            pw.ActualBodyWeight AS PrevBodyWeight, pw.ActualWeightGain AS PrevWeightGain, pw.ActualFeedIntake AS PrevFeedIntake,
            pw.UniformityPercentage AS PrevUniformity, pw.MortalityPercentage AS PrevMortalityPct, pw.PerformanceScore AS PrevScore,
            ip.ChickAvgWeight, sf.FeedTypeID AS SuggestedFeedTypeID,
            CAST(CASE WHEN @Wid IS NOT NULL OR @Wk = ISNULL(@Last + 1, @First) THEN 1 ELSE 0 END AS BIT) AS CanEnter
    FROM    dbo.tblFlock f
    INNER JOIN dbo.tblLot l ON l.LotID = f.LotID
    INNER JOIN dbo.tblBirdType bt ON bt.BirdTypeID = f.BirdTypeID
    INNER JOIN dbo.tblBreed b ON b.BreedID = f.BreedID
    INNER JOIN dbo.tblFarm fm ON fm.FarmID = f.FarmID
    INNER JOIN dbo.tblHouse h ON h.HouseID = f.HouseID
    LEFT  JOIN dbo.tblFlockWeekly pw ON pw.FlockWeeklyID = @PrevW
    LEFT  JOIN dbo.tblFlockPlacement ip ON ip.FlockID = f.FlockID AND ip.IsInitial = 1
    OUTER APPLY (
        SELECT TOP (1) ft.FeedTypeID FROM dbo.tblFeedType ft
        WHERE ft.IsActive = 1
          AND (ft.ApplicableFromWeek IS NULL OR ft.ApplicableFromWeek <= @Wk)
          AND (ft.ApplicableToWeek IS NULL OR ft.ApplicableToWeek >= @Wk)
          AND (ft.TargetSex = 'B' OR ft.TargetSex = CASE WHEN f.FemaleCount > 0 THEN 'F' ELSE 'M' END)
        ORDER BY CASE WHEN ft.TargetSex = 'B' THEN 1 ELSE 0 END, ft.ApplicableFromWeek DESC
    ) sf
    WHERE   f.FlockID = @FlockID;
END
GO

/* ---------------------------------------------------------------------
   WEEKLY ENTRY - save (one transaction)
   JSON inputs:
     @SamplesJson   [{"sex":"F","w":1850}, ...]                 weight samples, grams
     @FeedJson      [{"feedTypeId":4,"formulaId":2,"sex":"F","qty":980.5,"waste":3.0}, ...]
     @NutrientsJson [{"code":"ENERGY","value":2765}, ...]       actual nutrient values
     @MortalityJson [{"reasonId":2,"sex":"F","count":3}, ...]   mortality / culling by reason
   --------------------------------------------------------------------- */
CREATE OR ALTER PROCEDURE dbo.sp_FlockWeekly_Save
    @FlockWeeklyID INT, @FlockID INT, @WeekNo INT, @EntryDate DATE,
    @FeedTypeID INT = NULL, @FeedFormulaID INT = NULL,
    @MaleMortality INT = 0, @FemaleMortality INT = 0, @MaleCulling INT = 0, @FemaleCulling INT = 0,
    @ManualBodyWeight DECIMAL(10,2) = NULL, @TotalFeedKg DECIMAL(14,3) = NULL, @FeedWastageKg DECIMAL(14,3) = NULL,
    @NutritionSource CHAR(1) = NULL, @LabSampleRef NVARCHAR(50) = NULL, @AnalysisDate DATE = NULL,
    @MortalityReasonID INT = NULL, @Remarks NVARCHAR(500) = NULL,
    @SamplesJson NVARCHAR(MAX) = NULL, @FeedJson NVARCHAR(MAX) = NULL,
    @NutrientsJson NVARCHAR(MAX) = NULL, @MortalityJson NVARCHAR(MAX) = NULL,
    @RowVer BINARY(8) = NULL,
    @UserID INT, @UserName NVARCHAR(50), @IPAddress NVARCHAR(50) = NULL,
    @NewFlockWeeklyID INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    DECLARE @msg NVARCHAR(400);

    /* ---------- validation ---------- */
    DECLARE @Hatch DATE, @Placement DATE, @Status NVARCHAR(20), @HouseID INT;
    SELECT @Hatch = HatchDate, @Placement = PlacementDate, @Status = Status, @HouseID = HouseID FROM dbo.tblFlock WHERE FlockID = @FlockID;
    IF @Hatch IS NULL THROW 50300, N'Flock not found.', 1;
    IF @Status <> N'Active' THROW 50301, N'Weekly data can only be entered for an active flock.', 1;

    DECLARE @FirstWeek INT = dbo.fn_AgeWeek(@Hatch, @Placement);
    DECLARE @LastWeek INT = (SELECT MAX(WeekNo) FROM dbo.tblFlockWeekly WHERE FlockID = @FlockID);
    DECLARE @ExistingID INT = (SELECT FlockWeeklyID FROM dbo.tblFlockWeekly WHERE FlockID = @FlockID AND WeekNo = @WeekNo);
    DECLARE @WeekStart DATE = DATEADD(DAY, 7 * (@WeekNo - 1) + 1, @Hatch);

    IF @FlockWeeklyID = 0 AND @ExistingID IS NOT NULL
        THROW 50302, N'Data for this flock and week already exists. Open the existing entry to change it.', 1;
    IF @FlockWeeklyID > 0 AND (@ExistingID IS NULL OR @ExistingID <> @FlockWeeklyID)
        THROW 50303, N'Weekly entry not found for this flock and week.', 1;
    IF @FlockWeeklyID = 0 AND @WeekNo <> ISNULL(@LastWeek + 1, @FirstWeek)
    BEGIN
        SET @msg = CONCAT(N'The next week to enter for this flock is week ', ISNULL(@LastWeek + 1, @FirstWeek), N'. Weeks are entered in order.');
        THROW 50304, @msg, 1;
    END
    IF @EntryDate IS NULL OR @EntryDate < @WeekStart OR @EntryDate > CONVERT(DATE, GETDATE())
    BEGIN
        SET @msg = CONCAT(N'Entry date must be between ', CONVERT(NVARCHAR(11), @WeekStart, 106), N' (start of week ', @WeekNo, N') and today.');
        THROW 50305, @msg, 1;
    END
    IF @MaleMortality < 0 OR @FemaleMortality < 0 OR @MaleCulling < 0 OR @FemaleCulling < 0
        THROW 50306, N'Mortality and culling cannot be negative.', 1;
    IF @ManualBodyWeight < 0 OR @TotalFeedKg < 0 OR @FeedWastageKg < 0
        THROW 50307, N'Body weight and feed quantities cannot be negative.', 1;
    IF @FeedWastageKg > @TotalFeedKg
        THROW 50308, N'Feed wastage cannot exceed the feed quantity.', 1;
    IF @FlockWeeklyID > 0 AND @RowVer IS NOT NULL
       AND EXISTS (SELECT 1 FROM dbo.tblFlockWeekly WHERE FlockWeeklyID = @FlockWeeklyID AND RowVer <> @RowVer)
        THROW 50309, N'This week was changed by another user. Reload it and try again.', 1;
    IF (@SamplesJson IS NOT NULL AND ISJSON(@SamplesJson) = 0) OR (@FeedJson IS NOT NULL AND ISJSON(@FeedJson) = 0)
       OR (@NutrientsJson IS NOT NULL AND ISJSON(@NutrientsJson) = 0) OR (@MortalityJson IS NOT NULL AND ISJSON(@MortalityJson) = 0)
        THROW 50310, N'The entry contains invalid data. Reload the page and try again.', 1;
    IF @FeedTypeID IS NOT NULL AND NOT EXISTS (SELECT 1 FROM dbo.tblFeedType WHERE FeedTypeID = @FeedTypeID)
        THROW 50311, N'Feed type not found.', 1;
    IF @FeedFormulaID IS NOT NULL AND NOT EXISTS (SELECT 1 FROM dbo.tblFeedFormula WHERE FeedFormulaID = @FeedFormulaID)
        THROW 50312, N'Feed formula not found.', 1;

    /* ---------- parse child data ---------- */
    DECLARE @s TABLE (Sex CHAR(1), SampleNo INT, W DECIMAL(10,2));
    INSERT INTO @s (Sex, SampleNo, W)
    SELECT x.Sex, ROW_NUMBER() OVER (PARTITION BY x.Sex ORDER BY CAST(j.[key] AS INT)), x.W
    FROM OPENJSON(ISNULL(@SamplesJson, N'[]')) j
    CROSS APPLY (SELECT UPPER(ISNULL(JSON_VALUE(j.value, '$.sex'), 'F')) AS Sex,
                        TRY_CONVERT(DECIMAL(10,2), JSON_VALUE(j.value, '$.w')) AS W) x
    WHERE x.W IS NOT NULL;
    IF EXISTS (SELECT 1 FROM @s WHERE W <= 0 OR W >= 10000 OR Sex NOT IN ('F', 'M'))
        THROW 50313, N'Each sample weight must be between 1 and 9,999 g.', 1;

    DECLARE @f TABLE (SeqNo INT IDENTITY(1,1), FeedTypeID INT, FeedFormulaID INT, Sex CHAR(1), Qty DECIMAL(14,3), Waste DECIMAL(14,3));
    INSERT INTO @f (FeedTypeID, FeedFormulaID, Sex, Qty, Waste)
    SELECT TRY_CONVERT(INT, JSON_VALUE(j.value, '$.feedTypeId')),
           TRY_CONVERT(INT, JSON_VALUE(j.value, '$.formulaId')),
           UPPER(ISNULL(JSON_VALUE(j.value, '$.sex'), 'B')),
           TRY_CONVERT(DECIMAL(14,3), JSON_VALUE(j.value, '$.qty')),
           ISNULL(TRY_CONVERT(DECIMAL(14,3), JSON_VALUE(j.value, '$.waste')), 0)
    FROM OPENJSON(ISNULL(@FeedJson, N'[]')) j
    WHERE TRY_CONVERT(DECIMAL(14,3), JSON_VALUE(j.value, '$.qty')) IS NOT NULL
    ORDER BY CAST(j.[key] AS INT);
    IF EXISTS (SELECT 1 FROM @f WHERE Qty < 0 OR Waste < 0 OR Waste > Qty)
        THROW 50314, N'Feed quantity and wastage cannot be negative, and wastage cannot exceed the quantity.', 1;
    IF EXISTS (SELECT 1 FROM @f x WHERE x.Sex NOT IN ('F', 'M', 'B') OR NOT EXISTS (SELECT 1 FROM dbo.tblFeedType ft WHERE ft.FeedTypeID = x.FeedTypeID))
        THROW 50315, N'Each feed line needs a valid feed type.', 1;
    IF EXISTS (SELECT 1 FROM @f x WHERE x.FeedFormulaID IS NOT NULL AND NOT EXISTS (SELECT 1 FROM dbo.tblFeedFormula ff WHERE ff.FeedFormulaID = x.FeedFormulaID))
        THROW 50316, N'Feed formula on a feed line was not found.', 1;

    DECLARE @n TABLE (ParameterID INT PRIMARY KEY, Code NVARCHAR(30), Value DECIMAL(18,4), UnitID INT);
    INSERT INTO @n (ParameterID, Code, Value, UnitID)
    SELECT p.ParameterID, p.ParameterCode, MAX(x.Value), p.UnitID
    FROM OPENJSON(ISNULL(@NutrientsJson, N'[]')) j
    CROSS APPLY (SELECT JSON_VALUE(j.value, '$.code') AS Code, TRY_CONVERT(DECIMAL(18,4), JSON_VALUE(j.value, '$.value')) AS Value) x
    INNER JOIN dbo.tblParameter p ON p.ParameterCode = x.Code AND p.ParameterGroup = N'NUTRITION'
    WHERE x.Value IS NOT NULL
    GROUP BY p.ParameterID, p.ParameterCode, p.UnitID;

    /* formula values when the source is the formula and nothing was typed */
    IF @NutritionSource = 'F' AND NOT EXISTS (SELECT 1 FROM @n) AND @FeedFormulaID IS NOT NULL
        INSERT INTO @n (ParameterID, Code, Value, UnitID)
        SELECT p.ParameterID, p.ParameterCode, d.FormulatedValue, p.UnitID
        FROM dbo.tblFeedFormulaDetail d INNER JOIN dbo.tblParameter p ON p.ParameterID = d.ParameterID AND p.ParameterGroup = N'NUTRITION'
        WHERE d.FeedFormulaID = @FeedFormulaID;

    SELECT TOP (1) @msg = CONCAT(p.ParameterName, N' ', CONVERT(NVARCHAR(30), n.Value), N' is outside the logical range ',
                                 CONVERT(NVARCHAR(30), p.LogicalMin), N' - ', CONVERT(NVARCHAR(30), p.LogicalMax), N'.')
    FROM @n n INNER JOIN dbo.tblParameter p ON p.ParameterID = n.ParameterID
    WHERE (p.LogicalMin IS NOT NULL AND n.Value < p.LogicalMin) OR (p.LogicalMax IS NOT NULL AND n.Value > p.LogicalMax);
    IF @msg IS NOT NULL THROW 50317, @msg, 1;

    DECLARE @m TABLE (ReasonID INT, Sex CHAR(1), Cnt INT);
    INSERT INTO @m (ReasonID, Sex, Cnt)
    SELECT TRY_CONVERT(INT, JSON_VALUE(j.value, '$.reasonId')), UPPER(ISNULL(JSON_VALUE(j.value, '$.sex'), 'F')),
           TRY_CONVERT(INT, JSON_VALUE(j.value, '$.count'))
    FROM OPENJSON(ISNULL(@MortalityJson, N'[]')) j
    WHERE TRY_CONVERT(INT, JSON_VALUE(j.value, '$.count')) > 0;
    IF EXISTS (SELECT 1 FROM @m x WHERE x.Sex NOT IN ('F', 'M') OR NOT EXISTS (SELECT 1 FROM dbo.tblMortalityReason r WHERE r.MortalityReasonID = x.ReasonID))
        THROW 50318, N'Each mortality reason line needs a valid reason and sex.', 1;
    IF ISNULL((SELECT SUM(Cnt) FROM @m WHERE Sex = 'M'), 0) > @MaleMortality + @MaleCulling
       OR ISNULL((SELECT SUM(Cnt) FROM @m WHERE Sex = 'F'), 0) > @FemaleMortality + @FemaleCulling
        THROW 50319, N'Birds listed by mortality reason exceed the mortality and culling entered.', 1;

    /* ---------- write ---------- */
    DECLARE @Old NVARCHAR(MAX), @New NVARCHAR(MAX), @wid INT, @PlaceHolderOpen INT = @MaleMortality + @FemaleMortality + @MaleCulling + @FemaleCulling;

    BEGIN TRANSACTION;

    IF @FlockWeeklyID = 0
    BEGIN
        /* population columns are placeholders here; sp_Flock_RecalculatePopulation sets the real chain */
        INSERT INTO dbo.tblFlockWeekly (FlockID, WeekNo, AgeInWeeks, EntryDate, HouseID, OpeningPopulation, ClosingPopulation,
                                        MaleMortality, FemaleMortality, MaleCulling, FemaleCulling, FeedTypeID, FeedFormulaID,
                                        TotalFeedKg, FeedWastageKg, ManualBodyWeight, NutritionSource, LabSampleRef, AnalysisDate,
                                        Remarks, CreatedBy)
        VALUES (@FlockID, @WeekNo, @WeekNo, @EntryDate, @HouseID, @PlaceHolderOpen, 0,
                @MaleMortality, @FemaleMortality, @MaleCulling, @FemaleCulling, @FeedTypeID, @FeedFormulaID,
                @TotalFeedKg, @FeedWastageKg, @ManualBodyWeight, @NutritionSource, @LabSampleRef, @AnalysisDate,
                @Remarks, @UserID);
        SET @wid = SCOPE_IDENTITY();
    END
    ELSE
    BEGIN
        SET @wid = @FlockWeeklyID;
        SET @Old = (SELECT WeekNo, EntryDate, FeedTypeID, FeedFormulaID, OpeningPopulation, MaleMortality, FemaleMortality, MaleCulling,
                           FemaleCulling, ClosingPopulation, TotalFeedKg, FeedWastageKg, ActualBodyWeight, ActualFeedIntake,
                           UniformityPercentage, MortalityPercentage, Energy, Protein, Calcium, PerformanceScore, Remarks
                    FROM dbo.tblFlockWeekly WHERE FlockWeeklyID = @wid FOR JSON PATH, WITHOUT_ARRAY_WRAPPER);
        UPDATE dbo.tblFlockWeekly
           SET EntryDate = @EntryDate, OpeningPopulation = @PlaceHolderOpen, BirdsReceived = 0, TransferOut = 0, ClosingPopulation = 0,
               MaleMortality = @MaleMortality, FemaleMortality = @FemaleMortality, MaleCulling = @MaleCulling, FemaleCulling = @FemaleCulling,
               FeedTypeID = @FeedTypeID, FeedFormulaID = @FeedFormulaID, TotalFeedKg = @TotalFeedKg, FeedWastageKg = @FeedWastageKg,
               ManualBodyWeight = @ManualBodyWeight, NutritionSource = @NutritionSource, LabSampleRef = @LabSampleRef,
               AnalysisDate = @AnalysisDate, Remarks = @Remarks, ModifiedBy = @UserID, ModifiedDate = SYSDATETIME()
         WHERE FlockWeeklyID = @wid;
    END

    /* weight samples */
    DELETE FROM dbo.tblWeightSample WHERE FlockWeeklyID = @wid;
    INSERT INTO dbo.tblWeightSample (FlockWeeklyID, Sex, SampleNo, WeightGrams)
    SELECT @wid, Sex, SampleNo, W FROM @s;

    /* feed lines (BirdDays set by the population chain) */
    DELETE FROM dbo.tblFeedConsumption WHERE FlockWeeklyID = @wid;
    INSERT INTO dbo.tblFeedConsumption (FlockWeeklyID, FeedTypeID, FeedFormulaID, Sex, QuantityKg, WastageKg, BirdDays)
    SELECT @wid, FeedTypeID, FeedFormulaID, Sex, Qty, Waste, 0 FROM @f ORDER BY SeqNo;

    /* nutrients: detail rows + the flat columns on the weekly row */
    DELETE FROM dbo.tblNutritionAnalysis WHERE FlockWeeklyID = @wid;
    INSERT INTO dbo.tblNutritionAnalysis (FlockWeeklyID, ParameterID, ActualValue, UnitID, SampleRef, AnalysisDate)
    SELECT @wid, ParameterID, Value, UnitID, @LabSampleRef, @AnalysisDate FROM @n;

    UPDATE dbo.tblFlockWeekly
       SET Energy       = (SELECT Value FROM @n WHERE Code = N'ENERGY'),
           Protein      = (SELECT Value FROM @n WHERE Code = N'PROTEIN'),
           Methionine   = (SELECT Value FROM @n WHERE Code = N'METHIONINE'),
           Lysine       = (SELECT Value FROM @n WHERE Code = N'LYSINE'),
           LinoleicAcid = (SELECT Value FROM @n WHERE Code = N'LINOLEIC_ACID'),
           Calcium      = (SELECT Value FROM @n WHERE Code = N'CALCIUM'),
           Phosphorus   = (SELECT Value FROM @n WHERE Code = N'PHOSPHORUS'),
           Fat          = (SELECT Value FROM @n WHERE Code = N'FAT'),
           Fibre        = (SELECT Value FROM @n WHERE Code = N'FIBRE'),
           Salt         = (SELECT Value FROM @n WHERE Code = N'SALT')
     WHERE FlockWeeklyID = @wid;

    /* body weight statistics + uniformity, then the population chain from this week on
       (which also recalculates score and alerts) */
    EXEC dbo.sp_CalculateUniformity @FlockWeeklyID = @wid;
    EXEC dbo.sp_Flock_RecalculatePopulation @FlockID = @FlockID, @FromWeek = @WeekNo, @UserID = @UserID;

    /* mortality by reason */
    DECLARE @MortalityID INT = (SELECT MortalityID FROM dbo.tblMortality WHERE FlockID = @FlockID AND WeekNo = @WeekNo);
    UPDATE dbo.tblMortality
       SET MortalityReasonID = COALESCE(@MortalityReasonID, (SELECT TOP (1) ReasonID FROM @m GROUP BY ReasonID ORDER BY SUM(Cnt) DESC)),
           Remarks = @Remarks
     WHERE MortalityID = @MortalityID;
    DELETE FROM dbo.tblMortalityDetail WHERE MortalityID = @MortalityID;
    INSERT INTO dbo.tblMortalityDetail (MortalityID, MortalityReasonID, Sex, BirdCount)
    SELECT @MortalityID, ReasonID, Sex, SUM(Cnt) FROM @m GROUP BY ReasonID, Sex;

    SET @New = (SELECT WeekNo, EntryDate, FeedTypeID, FeedFormulaID, OpeningPopulation, MaleMortality, FemaleMortality, MaleCulling,
                       FemaleCulling, ClosingPopulation, TotalFeedKg, FeedWastageKg, ActualBodyWeight, ActualFeedIntake,
                       UniformityPercentage, MortalityPercentage, Energy, Protein, Calcium, PerformanceScore, Remarks
                FROM dbo.tblFlockWeekly WHERE FlockWeeklyID = @wid FOR JSON PATH, WITHOUT_ARRAY_WRAPPER);
    DECLARE @Action NVARCHAR(20) = CASE WHEN @Old IS NULL THEN N'INSERT' ELSE N'UPDATE' END;
    EXEC dbo.sp_AuditLog_Insert @UserID, @UserName, @Action, N'tblFlockWeekly', @wid, @Old, @New, @IPAddress;

    COMMIT TRANSACTION;
    SET @NewFlockWeeklyID = @wid;
END
GO

/* Mortality-only update of an existing week (Mortality Entry screen / API). */
CREATE OR ALTER PROCEDURE dbo.sp_SaveMortality
    @FlockWeeklyID INT, @MaleMortality INT, @FemaleMortality INT, @MaleCulling INT = 0, @FemaleCulling INT = 0,
    @MortalityReasonID INT = NULL, @MortalityJson NVARCHAR(MAX) = NULL, @Remarks NVARCHAR(500) = NULL,
    @UserID INT, @UserName NVARCHAR(50), @IPAddress NVARCHAR(50) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    DECLARE @FlockID INT, @WeekNo INT;
    SELECT @FlockID = FlockID, @WeekNo = WeekNo FROM dbo.tblFlockWeekly WHERE FlockWeeklyID = @FlockWeeklyID;
    IF @FlockID IS NULL THROW 50330, N'Weekly entry not found.', 1;
    IF @MaleMortality < 0 OR @FemaleMortality < 0 OR @MaleCulling < 0 OR @FemaleCulling < 0
        THROW 50306, N'Mortality and culling cannot be negative.', 1;
    IF @MortalityJson IS NOT NULL AND ISJSON(@MortalityJson) = 0 THROW 50310, N'Invalid mortality data.', 1;

    DECLARE @m TABLE (ReasonID INT, Sex CHAR(1), Cnt INT);
    INSERT INTO @m (ReasonID, Sex, Cnt)
    SELECT TRY_CONVERT(INT, JSON_VALUE(j.value, '$.reasonId')), UPPER(ISNULL(JSON_VALUE(j.value, '$.sex'), 'F')),
           TRY_CONVERT(INT, JSON_VALUE(j.value, '$.count'))
    FROM OPENJSON(ISNULL(@MortalityJson, N'[]')) j
    WHERE TRY_CONVERT(INT, JSON_VALUE(j.value, '$.count')) > 0;
    IF EXISTS (SELECT 1 FROM @m x WHERE x.Sex NOT IN ('F', 'M') OR NOT EXISTS (SELECT 1 FROM dbo.tblMortalityReason r WHERE r.MortalityReasonID = x.ReasonID))
        THROW 50318, N'Each mortality reason line needs a valid reason and sex.', 1;

    DECLARE @Old NVARCHAR(MAX) = (SELECT MaleMortality, FemaleMortality, MaleCulling, FemaleCulling, MortalityPercentage
                                  FROM dbo.tblFlockWeekly WHERE FlockWeeklyID = @FlockWeeklyID FOR JSON PATH, WITHOUT_ARRAY_WRAPPER);
    DECLARE @PlaceHolderOpen INT = @MaleMortality + @FemaleMortality + @MaleCulling + @FemaleCulling;

    BEGIN TRANSACTION;
    UPDATE dbo.tblFlockWeekly
       SET OpeningPopulation = @PlaceHolderOpen, BirdsReceived = 0, TransferOut = 0, ClosingPopulation = 0,
           MaleMortality = @MaleMortality, FemaleMortality = @FemaleMortality, MaleCulling = @MaleCulling, FemaleCulling = @FemaleCulling,
           ModifiedBy = @UserID, ModifiedDate = SYSDATETIME()
     WHERE FlockWeeklyID = @FlockWeeklyID;

    EXEC dbo.sp_Flock_RecalculatePopulation @FlockID = @FlockID, @FromWeek = @WeekNo, @UserID = @UserID;

    DECLARE @MortalityID INT = (SELECT MortalityID FROM dbo.tblMortality WHERE FlockID = @FlockID AND WeekNo = @WeekNo);
    UPDATE dbo.tblMortality
       SET MortalityReasonID = COALESCE(@MortalityReasonID, (SELECT TOP (1) ReasonID FROM @m GROUP BY ReasonID ORDER BY SUM(Cnt) DESC)),
           Remarks = COALESCE(@Remarks, Remarks)
     WHERE MortalityID = @MortalityID;
    DELETE FROM dbo.tblMortalityDetail WHERE MortalityID = @MortalityID;
    INSERT INTO dbo.tblMortalityDetail (MortalityID, MortalityReasonID, Sex, BirdCount)
    SELECT @MortalityID, ReasonID, Sex, SUM(Cnt) FROM @m GROUP BY ReasonID, Sex;

    DECLARE @New NVARCHAR(MAX) = (SELECT MaleMortality, FemaleMortality, MaleCulling, FemaleCulling, MortalityPercentage
                                  FROM dbo.tblFlockWeekly WHERE FlockWeeklyID = @FlockWeeklyID FOR JSON PATH, WITHOUT_ARRAY_WRAPPER);
    EXEC dbo.sp_AuditLog_Insert @UserID, @UserName, N'UPDATE', N'tblMortality', @MortalityID, @Old, @New, @IPAddress;
    COMMIT TRANSACTION;
END
GO

/* Removes the latest week of a flock (corrections). */
CREATE OR ALTER PROCEDURE dbo.sp_FlockWeekly_DeleteLast
    @FlockID INT, @UserID INT, @UserName NVARCHAR(50), @IPAddress NVARCHAR(50) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    DECLARE @wid INT, @wk INT;
    SELECT TOP (1) @wid = FlockWeeklyID, @wk = WeekNo FROM dbo.tblFlockWeekly WHERE FlockID = @FlockID ORDER BY WeekNo DESC;
    IF @wid IS NULL THROW 50320, N'This flock has no weekly data.', 1;

    DECLARE @Old NVARCHAR(MAX) = (SELECT * FROM dbo.tblFlockWeekly WHERE FlockWeeklyID = @wid FOR JSON PATH, WITHOUT_ARRAY_WRAPPER);
    BEGIN TRANSACTION;
    DELETE FROM dbo.tblAlert WHERE FlockWeeklyID = @wid;
    DELETE FROM dbo.tblMortality WHERE FlockWeeklyID = @wid OR (FlockID = @FlockID AND WeekNo = @wk);
    DELETE FROM dbo.tblFlockWeekly WHERE FlockWeeklyID = @wid;
    EXEC dbo.sp_Flock_RecalculatePopulation @FlockID = @FlockID, @FromWeek = 9999, @UserID = @UserID;
    EXEC dbo.sp_AuditLog_Insert @UserID, @UserName, N'DELETE', N'tblFlockWeekly', @wid, @Old, NULL, @IPAddress;
    COMMIT TRANSACTION;
END
GO

/* ---------------------------------------------------------------------
   FLOCK PLACEMENT (additional placements after the initial one)
   --------------------------------------------------------------------- */
CREATE OR ALTER PROCEDURE dbo.sp_FlockPlacement_Add
    @FlockID INT, @PlacementDate DATE, @HouseID INT = NULL, @MaleCount INT, @FemaleCount INT,
    @Source NVARCHAR(100) = NULL, @ChickAvgWeight DECIMAL(10,2) = NULL, @Remarks NVARCHAR(500) = NULL,
    @UserID INT, @UserName NVARCHAR(50), @IPAddress NVARCHAR(50) = NULL, @NewPlacementID INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    DECLARE @FirstPlacement DATE, @Status NVARCHAR(20), @FlockHouse INT, @Hatch DATE;
    SELECT @FirstPlacement = PlacementDate, @Status = Status, @FlockHouse = HouseID, @Hatch = HatchDate FROM dbo.tblFlock WHERE FlockID = @FlockID;
    IF @Status IS NULL THROW 50500, N'Flock not found.', 1;
    IF @Status <> N'Active' THROW 50501, N'Birds can only be placed into an active flock.', 1;
    IF @MaleCount < 0 OR @FemaleCount < 0 OR @MaleCount + @FemaleCount = 0 THROW 50502, N'Enter the number of birds placed.', 1;
    IF @PlacementDate < @FirstPlacement OR @PlacementDate > CONVERT(DATE, GETDATE())
        THROW 50503, N'Placement date must be between the flock''s first placement and today.', 1;
    SET @HouseID = ISNULL(@HouseID, @FlockHouse);

    DECLARE @Capacity INT = (SELECT Capacity FROM dbo.tblHouse WHERE HouseID = @HouseID);
    DECLARE @Occupied INT = (SELECT ISNULL(SUM(TotalBirdCount), 0) FROM dbo.tblFlock WHERE HouseID = @HouseID AND Status = N'Active');
    IF @Capacity > 0 AND @Occupied + @MaleCount + @FemaleCount > @Capacity THROW 50504, N'Placement exceeds the house capacity.', 1;

    DECLARE @FromWeek INT = dbo.fn_AgeWeek(@Hatch, @PlacementDate);

    BEGIN TRANSACTION;
    INSERT INTO dbo.tblFlockPlacement (FlockID, PlacementDate, HouseID, MaleCount, FemaleCount, Source, ChickAvgWeight, IsInitial, Remarks, CreatedBy)
    VALUES (@FlockID, @PlacementDate, @HouseID, @MaleCount, @FemaleCount, @Source, @ChickAvgWeight, 0, @Remarks, @UserID);
    SET @NewPlacementID = SCOPE_IDENTITY();

    UPDATE dbo.tblFlock
       SET MaleCount = MaleCount + @MaleCount, FemaleCount = FemaleCount + @FemaleCount,
           InitialBirdCount = InitialBirdCount + @MaleCount + @FemaleCount, ModifiedBy = @UserID, ModifiedDate = SYSDATETIME()
     WHERE FlockID = @FlockID;

    EXEC dbo.sp_Flock_RecalculatePopulation @FlockID = @FlockID, @FromWeek = @FromWeek, @UserID = @UserID;

    DECLARE @New NVARCHAR(MAX) = (SELECT * FROM dbo.tblFlockPlacement WHERE FlockPlacementID = @NewPlacementID FOR JSON PATH, WITHOUT_ARRAY_WRAPPER);
    EXEC dbo.sp_AuditLog_Insert @UserID, @UserName, N'INSERT', N'tblFlockPlacement', @NewPlacementID, NULL, @New, @IPAddress;
    COMMIT TRANSACTION;
END
GO

/* ---------------------------------------------------------------------
   FLOCK TRANSFER  (OUT / IN between flocks, or HOUSE move)
   --------------------------------------------------------------------- */
CREATE OR ALTER PROCEDURE dbo.sp_FlockTransfer_Save
    @FlockID INT, @TransferDate DATE, @TransferType NVARCHAR(10), @ToHouseID INT = NULL, @CounterFlockID INT = NULL,
    @MaleCount INT = 0, @FemaleCount INT = 0, @Reason NVARCHAR(250) = NULL, @ApprovedBy INT = NULL,
    @UserID INT, @UserName NVARCHAR(50), @IPAddress NVARCHAR(50) = NULL, @NewTransferID INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    DECLARE @Status NVARCHAR(20), @FromHouse INT, @Hatch DATE, @Placed DATE, @CurM INT, @CurF INT;
    SELECT @Status = Status, @FromHouse = HouseID, @Hatch = HatchDate, @Placed = PlacementDate,
           @CurM = CurrentMaleCount, @CurF = CurrentFemaleCount
    FROM dbo.tblFlock WHERE FlockID = @FlockID;
    IF @Status IS NULL THROW 50510, N'Flock not found.', 1;
    IF @Status <> N'Active' THROW 50511, N'Transfers can only be recorded for an active flock.', 1;
    IF @TransferType NOT IN (N'IN', N'OUT', N'HOUSE') THROW 50512, N'Invalid transfer type.', 1;
    IF @TransferDate < @Placed OR @TransferDate > CONVERT(DATE, GETDATE()) THROW 50513, N'Transfer date must be between placement and today.', 1;
    IF @CounterFlockID = @FlockID THROW 50514, N'A flock cannot transfer birds to itself.', 1;
    IF @CounterFlockID IS NOT NULL AND NOT EXISTS (SELECT 1 FROM dbo.tblFlock WHERE FlockID = @CounterFlockID AND Status = N'Active')
        THROW 50515, N'The other flock must exist and be active.', 1;

    DECLARE @Week INT = dbo.fn_AgeWeek(@Hatch, @TransferDate), @ToFarm INT, @CounterWeek INT;

    IF @TransferType = N'HOUSE'
    BEGIN
        IF @ToHouseID IS NULL OR @ToHouseID = @FromHouse THROW 50516, N'Select the new house.', 1;
        SELECT @ToFarm = FarmID FROM dbo.tblHouse WHERE HouseID = @ToHouseID AND IsActive = 1;
        IF @ToFarm IS NULL THROW 50517, N'The new house is not valid or inactive.', 1;
        DECLARE @Cap INT = (SELECT Capacity FROM dbo.tblHouse WHERE HouseID = @ToHouseID);
        DECLARE @Occ INT = (SELECT ISNULL(SUM(TotalBirdCount), 0) FROM dbo.tblFlock WHERE HouseID = @ToHouseID AND Status = N'Active');
        IF @Cap > 0 AND @Occ + @CurM + @CurF > @Cap THROW 50518, N'The flock does not fit in the new house.', 1;
        SET @MaleCount = @CurM;
        SET @FemaleCount = @CurF;
    END
    ELSE
    BEGIN
        IF @MaleCount < 0 OR @FemaleCount < 0 OR @MaleCount + @FemaleCount = 0 THROW 50519, N'Enter the number of birds transferred.', 1;
        IF @TransferType = N'OUT' AND (@MaleCount > @CurM OR @FemaleCount > @CurF) THROW 50520, N'More birds than the flock holds.', 1;
        IF @TransferType = N'IN' AND @CounterFlockID IS NOT NULL
           AND EXISTS (SELECT 1 FROM dbo.tblFlock WHERE FlockID = @CounterFlockID AND (CurrentMaleCount < @MaleCount OR CurrentFemaleCount < @FemaleCount))
            THROW 50521, N'The source flock does not hold that many birds.', 1;
    END

    BEGIN TRANSACTION;
    INSERT INTO dbo.tblFlockTransfer (FlockID, TransferDate, WeekNo, TransferType, FromHouseID, ToHouseID, CounterFlockID,
                                      MaleCount, FemaleCount, Reason, ApprovedBy, CreatedBy)
    VALUES (@FlockID, @TransferDate, @Week, @TransferType, @FromHouse, @ToHouseID, @CounterFlockID,
            @MaleCount, @FemaleCount, @Reason, @ApprovedBy, @UserID);
    SET @NewTransferID = SCOPE_IDENTITY();

    IF @TransferType = N'HOUSE'
        UPDATE dbo.tblFlock SET HouseID = @ToHouseID, FarmID = @ToFarm, ModifiedBy = @UserID, ModifiedDate = SYSDATETIME() WHERE FlockID = @FlockID;
    ELSE
    BEGIN
        IF @CounterFlockID IS NOT NULL
        BEGIN
            SELECT @CounterWeek = dbo.fn_AgeWeek(HatchDate, @TransferDate) FROM dbo.tblFlock WHERE FlockID = @CounterFlockID;
            INSERT INTO dbo.tblFlockTransfer (FlockID, TransferDate, WeekNo, TransferType, FromHouseID, ToHouseID, CounterFlockID,
                                              MaleCount, FemaleCount, Reason, ApprovedBy, CreatedBy)
            SELECT @CounterFlockID, @TransferDate, @CounterWeek, CASE WHEN @TransferType = N'OUT' THEN N'IN' ELSE N'OUT' END,
                   f.HouseID, f.HouseID, @FlockID, @MaleCount, @FemaleCount, @Reason, @ApprovedBy, @UserID
            FROM dbo.tblFlock f WHERE f.FlockID = @CounterFlockID;
            EXEC dbo.sp_Flock_RecalculatePopulation @FlockID = @CounterFlockID, @FromWeek = @CounterWeek, @UserID = @UserID;
        END
        EXEC dbo.sp_Flock_RecalculatePopulation @FlockID = @FlockID, @FromWeek = @Week, @UserID = @UserID;
    END

    DECLARE @New NVARCHAR(MAX) = (SELECT * FROM dbo.tblFlockTransfer WHERE FlockTransferID = @NewTransferID FOR JSON PATH, WITHOUT_ARRAY_WRAPPER);
    EXEC dbo.sp_AuditLog_Insert @UserID, @UserName, N'INSERT', N'tblFlockTransfer', @NewTransferID, NULL, @New, @IPAddress;
    COMMIT TRANSACTION;
END
GO

/* ---------------------------------------------------------------------
   FLOCK ADJUSTMENT (authorised +/- correction of the bird count)
   --------------------------------------------------------------------- */
CREATE OR ALTER PROCEDURE dbo.sp_FlockAdjustment_Save
    @FlockID INT, @AdjustmentDate DATE, @MaleAdjustment INT, @FemaleAdjustment INT, @Reason NVARCHAR(250),
    @ApprovedBy INT = NULL, @UserID INT, @UserName NVARCHAR(50), @IPAddress NVARCHAR(50) = NULL, @NewAdjustmentID INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    DECLARE @Hatch DATE, @Placed DATE, @Status NVARCHAR(20);
    SELECT @Hatch = HatchDate, @Placed = PlacementDate, @Status = Status FROM dbo.tblFlock WHERE FlockID = @FlockID;
    IF @Hatch IS NULL THROW 50530, N'Flock not found.', 1;
    IF @MaleAdjustment = 0 AND @FemaleAdjustment = 0 THROW 50531, N'Enter a male or female adjustment.', 1;
    IF LEN(LTRIM(RTRIM(ISNULL(@Reason, N'')))) < 5 THROW 50532, N'Give a reason for the adjustment (at least 5 characters).', 1;
    IF @AdjustmentDate < @Placed OR @AdjustmentDate > CONVERT(DATE, GETDATE()) THROW 50533, N'Adjustment date must be between placement and today.', 1;

    DECLARE @Week INT = dbo.fn_AgeWeek(@Hatch, @AdjustmentDate);
    DECLARE @Old NVARCHAR(MAX) = (SELECT CurrentMaleCount, CurrentFemaleCount FROM dbo.tblFlock WHERE FlockID = @FlockID FOR JSON PATH, WITHOUT_ARRAY_WRAPPER);

    BEGIN TRANSACTION;
    INSERT INTO dbo.tblFlockAdjustment (FlockID, WeekNo, AdjustmentDate, MaleAdjustment, FemaleAdjustment, Reason, ApprovedBy, CreatedBy)
    VALUES (@FlockID, @Week, @AdjustmentDate, @MaleAdjustment, @FemaleAdjustment, LTRIM(RTRIM(@Reason)), ISNULL(@ApprovedBy, @UserID), @UserID);
    SET @NewAdjustmentID = SCOPE_IDENTITY();

    EXEC dbo.sp_Flock_RecalculatePopulation @FlockID = @FlockID, @FromWeek = @Week, @UserID = @UserID;

    DECLARE @New NVARCHAR(MAX) = (SELECT a.MaleAdjustment, a.FemaleAdjustment, a.Reason, f.CurrentMaleCount, f.CurrentFemaleCount
                                  FROM dbo.tblFlockAdjustment a INNER JOIN dbo.tblFlock f ON f.FlockID = a.FlockID
                                  WHERE a.FlockAdjustmentID = @NewAdjustmentID FOR JSON PATH, WITHOUT_ARRAY_WRAPPER);
    EXEC dbo.sp_AuditLog_Insert @UserID, @UserName, N'ADJUST', N'tblFlockAdjustment', @NewAdjustmentID, @Old, @New, @IPAddress;
    COMMIT TRANSACTION;
END
GO

PRINT N'Phase 2-3 functions and procedures created.';
GO
