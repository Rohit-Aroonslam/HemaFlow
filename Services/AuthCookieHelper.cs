using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Project.Models.Entities;

namespace Project.Services
{
    public static class AuthCookieHelper
    {
        // Re-issues the sign-in cookie with the user's current name / e-mail claims
        // (every other claim, e.g. role and user id, is kept as it was).
        public static async Task RefreshNameAndEmailAsync(HttpContext http, User user)
        {
            var scheme = CookieAuthenticationDefaults.AuthenticationScheme;
            var auth = await http.AuthenticateAsync(scheme);
            if (!auth.Succeeded || auth.Principal == null) return;

            var claims = auth.Principal.Claims
                .Where(c => c.Type != ClaimTypes.Email && c.Type != ClaimTypes.Name).ToList();
            claims.Add(new Claim(ClaimTypes.Email, user.Email));
            claims.Add(new Claim(ClaimTypes.Name, $"{user.FirstName} {user.Surname}"));

            await http.SignInAsync(scheme, new ClaimsPrincipal(new ClaimsIdentity(claims, scheme)),
                auth.Properties ?? new AuthenticationProperties());
        }
    }
}
