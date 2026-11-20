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
                  .ThenByDescending(b => b.TimeSlot.StartTime);

        return View(m);
    }
}
