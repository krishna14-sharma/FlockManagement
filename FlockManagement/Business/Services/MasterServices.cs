using System;
using System.Collections.Generic;
using FlockManagement.Business.Interfaces;
using FlockManagement.Common.Entities;
using FlockManagement.Common.Helpers;
using FlockManagement.Common.Interfaces;
using FlockManagement.Data.Interfaces;

namespace FlockManagement.Business.Services
{
    /// <summary>
    /// Base for master services: normalises input, applies business validation, applies the
    /// user's farm restriction and turns rule violations into an OperationResult.
    /// </summary>
    public abstract class MasterServiceBase<T> : IMasterService<T> where T : class
    {
        protected readonly IMasterRepository<T> Repository;
        protected readonly IUserContext User;

        protected MasterServiceBase(IMasterRepository<T> repository, IUserContext user)
        {
            if (repository == null) throw new ArgumentNullException("repository");
            if (user == null) throw new ArgumentNullException("user");
            Repository = repository;
            User = user;
        }

        /// <summary>Trim / upper-case and enforce rules. Throw BusinessException on violation.</summary>
        protected virtual void Validate(T entity) { }

        /// <summary>Return false when the user may not touch this record (farm restriction).</summary>
        protected virtual bool CanAccess(T entity) { return true; }

        public virtual PagedResult<T> Search(PageRequest request)
        {
            request = request ?? new PageRequest();
            request.RestrictFarmID = User.FarmID;
            return Repository.Search(request);
        }

        public virtual T Get(int id)
        {
            var entity = Repository.GetById(id);
            return entity != null && CanAccess(entity) ? entity : null;
        }

        public virtual OperationResult<int> Save(T entity)
        {
            if (entity == null) return OperationResult<int>.Fail("No data received.");
            try
            {
                Validate(entity);
                if (!CanAccess(entity)) return OperationResult<int>.Fail("You do not have access to this farm.");
                var id = Repository.Save(entity, User);
                return OperationResult<int>.Ok(id, "Saved successfully.");
            }
            catch (BusinessException ex)
            {
                return OperationResult<int>.Fail(ex.Message, ex.FieldName);
            }
        }

        public virtual OperationResult SetActive(int id, bool isActive)
        {
            try
            {
                var entity = Get(id);
                if (entity == null) return OperationResult.Fail("Record not found.");
                Repository.SetActive(id, isActive, User);
                return OperationResult.Ok(isActive ? "Record activated." : "Record deactivated.");
            }
            catch (BusinessException ex)
            {
                return OperationResult.Fail(ex.Message);
            }
        }

        public virtual IList<LookupItem> Lookup(int? parentId = null)
        {
            return Repository.GetLookup(User.FarmID, parentId);
        }

        protected static string Clean(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        protected static string Code(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant();
        }

        protected static void Require(string value, string field, string label)
        {
            if (string.IsNullOrWhiteSpace(value)) throw new BusinessException(field, label + " is required.");
        }
    }

    public class FarmService : MasterServiceBase<Farm>, IFarmService
    {
        public FarmService(IFarmRepository repository, IUserContext user) : base(repository, user) { }

        protected override bool CanAccess(Farm e)
        {
            return !User.FarmID.HasValue || e.FarmID == 0 || e.FarmID == User.FarmID.Value;
        }

        protected override void Validate(Farm e)
        {
            e.FarmCode = Code(e.FarmCode);
            e.FarmName = Clean(e.FarmName);
            e.Location = Clean(e.Location);
            e.Address = Clean(e.Address);
            e.ContactPerson = Clean(e.ContactPerson);
            e.ContactNumber = Clean(e.ContactNumber);
            Require(e.FarmCode, "FarmCode", "Farm code");
            Require(e.FarmName, "FarmName", "Farm name");
            if (User.FarmID.HasValue && e.FarmID == 0)
                throw new BusinessException("Users restricted to one farm cannot create farms.");
        }

        public override OperationResult SetActive(int id, bool isActive)
        {
            if (!isActive)
            {
                var farm = Get(id);
                if (farm != null && farm.ActiveFlockCount > 0)
                    return OperationResult.Fail("This farm has active flocks and cannot be deactivated.");
            }
            return base.SetActive(id, isActive);
        }
    }

    public class HouseService : MasterServiceBase<House>, IHouseService
    {
        private readonly IHouseRepository _houses;

        public HouseService(IHouseRepository repository, IUserContext user) : base(repository, user)
        {
            _houses = repository;
        }

        protected override bool CanAccess(House e)
        {
            return !User.FarmID.HasValue || e.FarmID == User.FarmID.Value;
        }

