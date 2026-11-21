using System;
using System.Collections.Generic;
using FlockManagement.Business.Interfaces;
using FlockManagement.Common.Entities;
using FlockManagement.Common.Enums;
using FlockManagement.Common.Helpers;
using FlockManagement.Common.Interfaces;
using FlockManagement.Data.Interfaces;

namespace FlockManagement.Business.Services
{
    public class FlockService : IFlockService
    {
        private static readonly IList<string> StatusList = Enum.GetNames(typeof(FlockStatus));

        private readonly IFlockRepository _flocks;
        private readonly IHouseRepository _houses;
        private readonly IUserContext _user;

        public FlockService(IFlockRepository flocks, IHouseRepository houses, IUserContext user)
        {
            if (flocks == null) throw new ArgumentNullException("flocks");
            if (houses == null) throw new ArgumentNullException("houses");
            if (user == null) throw new ArgumentNullException("user");
            _flocks = flocks;
            _houses = houses;
            _user = user;
        }

        public IList<string> Statuses { get { return StatusList; } }

        public PagedResult<Flock> Search(FlockSearchCriteria criteria)
        {
            criteria = criteria ?? new FlockSearchCriteria();
            criteria.RestrictFarmID = _user.FarmID;
            return _flocks.Search(criteria);
        }

        public Flock Get(int flockId)
        {
            var flock = _flocks.GetById(flockId);
            if (flock == null) return null;
            if (_user.FarmID.HasValue && flock.FarmID != _user.FarmID.Value) return null;
            return flock;
        }

        public OperationResult<int> Save(Flock flock)
        {
            if (flock == null) return OperationResult<int>.Fail("No data received.");
            try
            {
                Validate(flock);
                if (flock.FlockID == 0)
                {
                    var id = _flocks.Create(flock, _user);
                    return OperationResult<int>.Ok(id, "Flock " + flock.FlockNumber + " created and placement recorded.");
                }

                var existing = Get(flock.FlockID);
                if (existing == null) return OperationResult<int>.Fail("Flock not found.");
                _flocks.Update(flock, _user);
                return OperationResult<int>.Ok(flock.FlockID, "Flock " + flock.FlockNumber + " updated.");
            }
            catch (BusinessException ex)
            {
                return OperationResult<int>.Fail(ex.Message, ex.FieldName);
            }
        }

        public OperationResult SetStatus(int flockId, string status, DateTime? depletionDate)
        {
            FlockStatus parsed;
            if (string.IsNullOrWhiteSpace(status) || !Enum.TryParse(status, false, out parsed) || !Enum.IsDefined(typeof(FlockStatus), parsed))
                return OperationResult.Fail("Invalid flock status.");

            var flock = Get(flockId);
            if (flock == null) return OperationResult.Fail("Flock not found.");
            if (depletionDate.HasValue && depletionDate.Value.Date < flock.PlacementDate.Date)
                return OperationResult.Fail("Depletion date cannot be before placement date.");
            if (depletionDate.HasValue && depletionDate.Value.Date > DateTime.Today)
                return OperationResult.Fail("Depletion date cannot be in the future.");

            try
            {
                _flocks.SetStatus(flockId, parsed.ToString(), depletionDate, _user);
                return OperationResult.Ok("Flock status changed to " + parsed + ".");
            }
            catch (BusinessException ex)
            {
                return OperationResult.Fail(ex.Message);
            }
        }

        public IList<LookupItem> ActiveLookup(int? farmId, int? houseId)
        {
            return _flocks.GetActiveLookup(farmId, houseId, _user.FarmID);
        }

        private void Validate(Flock f)
        {
            f.FlockNumber = string.IsNullOrWhiteSpace(f.FlockNumber) ? null : f.FlockNumber.Trim().ToUpperInvariant();
            f.Source = string.IsNullOrWhiteSpace(f.Source) ? null : f.Source.Trim();
            f.Remarks = string.IsNullOrWhiteSpace(f.Remarks) ? null : f.Remarks.Trim();

            if (f.FlockNumber == null) throw new BusinessException("FlockNumber", "Flock number is required.");
            if (f.FarmID <= 0) throw new BusinessException("FarmID", "Farm is required.");
            if (f.HouseID <= 0) throw new BusinessException("HouseID", "House is required.");
            if (f.LotID <= 0) throw new BusinessException("LotID", "Lot is required.");
            if (f.BirdTypeID <= 0) throw new BusinessException("BirdTypeID", "Bird type is required.");
            if (f.BreedID <= 0) throw new BusinessException("BreedID", "Breed is required.");
            if (f.MaleCount < 0) throw new BusinessException("MaleCount", "Male count cannot be negative.");
            if (f.FemaleCount < 0) throw new BusinessException("FemaleCount", "Female count cannot be negative.");
            if (f.MaleCount + f.FemaleCount <= 0) throw new BusinessException("FemaleCount", "The flock must contain at least one bird.");
            if (f.HatchDate.Date > f.PlacementDate.Date) throw new BusinessException("HatchDate", "Hatch date cannot be after placement date.");
            if (f.PlacementDate.Date > DateTime.Today.AddDays(1)) throw new BusinessException("PlacementDate", "Placement date cannot be in the future.");
            if ((f.PlacementDate.Date - f.HatchDate.Date).TotalDays > 7 * 30)
                throw new BusinessException("HatchDate", "Hatch date is more than 30 weeks before placement - please check the dates.");
            if (f.ChickAvgWeight.HasValue && (f.ChickAvgWeight.Value <= 0 || f.ChickAvgWeight.Value > 5000))
                throw new BusinessException("ChickAvgWeight", "Average weight at placement must be between 0 and 5000 g.");

            if (_user.FarmID.HasValue && f.FarmID != _user.FarmID.Value)
                throw new BusinessException("FarmID", "You can only manage flocks of your assigned farm.");

            var house = _houses.GetById(f.HouseID);
            if (house == null || house.FarmID != f.FarmID)
                throw new BusinessException("HouseID", "Selected house does not belong to the selected farm.");
        }
    }
}
