using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Project.Models;

// Alias to avoid clash with System.ComponentModel.IContainer
using QContainer = QuestPDF.Infrastructure.IContainer;

namespace Project.Services
{
    public class PdfService
    {
        private const string LabName = "Nelson Mandela Bay Haematology Diagnostic Laboratories";
        private const string LabShort = "NMBHDL / HemaFlow";
        private const string LabAddress = "Port Elizabeth, 6001, Eastern Cape, South Africa";
        private const string LabPhone = "+27 81 028 6437";
        private const string LabEmail = "info@hemaflow.co.za";
        private const string LabWeb = "www.hemaflow.co.za";

        private const string Navy = "#0f172a";
        private const string Red = "#c0392b";
        private const string LightBg = "#f8fafc";
        private const string Border = "#e2e8f0";
        private const string MidGray = "#64748b";
        private const string White = "#ffffff";

        // Reports show South African time (UTC+2) whatever the server clock/time zone is set to.
        private static DateTime Now => Project.Services.SastTime.ToSast(DateTime.UtcNow);

        // One date-range and one doctor-name format for every report, whichever role prints it.
        private static string Range(DateTime from, DateTime to) => $"{from:d MMM yyyy} to {to:d MMM yyyy}";
        private static string Dr(string? name)
        {
            var n = (name ?? "").Trim();
            if (n.StartsWith("Dr.", StringComparison.OrdinalIgnoreCase)) n = n[3..].Trim();
            else if (n.StartsWith("Dr ", StringComparison.OrdinalIgnoreCase)) n = n[3..].Trim();
            return n.Length == 0 ? "" : "Dr. " + n;
        }

        private readonly byte[]? _logoBytes;

        public PdfService(IWebHostEnvironment env)
        {
            QuestPDF.Settings.License = LicenseType.Community;
            var path = Path.Combine(env.WebRootPath, "images", "logo.png");
            if (File.Exists(path))
                _logoBytes = File.ReadAllBytes(path);
        }

        // ═══════════════════════════ PUBLIC METHODS ════════════════════════════

        public byte[] GeneratePatientResultsReport(PatientResultsReportData data)
            => Build(PageSizes.A4, "Test Results Report",
                $"Patient: {data.PatientName}  |  Requested by: {Dr(data.DoctorName)}",
                c => PatientResultsContent(c, data));

        public byte[] GeneratePatientCategoryReport(PatientCategoryReportData data)
            => Build(PageSizes.A4, $"Results Report - {data.Category}",
                $"{data.PatientName}  |  {Range(data.FromDate, data.ToDate)}",
                c => CategoryResultsContent(c, data));

        public byte[] GenerateUserListReport(List<UserRow> users, string filterRole = "All")
            => Build(PageSizes.A4.Landscape(), "User Management Report",
                $"Role Filter: {filterRole}  |  Total: {users.Count}",
                c => UserListContent(c, users, filterRole));

        public byte[] GenerateConditionsReport(List<ConditionRow> conditions)
            => Build(PageSizes.A4, "Medical Conditions Catalogue",
                $"Total entries: {conditions.Count}",
                c => ConditionsContent(c, conditions));

        public byte[] GenerateAllergiesReport(List<AllergyRow> allergies)
            => Build(PageSizes.A4, "Allergies Catalogue",
                $"Total entries: {allergies.Count}",
                c => AllergiesContent(c, allergies));

        public byte[] GenerateMedicationsReport(List<MedicationRow> medications)
            => Build(PageSizes.A4, "Medications Catalogue",
                $"Total entries: {medications.Count}",
                c => MedicationsContent(c, medications));

        public byte[] GenerateTechnicianReport(TechnicianReportData data)
            => Build(PageSizes.A4, "Technician Completed Tests Report",
                $"{data.TechnicianName}  |  {Range(data.FromDate, data.ToDate)}",
                c => TechnicianReportContent(c, data));

        public byte[] GenerateLabManagerCategoryReport(LabManagerCategoryReportData data)
            => Build(PageSizes.A4, "Tests Performed by Category Report",
                $"Period: {Range(data.FromDate, data.ToDate)}",
                c => LabManagerCategoryContent(c, data));

        public byte[] GenerateDoctorRequestsReport(DoctorRequestsReportData data)
            => Build(PageSizes.A4.Landscape(), "Test Requests Report",
                $"{Dr(data.DoctorName)}  |  {Range(data.FromDate, data.ToDate)}",
                c => DoctorRequestsContent(c, data));

        // ═══════════════════════════ CORE BUILDER ════════════════════════════

        private byte[] Build(PageSize size, string title, string subtitle,
            Action<QContainer> contentBuilder)
        {
            return Document.Create(doc =>
            {
                doc.Page(page =>
                {
                    page.Size(size);
                    page.Margin(0);
                    page.DefaultTextStyle(t => t.FontFamily("Arial").FontSize(9).FontColor(Navy));
                    page.Header().Element(c => Header(c, title, subtitle));
                    page.Content().Column(body =>
                    {
                        body.Item().Element(contentBuilder);
                        body.Item().PaddingHorizontal(20).PaddingTop(4).PaddingBottom(10)
                            .Text("This report contains confidential health information and is intended only for the named recipient. Generated by HemaFlow.")
                            .FontSize(6.5f).FontColor(MidGray);
                    });
                    page.Footer().Element(Footer);
                });
            }).GeneratePdf();
        }

        // ═══════════════════════════ HEADER & FOOTER ═════════════════════════

