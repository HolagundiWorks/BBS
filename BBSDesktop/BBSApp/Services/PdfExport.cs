using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace BBSApp.Services;

/// <summary>PDF reports and purchase orders (QuestPDF).</summary>
public static class PdfExport
{
    static PdfExport()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public static bool ExportProjectReport(
        string path,
        ProjectStore store,
        out string? error,
        AnnotatedDrawing? annotatedDrawing = null)
    {
        error = null;
        try
        {
            var sections = BuildReportSections(store);
            bool hasSteel = store.Columns.Count + store.Pedestals.Count + store.Beams.Count + store.Lintels.Count > 0;
            bool hasDrawing = annotatedDrawing is not null
                              || !string.IsNullOrWhiteSpace(store.Takeoff.PdfPath);

            Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4.Landscape());
                    page.Margin(28);
                    page.DefaultTextStyle(x => x.FontSize(9).FontFamily(Fonts.Calibri));

                    page.Header().Element(c => ReportHeader(c, store, "Project report"));
                    page.Footer().Element(c => ReportFooter(c, store));

                    page.Content().Column(col =>
                    {
                        col.Spacing(14);
                        if (sections.Count == 0 && !hasDrawing)
                        {
                            col.Item().Text("No element data in this project. Add RCC or civil BOQ items, then export again.");
                            return;
                        }

                        foreach (var sec in sections)
                            col.Item().Element(c => SectionBlock(c, sec));

                        if (hasSteel)
                        {
                            col.Item().PageBreak();
                            col.Item().Element(c => SketchPdf.DrawSteelArrangementSketches(c, store));
                        }

                        if (hasDrawing)
                        {
                            col.Item().PageBreak();
                            col.Item().Element(c => TakeoffAnnotatedPdf.Draw(c, store, annotatedDrawing));
                        }
                    });
                });
            }).GeneratePdf(path);
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    public static bool ExportPurchaseOrder(
        string path,
        ProjectStore store,
        IReadOnlySet<string> levels,
        IReadOnlyList<PoLine> steel,
        IReadOnlyList<PoLine> concreteByGrade,
        IReadOnlyList<ConcreteLine> concreteDetail,
        IReadOnlyList<PoLine> materials,
        bool rmcMode,
        out string? error)
    {
        error = null;
        try
        {
            var levelLabel = levels.Count == 0
                ? "none"
                : levels.Count == store.Levels.Count && store.Levels.Count > 0
                    ? "all storeys"
                    : string.Join(", ", levels.OrderBy(x => x));

            Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(28);
                    page.DefaultTextStyle(x => x.FontSize(9).FontFamily(Fonts.Calibri));

                    page.Header().Element(c => ReportHeader(c, store,
                        $"Purchase order · {levelLabel}" + (rmcMode ? " · RMC" : " · site batch")));
                    page.Footer().Element(c => ReportFooter(c, store));

                    page.Content().Column(col =>
                    {
                        col.Spacing(14);

                        col.Item().Element(c => TableSection(c, "Steel purchase order",
                            new[] { "Category", "Item", "Unit", "Qty", "Notes" },
                            steel.Select(p => new[]
                            {
                                p.Category, p.Item, p.Unit, p.Qty.ToString("0.##"), p.Notes
                            }).ToList()));

                        col.Item().Element(c => TableSection(c,
                            rmcMode ? "Concrete by grade (RMC m³)" : "Concrete by grade (m³)",
                            new[] { "Category", "Item", "Unit", "Qty", "Notes" },
                            concreteByGrade.Select(p => new[]
                            {
                                p.Category, p.Item, p.Unit, p.Qty.ToString("0.###"), p.Notes
                            }).ToList()));

                        if (!rmcMode)
                        {
                            col.Item().Element(c => TableSection(c, "Concrete BOQ detail (by element)",
                                new[] { "Storey", "Element", "Mark", "Grade", "Vol m³", "Cement bags", "Sand m³", "Agg m³" },
                                concreteDetail.Select(p => new[]
                                {
                                    p.Level, p.Element, p.Mark, p.Grade,
                                    p.VolumeM3.ToString("0.###"),
                                    p.CementBags.ToString("0.##"),
                                    p.SandM3.ToString("0.###"),
                                    p.AggregateM3.ToString("0.###")
                                }).ToList()));
                        }
                        else
                        {
                            col.Item().Element(c => TableSection(c, "Concrete detail by element (volume only)",
                                new[] { "Storey", "Element", "Mark", "Grade", "Vol m³" },
                                concreteDetail.Select(p => new[]
                                {
                                    p.Level, p.Element, p.Mark, p.Grade, p.VolumeM3.ToString("0.###")
                                }).ToList()));
                        }

                        col.Item().Element(c => TableSection(c,
                            rmcMode
                                ? "Other materials (civil — bricks, mortar, etc.; no RCC batching)"
                                : "Other materials (RCC batching + civil)",
                            new[] { "Category", "Item", "Unit", "Qty", "Notes" },
                            materials.Select(p => new[]
                            {
                                p.Category, p.Item, p.Unit, p.Qty.ToString("0.###"), p.Notes
                            }).ToList()));
                    });
                });
            }).GeneratePdf(path);
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    public static bool ExportEstimate(
        string path,
        ProjectStore store,
        EstimateResult result,
        IReadOnlySet<string> levels,
        out string? error,
        AnnotatedDrawing? annotatedDrawing = null)
    {
        error = null;
        try
        {
            var levelLabel = levels.Count == 0
                ? "none"
                : levels.Count == store.Levels.Count && store.Levels.Count > 0
                    ? "all storeys"
                    : string.Join(", ", levels.OrderBy(x => x));

            bool hasSteel = store.Columns.Count + store.Pedestals.Count + store.Beams.Count + store.Lintels.Count > 0;
            bool hasDrawing = annotatedDrawing is not null
                              || !string.IsNullOrWhiteSpace(store.Takeoff.PdfPath);

            Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4.Landscape());
                    page.Margin(28);
                    page.DefaultTextStyle(x => x.FontSize(7).FontFamily(Fonts.Calibri));

                    page.Header().Element(c => ReportHeader(c, store,
                        $"Abstract of cost (DSR) · {result.RateBookVersionName} · {levelLabel} · ₹ {result.GrandTotal:N2}"));
                    page.Footer().Element(c => ReportFooter(c, store));

                    page.Content().Column(col =>
                    {
                        col.Spacing(12);

                        var headers = DsrEstimateFormat.Headers;
                        int sl = 1;
                        void AddSection(string title, IReadOnlyList<EstimateLine> lines)
                        {
                            var rows = DsrEstimateFormat.ToStringRows(lines, sl);
                            sl += rows.Count;
                            col.Item().Element(c => TableSection(c, title, headers, rows));
                        }

                        AddSection("I. Civil / finishes / doors / windows", result.Civil);
                        AddSection("II. Materials", result.Materials);
                        AddSection("III. Steel reinforcement", result.Steel);

                        var mk = result.Markups;
                        col.Item().PaddingTop(4).Text($"Base total (₹) : {mk.BaseTotal:N2}").FontSize(9);
                        col.Item().Text($"Electrical {mk.ElectricalPct:0.##}% : ₹ {mk.ElectricalAmount:N2}").FontSize(8);
                        col.Item().Text($"Plumbing {mk.PlumbingPct:0.##}% : ₹ {mk.PlumbingAmount:N2}").FontSize(8);
                        col.Item().Text($"Escalation {mk.EscalationPct:0.##}% : ₹ {mk.EscalationAmount:N2}").FontSize(8);
                        col.Item().Text($"Consulting / PMC fees {mk.ConsultingFeePct:0.##}% : ₹ {mk.ConsultingFeeAmount:N2}").FontSize(8);
                        col.Item().PaddingTop(4).Text($"Grand total (₹) : {result.GrandTotal:N2}").Bold().FontSize(11);
                        if (result.MissingCodes.Count > 0)
                            col.Item().Text("Missing rates: " + string.Join(", ", result.MissingCodes)).FontColor(Colors.Red.Medium);

                        if (hasSteel)
                        {
                            col.Item().PageBreak();
                            col.Item().Element(c => SketchPdf.DrawSteelArrangementSketches(c, store));
                        }

                        if (hasDrawing)
                        {
                            col.Item().PageBreak();
                            col.Item().Element(c => TakeoffAnnotatedPdf.Draw(c, store, annotatedDrawing));
                        }
                    });
                });
            }).GeneratePdf(path);
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    public static bool ExportSchedule(string path, ProjectStore store, out string? error)
    {
        error = null;
        try
        {
            var schedule = store.Schedule;
            var result = ScheduleCalculator.Compute(schedule);
            var headers = new[] { "#", "Activity", "WBS", "Dur (d)", "Start", "Finish", "Total float", "Critical", "Preds" };
            var rows = new List<string[]>();
            for (int i = 0; i < schedule.Activities.Count; i++)
            {
                var a = schedule.Activities[i];
                string preds = string.Join(", ", a.Links
                    .Select(l => schedule.Find(l.PredecessorId))
                    .Where(p => p is not null)
                    .Select(p => schedule.IndexOf(p!).ToString()));
                rows.Add(new[]
                {
                    (i + 1).ToString(),
                    a.Name,
                    a.Wbs,
                    a.DurationDays.ToString("0.#"),
                    schedule.DateForOffset(a.EarlyStart).ToString("dd MMM yyyy"),
                    schedule.DateForOffset(a.EarlyFinish).ToString("dd MMM yyyy"),
                    a.InCycle ? "—" : a.TotalFloat.ToString("0.#"),
                    a.InCycle ? "cycle" : a.IsCritical ? "Yes" : "",
                    preds
                });
            }

            Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4.Landscape());
                    page.Margin(28);
                    page.DefaultTextStyle(x => x.FontSize(9).FontFamily(Fonts.Calibri));
                    page.Header().Element(c => ReportHeader(c, store,
                        $"Project schedule (CPM) · {result.ProjectDurationDays:0.#} working days · finish {result.FinishDate:dd MMM yyyy}"));
                    page.Footer().Element(c => ReportFooter(c, store));
                    page.Content().Column(col =>
                    {
                        col.Spacing(12);
                        col.Item().Text(
                            $"Start {schedule.StartDate:dd MMM yyyy} · {schedule.WorkingDaysPerWeek}-day week · "
                            + $"{result.ActivityCount} activities · {result.CriticalCount} on critical path"
                            + (result.HasCycle ? "  ·  WARNING: circular dependency present" : "")).FontSize(9);
                        col.Item().Element(c => TableSection(c, "Activity schedule", headers, rows));
                    });
                });
            }).GeneratePdf(path);
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    public static bool ExportOfficeDocument(string path, ProjectStore store, OfficeDocument doc, out string? error)
    {
        error = null;
        try
        {
            var info = store.Info;
            string number = string.IsNullOrWhiteSpace(doc.Number)
                ? store.Office.PreviewNumber(doc, info.CompanyDisplay) + "  (draft)"
                : doc.Number;
            string typeName = DocTypeInfo.DisplayFor(doc.TypeCode);
            string signName = string.IsNullOrWhiteSpace(doc.SignatoryName) ? info.PreparedByName : doc.SignatoryName;
            string signRole = string.IsNullOrWhiteSpace(doc.SignatoryRole) ? info.PreparedByRole : doc.SignatoryRole;
            bool hasTo = DocTypeInfo.HasRecipient(doc.TypeCode) && !string.IsNullOrWhiteSpace(doc.ToName);

            Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(40);
                    page.DefaultTextStyle(x => x.FontSize(11).FontFamily(Fonts.Calibri));
                    page.Footer().Element(c => ReportFooter(c, store));
                    page.Content().Column(col =>
                    {
                        col.Spacing(10);

                        // Letterhead
                        col.Item().Row(row =>
                        {
                            var logoPath = info.ResolvedLogoPath;
                            if (logoPath is not null)
                                row.ConstantItem(64).Height(52).PaddingRight(10).AlignMiddle().Image(logoPath).FitArea();
                            row.RelativeItem().Column(c =>
                            {
                                c.Item().Text(info.CompanyDisplay).SemiBold().FontSize(16);
                                if (!string.IsNullOrWhiteSpace(info.Address))
                                    c.Item().Text(info.Address).FontSize(8).FontColor(Colors.Grey.Darken2);
                                var bits = new List<string>();
                                if (!string.IsNullOrWhiteSpace(info.ContactPhone)) bits.Add(info.ContactPhone);
                                if (!string.IsNullOrWhiteSpace(info.ContactEmail)) bits.Add(info.ContactEmail);
                                if (bits.Count > 0) c.Item().Text(string.Join("  ·  ", bits)).FontSize(8).FontColor(Colors.Grey.Darken2);
                                if (!string.IsNullOrWhiteSpace(info.RegistrationLine))
                                    c.Item().Text(info.RegistrationLine).FontSize(8).FontColor(Colors.Grey.Darken1);
                            });
                        });
                        col.Item().PaddingVertical(2).LineHorizontal(1.5f).LineColor(Colors.Grey.Darken1);

                        // Number / date
                        col.Item().PaddingTop(6).Row(row =>
                        {
                            row.RelativeItem().Text(t => { t.Span("No: ").SemiBold(); t.Span(number); });
                            row.RelativeItem().AlignRight().Text(t =>
                            {
                                t.Span("Date: ").SemiBold();
                                t.Span(doc.IssueDate.ToString("dd MMM yyyy"));
                            });
                        });

                        // Recipient
                        if (hasTo)
                        {
                            col.Item().PaddingTop(6).Text("To,").SemiBold();
                            col.Item().Text(doc.ToName);
                            foreach (var line in SplitLines(doc.ToAddress))
                                col.Item().Text(line);
                        }

                        // Subject / title
                        col.Item().PaddingTop(10).AlignCenter().Text($"{typeName}".ToUpperInvariant())
                            .SemiBold().FontSize(12).FontColor(Colors.Grey.Darken2);
                        if (!string.IsNullOrWhiteSpace(doc.Subject))
                            col.Item().PaddingTop(2).Text(t =>
                            {
                                t.Span("Subject: ").SemiBold();
                                t.Span(doc.Subject).SemiBold();
                            });

                        // Body
                        col.Item().PaddingTop(10);
                        foreach (var line in SplitLines(doc.Body))
                        {
                            if (line.Length == 0) col.Item().Height(6);
                            else col.Item().Text(line).LineHeight(1.35f);
                        }

                        // Signature
                        col.Item().PaddingTop(28).AlignRight().Column(c =>
                        {
                            c.Item().Text($"For {info.CompanyDisplay}").SemiBold();
                            c.Item().Height(34);
                            if (!string.IsNullOrWhiteSpace(signName)) c.Item().Text(signName).SemiBold();
                            if (!string.IsNullOrWhiteSpace(signRole)) c.Item().Text(signRole).FontSize(9).FontColor(Colors.Grey.Darken2);
                        });
                    });
                });
            }).GeneratePdf(path);
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private static IEnumerable<string> SplitLines(string? s) =>
        (s ?? "").Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');

    private sealed class ReportSection
    {
        public string Title { get; init; } = "";
        public string? Note { get; init; }
        public List<(string Title, GenTable Table)> Tables { get; } = new();
    }

    private static List<ReportSection> BuildReportSections(ProjectStore store)
    {
        var list = new List<ReportSection>();
        void addSteel(string kind, string title, IEnumerable<Dictionary<string, string>> rows)
        {
            var rowList = rows.ToList();
            if (rowList.Count == 0) return;
            var expanded = MemberSheetHelper.ExpandForGenerate(kind, rowList);
            var res = EngineClient.Generate(MemberSheetHelper.EngineKind(kind), store.SettingsJson(), expanded);
            var sec = new ReportSection
            {
                Title = title,
                Note = res.Ok ? null : (res.Error ?? "Generate failed")
            };
            if (res.Ok)
            {
                if (res.Bbs.Headers.Count > 0)
                    sec.Tables.Add(("Bar bending schedule", res.Bbs));
                if (res.Summary.Headers.Count > 0)
                    sec.Tables.Add(("Steel summary", res.Summary));
                if (res.Checks.Headers.Count > 0 && res.Checks.Rows.Count > 0)
                    sec.Tables.Add(("Checks", res.Checks));
            }
            list.Add(sec);
        }

        void addCivil(string kind, string title, IEnumerable<Dictionary<string, string>> rows)
        {
            var rowList = rows.ToList();
            if (rowList.Count == 0) return;
            var res = CivilBoqCalculator.Generate(kind, rowList);
            var sec = new ReportSection { Title = title };
            if (res.Bbs.Headers.Count > 0)
                sec.Tables.Add(("Quantity take-off", res.Bbs));
            if (res.Summary.Headers.Count > 0)
                sec.Tables.Add(("Summary / materials", res.Summary));
            list.Add(sec);
        }

        addSteel("columns", "Columns", store.Columns);
        addSteel("beams", "Beams", store.Beams);
        addSteel("pedestals", "Pedestals", store.Pedestals);
        addSteel("lintels", "Lintels", store.Lintels);
        addSteel("slabs", "Slabs", store.Slabs);
        addSteel("footings", "Footings", store.Footings);
        addSteel("walls", "Retaining walls", store.Walls);
        addSteel("stairs", "Stairs", store.Stairs);
        addCivil("waterproofing", "Waterproofing", store.Waterproofing);
        addCivil("dpc", "Damp-proof course", store.Dpc);
        addCivil("coping", "Coping", store.Coping);
        addCivil("screed", "Screed", store.Screed);
        addCivil("vdf", "VDF flooring", store.Vdf);
        addCivil("skirting", "Skirting", store.Skirting);
        addCivil("parapet", "Parapet", store.Parapet);
        addCivil("plinth_protection", "Plinth protection", store.PlinthProtection);

        addCivil("masonry", "Masonry walls", store.MasonryWalls);
        addCivil("plaster", "Plastering", store.Plaster);
        addCivil("pcc", "PCC bed", store.PccBeds);
        addCivil("earthwork", "Earthwork", store.Earthwork);
        addCivil("ssm", "Size stone masonry", store.SizeStone);
        addCivil("shuttering", "Shuttering / formwork", store.Shuttering);
        addCivil("flooring", "Flooring", store.Flooring);
        FinishSurfacesCalculator.SyncPaintingFromPlaster(store);
        addCivil("painting", "Painting", store.Painting);
        addCivil("doors", "Doors", store.Doors);
        addCivil("windows", "Windows", store.Windows);

        // Project-level concrete by grade + materials
        var concrete = MaterialsCalculator.BuildConcreteBoq(store);
        var civil = CivilBoqCalculator.BuildAll(store);
        if (concrete.Count > 0 || civil.Count > 0)
        {
            var rmc = store.ConcreteFromRmc;
            var byGrade = MaterialsCalculator.ConcreteByGrade(concrete, rmc);
            var mats = MaterialsCalculator.MaterialPurchaseOrder(concrete, includeConcreteSplit: !rmc)
                .Concat(CivilBoqCalculator.MaterialPurchaseOrder(civil))
                .ToList();

            var overview = new ReportSection { Title = "Project materials overview" };
            if (byGrade.Count > 0)
            {
                overview.Tables.Add((rmc ? "Concrete by grade (RMC m³)" : "Concrete by grade (m³)",
                    new GenTable
                    {
                        Headers = new List<string> { "Category", "Item", "Unit", "Qty", "Notes" },
                        Rows = byGrade.Select(p => new List<string>
                        {
                            p.Category, p.Item, p.Unit, p.Qty.ToString("0.###"), p.Notes
                        }).ToList()
                    }));
            }
            if (mats.Count > 0)
            {
                overview.Tables.Add((rmc ? "Other materials (civil only)" : "Other materials (RCC batch + civil)",
                    new GenTable
                    {
                        Headers = new List<string> { "Category", "Item", "Unit", "Qty", "Notes" },
                        Rows = mats.Select(p => new List<string>
                        {
                            p.Category, p.Item, p.Unit, p.Qty.ToString("0.###"), p.Notes
                        }).ToList()
                    }));
            }
            if (overview.Tables.Count > 0)
                list.Add(overview);
        }

        return list;
    }

    private static void ReportHeader(IContainer container, ProjectStore store, string subtitle)
    {
        var info = store.Info;
        container.Column(col =>
        {
            col.Item().Row(row =>
            {
                var logoPath = info.ResolvedLogoPath;
                if (logoPath is not null)
                {
                    row.ConstantItem(52).Height(40).PaddingRight(8)
                        .AlignMiddle().Image(logoPath).FitArea();
                }

                row.RelativeItem().Column(c =>
                {
                    c.Item().Text(info.CompanyDisplay).SemiBold().FontSize(12);
                    c.Item().Text($"{Branding.AppName} · {subtitle}").FontSize(9).FontColor(Colors.Grey.Darken2);
                    if (!string.IsNullOrWhiteSpace(info.ContactPhone) || !string.IsNullOrWhiteSpace(info.ContactEmail))
                    {
                        var bits = new List<string>();
                        if (!string.IsNullOrWhiteSpace(info.ContactPhone)) bits.Add(info.ContactPhone);
                        if (!string.IsNullOrWhiteSpace(info.ContactEmail)) bits.Add(info.ContactEmail);
                        c.Item().Text(string.Join(" · ", bits)).FontSize(7).FontColor(Colors.Grey.Darken1);
                    }
                    if (!string.IsNullOrWhiteSpace(info.Address))
                        c.Item().Text(info.Address).FontSize(7).FontColor(Colors.Grey.Darken1);
                    if (!string.IsNullOrWhiteSpace(info.RegistrationLine))
                        c.Item().Text(info.RegistrationLine).FontSize(7).FontColor(Colors.Grey.Darken1);
                });

                row.ConstantItem(200).AlignRight().Column(c =>
                {
                    c.Item().Text(info.Name).SemiBold().FontSize(10);
                    if (!string.IsNullOrWhiteSpace(info.Location))
                        c.Item().Text(info.Location).FontSize(8).FontColor(Colors.Grey.Darken1);
                    if (!string.IsNullOrWhiteSpace(info.ClientName))
                        c.Item().Text($"Client: {info.ClientName}").FontSize(8).FontColor(Colors.Grey.Darken1);
                    c.Item().Text(info.PreparedByLine).FontSize(8).FontColor(Colors.Grey.Darken1);
                    c.Item().Text(DateTime.Now.ToString("dd MMM yyyy HH:mm")).FontSize(7).FontColor(Colors.Grey.Darken1);
                });
            });
            col.Item().PaddingTop(6).LineHorizontal(1).LineColor(Colors.Grey.Lighten1);
        });
    }

    private static void ReportFooter(IContainer container, ProjectStore store)
    {
        var company = store.Info.CompanyDisplay;
        container.AlignCenter().Text(t =>
        {
            t.Span($"{Branding.AppName} · {company}  ·  ").FontSize(8).FontColor(Colors.Grey.Darken1);
            t.Span("Page ").FontSize(8).FontColor(Colors.Grey.Darken1);
            t.CurrentPageNumber().FontSize(8).FontColor(Colors.Grey.Darken1);
            t.Span(" / ").FontSize(8).FontColor(Colors.Grey.Darken1);
            t.TotalPages().FontSize(8).FontColor(Colors.Grey.Darken1);
        });
    }

    private static void SectionBlock(IContainer container, ReportSection sec)
    {
        container.Column(col =>
        {
            col.Spacing(6);
            col.Item().Text(sec.Title).SemiBold().FontSize(12);
            if (!string.IsNullOrWhiteSpace(sec.Note))
                col.Item().Text(sec.Note).FontColor(Colors.Red.Medium).FontSize(9);

            foreach (var (title, table) in sec.Tables)
            {
                col.Item().Text(title).SemiBold().FontSize(10).FontColor(Colors.Grey.Darken2);
                col.Item().Element(c => DataTable(c, table.Headers, table.Rows));
            }
        });
    }

    private static void TableSection(IContainer container, string title, IReadOnlyList<string> headers, IReadOnlyList<string[]> rows)
    {
        container.Column(col =>
        {
            col.Spacing(4);
            col.Item().Text(title).SemiBold().FontSize(11);
            col.Item().Element(c => DataTable(c, headers, rows.Select(r => (IReadOnlyList<string>)r).ToList()));
        });
    }

    private static void DataTable(IContainer container, IReadOnlyList<string> headers, IReadOnlyList<IReadOnlyList<string>> rows)
    {
        if (headers.Count == 0)
        {
            container.Text("— no data —").FontColor(Colors.Grey.Medium);
            return;
        }

        container.Table(table =>
        {
            table.ColumnsDefinition(cols =>
            {
                for (var i = 0; i < headers.Count; i++)
                    cols.RelativeColumn();
            });

            table.Header(header =>
            {
                foreach (var h in headers)
                    header.Cell().Element(CellHeader).Text(h).SemiBold().FontSize(8);
            });

            if (rows.Count == 0)
            {
                table.Cell().ColumnSpan((uint)headers.Count).Element(CellBody)
                    .Text("No lines").FontColor(Colors.Grey.Medium);
                return;
            }

            var alt = false;
            foreach (var row in rows)
            {
                for (var i = 0; i < headers.Count; i++)
                {
                    var cell = i < row.Count ? row[i] : "";
                    var bg = alt ? Colors.Grey.Lighten4 : Colors.White;
                    table.Cell().Element(c => CellBody(c, bg)).Text(cell ?? "").FontSize(8);
                }
                alt = !alt;
            }
        });
    }

    private static IContainer CellHeader(IContainer c) =>
        c.BorderBottom(1).BorderColor(Colors.Grey.Darken1)
            .Background(Colors.Grey.Lighten3)
            .PaddingVertical(4).PaddingHorizontal(3);

    private static IContainer CellBody(IContainer c) => CellBody(c, Colors.White);

    private static IContainer CellBody(IContainer c, string background) =>
        c.BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2)
            .Background(background)
            .PaddingVertical(3).PaddingHorizontal(3);
}
