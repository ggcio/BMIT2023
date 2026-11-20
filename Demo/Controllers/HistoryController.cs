using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Demo.Controllers;

[Authorize(Roles = "student,public")]
public class HistoryController(DB db) : Controller
{
    // GET: History/Index
    public IActionResult Index()
    {
        // Only the logged-in user's bookings, latest slot first
        var m = db.Bookings
                  .Include(b => b.TimeSlot)
                  .ThenInclude(s => s.Facility)
                  .Where(b => b.MemberEmail == User.Identity!.Name)
                  .OrderByDescending(b => b.TimeSlot.Date)
                  .ThenByDescending(b => b.TimeSlot.StartTime)
                  .ToList();

        // Rows that get a Cancel button
        ViewBag.CancelIds = m.Where(b => CancelError(b) == "").Select(b => b.Id).ToList();

        return View(m);
    }

    // Return error message, or "" if the booking can be cancelled
    private string CancelError(Booking b)
    {
        var start = b.TimeSlot.Date.ToDateTime(b.TimeSlot.StartTime);

        if ((b.Status != "Pending" && b.Status != "Confirmed") ||
            b.CheckedInAt != null ||
            start <= DateTime.Now)
        {
            return "This booking cannot be cancelled.";
        }

        // Public users: up to 24 hours before the slot. Students: any time before it starts.
        if (User.IsInRole("public") && start < DateTime.Now.AddHours(24))
        {
            return "Bookings can only be cancelled up to 24 hours before the slot.";
        }

        return "";
    }

    // POST: History/Cancel/{id}
    [HttpPost]
    public IActionResult Cancel(int id)
    {
        var b = db.Bookings
                  .Include(b => b.TimeSlot)
                  .Include(b => b.Payment)
                  .FirstOrDefault(b => b.Id == id && b.MemberEmail == User.Identity!.Name);

        if (b == null)
        {
            TempData["Info"] = "Booking not found.";
            return RedirectToAction("Index");
        }

        var error = CancelError(b);

        if (error != "")
        {
            TempData["Info"] = error;
            return RedirectToAction("Index");
        }

        b.Status = "Cancelled";
        b.HoldExpiresAt = null;

        if (b.Payment?.Status == "Paid")
        {
            // An admin refunds outside the system and marks it refunded
            b.RefundStatus = "RefundDue";
            TempData["Info"] = "Booking cancelled. Your refund will be processed by an admin.";
        }
        else
        {
            TempData["Info"] = "Booking cancelled.";
        }

        db.SaveChanges();

        return RedirectToAction("Index");
    }
}
