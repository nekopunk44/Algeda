using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Globalization;
using Web.Models.Analytics;
using Web.Models.Dashboard;
using Web.Models.Properties;

namespace Web.Services;

public class AnalyticsPdfExportService
{
    public byte[] Build(AnalyticsIndexViewModel model)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(30);
                page.DefaultTextStyle(x => x.FontSize(10));

                page.Header().Element(header => RenderHeader(header, model));

                page.Content().PaddingTop(12).Column(col =>
                {
                    switch (model.ActiveTab)
                    {
                        case AnalyticsTabs.Agency:
                            RenderAgency(col, model.Agency);
                            break;
                        case AnalyticsTabs.Properties:
                            RenderProperties(col, model.Properties);
                            break;
                        case AnalyticsTabs.Quality:
                            RenderQuality(col, model.Quality);
                            break;
                        default:
                            RenderRealtor(col, model.Realtors);
                            break;
                    }
                });

                page.Footer()
                    .AlignRight()
                    .Text(text =>
                    {
                        text.Span("Сформировано: ");
                        text.Span(DateTime.Now.ToString("dd.MM.yyyy HH:mm"));
                    });
            });
        }).GeneratePdf();
    }

    private static void RenderHeader(IContainer container, AnalyticsIndexViewModel model)
    {
        container
            .Border(1)
            .BorderColor(Colors.Grey.Lighten2)
            .Padding(10)
            .Column(col =>
            {
                col.Item().Text("Аналитический отчёт Algeda").FontSize(16).SemiBold();
                col.Item().PaddingTop(4).Text($"Раздел: {TabName(model.ActiveTab)}");
                col.Item().Text($"Период: {FormatDate(model.Filters.DateFrom)} — {FormatDate(model.Filters.DateTo)}");
                col.Item().Text($"Тип сделки: {DealTypeLabel(model.Filters.DealType)} | Источник: {SourceLabel(model.Filters.Source)}");
            });
    }

    private static void RenderAgency(ColumnDescriptor col, AgencyAnalyticsViewModel vm)
    {
        col.Item().Text("Сводка по агентству").FontSize(13).SemiBold();
        col.Item().PaddingTop(8).Element(c => RenderKpiGrid(c, new[]
        {
            ("Всего сделок", vm.TotalDeals.ToString()),
            ("Завершено", vm.CompletedDeals.ToString()),
            ("Отменено", vm.CancelledDeals.ToString()),
            ("Конверсия", $"{vm.ConversionRate:F2}%"),
            ("Комиссия", FormatCurrency(vm.TotalCommissionUsd)),
            ("Payouts", FormatCurrency(vm.RealtorPayoutUsd)),
            ("Net income", FormatCurrency(vm.AgencyNetCommissionUsd)),
            ("Средний срок, дни", vm.AverageDealDurationDays.ToString("F2")),
            ("Подтв./част. жалобы", vm.ConfirmedOrPartiallyConfirmedComplaints.ToString())
        }));

        if (vm.MonthlyCompletedDeals.Count == 0)
        {
            col.Item().PaddingTop(10).Text("Недостаточно данных по динамике завершённых сделок.");
            return;
        }

        col.Item().PaddingTop(12).Text("Динамика завершённых сделок").SemiBold();
        col.Item().PaddingTop(6).Element(c =>
        {
            c.Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(2);
                    columns.RelativeColumn(1);
                });

                table.Header(header =>
                {
                    header.Cell().Element(CellHeader).Text("Месяц");
                    header.Cell().Element(CellHeader).AlignRight().Text("Сделок");
                });

                foreach (var item in vm.MonthlyCompletedDeals)
                {
                    table.Cell().Element(CellBody).Text(item.Label);
                    table.Cell().Element(CellBody).AlignRight().Text(item.Value.ToString());
                }
            });
        });
    }

    private static void RenderRealtor(ColumnDescriptor col, RealtorAnalyticsPanelViewModel vm)
    {
        col.Item().Text("Эффективность риелтора").FontSize(13).SemiBold();

        if (vm.LatestScore is null)
        {
            col.Item().PaddingTop(8).Text("Данные по риелтору пока отсутствуют.");
            return;
        }

        col.Item().PaddingTop(8).Element(c => RenderKpiGrid(c, new[]
        {
            ("CTS", vm.LatestScore.ClientTrustScore.ToString("F2")),
            ("APS", vm.LatestScore.AdminPerformanceScore.ToString("F2"))
        }));

        col.Item().PaddingTop(10).Text("Детализация CTS/APS").SemiBold();
        col.Item().PaddingTop(6).Element(c =>
        {
            c.Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(3);
                    columns.RelativeColumn(1);
                });

                table.Header(header =>
                {
                    header.Cell().Element(CellHeader).Text("Компонент");
                    header.Cell().Element(CellHeader).AlignRight().Text("Оценка");
                });

                var rows = new[]
                {
                    ("CTS: сервис клиента", vm.LatestScore.ClientTrustBreakdown.ClientServiceScoreComponent),
                    ("CTS: штраф по жалобам", vm.LatestScore.ClientTrustBreakdown.ComplaintPenaltyComponent),
                    ("APS: качество карточек", vm.LatestScore.AdminPerformanceBreakdown.PropertyDataQualityComponent),
                    ("APS: дисциплина", vm.LatestScore.AdminPerformanceBreakdown.WorkflowDisciplineComponent),
                    ("APS: результативность", vm.LatestScore.AdminPerformanceBreakdown.BusinessResultComponent),
                    ("APS: репутационные риски", vm.LatestScore.AdminPerformanceBreakdown.ReputationRiskComponent)
                };

                foreach (var row in rows)
                {
                    table.Cell().Element(CellBody).Text(row.Item1);
                    table.Cell().Element(CellBody).AlignRight().Text(row.Item2.ToString("F2"));
                }
            });
        });

        if (vm.ScoreHistory.Count == 0)
        {
            return;
        }

        col.Item().PaddingTop(12).Text("История оценок").SemiBold();
        col.Item().PaddingTop(6).Element(c =>
        {
            c.Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(2);
                    columns.RelativeColumn(1);
                    columns.RelativeColumn(1);
                });

                table.Header(header =>
                {
                    header.Cell().Element(CellHeader).Text("Дата");
                    header.Cell().Element(CellHeader).AlignRight().Text("CTS");
                    header.Cell().Element(CellHeader).AlignRight().Text("APS");
                });

                foreach (var row in vm.ScoreHistory.OrderByDescending(x => x.CreatedDate).Take(20))
                {
                    table.Cell().Element(CellBody).Text(row.CreatedDate.ToLocalTime().ToString("dd.MM.yyyy HH:mm"));
                    table.Cell().Element(CellBody).AlignRight().Text(row.ClientTrustScore.ToString("F2"));
                    table.Cell().Element(CellBody).AlignRight().Text(row.AdminPerformanceScore.ToString("F2"));
                }
            });
        });
    }

    private static void RenderProperties(ColumnDescriptor col, PropertyAnalyticsViewModel vm)
    {
        col.Item().Text("Аналитика по объектам").FontSize(13).SemiBold();
        col.Item().PaddingTop(8).Element(c => RenderKpiGrid(c, new[]
        {
            ("Продано объектов", vm.SoldObjectsCount.ToString()),
            ("Средняя цена", FormatCurrency(vm.AverageSoldPriceUsd)),
            ("Медианная цена", FormatCurrency(vm.MedianSoldPriceUsd)),
            ("Скорость продажи, дни", vm.AverageSaleDurationDays.ToString("F2")),
            ("Качество карточки", $"{vm.AverageCardQualityScore:F2} / 5")
        }));

        if (vm.SoldByType.Count == 0)
        {
            col.Item().PaddingTop(10).Text("Данные по типам объектов отсутствуют.");
            return;
        }

        col.Item().PaddingTop(12).Text("Продано по типам").SemiBold();
        col.Item().PaddingTop(6).Element(c =>
        {
            c.Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(2);
                    columns.RelativeColumn(1);
                });

                table.Header(header =>
                {
                    header.Cell().Element(CellHeader).Text("Тип");
                    header.Cell().Element(CellHeader).AlignRight().Text("Количество");
                });

                foreach (var item in vm.SoldByType)
                {
                    table.Cell().Element(CellBody).Text(item.Name);
                    table.Cell().Element(CellBody).AlignRight().Text(item.Count.ToString());
                }
            });
        });
    }

    private static void RenderQuality(ColumnDescriptor col, QualityAnalyticsViewModel vm)
    {
        col.Item().Text("Жалобы и качество").FontSize(13).SemiBold();
        col.Item().PaddingTop(8).Element(c => RenderKpiGrid(c, new[]
        {
            ("Открытые", vm.OpenComplaints.ToString()),
            ("В работе", vm.InProgressComplaints.ToString()),
            ("Решённые", vm.ResolvedComplaints.ToString()),
            ("Подтверждённые", vm.ConfirmedComplaints.ToString()),
            ("Частично подтверждённые", vm.PartiallyConfirmedComplaints.ToString()),
            ("Не подтверждённые", vm.NotConfirmedComplaints.ToString())
        }));

        if (vm.ComplaintsByCategory.Count == 0)
        {
            col.Item().PaddingTop(10).Text("Данные по категориям жалоб отсутствуют.");
            return;
        }

        col.Item().PaddingTop(12).Text("Категории жалоб").SemiBold();
        col.Item().PaddingTop(6).Element(c =>
        {
            c.Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(3);
                    columns.RelativeColumn(1);
                });

                table.Header(header =>
                {
                    header.Cell().Element(CellHeader).Text("Категория");
                    header.Cell().Element(CellHeader).AlignRight().Text("Количество");
                });

                foreach (var item in vm.ComplaintsByCategory)
                {
                    table.Cell().Element(CellBody).Text(DashboardValueDisplay.ComplaintCategory(item.Name));
                    table.Cell().Element(CellBody).AlignRight().Text(item.Count.ToString());
                }
            });
        });
    }

    private static void RenderKpiGrid(IContainer container, IReadOnlyList<(string Label, string Value)> items)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn();
                columns.RelativeColumn();
                columns.RelativeColumn();
            });

            var paddedItems = items.ToList();
            while (paddedItems.Count % 3 != 0)
            {
                paddedItems.Add((string.Empty, string.Empty));
            }

            foreach (var item in paddedItems)
            {
                table.Cell().Padding(4).Element(card =>
                {
                    card.Border(1)
                        .BorderColor(Colors.Grey.Lighten2)
                        .Padding(8);

                    if (string.IsNullOrWhiteSpace(item.Label))
                    {
                        return;
                    }

                    card.Column(col =>
                    {
                        col.Item().Text(item.Label).FontSize(9).FontColor(Colors.Grey.Darken2);
                        col.Item().PaddingTop(2).Text(item.Value).FontSize(12).SemiBold();
                    });
                });
            }
        });
    }

    private static IContainer CellHeader(IContainer container)
    {
        return container
            .BorderBottom(1)
            .BorderColor(Colors.Grey.Lighten2)
            .PaddingVertical(5)
            .PaddingHorizontal(4)
            .DefaultTextStyle(x => x.SemiBold());
    }

    private static IContainer CellBody(IContainer container)
    {
        return container
            .BorderBottom(1)
            .BorderColor(Colors.Grey.Lighten4)
            .PaddingVertical(4)
            .PaddingHorizontal(4);
    }

    private static string TabName(string tab)
    {
        return tab switch
        {
            AnalyticsTabs.Agency => "Агентство",
            AnalyticsTabs.Properties => "Объекты",
            AnalyticsTabs.Quality => "Жалобы и качество",
            _ => "Риелторы"
        };
    }

    private static string DealTypeLabel(string dealType)
    {
        return dealType switch
        {
            "purchase" => "Покупка",
            "sale" => "Продажа",
            _ => "Все"
        };
    }

    private static string SourceLabel(string source)
    {
        if (string.IsNullOrWhiteSpace(source) || string.Equals(source, "all", StringComparison.OrdinalIgnoreCase))
        {
            return "Все источники";
        }

        return source.Trim() switch
        {
            "Sale" => "Продажа",
            "Purchase" => "Покупка",
            "Manual" => "Ручной ввод",
            var other => other
        };
    }

    private static string FormatDate(DateOnly? date)
    {
        return date?.ToString("dd.MM.yyyy") ?? "не задан";
    }

    private static string FormatCurrency(decimal amount)
    {
        var culture = CultureInfo.GetCultureInfo("ru-RU");
        return $"{amount.ToString("N0", culture)} USD";
    }
}
