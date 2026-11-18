using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Demo.Controllers;

public class CatalogController(DB db) : Controller
{
    // GET: Catalog/Index
    [AllowAnonymous]
    public IActionResult Index(int? categoryId)
    {
        if (categoryId != null && !db.Categories.Any(c => c.Id == categoryId))
        {
            return RedirectToAction("Index");
        }

        ViewBag.Categories = db.Categories.OrderBy(c => c.Name);
        ViewBag.CategoryId = categoryId;

        var m = db.Facilities
                  .Include(f => f.Category)
                  .Where(f => categoryId == null || f.CategoryId == categoryId)
                  .OrderBy(f => f.Name);

        return View(m);
    }

    // GET: Catalog/Detail
    [AllowAnonymous]
    public IActionResult Detail(int? id)
    {
        var m = db.Facilities
                  .Include(f => f.Category)
                  .FirstOrDefault(f => f.Id == id);

        if (m == null)
        {
            return RedirectToAction("Index");
        }

        return View(m);
    }
}
