using System;
using AgroCore.ControlCenter.Models;
using AgroCore.Shared.DTOs;
using AgroCore.Shared.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace AgroCore.ControlCenter.Services
{
    /// <summary>
    /// Nekazari baten PDF txostena sortzen du QuestPDF liburutegiarekin.
    ///
    /// Datuak (ReportData) ZERBITZARIAK kalkulatzen ditu eta eskatutako ustiategia bakarrik dute
    /// (zerbitzariak egiaztatzen du erabiltzaileak hura ikusteko baimena duela). Hemen maketatu bakarrik egiten dira.
    ///
    /// QuestPDF dokumentua "deskribatuz" erabiltzen da: orria -> goiburua, edukia eta orri-oina;
    /// edukia blokeak dituen zutabe bat da (izenburuak, taulak...).
    /// </summary>
    public static class PdfReportGenerator
    {
        private static readonly string Green = "#2E7D32";
        private static readonly string LightGreen = "#E8F5E9";

        public static void Create(ReportData report, string path)
        {
            QuestPDF.Settings.License = LicenseType.Community; // doako lizentzia (hezkuntza-erabilera)

            Document document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(36);
                    page.DefaultTextStyle(style => style.FontSize(9.5f).FontFamily("Segoe UI"));

                    page.Header().Element(header => ComposeHeader(header, report));
                    page.Content().PaddingTop(14).Element(content => ComposeContent(content, report));
                    page.Footer().AlignCenter().Text(text =>
                    {
                        text.DefaultTextStyle(style => style.FontSize(8).FontColor(Colors.Grey.Darken1));
                        text.Span("AGROCORE · " + report.FarmName + " · Orria ");
                        text.CurrentPageNumber();
                        text.Span(" / ");
                        text.TotalPages();
                    });
                });
            });

            document.GeneratePdf(path);
        }

        // ==================================================================
        // GOIBURUA
        // ==================================================================

        private static void ComposeHeader(IContainer container, ReportData report)
        {
            container.Column(column =>
            {
                column.Item().Row(row =>
                {
                    row.RelativeItem().Column(title =>
                    {
                        title.Item().Text("AGROCORE").FontSize(24).Bold().FontColor(Green);
                        title.Item().Text("ERAGIKETA-TXOSTENA").FontSize(13).SemiBold().FontColor(Colors.Grey.Darken3);
                    });
                    row.ConstantItem(190).AlignRight().Column(info =>
                    {
                        info.Item().AlignRight().Text("Sortua: " + report.GeneratedAt.ToString("dd/MM/yyyy HH:mm")).FontSize(8);
                        info.Item().AlignRight().Text("Egilea: " + report.GeneratedBy).FontSize(8);
                    });
                });

                column.Item().PaddingTop(10).Background(LightGreen).Padding(10).Row(row =>
                {
                    row.RelativeItem().Column(c =>
                    {
                        c.Item().Text(text => { text.Span("Nekazaria: ").SemiBold(); text.Span(report.FarmerName); });
                        c.Item().Text(text => { text.Span("Ustiategia: ").SemiBold(); text.Span(report.FarmName); });
                    });
                    row.RelativeItem().Column(c =>
                    {
                        c.Item().Text(text =>
                        {
                            text.Span("Aldia: ").SemiBold();
                            text.Span(report.From.ToString("dd/MM/yyyy HH:mm") + " - " + report.To.ToString("dd/MM/yyyy HH:mm"));
                        });
                        c.Item().Text(text =>
                        {
                            text.Span("Flota: ").SemiBold();
                            text.Span(report.Machines.Count + " makina · " + report.Fields.Count + " soro");
                        });
                    });
                });
            });
        }

        // ==================================================================
        // EDUKIA
        // ==================================================================

        private static void ComposeContent(IContainer container, ReportData report)
        {
            FarmStatistics s = report.Statistics;

            container.Column(column =>
            {
                column.Spacing(14);

                // ---- 1. Laburpena ----
                column.Item().Element(c => SectionTitle(c, "1. Aldiaren laburpena"));
                column.Item().Table(table =>
                {
                    table.ColumnsDefinition(cols =>
                    {
                        cols.RelativeColumn(); cols.RelativeColumn(); cols.RelativeColumn(); cols.RelativeColumn();
                    });
                    Kpi(table, "Osatutako zereginak", s.TasksCompleted.ToString());
                    Kpi(table, "Bertan behera utzitako zereginak", s.TasksCancelled.ToString());
                    Kpi(table, "Etendako zereginak", s.TasksAborted.ToString());
                    Kpi(table, "Egindako distantzia", s.DistanceKm.ToString("0.00") + " km");
                    Kpi(table, "Ereindako soroak", s.FieldsSown.ToString());
                    Kpi(table, "Ureztatutako soroak", s.FieldsIrrigated.ToString());
                    Kpi(table, "Fumigatutako soroak", s.FieldsSprayed.ToString());
                    Kpi(table, "Uzta bildutako soroak", s.FieldsHarvested.ToString());
                    Kpi(table, "Jarduera-denbora", s.TotalRuntimeMinutes.ToString("0") + " min");
                    Kpi(table, "Zain emandako denbora", s.WaitingMinutes.ToString("0") + " min");
                    Kpi(table, "Kargatzen emandako denbora", s.ChargingMinutes.ToString("0") + " min");
                    Kpi(table, "Kontsumitutako bateria", s.BatteryConsumed.ToString("0") + " %");
                    Kpi(table, "Saihestutako gatazkak", s.ConflictsAvoided.ToString());
                    Kpi(table, "Saihestutako elkarblokeoak", s.DeadlocksAvoided.ToString());
                    Kpi(table, "Bateria-kargak", s.ChargeSessions.ToString());
                    Kpi(table, "Larrialdiko geldialdiak", s.Emergencies.ToString());
                });

                // ---- 2. Soroak ----
                column.Item().Element(c => SectionTitle(c, "2. Soroen egoera"));
                column.Item().Table(table =>
                {
                    table.ColumnsDefinition(cols =>
                    {
                        cols.RelativeColumn(1.2f); cols.RelativeColumn(1.6f); cols.RelativeColumn(1.2f);
                        cols.RelativeColumn(1); cols.RelativeColumn(1.6f); cols.RelativeColumn(1);
                    });
                    table.Header(header =>
                    {
                        HeaderCell(header.Cell(), "Soroa");
                        HeaderCell(header.Cell(), "Egoera");
                        HeaderCell(header.Cell(), "Laborea");
                        HeaderCell(header.Cell(), "Hazkuntza");
                        HeaderCell(header.Cell(), "Plana");
                        HeaderCell(header.Cell(), "Uzta-bilketak");
                    });
                    foreach (Field field in report.Fields)
                    {
                        Cell(table.Cell(), field.Name);
                        Cell(table.Cell(), Texts.FieldStatus(field.Status));
                        Cell(table.Cell(), Texts.Crop(field.Crop ?? field.PlannedCrop));
                        Cell(table.Cell(), field.Growth.ToString("0") + " %");
                        Cell(table.Cell(), field.PlannedCrop.HasValue ? Texts.Crop(field.PlannedCrop) + (field.AutoReplant ? " (auto)" : "") : "—");
                        Cell(table.Cell(), field.TimesHarvested.ToString());
                    }
                });

                // ---- 3. Makinak ----
                column.Item().Element(c => SectionTitle(c, "3. Jarduera makinaka"));
                column.Item().Table(table =>
                {
                    table.ColumnsDefinition(cols =>
                    {
                        cols.RelativeColumn(1.4f); cols.RelativeColumn(1.4f); cols.RelativeColumn(1);
                        cols.RelativeColumn(1); cols.RelativeColumn(1); cols.RelativeColumn(1);
                        cols.RelativeColumn(0.8f); cols.RelativeColumn(0.8f); cols.RelativeColumn(1);
                    });
                    table.Header(header =>
                    {
                        HeaderCell(header.Cell(), "Makina");
                        HeaderCell(header.Cell(), "Mota");
                        HeaderCell(header.Cell(), "Distantzia");
                        HeaderCell(header.Cell(), "Lanean");
                        HeaderCell(header.Cell(), "Zain");
                        HeaderCell(header.Cell(), "Kargatzen");
                        HeaderCell(header.Cell(), "Zereginak");
                        HeaderCell(header.Cell(), "Kargak");
                        HeaderCell(header.Cell(), "Eraginkortasuna");
                    });
                    foreach (MachineStatistics m in s.Machines)
                    {
                        Cell(table.Cell(), m.Name);
                        Cell(table.Cell(), Texts.MachineType(m.Type));
                        Cell(table.Cell(), m.DistanceKm.ToString("0.00") + " km");
                        Cell(table.Cell(), m.WorkingMinutes.ToString("0.0") + " min");
                        Cell(table.Cell(), m.WaitingMinutes.ToString("0.0") + " min");
                        Cell(table.Cell(), m.ChargingMinutes.ToString("0.0") + " min");
                        Cell(table.Cell(), m.TasksCompleted.ToString());
                        Cell(table.Cell(), m.ChargeCycles.ToString());
                        Cell(table.Cell(), m.Efficiency.ToString("0") + " %");
                    }
                });

                // ---- 4. Zereginak ----
                column.Item().Element(c => SectionTitle(c, "4. Aldiko zereginak"));
                if (report.Tasks.Count == 0)
                {
                    column.Item().Text("Ez dago zereginik hautatutako aldian.").Italic().FontColor(Colors.Grey.Darken1);
                }
                else
                {
                    column.Item().Table(table =>
                    {
                        table.ColumnsDefinition(cols =>
                        {
                            cols.RelativeColumn(1); cols.RelativeColumn(0.9f); cols.RelativeColumn(1);
                            cols.RelativeColumn(1); cols.RelativeColumn(1.1f); cols.RelativeColumn(1.3f); cols.RelativeColumn(1.3f);
                        });
                        table.Header(header =>
                        {
                            HeaderCell(header.Cell(), "Zeregina");
                            HeaderCell(header.Cell(), "Soroa");
                            HeaderCell(header.Cell(), "Mota");
                            HeaderCell(header.Cell(), "Laborea");
                            HeaderCell(header.Cell(), "Egoera");
                            HeaderCell(header.Cell(), "Sortua");
                            HeaderCell(header.Cell(), "Amaitua");
                        });
                        int count = 0;
                        foreach (FarmTask task in report.Tasks)
                        {
                            if (count >= 80)
                            {
                                break; // txostenak ez du amaigabea izan behar
                            }
                            count++;
                            Cell(table.Cell(), task.Id);
                            Cell(table.Cell(), task.FieldId.Replace("FIELD-", "") + " soroa");
                            Cell(table.Cell(), Texts.TaskType(task.Type));
                            Cell(table.Cell(), Texts.Crop(task.Crop));
                            Cell(table.Cell(), Texts.TaskStatus(task.Status));
                            Cell(table.Cell(), task.CreatedAt.ToString("dd/MM HH:mm:ss"));
                            Cell(table.Cell(), task.CompletedAt.HasValue ? task.CompletedAt.Value.ToString("dd/MM HH:mm:ss") : "—");
                        }
                    });
                    if (report.Tasks.Count > 80)
                    {
                        column.Item().Text("... eta beste " + (report.Tasks.Count - 80) + " zeregin.").Italic().FontColor(Colors.Grey.Darken1);
                    }
                }

                // ---- 5. Gorabeherak ----
                column.Item().Element(c => SectionTitle(c, "5. Gorabeherak"));
                if (report.Incidents.Count == 0)
                {
                    column.Item().Text("Ez dago gorabeherarik aldian.").Italic().FontColor(Colors.Grey.Darken1);
                }
                else
                {
                    column.Item().Table(table =>
                    {
                        table.ColumnsDefinition(cols =>
                        {
                            cols.ConstantColumn(85); cols.ConstantColumn(70); cols.RelativeColumn();
                        });
                        table.Header(header =>
                        {
                            HeaderCell(header.Cell(), "Data");
                            HeaderCell(header.Cell(), "Mota");
                            HeaderCell(header.Cell(), "Deskribapena");
                        });
                        int count = 0;
                        foreach (LogEntry entry in report.Incidents)
                        {
                            if (count >= 60)
                            {
                                break;
                            }
                            count++;
                            Cell(table.Cell(), entry.Timestamp.ToString("dd/MM HH:mm:ss"));
                            Cell(table.Cell(), entry.Severity.ToString());
                            Cell(table.Cell(), entry.Message);
                        }
                    });
                }
            });
        }

        // ==================================================================
        // ESTILOAK
        // ==================================================================

        private static void SectionTitle(IContainer container, string title)
        {
            container.BorderBottom(1.5f).BorderColor(Green).PaddingBottom(3)
                     .Text(title).FontSize(12).Bold().FontColor(Green);
        }

        private static void Kpi(TableDescriptor table, string label, string value)
        {
            table.Cell().Padding(3).Border(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(6).Column(c =>
            {
                c.Item().Text(label).FontSize(8).FontColor(Colors.Grey.Darken1);
                c.Item().Text(value).FontSize(13).Bold();
            });
        }

        private static void HeaderCell(IContainer cell, string text)
        {
            cell.Background(Green).PaddingVertical(4).PaddingHorizontal(4)
                .Text(text).FontColor(Colors.White).SemiBold().FontSize(8.5f);
        }

        private static void Cell(IContainer cell, string text)
        {
            cell.BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).PaddingVertical(3).PaddingHorizontal(4)
                .Text(text ?? "");
        }
    }
}
