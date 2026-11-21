using System;
using System.Collections.Generic;

namespace FlockManagement.Common.Helpers
{
    public class PagedResult<T>
    {
        public IList<T> Items { get; set; }
        public int TotalCount { get; set; }
        public int PageNo { get; set; }
        public int PageSize { get; set; }

        public int TotalPages
        {
            get { return PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize); }
        }

        public bool HasPrevious { get { return PageNo > 1; } }
        public bool HasNext { get { return PageNo < TotalPages; } }

        public PagedResult()
        {
            Items = new List<T>();
            PageNo = 1;
            PageSize = 25;
        }

        public PagedResult(IList<T> items, int totalCount, int pageNo, int pageSize)
        {
            Items = items ?? new List<T>();
            TotalCount = totalCount;
            PageNo = pageNo < 1 ? 1 : pageNo;
            PageSize = pageSize;
        }
    }

    /// <summary>Normalised paging/search request.</summary>
    public class PageRequest
    {
        public const int MaxPageSize = 200;

        public string Search { get; set; }
        public bool? IsActive { get; set; }
        /// <summary>Optional parent filter (e.g. FarmID for houses and lots).</summary>
        public int? ParentID { get; set; }
        /// <summary>Farm restriction of the current user (null = all farms).</summary>
        public int? RestrictFarmID { get; set; }
        public int PageNo { get; set; }
        public int PageSize { get; set; }

        public PageRequest()
        {
            PageNo = 1;
            PageSize = 25;
        }

        public PageRequest Normalize()
        {
            if (PageNo < 1) PageNo = 1;
            if (PageSize < 1) PageSize = 25;
            if (PageSize > MaxPageSize) PageSize = MaxPageSize;
            Search = string.IsNullOrWhiteSpace(Search) ? null : Search.Trim();
            return this;
        }
    }
}
