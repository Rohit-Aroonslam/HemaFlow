using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Project.Models.Entities;

using Project.Data;
namespace Project
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            //builder.Services.AddDbContext<ApplicationDbContext>(options =>
            //         options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

            var mvc = builder.Services.AddControllersWithViews()
                .AddRazorOptions(options =>
                {
                    options.ViewLocationFormats.Add("/Views/LabManager/{1}/{0}.cshtml");
                    options.ViewLocationFormats.Add("/Views/Doctor/{1}/{0}.cshtml");
                    options.ViewLocationFormats.Add("/Views/LabManager/LMDoctor/{0}.cshtml"); // ? added this line for LMDoctor views
                    options.ViewLocationFormats.Add("/Views/LabManager/ActivityLog/{0}.cshtml");
                    options.ViewLocationFormats.Add("/Views/LabManager/Profile/{0}.cshtml");
                });

            if (builder.Environment.IsDevelopment())
                mvc.AddRazorRuntimeCompilation();

            // Gzip/Brotli for HTML, CSS, JS and JSON - noticeably smaller pages over the campus network.
            builder.Services.AddResponseCompression(o => o.EnableForHttps = true);

            builder.Services.AddScoped<Project.Services.PdfService>();
            builder.Services.AddScoped<Project.Services.EmailService>();
            builder.Services.AddScoped<Project.Services.LabTechService>();
            builder.Services.AddMemoryCache();
            builder.Services.AddSingleton<Project.Services.DbTimingInterceptor>();
            builder.Services.AddDbContext<ApplicationDbContext>((sp, options) =>
                options.UseSqlServer(builder.Configuration.GetConnectionString("HemaFlowDb"))
                       .AddInterceptors(sp.GetRequiredService<Project.Services.DbTimingInterceptor>()));

            // ?? Authentication (real login) ??????????????????????????????????
            // Cookie-based sign-in. AccountController.Login issues the cookie
            // after verifying the password against Users.PasswordHash; anything
            // marked [Authorize] will redirect here if there's no valid cookie.
            //
            // A second, short-lived cookie scheme ("TwoFactorPending") backs the
            // real-time-OTP flow: once the password is verified for an account
            // with 2FA enabled, AccountController signs the user into THIS scheme
            // only (never the main one) so nothing is treated as "logged in" yet.
            // VerifyTwoFactor reads it to know who's completing the code step,
            // and only after a correct code does the real sign-in happen.
            builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
                .AddCookie(options =>
                {
                    options.LoginPath = "/Account/Login";
                    options.AccessDeniedPath = "/Account/AccessDenied";
                    options.ExpireTimeSpan = TimeSpan.FromHours(8);
                    options.SlidingExpiration = true;
                    // A deactivated (or archived) account must lose its session straight away, not when the
                    // cookie expires. The status is re-checked at most every 30 seconds per user.
                    options.Events.OnValidatePrincipal = async ctx =>
                    {
                        if (!int.TryParse(ctx.Principal?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var uid)) return;
                        var cache = ctx.HttpContext.RequestServices.GetRequiredService<Microsoft.Extensions.Caching.Memory.IMemoryCache>();
                        var key = "acct-active-" + uid;
                        if (!cache.TryGetValue(key, out bool stillActive))
                        {
                            var db = ctx.HttpContext.RequestServices.GetRequiredService<Project.Data.ApplicationDbContext>();
                            stillActive = await db.Users.AsNoTracking()
                                .AnyAsync(u => u.UserId == uid && u.Status == "Active" && !u.IsArchived);
                            cache.Set(key, stillActive, TimeSpan.FromSeconds(30));
                        }
                        if (!stillActive)
                        {
                            ctx.RejectPrincipal();
                            await ctx.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                        }
                    };
                })
                .AddCookie("TwoFactorPending", options =>
                {
                    options.Cookie.Name = "HemaFlow.TwoFactorPending";
                    options.ExpireTimeSpan = TimeSpan.FromMinutes(5);
                    options.SlidingExpiration = false;
                    options.LoginPath = "/Account/Login"; // never actually redirected to under this scheme
                });

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Home/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            // Branded pages for 404 (bad URL, or a controller's NotFound() result),
            // 403 (wrong role for an [Authorize(Roles=...)] area), etc. Safe to run
            // in every environment - it only re-executes on a bare status code, it
            // never exposes exception details (that stays gated to Development via
            // the default developer exception page, above/below this).
            app.UseStatusCodePagesWithReExecute("/Error/{0}");

            // When deployed as a virtual app (e.g. /GRP-04-13/), IIS sets
            // ASPNETCORE_APPL_PATH so tag-helper URLs include the correct base path.
            var pathBase = app.Configuration["ASPNETCORE_APPL_PATH"];
            if (!string.IsNullOrEmpty(pathBase) && pathBase != "/")
                app.UsePathBase(pathBase.TrimEnd('/'));

            // Compression is only worth it on the real server; in Visual Studio it just breaks Browser Link / hot reload.
            if (!app.Environment.IsDevelopment())
                app.UseResponseCompression();

            // Slow-request logger: anything over 700 ms is written to the log with its path,
            // and every response carries a Server-Timing header (visible in the browser's
            // Network tab) so it is easy to see whether a slow page is the server or the browser.
            app.Use(async (ctx, next) =>
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                var stat = new Project.Services.DbStat();
                Project.Services.DbTimingInterceptor.Current.Value = stat;
                ctx.Response.OnStarting(() =>
                {
                    ctx.Response.Headers["Server-Timing"] = $"app;dur={sw.ElapsedMilliseconds}, db;dur={(int)stat.Ms};desc=\"{stat.Queries} queries\"";
                    return Task.CompletedTask;
                });
                await next();
                if (sw.ElapsedMilliseconds > 700)
                    app.Logger.LogWarning("Slow request: {Method} {Path} took {Ms} ms ({Q} database queries, {DbMs} ms in the database)",
                        ctx.Request.Method, ctx.Request.Path, sw.ElapsedMilliseconds, stat.Queries, (int)stat.Ms);
            });

            app.UseHttpsRedirection();
            app.UseStaticFiles(new StaticFileOptions
            {
                // ?v=hash files: 1 year; /lib: 1 week; other static files: 1 hour so edits show up.
                OnPrepareResponse = ctx =>
                {
                    var versioned = ctx.Context.Request.Query.ContainsKey("v");
                    var isLib = ctx.Context.Request.Path.StartsWithSegments("/lib");
                    ctx.Context.Response.Headers["Cache-Control"] = versioned
                        ? "public,max-age=31536000,immutable"
                        : isLib ? "public,max-age=604800" : "public,max-age=3600";
                }
            });

            app.UseRouting();

            // UseAuthentication MUST come before UseAuthorization - it's what
            // reads the cookie and populates HttpContext.User; without it every
            // [Authorize] check would always see an anonymous user.
            app.UseAuthentication();
            // Logged-in pages must not be kept by the browser, otherwise the Back button
            // shows the old dashboard after logging out. Static files are served earlier
            // and keep their own caching rules.
            app.Use(async (ctx, next) =>
            {
                ctx.Response.OnStarting(() =>
                {
                    if (ctx.User.Identity?.IsAuthenticated == true && !ctx.Response.Headers.ContainsKey("Cache-Control"))
                    {
                        ctx.Response.Headers["Cache-Control"] = "no-store, no-cache, max-age=0";
                        ctx.Response.Headers["Pragma"] = "no-cache";
                        ctx.Response.Headers["Expires"] = "0";
                    }
                    return Task.CompletedTask;
                });
                await next();
            });
            // First login: accounts with MustChangePassword = true (e.g. doctors and
            // technicians created by the Lab Manager) must set their own password
            // before using anything else. Applies to Doctor / Technician / Patient.
            app.Use(async (ctx, next) =>
            {
                if (ctx.User.Identity?.IsAuthenticated == true)
                {
                    var role = ctx.User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
                    var path = ctx.Request.Path.Value ?? "";
                    bool exempt = path.StartsWith("/FirstLogin", StringComparison.OrdinalIgnoreCase)
                               || path.StartsWith("/Account/Logout", StringComparison.OrdinalIgnoreCase);
                    if (!exempt && (role == "Doctor" || role == "Technician" || role == "Patient")
                        && int.TryParse(ctx.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var uid))
                    {
                        // Looked up at most once a minute per user (it used to be a database round trip on EVERY page).
                        var cache = ctx.RequestServices.GetRequiredService<Microsoft.Extensions.Caching.Memory.IMemoryCache>();
                        var key = "mustchange:" + uid;
                        if (!Microsoft.Extensions.Caching.Memory.CacheExtensions.TryGetValue<bool>(cache, key, out var must))
                        {
                            var db = ctx.RequestServices.GetRequiredService<ApplicationDbContext>();
                            must = await db.Users.AsNoTracking()
                                .Where(u => u.UserId == uid)
                                .Select(u => u.MustChangePassword)
                                .FirstOrDefaultAsync();
                            Microsoft.Extensions.Caching.Memory.CacheExtensions.Set(cache, key, must, TimeSpan.FromMinutes(1));
                        }
                        if (must)
                        {
                            ctx.Response.Redirect(ctx.Request.PathBase + "/FirstLogin/ChangePassword");
                            return;
                        }
                    }
                }
                await next();
            });
            app.UseAuthorization();

            app.MapControllerRoute(
                name: "default",
                pattern: "{controller=Home}/{action=Index}/{id?}");

            app.Run();
        }
    }//end of class Program
}//end of namespace Project