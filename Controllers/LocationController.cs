using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Project.Data;
using Project.Services;

namespace Project.Controllers
{
    // JSON feeds for the Province -> Town -> Suburb dropdowns. Anonymous because the
    // public Register page needs them before anyone has an account.
    [AllowAnonymous]
    [Route("Location")]
    public class LocationController : Controller
    {
        private readonly ApplicationDbContext _context;
        public LocationController(ApplicationDbContext context) { _context = context; }

        [HttpGet("Provinces")]
        public IActionResult Provinces()
        {
            try
            {
                return Json(_context.Provinces.AsNoTracking().OrderBy(p => p.Name)
                    .Select(p => new { id = (int)p.ProvinceId, name = p.Name }).ToList());
            }
            catch { return Json(new List<object>()); }
        }

        [HttpGet("Towns")]
        public IActionResult Towns(int provinceId)
        {
            try
            {
                return Json(_context.Towns.AsNoTracking().Where(t => t.ProvinceId == provinceId).OrderBy(t => t.Name)
                    .Select(t => new { id = t.TownId, name = t.Name }).ToList());
            }
            catch { return Json(new List<object>()); }
        }

        [HttpGet("Suburbs")]
        public IActionResult Suburbs(int townId)
        {
            try
            {
                return Json(_context.Suburbs.AsNoTracking().Where(s => s.TownId == townId).OrderBy(s => s.Name)
                    .Select(s => new { id = s.SuburbId, name = s.Name, postalCode = s.PostalCode }).ToList());
            }
            catch { return Json(new List<object>()); }
        }

        // Used to pre-select the dropdowns from an existing saved address (town + suburb names).
        [HttpGet("Resolve")]
        public IActionResult Resolve(string? town, string? suburb, int? suburbId)
        {
            try
            {
                if (suburbId is int id && id > 0)
                {
                    var one = _context.Suburbs.AsNoTracking().Where(s => s.SuburbId == id)
                        .Select(s => new { s.SuburbId, s.TownId, ProvinceId = (int)s.Town.ProvinceId }).FirstOrDefault();
                    return one == null ? Json(new { found = false })
                        : Json(new { found = true, provinceId = one.ProvinceId, townId = one.TownId, suburbId = one.SuburbId });
                }
                var tn = LocationParser.Norm(town); var sn = LocationParser.Norm(suburb);
                if (sn.Length == 0) return Json(new { found = false });
                var candidates = _context.Suburbs.AsNoTracking()
                    .Select(s => new { s.SuburbId, s.Name, s.TownId, TownName = s.Town.Name, s.Town.ProvinceId }).ToList();
                var hit = candidates.FirstOrDefault(c => LocationParser.Norm(c.Name) == sn && (tn.Length == 0 || LocationParser.Norm(c.TownName) == tn))
                          ?? candidates.FirstOrDefault(c => LocationParser.Norm(c.Name) == sn);
                return hit == null ? Json(new { found = false })
                    : Json(new { found = true, provinceId = (int)hit.ProvinceId, townId = hit.TownId, suburbId = hit.SuburbId });
            }
            catch { return Json(new { found = false }); }
        }
    }
}