        private void Header(QContainer container, string title, string subtitle)
        {
            container.Column(col =>
            {
                col.Item().Background(Navy).Padding(14).Row(row =>
                {
                    if (_logoBytes != null)
                        row.ConstantItem(70).AlignMiddle().Image(_logoBytes).FitArea();
                    else
                        row.ConstantItem(70).AlignMiddle()
                            .Text("HF").Bold().FontSize(22).FontColor(White);

                    row.RelativeItem().AlignMiddle().PaddingLeft(14).Column(c =>
                    {
                        c.Item().Text(LabName).Bold().FontSize(10).FontColor(White);
                        c.Item().PaddingTop(2).Text(LabAddress).FontSize(7.5f).FontColor("#94a3b8");
                        c.Item().Text($"{LabPhone}  |  {LabEmail}  |  {LabWeb}")
                            .FontSize(7.5f).FontColor("#94a3b8");
                    });

                    row.ConstantItem(130).AlignMiddle().AlignRight().Column(c =>
                    {
                        c.Item().Text(Now.ToString("d MMM yyyy"))
                            .Bold().FontSize(9).FontColor(White);
                        c.Item().Text(Now.ToString("HH:mm"))
                            .FontSize(8).FontColor("#94a3b8");
                        c.Item().PaddingTop(4)
                            .Background(Red).Padding(3).PaddingHorizontal(6)
                            .Text("CONFIDENTIAL").Bold().FontSize(6.5f).FontColor(White);
                    });
                });

                col.Item().Background(Red).PaddingHorizontal(20).PaddingVertical(8).Row(row =>
                {
                    row.RelativeItem().Column(c =>
                    {
                        c.Item().Text(title).Bold().FontSize(13).FontColor(White);
                        if (!string.IsNullOrWhiteSpace(subtitle))
                            // Change "rgba(255,255,255,0.75)" to "#BFFFFFFF"
                            c.Item().PaddingTop(2).Text(subtitle)
                                .FontSize(8).FontColor("#BFFFFFFF");
                    });
                });

                col.Item().Height(2).Background(Border);
            });
        }

        private void Footer(QContainer container)
        {
            container.Column(col =>
            {
                col.Item().Height(1).Background(Border);
                col.Item().Background(Navy).PaddingHorizontal(20).PaddingVertical(7).Row(row =>
                {
                    row.RelativeItem().AlignMiddle().Text(t =>
                        t.Span($"{LabShort}  |  {LabEmail}  |  {LabWeb}")
                            .FontSize(7).FontColor("#94a3b8"));
                    row.ConstantItem(130).AlignRight().AlignMiddle().Text(t =>
                    {
                        t.Span("Page ").FontSize(7).FontColor("#94a3b8");
                        t.CurrentPageNumber().Bold().FontSize(7).FontColor(White);
                        t.Span(" of ").FontSize(7).FontColor("#94a3b8");
                        t.TotalPages().Bold().FontSize(7).FontColor(White);
                    });
                });
            });
        }

        // ═══════════════════════════ CONTENT BUILDERS ═══════════════════════

        private void PatientResultsContent(QContainer container, PatientResultsReportData data)
        {
            container.PaddingHorizontal(20).PaddingVertical(14).Column(col =>
            {
                InfoRow(col, new[]
                {
                    ("Patient Name",      data.PatientName),
                    ("SA ID Number",      data.IdNumber),
                    ("Date of Birth",     data.DateOfBirth.ToString("d MMMM yyyy")),
                    ("Requesting Doctor", Dr(data.DoctorName))
                });
                col.Item().Height(12);

                ResultGroupTables(col, data.Results.GroupBy(r => r.Category));

                if (!string.IsNullOrWhiteSpace(data.DoctorNote))
                {
                    col.Item().Border(1).BorderColor(Border).Padding(10).Column(n =>
                    {
                        n.Item().Text("Doctor's Note").Bold().FontSize(9);
                        n.Item().Height(4);
                        n.Item().Text(data.DoctorNote).FontSize(8.5f).FontColor(MidGray);
                    });
                }

                SignatureRow(col, Dr(data.DoctorName));
            });
        }

        private void CategoryResultsContent(QContainer container, PatientCategoryReportData data)
        {
            container.PaddingHorizontal(20).PaddingVertical(14).Column(col =>
            {
                InfoRow(col, new[]
                {
                    ("Patient",    data.PatientName),
                    ("SA ID",      data.IdNumber),
                    ("Category",   data.Category),
                    ("Date Range", Range(data.FromDate, data.ToDate))
                });
                col.Item().Height(6);
                InfoRow(col, new[]
                {
                    ("Total Results",  data.Results.Count.ToString()),
                    ("Abnormal",       data.Results.Count(r => r.IsAbnormal).ToString()),
                    ("Normal",         data.Results.Count(r => !r.IsAbnormal).ToString())
                });
                col.Item().Height(12);

                if (data.Results.Count == 0)
                {
                    col.Item().Border(0.5f).BorderColor("#e2e8f0").Padding(16).AlignCenter()
                        .Text("No records found for the selected date range.").FontSize(10).FontColor("#475569");
                    return;
                }

                // Sorted by category, then date (oldest first), so the report reads the same every time.
                ResultGroupTables(col, data.Results
                    .OrderBy(r => r.Category).ThenBy(r => r.ResultDate).ThenBy(r => r.TestName)
                    .GroupBy(r => r.Category));
            });
        }

