using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using FlockManagement.Business.Interfaces;
using FlockManagement.Common.Constants;
using FlockManagement.Common.Entities;
using FlockManagement.Web.Infrastructure;
using FlockManagement.Web.ViewModels;

namespace FlockManagement.Web.Controllers
{
    [PermissionAuthorize(PermissionCodes.UserView)]
    public class UserController : BaseController
    {
        private readonly IUserService _users;
        private readonly IFarmService _farms;

        public UserController(IUserService users, IFarmService farms)
        {
            _users = users;
            _farms = farms;
        }

        public ActionResult Index(string search, int? roleId, int page = 1)
        {
            var model = new UserListViewModel
            {
                Search = search,
                RoleID = roleId,
                Result = _users.Search(search, roleId, page, 25),
                Roles = SelectLists.From(_users.RoleLookup(), roleId, "All roles"),
                CanEdit = Can(PermissionCodes.UserEdit)
            };
            return View(model);
        }

        [PermissionAuthorize(PermissionCodes.UserEdit)]
        public ActionResult Create()
        {
            var model = new UserEditViewModel();
            Prepare(model);
            return View("Edit", model);
        }

        [PermissionAuthorize(PermissionCodes.UserEdit)]
        public ActionResult Edit(int id)
        {
            var user = _users.Get(id);
            if (user == null) return NotFoundView();
            var model = UserEditViewModel.From(user);
            Prepare(model);
            return View("Edit", model);
        }

        [HttpPost, ValidateAntiForgeryToken, PermissionAuthorize(PermissionCodes.UserEdit)]
        public ActionResult Save(UserEditViewModel model)
        {
            if (!ModelState.IsValid)
            {
                Prepare(model);
                return View("Edit", model);
            }
            var result = _users.Save(model.ToEntity(), model.UserID == 0 ? model.Password : null);
            if (!result.Success)
            {
                AddErrors(result);
                Prepare(model);
                return View("Edit", model);
            }
            Success(result.Message);
            return RedirectToAction("Index");
        }

        [PermissionAuthorize(PermissionCodes.UserEdit)]
        public ActionResult ResetPassword(int id)
        {
            var user = _users.Get(id);
            if (user == null) return NotFoundView();
            return View(new ResetPasswordViewModel { UserID = user.UserID, UserName = user.UserName });
        }

        [HttpPost, ValidateAntiForgeryToken, PermissionAuthorize(PermissionCodes.UserEdit)]
        public ActionResult ResetPassword(ResetPasswordViewModel model)
        {
            if (!ModelState.IsValid) return View(model);
            var result = _users.ResetPassword(model.UserID, model.NewPassword);
            if (!result.Success)
            {
                AddErrors(result);
                return View(model);
            }
            Success(result.Message);
            return RedirectToAction("Index");
        }

        private void Prepare(UserEditViewModel m)
        {
            m.Roles = SelectLists.From(_users.RoleLookup(), m.RoleID, "-- Select role --");
            m.Farms = SelectLists.From(_farms.Lookup(), m.FarmID, "All farms (no restriction)");
        }
    }

    [PermissionAuthorize(PermissionCodes.RoleView)]
    public class RoleController : BaseController
    {
        private readonly IRoleService _roles;

        public RoleController(IRoleService roles)
        {
            _roles = roles;
        }

        public ActionResult Index()
        {
            ViewBag.CanEdit = Can(PermissionCodes.RoleEdit);
            return View(_roles.GetAll());
        }

        [PermissionAuthorize(PermissionCodes.RoleEdit)]
        public ActionResult Create()
        {
            return View("Edit", new RoleEditViewModel());
        }

        [PermissionAuthorize(PermissionCodes.RoleEdit)]
        public ActionResult Edit(int id)
        {
            var role = _roles.Get(id);
            if (role == null) return NotFoundView();
            return View("Edit", RoleEditViewModel.From(role));
        }

        [HttpPost, ValidateAntiForgeryToken, PermissionAuthorize(PermissionCodes.RoleEdit)]
        public ActionResult Save(RoleEditViewModel model)
        {
            if (!ModelState.IsValid) return View("Edit", model);
            var result = _roles.Save(model.ToEntity());
            if (!result.Success)
            {
                AddErrors(result);
                return View("Edit", model);
            }
            Success(result.Message);
            return RedirectToAction("Permissions", new { id = result.Data });
        }

