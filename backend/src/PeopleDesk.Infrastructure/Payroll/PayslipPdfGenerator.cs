using PeopleDesk.Application.Common;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace PeopleDesk.Infrastructure.Payroll;

/// <summary>Professional A4 payslip rendering via QuestPDF (community license — see LICENSE note in README).</summary>
public class PayslipPdfGenerator : IPayslipPdfGenerator
{
    static PayslipPdfGenerator() => QuestPDF.Settings.License = LicenseType.Community;

    public byte[] Generate(PayslipModel model)
    {
        var navy = "#0F172A";
        var slate = "#334155";
        var light = "#F1F5F9";
        var accent = "#2563EB";

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(36);
                page.DefaultTextStyle(t => t.FontFamily("Lato").FontSize(10).FontColor(slate));

                page.Header().Column(col =>
                {
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text(model.OrganizationName).FontSize(16).Bold().FontColor(navy);
                            if (!string.IsNullOrWhiteSpace(model.OrganizationAddress))
                                c.Item().Text(model.OrganizationAddress).FontSize(9).FontColor("#64748B");
                        });
                        row.ConstantItem(150).AlignRight().Column(c =>
                        {
                            c.Item().Text("PAYSLIP").FontSize(18).Bold().FontColor(accent);
                            c.Item().Text(model.PeriodName).FontSize(11).FontColor(navy);
                        });
                    });

                    col.Item().PaddingTop(10).LineHorizontal(2).LineColor(navy);
                });

                page.Content().PaddingVertical(14).Column(col =>
                {
                    // Employee block
                    col.Item().Background(light).Padding(12).Grid(grid =>
                    {
                        grid.Columns(2);
                        grid.Item().Text(t =>
                        {
                            t.Span("Employee: ").SemiBold();
                            t.Span(model.EmployeeName);
                        });
                        grid.Item().Text(t =>
                        {
                            t.Span("Employee No: ").SemiBold();
                            t.Span(model.EmployeeNumber);
                        });
                        grid.Item().Text(t =>
                        {
                            t.Span("Department: ").SemiBold();
                            t.Span(model.Department ?? "—");
                        });
                        grid.Item().Text(t =>
                        {
                            t.Span("Position: ").SemiBold();
                            t.Span(model.Position ?? "—");
                        });
                        grid.Item().Text(t =>
                        {
                            t.Span("Payslip No: ").SemiBold();
                            t.Span(model.PayslipNumber);
                        });
                        grid.Item().Text(t =>
                        {
                            t.Span("Bank: ").SemiBold();
                            t.Span(model.BankAccountMasked ?? "—");
                        });
                    });

                    // Earnings
                    col.Item().PaddingTop(14).Text("EARNINGS").FontSize(11).Bold().FontColor(navy);
                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.ConstantColumn(28);
                            c.RelativeColumn();
                            c.ConstantColumn(90);
                        });
                        table.Header(h =>
                        {
                            h.Cell().Text("#").Bold();
                            h.Cell().Text("Description").Bold();
                            h.Cell().AlignRight().Text($"Amount ({model.Currency})").Bold();
                        });
                        var i = 1;
                        foreach (var (label, amount) in model.Earnings)
                        {
                            table.Cell().Text($"{i++}");
                            table.Cell().Text(label);
                            table.Cell().AlignRight().Text(amount.ToString("N2"));
                        }
                        table.Cell().Text("");
                        table.Cell().Text("Gross Pay").Bold();
                        table.Cell().AlignRight().Text(model.GrossPay.ToString("N2")).Bold();
                    });

                    // Deductions
                    col.Item().PaddingTop(14).Text("DEDUCTIONS").FontSize(11).Bold().FontColor(navy);
                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.ConstantColumn(28);
                            c.RelativeColumn();
                            c.ConstantColumn(90);
                        });
                        table.Header(h =>
                        {
                            h.Cell().Text("#").Bold();
                            h.Cell().Text("Description").Bold();
                            h.Cell().AlignRight().Text($"Amount ({model.Currency})").Bold();
                        });
                        var i = 1;
                        foreach (var (label, amount) in model.Deductions)
                        {
                            table.Cell().Text($"{i++}");
                            table.Cell().Text(label);
                            table.Cell().AlignRight().Text(amount.ToString("N2"));
                        }
                        table.Cell().Text("");
                        table.Cell().Text("Total Deductions").Bold();
                        table.Cell().AlignRight().Text(model.TotalDeductions.ToString("N2")).Bold();
                    });

                    // Net pay banner
                    col.Item().PaddingTop(16).Background(navy).Padding(12).Row(row =>
                    {
                        row.RelativeItem().Text("NET PAY").FontSize(12).Bold().FontColor("#FFFFFF");
                        row.ConstantItem(150).AlignRight()
                            .Text($"{model.NetPay:N2} {model.Currency}").FontSize(13).Bold().FontColor("#FFFFFF");
                    });

                    col.Item().PaddingTop(8).Text(t =>
                    {
                        t.Span("This is a system-generated payslip and does not require a signature. ")
                            .FontSize(8).FontColor("#94A3B8");
                        t.Span($"Generated by PeopleDesk for {model.PeriodName}. ").FontSize(8).FontColor("#94A3B8");
                    });
                });

                page.Footer().AlignCenter().Text(t =>
                {
                    t.Span(model.PayslipNumber).FontSize(8).FontColor("#94A3B8");
                    t.Span("  ·  Page ").FontSize(8).FontColor("#94A3B8");
                    t.CurrentPageNumber().FontSize(8).FontColor("#94A3B8");
                });
            });
        });

        return document.GeneratePdf();
    }
}