        // Shared by the patient/category results reports: one real QuestPDF
        // table per category group, header repeating on page breaks, with the
        // optional lab-note line rendered as a column-spanning row so it stays
        // inside the table's own borders instead of floating below it.
        private void ResultGroupTables(ColumnDescriptor col, IEnumerable<IGrouping<string, TestResultRow>> groups)
        {
            foreach (var group in groups)
            {
                col.Item().Background(Navy).PaddingHorizontal(12).PaddingVertical(6)
                    .Text(group.Key).Bold().FontSize(9).FontColor(White);

                col.Item().Table(table =>
                {
                    table.ColumnsDefinition(c =>
                    {
                        c.RelativeColumn(3f);   // Test Name
                        c.RelativeColumn(1f);   // Result
                        c.RelativeColumn(1f);   // Unit
                        c.RelativeColumn(1.6f); // Normal Range
                        c.RelativeColumn(1.5f); // Status
                        c.RelativeColumn(1.5f); // Date
                    });

                    table.Header(h =>
                    {
                        HeaderCellLight(h.Cell(), "Test Name");
                        HeaderCellLight(h.Cell(), "Result", CellAlign.Right);
                        HeaderCellLight(h.Cell(), "Unit", CellAlign.Center);
                        HeaderCellLight(h.Cell(), "Normal Range", CellAlign.Center);
                        HeaderCellLight(h.Cell(), "Status", CellAlign.Center);
                        HeaderCellLight(h.Cell(), "Date", CellAlign.Right);
                    });

                    foreach (var r in group)
                    {
                        var bg = r.IsAbnormal ? "#fff5f5" : White;

                        DataCell(table.Cell(), r.TestName, bg, bold: true);
                        DataCell(table.Cell(), r.ResultValue, bg, CellAlign.Right,
                            bold: r.IsAbnormal, color: r.IsAbnormal ? Red : Navy);
                        DataCell(table.Cell(), r.Unit, bg, CellAlign.Center);
                        DataCell(table.Cell(), r.RangeDisplay, bg, CellAlign.Center);
                        DataCell(table.Cell(), r.IsAbnormal ? "ABNORMAL" : "Normal", bg, CellAlign.Center,
                            bold: r.IsAbnormal, color: r.IsAbnormal ? Red : "#16a34a");
                        DataCell(table.Cell(), r.ResultDate.ToString("d MMM yyyy"), bg, CellAlign.Right);

                        if (!string.IsNullOrWhiteSpace(r.Notes))
                            NoteCell(table.Cell().ColumnSpan(6), "Lab Technician Notes: ", r.Notes, bg);
                    }
                });

                col.Item().Height(10);
            }
        }

        private void UserListContent(QContainer container, List<UserRow> users, string filterRole)
        {
            container.PaddingHorizontal(20).PaddingVertical(14).Column(col =>
            {
                InfoRow(col, new[] { ("Report", "User Management"), ("Role Filter", filterRole), ("Total Users", users.Count.ToString()) });
                col.Item().Height(12);

                col.Item().Table(table =>
                {
                    table.ColumnsDefinition(c =>
                    {
                        c.RelativeColumn(1f);
                        c.RelativeColumn(2.5f);
                        c.RelativeColumn(1.2f);
                        c.RelativeColumn(2.5f);
                        c.RelativeColumn(3f);
                        c.RelativeColumn(1.2f);
                    });

                    table.Header(h =>
                    {
                        HeaderCellNavy(h.Cell(), "User ID", CellAlign.Center);
                        HeaderCellNavy(h.Cell(), "Full Name");
                        HeaderCellNavy(h.Cell(), "Role", CellAlign.Center);
                        HeaderCellNavy(h.Cell(), "Identifier", CellAlign.Center);
                        HeaderCellNavy(h.Cell(), "Email");
                        HeaderCellNavy(h.Cell(), "Status", CellAlign.Center);
                    });

                    bool alt = false;
                    foreach (var u in users)
                    {
                        var sc = u.Status switch { "Active" => "#16a34a", "Pending" => "#d97706", _ => "#dc2626" };
                        var bg = alt ? LightBg : White;

                        DataCell(table.Cell(), u.UserId, bg, CellAlign.Center, mono: true);
                        DataCell(table.Cell(), u.FullName, bg, bold: true);
                        DataCell(table.Cell(), u.Role, bg, CellAlign.Center);
                        DataCell(table.Cell(), u.Identifier, bg, CellAlign.Center, mono: true);
                        DataCell(table.Cell(), u.Email, bg);
                        DataCell(table.Cell(), u.Status, bg, CellAlign.Center, bold: true, color: sc);

                        alt = !alt;
                    }
                });
            });
        }

        private void ConditionsContent(QContainer container, List<ConditionRow> conditions)
        {
            container.PaddingHorizontal(20).PaddingVertical(14).Column(col =>
            {
                InfoRow(col, new[] { ("Report", "Medical Conditions Catalogue"), ("Total Entries", conditions.Count.ToString()), ("Sorted By", "Category, then name") });
                col.Item().Height(12);

                col.Item().Table(table =>
                {
                    table.ColumnsDefinition(c =>
                    {
                        c.RelativeColumn(4f);
                        c.RelativeColumn(2f);
                        c.RelativeColumn(1.5f);
                        c.RelativeColumn(4f);
                    });

                    table.Header(h =>
                    {
                        HeaderCellNavy(h.Cell(), "Condition Name");
                        HeaderCellNavy(h.Cell(), "Category", CellAlign.Center);
                        HeaderCellNavy(h.Cell(), "ICD-10", CellAlign.Center);
                        HeaderCellNavy(h.Cell(), "Description");
                    });

                    bool alt = false;
                    foreach (var c in conditions)
                    {
                        var bg = alt ? LightBg : White;
                        DataCell(table.Cell(), c.Name, bg, bold: true);
                        DataCell(table.Cell(), c.Category, bg, CellAlign.Center);
                        DataCell(table.Cell(), c.Icd10Code, bg, CellAlign.Center, mono: true);
                        DataCell(table.Cell(), c.Description, bg, color: MidGray);
                        alt = !alt;
                    }
                });
            });
        }

