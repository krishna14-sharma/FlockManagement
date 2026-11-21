using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Caching;
using FlockManagement.Business.Interfaces;
using FlockManagement.Common.Constants;
using FlockManagement.Common.Entities;
using FlockManagement.Common.Enums;
using FlockManagement.Common.Helpers;
using FlockManagement.Common.Interfaces;
using FlockManagement.Data.Interfaces;

namespace FlockManagement.Business.Services
{
    public class AuthService : IAuthService
    {
        private static readonly Lazy<string> DummyHash = new Lazy<string>(() => PasswordHasher.Hash(Guid.NewGuid().ToString("N")));

        private readonly IUserRepository _users;
        private readonly IAuditLogRepository _audit;
        private readonly ISettingsService _settings;
        private readonly IUserContext _user;

        public AuthService(IUserRepository users, IAuditLogRepository audit, ISettingsService settings, IUserContext user)
        {
            _users = users;
            _audit = audit;
            _settings = settings;
            _user = user;
        }

        public LoginResult Login(string userName, string password, string ipAddress)
        {
            const string invalid = "Invalid user name or password.";
            userName = (userName ?? string.Empty).Trim();
            var maxFailed = _settings.GetInt(SettingKeys.MaxFailedLogins, 5);

            if (userName.Length == 0 || string.IsNullOrEmpty(password))
                return new LoginResult { Outcome = LoginOutcome.InvalidCredentials, Message = invalid };

            var user = _users.GetForLogin(userName);
            if (user == null)
            {
                // Hash anyway so response time does not reveal whether the user exists
                PasswordHasher.Verify(password, DummyHash.Value);
                _users.RecordLogin(null, userName, false, ipAddress, maxFailed, "Unknown user");
                return new LoginResult { Outcome = LoginOutcome.InvalidCredentials, Message = invalid };
            }

            if (user.IsLocked)
            {
                _users.RecordLogin(user.UserID, userName, false, ipAddress, maxFailed, "Account locked");
                return new LoginResult { Outcome = LoginOutcome.Locked, Message = "Your account is locked. Please contact the administrator." };
            }

            if (!user.IsActive || !user.RoleIsActive)
            {
                _users.RecordLogin(user.UserID, userName, false, ipAddress, maxFailed, "Inactive");
                return new LoginResult { Outcome = LoginOutcome.Inactive, Message = "Your account is inactive. Please contact the administrator." };
            }

            if (!PasswordHasher.Verify(password, user.PasswordHash))
            {
                var nowLocked = _users.RecordLogin(user.UserID, userName, false, ipAddress, maxFailed, "Wrong password");
                return new LoginResult
                {
                    Outcome = nowLocked ? LoginOutcome.Locked : LoginOutcome.InvalidCredentials,
                    Message = nowLocked ? "Too many failed attempts. Your account has been locked." : invalid
                };
            }

            _users.RecordLogin(user.UserID, userName, true, ipAddress, maxFailed, null);
            user.PasswordHash = null; // never keep the hash beyond this point
            return new LoginResult
            {
                Outcome = user.MustChangePassword ? LoginOutcome.MustChangePassword : LoginOutcome.Success,
                User = user
            };
        }

        public void RecordLogout(int userId, string userName, string ipAddress)
        {
            _audit.Write(_user, AuditActions.Logout, "tblUsers", userId, null, null);
        }

        public OperationResult ChangePassword(int userId, string currentPassword, string newPassword)
        {
            var stored = _users.GetPasswordHash(userId);
            if (stored == null) return OperationResult.Fail("User not found.");
            if (!PasswordHasher.Verify(currentPassword, stored))
                return OperationResult.Fail("Current password is incorrect.", "CurrentPassword");
            if (string.Equals(currentPassword, newPassword, StringComparison.Ordinal))
                return OperationResult.Fail("The new password must be different from the current password.", "NewPassword");

            var policyError = ValidatePasswordPolicy(newPassword);
            if (policyError != null) return OperationResult.Fail(policyError, "NewPassword");

            try
            {
                _users.ChangePassword(userId, PasswordHasher.Hash(newPassword), false, _user);
                return OperationResult.Ok("Password changed successfully.");
            }
            catch (BusinessException ex)
            {
                return OperationResult.Fail(ex.Message);
            }
        }

        public string ValidatePasswordPolicy(string password)
        {
            var min = _settings.GetInt(SettingKeys.MinPasswordLength, 8);
            if (string.IsNullOrEmpty(password) || password.Length < min)
                return string.Format("Password must be at least {0} characters.", min);
            if (password.Length > 128) return "Password is too long.";
            if (!password.Any(char.IsUpper) || !password.Any(char.IsLower) || !password.Any(char.IsDigit))
                return "Password must contain upper-case, lower-case letters and a digit.";
            return null;
        }
    }

    /// <summary>Role permissions and menus, cached in memory and invalidated when a role changes.</summary>
    public class PermissionService : IPermissionService
    {
        private static readonly MemoryCache Cache = MemoryCache.Default;
        private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(20);
        private readonly IRoleRepository _roles;

        public PermissionService(IRoleRepository roles)
        {
            _roles = roles;
        }

        public ISet<string> GetPermissionCodes(int roleId)
        {
            var key = "perm:" + roleId;
            var cached = Cache.Get(key) as ISet<string>;
            if (cached != null) return cached;

            ISet<string> codes = new HashSet<string>(_roles.GetPermissionCodes(roleId), StringComparer.OrdinalIgnoreCase);
            Cache.Set(key, codes, DateTimeOffset.UtcNow.Add(Lifetime));
            return codes;
        }

