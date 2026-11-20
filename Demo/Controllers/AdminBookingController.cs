using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using X.PagedList.Extensions;

namespace Demo.Controllers;

[Authorize(Roles = "admin")]
public class AdminBookingController(DB db) : Controller
{
    // GET: AdminBooking/Index?status=&page=
    public IActionResult Index(string? status, int page = 1)
    {
        // (1) Filtering ------------------------
        string[] statuses = ["Pending", "Confirmed", "Cancelled"];
        ViewBag.Status = status = statuses.Contains(status) ? status : "All";

        var filtered = db.Bookings
                         .Include(b => b.User)
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
                  .Include(b => b.User)
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

    // GET: AdminBooking/CheckIn
    public IActionResult CheckIn()
    {
        return View();
    }

    // POST: AdminBooking/CheckIn
    [HttpPost]
    public IActionResult CheckIn(CheckInVM vm)
    {
        if (!ModelState.IsValid)
        {
            return View(vm);
        }

        var b = db.Bookings
                  .Include(b => b.User)
                  .Include(b => b.TimeSlot)
                  .ThenInclude(s => s.Facility)
                  .FirstOrDefault(b => b.Id == vm.BookingId);

        if (b == null)
        {
            ViewBag.Result = "Booking not found.";
        }
        else if (b.Status != "Confirmed")
        {
            ViewBag.Result = $"Not valid: this booking is {b.Status}.";
        }
        else if (b.CheckedInAt != null)
        {
            ViewBag.Result = $"Already used: checked in at {b.CheckedInAt:dd MMM yyyy HH:mm}.";
        }
        else if (b.TimeSlot.Date != DateTime.Today.ToDateOnly())
        {
            ViewBag.Result = $"Wrong date: this booking is for {b.TimeSlot.Date:dd MMM yyyy}.";
        }
        else
        {
            b.CheckedInAt = DateTime.Now;
            db.SaveChanges();

            ViewBag.Result = "Valid: checked in.";
        }

        ViewBag.Booking = b;

        // Clear the input so the next ID can be entered
        ModelState.Clear();
        return View(new CheckInVM());
    }

    // GET: AdminBooking/Charts
    public IActionResult Charts()
    {
        // Chart 1: Confirmed bookings per facility (every facility, 0 if none)
        var facilities = db.Facilities
                           .OrderBy(f => f.Id)
                           .Select(f => new
                           {
                               f.Name,
                               Count = f.TimeSlots.SelectMany(s => s.Bookings)
                                                  .Count(b => b.Status == "Confirmed"),
                           })
                           .ToList();

        ViewBag.FacilityLabels = facilities.Select(f => f.Name).ToList();
        ViewBag.FacilityValues = facilities.Select(f => f.Count).ToList();

        // Chart 2: revenue per day for the last 14 days (paid and not refunded)
        var today = DateTime.Today;
        var start = today.AddDays(-13);

        var payments = db.Payments
                         .Where(p => p.Status == "Paid" &&
                                     p.Booking.RefundStatus == "None" &&
                                     p.PaidAt >= start &&
                                     p.PaidAt < today.AddDays(1))
                         .Select(p => new { p.PaidAt, p.Amount })
                         .ToList();

        var days = Enumerable.Range(0, 14).Select(n => start.AddDays(n)).ToList();

        ViewBag.RevenueLabels = days.Select(d => d.ToString("dd MMM")).ToList();
        ViewBag.RevenueValues = days.Select(d => payments.Where(p => p.PaidAt!.Value.Date == d)
                                                         .Sum(p => p.Amount))
                                    .ToList();

        return View();
    }

    // POST: AdminBooking/Cancel/{id}
    [HttpPost]
    public IActionResult Cancel(int id)
    {
        var b = db.Bookings
                  .Include(b => b.Payment)
                  .FirstOrDefault(b => b.Id == id);

        if (b == null)
        {
            TempData["Info"] = "Booking not found.";
        }
        else if ((b.Status == "Pending" || b.Status == "Confirmed") && b.CheckedInAt == null)
        {
            b.Status = "Cancelled";
            b.HoldExpiresAt = null;

            // The member paid, so an admin must refund outside the system
            if (b.Payment?.Status == "Paid")
            {
                b.RefundStatus = "RefundDue";
            }
            db.SaveChanges();

            TempData["Info"] = "Booking cancelled.";
        }
        else
        {
            TempData["Info"] = "This booking cannot be cancelled.";
        }

        return RedirectToAction("Detail", new { id });
    }

    // POST: AdminBooking/Refunded/{id}
    [HttpPost]
    public IActionResult Refunded(int id)
    {
        var b = db.Bookings.Find(id);

        if (b == null)
        {
            TempData["Info"] = "Booking not found.";
        }
        else if (b.RefundStatus == "RefundDue")
        {
            b.RefundStatus = "Refunded";
            db.SaveChanges();

            TempData["Info"] = "Refund marked as paid.";
        }
        else
        {
            TempData["Info"] = "No refund is due for this booking.";
        }

        return RedirectToAction("Detail", new { id });
    }
}
