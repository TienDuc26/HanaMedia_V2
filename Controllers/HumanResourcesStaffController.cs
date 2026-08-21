using HanaMedia.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HanaMedia.Controllers
{
    [Authorize(Roles = AppRoles.HumanResourcesStaff)]
    public class HumanResourcesStaffController : Controller
    {
        public IActionResult HumanResources()
        {
            // Share view chung với ql_hcns — FE sẽ ẩn cột Lương + nút Xóa nếu IsStaff.
            ViewBag.IsStaff = true;
            return View("~/Views/ManageHuman/HumanResources.cshtml");
        }

        public IActionResult Reported()
        {
            return View();
        }
    }
}
