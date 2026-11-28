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
        LoadTodayLists();
        return View();
    }

    // POST: AdminBooking/CheckIn
    [HttpPost]
    public IActionResult CheckIn(CheckInVM vm)
    {
        if (!ModelState.IsValid)
        {
            LoadTodayLists();
            return View(vm);
        }

        var b = db.Bookings
                  .Include(b => b.User)
                  .Include(b => b.TimeSlot)
                  .ThenInclude(s => s.Facility)
                  .FirstOrDefault(b => b.Id == vm.BookingId);

        ViewBag.Result = DoCheckIn(b);
        ViewBag.Booking = b;

        LoadTodayLists();

        // Clear the input so the next ID can be entered
        ModelState.Clear();
        return View(new CheckInVM());
    }

    // POST: AdminBooking/CheckInById/{id} (the Check In button in the Ready list)
    [HttpPost]
    public IActionResult CheckInById(int id)
    {
        var b = db.Bookings
                  .Include(b => b.TimeSlot)
                  .FirstOrDefault(b => b.Id == id);

        TempData["Info"] = DoCheckIn(b);
        return RedirectToAction("CheckIn");
    }

    // Check in the booking if allowed, and return the result message
    private string DoCheckIn(Booking? b)
    {
        if (b == null)
        {
            return "Booking not found.";
        }
        else if (b.Status != "Confirmed")
        {
            return $"Not valid: this booking is {b.Status}.";
        }
        else if (b.CheckedInAt != null)
        {
            return $"Already used: checked in at {b.CheckedInAt:dd MMM yyyy HH:mm}.";
        }
        else if (b.TimeSlot.Date != DateTime.Today.ToDateOnly())
        {
            return $"Wrong date: this booking is for {b.TimeSlot.Date:dd MMM yyyy}.";
        }

        b.CheckedInAt = DateTime.Now;
        db.SaveChanges();

        return "Valid: checked in.";
    }

    // Today's Confirmed bookings: not checked in yet, and already checked in
    private void LoadTodayLists()
    {
        var today = DateTime.Today.ToDateOnly();

        var todays = db.Bookings
                       .Include(b => b.User)
                       .Include(b => b.TimeSlot)
                       .ThenInclude(s => s.Facility)
                       .Where(b => b.Status == "Confirmed" && b.TimeSlot.Date == today)
                       .OrderBy(b => b.TimeSlot.StartTime)
                       .ToList();

        ViewBag.Ready = todays.Where(b => b.CheckedInAt == null).ToList();
        ViewBag.CheckedIn = todays.Where(b => b.CheckedInAt != null).ToList();
    }

    // GET: AdminBooking/Charts (moved to the Reports page)
    public IActionResult Charts()
    {
        return RedirectToAction("Index", "Report");
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
