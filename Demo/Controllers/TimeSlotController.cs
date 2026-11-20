using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Demo.Controllers;

[Authorize(Roles = "Admin,SuperAdmin")]
public class TimeSlotController(DB db) : Controller
{
    // GET: TimeSlot/Index
    public IActionResult Index(int? facilityId, DateOnly? date)
    {
        date ??= DateTime.Today.ToDateOnly();

        ViewBag.FacilityList = new SelectList(db.Facilities.OrderBy(f => f.Name), "Id", "Name", facilityId);
        ViewBag.Date = date.Value.ToString("yyyy-MM-dd");

        var m = db.TimeSlots
                  .Include(s => s.Facility)
                  .Include(s => s.Bookings)
                  .Where(s => s.Date == date)
                  .Where(s => facilityId == null || s.FacilityId == facilityId)
                  .OrderBy(s => s.Date)
                  .ThenBy(s => s.StartTime);

        return View(m);
    }

    // GET: TimeSlot/Insert
    public IActionResult Insert()
    {
        var vm = new TimeSlotVM
        {
            Date = DateTime.Today.ToDateOnly(),
            StartTime = new TimeOnly(8, 0),
        };

        ViewBag.FacilityList = new SelectList(db.Facilities.OrderBy(f => f.Name), "Id", "Name");
        return View(vm);
    }

    // POST: TimeSlot/Insert
    [HttpPost]
    public IActionResult Insert(TimeSlotVM vm)
    {
        var today = DateTime.Today.ToDateOnly();
        var now = DateTime.Now.ToTimeOnly();

        if (ModelState.IsValid("FacilityId") && !db.Facilities.Any(f => f.Id == vm.FacilityId))
        {
            ModelState.AddModelError("FacilityId", "Invalid Facility.");
        }

        if (ModelState.IsValid("Date") && vm.Date < today)
        {
            ModelState.AddModelError("Date", "Date out of range.");
        }

        if (ModelState.IsValid("StartTime") &&
            (vm.StartTime.Minute != 0 || vm.StartTime.Second != 0 ||
             vm.StartTime.Hour < 8 || vm.StartTime.Hour > 19))
        {
            ModelState.AddModelError("StartTime", "Start Time must be on the hour from 08:00 to 19:00.");
        }

        if (ModelState.IsValid("Date") && ModelState.IsValid("StartTime") &&
            vm.Date == today && vm.StartTime <= now)
        {
            ModelState.AddModelError("StartTime", "Start Time has already passed.");
        }

        if (ModelState.IsValid &&
            db.TimeSlots.Any(s => s.FacilityId == vm.FacilityId && s.Date == vm.Date && s.StartTime == vm.StartTime))
        {
            ModelState.AddModelError("StartTime", "This time slot already exists.");
        }

        if (ModelState.IsValid)
        {
            db.TimeSlots.Add(new()
            {
                FacilityId = vm.FacilityId,
                Date = vm.Date,
                StartTime = vm.StartTime,
                EndTime = vm.StartTime.AddHours(1),
                IsPeak = vm.IsPeak,
            });
            db.SaveChanges();

            TempData["Info"] = "Time slot inserted.";
            return RedirectToAction("Index", new { facilityId = vm.FacilityId, date = vm.Date.ToString("yyyy-MM-dd") });
        }

        ViewBag.FacilityList = new SelectList(db.Facilities.OrderBy(f => f.Name), "Id", "Name");
        return View(vm);
    }

    // GET: TimeSlot/Update
    public IActionResult Update(int? id)
    {
        var s = db.TimeSlots.Find(id);

        if (s == null)
        {
            return RedirectToAction("Index");
        }

        if (db.Bookings.Any(b => b.TimeSlotId == s.Id))
        {
            TempData["Info"] = "Cannot update a time slot that has bookings.";
            return RedirectToAction("Index");
        }

        var vm = new TimeSlotVM
        {
            Id = s.Id,
            FacilityId = s.FacilityId,
            Date = s.Date,
            StartTime = s.StartTime,
            IsPeak = s.IsPeak,
        };

        ViewBag.FacilityList = new SelectList(db.Facilities.OrderBy(f => f.Name), "Id", "Name");
        return View(vm);
    }