        private void AllergiesContent(QContainer container, List<AllergyRow> allergies)
        {
            container.PaddingHorizontal(20).PaddingVertical(14).Column(col =>
            {
                InfoRow(col, new[] { ("Report", "Allergies Catalogue"), ("Total Entries", allergies.Count.ToString()), ("Sorted By", "Category, then name") });
                col.Item().Height(12);

                col.Item().Table(table =>
                {
                    table.ColumnsDefinition(c =>
                    {
                        c.RelativeColumn(3.5f);
                        c.RelativeColumn(1.5f);
                        c.RelativeColumn(3f);
                        c.RelativeColumn(3f);
                    });

                    table.Header(h =>
                    {
                        HeaderCellNavy(h.Cell(), "Allergy Name");
                        HeaderCellNavy(h.Cell(), "Category", CellAlign.Center);
                        HeaderCellNavy(h.Cell(), "Typical Reaction");
                        HeaderCellNavy(h.Cell(), "Notes");
                    });

                    bool alt = false;
                    foreach (var a in allergies)
                    {
                        var bg = alt ? LightBg : White;
                        DataCell(table.Cell(), a.Name, bg, bold: true);
                        DataCell(table.Cell(), a.Category, bg, CellAlign.Center);
                        DataCell(table.Cell(), a.TypicalReaction, bg);
                        DataCell(table.Cell(), a.Notes, bg, color: MidGray);
                        alt = !alt;
                    }
                });
            });
        }

        private void MedicationsContent(QContainer container, List<MedicationRow> medications)
        {
            container.PaddingHorizontal(20).PaddingVertical(14).Column(col =>
            {
                InfoRow(col, new[] { ("Report", "Medications Catalogue"), ("Total Entries", medications.Count.ToString()), ("Sorted By", "Category, then name") });
                col.Item().Height(12);

                col.Item().Table(table =>
                {
                    table.ColumnsDefinition(c =>
                    {
                        c.RelativeColumn(3.5f);
                        c.RelativeColumn(2f);
                        c.RelativeColumn(5.5f);
                    });

                    table.Header(h =>
                    {
                        HeaderCellNavy(h.Cell(), "Medication Name");
                        HeaderCellNavy(h.Cell(), "Category", CellAlign.Center);
                        HeaderCellNavy(h.Cell(), "Notes");
                    });

                    bool alt = false;
                    foreach (var m in medications)
                    {
                        var bg = alt ? LightBg : White;
                        DataCell(table.Cell(), m.Name, bg, bold: true);
                        DataCell(table.Cell(), m.Category, bg, CellAlign.Center);
                        DataCell(table.Cell(), m.Notes, bg, color: MidGray);
                        alt = !alt;
                    }
                });
            });
        }

        private void TechnicianReportContent(QContainer container, TechnicianReportData data)
        {
            container.PaddingHorizontal(20).PaddingVertical(14).Column(col =>
            {
                int abnormalCount = data.Results.Count(r => r.IsAbnormal);

                InfoRow(col, new[]
                {
                    ("Technician",      data.TechnicianName),
                    ("Employee #",      data.EmployeeNumber),
                    ("Date Range",      Range(data.FromDate, data.ToDate)),
                    ("Tests Completed", data.Results.Count.ToString())
                });
                col.Item().Height(12);

                if (!data.Results.Any())
                {
                    col.Item().Border(1).BorderColor(Border).Padding(16).AlignCenter()
                        .Text("No completed tests in the selected date range.")
                        .FontSize(9).FontColor(MidGray);
                    return;
                }

                foreach (var group in data.Results.GroupBy(r => r.Category))
                {
                    col.Item().Background(Navy).PaddingHorizontal(12).PaddingVertical(6)
                        .Text(group.Key).Bold().FontSize(9).FontColor(White);

                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(1.2f); // Request #
                            c.RelativeColumn(2.5f); // Test Name
                            c.RelativeColumn(1f);   // Result
                            c.RelativeColumn(0.9f); // Unit
                            c.RelativeColumn(1.4f); // Normal Range
                            c.RelativeColumn(1.5f); // Status
                            c.RelativeColumn(1.4f); // Date
                        });

                        table.Header(h =>
                        {
                            HeaderCellLight(h.Cell(), "Request #", CellAlign.Center);
                            HeaderCellLight(h.Cell(), "Test Name");
                            HeaderCellLight(h.Cell(), "Result", CellAlign.Right);
                            HeaderCellLight(h.Cell(), "Unit", CellAlign.Center);
                            HeaderCellLight(h.Cell(), "Normal Range", CellAlign.Center);
                            HeaderCellLight(h.Cell(), "Status", CellAlign.Center);
                            HeaderCellLight(h.Cell(), "Date", CellAlign.Right);
                        });

                        foreach (var r in group.OrderByDescending(r => r.CompletedAt))
                        {
                            var bg = r.IsAbnormal ? "#fff5f5" : White;

                            DataCell(table.Cell(), r.RequestNumber, bg, CellAlign.Center, mono: true);
                            DataCell(table.Cell(), r.TestName, bg, bold: true);
                            DataCell(table.Cell(), r.ResultValue, bg, CellAlign.Right,
                                bold: r.IsAbnormal, color: r.IsAbnormal ? Red : Navy);
                            DataCell(table.Cell(), r.Unit, bg, CellAlign.Center);
                            DataCell(table.Cell(), r.NormalRange, bg, CellAlign.Center);
                            DataCell(table.Cell(), r.IsAbnormal ? "ABNORMAL" : "Normal", bg, CellAlign.Center,
                                bold: r.IsAbnormal, color: r.IsAbnormal ? Red : "#16a34a");
                            DataCell(table.Cell(), r.CompletedAt.ToString("d MMM yyyy"), bg, CellAlign.Right);
                        }
                    });
                    col.Item().Height(10);
                }

