using System.Configuration;
using System.Web.Mvc;
using System.Web.Routing;
using FlockManagement.Business.Interfaces;
using FlockManagement.Business.Services;
using FlockManagement.Common.Interfaces;
using FlockManagement.Data.Infrastructure;
using FlockManagement.Data.Interfaces;
using FlockManagement.Data.Repositories;
using FlockManagement.Web.Infrastructure;

namespace FlockManagement.Web
{
    public static class RouteConfig
    {
        public static void RegisterRoutes(RouteCollection routes)
        {
            routes.IgnoreRoute("{resource}.axd/{*pathInfo}");
            routes.LowercaseUrls = true;
            routes.MapMvcAttributeRoutes();

            routes.MapRoute(
                name: "Default",
                url: "{controller}/{action}/{id}",
                defaults: new { controller = "Home", action = "Index", id = UrlParameter.Optional });
        }
    }

    public static class FilterConfig
    {
        public static void RegisterGlobalFilters(GlobalFilterCollection filters)
        {
            filters.Add(new HandleErrorAttribute());
            filters.Add(new MustChangePasswordFilter());
        }
    }

    /// <summary>
    /// Composition root. Every layer is wired here; the same services can later be exposed
    /// through ASP.NET Web API controllers for the mobile app without changes.
    /// </summary>
    public static class DependencyConfig
    {
        public static SimpleDependencyResolver Container { get; private set; }

        public static void Register()
        {
            var cs = ConfigurationManager.ConnectionStrings["FlockDb"];
            if (cs == null || string.IsNullOrWhiteSpace(cs.ConnectionString))
                throw new ConfigurationErrorsException("Connection string 'FlockDb' is missing in web.config.");

            int timeout;
            if (!int.TryParse(ConfigurationManager.AppSettings["SqlCommandTimeoutSeconds"], out timeout) || timeout <= 0)
                timeout = 60;

            var container = new SimpleDependencyResolver();

            // Data
            container.Singleton<IDb>(new SqlDb(cs.ConnectionString, timeout));
            container.Register<IUserContext>(r => new HttpUserContext());
            container.Register<IFarmRepository, FarmRepository>();
            container.Register<IHouseRepository, HouseRepository>();
            container.Register<IBirdTypeRepository, BirdTypeRepository>();
            container.Register<IBreedRepository, BreedRepository>();
            container.Register<ILotRepository, LotRepository>();
            container.Register<IFeedTypeRepository, FeedTypeRepository>();
            container.Register<IFlockRepository, FlockRepository>();
            container.Register<IUserRepository, UserRepository>();
            container.Register<IRoleRepository, RoleRepository>();
            container.Register<IAuditLogRepository, AuditLogRepository>();
            container.Register<IConfigRepository, ConfigRepository>();
            // Phases 2-5
            container.Register<IParameterRepository, ParameterRepository>();
            container.Register<IStandardRepository, StandardRepository>();
            container.Register<IMortalityReasonRepository, MortalityReasonRepository>();
            container.Register<IFeedFormulaRepository, FeedFormulaRepository>();
            container.Register<IMovementRepository, MovementRepository>();
            container.Register<IWeeklyRepository, WeeklyRepository>();
            container.Register<IHatchResultRepository, HatchResultRepository>();
            container.Register<IAnalysisRepository, AnalysisRepository>();
            container.Register<IAlertRepository, AlertRepository>();
            container.Register<IReportRepository, ReportRepository>();
            container.Register<IConfigurationRepository, ConfigurationRepository>();

            // Business
            container.Register<IFarmService, FarmService>();
            container.Register<IHouseService, HouseService>();
            container.Register<IBirdTypeService, BirdTypeService>();
            container.Register<IBreedService, BreedService>();
            container.Register<ILotService, LotService>();
            container.Register<IFeedTypeService, FeedTypeService>();
            container.Register<IFlockService, FlockService>();
            container.Register<IAuthService, AuthService>();
            container.Register<IPermissionService, PermissionService>();
            container.Register<IUserService, UserService>();
            container.Register<IRoleService, RoleService>();
            container.Register<IAuditService, AuditService>();
            container.Register<ISettingsService, SettingsService>();
            container.Register<IDashboardService, DashboardService>();
            // Phases 2-5
            container.Register<IStandardService, StandardService>();
            container.Register<IMortalityReasonService, MortalityReasonService>();
            container.Register<IFeedFormulaService, FeedFormulaService>();
            container.Register<IMovementService, MovementService>();
            container.Register<IWeeklyService, WeeklyService>();
            container.Register<IHatchResultService, HatchResultService>();
            container.Register<IAnalysisService, AnalysisService>();
            container.Register<IAlertService, AlertService>();
            container.Register<IReportService, ReportService>();
            container.Register<IConfigurationService, ConfigurationService>();

            Container = container;
            DependencyResolver.SetResolver(container);
        }
    }
}
