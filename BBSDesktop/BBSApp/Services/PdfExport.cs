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

    public static bool ExportProjectReport(string path, ProjectStore store, out string? error)
    {
        error = null;
        try
        {
            var sections = BuildReportSections(store);
            Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4.Landscape());
                    page.Margin(28);
                    page.DefaultTextStyle(x => x.FontSize(9).FontFamily(Fonts.Calibri));

                    page.Header().Element(c => ReportHeader(c, store.Name, "BOQ / BBS project report"));
                    page.Footer().Element(ReportFooter);

                    page.Content().Column(col =>
                    {
                        col.Spacing(14);
                        if (sections.Count == 0)
                        {
                            col.Item().Text("No element data in this project. Add RCC or civil BOQ items, then export again.");
                            return;
                        }

                        foreach (var sec in sections)
                            col.Item().Element(c => SectionBlock(c, sec));
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

                    page.Header().Element(c => ReportHeader(c, store.Name,
                        $"Purchase order · {levelLabel}" + (rmcMode ? " · RMC" : " · site batch")));
                    page.Footer().Element(ReportFooter);

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
        addCivil("painting", "Painting", store.Painting);

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

    private static void ReportHeader(IContainer container, string projectName, string subtitle)
    {
        container.Column(col =>
        {
            col.Item().Row(row =>
            {
                row.RelativeItem().Column(c =>
                {
                    c.Item().Text(Branding.AppName).SemiBold().FontSize(14);
                    c.Item().Text(subtitle).FontSize(10).FontColor(Colors.Grey.Darken2);
                });
                row.ConstantItem(180).AlignRight().Column(c =>
                {
                    c.Item().Text(projectName).SemiBold().FontSize(10);
                    c.Item().Text(Branding.CompanyLine).FontSize(8).FontColor(Colors.Grey.Darken1);
                    c.Item().Text(DateTime.Now.ToString("dd MMM yyyy HH:mm")).FontSize(8).FontColor(Colors.Grey.Darken1);
                });
            });
            col.Item().PaddingTop(6).LineHorizontal(1).LineColor(Colors.Grey.Lighten1);
        });
    }

    private static void ReportFooter(IContainer container)
    {
        container.AlignCenter().Text(t =>
        {
            t.Span($"{Branding.AppName} · {Branding.CompanyLine}  ·  ").FontSize(8).FontColor(Colors.Grey.Darken1);
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