                col.Item().Height(6);
                col.Item().Background(LightBg).Border(1).BorderColor(Border).Padding(8).Row(row =>
                {
                    row.RelativeItem().Text($"Total tests in range: {data.Results.Count}")
                        .Bold().FontSize(8).FontColor(Navy);
                    row.RelativeItem().AlignRight()
                        .Text($"Abnormal flags: {abnormalCount}")
                        .Bold().FontSize(8).FontColor(abnormalCount > 0 ? Red : "#16a34a");
                });

                SignatureRow(col, data.TechnicianName);
            });
        }

        // ── Lab Manager: Tests Performed by Category (ONP400 Report Guidelines) ──
        //  1. Report criteria   2. Key figures   3. Summary by category
        //  4. Breakdown by test type (one table per category, with subtotal)
        //  5. Notes + sign-off.  Empty period → message, criteria still shown.
        private void LabManagerCategoryContent(QContainer container, LabManagerCategoryReportData data)
        {
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            string Num(int v) => v.ToString("#,0", inv);
            string Pct(int part, int whole) =>
                whole == 0 ? "-" : (part * 100m / whole).ToString("0.0", inv) + "%";
            string Label(string name, bool archived) => archived ? $"{name} (archived)" : name;

            int grand    = data.Rows.Sum(r => r.TotalTests);
            int abnormal = data.Rows.Sum(r => r.AbnormalTests);
            var used     = data.Rows.Where(r => r.TotalTests > 0).ToList();
            var unused   = data.Rows.Where(r => r.TotalTests == 0).Select(r => r.Category).OrderBy(n => n).ToList();

            container.PaddingHorizontal(20).PaddingVertical(14).Column(col =>
            {
                InfoRow(col, new[]
                {
                    ("Report Period", Range(data.FromDate, data.ToDate)),
                    ("Generated On",  Now.ToString("d MMMM yyyy HH:mm")),
                    ("Generated By",  data.GeneratedBy),
                    ("Criteria",      "All categories, completed tests")
                });

                // ── Empty period: no blank tables ──
                if (grand == 0)
                {
                    col.Item().Height(16);
                    col.Item().Background(LightBg).Border(1).BorderColor(Border).Padding(24).Column(m =>
                    {
                        m.Item().AlignCenter().Text("No tests were performed in the selected date range.")
                            .Bold().FontSize(10).FontColor(Navy);
                        m.Item().PaddingTop(4).AlignCenter()
                            .Text($"No test results were captured between {data.FromDate:d MMMM yyyy} and {data.ToDate:d MMMM yyyy}.")
                            .FontSize(8.5f).FontColor(MidGray);
                    });
                    return;
                }

                // ── Key figures ──
                col.Item().Height(10);
                InfoRow(col, new[]
                {
                    ("Tests Performed",       Num(grand)),
                    ("Categories With Tests", $"{used.Count} of {data.Rows.Count}"),
                    ("Test Types Performed",  Num(used.Sum(r => r.TestTypes.Count(t => t.TotalTests > 0)))),
                    ("Abnormal Results",      $"{Num(abnormal)} ({Pct(abnormal, grand)})")
                });

                // ── 1. Summary by category ──
                col.Item().PaddingTop(16).EnsureSpace(120).Column(sec =>
                {
                    SectionHeading(sec, "1. Summary by Category",
                        "Sorted by tests performed (highest first)");

                    sec.Item().Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(3.2f);
                            c.RelativeColumn(1.2f);
                            c.RelativeColumn(1.4f);
                            c.RelativeColumn(1.2f);
                            c.RelativeColumn(1.2f);
                            c.RelativeColumn(1.2f);
                        });
                        table.Header(h =>
                        {
                            HeaderCellNavy(h.Cell(), "Test Category");
                            HeaderCellNavy(h.Cell(), "Test Types", CellAlign.Right);
                            HeaderCellNavy(h.Cell(), "Tests Performed", CellAlign.Right);
                            HeaderCellNavy(h.Cell(), "% of Total", CellAlign.Right);
                            HeaderCellNavy(h.Cell(), "Abnormal", CellAlign.Right);
                            HeaderCellNavy(h.Cell(), "% Abnormal", CellAlign.Right);
                        });

                        bool alt = false;
                        foreach (var r in data.Rows)
                        {
                            var bg = alt ? LightBg : White;
                            var muted = r.TotalTests == 0 ? MidGray : null;
                            DataCell(table.Cell(), Label(r.Category, r.IsArchived), bg, bold: true, color: muted);
                            DataCell(table.Cell(), Num(r.TestTypes.Count), bg, CellAlign.Right, color: muted);
                            DataCell(table.Cell(), Num(r.TotalTests), bg, CellAlign.Right, bold: true, color: muted);
                            DataCell(table.Cell(), Pct(r.TotalTests, grand), bg, CellAlign.Right, color: muted);
                            DataCell(table.Cell(), Num(r.AbnormalTests), bg, CellAlign.Right, color: muted);
                            DataCell(table.Cell(), Pct(r.AbnormalTests, r.TotalTests), bg, CellAlign.Right, color: muted);
                            alt = !alt;
                        }

                        TotalCell(table.Cell(), "Total");
                        TotalCell(table.Cell(), Num(data.Rows.Sum(r => r.TestTypes.Count)), CellAlign.Right);
                        TotalCell(table.Cell(), Num(grand), CellAlign.Right);
                        TotalCell(table.Cell(), "100.0%", CellAlign.Right);
                        TotalCell(table.Cell(), Num(abnormal), CellAlign.Right);
                        TotalCell(table.Cell(), Pct(abnormal, grand), CellAlign.Right);
                    });

                    if (unused.Any())
                        sec.Item().PaddingTop(6)
                            .Text($"Categories with no tests in this period: {string.Join(", ", unused)}.")
                            .FontSize(7.5f).Italic().FontColor(MidGray);
                });

                // ── 2. Breakdown by test type ──
                col.Item().PaddingTop(18).EnsureSpace(140).Column(sec =>
                {
                    SectionHeading(sec, "2. Breakdown by Test Type",
                        "Categories with tests in this period; test types sorted by tests performed");

                    foreach (var r in used)
                    {
                        // heading + table kept together so a heading never sits alone at a page bottom
                        sec.Item().PaddingTop(8).EnsureSpace(110).Column(block =>
                        {
                            block.Item().Background(Navy).PaddingVertical(5).PaddingHorizontal(8).Row(row =>
                            {
                                row.RelativeItem().Text(Label(r.Category, r.IsArchived))
                                    .Bold().FontSize(9).FontColor(White);
                                row.ConstantItem(170).AlignRight()
                                    .Text($"{Num(r.TotalTests)} tests  |  {Pct(r.TotalTests, grand)} of total")
                                    .FontSize(8).FontColor(White);
                            });

                            block.Item().Table(table =>
                            {
                                table.ColumnsDefinition(c =>
                                {
                                    c.RelativeColumn(3.4f);
                                    c.RelativeColumn(1.8f);
                                    c.RelativeColumn(1.2f);
                                    c.RelativeColumn(1.3f);
                                    c.RelativeColumn(1.1f);
                                });
                                table.Header(h =>
                                {
                                    HeaderCellLight(h.Cell(), "Test Type");
                                    HeaderCellLight(h.Cell(), "Sample Type");
                                    HeaderCellLight(h.Cell(), "Tests", CellAlign.Right);
                                    HeaderCellLight(h.Cell(), "% of Category", CellAlign.Right);
                                    HeaderCellLight(h.Cell(), "Abnormal", CellAlign.Right);
                                });

                                bool alt = false;
                                foreach (var t in r.TestTypes)
                                {
                                    var bg = alt ? LightBg : White;
                                    var muted = t.TotalTests == 0 ? MidGray : null;
                                    DataCell(table.Cell(), Label(t.TestType, t.IsArchived), bg, color: muted);
                                    DataCell(table.Cell(), t.SampleType, bg, color: muted);
                                    DataCell(table.Cell(), Num(t.TotalTests), bg, CellAlign.Right, bold: true, color: muted);
                                    DataCell(table.Cell(), Pct(t.TotalTests, r.TotalTests), bg, CellAlign.Right, color: muted);
                                    DataCell(table.Cell(), Num(t.AbnormalTests), bg, CellAlign.Right, color: muted);
                                    alt = !alt;
                                }

                                TotalCell(table.Cell(), $"Subtotal - {r.Category}");
                                TotalCell(table.Cell(), "");
                                TotalCell(table.Cell(), Num(r.TotalTests), CellAlign.Right);
                                TotalCell(table.Cell(), "100.0%", CellAlign.Right);
                                TotalCell(table.Cell(), Num(r.AbnormalTests), CellAlign.Right);
                            });
                        });
                    }
                });

                // ── Notes ──
                col.Item().PaddingTop(16).ShowEntire().Border(1).BorderColor(Border).Padding(10).Column(n =>
                {
                    n.Item().Text("Notes").Bold().FontSize(8.5f);
                    n.Item().PaddingTop(3).Text("• A test is counted as performed when its result was captured within the report period.")
                        .FontSize(7.5f).FontColor(MidGray);
                    n.Item().Text("• Abnormal: result outside the reference range that applied at the time of testing.")
                        .FontSize(7.5f).FontColor(MidGray);
                    n.Item().Text("• Archived categories and test types are shown only when they had tests in the period.")
                        .FontSize(7.5f).FontColor(MidGray);
                });

                col.Item().ShowEntire().Element(c => c.Column(s => SignatureRow(s, data.GeneratedBy)));
            });
        }

        // Section heading with a short description of how the section is sorted/filtered.
        private void SectionHeading(ColumnDescriptor col, string title, string description)
        {
            col.Item().BorderBottom(1.5f).BorderColor(Navy).PaddingBottom(3).Row(row =>
            {
                row.RelativeItem().Text(title).Bold().FontSize(11).FontColor(Navy);
                row.RelativeItem().AlignRight().AlignBottom().Text(description).FontSize(7).FontColor(MidGray);
            });
            col.Item().Height(6);
        }

        // Bold total / subtotal cell with a heavier top rule.
        private void TotalCell(QContainer cell, string text, CellAlign align = CellAlign.Left)
        {
            var c = cell.Background(LightBg).BorderTop(1).BorderBottom(1).BorderColor(Navy)
                .PaddingVertical(6).PaddingHorizontal(8);
            Aligned(c, align).Text(text).Bold().FontSize(8.5f).FontColor(Navy);
        }

        // ═══════════════════════════ HELPERS ════════════════════════════════

        private void DoctorRequestsContent(QContainer container, DoctorRequestsReportData data)
        {
            container.PaddingHorizontal(20).PaddingVertical(14).Column(col =>
            {
                InfoRow(col, new[]
                {
                    ("Doctor",         Dr(data.DoctorName)),
                    ("HPCSA No.",      data.HpcsaNumber),
                    ("Date Range",     Range(data.FromDate, data.ToDate)),
                    ("Priority",       string.IsNullOrWhiteSpace(data.UrgencyFilter) ? "All priorities" : data.UrgencyFilter),
                    ("Total Requests", data.Rows.Count.ToString())
                });
                col.Item().Height(12);

                if (data.Rows.Count == 0)
                {
                    col.Item().Background(LightBg).Border(1).BorderColor(Border).Padding(20).AlignCenter()
                        .Text("No test requests were found for the selected date range and filters.")
                        .FontSize(10).FontColor(MidGray);
                    return;
                }

                col.Item().Table(table =>
                {
                    table.ColumnsDefinition(c =>
                    {
                        c.RelativeColumn(1.1f);   // request
                        c.RelativeColumn(2.2f);   // patient
                        c.RelativeColumn(4f);     // tests
                        c.RelativeColumn(1.1f);   // priority
                        c.RelativeColumn(1.6f);   // status
                        c.RelativeColumn(1.6f);   // date
                    });

                    // table.Header repeats on every page
                    table.Header(h =>
                    {
                        HeaderCellNavy(h.Cell(), "Request", CellAlign.Center);
                        HeaderCellNavy(h.Cell(), "Patient");
                        HeaderCellNavy(h.Cell(), "Tests");
                        HeaderCellNavy(h.Cell(), "Priority", CellAlign.Center);
                        HeaderCellNavy(h.Cell(), "Status", CellAlign.Center);
                        HeaderCellNavy(h.Cell(), "Requested", CellAlign.Right);
                    });

                    bool alt = false;
                    foreach (var r in data.Rows)
                    {
                        var bg = alt ? LightBg : White;
                        var pc = r.Urgency.Equals("STAT", StringComparison.OrdinalIgnoreCase) ? Red
                               : r.Urgency.Equals("Urgent", StringComparison.OrdinalIgnoreCase) ? "#ea580c" : Navy;

                        DataCell(table.Cell(), r.RequestNumber, bg, CellAlign.Center, bold: true, mono: true);
                        DataCell(table.Cell(), r.Patient, bg, bold: true);
                        DataCell(table.Cell(), r.Tests, bg);
                        DataCell(table.Cell(), r.Urgency, bg, CellAlign.Center, bold: true, color: pc);
                        DataCell(table.Cell(), r.Status, bg, CellAlign.Center);
                        DataCell(table.Cell(), r.RequestedOn.ToString("d MMM yyyy"), bg, CellAlign.Right);

                        alt = !alt;
                    }
                });
            });
        }

        private void InfoRow(ColumnDescriptor col, (string label, string value)[] fields)
        {
            col.Item().Background(LightBg).Border(1).BorderColor(Border).Padding(10).Row(row =>
            {
                foreach (var (label, value) in fields)
                    row.RelativeItem().Column(c =>
                    {
                        c.Item().Text(label.ToUpper()).FontSize(6.5f).FontColor(MidGray).Bold();
                        c.Item().Text(value).FontSize(8.5f).Bold().FontColor(Navy);
                    });
            });
        }

        private void SignatureRow(ColumnDescriptor col, string name)
        {
            col.Item().Height(20);
            col.Item().Row(row =>
            {
                row.RelativeItem().Column(sig =>
                {
                    sig.Item().Height(28).BorderBottom(1).BorderColor(Border);
                    sig.Item().PaddingTop(3).Text("Authorised by").FontSize(7).FontColor(MidGray);
                    sig.Item().Text(name).Bold().FontSize(8);
                });
                row.ConstantItem(50);
                row.RelativeItem().Column(sig =>
                {
                    sig.Item().Height(28).BorderBottom(1).BorderColor(Border);
                    sig.Item().PaddingTop(3).Text("Date").FontSize(7).FontColor(MidGray);
                    sig.Item().Text(Now.ToString("d MMMM yyyy")).Bold().FontSize(8);
                });
            });
        }

        // ── Table cell styling, shared by every report's data table ──
        //
        // Every table below is a real QuestPDF Table (not a stack of Rows), so
        // table.Header(...) repeats the header row on page breaks for long
        // reports - the previous Row-based "tables" only ever drew the header
        // once at the very top of the page they started on.

        private enum CellAlign { Left, Center, Right }

        private static QContainer Aligned(QContainer c, CellAlign align) => align switch
        {
            CellAlign.Center => c.AlignCenter(),
            CellAlign.Right => c.AlignRight(),
            _ => c
        };

        // Solid navy header cell - used by flat catalogue/list tables.
        private void HeaderCellNavy(QContainer cell, string text, CellAlign align = CellAlign.Left)
        {
            var c = cell.Background(Navy).BorderBottom(1).BorderColor(Border)
                .PaddingVertical(6).PaddingHorizontal(8);
            Aligned(c, align).Text(text.ToUpper()).Bold().FontSize(7).FontColor(White);
        }

        // Light header cell under a navy category banner - used by grouped result tables.
        private void HeaderCellLight(QContainer cell, string text, CellAlign align = CellAlign.Left)
        {
            var c = cell.Background(LightBg).BorderBottom(1).BorderColor(Border)
                .PaddingVertical(6).PaddingHorizontal(8);
            Aligned(c, align).Text(text.ToUpper()).Bold().FontSize(7).FontColor(Navy);
        }

        // Standard body cell: consistent padding/alignment and a crisp row-bottom rule.
        private void DataCell(QContainer cell, string? text, string rowBg,
            CellAlign align = CellAlign.Left, bool bold = false, string? color = null, bool mono = false)
        {
            var c = cell.Background(rowBg).BorderBottom(1).BorderColor(Border)
                .PaddingVertical(6).PaddingHorizontal(8);
            var t = Aligned(c, align).Text(text ?? "").FontSize(8.5f).FontColor(color ?? Navy);
            if (bold) t.Bold();
            if (mono) t.FontFamily("Courier New");
        }

        // Full-width note cell spanning every column of a table (via .ColumnSpan on
        // the caller's table.Cell()), so a result's lab note stays inside the
        // table's own borders instead of floating beneath it as a separate block.
        private void NoteCell(QContainer cell, string label, string note, string rowBg)
        {
            cell.Background(rowBg).BorderBottom(1).BorderColor(Border)
                .PaddingVertical(4).PaddingHorizontal(8)
                .Text(t =>
                {
                    t.Span(label).FontSize(7.5f).Bold().FontColor(MidGray);
                    t.Span(note).FontSize(7.5f).Italic().FontColor(MidGray);
                });
        }
    }

    // ═══════════════════════════ DATA TRANSFER OBJECTS ══════════════════════

    public class TestResultRow
    {
        public int RequestId { get; set; }
        public string TestName { get; set; } = "";
        public string Category { get; set; } = "";
        public string ResultValue { get; set; } = "";
        public string Unit { get; set; } = "";
        public decimal? NormalRangeMin { get; set; }
        public decimal? NormalRangeMax { get; set; }
        public string Notes { get; set; } = "";
        public bool IsAbnormal { get; set; }
        public DateTime ResultDate { get; set; }

        // Same number style everywhere (dot decimal, no trailing zeros) so "13.9" and "12 - 16.5" match.
        private static string F(decimal? v) => v!.Value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);

        // Formats the min/max into a display string, handling one-sided ranges.
        public string RangeDisplay =>
            (NormalRangeMin, NormalRangeMax) switch
            {
                (not null, not null) => $"{F(NormalRangeMin)} - {F(NormalRangeMax)}",
                (not null, null) => $"> {F(NormalRangeMin)}",
                (null, not null) => $"< {F(NormalRangeMax)}",
                _ => "-"
            };
    }

    public class PatientResultsReportData
    {
        public string PatientName { get; set; } = "";
        public string IdNumber { get; set; } = "";
        public DateTime DateOfBirth { get; set; }
        public string DoctorName { get; set; } = "";
        public string? DoctorNote { get; set; }
        public List<TestResultRow> Results { get; set; } = new();
    }

    public class TechnicianReportRow
    {
        public string RequestNumber { get; set; } = "";
        public string TestName { get; set; } = "";
        public string Category { get; set; } = "";
        public string ResultValue { get; set; } = "";
        public string Unit { get; set; } = "";
        public string NormalRange { get; set; } = "";
        public bool IsAbnormal { get; set; }
        public DateTime CompletedAt { get; set; }
    }

    public class TechnicianReportData
    {
        public string TechnicianName { get; set; } = "";
        public string EmployeeNumber { get; set; } = "";
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public List<TechnicianReportRow> Results { get; set; } = new();
    }

    public class PatientCategoryReportData
    {
        public string PatientName { get; set; } = "";
        public string IdNumber { get; set; } = "";
        public string Category { get; set; } = "";
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public List<TestResultRow> Results { get; set; } = new();
    }

    public class LabManagerCategoryReportRow
    {
        public string Category { get; set; } = "";
        public bool IsArchived { get; set; }
        public int TotalTests { get; set; }
        public int AbnormalTests { get; set; }
        public List<LabManagerTestTypeReportRow> TestTypes { get; set; } = new();
    }

    public class LabManagerTestTypeReportRow
    {
        public string TestType { get; set; } = "";
        public string SampleType { get; set; } = "";
        public bool IsArchived { get; set; }
        public int TotalTests { get; set; }
        public int AbnormalTests { get; set; }
    }

    public class LabManagerCategoryReportData
    {
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public string GeneratedBy { get; set; } = "Lab Manager";
        public List<LabManagerCategoryReportRow> Rows { get; set; } = new();
    }

    public class DoctorRequestsReportRow
    {
        public string RequestNumber { get; set; } = "";
        public string Patient { get; set; } = "";
        public string Tests { get; set; } = "";
        public string Urgency { get; set; } = "";
        public string Status { get; set; } = "";
        public DateTime RequestedOn { get; set; }
    }

    public class DoctorRequestsReportData
    {
        public string DoctorName { get; set; } = "";
        public string HpcsaNumber { get; set; } = "";
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public string? UrgencyFilter { get; set; }
        public List<DoctorRequestsReportRow> Rows { get; set; } = new();
    }
}
