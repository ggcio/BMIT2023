using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Net;
using X.PagedList.Extensions;

namespace Demo.Controllers;

[Authorize(Roles = "admin")]
public class FacilityController(DB db, Helper hp) : Controller
{
    // GET: Facility/Index
    public IActionResult Index(string? name, string? sort, string? dir, int page = 1)
    {
        // (1) Searching ------------------------
        ViewBag.Name = name = name?.Trim() ?? "";

        var searched = db.Facilities
                         .Include(f => f.Category)
                         .Where(f => f.Name.Contains(name));

        // (2) Sorting --------------------------
        string[] sorts = ["Id", "Name", "Category", "Location", "Capacity", "Rate"];
        ViewBag.Sort = sort = sorts.Contains(sort) ? sort : "Name";
        ViewBag.Dir = dir = dir == "des" ? "des" : "asc";

        Func<Facility, object> fn = sort switch
        {
            "Id"       => f => f.Id,
            "Category" => f => f.Category.Name,
            "Location" => f => f.Location,
            "Capacity" => f => f.Capacity,
            "Rate"     => f => f.HourlyRate,
            _          => f => f.Name,
        };

        var sorted = dir == "des" ? searched.OrderByDescending(fn) : searched.OrderBy(fn);

        // (3) Paging ---------------------------
        if (page < 1)
        {
            return RedirectToAction(null, new { name, sort, dir, page = 1 });
        }

        var m = sorted.ToPagedList(page, 10);

        if (page > m.PageCount && m.PageCount > 0)
        {
            return RedirectToAction(null, new { name, sort, dir, page = m.PageCount });
        }

        if (Request.IsAjax())
        {
            return PartialView("_Index", m);
        }

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
        ViewBag.Photos = db.FacilityPhotos.Where(p => p.FacilityId == f.Id).OrderBy(p => p.Id).ToList();
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
        ViewBag.Photos = db.FacilityPhotos.Where(p => p.FacilityId == f.Id).OrderBy(p => p.Id).ToList();
        return View(vm);
    }

    // POST: Facility/AddPhotos
    [HttpPost]
    public IActionResult AddPhotos(int? id, List<IFormFile> photos)
    {
        var f = db.Facilities.Find(id);

        if (f == null)
        {
            return RedirectToAction("Index");
        }

        int existing = db.FacilityPhotos.Count(p => p.FacilityId == f.Id);

        if (photos.Count == 0)
        {
            TempData["Info"] = "Please select at least one photo.";
            return RedirectToAction("Update", new { id = f.Id });
        }

        if (existing + photos.Count > 5)
        {
            TempData["Info"] = "A facility can have at most 5 photos.";
            return RedirectToAction("Update", new { id = f.Id });
        }

        // Check every file first, so nothing is saved if one is invalid
        foreach (var p in photos)
        {
            var err = hp.ValidatePhoto(p);
            if (err != "")
            {
                // Layout shows Info as raw HTML, so encode the user's file name
                TempData["Info"] = $"{WebUtility.HtmlEncode(p.FileName)}: {err}";
                return RedirectToAction("Update", new { id = f.Id });
            }
        }

        // Save every file first; if one fails (e.g. a fake image), remove the ones saved so far
        var saved = new List<string>();
        foreach (var p in photos)
        {
            try
            {
                saved.Add(hp.SavePhoto(p, "photos", 600, 400));
            }
            catch
            {
                foreach (var file in saved)
                {
                    hp.DeletePhoto(file, "photos");
                }

                TempData["Info"] = $"{WebUtility.HtmlEncode(p.FileName)}: Photo could not be processed.";
                return RedirectToAction("Update", new { id = f.Id });
            }
        }

        foreach (var file in saved)
        {
            db.FacilityPhotos.Add(new()
            {
                FileName = file,
                FacilityId = f.Id,
            });
        }
        db.SaveChanges();

        TempData["Info"] = $"{photos.Count} photo(s) added.";
        return RedirectToAction("Update", new { id = f.Id });
    }

    // POST: Facility/DeletePhoto
    [HttpPost]
    public IActionResult DeletePhoto(int? id)
    {
        var p = db.FacilityPhotos.Find(id);

        if (p == null)
        {
            return RedirectToAction("Index");
        }

        db.FacilityPhotos.Remove(p);
        db.SaveChanges();

        hp.DeletePhoto(p.FileName, "photos");

        TempData["Info"] = "Photo deleted.";
        return RedirectToAction("Update", new { id = p.FacilityId });
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
                // Read the file names first, the photo rows are removed with the facility
                var files = db.FacilityPhotos.Where(p => p.FacilityId == f.Id).Select(p => p.FileName).ToList();

                db.Facilities.Remove(f);
                db.SaveChanges();

                foreach (var file in files)
                {
                    hp.DeletePhoto(file, "photos");
                }

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