    // POST: TimeSlot/Update
    [HttpPost]
    public IActionResult Update(TimeSlotVM vm)
    {
        var s = db.TimeSlots.Find(vm.Id);

        if (s == null)
        {
            return RedirectToAction("Index");
        }

        if (db.Bookings.Any(b => b.TimeSlotId == s.Id))
        {
            TempData["Info"] = "Cannot update a time slot that has bookings.";
            return RedirectToAction("Index");
        }

        var today = DateTime.Today.ToDateOnly();
        var now = DateTime.Now.ToTimeOnly();

        if (ModelState.IsValid("FacilityId") && !db.Facilities.Any(f => f.Id == vm.FacilityId))
        {
            ModelState.AddModelError("FacilityId", "Invalid Facility.");
        }

        if (ModelState.IsValid("Date") && vm.Date < today)
        {
            ModelState.AddModelError("Date", "Date out of range.");
        }

        if (ModelState.IsValid("StartTime") &&
            (vm.StartTime.Minute != 0 || vm.StartTime.Second != 0 ||
             vm.StartTime.Hour < 8 || vm.StartTime.Hour > 19))
        {
            ModelState.AddModelError("StartTime", "Start Time must be on the hour from 08:00 to 19:00.");
        }

        if (ModelState.IsValid("Date") && ModelState.IsValid("StartTime") &&
            vm.Date == today && vm.StartTime <= now)
        {
            ModelState.AddModelError("StartTime", "Start Time has already passed.");
        }

        // Ignore the slot itself when checking for duplicates
        if (ModelState.IsValid &&
            db.TimeSlots.Any(t => t.FacilityId == vm.FacilityId && t.Date == vm.Date && t.StartTime == vm.StartTime && t.Id != vm.Id))
        {
            ModelState.AddModelError("StartTime", "This time slot already exists.");
        }

        if (ModelState.IsValid)
        {
            s.FacilityId = vm.FacilityId;
            s.Date = vm.Date;
            s.StartTime = vm.StartTime;
            s.EndTime = vm.StartTime.AddHours(1);
            s.IsPeak = vm.IsPeak;
            db.SaveChanges();

            TempData["Info"] = "Time slot updated.";
            return RedirectToAction("Index", new { facilityId = s.FacilityId, date = s.Date.ToString("yyyy-MM-dd") });
        }

        ViewBag.FacilityList = new SelectList(db.Facilities.OrderBy(f => f.Name), "Id", "Name");
        return View(vm);
    }

    // POST: TimeSlot/Delete
    [HttpPost]
    public IActionResult Delete(int? id)
    {
        var s = db.TimeSlots.Find(id);

        if (s == null)
        {
            return RedirectToAction("Index");
        }

        if (db.Bookings.Any(b => b.TimeSlotId == s.Id))
        {
            TempData["Info"] = "Cannot delete a time slot that has bookings.";
        }
        else
        {
            db.TimeSlots.Remove(s);
            db.SaveChanges();

            TempData["Info"] = "Time slot deleted.";
        }

        // Go back to the referring page only if it is on this site, otherwise to Index
        var referer = Request.Headers.Referer.ToString();
        if (Uri.TryCreate(referer, UriKind.Absolute, out var uri) &&
            uri.Authority == Request.Host.Value &&
            Url.IsLocalUrl(uri.PathAndQuery))
        {
            return Redirect(uri.PathAndQuery);
        }

        return RedirectToAction("Index", new { facilityId = s.FacilityId, date = s.Date.ToString("yyyy-MM-dd") });
    }
}
