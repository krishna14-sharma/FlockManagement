namespace FlockManagement.Common.Interfaces
{
    /// <summary>
    /// Who is performing the current operation. Implemented by the web layer from the
    /// authenticated principal; an API or background job can supply its own implementation.
    /// </summary>
    public interface IUserContext
    {
        int UserID { get; }
        string UserName { get; }
        int RoleID { get; }
        /// <summary>Farm restriction for the user; null means all farms.</summary>
        int? FarmID { get; }
        string IPAddress { get; }
        bool IsAuthenticated { get; }
    }
}
