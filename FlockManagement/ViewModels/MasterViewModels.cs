using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Web.Mvc;
using FlockManagement.Common.Entities;
using FlockManagement.Common.Helpers;

namespace FlockManagement.Web.ViewModels
{
    public class MasterListViewModel<T>
    {
        public string Title { get; set; }
        public string Search { get; set; }
        /// <summary>active | inactive | all</summary>
        public string Status { get; set; }
        public int? ParentID { get; set; }
        public PagedResult<T> Result { get; set; }
        public bool CanEdit { get; set; }
        public IEnumerable<SelectListItem> ParentOptions { get; set; }

        public MasterListViewModel()
        {
            Result = new PagedResult<T>();
            ParentOptions = new List<SelectListItem>();
        }

        public static bool? ParseStatus(string status)
        {
            switch ((status ?? "active").ToLowerInvariant())
            {
                case "inactive": return false;
                case "all": return null;
                default: return true;
            }
        }
    }

    public abstract class MasterEditViewModel
    {
        [Display(Name = "Active")]
        public bool IsActive { get; set; }

        protected MasterEditViewModel()
        {
            IsActive = true;
        }
    }

    public class FarmViewModel : MasterEditViewModel
    {
        public int FarmID { get; set; }

        [Required, StringLength(20), Display(Name = "Farm Code")]
        [RegularExpression(@"^[A-Za-z0-9\-_]+$", ErrorMessage = "Use letters, digits, '-' or '_' only.")]
        public string FarmCode { get; set; }

        [Required, StringLength(100), Display(Name = "Farm Name")]
        public string FarmName { get; set; }

        [StringLength(100)]
        public string Location { get; set; }

        [StringLength(300), DataType(DataType.MultilineText)]
        public string Address { get; set; }

        [StringLength(100), Display(Name = "Contact Person")]
        public string ContactPerson { get; set; }

        [StringLength(30), Display(Name = "Contact Number"), RegularExpression(@"^[0-9+\-\s()]*$", ErrorMessage = "Invalid phone number.")]
        public string ContactNumber { get; set; }

        public static FarmViewModel From(Farm e)
        {
            return new FarmViewModel
            {
                FarmID = e.FarmID, FarmCode = e.FarmCode, FarmName = e.FarmName, Location = e.Location, Address = e.Address,
                ContactPerson = e.ContactPerson, ContactNumber = e.ContactNumber, IsActive = e.IsActive
            };
        }

        public Farm ToEntity()
        {
            return new Farm
            {
                FarmID = FarmID, FarmCode = FarmCode, FarmName = FarmName, Location = Location, Address = Address,
                ContactPerson = ContactPerson, ContactNumber = ContactNumber, IsActive = IsActive
            };
        }
    }

    public class HouseViewModel : MasterEditViewModel
    {
        public int HouseID { get; set; }

        [Required(ErrorMessage = "Farm is required."), Display(Name = "Farm")]
        [Range(1, int.MaxValue, ErrorMessage = "Farm is required.")]
        public int FarmID { get; set; }

        [Required, StringLength(20), Display(Name = "House Code")]
        [RegularExpression(@"^[A-Za-z0-9\-_]+$", ErrorMessage = "Use letters, digits, '-' or '_' only.")]
        public string HouseCode { get; set; }

        [Required, StringLength(100), Display(Name = "House / Shed Name")]
        public string HouseName { get; set; }

        [StringLength(50), Display(Name = "House Type")]
        public string HouseType { get; set; }

        [Range(0, 1000000, ErrorMessage = "Capacity must be between 0 and 1,000,000.")]
        [Display(Name = "Bird Capacity")]
        public int Capacity { get; set; }

        [Range(typeof(decimal), "0", "10000000", ErrorMessage = "Area cannot be negative.")]
        [Display(Name = "Area (sq ft)")]
        public decimal? AreaSqFt { get; set; }

        public IEnumerable<SelectListItem> Farms { get; set; }
        public IEnumerable<string> HouseTypes
        {
            get { return new[] { "Environment Controlled", "Curtain Sided", "Open Sided", "Slatted", "Litter" }; }
        }

        public static HouseViewModel From(House e)
        {
            return new HouseViewModel
            {
                HouseID = e.HouseID, FarmID = e.FarmID, HouseCode = e.HouseCode, HouseName = e.HouseName,
                HouseType = e.HouseType, Capacity = e.Capacity, AreaSqFt = e.AreaSqFt, IsActive = e.IsActive
            };
        }

        public House ToEntity()
        {
            return new House
            {
                HouseID = HouseID, FarmID = FarmID, HouseCode = HouseCode, HouseName = HouseName,
                HouseType = HouseType, Capacity = Capacity, AreaSqFt = AreaSqFt, IsActive = IsActive
            };
        }
    }

    public class BirdTypeViewModel : MasterEditViewModel
    {
        public int BirdTypeID { get; set; }

        [Required, StringLength(20), Display(Name = "Code")]
        [RegularExpression(@"^[A-Za-z0-9\-_]+$", ErrorMessage = "Use letters, digits, '-' or '_' only.")]
        public string BirdTypeCode { get; set; }

        [Required, StringLength(60), Display(Name = "Bird Type")]
        public string BirdTypeName { get; set; }

        [StringLength(250), DataType(DataType.MultilineText)]
        public string Description { get; set; }

