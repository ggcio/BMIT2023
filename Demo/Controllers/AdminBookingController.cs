using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using X.PagedList.Extensions;

namespace Demo.Controllers;

[Authorize(Roles = "Admin,SuperAdmin")]
public class AdminBookingController(DB db) : Controller
{
    // GET: AdminBooking/Index?status=&page=
    public IActionResult Index(string? status, int page = 1)
    {
        // (1) Filtering ------------------------
        string[] statuses = ["Pending", "Confirmed", "Cancelled"];
        ViewBag.Status = status = statuses.Contains(status) ? status : "All";

        var filtered = db.Bookings
                         .Include(b => b.Member)
                         .Include(b => b.TimeSlot)
                         .ThenInclude(s => s.Facility)
                         .Where(b => status == "All" || b.Status == status);

        // (2) Sorting: newest first ------------
        var sorted = filtered.OrderByDescending(b => b.Id);

        // (3) Paging ---------------------------
        if (page < 1)
        {
            return RedirectToAction(null, new { status, page = 1 });
        }

        var m = sorted.ToPagedList(page, 10);

        if (page > m.PageCount && m.PageCount > 0)
        {
            return RedirectToAction(null, new { status, page = m.PageCount });
        }

        if (Request.IsAjax())
        {
            return PartialView("_Index", m);
        }

        return View(m);
    }

    // GET: AdminBooking/Detail/{id}
    public IActionResult Detail(int id)
    {
        var m = db.Bookings
                  .Include(b => b.Member)
                  .Include(b => b.TimeSlot)
                  .ThenInclude(s => s.Facility)
                  .Include(b => b.Payment)
                  .FirstOrDefault(b => b.Id == id);

        if (m == null)
        {
            TempData["Info"] = "Booking not found.";
            return RedirectToAction("Index");
        }

        return View(m);
    }
}
