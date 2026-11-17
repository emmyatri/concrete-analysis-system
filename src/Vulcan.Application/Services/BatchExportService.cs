using ClosedXML.Excel;
using Vulcan.Application.Interfaces;
using Vulcan.Domain.Entities;

namespace Vulcan.Application.Services;

/// <summary>
/// Exports a batch as one Excel workbook (SCRUM-213, ADR-007 §2.2): a tab each for the batch, its curing
/// readings, its breaks and its predictions. Numbers and dates are real Excel values, not text, and text is
/// never read as a formula. ClosedXML is MIT-licensed, so the export adds no cost for Vulcan.
/// </summary>
public sealed class BatchExportService(
    BatchService batchService,
    IPredictionRepository predictionRepository)
{
    public const string ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    public const string BatchSheet = "Batch";
    public const string CuringReadingsSheet = "Curing readings";
    public const string BreaksSheet = "Breaks";
    public const string PredictionsSheet = "Predictions";

    private const string _dateFormat = "yyyy-mm-dd hh:mm:ss";

    public async Task<byte[]?> ExportAsync(string batchNumber)
    {
        var batch = await batchService.GetByNumberAsync(batchNumber);

        if (batch is null)
        {
            return null;
        }

        var predictions =
            await predictionRepository.GetByBatchIdAsync(batch.Id);

        using var workbook = new XLWorkbook();

        AddSheet(workbook, BatchSheet, _batchHeaders, [BatchRow(batch)]);
        AddSheet(workbook, CuringReadingsSheet, _curingReadingHeaders, CuringReadingRows(batch));
        AddSheet(workbook, BreaksSheet, _breakHeaders, BreakRows(batch));
        AddSheet(workbook, PredictionsSheet, _predictionHeaders, PredictionRows(batch, predictions));

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static readonly string[] _batchHeaders =
    [
        "Batch number",
        "Cast at (UTC)",
        "Final weight (lb)",
        "Expected final break (PSI)",
        "Cement type",
        "Mix water pH",
        "Cement surface pH",
        "Cement (%)",
        "SCM (%)",
        "SCM type",
        "Coarse aggregate (%)",
        "Fine aggregate (%)",
        "Water (%)",
        "Alumina (%)",
        "Iron oxide (%)",
        "Magnesium oxide (%)",
        "Sulfur trioxide (%)",
        "IsSynthetic"
    ];

    private static object?[] BatchRow(Batch batch)
    {
        var composition = batch.Composition;
        var oxides = batch.CementOxideAnalysis;

        return
        [
            batch.BatchNumber,
            batch.CastAt,
            batch.FinalWeightLb,
            batch.ExpectedFinalBreakPsi,
            batch.CementType.ToString(),
            batch.MixWaterPh,
            batch.CementSurfacePh,
            composition?.CementPct,
            composition?.ScmPct,
            composition?.ScmType.ToString(),
            composition?.CoarseAggregatePct,
            composition?.FineAggregatePct,
            composition?.WaterPct,
            oxides?.AluminaPct,
            oxides?.IronOxidePct,
            oxides?.MagnesiumOxidePct,
            oxides?.SulfurTrioxidePct,
            batch.IsSynthetic
        ];
    }

    private static readonly string[] _curingReadingHeaders =
    [
        "Batch number",
        "Specimen label",
        "Taken at (UTC)",
        "Internal temperature (°F)",
        "Ambient temperature (°F)",
        "Moisture (% of sample weight)",
        "Relative humidity (%)",
        "Resistivity (Ω/ft)",
        "CO₂ (ppm)",
        "IsSynthetic"
    ];

    private static IEnumerable<object?[]> CuringReadingRows(Batch batch) =>
        batch.Specimens
            .SelectMany(specimen => specimen.CuringReadings.Select(reading => (specimen, reading)))
            .OrderBy(x => x.reading.TakenAt)
            .ThenBy(x => x.specimen.Label, StringComparer.Ordinal)
            .ThenBy(x => x.reading.Id)
            .Select(x => new object?[]
            {
                batch.BatchNumber,
                x.specimen.Label,
                x.reading.TakenAt,
                x.reading.InternalTempF,
                x.reading.AmbientTempF,
                x.reading.MoisturePct,
                x.reading.RelativeHumidityPct,
                x.reading.ResistivityOhmPerFt,
                x.reading.Co2Ppm,
                batch.IsSynthetic || x.specimen.IsSynthetic
            });

    private static readonly string[] _breakHeaders =
    [
        "Batch number",
        "Specimen label",
        "Test age (days)",
        "Broken at (UTC)",
        "Compressive strength (PSI)",
        "IsSynthetic"
    ];

    private static IEnumerable<object?[]> BreakRows(Batch batch) =>
        batch.Specimens
            .Where(s => s.BreakResult is not null)
            .OrderBy(s => (int)s.TestAgeDays)
            .ThenBy(s => s.Label, StringComparer.Ordinal)
            .Select(s => new object?[]
            {
                batch.BatchNumber,
                s.Label,
                (int)s.TestAgeDays,
                s.BreakResult!.BrokenAt,
                s.BreakResult!.StrengthPsi,
                batch.IsSynthetic || s.IsSynthetic
            });

    private static readonly string[] _predictionHeaders =
    [
        "Batch number",
        "Calibration version",
        "Basis",
        "Horizon (days)",
        "Estimated strength (PSI)",
        "Equivalent age (days)",
        "Adjustment stage",
        "Unadjusted strength (PSI)",
        "Created at (UTC)",
        "IsSynthetic"
    ];

    // Stored values at full precision: rounding is for the screen, and engineers may recalculate from these.
    private static IEnumerable<object?[]> PredictionRows(
        Batch batch,
        IReadOnlyList<PredictionEntity> predictions) =>
        predictions
            .OrderBy(p => p.HorizonDays)
            .ThenBy(p => p.CalibrationVersion, StringComparer.Ordinal)
            .ThenBy(p => p.Id)
            .Select(p => new object?[]
            {
                batch.BatchNumber,
                p.CalibrationVersion,
                p.Basis,
                p.HorizonDays,
                p.EstimatedPsi,
                p.EquivalentAgeDays,
                p.AdjustmentStage?.ToString(),
                p.UnadjustedPsi,
                p.CreatedAtUtc,
                p.IsSynthetic
            });

    private static void AddSheet(
        XLWorkbook workbook,
        string name,
        string[] headers,
        IEnumerable<object?[]> rows)
    {
        var sheet = workbook.Worksheets.Add(name);

        // Rows can be tall when Basis wraps; keep every value at the top of its row.
        sheet.Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;

        for (var column = 0; column < headers.Length; column++)
        {
            sheet.Cell(1, column + 1).Value = headers[column];

            if (headers[column] == "Basis")
            {
                // The estimate's explanation is a paragraph: wrap it in a fixed width.
                sheet.Column(column + 1).Width = 60;
                sheet.Column(column + 1).Style.Alignment.WrapText = true;
            }
            else
            {
                // Dates show as yyyy-mm-dd hh:mm:ss, wider than their headers.
                sheet.Column(column + 1).Width = headers[column].EndsWith("(UTC)", StringComparison.Ordinal)
                    ? 21
                    : Math.Max(14, headers[column].Length + 4);
            }
        }

        var header = sheet.Range(1, 1, 1, headers.Length);
        header.Style.Font.Bold = true;
        header.Style.Fill.BackgroundColor = XLColor.FromHtml("#E7E2D7");
        sheet.SheetView.FreezeRows(1);

        var basisColumn = Array.IndexOf(headers, "Basis");

        var rowNumber = 2;
        foreach (var row in rows)
        {
            for (var column = 0; column < row.Length; column++)
            {
                var cell = sheet.Cell(rowNumber, column + 1);
                cell.Value = ToCellValue(row[column]);

                if (row[column] is DateTime)
                {
                    cell.Style.DateFormat.Format = _dateFormat;
                }
                else if (row[column] is decimal or int && DisplayFormat(headers[column]) is { } format)
                {
                    // Shown rounded like the app; the cell keeps the full stored value.
                    cell.Style.NumberFormat.Format = format;
                }
            }

            if (basisColumn >= 0 && row[basisColumn] is string basis)
            {
                // Excel doesn't resize rows for wrapped text it didn't type, so size them here:
                // about 55 characters per line at width 60, 15 points per line.
                sheet.Row(rowNumber).Height = 15 * Math.Max(1, Math.Ceiling(basis.Length / 55.0));
            }

            rowNumber++;
        }

        if (rowNumber > 2)
        {
            sheet.Range(1, 1, rowNumber - 1, headers.Length).SetAutoFilter();
        }
    }

    /// <summary>How a stored decimal is shown: PSI as whole numbers, equivalent age to 2 places.</summary>
    private static string? DisplayFormat(string header) =>
        header.Contains("(PSI)", StringComparison.Ordinal) ? "#,##0"
        : header == "Equivalent age (days)" ? "0.00"
        : null;

    /// <summary>
    /// A string is always stored as text, so a value like "=SUM(1+1)" is never run as a formula.
    /// </summary>
    private static XLCellValue ToCellValue(object? value) => value switch
    {
        null => Blank.Value,
        string text => text,
        bool flag => flag,
        int number => (double)number,
        decimal number => (double)number,
        DateTime date => AsUtc(date),
        _ => value.ToString() ?? string.Empty
    };

    // CBAS saves UTC; SQLite can return it with an unspecified Kind. Keep the clock value as UTC.
    private static DateTime AsUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };
}
