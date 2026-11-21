using System;

namespace FlockManagement.Common.Entities
{
    /// <summary>Audit columns shared by master tables.</summary>
    public abstract class AuditableEntity
    {
        public bool IsActive { get; set; }
        public int? CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public int? ModifiedBy { get; set; }
        public DateTime? ModifiedDate { get; set; }

        protected AuditableEntity()
        {
            IsActive = true;
        }
    }

    public class Farm : AuditableEntity
    {
        public int FarmID { get; set; }
        public string FarmCode { get; set; }
        public string FarmName { get; set; }
        public string Location { get; set; }
        public string Address { get; set; }
        public string ContactPerson { get; set; }
        public string ContactNumber { get; set; }
        public int HouseCount { get; set; }
        public int ActiveFlockCount { get; set; }
    }

    public class House : AuditableEntity
    {
        public int HouseID { get; set; }
        public int FarmID { get; set; }
        public string FarmName { get; set; }
        public string HouseCode { get; set; }
        public string HouseName { get; set; }
        public string HouseType { get; set; }
        public int Capacity { get; set; }
        public decimal? AreaSqFt { get; set; }
        public int CurrentBirds { get; set; }
    }

    public class BirdType : AuditableEntity
    {
        public int BirdTypeID { get; set; }
        public string BirdTypeCode { get; set; }
        public string BirdTypeName { get; set; }
        public string Description { get; set; }
    }

    public class Breed : AuditableEntity
    {
        public int BreedID { get; set; }
        public string BreedCode { get; set; }
        public string BreedName { get; set; }
        public string Supplier { get; set; }
        public string Description { get; set; }
    }

    public class Lot : AuditableEntity
    {
        public int LotID { get; set; }
        public string LotNumber { get; set; }
        public string LotName { get; set; }
        public int? FarmID { get; set; }
        public string FarmName { get; set; }
        public string Description { get; set; }
    }

    public class FeedType : AuditableEntity
    {
        public int FeedTypeID { get; set; }
        public string FeedTypeCode { get; set; }
        public string FeedTypeName { get; set; }
        /// <summary>F = female, M = male, B = both.</summary>
        public string TargetSex { get; set; }
        public int? ApplicableFromWeek { get; set; }
        public int? ApplicableToWeek { get; set; }
        public string Description { get; set; }
    }

    /// <summary>Generic id/text pair for dropdowns.</summary>
    public class LookupItem
    {
        public int Id { get; set; }
        public string Text { get; set; }
        public int? ParentId { get; set; }

        public LookupItem() { }

        public LookupItem(int id, string text, int? parentId = null)
        {
            Id = id;
            Text = text;
            ParentId = parentId;
        }
    }
}
