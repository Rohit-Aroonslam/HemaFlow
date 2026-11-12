using Project.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Project.Models.Entities;
using Project.Models.LabManager;
using Project.Services;

/*
 =========================================================
 ReportController - Lab Manager, spec §8 (Management Reports, PDF)
 "Generate PDF reports in date range: Total tests performed
  grouped by category"

 ✅ DB: a test counts as PERFORMED when its result has been captured:
        dbo.TestResult.RecordedOn within the range
          → TestRequestItem → TestType → TestCategory
        (one result per test request item, so each test counts once)

 Report (ONP400 Report Guidelines):
   1. Summary by category  - tests, % of total, abnormal results, totals
   2. Breakdown by test type per category - subtotals, % of category
   Sorted by tests performed (highest first), then name.
   Abnormal = result outside the reference range stored at test time.
   Active categories/test types are always listed; archived ones only
   when they still had tests in the period.
   Date range is South African calendar days (SastTime), so the
   report is correct whatever time zone the server runs in.
 =========================================================
*/
[Authorize(Roles = "LabManager")]
public class ReportController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly PdfService _pdfService;

    public ReportController(ApplicationDbContext context, PdfService pdfService)
    {
        _context = context;
        _pdfService = pdfService;
    }

    public async Task<IActionResult> Index()
    {
        await SetStatViewBags();
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Generate(DateTime startDate, DateTime endDate, string export = "view")
    {
        await SetStatViewBags();

        if (startDate == default || endDate == default)
        {
            ViewBag.ErrorMessage = "Please choose a start and end date.";
            return View("Index");
        }
        if (startDate.Date > endDate.Date)
        {
            ViewBag.ErrorMessage = "The start date must be on or before the end date.";
            return View("Index");
        }

        var rows = await BuildReport(startDate, endDate);

        ViewBag.PeriodLabel = $"{startDate:d MMM yyyy} – {endDate:d MMM yyyy}";

        if (!rows.Any())
        {
            ViewBag.ErrorMessage = "There are no test categories to report on.";
            return View("Index");
        }

        // PDF is always produced - with a "no tests" message when the period is empty
        if (export == "pdf")
            return await GeneratePdf(rows, startDate, endDate);

        ViewBag.Report = rows
            .Select(r => new ReportRow { Category = r.Category, TotalTests = r.TotalTests })
            .ToList();
        if (rows.All(r => r.TotalTests == 0))
            ViewBag.ErrorMessage = "No tests were performed in the selected date range.";
        return View("Index");
    }

    // ── Tests performed per test type, rolled up per category ──────────
    private async Task<List<LabManagerCategoryReportRow>> BuildReport(DateTime startDate, DateTime endDate)
    {
        // South African calendar dates → UTC range (RecordedOn is stored in UTC)
        var fromUtc = SastTime.SastDateToUtc(startDate.Date);
        var toUtc   = SastTime.SastDateToUtc(endDate.Date.AddDays(1));

        var perType = await _context.TestResults.AsNoTracking()
            .Where(r => r.RecordedOn >= fromUtc && r.RecordedOn < toUtc)
            .GroupBy(r => r.TestRequestItem.TestTypeId)
            .Select(g => new
            {
                TestTypeId = g.Key,
                Total      = g.Count(),
                Abnormal   = g.Sum(r => (r.NormalMinAtTest != null && r.ResultValue < r.NormalMinAtTest) ||
                                        (r.NormalMaxAtTest != null && r.ResultValue > r.NormalMaxAtTest) ? 1 : 0)
            })
            .ToListAsync();
        var countedTypeIds = perType.Select(x => x.TestTypeId).ToList();

        // Active test types + any archived test type that still had tests in the period
        var testTypes = await _context.TestTypes.AsNoTracking()
            .Where(t => !t.IsArchived || countedTypeIds.Contains(t.TestTypeId))
            .Select(t => new
            {
                t.TestTypeId,
                t.Name,
                t.IsArchived,
                t.TestCategoryId,
                SampleType = t.SampleType.Name
            })
            .ToListAsync();
        var categoryIdsWithTypes = testTypes
            .Where(t => countedTypeIds.Contains(t.TestTypeId))
            .Select(t => t.TestCategoryId)
            .Distinct()
            .ToList();

        // Active categories + any archived category that still had tests in the period
        var categories = await _context.TestCategories.AsNoTracking()
            .Where(c => !c.IsArchived || categoryIdsWithTypes.Contains(c.TestCategoryId))
            .Select(c => new { c.TestCategoryId, c.Name, c.IsArchived })
            .ToListAsync();

        return categories
            .Select(c =>
            {
                var types = testTypes
                    .Where(t => t.TestCategoryId == c.TestCategoryId)
                    .Select(t =>
                    {
                        var n = perType.FirstOrDefault(x => x.TestTypeId == t.TestTypeId);
                        return new LabManagerTestTypeReportRow
                        {
                            TestType      = t.Name,
                            SampleType    = t.SampleType,
                            IsArchived    = t.IsArchived,
                            TotalTests    = n?.Total ?? 0,
                            AbnormalTests = n?.Abnormal ?? 0
                        };
                    })
                    // an archived test type in an active category is only shown if it was used
                    .Where(t => !t.IsArchived || t.TotalTests > 0)
                    .OrderByDescending(t => t.TotalTests).ThenBy(t => t.TestType)
                    .ToList();

                return new LabManagerCategoryReportRow
                {
                    Category      = c.Name,
                    IsArchived    = c.IsArchived,
                    TotalTests    = types.Sum(t => t.TotalTests),
                    AbnormalTests = types.Sum(t => t.AbnormalTests),
                    TestTypes     = types
                };
            })
            .OrderByDescending(c => c.TotalTests).ThenBy(c => c.Category)
            .ToList();
    }

    private async Task SetStatViewBags()
    {
        ViewBag.CategoryCount = await _context.TestCategories.CountAsync(c => !c.IsArchived);
        ViewBag.TestTypeCount = await _context.TestTypes.CountAsync(t => !t.IsArchived);
    }

    private async Task<IActionResult> GeneratePdf(List<LabManagerCategoryReportRow> rows, DateTime start, DateTime end)
    {
        var managerId = await LabManagerActivityLog.CurrentLabManagerIdAsync(_context, User);
        var managerName = managerId == null ? null : await _context.Users
            .Where(u => u.UserId == managerId)
            .Select(u => u.FirstName + " " + u.Surname)
            .FirstOrDefaultAsync();

        var reportData = new LabManagerCategoryReportData
        {
            FromDate    = start,
            ToDate      = end,
            GeneratedBy = string.IsNullOrWhiteSpace(managerName) ? "Lab Manager" : managerName,
            Rows        = rows
        };

        var pdfBytes = _pdfService.GenerateLabManagerCategoryReport(reportData);
        return File(pdfBytes, "application/pdf",
            $"TestsPerformedByCategory_{start:yyyyMMdd}_{end:yyyyMMdd}.pdf");
    }

}//end of class ReportController

public class ReportRow
{
    public string Category { get; set; } = "";
    public int TotalTests { get; set; }
}
