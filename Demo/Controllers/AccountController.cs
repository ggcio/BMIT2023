using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text.Json;

namespace Demo.Controllers;

public class AccountController(DB db,
                               Helper hp,
                               IConfiguration config) : Controller
{
    private static readonly HttpClient http = new() { Timeout = TimeSpan.FromSeconds(10) };

    // True if Google confirms the reCAPTCHA response sent with the form
    private async Task<bool> IsCaptchaValid()
    {
        var response = Request.Form["g-recaptcha-response"].ToString();

        if (string.IsNullOrWhiteSpace(response))
        {
            return false;
        }

        var data = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["secret"] = config["Recaptcha:SecretKey"] ?? "",
            ["response"] = response,
            ["remoteip"] = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "",
        });

        try
        {
            var result = await http.PostAsync("https://www.google.com/recaptcha/api/siteverify", data);

            if (!result.IsSuccessStatusCode)
            {
                return false;
            }

            var json = await result.Content.ReadAsStringAsync();

            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.TryGetProperty("success", out var success) &&
                   success.ValueKind == JsonValueKind.True &&
                   success.GetBoolean();
        }
        catch
        {
            // Network error, timeout or an unexpected reply: treat as not verified
            return false;
        }
    }

    private string StudentEmailDomain =>
        config["UniversityEmailDomain"]?.Trim().TrimStart('@') ?? "student.tarc.edu.my";

    private bool IsStudentEmail(string email)
    {
        var atIndex = email.LastIndexOf('@');

        return atIndex > 0 &&
               email[(atIndex + 1)..].Equals(StudentEmailDomain, StringComparison.OrdinalIgnoreCase);
    }

    private static void AddLockoutError(User user, ModelStateDictionary modelState)
    {
        if (user.IsBlocked)
        {
            modelState.AddModelError("", "This account is permanently blocked. Please contact an administrator.");
            return;
        }

        if (user.LockedUntil.HasValue && user.LockedUntil.Value > DateTime.UtcNow)
        {
            var seconds = Math.Max(1, (int)Math.Ceiling((user.LockedUntil.Value - DateTime.UtcNow).TotalSeconds));
            modelState.AddModelError("", $"Too many failed attempts. Try again in {seconds} seconds.");
        }
    }

    private static string CreateVerificationToken()
    {
        return Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    }

    private static void ApplyFailedLogin(User user)
    {
        user.FailedAttempts++;

        // Lock for 1 minute on the 3rd failure, then start counting again
        if (user.FailedAttempts >= 3)
        {
            user.LockedUntil = DateTime.UtcNow.AddMinutes(1);
            user.FailedAttempts = 0;
        }
    }

    // GET: Account/Login
    public IActionResult Login()
    {
        return View();
    }

    // POST: Account/Login
    [HttpPost]
    public async Task<IActionResult> Login(LoginVM vm, string? returnURL)
    {
        var captchaValid = await IsCaptchaValid();
        var email = vm.Email.Trim();
        var u = db.Users.Find(email);

        // Captcha first, so a missing captcha is not counted as a failed login
        if (!captchaValid)
        {
            ModelState.AddModelError("", "Please confirm you are not a robot.");
        }
        else if (u != null && (u.IsBlocked || u.LockedUntil > DateTime.UtcNow))
        {
            AddLockoutError(u, ModelState);
        }
        else if (u == null ||
                 !hp.VerifyPassword(u.Password, vm.Password) ||
                 (u.Role == UserRole.student && !IsStudentEmail(u.Email)))
        {
            if (u != null)
            {
                ApplyFailedLogin(u);
                db.SaveChanges();
            }

            // The 3rd failure locks the account, so show the lockout message straight away
            if (u != null && u.LockedUntil > DateTime.UtcNow)
            {
                AddLockoutError(u, ModelState);
            }
            else
            {
                ModelState.AddModelError("", "Login credentials not matched.");
            }
        }
        else if (!u.EmailVerified)
        {
            ModelState.AddModelError("", "Please verify your email before logging in.");
        }

        if (ModelState.IsValid)
        {
            u!.FailedAttempts = 0;
            u.LockedUntil = null;
            db.SaveChanges();

            TempData["Info"] = "Login successfully.";

            await hp.SignIn(u!.Email, u.Role.ToString(), vm.RememberMe);
            
            if (string.IsNullOrEmpty(returnURL))
            {
                return RedirectToAction("Index", "Home");
            }
        }
        
        if (u?.LockedUntil is DateTime lockedUntil && lockedUntil > DateTime.UtcNow)
        {
            ViewBag.LockoutSeconds = Math.Max(1, (int)Math.Ceiling((lockedUntil - DateTime.UtcNow).TotalSeconds));
        }

        return View(vm);
    }

    // GET: Account/Logout
    public async Task<IActionResult> Logout(string? returnURL)
    {
        TempData["Info"] = "Logout successfully.";

        await hp.SignOut();

        return RedirectToAction("Index", "Home");
    }

    // GET: Account/AccessDenied
    public IActionResult AccessDenied(string? returnURL)
    {
        return View();
    }



    // ------------------------------------------------------------------------
    // Others
    // ------------------------------------------------------------------------

    // GET: Account/CheckEmail
    public bool CheckEmail(string email)
    {
        email = email.Trim();
        return !db.Users.Any(u => u.Email == email);
    }

    // GET: Account/Register
    public IActionResult Register()
    {
        return View();
    }

    // POST: Account/Register
    [HttpPost]
    public async Task<IActionResult> Register(RegisterVM vm)
    {
        if (!await IsCaptchaValid())
        {
            ModelState.AddModelError("", "Please confirm you are not a robot.");
        }

        var email = vm.Email.Trim();

        if (ModelState.IsValid("Email") &&
            db.Users.Any(u => u.Email == email))
        {
            ModelState.AddModelError("Email", "Duplicated Email.");
        }

        if (ModelState.IsValid("Photo"))
        {
            var err = hp.ValidatePhoto(vm.Photo);
            if (err != "") ModelState.AddModelError("Photo", err);
        }
        
        if (ModelState.IsValid)
        {
            var token = CreateVerificationToken();
            var user = new User
            {
                Email = email,
                Password = hp.HashPassword(vm.Password),
                Name = vm.Name,
                PhotoURL = vm.Photo == null ? null : hp.SavePhoto(vm.Photo, "photos"),
                Role = IsStudentEmail(email) ? UserRole.student : UserRole.@public,
                EmailVerified = false,
                VerifyToken = token,
                VerifyExpiresAt = DateTime.UtcNow.AddHours(24),
            };

            db.Users.Add(user);
            db.SaveChanges();

            try
            {
                SendVerificationEmail(user, token);
                TempData["Info"] = "Registration successful. Check your email to verify your account.";
            }
            catch
            {
                TempData["Info"] = "Registration successful, but the verification email could not be sent. Use Resend Verification.";
            }

            return RedirectToAction("Login");
        }

        return View(vm);
    }

    // GET: Account/VerifyEmail
    public IActionResult VerifyEmail(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            ViewBag.Message = "The verification link is invalid.";
            return View();
        }

        var user = db.Users.SingleOrDefault(u => u.VerifyToken == token);

        if (user == null || user.VerifyExpiresAt is not DateTime expiresAt || expiresAt <= DateTime.UtcNow)
        {
            ViewBag.Message = "The verification link is invalid or has expired.";
            return View();
        }

        user.EmailVerified = true;
        user.VerifyToken = null;
        user.VerifyExpiresAt = null;
        db.SaveChanges();

        ViewBag.Message = "Your email has been verified. You can now log in.";
        return View();
    }

    // GET: Account/ResendVerification
    public IActionResult ResendVerification()
    {
        return View();
    }

    // POST: Account/ResendVerification
    [HttpPost]
    public IActionResult ResendVerification(ResendVerificationVM vm)
    {
        if (!ModelState.IsValid)
        {
            return View(vm);
        }

        var email = vm.Email.Trim();
        var user = db.Users.Find(email);

        if (user != null && !user.EmailVerified)
        {
            var token = CreateVerificationToken();
            user.VerifyToken = token;
            user.VerifyExpiresAt = DateTime.UtcNow.AddHours(24);
            db.SaveChanges();

            try
            {
                SendVerificationEmail(user, token);
            }
            catch
            {
                TempData["Info"] = "The verification email could not be sent. Please try again later.";
                return RedirectToAction(nameof(Login));
            }
        }

        TempData["Info"] = "If the account requires verification, a new verification email has been sent.";
        return RedirectToAction(nameof(Login));
    }

    // GET: Account/UpdatePassword
    [Authorize]
    public IActionResult UpdatePassword()
    {
        return View();
    }

    // POST: Account/UpdatePassword
    [Authorize]
    [HttpPost]
    public IActionResult UpdatePassword(UpdatePasswordVM vm)
    {
        var u = db.Users.Find(User.Identity!.Name);
        if (u == null) return RedirectToAction("Index", "Home");

        if (!hp.VerifyPassword(u.Password, vm.Current))
        {
            ModelState.AddModelError("Current", "Current Password not matched.");
        }

        if (ModelState.IsValid)
        {
            u.Password = hp.HashPassword(vm.New);
            db.SaveChanges();

            TempData["Info"] = "Password updated.";
            return RedirectToAction();
        }

        return View();
    }

    // GET: Account/UpdateProfile
    [Authorize(Roles = "student,public")]
    public IActionResult UpdateProfile()
    {
        var m = db.Users.Find(User.Identity!.Name);
        if (m == null) return RedirectToAction("Index", "Home");

        var vm = new UpdateProfileVM
        {
            Email = m.Email,
            Name = m.Name,
            PhotoURL = m.PhotoURL,
        };

        return View(vm);
    }

    // POST: Account/UpdateProfile
    [Authorize(Roles = "student,public")]
    [HttpPost]
    public IActionResult UpdateProfile(UpdateProfileVM vm)
    {
        var m = db.Users.Find(User.Identity!.Name);
        if (m == null) return RedirectToAction("Index", "Home");

        if (vm.Photo != null)
        {
            var err = hp.ValidatePhoto(vm.Photo);
            if (err != "") ModelState.AddModelError("Photo", err);
        }

        if (ModelState.IsValid)
        {
            m.Name = vm.Name;

            if (vm.Photo != null)
            {
                if (m.PhotoURL != null)
                {
                    hp.DeletePhoto(m.PhotoURL, "photos");
                }

                m.PhotoURL = hp.SavePhoto(vm.Photo, "photos");
            }

            db.SaveChanges();

            TempData["Info"] = "Profile updated.";
            return RedirectToAction();
        }

        vm.Email = m.Email;
        vm.PhotoURL = m.PhotoURL;
        return View(vm);
    }

    // GET: Account/ResetPassword
    public IActionResult ResetPassword()
    {
        return View();
    }

    // POST: Account/ResetPassword
    [HttpPost]
    public IActionResult ResetPassword(ResetPasswordVM vm)
    {
        if (ModelState.IsValid)
        {
            var email = vm.Email.Trim();
            var user = db.Users.Find(email);

            if (user != null)
            {
                var token = CreateVerificationToken();
                user.ResetToken = token;
                user.ResetExpiresAt = DateTime.UtcNow.AddHours(1);
                db.SaveChanges();

                try
                {
                    SendPasswordResetEmail(user, token);
                }
                catch
                {
                    TempData["Info"] = "The reset email could not be sent. Please try again later.";
                    return RedirectToAction(nameof(Login));
                }
            }

            TempData["Info"] = "If the account exists, a password reset email has been sent.";
            return RedirectToAction(nameof(Login));
        }

        return View(vm);
    }

    // GET: Account/ResetPasswordWithToken
    public IActionResult ResetPasswordWithToken(string? token)
    {
        var vm = new ResetPasswordTokenVM { Token = token ?? "" };
        var user = FindValidResetUser(vm.Token);

        if (user == null)
        {
            ViewBag.Message = "The password reset link is invalid or has expired.";
        }

        return View(vm);
    }

    // POST: Account/ResetPasswordWithToken
    [HttpPost]
    public IActionResult ResetPasswordWithToken(ResetPasswordTokenVM vm)
    {
        var user = FindValidResetUser(vm.Token);

        if (user == null)
        {
            ModelState.AddModelError("", "The password reset link is invalid or has expired.");
        }

        if (ModelState.IsValid)
        {
            user!.Password = hp.HashPassword(vm.New);
            user.ResetToken = null;
            user.ResetExpiresAt = null;
            db.SaveChanges();

            TempData["Info"] = "Password reset successfully. You can now log in.";
            return RedirectToAction(nameof(Login));
        }

        return View(vm);
    }

    private User? FindValidResetUser(string token)
    {
        if (string.IsNullOrWhiteSpace(token)) return null;

        return db.Users.SingleOrDefault(u =>
            u.ResetToken == token &&
            u.ResetExpiresAt.HasValue &&
            u.ResetExpiresAt.Value > DateTime.UtcNow);
    }

    private void SendPasswordResetEmail(User user, string token)
    {
        var url = Url.Action("ResetPasswordWithToken", "Account", new { token }, Request.Scheme);
        var name = System.Net.WebUtility.HtmlEncode(user.Name);

        using var mail = new MailMessage();
        mail.To.Add(new MailAddress(user.Email, user.Name));
        mail.Subject = "Reset your password";
        mail.IsBodyHtml = true;
        mail.Body = $@"
            <p>Dear {name},</p>
            <p>Use the link below to choose a new password:</p>
            <p><a href='{url}'>Reset password</a></p>
            <p>This link expires in one hour and can only be used once.</p>
        ";

        hp.SendEmail(mail);
    }

    private void SendVerificationEmail(User user, string token)
    {
        var url = Url.Action("VerifyEmail", "Account", new { token }, Request.Scheme);
        var name = System.Net.WebUtility.HtmlEncode(user.Name);

        using var mail = new MailMessage();
        mail.To.Add(new MailAddress(user.Email, user.Name));
        mail.Subject = "Verify your email address";
        mail.IsBodyHtml = true;
        mail.Body = $@"
            <p>Dear {name},</p>
            <p>Please verify your email address by clicking the link below:</p>
            <p><a href='{url}'>Verify email address</a></p>
            <p>This link expires in 24 hours.</p>
        ";

        hp.SendEmail(mail);
    }
}