        public IList<MenuItem> GetMenu(int roleId)
        {
            var key = "menu:" + roleId;
            var cached = Cache.Get(key) as IList<MenuItem>;
            if (cached != null) return cached;

            var menu = _roles.GetMenu(roleId);
            Cache.Set(key, menu, DateTimeOffset.UtcNow.Add(Lifetime));
            return menu;
        }

        public void Invalidate(int roleId)
        {
            Cache.Remove("perm:" + roleId);
            Cache.Remove("menu:" + roleId);
        }
    }

    public class UserService : IUserService
    {
        private readonly IUserRepository _users;
        private readonly IRoleRepository _roles;
        private readonly IAuthService _auth;
        private readonly IUserContext _user;

        public UserService(IUserRepository users, IRoleRepository roles, IAuthService auth, IUserContext user)
        {
            _users = users;
            _roles = roles;
            _auth = auth;
            _user = user;
        }

        public PagedResult<User> Search(string search, int? roleId, int pageNo, int pageSize)
        {
            if (pageNo < 1) pageNo = 1;
            if (pageSize < 1 || pageSize > PageRequest.MaxPageSize) pageSize = 25;
            return _users.Search(search, roleId, pageNo, pageSize);
        }

        public User Get(int userId)
        {
            return _users.GetById(userId);
        }

        public OperationResult<int> Save(User u, string newPassword)
        {
            if (u == null) return OperationResult<int>.Fail("No data received.");
            u.UserName = string.IsNullOrWhiteSpace(u.UserName) ? null : u.UserName.Trim().ToLowerInvariant();
            u.FullName = string.IsNullOrWhiteSpace(u.FullName) ? null : u.FullName.Trim();
            u.Email = string.IsNullOrWhiteSpace(u.Email) ? null : u.Email.Trim();
            u.MobileNumber = string.IsNullOrWhiteSpace(u.MobileNumber) ? null : u.MobileNumber.Trim();

            if (u.UserName == null) return OperationResult<int>.Fail("User name is required.", "UserName");
            if (u.UserName.Any(ch => !(char.IsLetterOrDigit(ch) || ch == '.' || ch == '_' || ch == '-')))
                return OperationResult<int>.Fail("User name may only contain letters, digits, '.', '_' and '-'.", "UserName");
            if (u.FullName == null) return OperationResult<int>.Fail("Full name is required.", "FullName");

            string hash = null;
            if (u.UserID == 0)
            {
                var policy = _auth.ValidatePasswordPolicy(newPassword);
                if (policy != null) return OperationResult<int>.Fail(policy, "Password");
                hash = PasswordHasher.Hash(newPassword);
            }

            try
            {
                var id = _users.Save(u, hash, _user);
                return OperationResult<int>.Ok(id, u.UserID == 0
                    ? "User created. The user must change the password at first login."
                    : "User updated.");
            }
            catch (BusinessException ex)
            {
                return OperationResult<int>.Fail(ex.Message, ex.FieldName);
            }
        }

        public OperationResult ResetPassword(int userId, string newPassword)
        {
            var policy = _auth.ValidatePasswordPolicy(newPassword);
            if (policy != null) return OperationResult.Fail(policy, "NewPassword");
            try
            {
                _users.ChangePassword(userId, PasswordHasher.Hash(newPassword), true, _user);
                return OperationResult.Ok("Password reset. The user must change it at next login.");
            }
            catch (BusinessException ex)
            {
                return OperationResult.Fail(ex.Message);
            }
        }

        public IList<LookupItem> RoleLookup()
        {
            return _roles.GetLookup();
        }
    }

    public class RoleService : IRoleService
    {
        private readonly IRoleRepository _roles;
        private readonly IPermissionService _permissions;
        private readonly IUserContext _user;

        public RoleService(IRoleRepository roles, IPermissionService permissions, IUserContext user)
        {
            _roles = roles;
            _permissions = permissions;
            _user = user;
        }

        public IList<Role> GetAll() { return _roles.GetAll(); }

        public Role Get(int roleId) { return _roles.GetById(roleId); }

        public OperationResult<int> Save(Role role)
        {
            if (role == null) return OperationResult<int>.Fail("No data received.");
            role.RoleName = string.IsNullOrWhiteSpace(role.RoleName) ? null : role.RoleName.Trim();
            role.Description = string.IsNullOrWhiteSpace(role.Description) ? null : role.Description.Trim();
            if (role.RoleName == null) return OperationResult<int>.Fail("Role name is required.", "RoleName");
            try
            {
                var id = _roles.Save(role, _user);
                _permissions.Invalidate(id);
                return OperationResult<int>.Ok(id, "Role saved.");
            }
            catch (BusinessException ex)
            {
                return OperationResult<int>.Fail(ex.Message, ex.FieldName);
            }
        }

        public IList<PermissionGrant> GetPermissionMatrix(int roleId)
        {
            return _roles.GetPermissionMatrix(roleId);
        }

        public OperationResult SavePermissions(int roleId, IEnumerable<int> permissionIds)
        {
            try
            {
                _roles.SavePermissions(roleId, permissionIds, _user);
                _permissions.Invalidate(roleId);
                return OperationResult.Ok("Permissions saved. Users of this role see the change on their next page load.");
            }
            catch (BusinessException ex)
            {
                return OperationResult.Fail(ex.Message);
            }
        }
    }

    public class AuditService : IAuditService
    {
        private readonly IAuditLogRepository _audit;

        public AuditService(IAuditLogRepository audit)
        {
            _audit = audit;
        }

        public PagedResult<AuditLogEntry> Search(AuditSearchCriteria criteria)
        {
            return _audit.Search(criteria);
        }

        public IList<string> TableNames()
        {
            return _audit.GetTableNames();
        }
    }
}
