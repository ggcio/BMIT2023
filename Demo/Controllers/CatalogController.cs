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
