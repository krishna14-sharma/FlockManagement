using System;
using System.Web.Mvc;
using FlockManagement.Business.Interfaces;
using FlockManagement.Common.Helpers;
using FlockManagement.Web.Infrastructure;
using FlockManagement.Web.ViewModels;

namespace FlockManagement.Web.Controllers
{
    /// <summary>Marks an action that changes data; it requires the controller's edit permission.</summary>
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class EditActionAttribute : Attribute { }

    /// <summary>
    /// List / create / edit / activate-deactivate for simple masters.
    /// View permission protects every action; actions marked [EditAction] also need edit permission.
    /// </summary>
    public abstract class MasterController<TEntity, TViewModel> : BaseController
        where TEntity : class
        where TViewModel : class, new()
    {
        protected readonly IMasterService<TEntity> Service;

        protected MasterController(IMasterService<TEntity> service)
        {
            Service = service;
        }

        protected abstract string ViewPermission { get; }
        protected abstract string EditPermission { get; }
        protected abstract string EntityTitle { get; }

        protected abstract TViewModel ToViewModel(TEntity entity);
        protected abstract TEntity ToEntity(TViewModel model);
        protected abstract int GetId(TViewModel model);

        /// <summary>Fill dropdown lists etc. before the edit view is rendered.</summary>
        protected virtual void PrepareEdit(TViewModel model) { }

        /// <summary>Fill filter dropdowns before the list view is rendered.</summary>
        protected virtual void PrepareList(MasterListViewModel<TEntity> model) { }

        protected override void OnAuthorization(AuthorizationContext filterContext)
        {
            base.OnAuthorization(filterContext);
            if (filterContext.Result != null) return;

            var principal = filterContext.HttpContext.User as AppPrincipal;
            if (principal == null) return; // [Authorize] on BaseController handles anonymous users

            var isEdit = filterContext.ActionDescriptor.IsDefined(typeof(EditActionAttribute), true);
            var required = isEdit ? EditPermission : ViewPermission;
            if (!principal.HasPermission(required) || (isEdit && !principal.HasPermission(ViewPermission)))
                AccessDenied.Handle(filterContext);
        }

        public virtual ActionResult Index(string search, string status = "active", int? parentId = null, int page = 1)
        {
            var request = new PageRequest
            {
                Search = search,
                IsActive = MasterListViewModel<TEntity>.ParseStatus(status),
                ParentID = parentId,
                PageNo = page,
                PageSize = 25
            };

            var model = new MasterListViewModel<TEntity>
            {
                Title = EntityTitle,
                Search = search,
                Status = string.IsNullOrEmpty(status) ? "active" : status,
                ParentID = parentId,
                Result = Service.Search(request),
                CanEdit = Can(EditPermission)
            };
            PrepareList(model);
            return View(model);
        }

        [EditAction]
        public virtual ActionResult Create()
        {
            var model = new TViewModel();
            PrepareEdit(model);
            ViewBag.Title = "New " + EntityTitle;
            return View("Edit", model);
        }

        [EditAction]
        public virtual ActionResult Edit(int id)
        {
            var entity = Service.Get(id);
            if (entity == null) return NotFoundView();
            var model = ToViewModel(entity);
            PrepareEdit(model);
            ViewBag.Title = "Edit " + EntityTitle;
            return View("Edit", model);
        }

        [HttpPost, ValidateAntiForgeryToken, EditAction]
        public virtual ActionResult Save(TViewModel model)
        {
            if (model == null) return RedirectToAction("Index");
            ViewBag.Title = (GetId(model) == 0 ? "New " : "Edit ") + EntityTitle;

            if (!ModelState.IsValid)
            {
                PrepareEdit(model);
                return View("Edit", model);
            }

            var result = Service.Save(ToEntity(model));
            if (!result.Success)
            {
                AddErrors(result);
                PrepareEdit(model);
                return View("Edit", model);
            }

            Success(result.Message);
            return RedirectToAction("Index");
        }

        [HttpPost, ValidateAntiForgeryToken, EditAction]
        public virtual ActionResult ToggleActive(int id, bool active)
        {
            var result = Service.SetActive(id, active);
            if (result.Success) Success(result.Message);
            else Error(result.Message);
            return RedirectToAction("Index");
        }
    }
}
