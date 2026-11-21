using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web.Mvc;
using FlockManagement.Common.Entities;
using FlockManagement.Common.Helpers;

namespace FlockManagement.Web.ViewModels
{
    public class LoginViewModel
    {
        [Required(ErrorMessage = "User name is required."), StringLength(50), Display(Name = "User Name")]
        public string UserName { get; set; }

        [Required(ErrorMessage = "Password is required."), DataType(DataType.Password), StringLength(128)]
        public string Password { get; set; }

        public string ReturnUrl { get; set; }
    }

    public class ChangePasswordViewModel
    {
        [Required, DataType(DataType.Password), Display(Name = "Current Password")]
        public string CurrentPassword { get; set; }

        [Required, DataType(DataType.Password), StringLength(128, MinimumLength = 8), Display(Name = "New Password")]
        public string NewPassword { get; set; }

        [Required, DataType(DataType.Password), Display(Name = "Confirm New Password")]
        [System.ComponentModel.DataAnnotations.Compare("NewPassword", ErrorMessage = "The passwords do not match.")]
        public string ConfirmPassword { get; set; }

        public bool IsForced { get; set; }
    }

    public class UserListViewModel
    {
        public string Search { get; set; }
        public int? RoleID { get; set; }
        public PagedResult<User> Result { get; set; }
        public IEnumerable<SelectListItem> Roles { get; set; }
        public bool CanEdit { get; set; }
    }

    public class UserEditViewModel : IValidatableObject
    {
        public int UserID { get; set; }

        [Required, StringLength(50, MinimumLength = 3), Display(Name = "User Name")]
        [RegularExpression(@"^[A-Za-z0-9._\-]+$", ErrorMessage = "Use letters, digits, '.', '_' or '-' only.")]
        public string UserName { get; set; }

        [Required, StringLength(100), Display(Name = "Full Name")]
        public string FullName { get; set; }

        [EmailAddress, StringLength(150)]
        public string Email { get; set; }

        [StringLength(20), Display(Name = "Mobile"), RegularExpression(@"^[0-9+\-\s()]*$", ErrorMessage = "Invalid mobile number.")]
        public string MobileNumber { get; set; }

        [Display(Name = "Role"), Range(1, int.MaxValue, ErrorMessage = "Role is required.")]
        public int RoleID { get; set; }

        [Display(Name = "Restrict to Farm")]
        public int? FarmID { get; set; }

        [Display(Name = "Active")]
        public bool IsActive { get; set; }

        [Display(Name = "Locked")]
        public bool IsLocked { get; set; }

        [DataType(DataType.Password), StringLength(128), Display(Name = "Initial Password")]
        public string Password { get; set; }

        [DataType(DataType.Password), Display(Name = "Confirm Password")]
        [System.ComponentModel.DataAnnotations.Compare("Password", ErrorMessage = "The passwords do not match.")]
        public string ConfirmPassword { get; set; }

        public DateTime? LastLoginDate { get; set; }

        public IEnumerable<SelectListItem> Roles { get; set; }
        public IEnumerable<SelectListItem> Farms { get; set; }

        public UserEditViewModel()
        {
            IsActive = true;
        }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (UserID == 0 && string.IsNullOrEmpty(Password))
                yield return new ValidationResult("Initial password is required for a new user.", new[] { "Password" });
        }

        public static UserEditViewModel From(User u)
        {
            return new UserEditViewModel
            {
                UserID = u.UserID, UserName = u.UserName, FullName = u.FullName, Email = u.Email, MobileNumber = u.MobileNumber,
                RoleID = u.RoleID, FarmID = u.FarmID, IsActive = u.IsActive, IsLocked = u.IsLocked, LastLoginDate = u.LastLoginDate
            };
        }

        public User ToEntity()
        {
            return new User
            {
                UserID = UserID, UserName = UserName, FullName = FullName, Email = Email, MobileNumber = MobileNumber,
                RoleID = RoleID, FarmID = FarmID, IsActive = IsActive, IsLocked = IsLocked
            };
        }
    }

    public class ResetPasswordViewModel
    {
        public int UserID { get; set; }
        public string UserName { get; set; }

        [Required, DataType(DataType.Password), StringLength(128, MinimumLength = 8), Display(Name = "New Password")]
        public string NewPassword { get; set; }

        [Required, DataType(DataType.Password), Display(Name = "Confirm Password")]
        [System.ComponentModel.DataAnnotations.Compare("NewPassword", ErrorMessage = "The passwords do not match.")]
        public string ConfirmPassword { get; set; }
    }

    public class RoleEditViewModel
    {
        public int RoleID { get; set; }

        [Required, StringLength(50), Display(Name = "Role Name")]
        public string RoleName { get; set; }

        [StringLength(250), DataType(DataType.MultilineText)]
        public string Description { get; set; }

        [Display(Name = "Active")]
        public bool IsActive { get; set; }

        public bool IsSystem { get; set; }

        public RoleEditViewModel()
        {
            IsActive = true;
        }

        public static RoleEditViewModel From(Role r)
        {
            return new RoleEditViewModel { RoleID = r.RoleID, RoleName = r.RoleName, Description = r.Description, IsActive = r.IsActive, IsSystem = r.IsSystem };
        }

        public Role ToEntity()
        {
            return new Role { RoleID = RoleID, RoleName = RoleName, Description = Description, IsActive = IsActive };
        }
    }

    public class RolePermissionsViewModel
    {
        public int RoleID { get; set; }
        public string RoleName { get; set; }
        public bool CanEdit { get; set; }
        public IList<PermissionGrant> Permissions { get; set; }
        public int[] SelectedIds { get; set; }

        public IEnumerable<IGrouping<string, PermissionGrant>> Groups
        {
            get { return (Permissions ?? new List<PermissionGrant>()).OrderBy(p => p.SortOrder).GroupBy(p => p.MenuGroup); }
        }
    }

    public class AuditListViewModel
    {
        [DataType(DataType.Date)]
        public DateTime? FromDate { get; set; }

        [DataType(DataType.Date)]
        public DateTime? ToDate { get; set; }

        public string TableName { get; set; }
        public string UserName { get; set; }
        public PagedResult<AuditLogEntry> Result { get; set; }
        public IEnumerable<SelectListItem> Tables { get; set; }
    }

    public class SidebarViewModel
    {
        public string CurrentController { get; set; }
        /// <summary>The one menu item to highlight (null = none).</summary>
        public MenuItem ActiveItem { get; set; }
        public IList<IGrouping<string, MenuItem>> Groups { get; set; }
    }

    public class DashboardViewModel
    {
        public DashboardSummary Summary { get; set; }
        public IList<Flock> ActiveFlocks { get; set; }
        public string UserFullName { get; set; }
        public string RoleName { get; set; }
    }
}
