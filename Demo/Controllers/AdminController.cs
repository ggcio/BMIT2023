using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Demo.Controllers;

[Authorize(Roles = "admin")]
public class AdminController(DB db, Helper hp, IConfiguration config) : Controller
{
    // Same rule as Register: a student email domain gives role student, any other email gives public
    private bool IsStudentEmail(string email)
    {
        var domain = config["UniversityEmailDomain"]?.Trim().TrimStart('@') ?? "student.tarc.edu.my";
        var atIndex = email.LastIndexOf('@');

        return atIndex > 0 &&
               email[(atIndex + 1)..].Equals(domain, StringComparison.OrdinalIgnoreCase);
    }

    // GET: Admin/Insert
    public IActionResult Insert()
    {
        return View();
    }

    // POST: Admin/Insert
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Insert(AdminUserInsertVM vm)
    {
        var email = vm.Email?.Trim() ?? "";

        if (ModelState.IsValid("Email") && db.Users.Any(u => u.Email == email))
        {
            ModelState.AddModelError("Email", "Duplicated Email.");
        }

        if (ModelState.IsValid)
        {
            db.Users.Add(new()
            {
                Email = email,
                Password = hp.HashPassword(vm.Password),
                Name = vm.Name,
                Role = IsStudentEmail(email) ? UserRole.student : UserRole.@public,
                EmailVerified = true,
            });
            db.SaveChanges();

            TempData["Info"] = "User created.";
            return RedirectToAction(nameof(Users));
        }

        return View(vm);
    }

    // GET: Admin/Update?email=
    public IActionResult Update(string? email)
    {
        var user = db.Users.Find(email);

        if (user == null)
        {
            TempData["Info"] = "User account not found.";
            return RedirectToAction(nameof(Users));
        }

        if (user.Role == UserRole.admin)
        {
            TempData["Info"] = "Admin accounts cannot be changed here.";
            return RedirectToAction(nameof(Users));
        }

        return View(new AdminUserUpdateVM { Email = user.Email, Name = user.Name });
    }

    // POST: Admin/Update
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Update(AdminUserUpdateVM vm)
    {
        // The form posts back to /Admin/Update?email=..., so the hidden Email must match the page it came from
        var user = vm.Email == Request.Query["email"].ToString() ? db.Users.Find(vm.Email) : null;

        if (user == null)
        {
            TempData["Info"] = "User account not found.";
            return RedirectToAction(nameof(Users));
        }

        if (user.Role == UserRole.admin)
        {
            TempData["Info"] = "Admin accounts cannot be changed here.";
            return RedirectToAction(nameof(Users));
        }

        if (ModelState.IsValid)
        {
            user.Name = vm.Name;
            db.SaveChanges();

            TempData["Info"] = "User updated.";
            return RedirectToAction(nameof(Users));
        }

        return View(vm);
    }

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
            // Keep the file if another user still uses the same photo
            if (user.PhotoURL != null &&
                !db.Users.Any(u => u.PhotoURL == user.PhotoURL && u.Email != user.Email))
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
