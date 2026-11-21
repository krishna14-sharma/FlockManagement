using System;
using System.Collections.Generic;
using FlockManagement.Common.Entities;
using FlockManagement.Common.Helpers;

namespace FlockManagement.Business.Interfaces
{
    public interface IMasterService<T> where T : class
    {
        PagedResult<T> Search(PageRequest request);
        T Get(int id);
        OperationResult<int> Save(T entity);
        OperationResult SetActive(int id, bool isActive);
        IList<LookupItem> Lookup(int? parentId = null);
    }

    public interface IFarmService : IMasterService<Farm> { }
    public interface IHouseService : IMasterService<House> { }
    public interface IBirdTypeService : IMasterService<BirdType> { }
    public interface IBreedService : IMasterService<Breed> { }
    public interface ILotService : IMasterService<Lot> { }
    public interface IFeedTypeService : IMasterService<FeedType> { }

    public interface IFlockService
    {
        PagedResult<Flock> Search(FlockSearchCriteria criteria);
        Flock Get(int flockId);
        OperationResult<int> Save(Flock flock);
        OperationResult SetStatus(int flockId, string status, DateTime? depletionDate);
        IList<LookupItem> ActiveLookup(int? farmId, int? houseId);
        IList<string> Statuses { get; }
    }

    public interface IAuthService
    {
        LoginResult Login(string userName, string password, string ipAddress);
        void RecordLogout(int userId, string userName, string ipAddress);
        OperationResult ChangePassword(int userId, string currentPassword, string newPassword);
        string ValidatePasswordPolicy(string password);
    }

    public interface IPermissionService
    {
        ISet<string> GetPermissionCodes(int roleId);
        IList<MenuItem> GetMenu(int roleId);
        void Invalidate(int roleId);
    }

    public interface IUserService
    {
        PagedResult<User> Search(string search, int? roleId, int pageNo, int pageSize);
        User Get(int userId);
        OperationResult<int> Save(User user, string newPassword);
        OperationResult ResetPassword(int userId, string newPassword);
        IList<LookupItem> RoleLookup();
    }

    public interface IRoleService
    {
        IList<Role> GetAll();
        Role Get(int roleId);
        OperationResult<int> Save(Role role);
        IList<PermissionGrant> GetPermissionMatrix(int roleId);
        OperationResult SavePermissions(int roleId, IEnumerable<int> permissionIds);
    }

    public interface IAuditService
    {
        PagedResult<AuditLogEntry> Search(AuditSearchCriteria criteria);
        IList<string> TableNames();
    }

    public interface ISettingsService
    {
        int GetInt(string key, int defaultValue);
        decimal GetDecimal(string key, decimal defaultValue);
        string GetString(string key, string defaultValue);
        IList<StatusDefinition> Statuses();
        StatusDefinition Status(string statusCode);
        void Refresh();
    }

    public interface IDashboardService
    {
        DashboardSummary GetSummary();
    }
}
