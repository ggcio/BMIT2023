using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using X.PagedList.Extensions;

namespace Demo.Controllers;

public class CatalogController(DB db) : Controller
{
    // GET: Catalog/Index
    [AllowAnonymous]
    public IActionResult Index(int? categoryId, string? name, string? sort, string? dir, int page = 1)
    {
        if (categoryId != null && !db.Categories.Any(c => c.Id == categoryId))
        {
            return RedirectToAction("Index");
        }

        ViewBag.Categories = db.Categories.OrderBy(c => c.Name);
        ViewBag.CategoryId = categoryId;

        // (1) Filtering and searching ----------
        ViewBag.Name = name = name?.Trim() ?? "";

        var searched = db.Facilities
                         .Include(f => f.Category)
                         .Where(f => categoryId == null || f.CategoryId == categoryId)
                         .Where(f => f.Name.Contains(name));

        // (2) Sorting --------------------------
        string[] sorts = ["Name", "Rate", "Capacity"];
        ViewBag.Sort = sort = sorts.Contains(sort) ? sort : "Name";
        ViewBag.Dir = dir = dir == "des" ? "des" : "asc";

        Func<Facility, object> fn = sort switch
        {
            "Rate"     => f => f.HourlyRate,
            "Capacity" => f => f.Capacity,
            _          => f => f.Name,
        };

        var sorted = dir == "des" ? searched.OrderByDescending(fn) : searched.OrderBy(fn);

        // (3) Paging ---------------------------
        if (page < 1)
        {
            return RedirectToAction(null, new { categoryId, name, sort, dir, page = 1 });
        }

        var m = sorted.ToPagedList(page, 10);

        if (page > m.PageCount && m.PageCount > 0)
        {
            return RedirectToAction(null, new { categoryId, name, sort, dir, page = m.PageCount });
        }

        if (Request.IsAjax())
        {
            return PartialView("_Index", m);
        }

        return View(m);
    }

    // GET: Catalog/Detail
    [AllowAnonymous]
    public IActionResult Detail(int? id, DateOnly? date)
    {
        var m = db.Facilities
                  .Include(f => f.Category)
                  .FirstOrDefault(f => f.Id == id);

        if (m == null)
        {
            return RedirectToAction("Index");
        }

        // Only today to today+6 can be shown
        var today = DateTime.Today.ToDateOnly();
        if (date == null || date < today || date > today.AddDays(6))
        {
            date = today;
        }

        var now = DateTime.Now;
        var nowTime = now.ToTimeOnly();
        var isToday = date == today;

        // Active bookings = Confirmed + Pending still within the 15-minute hold
        ViewBag.Slots = db.TimeSlots
                          .Where(s => s.FacilityId == m.Id && s.Date == date)
                          .OrderBy(s => s.StartTime)
                          .Select(s => new SlotRowVM
                          {
                              Id = s.Id,
                              StartTime = s.StartTime,
                              EndTime = s.EndTime,
                              IsPeak = s.IsPeak,
                              Price = m.HourlyRate + (s.IsPeak ? m.PeakSurcharge : 0),
                              SpotsLeft = m.Capacity - s.Bookings.Count(b => b.Status == "Confirmed" ||
                                                                             (b.Status == "Pending" && b.HoldExpiresAt > now)),
                              IsClosed = isToday && s.StartTime <= nowTime,
                          })
                          .ToList();

        ViewBag.Date = date.Value.ToString("yyyy-MM-dd");

        return View(m);
    }
}
