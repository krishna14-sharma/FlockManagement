using System.Collections.Generic;
using FlockManagement.Common.Entities;
using FlockManagement.Common.Helpers;
using FlockManagement.Common.Interfaces;

namespace FlockManagement.Data.Interfaces
{
    /// <summary>Common contract for master tables with soft delete (IsActive).</summary>
    public interface IMasterRepository<T> where T : class
    {
        PagedResult<T> Search(PageRequest request);
        T GetById(int id);
        /// <summary>Insert when the key is 0, otherwise update. Writes the audit log in the same transaction.</summary>
        int Save(T entity, IUserContext user);
        void SetActive(int id, bool isActive, IUserContext user);
        IList<LookupItem> GetLookup(int? restrictFarmId = null, int? parentId = null);
    }

    public interface IFarmRepository : IMasterRepository<Farm> { }

    public interface IHouseRepository : IMasterRepository<House>
    {
        int GetCurrentBirds(int houseId);
    }

    public interface IBirdTypeRepository : IMasterRepository<BirdType> { }

    public interface IBreedRepository : IMasterRepository<Breed> { }

    public interface ILotRepository : IMasterRepository<Lot> { }

    public interface IFeedTypeRepository : IMasterRepository<FeedType> { }

    public interface IFlockRepository
    {
        PagedResult<Flock> Search(FlockSearchCriteria criteria);
        Flock GetById(int flockId);
        int Create(Flock flock, IUserContext user);
        void Update(Flock flock, IUserContext user);
        void SetStatus(int flockId, string status, System.DateTime? depletionDate, IUserContext user);
        IList<LookupItem> GetActiveLookup(int? farmId, int? houseId, int? restrictFarmId);
    }

    public interface IUserRepository
    {
        UserCredential GetForLogin(string userName);
        /// <summary>Records the attempt and returns true when the account is now locked.</summary>
        bool RecordLogin(int? userId, string userName, bool success, string ipAddress, int maxFailedAttempts, string remarks);
        string GetPasswordHash(int userId);
        void ChangePassword(int userId, string passwordHash, bool mustChange, IUserContext user);
        PagedResult<User> Search(string search, int? roleId, int pageNo, int pageSize);
        User GetById(int userId);
        int Save(User user, string passwordHash, IUserContext actionUser);
    }

    public interface IRoleRepository
    {
        IList<Role> GetAll();
        Role GetById(int roleId);
        int Save(Role role, IUserContext user);
        IList<PermissionGrant> GetPermissionMatrix(int roleId);
        void SavePermissions(int roleId, IEnumerable<int> permissionIds, IUserContext user);
        IList<string> GetPermissionCodes(int roleId);
        IList<MenuItem> GetMenu(int roleId);
        IList<LookupItem> GetLookup();
    }

    public interface IAuditLogRepository
    {
        PagedResult<AuditLogEntry> Search(AuditSearchCriteria criteria);
        IList<string> GetTableNames();
        void Write(IUserContext user, string action, string table, object recordId, string oldJson, string newJson);
    }

    public interface IConfigRepository
    {
        IDictionary<string, string> GetSettings();
        IList<StatusDefinition> GetStatuses();
        DashboardSummary GetDashboardSummary(int? restrictFarmId);
    }
}
