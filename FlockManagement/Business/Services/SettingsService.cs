using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.Caching;
using FlockManagement.Business.Interfaces;
using FlockManagement.Common.Constants;
using FlockManagement.Common.Entities;
using FlockManagement.Common.Interfaces;
using FlockManagement.Data.Interfaces;

namespace FlockManagement.Business.Services
{
    /// <summary>System settings and status definitions, cached for 10 minutes.</summary>
    public class SettingsService : ISettingsService
    {
        private const string SettingsKey = "cfg:settings";
        private const string StatusKey = "cfg:statuses";
        private static readonly MemoryCache Cache = MemoryCache.Default;
        private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(10);

        private readonly IConfigRepository _config;

        public SettingsService(IConfigRepository config)
        {
            _config = config;
        }

        private IDictionary<string, string> Settings
        {
            get
            {
                var s = Cache.Get(SettingsKey) as IDictionary<string, string>;
                if (s != null) return s;
                s = _config.GetSettings();
                Cache.Set(SettingsKey, s, DateTimeOffset.UtcNow.Add(Lifetime));
                return s;
            }
        }

        public int GetInt(string key, int defaultValue)
        {
            string raw;
            int value;
            return Settings.TryGetValue(key, out raw) && int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out value)
                ? value : defaultValue;
        }

        public decimal GetDecimal(string key, decimal defaultValue)
        {
            string raw;
            decimal value;
            return Settings.TryGetValue(key, out raw) && decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out value)
                ? value : defaultValue;
        }

        public string GetString(string key, string defaultValue)
        {
            string raw;
            return Settings.TryGetValue(key, out raw) && !string.IsNullOrEmpty(raw) ? raw : defaultValue;
        }

        public IList<StatusDefinition> Statuses()
        {
            var s = Cache.Get(StatusKey) as IList<StatusDefinition>;
            if (s != null) return s;
            s = _config.GetStatuses();
            Cache.Set(StatusKey, s, DateTimeOffset.UtcNow.Add(Lifetime));
            return s;
        }

        public StatusDefinition Status(string statusCode)
        {
            var found = Statuses().FirstOrDefault(x => string.Equals(x.StatusCode, statusCode, StringComparison.OrdinalIgnoreCase));
            return found ?? new StatusDefinition
            {
                StatusCode = statusCode ?? StatusCodes.NoData,
                StatusName = statusCode ?? "No Data",
                BadgeCssClass = "status-nodata",
                ColorHex = "#6c757d"
            };
        }

        public void Refresh()
        {
            Cache.Remove(SettingsKey);
            Cache.Remove(StatusKey);
        }
    }

    public class DashboardService : IDashboardService
    {
        private readonly IConfigRepository _config;
        private readonly IUserContext _user;

        public DashboardService(IConfigRepository config, IUserContext user)
        {
            _config = config;
            _user = user;
        }

        public DashboardSummary GetSummary()
        {
            return _config.GetDashboardSummary(_user.FarmID);
        }
    }
}
