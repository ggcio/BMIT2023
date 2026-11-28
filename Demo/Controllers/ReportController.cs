using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Demo.Controllers;

[Authorize(Roles = "admin")]
public class ReportController(DB db, Helper hp) : Controller
{
    // Fill in the defaults and work out the date range.
    // Returns false (with the error in ModelState) when the filter is invalid.
    private bool TryGetRange(ReportFilterVM vm, out DateOnly from, out DateOnly to)
    {
        var today = DateTime.Today.ToDateOnly();
        from = to = today;

        // Defaults: last 30 days, and this month for the month fields
        vm.From      ??= today.AddDays(-29);
        vm.To        ??= today;
        vm.FromMonth ??= today.Month;
        vm.FromYear  ??= today.Year;
        vm.ToMonth   ??= today.Month;
        vm.ToYear    ??= today.Year;

        // Unknown facility or category in the query string
        if (vm.FacilityId != null && !db.Facilities.Any(f => f.Id == vm.FacilityId))
        {
            ModelState.AddModelError("FacilityId", "Invalid Facility.");
        }

        if (vm.CategoryId != null && !db.Categories.Any(c => c.Id == vm.CategoryId))
        {
            ModelState.AddModelError("CategoryId", "Invalid Category.");
        }

        // Invalid field, e.g. month 13
        if (!ModelState.IsValid)
        {
            return false;
        }

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
            return false;
        }

