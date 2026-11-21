/* =====================================================================
   FLOCK WEIGHT MANAGEMENT & ANALYSIS SYSTEM
   Script 06 : Phase 4 + 5 procedures
     performance score + alerts, weekly analysis, dashboard, trend,
     comparison, exceptions, alert center, reports
   Run after 05. Safe to re-run.
   ===================================================================== */
USE FlockManagementDB;
GO
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

/* ---------------------------------------------------------------------
   PERFORMANCE SCORE + ALERTS for one weekly entry
   score(component) = 100 when within standard, else
                      100 x (1 - unfavourable deviation % / ZeroScoreDeviationPct), floor 0
   nutrition        = % of analysed nutrients within standard
   total            = weighted average of the components that have data (weights in tblScoreWeightage)
   --------------------------------------------------------------------- */
CREATE OR ALTER PROCEDURE dbo.sp_CalculateFlockPerformance
    @FlockWeeklyID INT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @FlockID INT, @WeekNo INT, @Age INT, @FeedTypeID INT, @EntryDate DATE;
    SELECT @FlockID = FlockID, @WeekNo = WeekNo, @Age = AgeInWeeks, @FeedTypeID = FeedTypeID, @EntryDate = EntryDate
    FROM dbo.tblFlockWeekly WHERE FlockWeeklyID = @FlockWeeklyID;
    IF @FlockID IS NULL RETURN;

    /* capture the standards applied to this week */
    SELECT * INTO #std FROM dbo.fn_FlockStandard(@FlockID, @Age, @FeedTypeID, @EntryDate);

    UPDATE w
       SET StandardBodyWeight   = COALESCE((SELECT StandardValue FROM #std WHERE ParameterCode = N'BODY_WEIGHT'), w.StandardBodyWeight),
           StandardWeightGain   = COALESCE((SELECT StandardValue FROM #std WHERE ParameterCode = N'WEIGHT_GAIN'), w.StandardWeightGain),
           StandardFeedIntake   = COALESCE((SELECT StandardValue FROM #std WHERE ParameterCode = N'FEED_INTAKE'), w.StandardFeedIntake),
           StandardMortalityPct = COALESCE((SELECT StandardValue FROM #std WHERE ParameterCode = N'MORTALITY'), w.StandardMortalityPct),
           StandardUniformity   = COALESCE((SELECT StandardValue FROM #std WHERE ParameterCode = N'UNIFORMITY'), w.StandardUniformity)
    FROM dbo.tblFlockWeekly w WHERE w.FlockWeeklyID = @FlockWeeklyID;

    UPDATE na
       SET StandardValue = s.StandardValue, MinValue = s.MinValue, MaxValue = s.MaxValue,
           StatusCode = dbo.fn_VarianceStatus(s.StandardValue, na.ActualValue, s.MinValue, s.MaxValue, s.CriticalMinValue,
                                              s.CriticalMaxValue, s.WarningPct, s.CriticalPct, s.Direction)
    FROM dbo.tblNutritionAnalysis na INNER JOIN #std s ON s.ParameterID = na.ParameterID
    WHERE na.FlockWeeklyID = @FlockWeeklyID;

    SELECT * INTO #p FROM dbo.fn_WeeklyParameters(@FlockWeeklyID);

    /* component scores */
    SELECT  sw.ComponentCode, sw.Weightage,
            CAST(CASE
                WHEN sw.ComponentCode = N'NUTRITION' THEN
                    (SELECT CASE WHEN COUNT(*) = 0 THEN NULL
                                 ELSE SUM(CASE WHEN x.StatusCode = N'WITHIN' THEN 1 ELSE 0 END) * 100.0 / COUNT(*) END
                     FROM #p x WHERE x.ParameterGroup = N'NUTRITION' AND x.StatusCode <> N'NODATA')
                ELSE
                    (SELECT CASE WHEN x.StatusCode = N'NODATA' THEN NULL
                                 WHEN x.StatusCode = N'WITHIN' THEN 100
                                 ELSE dbo.fn_ComponentScore(x.VariancePct, x.Direction, sw.ZeroScoreDeviationPct) END
                     FROM #p x
                     WHERE x.ParameterCode = CASE sw.ComponentCode WHEN N'BODYWEIGHT' THEN N'BODY_WEIGHT'
                                                                   WHEN N'WEIGHTGAIN' THEN N'WEIGHT_GAIN'
                                                                   WHEN N'FEED'       THEN N'FEED_INTAKE'
                                                                   WHEN N'UNIFORMITY' THEN N'UNIFORMITY'
                                                                   WHEN N'MORTALITY'  THEN N'MORTALITY' END)
            END AS DECIMAL(9,4)) AS Score
    INTO    #c
    FROM    dbo.tblScoreWeightage sw
    WHERE   sw.IsActive = 1 AND sw.EffectiveFrom <= @EntryDate AND (sw.EffectiveTo IS NULL OR sw.EffectiveTo >= @EntryDate);

    DECLARE @Total DECIMAL(9,4) =
        (SELECT CAST(ROUND(SUM(Score * Weightage) / NULLIF(SUM(Weightage), 0), 2) AS DECIMAL(9,4)) FROM #c WHERE Score IS NOT NULL);
    DECLARE @BandID INT, @BandStatus NVARCHAR(20);
    SELECT TOP (1) @BandID = ScoreBandID, @BandStatus = StatusCode FROM dbo.tblScoreBand
    WHERE @Total BETWEEN MinScore AND MaxScore ORDER BY MinScore DESC;

    DELETE FROM dbo.tblPerformanceScore WHERE FlockWeeklyID = @FlockWeeklyID;
    IF @Total IS NOT NULL
        INSERT INTO dbo.tblPerformanceScore (FlockWeeklyID, FlockID, WeekNo, BodyWeightScore, WeightGainScore, FeedIntakeScore,
                                             UniformityScore, MortalityScore, NutritionScore, TotalScore, ScoreBandID)
        SELECT @FlockWeeklyID, @FlockID, @WeekNo,
               MAX(CASE WHEN ComponentCode = N'BODYWEIGHT' THEN Score END),
               MAX(CASE WHEN ComponentCode = N'WEIGHTGAIN' THEN Score END),
               MAX(CASE WHEN ComponentCode = N'FEED' THEN Score END),
               MAX(CASE WHEN ComponentCode = N'UNIFORMITY' THEN Score END),
               MAX(CASE WHEN ComponentCode = N'MORTALITY' THEN Score END),
               MAX(CASE WHEN ComponentCode = N'NUTRITION' THEN Score END),
               @Total, @BandID
        FROM #c;

    UPDATE dbo.tblFlockWeekly SET PerformanceScore = @Total, OverallStatusCode = @BandStatus WHERE FlockWeeklyID = @FlockWeeklyID;

    /* alerts: open alerts of this week are rebuilt; acknowledged / closed ones are kept */
    DELETE FROM dbo.tblAlert WHERE FlockWeeklyID = @FlockWeeklyID AND Status = N'Open';

    INSERT INTO dbo.tblAlert (FlockID, FlockWeeklyID, WeekNo, AlertType, ParameterID, StandardValue, ActualValue,
                              Deviation, DeviationPct, Severity, Message)
    SELECT  @FlockID, @FlockWeeklyID, @WeekNo, p.ParameterGroup, p.ParameterID, p.StandardValue, p.ActualValue,
            p.Variance, p.VariancePct,
            CASE WHEN p.StatusCode = N'CRITICAL' THEN N'CRITICAL' ELSE N'WARNING' END,
            CASE p.ParameterGroup
                WHEN N'BODYWEIGHT' THEN
                    CASE WHEN p.StatusCode = N'CRITICAL'
                         THEN N'CRITICAL: Flock requires immediate attention (body weight ' + CASE WHEN p.Variance < 0 THEN N'below' ELSE N'above' END + N' standard)'
                         WHEN p.Variance < 0 THEN N'WARNING: Flock weight is below standard'
                         ELSE N'WARNING: Flock weight is above standard' END
                WHEN N'WEIGHTGAIN' THEN N'Weight gain deviation (' + CASE WHEN p.Variance < 0 THEN N'below' ELSE N'above' END + N' standard)'
                WHEN N'FEED'       THEN N'Feed Intake Deviation (' + CASE WHEN p.Variance < 0 THEN N'below' ELSE N'above' END + N' standard)'
                WHEN N'MORTALITY'  THEN N'HIGH MORTALITY ALERT'
                WHEN N'UNIFORMITY' THEN N'LOW UNIFORMITY ALERT'
                WHEN N'PRODUCTION' THEN
                    CASE p.ParameterCode
                         WHEN N'HD_PRODUCTION' THEN N'LOW EGG PRODUCTION ALERT'
                         WHEN N'HATCHING_EGG_PCT' THEN N'LOW HATCHING EGG % ALERT'
                         ELSE N'Egg weight deviation (' + CASE WHEN p.Variance < 0 THEN N'below' ELSE N'above' END + N' standard)' END
                WHEN N'FERTILITY'  THEN N'LOW FERTILITY ALERT'
                WHEN N'HATCH'      THEN N'LOW HATCHABILITY ALERT'
                WHEN N'ENVIRONMENT' THEN
                    CASE WHEN p.ParameterCode LIKE N'TEMP%'
                         THEN N'Temperature deviation: ' + p.ParameterName + CASE WHEN p.Variance < 0 THEN N' below' ELSE N' above' END + N' standard'
                         ELSE N'Lighting deviation: ' + p.ParameterName + CASE WHEN p.Variance < 0 THEN N' below' ELSE N' above' END + N' standard' END
                ELSE N'NUTRITIONAL DEVIATION: ' + p.ParameterName + CASE WHEN p.Variance < 0 THEN N' below' ELSE N' above' END + N' limits'
            END
    FROM    #p p
    WHERE   p.StatusCode IN (N'BELOW', N'ABOVE', N'CRITICAL')
      /* no alert for a deviation in the favourable direction (low mortality, high uniformity, high production ...) */
      AND   NOT (p.Direction = 'L' AND p.Variance <= 0)
      AND   NOT (p.Direction = 'H' AND p.Variance >= 0)
      AND   NOT EXISTS (SELECT 1 FROM dbo.tblAlert a WHERE a.FlockWeeklyID = @FlockWeeklyID AND a.ParameterID = p.ParameterID AND a.Status <> N'Open');

    IF @BandStatus = N'CRITICAL'
       AND NOT EXISTS (SELECT 1 FROM dbo.tblAlert a WHERE a.FlockWeeklyID = @FlockWeeklyID AND a.AlertType = N'SCORE' AND a.Status <> N'Open')
        INSERT INTO dbo.tblAlert (FlockID, FlockWeeklyID, WeekNo, AlertType, ParameterID, StandardValue, ActualValue, Deviation, DeviationPct, Severity, Message)
        VALUES (@FlockID, @FlockWeeklyID, @WeekNo, N'SCORE', NULL, 100, @Total, @Total - 100, NULL, N'CRITICAL',
                CONCAT(N'CRITICAL: Overall performance score ', CONVERT(NVARCHAR(10), CAST(@Total AS DECIMAL(6,1))), N' - flock requires immediate attention'));

    DROP TABLE #c;
    DROP TABLE #p;
    DROP TABLE #std;
END
GO

/* ---------------------------------------------------------------------
   WEEKLY ENTRY / ANALYSIS - read
   --------------------------------------------------------------------- */
CREATE OR ALTER PROCEDURE dbo.sp_FlockWeekly_Get
    @FlockWeeklyID INT = NULL, @FlockID INT = NULL, @WeekNo INT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    IF @FlockWeeklyID IS NULL
        SELECT @FlockWeeklyID = FlockWeeklyID FROM dbo.tblFlockWeekly WHERE FlockID = @FlockID AND WeekNo = @WeekNo;

    SELECT  w.*, f.FlockNumber, l.LotNumber, f.BirdTypeID, bt.BirdTypeName, b.BreedName, f.FarmID, fm.FarmName, h.HouseName,
            ft.FeedTypeName, ff.FormulaCode, sb.BandName, ps.BodyWeightScore, ps.WeightGainScore, ps.FeedIntakeScore,
            ps.UniformityScore, ps.MortalityScore, ps.NutritionScore,
            (SELECT MAX(WeekNo) FROM dbo.tblFlockWeekly x WHERE x.FlockID = w.FlockID) AS LastWeek,
            (SELECT MIN(WeekNo) FROM dbo.tblFlockWeekly x WHERE x.FlockID = w.FlockID) AS FirstSavedWeek
    FROM    dbo.tblFlockWeekly w
    INNER JOIN dbo.tblFlock f ON f.FlockID = w.FlockID
    INNER JOIN dbo.tblLot l ON l.LotID = f.LotID
    INNER JOIN dbo.tblBirdType bt ON bt.BirdTypeID = f.BirdTypeID
    INNER JOIN dbo.tblBreed b ON b.BreedID = f.BreedID
    INNER JOIN dbo.tblFarm fm ON fm.FarmID = f.FarmID
    INNER JOIN dbo.tblHouse h ON h.HouseID = w.HouseID
    LEFT  JOIN dbo.tblFeedType ft ON ft.FeedTypeID = w.FeedTypeID
    LEFT  JOIN dbo.tblFeedFormula ff ON ff.FeedFormulaID = w.FeedFormulaID
    LEFT  JOIN dbo.tblPerformanceScore ps ON ps.FlockWeeklyID = w.FlockWeeklyID
    LEFT  JOIN dbo.tblScoreBand sb ON sb.ScoreBandID = ps.ScoreBandID
    WHERE   w.FlockWeeklyID = @FlockWeeklyID;
END
GO

/* Standard vs Actual for one week + previous week's actual for comparison */
CREATE OR ALTER PROCEDURE dbo.sp_GetFlockWeeklyAnalysis
    @FlockWeeklyID INT
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @FlockID INT, @WeekNo INT, @PrevID INT;
    SELECT @FlockID = FlockID, @WeekNo = WeekNo FROM dbo.tblFlockWeekly WHERE FlockWeeklyID = @FlockWeeklyID;
    SELECT TOP (1) @PrevID = FlockWeeklyID FROM dbo.tblFlockWeekly WHERE FlockID = @FlockID AND WeekNo < @WeekNo ORDER BY WeekNo DESC;

    SELECT  p.ParameterID, p.ParameterCode, p.ParameterName, p.ParameterGroup, p.UnitCode, p.Direction, p.DecimalPlaces, p.SortOrder,
            p.StandardValue, p.MinValue, p.MaxValue, p.ActualValue, p.Variance, p.VariancePct, p.StatusCode,
            pp.ActualValue AS PreviousActual, pp.StatusCode AS PreviousStatusCode
    FROM    dbo.fn_WeeklyParameters(@FlockWeeklyID) p
    LEFT JOIN dbo.fn_WeeklyParameters(@PrevID) pp ON pp.ParameterID = p.ParameterID
    ORDER BY p.SortOrder;
END
GO

/* ---------------------------------------------------------------------
   DASHBOARD - latest week of every active flock in the filter
   --------------------------------------------------------------------- */
CREATE OR ALTER PROCEDURE dbo.sp_GetFlockDashboard
    @FarmID INT = NULL, @HouseID INT = NULL, @BirdTypeID INT = NULL, @FlockID INT = NULL, @LotID INT = NULL,
    @AsOfDate DATE = NULL, @WeekNo INT = NULL, @RestrictFarmID INT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT  f.FlockID, f.FlockNumber, l.LotNumber, bt.BirdTypeName, fm.FarmName, h.HouseName, f.TotalBirdCount,
            f.CurrentFemaleCount, f.CurrentMaleCount, f.CurrentAge,
            w.FlockWeeklyID, w.WeekNo, w.EntryDate, w.ClosingPopulation, w.PerformanceScore, w.OverallStatusCode, sb.BandName,
            s.StdBodyWeight, s.BodyWeight, s.BodyWeightVarPct, s.BodyWeightStatus,
            s.StdWeightGain, s.WeightGain, s.WeightGainStatus,
            s.StdFeedIntake, s.FeedIntake, s.FeedIntakeVarPct, s.FeedIntakeStatus,
            s.StdMortality, s.Mortality, s.MortalityStatus,
            s.StdUniformity, s.Uniformity, s.UniformityStatus,
            s.NutritionOut, s.NutritionChecked, s.CriticalCount, s.OutOfStandardCount,
            (SELECT COUNT(*) FROM dbo.tblAlert a WHERE a.FlockID = f.FlockID AND a.Status = N'Open') AS OpenAlerts
    FROM    dbo.tblFlock f
    INNER JOIN dbo.tblLot l ON l.LotID = f.LotID
    INNER JOIN dbo.tblBirdType bt ON bt.BirdTypeID = f.BirdTypeID
    INNER JOIN dbo.tblFarm fm ON fm.FarmID = f.FarmID
    INNER JOIN dbo.tblHouse h ON h.HouseID = f.HouseID
    OUTER APPLY (SELECT TOP (1) x.* FROM dbo.tblFlockWeekly x
                 WHERE x.FlockID = f.FlockID AND (@AsOfDate IS NULL OR x.EntryDate <= @AsOfDate) AND (@WeekNo IS NULL OR x.WeekNo = @WeekNo)
                 ORDER BY x.WeekNo DESC) w
    OUTER APPLY dbo.fn_WeeklySummary(w.FlockWeeklyID) s
    LEFT JOIN dbo.tblPerformanceScore ps ON ps.FlockWeeklyID = w.FlockWeeklyID
    LEFT JOIN dbo.tblScoreBand sb ON sb.ScoreBandID = ps.ScoreBandID
    WHERE   f.Status = N'Active'
      AND   (@FarmID IS NULL OR f.FarmID = @FarmID)
      AND   (@HouseID IS NULL OR f.HouseID = @HouseID)
      AND   (@BirdTypeID IS NULL OR f.BirdTypeID = @BirdTypeID)
      AND   (@FlockID IS NULL OR f.FlockID = @FlockID)
      AND   (@LotID IS NULL OR f.LotID = @LotID)
      AND   (@RestrictFarmID IS NULL OR f.FarmID = @RestrictFarmID)
    ORDER BY ISNULL(w.PerformanceScore, 999), f.FlockNumber
    OPTION (RECOMPILE);
END
GO

/* ---------------------------------------------------------------------
   TREND - every week of a flock, long format (one row per week x parameter)
   plus SCORE and POPULATION pseudo-parameters for charting
   --------------------------------------------------------------------- */
CREATE OR ALTER PROCEDURE dbo.sp_GetFlockTrend
    @FlockID INT, @RestrictFarmID INT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    IF @RestrictFarmID IS NOT NULL AND NOT EXISTS (SELECT 1 FROM dbo.tblFlock WHERE FlockID = @FlockID AND FarmID = @RestrictFarmID)
        RETURN;

    SELECT  w.FlockWeeklyID, w.WeekNo, w.AgeInWeeks, w.EntryDate,
            p.ParameterCode, p.ParameterName, p.ParameterGroup, p.UnitCode, p.DecimalPlaces, p.SortOrder,
            p.StandardValue, p.MinValue, p.MaxValue, p.ActualValue, p.VariancePct, p.StatusCode
    FROM    dbo.tblFlockWeekly w
    CROSS APPLY dbo.fn_WeeklyParameters(w.FlockWeeklyID) p
    WHERE   w.FlockID = @FlockID
    UNION ALL
    SELECT  w.FlockWeeklyID, w.WeekNo, w.AgeInWeeks, w.EntryDate,
            N'SCORE', N'Performance Score', N'SCORE', N'pts', 1, 900,
            CAST(NULL AS DECIMAL(18,4)), CAST(NULL AS DECIMAL(18,4)), CAST(NULL AS DECIMAL(18,4)),
            CAST(w.PerformanceScore AS DECIMAL(18,4)), CAST(NULL AS DECIMAL(12,2)), ISNULL(w.OverallStatusCode, N'NODATA')
    FROM    dbo.tblFlockWeekly w WHERE w.FlockID = @FlockID
    UNION ALL
    SELECT  w.FlockWeeklyID, w.WeekNo, w.AgeInWeeks, w.EntryDate,
            N'POPULATION', N'Closing Population', N'POPULATION', N'birds', 0, 910,
            CAST(NULL AS DECIMAL(18,4)), CAST(NULL AS DECIMAL(18,4)), CAST(NULL AS DECIMAL(18,4)),
            CAST(w.ClosingPopulation AS DECIMAL(18,4)), CAST(NULL AS DECIMAL(12,2)), N'INFO'
    FROM    dbo.tblFlockWeekly w WHERE w.FlockID = @FlockID
    ORDER BY WeekNo, SortOrder;
END
GO

/* ---------------------------------------------------------------------
   FLOCK COMPARISON - chosen flocks at one age (or their latest week)
   --------------------------------------------------------------------- */
CREATE OR ALTER PROCEDURE dbo.sp_GetFlockComparison
    @FlockIDs NVARCHAR(MAX), @AgeWeek INT = NULL, @RestrictFarmID INT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT  f.FlockID, f.FlockNumber, l.LotNumber, bt.BirdTypeName, b.BreedName, fm.FarmName, h.HouseName,
            w.FlockWeeklyID, w.WeekNo, w.AgeInWeeks, w.EntryDate, w.ClosingPopulation, w.PerformanceScore, w.OverallStatusCode, sb.BandName,
            s.StdBodyWeight, s.BodyWeight, s.BodyWeightVarPct, s.BodyWeightStatus,
            s.StdWeightGain, s.WeightGain, s.WeightGainStatus,
            s.StdFeedIntake, s.FeedIntake, s.FeedIntakeVarPct, s.FeedIntakeStatus,
            s.StdMortality, s.Mortality, s.MortalityStatus,
            s.StdUniformity, s.Uniformity, s.UniformityStatus, s.NutritionOut, s.NutritionChecked
    FROM    (SELECT DISTINCT TRY_CONVERT(INT, LTRIM(RTRIM(value))) AS FlockID FROM STRING_SPLIT(ISNULL(@FlockIDs, N''), N',')) ids
    INNER JOIN dbo.tblFlock f ON f.FlockID = ids.FlockID
    INNER JOIN dbo.tblLot l ON l.LotID = f.LotID
    INNER JOIN dbo.tblBirdType bt ON bt.BirdTypeID = f.BirdTypeID
    INNER JOIN dbo.tblBreed b ON b.BreedID = f.BreedID
    INNER JOIN dbo.tblFarm fm ON fm.FarmID = f.FarmID
    INNER JOIN dbo.tblHouse h ON h.HouseID = f.HouseID
    OUTER APPLY (SELECT TOP (1) x.* FROM dbo.tblFlockWeekly x
                 WHERE x.FlockID = f.FlockID AND (@AgeWeek IS NULL OR x.AgeInWeeks = @AgeWeek)
                 ORDER BY x.WeekNo DESC) w
    OUTER APPLY dbo.fn_WeeklySummary(w.FlockWeeklyID) s
    LEFT JOIN dbo.tblPerformanceScore ps ON ps.FlockWeeklyID = w.FlockWeeklyID
    LEFT JOIN dbo.tblScoreBand sb ON sb.ScoreBandID = ps.ScoreBandID
    WHERE   (@RestrictFarmID IS NULL OR f.FarmID = @RestrictFarmID)
    ORDER BY f.FlockNumber;
END
GO

/* ---------------------------------------------------------------------
   EXCEPTIONS - "Flocks requiring attention" (latest week of active flocks)
   --------------------------------------------------------------------- */
CREATE OR ALTER PROCEDURE dbo.sp_GetExceptions
    @FarmID INT = NULL, @HouseID INT = NULL, @BirdTypeID INT = NULL, @CriticalOnly BIT = 0, @RestrictFarmID INT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT  f.FlockID, f.FlockNumber, l.LotNumber, fm.FarmName, h.HouseName, bt.BirdTypeName,
            w.FlockWeeklyID, w.WeekNo, w.AgeInWeeks, w.EntryDate,
            p.ParameterCode, p.ParameterName, p.ParameterGroup, p.UnitCode, p.DecimalPlaces,
            p.StandardValue, p.ActualValue, p.Variance, p.VariancePct, p.StatusCode,
            CASE WHEN p.StatusCode = N'CRITICAL' THEN N'CRITICAL' ELSE N'WARNING' END AS Severity
    FROM    dbo.tblFlock f
    INNER JOIN dbo.tblLot l ON l.LotID = f.LotID
    INNER JOIN dbo.tblFarm fm ON fm.FarmID = f.FarmID
    INNER JOIN dbo.tblHouse h ON h.HouseID = f.HouseID
    INNER JOIN dbo.tblBirdType bt ON bt.BirdTypeID = f.BirdTypeID
    CROSS APPLY (SELECT TOP (1) x.FlockWeeklyID, x.WeekNo, x.AgeInWeeks, x.EntryDate FROM dbo.tblFlockWeekly x
                 WHERE x.FlockID = f.FlockID ORDER BY x.WeekNo DESC) w
    CROSS APPLY dbo.fn_WeeklyParameters(w.FlockWeeklyID) p
    WHERE   f.Status = N'Active'
      AND   p.StatusCode IN (N'BELOW', N'ABOVE', N'CRITICAL')
      AND   NOT (p.ParameterGroup = N'MORTALITY' AND p.Variance <= 0)
      AND   NOT (p.ParameterGroup = N'UNIFORMITY' AND p.Variance >= 0)
      AND   (@CriticalOnly = 0 OR p.StatusCode = N'CRITICAL')
      AND   (@FarmID IS NULL OR f.FarmID = @FarmID)
      AND   (@HouseID IS NULL OR f.HouseID = @HouseID)
      AND   (@BirdTypeID IS NULL OR f.BirdTypeID = @BirdTypeID)
      AND   (@RestrictFarmID IS NULL OR f.FarmID = @RestrictFarmID)
    ORDER BY CASE WHEN p.StatusCode = N'CRITICAL' THEN 0 ELSE 1 END, ABS(ISNULL(p.VariancePct, 0)) DESC, f.FlockNumber
    OPTION (RECOMPILE);
END
GO

/* ---------------------------------------------------------------------
   ALERT CENTER
   --------------------------------------------------------------------- */
CREATE OR ALTER PROCEDURE dbo.sp_GetAlerts
    @Status NVARCHAR(20) = NULL, @Severity NVARCHAR(20) = NULL, @AlertType NVARCHAR(30) = NULL, @FlockID INT = NULL,
    @FarmID INT = NULL, @FromDate DATE = NULL, @ToDate DATE = NULL, @RestrictFarmID INT = NULL,
    @PageNo INT = 1, @PageSize INT = 25
AS
BEGIN
    SET NOCOUNT ON;
    IF @PageNo < 1 SET @PageNo = 1;
    IF @PageSize NOT BETWEEN 1 AND 500 SET @PageSize = 25;
    SELECT  a.AlertID, a.FlockID, f.FlockNumber, fm.FarmName, h.HouseName, a.FlockWeeklyID, a.WeekNo, a.AlertDate, a.AlertType,
            a.ParameterID, p.ParameterName, u.UnitCode, ISNULL(p.DecimalPlaces, 2) AS DecimalPlaces,
            a.StandardValue, a.ActualValue, a.Deviation, a.DeviationPct, a.Severity, a.Message, a.Status, a.ActionTaken,
            a.AcknowledgedBy, au.UserName AS AcknowledgedByName, a.AcknowledgedDate, COUNT(*) OVER () AS TotalCount
    FROM    dbo.tblAlert a
    INNER JOIN dbo.tblFlock f ON f.FlockID = a.FlockID
    INNER JOIN dbo.tblFarm fm ON fm.FarmID = f.FarmID
    INNER JOIN dbo.tblHouse h ON h.HouseID = f.HouseID
    LEFT  JOIN dbo.tblParameter p ON p.ParameterID = a.ParameterID
    LEFT  JOIN dbo.tblUnit u ON u.UnitID = p.UnitID
    LEFT  JOIN dbo.tblUsers au ON au.UserID = a.AcknowledgedBy
    WHERE   (@Status IS NULL OR a.Status = @Status)
      AND   (@Severity IS NULL OR a.Severity = @Severity)
      AND   (@AlertType IS NULL OR a.AlertType = @AlertType)
      AND   (@FlockID IS NULL OR a.FlockID = @FlockID)
      AND   (@FarmID IS NULL OR f.FarmID = @FarmID)
      AND   (@FromDate IS NULL OR a.AlertDate >= @FromDate)
      AND   (@ToDate IS NULL OR a.AlertDate < DATEADD(DAY, 1, @ToDate))
      AND   (@RestrictFarmID IS NULL OR f.FarmID = @RestrictFarmID)
    ORDER BY CASE a.Status WHEN N'Open' THEN 0 WHEN N'Acknowledged' THEN 1 ELSE 2 END,
             CASE a.Severity WHEN N'CRITICAL' THEN 0 ELSE 1 END, a.AlertDate DESC, a.AlertID DESC
    OFFSET (@PageNo - 1) * @PageSize ROWS FETCH NEXT @PageSize ROWS ONLY
    OPTION (RECOMPILE);
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_AcknowledgeAlert
    @AlertID BIGINT, @ActionTaken NVARCHAR(500), @NewStatus NVARCHAR(20) = N'Acknowledged',
    @UserID INT, @UserName NVARCHAR(50), @IPAddress NVARCHAR(50) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    IF @NewStatus NOT IN (N'Acknowledged', N'Closed') THROW 50600, N'Invalid alert status.', 1;
    IF LEN(LTRIM(RTRIM(ISNULL(@ActionTaken, N'')))) < 3 THROW 50601, N'Describe the action taken.', 1;

    DECLARE @Old NVARCHAR(MAX) = (SELECT Status, ActionTaken FROM dbo.tblAlert WHERE AlertID = @AlertID FOR JSON PATH, WITHOUT_ARRAY_WRAPPER);
    IF @Old IS NULL THROW 50602, N'Alert not found.', 1;
    IF EXISTS (SELECT 1 FROM dbo.tblAlert WHERE AlertID = @AlertID AND Status = N'Closed') THROW 50603, N'This alert is already closed.', 1;

    BEGIN TRANSACTION;
    UPDATE dbo.tblAlert
       SET Status = @NewStatus, ActionTaken = LTRIM(RTRIM(@ActionTaken)), AcknowledgedBy = @UserID, AcknowledgedDate = SYSDATETIME()
     WHERE AlertID = @AlertID;
    DECLARE @New NVARCHAR(MAX) = (SELECT Status, ActionTaken FROM dbo.tblAlert WHERE AlertID = @AlertID FOR JSON PATH, WITHOUT_ARRAY_WRAPPER);
    EXEC dbo.sp_AuditLog_Insert @UserID, @UserName, N'STATUS', N'tblAlert', @AlertID, @Old, @New, @IPAddress;
    COMMIT TRANSACTION;
END
GO

/* ---------------------------------------------------------------------
   REPORTS
   @ReportType: WEEKLY_PERFORMANCE, FLOCK_WEIGHT, FEED, MORTALITY, NUTRITION, UNIFORMITY,
                STD_VS_ACTUAL, FLOCK_COMPARISON, FARM_WISE, AGE_WISE
   Column names are the report headings.
   --------------------------------------------------------------------- */
CREATE OR ALTER PROCEDURE dbo.sp_GetFlockReport
    @ReportType NVARCHAR(30),
    @FarmID INT = NULL, @HouseID INT = NULL, @BirdTypeID INT = NULL, @FlockID INT = NULL, @LotID INT = NULL,
    @FromWeek INT = NULL, @ToWeek INT = NULL, @FromDate DATE = NULL, @ToDate DATE = NULL,
    @ActiveOnly BIT = 0, @RestrictFarmID INT = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SELECT  w.FlockWeeklyID, w.FlockID, w.WeekNo, w.AgeInWeeks, w.EntryDate,
            fm.FarmName, h.HouseName, f.FlockNumber, l.LotNumber, bt.BirdTypeName, f.FarmID
    INTO    #w
    FROM    dbo.tblFlockWeekly w
    INNER JOIN dbo.tblFlock f ON f.FlockID = w.FlockID
    INNER JOIN dbo.tblLot l ON l.LotID = f.LotID
    INNER JOIN dbo.tblBirdType bt ON bt.BirdTypeID = f.BirdTypeID
    INNER JOIN dbo.tblFarm fm ON fm.FarmID = f.FarmID
    INNER JOIN dbo.tblHouse h ON h.HouseID = w.HouseID
    WHERE   (@FarmID IS NULL OR f.FarmID = @FarmID)
      AND   (@HouseID IS NULL OR w.HouseID = @HouseID)
      AND   (@BirdTypeID IS NULL OR f.BirdTypeID = @BirdTypeID)
      AND   (@FlockID IS NULL OR f.FlockID = @FlockID)
      AND   (@LotID IS NULL OR f.LotID = @LotID)
      AND   (@FromWeek IS NULL OR w.AgeInWeeks >= @FromWeek)
      AND   (@ToWeek IS NULL OR w.AgeInWeeks <= @ToWeek)
      AND   (@FromDate IS NULL OR w.EntryDate >= @FromDate)
      AND   (@ToDate IS NULL OR w.EntryDate <= @ToDate)
      AND   (@ActiveOnly = 0 OR f.Status = N'Active')
      AND   (@RestrictFarmID IS NULL OR f.FarmID = @RestrictFarmID);

    IF @ReportType IN (N'STD_VS_ACTUAL', N'NUTRITION')
    BEGIN
        SELECT  x.FarmName AS [Farm], x.HouseName AS [House], x.FlockNumber AS [Flock], x.LotNumber AS [Lot],
                x.BirdTypeName AS [Bird Type], x.AgeInWeeks AS [Age], x.WeekNo AS [Week], x.EntryDate AS [Entry Date],
                p.ParameterName AS [Parameter], p.UnitCode AS [Unit],
                CAST(p.StandardValue AS DECIMAL(18,3)) AS [Standard], CAST(p.MinValue AS DECIMAL(18,3)) AS [Min],
                CAST(p.MaxValue AS DECIMAL(18,3)) AS [Max], CAST(p.ActualValue AS DECIMAL(18,3)) AS [Actual],
                CAST(p.Variance AS DECIMAL(18,3)) AS [Variance], p.VariancePct AS [Variance %], sc.StatusName AS [Status]
        FROM    #w x
        CROSS APPLY dbo.fn_WeeklyParameters(x.FlockWeeklyID) p
        LEFT JOIN dbo.tblStatusConfig sc ON sc.StatusCode = p.StatusCode
        WHERE   @ReportType = N'STD_VS_ACTUAL' OR p.ParameterGroup = N'NUTRITION'
        ORDER BY x.FarmName, x.FlockNumber, x.WeekNo, p.SortOrder;
    END
    ELSE IF @ReportType = N'PRODUCTION_PERFORMANCE'
    BEGIN
        /* Flock Production Performance Analysis: one row per flock-week, standard and actual side by side */
        SELECT  x.BirdTypeName AS [Bird Type], x.FlockNumber AS [Flock No], x.LotNumber AS [Lot No], x.AgeInWeeks AS [Age],
                ft.FeedTypeName AS [Feed Type], x.FarmName AS [Farm], x.HouseName AS [House], x.EntryDate AS [Entry Date],
                CAST(MAX(CASE WHEN p.ParameterCode = N'FEED_INTAKE' THEN p.StandardValue END) AS DECIMAL(10,1)) AS [Feed Intake STD],
                CAST(MAX(CASE WHEN p.ParameterCode = N'FEED_INTAKE' THEN p.ActualValue END) AS DECIMAL(10,1))   AS [Feed Intake ACT],
                CAST(MAX(CASE WHEN p.ParameterCode = N'BODY_WEIGHT' THEN p.StandardValue END) AS DECIMAL(10,0)) AS [Body Weight STD],
                CAST(MAX(CASE WHEN p.ParameterCode = N'BODY_WEIGHT' THEN p.ActualValue END) AS DECIMAL(10,0))   AS [Body Weight ACT],
                CAST(MAX(CASE WHEN p.ParameterCode = N'ENERGY' THEN p.ActualValue END) AS DECIMAL(10,0))        AS [Energy],
                CAST(MAX(CASE WHEN p.ParameterCode = N'PROTEIN' THEN p.ActualValue END) AS DECIMAL(10,2))       AS [Protein],
                CAST(MAX(CASE WHEN p.ParameterCode = N'METHIONINE' THEN p.ActualValue END) AS DECIMAL(10,3))    AS [Methionine],
                CAST(MAX(CASE WHEN p.ParameterCode = N'LYSINE' THEN p.ActualValue END) AS DECIMAL(10,3))        AS [Lysine],
                CAST(MAX(CASE WHEN p.ParameterCode = N'LINOLEIC_ACID' THEN p.ActualValue END) AS DECIMAL(10,2)) AS [Linoleic Acid],
                CAST(MAX(CASE WHEN p.ParameterCode = N'CALCIUM' THEN p.ActualValue END) AS DECIMAL(10,2))       AS [Calcium],
                CAST(MAX(CASE WHEN p.ParameterCode = N'PHOSPHORUS' THEN p.ActualValue END) AS DECIMAL(10,2))    AS [Phosphorus],
                CAST(MAX(CASE WHEN p.ParameterCode = N'FAT' THEN p.ActualValue END) AS DECIMAL(10,2))           AS [Fat],
                CAST(MAX(CASE WHEN p.ParameterCode = N'FIBRE' THEN p.ActualValue END) AS DECIMAL(10,2))         AS [Fibre],
                CAST(MAX(CASE WHEN p.ParameterCode = N'SALT' THEN p.ActualValue END) AS DECIMAL(10,3))          AS [Salt],
                CAST(MAX(CASE WHEN p.ParameterCode = N'HD_PRODUCTION' THEN p.StandardValue END) AS DECIMAL(9,2)) AS [Egg Production % STD],
                CAST(MAX(CASE WHEN p.ParameterCode = N'HD_PRODUCTION' THEN p.ActualValue END) AS DECIMAL(9,2))   AS [Egg Production % ACT],
                CAST(MAX(CASE WHEN p.ParameterCode = N'EGG_WEIGHT' THEN p.StandardValue END) AS DECIMAL(9,1))    AS [Egg Weight STD (g)],
                CAST(MAX(CASE WHEN p.ParameterCode = N'EGG_WEIGHT' THEN p.ActualValue END) AS DECIMAL(9,1))      AS [Egg Weight ACT (g)],
                CAST(MAX(CASE WHEN p.ParameterCode = N'FERTILITY' THEN p.StandardValue END) AS DECIMAL(9,2))     AS [Fertility % STD],
                CAST(MAX(CASE WHEN p.ParameterCode = N'FERTILITY' THEN p.ActualValue END) AS DECIMAL(9,2))       AS [Fertility % ACT],
                CAST(MAX(CASE WHEN p.ParameterCode = N'HATCHABILITY' THEN p.StandardValue END) AS DECIMAL(9,2))  AS [Hatchability % STD],
                CAST(MAX(CASE WHEN p.ParameterCode = N'HATCHABILITY' THEN p.ActualValue END) AS DECIMAL(9,2))    AS [Hatchability % ACT],
                CAST(MAX(CASE WHEN p.ParameterCode = N'MORTALITY' THEN p.ActualValue END) AS DECIMAL(9,2))       AS [Weekly Mortality %],
                CAST(MAX(CASE WHEN p.ParameterCode = N'LIGHT_HOURS' THEN p.ActualValue END) AS DECIMAL(5,1))     AS [Lighting (hrs)],
                CAST(MAX(CASE WHEN p.ParameterCode = N'LIGHT_INTENSITY' THEN p.ActualValue END) AS DECIMAL(8,0)) AS [Light Intensity (lux)],
                CAST(MAX(CASE WHEN p.ParameterCode = N'TEMP_DAY' THEN p.ActualValue END) AS DECIMAL(5,1))        AS [Temperature Day (C)],
                CAST(MAX(CASE WHEN p.ParameterCode = N'TEMP_NIGHT' THEN p.ActualValue END) AS DECIMAL(5,1))      AS [Temperature Night (C)]
        FROM    #w x
        INNER JOIN dbo.tblFlockWeekly w ON w.FlockWeeklyID = x.FlockWeeklyID
        LEFT  JOIN dbo.tblFeedType ft ON ft.FeedTypeID = w.FeedTypeID
        CROSS APPLY dbo.fn_WeeklyParameters(x.FlockWeeklyID) p
        GROUP BY x.BirdTypeName, x.FlockNumber, x.LotNumber, x.AgeInWeeks, ft.FeedTypeName, x.FarmName, x.HouseName, x.EntryDate, x.WeekNo
        ORDER BY x.FarmName, x.FlockNumber, x.WeekNo;
    END
    ELSE IF @ReportType = N'WEEKLY_PERFORMANCE'
    BEGIN
        SELECT  x.FarmName AS [Farm], x.HouseName AS [House], x.FlockNumber AS [Flock], x.LotNumber AS [Lot],
                x.BirdTypeName AS [Bird Type], x.AgeInWeeks AS [Age], x.WeekNo AS [Week], x.EntryDate AS [Entry Date],
                w.OpeningPopulation AS [Opening Birds], w.TotalMortality AS [Mortality], w.MaleCulling + w.FemaleCulling AS [Culls],
                w.ClosingPopulation AS [Closing Birds],
                CAST(s.StdBodyWeight AS DECIMAL(10,0)) AS [BW Std (g)], CAST(s.BodyWeight AS DECIMAL(10,0)) AS [BW Actual (g)], s.BodyWeightVarPct AS [BW Var %],
                CAST(s.StdFeedIntake AS DECIMAL(10,1)) AS [Feed Std (g/b/d)], CAST(s.FeedIntake AS DECIMAL(10,1)) AS [Feed Actual (g/b/d)],
                CAST(s.Mortality AS DECIMAL(9,2)) AS [Mortality %], CAST(s.StdMortality AS DECIMAL(9,2)) AS [Mortality Std %],
                CAST(s.Uniformity AS DECIMAL(9,1)) AS [Uniformity %], CAST(s.StdUniformity AS DECIMAL(9,1)) AS [Uniformity Std %],
                s.NutritionOut AS [Nutrients Out], CAST(w.PerformanceScore AS DECIMAL(6,1)) AS [Score], sb.BandName AS [Status]
        FROM    #w x
        INNER JOIN dbo.tblFlockWeekly w ON w.FlockWeeklyID = x.FlockWeeklyID
        OUTER APPLY dbo.fn_WeeklySummary(x.FlockWeeklyID) s
        LEFT JOIN dbo.tblPerformanceScore ps ON ps.FlockWeeklyID = w.FlockWeeklyID
        LEFT JOIN dbo.tblScoreBand sb ON sb.ScoreBandID = ps.ScoreBandID
        ORDER BY x.FarmName, x.FlockNumber, x.WeekNo;
    END
    ELSE IF @ReportType = N'FLOCK_WEIGHT'
    BEGIN
        SELECT  x.FarmName AS [Farm], x.HouseName AS [House], x.FlockNumber AS [Flock], x.LotNumber AS [Lot],
                x.BirdTypeName AS [Bird Type], x.AgeInWeeks AS [Age], x.WeekNo AS [Week], x.EntryDate AS [Entry Date],
                w.SampleSize AS [Birds Weighed],
                CAST(s.StdBodyWeight AS DECIMAL(10,0)) AS [Standard (g)], CAST(w.ActualBodyWeight AS DECIMAL(10,0)) AS [Actual (g)],
                CAST(w.ActualBodyWeight - s.StdBodyWeight AS DECIMAL(10,0)) AS [Variance (g)], s.BodyWeightVarPct AS [Variance %],
                sc.StatusName AS [Status], w.MinBodyWeight AS [Min (g)], w.MaxBodyWeight AS [Max (g)],
                CAST(s.StdWeightGain AS DECIMAL(10,0)) AS [Gain Std (g)], CAST(w.ActualWeightGain AS DECIMAL(10,0)) AS [Gain Actual (g)],
                sg.StatusName AS [Gain Status]
        FROM    #w x
        INNER JOIN dbo.tblFlockWeekly w ON w.FlockWeeklyID = x.FlockWeeklyID
        OUTER APPLY dbo.fn_WeeklySummary(x.FlockWeeklyID) s
        LEFT JOIN dbo.tblStatusConfig sc ON sc.StatusCode = s.BodyWeightStatus
        LEFT JOIN dbo.tblStatusConfig sg ON sg.StatusCode = s.WeightGainStatus
        ORDER BY x.FarmName, x.FlockNumber, x.WeekNo;
    END
    ELSE IF @ReportType = N'FEED'
    BEGIN
        SELECT  x.FarmName AS [Farm], x.HouseName AS [House], x.FlockNumber AS [Flock], x.LotNumber AS [Lot],
                x.BirdTypeName AS [Bird Type], x.AgeInWeeks AS [Age], x.WeekNo AS [Week], x.EntryDate AS [Entry Date],
                ft.FeedTypeName AS [Feed Type], w.TotalFeedKg AS [Feed (kg)], w.FeedWastageKg AS [Wastage (kg)],
                CAST((w.OpeningPopulation + w.ClosingPopulation) / 2.0 AS DECIMAL(12,0)) AS [Average Birds],
                CAST(s.StdFeedIntake AS DECIMAL(10,1)) AS [Standard (g/b/d)], CAST(s.FeedIntake AS DECIMAL(10,1)) AS [Actual (g/b/d)],
                s.FeedIntakeVarPct AS [Variance %], sc.StatusName AS [Status],
                CAST(CASE WHEN w.ActualWeightGain > 0 THEN s.FeedIntake * 7 / w.ActualWeightGain END AS DECIMAL(10,2)) AS [Feed : Gain]
        FROM    #w x
        INNER JOIN dbo.tblFlockWeekly w ON w.FlockWeeklyID = x.FlockWeeklyID
        LEFT JOIN dbo.tblFeedType ft ON ft.FeedTypeID = w.FeedTypeID
        OUTER APPLY dbo.fn_WeeklySummary(x.FlockWeeklyID) s
        LEFT JOIN dbo.tblStatusConfig sc ON sc.StatusCode = s.FeedIntakeStatus
        ORDER BY x.FarmName, x.FlockNumber, x.WeekNo;
    END
    ELSE IF @ReportType = N'MORTALITY'
    BEGIN
        SELECT  x.FarmName AS [Farm], x.HouseName AS [House], x.FlockNumber AS [Flock], x.LotNumber AS [Lot],
                x.BirdTypeName AS [Bird Type], x.AgeInWeeks AS [Age], x.WeekNo AS [Week], x.EntryDate AS [Entry Date],
                w.OpeningPopulation AS [Opening], w.BirdsReceived AS [Received], w.MaleMortality AS [Male Mort.],
                w.FemaleMortality AS [Female Mort.], w.TotalMortality AS [Total Mort.], w.MaleCulling + w.FemaleCulling AS [Culls],
                w.TransferOut AS [Transfer Out], w.ClosingPopulation AS [Closing],
                CAST(w.MortalityPercentage AS DECIMAL(9,3)) AS [Mortality %], CAST(s.StdMortality AS DECIMAL(9,3)) AS [Standard %],
                sc.StatusName AS [Status], mr.ReasonName AS [Main Reason]
        FROM    #w x
        INNER JOIN dbo.tblFlockWeekly w ON w.FlockWeeklyID = x.FlockWeeklyID
        OUTER APPLY dbo.fn_WeeklySummary(x.FlockWeeklyID) s
        LEFT JOIN dbo.tblStatusConfig sc ON sc.StatusCode = s.MortalityStatus
        LEFT JOIN dbo.tblMortality m ON m.FlockID = x.FlockID AND m.WeekNo = x.WeekNo
        LEFT JOIN dbo.tblMortalityReason mr ON mr.MortalityReasonID = m.MortalityReasonID
        ORDER BY x.FarmName, x.FlockNumber, x.WeekNo;
    END
    ELSE IF @ReportType = N'UNIFORMITY'
    BEGIN
        DECLARE @ex DECIMAL(9,4) = ISNULL(TRY_CONVERT(DECIMAL(9,4), (SELECT SettingValue FROM dbo.tblSystemSetting WHERE SettingKey = N'UNIFORMITY_EXCELLENT_MIN')), 85);
        DECLARE @gd DECIMAL(9,4) = ISNULL(TRY_CONVERT(DECIMAL(9,4), (SELECT SettingValue FROM dbo.tblSystemSetting WHERE SettingKey = N'UNIFORMITY_GOOD_MIN')), 80);
        DECLARE @wn DECIMAL(9,4) = ISNULL(TRY_CONVERT(DECIMAL(9,4), (SELECT SettingValue FROM dbo.tblSystemSetting WHERE SettingKey = N'UNIFORMITY_WARNING_MIN')), 70);
        SELECT  x.FarmName AS [Farm], x.HouseName AS [House], x.FlockNumber AS [Flock], x.LotNumber AS [Lot],
                x.BirdTypeName AS [Bird Type], x.AgeInWeeks AS [Age], x.WeekNo AS [Week], x.EntryDate AS [Entry Date],
                w.SampleSize AS [Birds Weighed], CAST(w.ActualBodyWeight AS DECIMAL(10,0)) AS [Average (g)],
                w.MinBodyWeight AS [Min (g)], w.MaxBodyWeight AS [Max (g)], w.StdDevBodyWeight AS [Std Dev (g)],
                CAST(w.CVPercentage AS DECIMAL(9,2)) AS [CV %], CAST(w.UniformityPercentage AS DECIMAL(9,1)) AS [Uniformity %],
                CAST(s.StdUniformity AS DECIMAL(9,1)) AS [Target %],
                CASE WHEN w.UniformityPercentage IS NULL THEN N'No Data' WHEN w.UniformityPercentage >= @ex THEN N'Excellent'
                     WHEN w.UniformityPercentage >= @gd THEN N'Good' WHEN w.UniformityPercentage >= @wn THEN N'Warning' ELSE N'Critical' END AS [Grade],
                sc.StatusName AS [Status vs Target]
        FROM    #w x
        INNER JOIN dbo.tblFlockWeekly w ON w.FlockWeeklyID = x.FlockWeeklyID
        OUTER APPLY dbo.fn_WeeklySummary(x.FlockWeeklyID) s
        LEFT JOIN dbo.tblStatusConfig sc ON sc.StatusCode = s.UniformityStatus
        ORDER BY x.FarmName, x.FlockNumber, x.WeekNo;
    END
    ELSE IF @ReportType = N'FLOCK_COMPARISON'
    BEGIN
        SELECT  x.FarmName AS [Farm], x.HouseName AS [House], x.FlockNumber AS [Flock], x.LotNumber AS [Lot],
                x.BirdTypeName AS [Bird Type], x.AgeInWeeks AS [Age], x.WeekNo AS [Week],
                CAST(s.BodyWeight AS DECIMAL(10,0)) AS [Body Weight (g)], CAST(s.StdBodyWeight AS DECIMAL(10,0)) AS [BW Std (g)],
                CAST(s.FeedIntake AS DECIMAL(10,1)) AS [Feed Intake (g/b/d)], CAST(s.Uniformity AS DECIMAL(9,1)) AS [Uniformity %],
                CAST(s.Mortality AS DECIMAL(9,2)) AS [Mortality %], CAST(w.PerformanceScore AS DECIMAL(6,1)) AS [Score], sb.BandName AS [Status]
        FROM    #w x
        INNER JOIN (SELECT FlockID, MAX(WeekNo) AS WeekNo FROM #w GROUP BY FlockID) lastw ON lastw.FlockID = x.FlockID AND lastw.WeekNo = x.WeekNo
        INNER JOIN dbo.tblFlockWeekly w ON w.FlockWeeklyID = x.FlockWeeklyID
        OUTER APPLY dbo.fn_WeeklySummary(x.FlockWeeklyID) s
        LEFT JOIN dbo.tblPerformanceScore ps ON ps.FlockWeeklyID = w.FlockWeeklyID
        LEFT JOIN dbo.tblScoreBand sb ON sb.ScoreBandID = ps.ScoreBandID
        ORDER BY w.PerformanceScore DESC;
    END
    ELSE IF @ReportType = N'FARM_WISE'
    BEGIN
        SELECT  x.FarmName AS [Farm], COUNT(*) AS [Flocks], SUM(w.ClosingPopulation) AS [Live Birds],
                CAST(AVG(CAST(x.AgeInWeeks AS DECIMAL(9,2))) AS DECIMAL(9,1)) AS [Avg Age (wk)],
                CAST(AVG(s.BodyWeightVarPct) AS DECIMAL(9,2)) AS [Avg BW Var %],
                CAST(AVG(s.FeedIntakeVarPct) AS DECIMAL(9,2)) AS [Avg Feed Var %],
                CAST(AVG(s.Mortality) AS DECIMAL(9,3)) AS [Avg Weekly Mort. %],
                CAST(AVG(s.Uniformity) AS DECIMAL(9,1)) AS [Avg Uniformity %],
                CAST(AVG(w.PerformanceScore) AS DECIMAL(6,1)) AS [Avg Score],
                SUM(CASE WHEN s.CriticalCount > 0 THEN 1 ELSE 0 END) AS [Flocks with Critical],
                SUM(s.OutOfStandardCount) AS [Parameters Out]
        FROM    #w x
        INNER JOIN (SELECT FlockID, MAX(WeekNo) AS WeekNo FROM #w GROUP BY FlockID) lastw ON lastw.FlockID = x.FlockID AND lastw.WeekNo = x.WeekNo
        INNER JOIN dbo.tblFlockWeekly w ON w.FlockWeeklyID = x.FlockWeeklyID
        OUTER APPLY dbo.fn_WeeklySummary(x.FlockWeeklyID) s
        GROUP BY x.FarmName
        ORDER BY x.FarmName;
    END
    ELSE IF @ReportType = N'AGE_WISE'
    BEGIN
        SELECT  x.AgeInWeeks AS [Age (wk)], COUNT(*) AS [Flocks],
                CAST(AVG(s.StdBodyWeight) AS DECIMAL(10,0)) AS [BW Std (g)], CAST(AVG(s.BodyWeight) AS DECIMAL(10,0)) AS [BW Avg (g)],
                CAST(AVG(s.BodyWeightVarPct) AS DECIMAL(9,2)) AS [BW Var %],
                CAST(AVG(s.StdFeedIntake) AS DECIMAL(10,1)) AS [Feed Std (g/b/d)], CAST(AVG(s.FeedIntake) AS DECIMAL(10,1)) AS [Feed Avg (g/b/d)],
                CAST(AVG(s.Mortality) AS DECIMAL(9,3)) AS [Mortality %], CAST(AVG(s.Uniformity) AS DECIMAL(9,1)) AS [Uniformity %],
                CAST(AVG(w.PerformanceScore) AS DECIMAL(6,1)) AS [Avg Score]
        FROM    #w x
        INNER JOIN dbo.tblFlockWeekly w ON w.FlockWeeklyID = x.FlockWeeklyID
        OUTER APPLY dbo.fn_WeeklySummary(x.FlockWeeklyID) s
        GROUP BY x.AgeInWeeks
        ORDER BY x.AgeInWeeks;
    END
    ELSE
        THROW 50700, N'Unknown report type.', 1;

    DROP TABLE #w;
END
GO

PRINT N'Phase 4-5 procedures created.';
GO
