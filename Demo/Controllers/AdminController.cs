using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Demo.Controllers;

[Authorize(Roles = "admin")]
public class AdminController(DB db) : Controller
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
}
