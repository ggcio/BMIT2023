using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Demo.Controllers;

[Authorize(Roles = "Admin,SuperAdmin")]
public class FacilityController(DB db) : Controller
{
    // GET: Facility/Index
    public IActionResult Index()
    {
        var m = db.Facilities.Include(f => f.Category);
        return View(m);
    }

    // GET: Facility/CheckName
    // True if no other facility has the same name (ignoring case)
    public bool CheckName(string name, int? id)
    {
        name = (name ?? "").Trim().ToLower();
        return !db.Facilities.Any(f => f.Name.ToLower() == name && f.Id != id);
    }

    // GET: Facility/Insert
    public IActionResult Insert()
    {
        ViewBag.CategoryList = new SelectList(db.Categories.OrderBy(c => c.Name), "Id", "Name");
        return View();
    }

    // POST: Facility/Insert
    [HttpPost]
    public IActionResult Insert(FacilityVM vm)
    {
        if (ModelState.IsValid("Name") && !CheckName(vm.Name, null))
        {
            ModelState.AddModelError("Name", "Duplicated Name.");
        }

        if (ModelState.IsValid("CategoryId") && !db.Categories.Any(c => c.Id == vm.CategoryId))
        {
            ModelState.AddModelError("CategoryId", "Invalid Category.");
        }

        if (ModelState.IsValid)
        {
            db.Facilities.Add(new()
            {
                Name = vm.Name.Trim(),
                Location = vm.Location.Trim(),
                Capacity = vm.Capacity,
                HourlyRate = vm.HourlyRate,
                PeakSurcharge = vm.PeakSurcharge,
                CategoryId = vm.CategoryId,
            });
            db.SaveChanges();

            TempData["Info"] = "Facility inserted.";
            return RedirectToAction("Index");
        }

        ViewBag.CategoryList = new SelectList(db.Categories.OrderBy(c => c.Name), "Id", "Name");
        return View(vm);
    }

    // GET: Facility/Update
    public IActionResult Update(int? id)
    {
        var f = db.Facilities.Find(id);

        if (f == null)
        {
            return RedirectToAction("Index");
        }

        var vm = new FacilityVM
        {
            Id = f.Id,
            Name = f.Name,
            Location = f.Location,
            Capacity = f.Capacity,
            HourlyRate = f.HourlyRate,
            PeakSurcharge = f.PeakSurcharge,
            CategoryId = f.CategoryId,
        };

        ViewBag.CategoryList = new SelectList(db.Categories.OrderBy(c => c.Name), "Id", "Name");
        return View(vm);
    }

    // POST: Facility/Update
    [HttpPost]
    public IActionResult Update(FacilityVM vm)
    {
        var f = db.Facilities.Find(vm.Id);

        if (f == null)
        {
            return RedirectToAction("Index");
        }

        if (ModelState.IsValid("Name") && !CheckName(vm.Name, vm.Id))
        {
            ModelState.AddModelError("Name", "Duplicated Name.");
        }

        if (ModelState.IsValid("CategoryId") && !db.Categories.Any(c => c.Id == vm.CategoryId))
        {
            ModelState.AddModelError("CategoryId", "Invalid Category.");
        }

        if (ModelState.IsValid)
        {
            f.Name = vm.Name.Trim();
            f.Location = vm.Location.Trim();
            f.Capacity = vm.Capacity;
            f.HourlyRate = vm.HourlyRate;
            f.PeakSurcharge = vm.PeakSurcharge;
            f.CategoryId = vm.CategoryId;
            db.SaveChanges();

            TempData["Info"] = "Facility updated.";
            return RedirectToAction("Index");
        }

        ViewBag.CategoryList = new SelectList(db.Categories.OrderBy(c => c.Name), "Id", "Name");
        return View(vm);
    }

    // POST: Facility/Delete
    [HttpPost]
    public IActionResult Delete(int? id)
    {
        var f = db.Facilities.Find(id);

        if (f != null)
        {
            if (db.Bookings.Any(b => b.TimeSlot.FacilityId == f.Id))
            {
                TempData["Info"] = "Cannot delete a facility that has bookings.";
            }
            else
            {
                db.Facilities.Remove(f);
                db.SaveChanges();

                TempData["Info"] = "Facility deleted.";
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
