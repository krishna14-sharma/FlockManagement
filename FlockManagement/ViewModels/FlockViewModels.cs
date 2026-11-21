using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Web.Mvc;
using FlockManagement.Common.Entities;
using FlockManagement.Common.Helpers;

namespace FlockManagement.Web.ViewModels
{
    public class FlockListViewModel
    {
        public string Search { get; set; }
        public int? FarmID { get; set; }
        public int? HouseID { get; set; }
        public int? BirdTypeID { get; set; }
        public int? LotID { get; set; }
        public string Status { get; set; }
        public PagedResult<Flock> Result { get; set; }
        public bool CanEdit { get; set; }

        public IEnumerable<SelectListItem> Farms { get; set; }
        public IEnumerable<SelectListItem> Houses { get; set; }
        public IEnumerable<SelectListItem> BirdTypes { get; set; }
        public IEnumerable<SelectListItem> Lots { get; set; }
        public IEnumerable<SelectListItem> Statuses { get; set; }
    }

    public class FlockEditViewModel : IValidatableObject
    {
        public int FlockID { get; set; }

        [Required, StringLength(30), Display(Name = "Flock Number")]
        [RegularExpression(@"^[A-Za-z0-9\-_/]+$", ErrorMessage = "Use letters, digits, '-', '_' or '/' only.")]
        public string FlockNumber { get; set; }

        [Display(Name = "Lot Number"), Range(1, int.MaxValue, ErrorMessage = "Lot is required.")]
        public int LotID { get; set; }

        [Display(Name = "Bird Type"), Range(1, int.MaxValue, ErrorMessage = "Bird type is required.")]
        public int BirdTypeID { get; set; }

        [Display(Name = "Breed"), Range(1, int.MaxValue, ErrorMessage = "Breed is required.")]
        public int BreedID { get; set; }

        [Display(Name = "Farm"), Range(1, int.MaxValue, ErrorMessage = "Farm is required.")]
        public int FarmID { get; set; }

        [Display(Name = "House / Shed"), Range(1, int.MaxValue, ErrorMessage = "House is required.")]
        public int HouseID { get; set; }

        [Required, DataType(DataType.Date), Display(Name = "Hatch Date")]
        [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd}", ApplyFormatInEditMode = true)]
        public DateTime? HatchDate { get; set; }

        [Required, DataType(DataType.Date), Display(Name = "Placement Date")]
        [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd}", ApplyFormatInEditMode = true)]
        public DateTime? PlacementDate { get; set; }

        [Range(0, 1000000, ErrorMessage = "Male count cannot be negative."), Display(Name = "Male Count")]
        public int MaleCount { get; set; }

        [Range(0, 1000000, ErrorMessage = "Female count cannot be negative."), Display(Name = "Female Count")]
        public int FemaleCount { get; set; }

        [Display(Name = "Total Birds")]
        public int TotalBirdCount { get { return MaleCount + FemaleCount; } }

        [Range(typeof(decimal), "0.1", "5000", ErrorMessage = "Enter a weight between 0.1 and 5000 g."), Display(Name = "Avg. Weight at Placement (g)")]
        public decimal? ChickAvgWeight { get; set; }

        [StringLength(100), Display(Name = "Source / Hatchery")]
        public string Source { get; set; }

        [StringLength(500), DataType(DataType.MultilineText)]
        public string Remarks { get; set; }

        /// <summary>Base64 rowversion for optimistic concurrency.</summary>
        public string RowVersion { get; set; }

        public bool HasWeeklyData { get; set; }
        public string Status { get; set; }

        public IEnumerable<SelectListItem> Farms { get; set; }
        public IEnumerable<SelectListItem> Houses { get; set; }
        public IEnumerable<SelectListItem> BirdTypes { get; set; }
        public IEnumerable<SelectListItem> Breeds { get; set; }
        public IEnumerable<SelectListItem> Lots { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (MaleCount + FemaleCount <= 0)
                yield return new ValidationResult("The flock must contain at least one bird.", new[] { "FemaleCount" });
            if (HatchDate.HasValue && PlacementDate.HasValue && HatchDate.Value.Date > PlacementDate.Value.Date)
                yield return new ValidationResult("Hatch date cannot be after placement date.", new[] { "HatchDate" });
            if (PlacementDate.HasValue && PlacementDate.Value.Date > DateTime.Today.AddDays(1))
                yield return new ValidationResult("Placement date cannot be in the future.", new[] { "PlacementDate" });
        }

        public static FlockEditViewModel From(Flock f)
        {
            return new FlockEditViewModel
            {
                FlockID = f.FlockID,
                FlockNumber = f.FlockNumber,
                LotID = f.LotID,
                BirdTypeID = f.BirdTypeID,
                BreedID = f.BreedID,
                FarmID = f.FarmID,
                HouseID = f.HouseID,
                HatchDate = f.HatchDate,
                PlacementDate = f.PlacementDate,
                MaleCount = f.MaleCount,
                FemaleCount = f.FemaleCount,
                Source = f.Source,
                Remarks = f.Remarks,
                RowVersion = f.RowVer != null ? Convert.ToBase64String(f.RowVer) : null,
                HasWeeklyData = f.HasWeeklyData,
                Status = f.Status
            };
        }

        public Flock ToEntity()
        {
            byte[] rowVer = null;
            if (!string.IsNullOrEmpty(RowVersion))
            {
                try { rowVer = Convert.FromBase64String(RowVersion); }
                catch (FormatException) { rowVer = null; }
            }

            return new Flock
            {
                FlockID = FlockID,
                FlockNumber = FlockNumber,
                LotID = LotID,
                BirdTypeID = BirdTypeID,
                BreedID = BreedID,
                FarmID = FarmID,
                HouseID = HouseID,
                HatchDate = HatchDate ?? DateTime.Today,
                PlacementDate = PlacementDate ?? DateTime.Today,
                MaleCount = MaleCount,
                FemaleCount = FemaleCount,
                ChickAvgWeight = ChickAvgWeight,
                Source = Source,
                Remarks = Remarks,
                RowVer = rowVer
            };
        }
    }

    public class FlockStatusViewModel
    {
        public int FlockID { get; set; }

        [Required]
        public string Status { get; set; }

        [DataType(DataType.Date), Display(Name = "Depletion / Closing Date")]
        public DateTime? DepletionDate { get; set; }
    }
}
