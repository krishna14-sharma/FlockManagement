namespace FlockManagement.Common.Constants
{
    /// <summary>Status codes defined in tblStatusConfig. Labels, colours and CSS come from the database.</summary>
    public static class StatusCodes
    {
        public const string Within = "WITHIN";
        public const string Below = "BELOW";
        public const string Above = "ABOVE";
        public const string Critical = "CRITICAL";
        public const string Warning = "WARNING";
        public const string Info = "INFO";
        public const string NoData = "NODATA";
        public const string Excellent = "EXCELLENT";
        public const string Good = "GOOD";
        public const string Average = "AVERAGE";
        public const string Poor = "POOR";
    }

    public static class SettingKeys
    {
        public const string MaxFailedLogins = "MAX_FAILED_LOGINS";
        public const string MinPasswordLength = "MIN_PASSWORD_LENGTH";
        public const string DefaultPageSize = "DEFAULT_PAGE_SIZE";
        public const string UniformityRangePct = "UNIFORMITY_RANGE_PCT";
        public const string AppTitle = "APP_TITLE";
    }

    public static class AuditActions
    {
        public const string Insert = "INSERT";
        public const string Update = "UPDATE";
        public const string Delete = "DELETE";
        public const string Status = "STATUS";
        public const string Login = "LOGIN";
        public const string Logout = "LOGOUT";
        public const string Password = "PASSWORD";
    }
}
