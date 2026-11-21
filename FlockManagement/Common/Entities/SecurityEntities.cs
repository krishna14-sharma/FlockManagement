using System;
using FlockManagement.Common.Enums;

namespace FlockManagement.Common.Entities
{
    public class User
    {
        public int UserID { get; set; }
        public string UserName { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }
        public string MobileNumber { get; set; }
        public int RoleID { get; set; }
        public string RoleName { get; set; }
        public int? FarmID { get; set; }
        public string FarmName { get; set; }
        public bool IsActive { get; set; }
        public bool IsLocked { get; set; }
        public bool MustChangePassword { get; set; }
        public DateTime? LastLoginDate { get; set; }
    }

    /// <summary>User record including credential data; used only by the authentication service.</summary>
    public class UserCredential
    {
        public int UserID { get; set; }
        public string UserName { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }
        public string PasswordHash { get; set; }
        public int RoleID { get; set; }
        public string RoleName { get; set; }
        public int? FarmID { get; set; }
        public bool IsActive { get; set; }
        public bool IsLocked { get; set; }
        public bool RoleIsActive { get; set; }
        public int FailedLoginCount { get; set; }
        public bool MustChangePassword { get; set; }
    }

    public class LoginResult
    {
        public LoginOutcome Outcome { get; set; }
        public string Message { get; set; }
        public UserCredential User { get; set; }

        public bool Succeeded
        {
            get { return Outcome == LoginOutcome.Success || Outcome == LoginOutcome.MustChangePassword; }
        }
    }

    public class Role
    {
        public int RoleID { get; set; }
        public string RoleName { get; set; }
        public string Description { get; set; }
        public bool IsSystem { get; set; }
        public bool IsActive { get; set; }
        public int UserCount { get; set; }
        public int PermissionCount { get; set; }
    }

    public class PermissionGrant
    {
        public int PermissionID { get; set; }
        public string PermissionCode { get; set; }
        public string PermissionName { get; set; }
        public string MenuGroup { get; set; }
        public int SortOrder { get; set; }
        public bool IsGranted { get; set; }
    }

    public class MenuItem
    {
        public int PermissionID { get; set; }
        public string PermissionCode { get; set; }
        public string MenuGroup { get; set; }
        public string MenuText { get; set; }
        public string ControllerName { get; set; }
        public string ActionName { get; set; }
        public string IconClass { get; set; }
        public int SortOrder { get; set; }
    }

    public class AuditLogEntry
    {
        public long AuditLogID { get; set; }
        public int? UserID { get; set; }
        public string UserName { get; set; }
        public DateTime ActionDate { get; set; }
        public string ActionType { get; set; }
        public string TableName { get; set; }
        public string RecordID { get; set; }
        public string OldValue { get; set; }
        public string NewValue { get; set; }
        public string IPAddress { get; set; }
    }

    public class AuditSearchCriteria
    {
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public string TableName { get; set; }
        public string UserName { get; set; }
        public int PageNo { get; set; }
        public int PageSize { get; set; }

        public AuditSearchCriteria()
        {
            PageNo = 1;
            PageSize = 25;
        }
    }

    public class StatusDefinition
    {
        public string StatusCode { get; set; }
        public string StatusName { get; set; }
        public string StatusGroup { get; set; }
        public string BadgeCssClass { get; set; }
        public string ColorHex { get; set; }
        public int SortOrder { get; set; }
    }

    public class DashboardSummary
    {
        public int ActiveFlocks { get; set; }
        public int TotalBirds { get; set; }
        public int FemaleBirds { get; set; }
        public int MaleBirds { get; set; }
        public int Farms { get; set; }
        public int Houses { get; set; }
        public decimal HouseUtilisationPct { get; set; }
        public int AvgAgeWeeks { get; set; }
    }
}
