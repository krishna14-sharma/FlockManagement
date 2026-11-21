namespace FlockManagement.Common.Enums
{
    public enum FlockStatus
    {
        Active,
        Closed,
        Depleted,
        Transferred
    }

    /// <summary>Direction of a parameter: Target (both sides matter), HigherIsBetter, LowerIsBetter.</summary>
    public enum ParameterTrend
    {
        Target = 'T',
        HigherIsBetter = 'H',
        LowerIsBetter = 'L'
    }

    public enum LoginOutcome
    {
        Success,
        InvalidCredentials,
        Locked,
        Inactive,
        MustChangePassword
    }
}