        public static BirdTypeViewModel From(BirdType e)
        {
            return new BirdTypeViewModel
            {
                BirdTypeID = e.BirdTypeID, BirdTypeCode = e.BirdTypeCode, BirdTypeName = e.BirdTypeName,
                Description = e.Description, IsActive = e.IsActive
            };
        }

        public BirdType ToEntity()
        {
            return new BirdType
            {
                BirdTypeID = BirdTypeID, BirdTypeCode = BirdTypeCode, BirdTypeName = BirdTypeName,
                Description = Description, IsActive = IsActive
            };
        }
    }

    public class BreedViewModel : MasterEditViewModel
    {
        public int BreedID { get; set; }

        [Required, StringLength(20), Display(Name = "Code")]
        [RegularExpression(@"^[A-Za-z0-9\-_]+$", ErrorMessage = "Use letters, digits, '-' or '_' only.")]
        public string BreedCode { get; set; }

        [Required, StringLength(100), Display(Name = "Breed")]
        public string BreedName { get; set; }

        [StringLength(100), Display(Name = "Supplier / Genetics Company")]
        public string Supplier { get; set; }

        [StringLength(250), DataType(DataType.MultilineText)]
        public string Description { get; set; }

        public static BreedViewModel From(Breed e)
        {
            return new BreedViewModel
            {
                BreedID = e.BreedID, BreedCode = e.BreedCode, BreedName = e.BreedName, Supplier = e.Supplier,
                Description = e.Description, IsActive = e.IsActive
            };
        }

        public Breed ToEntity()
        {
            return new Breed
            {
                BreedID = BreedID, BreedCode = BreedCode, BreedName = BreedName, Supplier = Supplier,
                Description = Description, IsActive = IsActive
            };
        }
    }

    public class LotViewModel : MasterEditViewModel
    {
        public int LotID { get; set; }

        [Required, StringLength(30), Display(Name = "Lot Number")]
        [RegularExpression(@"^[A-Za-z0-9\-_/]+$", ErrorMessage = "Use letters, digits, '-', '_' or '/' only.")]
        public string LotNumber { get; set; }

        [StringLength(100), Display(Name = "Lot Name")]
        public string LotName { get; set; }

        [Display(Name = "Farm (optional)")]
        public int? FarmID { get; set; }

        [StringLength(250), DataType(DataType.MultilineText)]
        public string Description { get; set; }

        public IEnumerable<SelectListItem> Farms { get; set; }

        public static LotViewModel From(Lot e)
        {
            return new LotViewModel
            {
                LotID = e.LotID, LotNumber = e.LotNumber, LotName = e.LotName, FarmID = e.FarmID,
                Description = e.Description, IsActive = e.IsActive
            };
        }

        public Lot ToEntity()
        {
            return new Lot
            {
                LotID = LotID, LotNumber = LotNumber, LotName = LotName, FarmID = FarmID,
                Description = Description, IsActive = IsActive
            };
        }
    }

    public class FeedTypeViewModel : MasterEditViewModel
    {
        public int FeedTypeID { get; set; }

        [Required, StringLength(20), Display(Name = "Code")]
        [RegularExpression(@"^[A-Za-z0-9\-_]+$", ErrorMessage = "Use letters, digits, '-' or '_' only.")]
        public string FeedTypeCode { get; set; }

        [Required, StringLength(100), Display(Name = "Feed Type")]
        public string FeedTypeName { get; set; }

        [Required, Display(Name = "Fed To")]
        [RegularExpression("^[FMB]$", ErrorMessage = "Select Female, Male or Both.")]
        public string TargetSex { get; set; }

        [Range(0, 120), Display(Name = "From Week")]
        public int? ApplicableFromWeek { get; set; }

        [Range(0, 120), Display(Name = "To Week")]
        public int? ApplicableToWeek { get; set; }

        [StringLength(250), DataType(DataType.MultilineText)]
        public string Description { get; set; }

        public FeedTypeViewModel()
        {
            TargetSex = "B";
        }

        public static IEnumerable<SelectListItem> SexOptions(string selected)
        {
            return new[]
            {
                new SelectListItem { Value = "B", Text = "Both (Male & Female)", Selected = selected == "B" },
                new SelectListItem { Value = "F", Text = "Female", Selected = selected == "F" },
                new SelectListItem { Value = "M", Text = "Male", Selected = selected == "M" }
            };
        }

        public static string SexText(string code)
        {
            switch (code)
            {
                case "F": return "Female";
                case "M": return "Male";
                default: return "Both";
            }
        }

        public static FeedTypeViewModel From(FeedType e)
        {
            return new FeedTypeViewModel
            {
                FeedTypeID = e.FeedTypeID, FeedTypeCode = e.FeedTypeCode, FeedTypeName = e.FeedTypeName, TargetSex = e.TargetSex,
                ApplicableFromWeek = e.ApplicableFromWeek, ApplicableToWeek = e.ApplicableToWeek, Description = e.Description,
                IsActive = e.IsActive
            };
        }

        public FeedType ToEntity()
        {
            return new FeedType
            {
                FeedTypeID = FeedTypeID, FeedTypeCode = FeedTypeCode, FeedTypeName = FeedTypeName, TargetSex = TargetSex,
                ApplicableFromWeek = ApplicableFromWeek, ApplicableToWeek = ApplicableToWeek, Description = Description,
                IsActive = IsActive
            };
        }
    }
}