        public ActionResult Permissions(int id)
        {
            var role = _roles.Get(id);
            if (role == null) return NotFoundView();
            return View(new RolePermissionsViewModel
            {
                RoleID = role.RoleID,
                RoleName = role.RoleName,
                CanEdit = Can(PermissionCodes.RoleEdit),
                Permissions = _roles.GetPermissionMatrix(id)
            });
        }

        [HttpPost, ValidateAntiForgeryToken, PermissionAuthorize(PermissionCodes.RoleEdit)]
        public ActionResult Permissions(RolePermissionsViewModel model)
        {
            var result = _roles.SavePermissions(model.RoleID, model.SelectedIds ?? new int[0]);
            if (result.Success) Success(result.Message);
            else Error(result.Message);
            return RedirectToAction("Permissions", new { id = model.RoleID });
        }
    }

    [PermissionAuthorize(PermissionCodes.AuditView)]
    public class AuditLogController : BaseController
    {
        private readonly IAuditService _audit;

        public AuditLogController(IAuditService audit)
        {
            _audit = audit;
        }

        public ActionResult Index(DateTime? fromDate, DateTime? toDate, string tableName, string userName, int page = 1)
        {
            if (!fromDate.HasValue && !toDate.HasValue) fromDate = DateTime.Today.AddDays(-30);
            var result = _audit.Search(new AuditSearchCriteria
            {
                FromDate = fromDate,
                ToDate = toDate,
                TableName = tableName,
                UserName = userName,
                PageNo = page,
                PageSize = 25
            });
            return View(new AuditListViewModel
            {
                FromDate = fromDate,
                ToDate = toDate,
                TableName = tableName,
                UserName = userName,
                Result = result,
                Tables = SelectLists.FromStrings(_audit.TableNames(), tableName, "All tables")
            });
        }
    }

    public class HomeController : BaseController
    {
        private readonly IAnalysisService _analysis;
        private readonly IFarmService _farms;
        private readonly IHouseService _houses;
        private readonly IBirdTypeService _birdTypes;
        private readonly IFlockService _flocks;
        private readonly ILotService _lots;

        public HomeController(IAnalysisService analysis, IFarmService farms, IHouseService houses, IBirdTypeService birdTypes,
            IFlockService flocks, ILotService lots)
        {
            _analysis = analysis;
            _farms = farms;
            _houses = houses;
            _birdTypes = birdTypes;
            _flocks = flocks;
            _lots = lots;
        }

        /// <summary>Flock performance dashboard (KPIs, charts, latest week of every active flock).</summary>
        public ActionResult Index(DashboardFilterViewModel filter)
        {
            // Users without the dashboard permission land on the first screen they may see.
            if (!Can(PermissionCodes.DashboardView))
                return View("Welcome");

            filter = filter ?? new DashboardFilterViewModel();
            ModelState.Clear();
            var data = _analysis.Dashboard(new DashboardFilter
            {
                FarmID = filter.FarmID,
                HouseID = filter.HouseID,
                BirdTypeID = filter.BirdTypeID,
                FlockID = filter.FlockID,
                LotID = filter.LotID,
                AsOfDate = filter.AsOfDate,
                WeekNo = filter.WeekNo
            });

            var withData = data.Flocks.Where(f => f.HasData).ToList();
            if (!filter.ChartFlockID.HasValue || withData.All(f => f.FlockID != filter.ChartFlockID.Value))
                filter.ChartFlockID = withData.OrderBy(f => f.PerformanceScore ?? 999).Select(f => (int?)f.FlockID).FirstOrDefault();

            filter.Farms = SelectLists.From(_farms.Lookup(), filter.FarmID, "All farms");
            filter.Houses = SelectLists.From(filter.FarmID.HasValue ? _houses.Lookup(filter.FarmID) : new System.Collections.Generic.List<LookupItem>(), filter.HouseID, "All houses");
            filter.BirdTypes = SelectLists.From(_birdTypes.Lookup(), filter.BirdTypeID, "All bird types");
            filter.Flocks = SelectLists.From(_flocks.ActiveLookup(filter.FarmID, filter.HouseID), filter.FlockID, "All flocks");
            filter.Lots = SelectLists.From(_lots.Lookup(), filter.LotID, "All lots");
            filter.ChartFlocks = SelectLists.From(withData.Select(f => new LookupItem(f.FlockID, f.FlockNumber + " - " + f.BirdTypeName)), filter.ChartFlockID);

            return View(new AnalysisDashboardViewModel
            {
                Filter = filter,
                Data = data,
                UserFullName = CurrentUser.FullName,
                RoleName = CurrentUser.RoleName,
                CanSeeFlocks = Can(PermissionCodes.FlockView)
            });
        }
    }

