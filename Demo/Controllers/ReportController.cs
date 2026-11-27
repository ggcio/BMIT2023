using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Demo.Controllers;

[Authorize(Roles = "admin")]
public class ReportController(DB db, Helper hp) : Controller
{
    // GET: Report/Index?Mode=&From=&To=&FromMonth=&FromYear=&ToMonth=&ToYear=&FacilityId=&CategoryId=
    public IActionResult Index(ReportFilterVM vm)
    {
        var today = DateTime.Today.ToDateOnly();

        // Dropdowns ----------------------------------------------------------
        ViewBag.FacilityList = new SelectList(db.Facilities.OrderBy(f => f.Name), "Id", "Name");
        ViewBag.CategoryList = new SelectList(db.Categories.OrderBy(c => c.Name), "Id", "Name");
        ViewBag.MonthList    = hp.GetMonthList();
        ViewBag.YearList     = hp.GetYearList(today.Year - 2, today.Year, true);
        ViewBag.Today        = today;
        ViewBag.HasData      = false;

        // Defaults: last 30 days, and this month for the month fields
        vm.From      ??= today.AddDays(-29);
        vm.To        ??= today;
        vm.FromMonth ??= today.Month;
        vm.FromYear  ??= today.Year;
        vm.ToMonth   ??= today.Month;
        vm.ToYear    ??= today.Year;

        // Invalid filter (e.g. month 13): show the form with errors and no data
        if (!ModelState.IsValid)
        {
            return View(vm);
        }

        // (1) Work out the date range ---------------------------------------
        DateOnly from, to;

        if (vm.Mode == "Month")
        {
            // First day of the From month to the last day of the To month
            from = new DateOnly(vm.FromYear.Value, vm.FromMonth.Value, 1);
            to   = new DateOnly(vm.ToYear.Value, vm.ToMonth.Value, 1).AddMonths(1).AddDays(-1);
        }
        else
        {
            from = vm.From.Value;
            to   = vm.To.Value;
        }

        if (from > to)
        {
            ModelState.AddModelError("", "From must not be after To.");
            return View(vm);
        }

        ViewBag.HasData = true;
        ViewBag.RangeText = $"{from:dd MMM yyyy} to {to:dd MMM yyyy}";

        // Facilities that match both filters
        var facilityIds = db.Facilities
                            .Where(f => (vm.FacilityId == null || f.Id == vm.FacilityId) &&
                                        (vm.CategoryId == null || f.CategoryId == vm.CategoryId))
                            .Select(f => f.Id)
                            .ToList();

        // (2) Chart 1: Confirmed bookings per facility, by slot date ---------
        var facilities = db.Facilities
                           .Where(f => facilityIds.Contains(f.Id))
                           .OrderBy(f => f.Id)
                           .Select(f => new
                           {
                               f.Name,
                               Count = f.TimeSlots.Where(s => s.Date >= from && s.Date <= to)
                                                  .SelectMany(s => s.Bookings)
                                                  .Count(b => b.Status == "Confirmed"),
                           })
                           .ToList();

        ViewBag.FacilityLabels = facilities.Select(f => f.Name).ToList();
        ViewBag.FacilityValues = facilities.Select(f => f.Count).ToList();

        // (3) Chart 2: paid, not refunded revenue per day, by PaidAt ---------
        var start = from.ToDateTime(TimeOnly.MinValue);
        var end   = to.AddDays(1).ToDateTime(TimeOnly.MinValue);

        var payments = db.Payments
                         .Where(p => p.Status == "Paid" &&
                                     p.Booking.RefundStatus == "None" &&
                                     facilityIds.Contains(p.Booking.TimeSlot.FacilityId) &&
                                     p.PaidAt >= start &&
                                     p.PaidAt < end)
                         .Select(p => new { p.PaidAt, p.Amount })
                         .ToList();

        var days = Enumerable.Range(0, to.DayNumber - from.DayNumber + 1)
                             .Select(n => from.AddDays(n))
                             .ToList();

        ViewBag.RevenueLabels = days.Select(d => d.ToString("dd MMM")).ToList();
        ViewBag.RevenueValues = days.Select(d => payments.Where(p => p.PaidAt!.Value.ToDateOnly() == d)
                                                         .Sum(p => p.Amount))
                                    .ToList();

        // (4) Summary cards ---------------------------------------------------
        var bookings = db.Bookings
                         .Where(b => facilityIds.Contains(b.TimeSlot.FacilityId) &&
                                     b.TimeSlot.Date >= from &&
                                     b.TimeSlot.Date <= to)
                         .Select(b => new
                         {
                             b.Status,
                             b.RefundStatus,
                             b.CheckedInAt,
                             b.TimeSlot.Date,
                             b.TimeSlot.EndTime,
                             Amount = b.Payment == null ? 0 : b.Payment.Amount,
                         })
                         .ToList();

        // Bookings by slot date
        ViewBag.Total     = bookings.Count;
        ViewBag.Confirmed = bookings.Count(b => b.Status == "Confirmed");
        ViewBag.Pending   = bookings.Count(b => b.Status == "Pending");
        ViewBag.Cancelled = bookings.Count(b => b.Status == "Cancelled");

        // Net revenue: same payments as the revenue chart
        ViewBag.NetRevenue = payments.Sum(p => p.Amount);

        // Refunds
        var refundDue = bookings.Where(b => b.RefundStatus == "RefundDue").ToList();
        var refunded  = bookings.Where(b => b.RefundStatus == "Refunded").ToList();

        ViewBag.RefundDueCount  = refundDue.Count;
        ViewBag.RefundDueAmount = refundDue.Sum(b => b.Amount);
        ViewBag.RefundedCount   = refunded.Count;
        ViewBag.RefundedAmount  = refunded.Sum(b => b.Amount);

        // Attendance: Confirmed bookings whose slot has already ended
        var now   = DateTime.Now;
        var ended = bookings.Where(b => b.Status == "Confirmed" && b.Date.ToDateTime(b.EndTime) <= now).ToList();

        int checkedIn = ended.Count(b => b.CheckedInAt != null);
        int noShows   = ended.Count(b => b.CheckedInAt == null);

        ViewBag.CheckedIn   = checkedIn;
        ViewBag.NoShows     = noShows;
        ViewBag.CheckInRate = ended.Count == 0 ? "-" : $"{100.0 * checkedIn / ended.Count:0}%";

        return View(vm);
    }
}