        return true;
    }

    // Facilities that match both the facility and the category filter
    private List<int> FacilityIds(ReportFilterVM vm)
    {
        return db.Facilities
                 .Where(f => (vm.FacilityId == null || f.Id == vm.FacilityId) &&
                             (vm.CategoryId == null || f.CategoryId == vm.CategoryId))
                 .Select(f => f.Id)
                 .ToList();
    }

    // CSV value: quote it when it has a comma, quote or line break, doubling any quotes
    private static string Csv(string? value)
    {
        value ??= "";

        if (value.IndexOfAny([',', '"', '\r', '\n']) >= 0)
        {
            value = "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        return value;
    }

    // GET: Report/Export (same query fields as Index)
    public IActionResult Export(ReportFilterVM vm)
    {
        if (!TryGetRange(vm, out var from, out var to))
        {
            TempData["Info"] = string.Join(" ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
            return Redirect("/Report" + Request.QueryString);
        }

        var facilityIds = FacilityIds(vm);

        var bookings = db.Bookings
                         .Include(b => b.User)
                         .Include(b => b.TimeSlot)
                         .ThenInclude(s => s.Facility)
                         .ThenInclude(f => f.Category)
                         .Include(b => b.Payment)
                         .Where(b => facilityIds.Contains(b.TimeSlot.FacilityId) &&
                                     b.TimeSlot.Date >= from &&
                                     b.TimeSlot.Date <= to)
                         .OrderBy(b => b.TimeSlot.Date)
                         .ThenBy(b => b.TimeSlot.StartTime)
                         .ThenBy(b => b.Id)
                         .ToList();

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Booking ID,Member,Email,Member Type,Facility,Category,Date,Start,End,Peak,Price,Status,Refund Status,Amount Paid,Paid At,Checked In At");

        foreach (var b in bookings)
        {
            var paid = b.Payment?.Status == "Paid";

            string[] row =
            [
                b.Id.ToString(),
                b.User.Name,
                b.User.Email,
                b.User.Role == UserRole.student ? "Student" : "Public",
                b.TimeSlot.Facility.Name,
                b.TimeSlot.Facility.Category.Name,
                b.TimeSlot.Date.ToString("yyyy-MM-dd"),
                b.TimeSlot.StartTime.ToString("HH:mm"),
                b.TimeSlot.EndTime.ToString("HH:mm"),
                b.TimeSlot.IsPeak ? "Yes" : "No",
                b.Price.ToString("0.00"),
                b.Status,
                b.RefundStatus,
                paid ? b.Payment!.Amount.ToString("0.00") : "",
                b.Payment?.PaidAt?.ToString("yyyy-MM-dd HH:mm") ?? "",
                b.CheckedInAt?.ToString("yyyy-MM-dd HH:mm") ?? "",
            ];

            sb.AppendLine(string.Join(",", row.Select(Csv)));
        }

        // UTF-8 with a BOM so Excel reads the text correctly
        var encoding = new System.Text.UTF8Encoding(true);
        var bytes = encoding.GetPreamble().Concat(encoding.GetBytes(sb.ToString())).ToArray();

        return File(bytes, "text/csv", $"bookings_{from:yyyyMMdd}_{to:yyyyMMdd}.csv");
    }

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

        // (1) Work out the date range (invalid filter: show the form with errors and no data)
        if (!TryGetRange(vm, out var from, out var to))
        {
            return View(vm);
        }

        ViewBag.HasData = true;
        ViewBag.RangeText = $"{from:dd MMM yyyy} to {to:dd MMM yyyy}";

        var facilityIds = FacilityIds(vm);

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

        // (3) Chart 2: paid, not refunded revenue, by PaidAt, grouped --------
        var start = from.ToDateTime(TimeOnly.MinValue);
        var end   = to.AddDays(1).ToDateTime(TimeOnly.MinValue);

        var payments = db.Payments
                         .Where(p => p.Status == "Paid" &&
                                     p.Booking.RefundStatus == "None" &&
                                     facilityIds.Contains(p.Booking.TimeSlot.FacilityId) &&
                                     p.PaidAt >= start &&
                                     p.PaidAt < end)
                         .Select(p => new
                         {
                             p.Amount,
                             PaidDate = p.PaidAt!.Value.Date,
                             p.Booking.TimeSlot.FacilityId,
                             p.Booking.TimeSlot.Facility.CategoryId,
                             p.Booking.TimeSlot.IsPeak,
                         })
                         .ToList();

        // Start of the period a date belongs to (weeks start on Monday)
        DateOnly PeriodOf(DateOnly d) => vm.GroupBy switch
        {
            "Week"  => d.AddDays(-(((int)d.DayOfWeek + 6) % 7)),
            "Month" => new DateOnly(d.Year, d.Month, 1),
            _       => d,
        };

        // Every period from the one holding From up to To
        var periods = new List<DateOnly>();
        for (var p = PeriodOf(from); p <= to; p = vm.GroupBy == "Month" ? p.AddMonths(1) : p.AddDays(vm.GroupBy == "Week" ? 7 : 1))
        {
            periods.Add(p);
        }

        var labelFormat = vm.GroupBy == "Month" ? "MMM yyyy" : "dd MMM";

        ViewBag.RevenueTitle  = $"Revenue (RM) per {vm.GroupBy}";
        ViewBag.RevenueLabels = periods.Select(p => p.ToString(labelFormat)).ToList();
        ViewBag.RevenueValues = periods.Select(p => payments.Where(x => PeriodOf(DateOnly.FromDateTime(x.PaidDate)) == p)
                                                            .Sum(x => x.Amount))
                                       .ToList();

        // Revenue by facility and by category (every one shown, 0 if none)
        var facilityNames = db.Facilities
                              .Where(f => facilityIds.Contains(f.Id))
                              .OrderBy(f => f.Id)
                              .Select(f => new { f.Id, f.Name })
                              .ToList();

        ViewBag.FacilityRevenueLabels = facilityNames.Select(f => f.Name).ToList();
        ViewBag.FacilityRevenueValues = facilityNames.Select(f => payments.Where(x => x.FacilityId == f.Id).Sum(x => x.Amount)).ToList();

        var categories = db.Categories
                           .Where(c => (vm.CategoryId == null || c.Id == vm.CategoryId) &&
                                       (vm.FacilityId == null || c.Facilities.Any(f => f.Id == vm.FacilityId)))
                           .OrderBy(c => c.Id)
                           .Select(c => new { c.Id, c.Name })
                           .ToList();

        ViewBag.CategoryRevenueLabels = categories.Select(c => c.Name).ToList();
        ViewBag.CategoryRevenueValues = categories.Select(c => payments.Where(x => x.CategoryId == c.Id).Sum(x => x.Amount)).ToList();

        // (4) Bookings by slot date (summary cards and booking charts) -------
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
                             b.TimeSlot.StartTime,
                             b.TimeSlot.EndTime,
                             b.TimeSlot.IsPeak,
                             b.User.Role,
                             Amount = b.Payment == null ? 0 : b.Payment.Amount,
                         })
                         .ToList();

        var confirmed = bookings.Where(b => b.Status == "Confirmed").ToList();

        // Peak vs off-peak: confirmed bookings and revenue
        ViewBag.PeakBookingValues = new[] { confirmed.Count(b => b.IsPeak), confirmed.Count(b => !b.IsPeak) };
        ViewBag.PeakRevenueValues = new[] { payments.Where(x => x.IsPeak).Sum(x => x.Amount), payments.Where(x => !x.IsPeak).Sum(x => x.Amount) };

        // Student vs public: confirmed bookings by the user's role
        ViewBag.RoleValues = new[] { confirmed.Count(b => b.Role == UserRole.student), confirmed.Count(b => b.Role == UserRole.@public) };

        // Busiest hours: confirmed bookings by start time, 08:00 to 19:00
        var hours = Enumerable.Range(8, 12).ToList();
        ViewBag.HourLabels = hours.Select(h => $"{h:00}:00").ToList();
        ViewBag.HourValues = hours.Select(h => confirmed.Count(b => b.StartTime.Hour == h)).ToList();

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

        // (5) Lists: no-shows and refunds, same bookings as the cards --------
        var listed = db.Bookings
                       .Include(b => b.User)
                       .Include(b => b.TimeSlot)
                       .ThenInclude(s => s.Facility)
                       .Include(b => b.Payment)
                       .Where(b => facilityIds.Contains(b.TimeSlot.FacilityId) &&
                                   b.TimeSlot.Date >= from &&
                                   b.TimeSlot.Date <= to &&
                                   ((b.Status == "Confirmed" && b.CheckedInAt == null) ||
                                    b.RefundStatus == "RefundDue" || b.RefundStatus == "Refunded"))
                       .OrderByDescending(b => b.TimeSlot.Date)
                       .ThenByDescending(b => b.TimeSlot.StartTime)
                       .ToList();

        // No-shows: Confirmed, slot already ended, never checked in (latest first)
        ViewBag.NoShowList = listed.Where(b => b.Status == "Confirmed" &&
                                               b.CheckedInAt == null &&
                                               b.TimeSlot.Date.ToDateTime(b.TimeSlot.EndTime) <= now)
                                   .ToList();

        // Refunds: Refund due first, then Refunded, latest first within each
        ViewBag.RefundList = listed.Where(b => b.RefundStatus == "RefundDue" || b.RefundStatus == "Refunded")
                                   .OrderBy(b => b.RefundStatus == "RefundDue" ? 0 : 1)
                                   .ToList();

        return View(vm);
    }
}
