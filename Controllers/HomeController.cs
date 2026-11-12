using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Project.Models;

namespace Project.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;

        public HomeController(ILogger<HomeController> logger)
        {
            _logger = logger;
        }

        public IActionResult Index()
        {
            return View();
        }

        // Simple contact details page, no form needed.
        public IActionResult Contact()
        {
            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { StatusCode = 500, RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        // Reached via app.UseStatusCodePagesWithReExecute("/Error/{0}") in Program.cs -
        // covers 404 (bad URL, NotFound() results like LabTechController's missing-item
        // guards), 403 (wrong role for an [Authorize(Roles=...)] area), etc. Renders the
        // same branded Error.cshtml, just with the real status code and matching copy.
        [Route("Error/{statusCode:int}")]
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult HandleStatusCode(int statusCode)
        {
            return View("Error", new ErrorViewModel { StatusCode = statusCode, RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