    /// <summary>Permission-driven sidebar menu (rendered as a child action from the layout).</summary>
    public class NavigationController : BaseController
    {
        private readonly IPermissionService _permissions;

        public NavigationController(IPermissionService permissions)
        {
            _permissions = permissions;
        }

        [ChildActionOnly]
        public ActionResult Sidebar(string currentController, string currentAction, string currentTab)
        {
            if (CurrentUser == null) return new EmptyResult();
            var menu = _permissions.GetMenu(CurrentUser.RoleID);
            var groups = menu.OrderBy(m => m.SortOrder).GroupBy(m => m.MenuGroup).ToList();
            return PartialView("_Sidebar", new SidebarViewModel
            {
                CurrentController = currentController,
                ActiveItem = FindActiveItem(menu, currentController, currentAction, currentTab),
                Groups = groups
            });
        }

        /// <summary>Weekly entry tabs and the menu actions that open them (Weekly/BodyWeight redirects to Entry?tab=weight, ...).</summary>
        private static readonly Dictionary<string, string> WeeklyTabActions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "weight", "BodyWeight" }, { "feed", "Feed" }, { "nutrition", "Nutrition" },
            { "population", "Mortality" }, { "production", "Production" }
        };

        /// <summary>
        /// Picks the single menu item to highlight: exact controller + action first (taking the weekly entry tab into account),
        /// otherwise the first menu item of the same controller (e.g. HatchResult/Edit highlights "Hatch Results").
        /// </summary>
        private static MenuItem FindActiveItem(IEnumerable<MenuItem> menu, string controller, string action, string tab)
        {
            if (string.IsNullOrEmpty(controller)) return null;
            var sameController = menu.Where(m => string.Equals(m.ControllerName, controller, StringComparison.OrdinalIgnoreCase))
                                     .OrderBy(m => m.SortOrder).ToList();
            if (sameController.Count == 0) return null;

            string tabAction;
            if (string.Equals(controller, "Weekly", StringComparison.OrdinalIgnoreCase)
                && string.Equals(action, "Entry", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrEmpty(tab) && WeeklyTabActions.TryGetValue(tab, out tabAction))
            {
                var tabItem = sameController.FirstOrDefault(m => string.Equals(m.ActionName, tabAction, StringComparison.OrdinalIgnoreCase));
                if (tabItem != null) return tabItem;
            }

            return sameController.FirstOrDefault(m => string.Equals(m.ActionName, action, StringComparison.OrdinalIgnoreCase))
                   ?? sameController[0];
        }
    }

    /// <summary>JSON endpoints for cascading dropdowns.</summary>
    public class LookupController : BaseController
    {
        private readonly IHouseService _houses;
        private readonly IFlockService _flocks;
        private readonly IFeedFormulaService _formulas;

        public LookupController(IHouseService houses, IFlockService flocks, IFeedFormulaService formulas)
        {
            _houses = houses;
            _flocks = flocks;
            _formulas = formulas;
        }

        [HttpGet]
        public JsonResult Houses(int? farmId)
        {
            var items = farmId.HasValue ? _houses.Lookup(farmId) : new System.Collections.Generic.List<LookupItem>();
            return Json(items.Select(i => new { id = i.Id, text = i.Text }), JsonRequestBehavior.AllowGet);
        }

        [HttpGet]
        public JsonResult FeedFormulas(int? feedTypeId)
        {
            var items = _formulas.Lookup(feedTypeId, null);
            return Json(items.Select(i => new { id = i.Id, text = i.Text }), JsonRequestBehavior.AllowGet);
        }

        [HttpGet]
        public JsonResult Flocks(int? farmId, int? houseId)
        {
            var items = _flocks.ActiveLookup(farmId, houseId);
            return Json(items.Select(i => new { id = i.Id, text = i.Text }), JsonRequestBehavior.AllowGet);
        }
    }

    [AllowAnonymous]
    public class ErrorController : Controller
    {
        public ActionResult NotFound()
        {
            Response.StatusCode = 404;
            Response.TrySkipIisCustomErrors = true;
            return View("~/Views/Shared/NotFound.cshtml");
        }

        public ActionResult Index()
        {
            Response.StatusCode = 500;
            Response.TrySkipIisCustomErrors = true;
            return View("~/Views/Shared/Error.cshtml");
        }
    }
}
