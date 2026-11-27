using Microsoft.AspNetCore.Mvc;

namespace Demo.Controllers;

public class HomeController : Controller
{
    // GET: Home/Index (the catalog is the landing page now)
    public IActionResult Index()
    {
        return RedirectToAction("Index", "Catalog");
    }
}
