using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Demo.Controllers;

[Authorize(Roles = "admin")]
public class AdminController(DB db, Helper hp) : Controller
{
    // GET: Admin/Users
    public IActionResult Users()
    {
        var users = db.Users
            .OrderBy(u => u.Email)
            .ToList();

        return View(users);
    }

    // POST: Admin/Unblock
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Unblock(string email)
    {
        var user = db.Users.Find(email);

        if (user == null)
        {
            TempData["Info"] = "User account not found.";
            return RedirectToAction(nameof(Users));
        }

        user.IsBlocked = false;
        user.FailedAttempts = 0;
        user.LockedUntil = null;
        db.SaveChanges();

        TempData["Info"] = $"{user.Email} has been unblocked.";
        return RedirectToAction(nameof(Users));
    }

    // POST: Admin/Block
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Block(string email)
    {
        var user = db.Users.Find(email);

        if (user == null)
        {
            TempData["Info"] = "User account not found.";
        }
        else if (user.Role == UserRole.admin)
        {
            TempData["Info"] = "Admin accounts cannot be changed here.";
        }
        else
        {
            user.IsBlocked = true;
            db.SaveChanges();

            TempData["Info"] = $"{user.Email} has been blocked.";
        }

        return RedirectToAction(nameof(Users));
    }

    // POST: Admin/Delete
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Delete(string email)
    {
        var user = db.Users.Find(email);

        if (user == null)
        {
            TempData["Info"] = "User account not found.";
        }
        else if (user.Role == UserRole.admin)
        {
            TempData["Info"] = "Admin accounts cannot be changed here.";
        }
        else if (db.Bookings.Any(b => b.MemberEmail == user.Email))
        {
            TempData["Info"] = "This user has bookings and cannot be deleted. Block the account instead.";
        }
        else
        {
            if (user.PhotoURL != null)
            {
                hp.DeletePhoto(user.PhotoURL, "photos");
            }

            db.Users.Remove(user);
            db.SaveChanges();

            TempData["Info"] = $"{user.Email} has been deleted.";
        }

        return RedirectToAction(nameof(Users));
    }
}
