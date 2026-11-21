namespace FlockManagement.Common.Constants
{
    /// <summary>
    /// Permission codes stored in tblPermissions. Controllers reference these constants
    /// (never role names), so access rules are changed in the database, not in code.
    /// </summary>
    public static class PermissionCodes
    {
        public const string DashboardView = "DASHBOARD_VIEW";

        public const string FarmView = "FARM_VIEW";
        public const string FarmEdit = "FARM_EDIT";
        public const string HouseView = "HOUSE_VIEW";
        public const string HouseEdit = "HOUSE_EDIT";
        public const string BirdTypeView = "BIRDTYPE_VIEW";
        public const string BirdTypeEdit = "BIRDTYPE_EDIT";
        public const string BreedView = "BREED_VIEW";
        public const string BreedEdit = "BREED_EDIT";
        public const string LotView = "LOT_VIEW";
        public const string LotEdit = "LOT_EDIT";
        public const string FeedTypeView = "FEEDTYPE_VIEW";
        public const string FeedTypeEdit = "FEEDTYPE_EDIT";
        public const string FlockView = "FLOCK_VIEW";
        public const string FlockEdit = "FLOCK_EDIT";
        public const string FlockStatus = "FLOCK_STATUS";

        // Phase 2-5 masters
        public const string StandardView = "STANDARD_VIEW";
        public const string StandardEdit = "STANDARD_EDIT";
        public const string FeedFormulaView = "FEEDFORMULA_VIEW";
        public const string FeedFormulaEdit = "FEEDFORMULA_EDIT";
        public const string MortalityReasonView = "MORTREASON_VIEW";
        public const string MortalityReasonEdit = "MORTREASON_EDIT";

        // Transactions
        public const string PlacementView = "PLACEMENT_VIEW";
        public const string PlacementEdit = "PLACEMENT_EDIT";
        public const string WeeklyView = "WEEKLY_VIEW";
        public const string WeeklyEntry = "TXN_WEEKLY";
        public const string BodyWeightEntry = "TXN_BODYWEIGHT";
        public const string FeedEntry = "TXN_FEED";
        public const string NutritionEntry = "TXN_NUTRITION";
        public const string MortalityEntry = "TXN_MORTALITY";
        public const string WeeklyDelete = "WEEKLY_DELETE";
        public const string TransferView = "TRANSFER_VIEW";
        public const string TransferEdit = "TRANSFER_EDIT";
        public const string AdjustView = "ADJUST_VIEW";
        public const string AdjustEdit = "ADJUST_EDIT";

        /// <summary>Any of these opens the weekly entry screen.</summary>
        public const string ProductionEntry = "TXN_PRODUCTION";
        public const string HatchView = "HATCH_VIEW";
        public const string HatchEdit = "HATCH_EDIT";
        public const string AnalysisProduction = "ANALYSIS_PRODUCTION";
        public static readonly string[] AnyWeeklyEntry = { WeeklyEntry, BodyWeightEntry, FeedEntry, NutritionEntry, MortalityEntry, ProductionEntry };

        // Analysis
        public const string AnalysisWeekly = "ANALYSIS_WEEKLY";
        public const string AnalysisStdVsActual = "ANALYSIS_SVA";
        public const string AnalysisWeight = "ANALYSIS_WEIGHT";
        public const string AnalysisFeed = "ANALYSIS_FEED";
        public const string AnalysisNutrition = "ANALYSIS_NUTRITION";
        public const string AnalysisUniformity = "ANALYSIS_UNIFORMITY";
        public const string AnalysisMortality = "ANALYSIS_MORTALITY";
        public const string AnalysisTrend = "ANALYSIS_TREND";
        public const string AnalysisCompare = "ANALYSIS_COMPARE";
        public const string ExceptionView = "EXCEPTION_VIEW";
        public const string AlertView = "ALERT_VIEW";
        public const string AlertAck = "ALERT_ACK";

        // Reports & admin
        public const string ReportView = "REPORT_VIEW";
        public const string ReportExport = "REPORT_EXPORT";
        public const string ConfigEdit = "CONFIG_EDIT";
        public const string ApiAccess = "API_ACCESS";

        public const string UserView = "USER_VIEW";
        public const string UserEdit = "USER_EDIT";
        public const string RoleView = "ROLE_VIEW";
        public const string RoleEdit = "ROLE_EDIT";
        public const string AuditView = "AUDIT_VIEW";
    }
}
