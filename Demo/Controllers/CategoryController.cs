using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Demo.Controllers;

[Authorize(Roles = "Admin,SuperAdmin")]
public class CategoryController(DB db) : Controller
{
    // GET: Category/Index
    public IActionResult Index()
    {
        var m = db.Categories.Include(c => c.Facilities);
        return View(m);
    }

    // GET: Category/CheckName
    // True if no other category has the same name (ignoring case)
    public bool CheckName(string name, int? id)
    {
        name = (name ?? "").Trim().ToLower();
        return !db.Categories.Any(c => c.Name.ToLower() == name && c.Id != id);
    }

    // GET: Category/Insert
    public IActionResult Insert()
    {
        return View();
    }

    // POST: Category/Insert
    [HttpPost]
    public IActionResult Insert(CategoryVM vm)
    {
        if (ModelState.IsValid("Name") && !CheckName(vm.Name, null))
        {
            ModelState.AddModelError("Name", "Duplicated Name.");
        }

        if (ModelState.IsValid)
        {
            db.Categories.Add(new()
            {
                Name = vm.Name.Trim(),
            });
            db.SaveChanges();

            TempData["Info"] = "Category inserted.";
            return RedirectToAction("Index");
        }

        return View(vm);
    }

    // GET: Category/Update
    public IActionResult Update(int? id)
    {
        var c = db.Categories.Find(id);

        if (c == null)
        {
            return RedirectToAction("Index");
        }

        var vm = new CategoryVM
        {
            Id = c.Id,
            Name = c.Name,
        };

        return View(vm);
    }

    // POST: Category/Update
    [HttpPost]
    public IActionResult Update(CategoryVM vm)
    {
        var c = db.Categories.Find(vm.Id);

        if (c == null)
        {
            return RedirectToAction("Index");
        }

        if (ModelState.IsValid("Name") && !CheckName(vm.Name, vm.Id))
        {
            ModelState.AddModelError("Name", "Duplicated Name.");
        }

        if (ModelState.IsValid)
        {
            c.Name = vm.Name.Trim();
            db.SaveChanges();

            TempData["Info"] = "Category updated.";
            return RedirectToAction("Index");
        }

        return View(vm);
    }

    // POST: Category/Delete
    [HttpPost]
    public IActionResult Delete(int? id)
    {
        var c = db.Categories.Find(id);

        if (c != null)
        {
            if (db.Facilities.Any(f => f.CategoryId == c.Id))
            {
                TempData["Info"] = "Cannot delete a category that still has facilities.";
            }
            else
            {
                db.Categories.Remove(c);
                db.SaveChanges();

                TempData["Info"] = "Category deleted.";
            }
        }

        // Go back to the referring page only if it is on this site, otherwise to Index
        var referer = Request.Headers.Referer.ToString();
        if (Uri.TryCreate(referer, UriKind.Absolute, out var uri) &&
            uri.Authority == Request.Host.Value &&
            Url.IsLocalUrl(uri.PathAndQuery))
        {
            return Redirect(uri.PathAndQuery);
        }

        return RedirectToAction("Index");
    }
}