        protected override void Validate(House e)
        {
            e.HouseCode = Code(e.HouseCode);
            e.HouseName = Clean(e.HouseName);
            e.HouseType = Clean(e.HouseType);
            if (e.FarmID <= 0) throw new BusinessException("FarmID", "Farm is required.");
            Require(e.HouseCode, "HouseCode", "House code");
            Require(e.HouseName, "HouseName", "House name");
            if (e.Capacity < 0) throw new BusinessException("Capacity", "Capacity cannot be negative.");
            if (e.AreaSqFt.HasValue && e.AreaSqFt.Value < 0) throw new BusinessException("AreaSqFt", "Area cannot be negative.");

            if (e.HouseID > 0)
            {
                var birds = _houses.GetCurrentBirds(e.HouseID);
                if (e.Capacity > 0 && e.Capacity < birds)
                    throw new BusinessException("Capacity", string.Format("Capacity cannot be lower than the {0:N0} birds currently housed.", birds));
                var existing = _houses.GetById(e.HouseID);
                if (existing != null && existing.FarmID != e.FarmID && birds > 0)
                    throw new BusinessException("FarmID", "A house with active flocks cannot be moved to another farm.");
            }
        }

        public override OperationResult SetActive(int id, bool isActive)
        {
            if (!isActive && _houses.GetCurrentBirds(id) > 0)
                return OperationResult.Fail("This house holds active flocks and cannot be deactivated.");
            return base.SetActive(id, isActive);
        }
    }

    public class BirdTypeService : MasterServiceBase<BirdType>, IBirdTypeService
    {
        public BirdTypeService(IBirdTypeRepository repository, IUserContext user) : base(repository, user) { }

        protected override void Validate(BirdType e)
        {
            e.BirdTypeCode = Code(e.BirdTypeCode);
            e.BirdTypeName = Clean(e.BirdTypeName);
            e.Description = Clean(e.Description);
            Require(e.BirdTypeCode, "BirdTypeCode", "Bird type code");
            Require(e.BirdTypeName, "BirdTypeName", "Bird type name");
        }
    }

    public class BreedService : MasterServiceBase<Breed>, IBreedService
    {
        public BreedService(IBreedRepository repository, IUserContext user) : base(repository, user) { }

        protected override void Validate(Breed e)
        {
            e.BreedCode = Code(e.BreedCode);
            e.BreedName = Clean(e.BreedName);
            e.Supplier = Clean(e.Supplier);
            e.Description = Clean(e.Description);
            Require(e.BreedCode, "BreedCode", "Breed code");
            Require(e.BreedName, "BreedName", "Breed name");
        }
    }

    public class LotService : MasterServiceBase<Lot>, ILotService
    {
        public LotService(ILotRepository repository, IUserContext user) : base(repository, user) { }

        protected override bool CanAccess(Lot e)
        {
            return !User.FarmID.HasValue || !e.FarmID.HasValue || e.FarmID.Value == User.FarmID.Value;
        }

        protected override void Validate(Lot e)
        {
            e.LotNumber = Code(e.LotNumber);
            e.LotName = Clean(e.LotName);
            e.Description = Clean(e.Description);
            Require(e.LotNumber, "LotNumber", "Lot number");
            if (User.FarmID.HasValue) e.FarmID = User.FarmID;
        }
    }

    public class FeedTypeService : MasterServiceBase<FeedType>, IFeedTypeService
    {
        private static readonly HashSet<string> Sexes = new HashSet<string>(StringComparer.Ordinal) { "F", "M", "B" };

        public FeedTypeService(IFeedTypeRepository repository, IUserContext user) : base(repository, user) { }

        protected override void Validate(FeedType e)
        {
            e.FeedTypeCode = Code(e.FeedTypeCode);
            e.FeedTypeName = Clean(e.FeedTypeName);
            e.Description = Clean(e.Description);
            e.TargetSex = Code(e.TargetSex) ?? "B";
            Require(e.FeedTypeCode, "FeedTypeCode", "Feed type code");
            Require(e.FeedTypeName, "FeedTypeName", "Feed type name");
            if (!Sexes.Contains(e.TargetSex)) throw new BusinessException("TargetSex", "Target sex must be Female, Male or Both.");
            if (e.ApplicableFromWeek.HasValue && (e.ApplicableFromWeek < 0 || e.ApplicableFromWeek > 120))
                throw new BusinessException("ApplicableFromWeek", "From week must be between 0 and 120.");
            if (e.ApplicableToWeek.HasValue && (e.ApplicableToWeek < 0 || e.ApplicableToWeek > 120))
                throw new BusinessException("ApplicableToWeek", "To week must be between 0 and 120.");
            if (e.ApplicableFromWeek.HasValue && e.ApplicableToWeek.HasValue && e.ApplicableFromWeek > e.ApplicableToWeek)
                throw new BusinessException("ApplicableToWeek", "To week must be greater than or equal to from week.");
        }
    }
}
