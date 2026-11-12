using System.Security.Claims;
using Project.Data;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Project.Models;
using Project.Services;
using GenUser = Project.Models.Entities.User;
using GenPatientProfile = Project.Models.Entities.PatientProfile;
using GenUserBackupCode = Project.Models.Entities.UserBackupCode;
using GenPasswordResetToken = Project.Models.Entities.PasswordResetToken;

namespace Project.Controllers
{
    public class AccountController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly EmailService _emailService;

        public AccountController(ApplicationDbContext context, EmailService emailService)
        {
            _context = context;
            _emailService = emailService;
        }

        // GET: /Account/Login
        [HttpGet]
        public IActionResult Login()
        {
            return View();
        }

        // POST: /Account/Login
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(string email, string password, bool rememberMe = false)
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                TempData["Error"] = "Please enter both your email and password.";
                return RedirectToAction("Login");
            }

            var user = _context.Users.Include(u => u.Role).FirstOrDefault(u => u.Email == email.Trim());

            // Deliberately identical error for "no such user" and "wrong password" -
            // don't leak which emails are registered.
            if (user == null || user.IsArchived)
            {
                TempData["Error"] = "Invalid email or password.";
                return RedirectToAction("Login");
            }

            if (!string.Equals(user.Status, "Active", StringComparison.OrdinalIgnoreCase))
            {
                TempData["Error"] = "This account is not active. Contact your administrator.";
                return RedirectToAction("Login");
            }

            var hasher = new PasswordHasher<GenUser>();
            var verification = hasher.VerifyHashedPassword(user, user.PasswordHash, password);
            if (verification == PasswordVerificationResult.Failed)
            {
                TempData["Error"] = "Invalid email or password.";
                return RedirectToAction("Login");
            }

            // Password is correct. If this account has real TOTP 2FA enabled,
            // do NOT sign them in yet - hand off to a short-lived "pending"
            // cookie scheme instead, and make them prove the second factor
            // before CompleteSignIn issues the real auth cookie.
            var twoFactor = _context.UserTwoFactors.FirstOrDefault(t => t.UserId == user.UserId && t.IsEnabled);
            if (twoFactor != null)
            {
                var pendingClaims = new List<Claim>
                {
                    new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
                    new Claim("RememberMe", rememberMe ? "true" : "false")
                };
                var pendingIdentity = new ClaimsIdentity(pendingClaims, "TwoFactorPending");
                await HttpContext.SignInAsync("TwoFactorPending", new ClaimsPrincipal(pendingIdentity),
                    new AuthenticationProperties { ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(5) });

                return RedirectToAction("VerifyTwoFactor");
            }

            await CompleteSignIn(user, rememberMe);

            return user.Role.Code switch
            {
                "SuperAdmin" => RedirectToAction("Index", "Admin"),
                "Admin" => RedirectToAction("Index", "Admin"),
                "Doctor" => RedirectToAction("Index", "Doctor"),
                "LabManager" => RedirectToAction("Index", "Dashboard"),
                "Technician" => RedirectToAction("Index", "LabTech"),
                "Patient" => RedirectToAction("Index", "Patient"),
                _ => RedirectToAction("Login")
            };
        }

        // Issues the REAL auth cookie. Shared by the direct (no 2FA) path above
        // and by VerifyTwoFactor once a correct code/backup code is supplied.
        private async Task CompleteSignIn(GenUser user, bool rememberMe)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Name, $"{user.FirstName} {user.Surname}"),
                new Claim(ClaimTypes.Role, user.Role.Code)
            };
            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal,
                new AuthenticationProperties
                {
                    IsPersistent = rememberMe,
                    ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8)
                });
            LogPatientEvent(user.UserId, user.Role.Code, "Signed in", "You signed in to your account.", "bi-box-arrow-in-right");
        }

        // Adds a line to a patient's own activity log (does nothing for staff accounts).
        private void LogPatientEvent(int userId, string roleCode, string action, string detail, string icon)
        {
            if (roleCode != "Patient") return;
            _context.PatientActivityLogs.Add(new Project.Models.Entities.PatientActivityLog
            {
                PatientUserId = userId, Action = action, Detail = detail, Icon = icon, LogTypeId = 1
            });
            _context.SaveChanges();
        }

        // GET: /Account/VerifyTwoFactor
        // Reached only after the password has already been verified (Login
        // signs the "TwoFactorPending" scheme, never the real one, for that).
        [HttpGet]
        public async Task<IActionResult> VerifyTwoFactor()
        {
            var pending = await HttpContext.AuthenticateAsync("TwoFactorPending");
            if (!pending.Succeeded || pending.Principal == null)
            {
                TempData["Error"] = "Your session expired. Please log in again.";
                return RedirectToAction("Login");
            }
            return View(new VerifyTwoFactorViewModel());
        }

        // POST: /Account/VerifyTwoFactor
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> VerifyTwoFactor(VerifyTwoFactorViewModel model)
        {
            var pending = await HttpContext.AuthenticateAsync("TwoFactorPending");
            if (!pending.Succeeded || pending.Principal == null)
            {
                TempData["Error"] = "Your session expired. Please log in again.";
                return RedirectToAction("Login");
            }

            var userId = int.Parse(pending.Principal.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var rememberMe = pending.Principal.HasClaim(c => c.Type == "RememberMe" && c.Value == "true");

            var user = _context.Users.Include(u => u.Role).FirstOrDefault(u => u.UserId == userId);
            var twoFactor = _context.UserTwoFactors.FirstOrDefault(t => t.UserId == userId && t.IsEnabled);
            if (user == null || twoFactor == null)
            {
                TempData["Error"] = "Your session expired. Please log in again.";
                return RedirectToAction("Login");
            }

            var code = (model.Code ?? "").Trim();
            var isValid = false;

            if (code.Length == 6 && code.All(char.IsDigit))
            {
                isValid = TotpService.ValidateCode(twoFactor.SecretKey, code);
                if (isValid) twoFactor.LastVerifiedOn = DateTime.UtcNow;
            }
            else
            {
                // Treat anything else as an attempted backup code.
                var hash = TotpService.HashBackupCode(code);
                var backupCode = _context.UserBackupCodes
                    .FirstOrDefault(b => b.UserId == userId && b.CodeHash == hash && b.UsedOn == null);
                if (backupCode != null)
                {
                    backupCode.UsedOn = DateTime.UtcNow;
                    isValid = true;
                }
            }

            if (!isValid)
            {
                TempData["Error"] = "That code wasn't recognized. Check your authenticator app, or use one of your backup codes.";
                return View(model);
            }

            _context.SaveChanges();
            await HttpContext.SignOutAsync("TwoFactorPending");
            await CompleteSignIn(user, rememberMe);

            return user.Role.Code switch
            {
                "SuperAdmin" => RedirectToAction("Index", "Admin"),
                "Admin" => RedirectToAction("Index", "Admin"),
                "Doctor" => RedirectToAction("Index", "Doctor"),
                "LabManager" => RedirectToAction("Index", "Dashboard"),
                "Technician" => RedirectToAction("Index", "LabTech"),
                "Patient" => RedirectToAction("Index", "Patient"),
                _ => RedirectToAction("Login")
            };
        }

        // GET: /Account/Register
        [HttpGet]
        public IActionResult Register()
        {
            return View();
        }

        // GET: /Account/AccessDenied
        // A signed-in user opened a page their role is not allowed to see: send them back to
        // their own home page with a message instead of the login screen.
        [HttpGet]
        public IActionResult AccessDenied()
        {
            if (User.Identity?.IsAuthenticated != true) return RedirectToAction("Login");
            var role = User.FindFirst(ClaimTypes.Role)?.Value;
            // The admin layout shows "DupError" as a toast on every page; other portals show "Error"
            TempData[role == "Admin" || role == "SuperAdmin" ? "DupError" : "Error"] = "You do not have permission to open that page.";
            return role switch
            {
                "SuperAdmin" or "Admin" => RedirectToAction("Index", "Admin"),
                "Doctor" => RedirectToAction("Index", "Doctor"),
                "LabManager" => RedirectToAction("Index", "Dashboard"),
                "Technician" => RedirectToAction("Index", "LabTech"),
                "Patient" => RedirectToAction("Index", "Patient"),
                _ => RedirectToAction("Login")
            };
        }

        // GET: /Account/Logout
        [HttpGet]
        public async Task<IActionResult> Logout()
        {
            if (User.Identity?.IsAuthenticated == true && int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var outId))
                LogPatientEvent(outId, User.FindFirstValue(ClaimTypes.Role) ?? "", "Signed out", "You signed out of your account.", "bi-box-arrow-right");
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Login");
        }

        // GET: /Account/ForgotPassword
        [HttpGet]
        public IActionResult ForgotPassword()
        {
            return View(new ForgotPasswordViewModel());
        }

        // POST: /Account/ForgotPassword
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ForgotPassword(ForgotPasswordViewModel model)
        {
            // Deliberately identical outcome whether or not the email exists -
            // don't leak which addresses are registered.
            var genericMessage = "If an account exists for that email address, a password reset link has been sent.";

            var email = (model.Email ?? "").Trim();
            var user = string.IsNullOrWhiteSpace(email) ? null :
                _context.Users.FirstOrDefault(u => u.Email == email && !u.IsArchived);

            if (user != null)
            {
                var rawToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
                    .Replace('+', '-').Replace('/', '_').TrimEnd('=');

                _context.PasswordResetTokens.Add(new GenPasswordResetToken
                {
                    UserId = user.UserId,
                    Token = rawToken,
                    ExpiresOn = DateTime.UtcNow.AddMinutes(30)
                });
                _context.SaveChanges();

                var resetLink = Url.Action("ResetPassword", "Account", new { token = rawToken }, Request.Scheme);
                var body = $@"
                    <p>Hi {user.FirstName},</p>
                    <p>Someone requested a password reset for your HemaFlow account. Click the link below to choose a new password - this link expires in 30 minutes and can only be used once.</p>
                    <p><a href=""{resetLink}"">Reset your password</a></p>
                    <p>If you didn't request this, you can safely ignore this email.</p>";

                try
                {
                    _emailService.SendEmail(user.Email, "Reset your HemaFlow password", body);
                }
                catch (Exception ex)
                {
                    // Always show the generic message (a delivery failure must not confirm that the
                    // address exists). The real reason (bad SMTP key, unverified sender...) goes to the log.
                    HttpContext.RequestServices.GetService<ILogger<AccountController>>()
                        ?.LogError(ex, "Password reset e-mail could not be sent to user {UserId}", user.UserId);
                }
            }

            TempData["Success"] = genericMessage;
            return RedirectToAction("Login");
        }

        // GET: /Account/ResetPassword?token=...
        [HttpGet]
        public IActionResult ResetPassword(string token)
        {
            var tokenRow = _context.PasswordResetTokens
                .FirstOrDefault(t => t.Token == token && t.UsedOn == null && t.ExpiresOn > DateTime.UtcNow);

            if (tokenRow == null)
            {
                TempData["Error"] = "This reset link is invalid or has expired. Please request a new one.";
                return RedirectToAction("ForgotPassword");
            }

            return View(new ResetPasswordViewModel { Token = token });
        }

        // POST: /Account/ResetPassword
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ResetPassword(ResetPasswordViewModel model)
        {
            var tokenRow = _context.PasswordResetTokens
                .FirstOrDefault(t => t.Token == model.Token && t.UsedOn == null && t.ExpiresOn > DateTime.UtcNow);

            if (tokenRow == null)
            {
                TempData["Error"] = "This reset link is invalid or has expired. Please request a new one.";
                return RedirectToAction("ForgotPassword");
            }

            if (model.NewPassword != model.ConfirmPassword)
            {
                TempData["Error"] = "Passwords do not match.";
                return View(model);
            }

            if (!PasswordPolicy.IsValid(model.NewPassword))
            {
                TempData["Error"] = PasswordPolicy.Message;
                return View(model);
            }

            var user = _context.Users.FirstOrDefault(u => u.UserId == tokenRow.UserId);
            if (user == null)
            {
                TempData["Error"] = "This reset link is invalid or has expired. Please request a new one.";
                return RedirectToAction("ForgotPassword");
            }

            var hasher = new PasswordHasher<GenUser>();
            user.PasswordHash = hasher.HashPassword(user, model.NewPassword);
            tokenRow.UsedOn = DateTime.UtcNow;

            // Any other outstanding reset links for this user are no longer valid either.
            var otherTokens = _context.PasswordResetTokens
                .Where(t => t.UserId == user.UserId && t.UsedOn == null && t.TokenId != tokenRow.TokenId);
            foreach (var t in otherTokens) t.UsedOn = DateTime.UtcNow;

            _context.SaveChanges();

            TempData["Success"] = "Your password has been reset. Please log in.";
            return RedirectToAction("Login");
        }

        // POST: /Account/Register
        // Register.cshtml submits this via fetch() as multipart form data
        // (not a normal form post), so it returns JSON rather than a redirect
        // - the page's own JS shows the success/error message and only
        // redirects to Login once this returns { success: true }.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Register(RegisterViewModel model)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    var firstError = ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage)
                        .FirstOrDefault();
                    return Json(new { success = false, message = firstError ?? "Please fill in all required fields correctly." });
                }

                if (model.Password != model.ConfirmPassword)
                {
                    return Json(new { success = false, message = "Passwords do not match." });
                }
                if (!PasswordPolicy.IsValid(model.Password))
                {
                    return Json(new { success = false, message = PasswordPolicy.Message });
                }

                var email = model.Email.Trim();
                if (_context.Users.Any(u => u.Email == email))
                {
                    return Json(new { success = false, message = "An account with this email address already exists." });
                }

                var idNumber = model.IdNumber.Trim();
                if (_context.PatientProfiles.Any(p => p.IdNumber == idNumber))
                {
                    return Json(new { success = false, message = "An account with this ID number already exists." });
                }

                var patientRole = _context.Roles.FirstOrDefault(r => r.Code == "Patient");
                if (patientRole == null)
                {
                    return Json(new { success = false, message = "Registration is temporarily unavailable. Please contact support." });
                }

                // Address: picked from the dropdowns (SuburbId) or typed in as "not listed" - either way it is
                // stored as a Suburb row (Province -> Town -> Suburb), so the address is always normalised.
                var suburbIdToSave = LocationHelper.ResolveOrCreate(_context, model.SuburbId, model.ProvinceId, model.City, model.Suburb, model.PostalCode, out var addrError);
                if (suburbIdToSave == null) return Json(new { success = false, message = addrError ?? "Please check your address." });

                // PatientCode/Users.UserCode are both NOT NULL UNIQUE - generate the
                // next free PAT-#### code up front and use the SAME value for both,
                // the same way ADM-#### is shared between Users.UserCode and
                // AdminProfile.AdminCode for staff accounts.
                var seq = _context.PatientProfiles.Count() + 1;
                var patientCode = $"PAT-{seq:D4}";
                while (_context.Users.Any(u => u.UserCode == patientCode) || _context.PatientProfiles.Any(p => p.PatientCode == patientCode))
                {
                    seq++;
                    patientCode = $"PAT-{seq:D4}";
                }

                var hasher = new PasswordHasher<GenUser>();
                var user = new GenUser
                {
                    RoleId = patientRole.RoleId,
                    FirstName = model.FirstName.Trim(),
                    Surname = model.Surname.Trim(),
                    Email = email,
                    Phone = model.Phone.Trim(),
                    UserCode = patientCode,
                    Identifier = idNumber,
                    Status = "Active",
                    IsArchived = false,
                    PasswordHash = ""
                };
                user.PasswordHash = hasher.HashPassword(user, model.Password);
                _context.Users.Add(user);
                _context.SaveChanges(); // need the generated UserId before creating the PatientProfile row

                _context.PatientProfiles.Add(new GenPatientProfile
                {
                    UserId = user.UserId,
                    PatientCode = patientCode,
                    IdNumber = idNumber,
                    DateOfBirth = DateOnly.FromDateTime(model.DateOfBirth),
                    AddressLine1 = model.AddressLine1.Trim(),
                    ComplexName = string.IsNullOrWhiteSpace(model.ComplexName) ? null : model.ComplexName.Trim(),
                    SuburbId = suburbIdToSave
                });
                _context.SaveChanges();

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                // Log the real reason for us; show the visitor a generic message (no DB/stack details).
                HttpContext.RequestServices.GetService<ILogger<AccountController>>()?.LogError(ex, "Registration failed");
                return Json(new { success = false, message = "We could not create your account right now. Please try again, or contact support if it keeps happening." });
            }
        }
    }
}