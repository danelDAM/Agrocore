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
    /// Genera el informe PDF de un agricultor con la librería QuestPDF.
    ///
    /// Los datos (ReportData) los calcula el SERVIDOR y solo incluyen la explotación pedida
    /// (el servidor comprueba que el usuario tenga permiso para verla). Aquí solo se maquetan.
    ///
    /// QuestPDF se usa "describiendo" el documento: página -> cabecera, contenido y pie;
    /// el contenido es una columna con bloques (títulos, tablas...).
    /// </summary>
    public static class PdfReportGenerator
    {
        private static readonly string Green = "#2E7D32";
        private static readonly string LightGreen = "#E8F5E9";

        public static void Create(ReportData report, string path)
        {
            QuestPDF.Settings.License = LicenseType.Community; // licencia gratuita (uso educativo)

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
                        text.Span("AGROCORE · " + report.FarmName + " · Página ");
                        text.CurrentPageNumber();
                        text.Span(" de ");
                        text.TotalPages();
                    });
                });
            });

            document.GeneratePdf(path);
        }

        // ==================================================================
        // CABECERA
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
                        title.Item().Text("INFORME DE OPERACIONES").FontSize(13).SemiBold().FontColor(Colors.Grey.Darken3);
                    });
                    row.ConstantItem(190).AlignRight().Column(info =>
                    {
                        info.Item().AlignRight().Text("Generado: " + report.GeneratedAt.ToString("dd/MM/yyyy HH:mm")).FontSize(8);
                        info.Item().AlignRight().Text("Por: " + report.GeneratedBy).FontSize(8);
                    });
                });

                column.Item().PaddingTop(10).Background(LightGreen).Padding(10).Row(row =>
                {
                    row.RelativeItem().Column(c =>
                    {
                        c.Item().Text(text => { text.Span("Agricultor: ").SemiBold(); text.Span(report.FarmerName); });
                        c.Item().Text(text => { text.Span("Explotación: ").SemiBold(); text.Span(report.FarmName); });
                    });
                    row.RelativeItem().Column(c =>
                    {
                        c.Item().Text(text =>
                        {
                            text.Span("Periodo: ").SemiBold();
                            text.Span(report.From.ToString("dd/MM/yyyy HH:mm") + " - " + report.To.ToString("dd/MM/yyyy HH:mm"));
                        });
                        c.Item().Text(text =>
                        {
                            text.Span("Flota: ").SemiBold();
                            text.Span(report.Machines.Count + " máquinas · " + report.Fields.Count + " campos");
                        });
                    });
                });
            });
        }

        // ==================================================================
        // CONTENIDO
        // ==================================================================

        private static void ComposeContent(IContainer container, ReportData report)
        {
            FarmStatistics s = report.Statistics;

            container.Column(column =>
            {
                column.Spacing(14);

                // ---- 1. Resumen ----
                column.Item().Element(c => SectionTitle(c, "1. Resumen del periodo"));
                column.Item().Table(table =>
                {
                    table.ColumnsDefinition(cols =>
                    {
                        cols.RelativeColumn(); cols.RelativeColumn(); cols.RelativeColumn(); cols.RelativeColumn();
                    });
                    Kpi(table, "Tareas completadas", s.TasksCompleted.ToString());
                    Kpi(table, "Tareas canceladas", s.TasksCancelled.ToString());
                    Kpi(table, "Tareas interrumpidas", s.TasksAborted.ToString());
                    Kpi(table, "Distancia recorrida", s.DistanceKm.ToString("0.00") + " km");
                    Kpi(table, "Campos sembrados", s.FieldsSown.ToString());
                    Kpi(table, "Campos regados", s.FieldsIrrigated.ToString());
                    Kpi(table, "Campos fumigados", s.FieldsSprayed.ToString());
                    Kpi(table, "Campos cosechados", s.FieldsHarvested.ToString());
                    Kpi(table, "Tiempo de actividad", s.TotalRuntimeMinutes.ToString("0") + " min");
                    Kpi(table, "Tiempo esperando", s.WaitingMinutes.ToString("0") + " min");
                    Kpi(table, "Tiempo cargando", s.ChargingMinutes.ToString("0") + " min");
                    Kpi(table, "Batería consumida", s.BatteryConsumed.ToString("0") + " %");
                    Kpi(table, "Conflictos evitados", s.ConflictsAvoided.ToString());
                    Kpi(table, "Interbloqueos evitados", s.DeadlocksAvoided.ToString());
                    Kpi(table, "Cargas de batería", s.ChargeSessions.ToString());
                    Kpi(table, "Paradas de emergencia", s.Emergencies.ToString());
                });

                // ---- 2. Campos ----
                column.Item().Element(c => SectionTitle(c, "2. Estado de los campos"));
                column.Item().Table(table =>
                {
                    table.ColumnsDefinition(cols =>
                    {
                        cols.RelativeColumn(1.2f); cols.RelativeColumn(1.6f); cols.RelativeColumn(1.2f);
                        cols.RelativeColumn(1); cols.RelativeColumn(1.6f); cols.RelativeColumn(1);
                    });
                    table.Header(header =>
                    {
                        HeaderCell(header.Cell(), "Campo");
                        HeaderCell(header.Cell(), "Estado");
                        HeaderCell(header.Cell(), "Cultivo");
                        HeaderCell(header.Cell(), "Crecimiento");
                        HeaderCell(header.Cell(), "Plan");
                        HeaderCell(header.Cell(), "Cosechas");
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

                // ---- 3. Máquinas ----
                column.Item().Element(c => SectionTitle(c, "3. Actividad por máquina"));
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
                        HeaderCell(header.Cell(), "Máquina");
                        HeaderCell(header.Cell(), "Tipo");
                        HeaderCell(header.Cell(), "Distancia");
                        HeaderCell(header.Cell(), "Trabajando");
                        HeaderCell(header.Cell(), "Esperando");
                        HeaderCell(header.Cell(), "Cargando");
                        HeaderCell(header.Cell(), "Tareas");
                        HeaderCell(header.Cell(), "Cargas");
                        HeaderCell(header.Cell(), "Eficiencia");
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

                // ---- 4. Tareas ----
                column.Item().Element(c => SectionTitle(c, "4. Tareas del periodo"));
                if (report.Tasks.Count == 0)
                {
                    column.Item().Text("No hay tareas en el periodo seleccionado.").Italic().FontColor(Colors.Grey.Darken1);
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
                            HeaderCell(header.Cell(), "Tarea");
                            HeaderCell(header.Cell(), "Campo");
                            HeaderCell(header.Cell(), "Tipo");
                            HeaderCell(header.Cell(), "Cultivo");
                            HeaderCell(header.Cell(), "Estado");
                            HeaderCell(header.Cell(), "Creada");
                            HeaderCell(header.Cell(), "Terminada");
                        });
                        int count = 0;
                        foreach (FarmTask task in report.Tasks)
                        {
                            if (count >= 80)
                            {
                                break; // el informe no debe ser interminable
                            }
                            count++;
                            Cell(table.Cell(), task.Id);
                            Cell(table.Cell(), task.FieldId.Replace("FIELD-", "Campo "));
                            Cell(table.Cell(), Texts.TaskType(task.Type));
                            Cell(table.Cell(), Texts.Crop(task.Crop));
                            Cell(table.Cell(), Texts.TaskStatus(task.Status));
                            Cell(table.Cell(), task.CreatedAt.ToString("dd/MM HH:mm:ss"));
                            Cell(table.Cell(), task.CompletedAt.HasValue ? task.CompletedAt.Value.ToString("dd/MM HH:mm:ss") : "—");
                        }
                    });
                    if (report.Tasks.Count > 80)
                    {
                        column.Item().Text("... y " + (report.Tasks.Count - 80) + " tareas más.").Italic().FontColor(Colors.Grey.Darken1);
                    }
                }

                // ---- 5. Incidencias ----
                column.Item().Element(c => SectionTitle(c, "5. Incidencias"));
                if (report.Incidents.Count == 0)
                {
                    column.Item().Text("Sin incidencias en el periodo.").Italic().FontColor(Colors.Grey.Darken1);
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
                            HeaderCell(header.Cell(), "Fecha");
                            HeaderCell(header.Cell(), "Tipo");
                            HeaderCell(header.Cell(), "Descripción");
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
        // ESTILOS
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
