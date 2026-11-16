
using System.Globalization;
using System.IO.Compression;
using System.Text;
using Vulcan.Application.Interfaces;
using Vulcan.Domain.Entities;

namespace Vulcan.Application.Services;

public sealed class BatchExportService(
    BatchService batchService,
    IPredictionRepository predictionRepository)
{
    public async Task<byte[]?> ExportAsync(string batchNumber)
    {
        var batch = await batchService.GetByNumberAsync(batchNumber);

        if (batch is null)
        {
            return null;
        }

        var predictions =
            await predictionRepository.GetByBatchIdAsync(batch.Id);

        using var stream = new MemoryStream();

        using (var archive = new ZipArchive(
                   stream,
                   ZipArchiveMode.Create,
                   leaveOpen: true))
        {
            WriteEntry(archive, "batch.csv", BuildBatchCsv(batch));

            WriteEntry(
                archive,
                "curing-readings.csv",
                BuildCuringReadingsCsv(batch));

            WriteEntry(archive, "breaks.csv", BuildBreaksCsv(batch));

            WriteEntry(
                archive,
                "predictions.csv",
                BuildPredictionsCsv(batch, predictions));
        }

        return stream.ToArray();
    }

    private static void WriteEntry(
        ZipArchive archive,
        string filename,
        string content)
    {
        var entry = archive.CreateEntry(
            filename,
            CompressionLevel.Optimal);

        using var writer = new StreamWriter(
            entry.Open(),
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

        writer.Write(content);
    }

    private static string BuildBatchCsv(Batch batch)
    {
        var csv = new StringBuilder();

        AppendRow(csv,
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
            "IsSynthetic");

        var composition = batch.Composition;
        var oxides = batch.CementOxideAnalysis;

        AppendRow(csv,
            batch.BatchNumber,
            FormatDate(batch.CastAt),
            Format(batch.FinalWeightLb),
            Format(batch.ExpectedFinalBreakPsi),
            batch.CementType.ToString(),
            Format(batch.MixWaterPh),
            Format(batch.CementSurfacePh),
            Format(composition?.CementPct),
            Format(composition?.ScmPct),
            composition?.ScmType.ToString() ?? "",
            Format(composition?.CoarseAggregatePct),
            Format(composition?.FineAggregatePct),
            Format(composition?.WaterPct),
            Format(oxides?.AluminaPct),
            Format(oxides?.IronOxidePct),
            Format(oxides?.MagnesiumOxidePct),
            Format(oxides?.SulfurTrioxidePct),
            Format(batch.IsSynthetic));

        return csv.ToString();
    }

    private static string BuildCuringReadingsCsv(Batch batch)
    {
        var csv = new StringBuilder();

        AppendRow(csv,
            "Batch number",
            "Specimen label",
            "Taken at (UTC)",
            "Internal temperature (°F)",
            "Ambient temperature (°F)",
            "Moisture (% of sample weight)",
            "Relative humidity (%)",
            "Resistivity (Ω/ft)",
            "CO₂ (ppm)",
            "IsSynthetic");

        var readings = batch.Specimens
            .SelectMany(specimen =>
                specimen.CuringReadings.Select(reading =>
                    new { specimen, reading }))
            .OrderBy(x => x.reading.TakenAt)
            .ThenBy(x => x.specimen.Label, StringComparer.Ordinal)
            .ThenBy(x => x.reading.Id);

        foreach (var item in readings)
        {
            var reading = item.reading;

            AppendRow(csv,
                batch.BatchNumber,
                item.specimen.Label,
                FormatDate(reading.TakenAt),
                Format(reading.InternalTempF),
                Format(reading.AmbientTempF),
                Format(reading.MoisturePct),
                Format(reading.RelativeHumidityPct),
                Format(reading.ResistivityOhmPerFt),
                Format(reading.Co2Ppm),
                Format(batch.IsSynthetic || item.specimen.IsSynthetic));
        }

        return csv.ToString();
    }

    private static string BuildBreaksCsv(Batch batch)
    {
        var csv = new StringBuilder();

        AppendRow(csv,
            "Batch number",
            "Specimen label",
            "Test age (days)",
            "Broken at (UTC)",
            "Compressive strength (PSI)",
            "IsSynthetic");

        var specimens = batch.Specimens
            .Where(s => s.BreakResult is not null)
            .OrderBy(s => (int)s.TestAgeDays)
            .ThenBy(s => s.Label, StringComparer.Ordinal);

        foreach (var specimen in specimens)
        {
            var result = specimen.BreakResult!;

            AppendRow(csv,
                batch.BatchNumber,
                specimen.Label,
                Format(Convert.ToInt32(specimen.TestAgeDays)),
                FormatDate(result.BrokenAt),
                Format(result.StrengthPsi),
                Format(batch.IsSynthetic || specimen.IsSynthetic));
        }

        return csv.ToString();
    }

    private static string BuildPredictionsCsv(
        Batch batch,
        IReadOnlyList<PredictionEntity> predictions)
    {
        var csv = new StringBuilder();

        AppendRow(csv,
            "Batch number",
            "Calibration version",
            "Basis",
            "Horizon (days)",
            "Estimated strength (PSI)",
            "Equivalent age (days)",
            "Adjustment stage",
            "Unadjusted strength (PSI)",
            "IsSynthetic");

        foreach (var prediction in predictions
                     .OrderBy(p => p.HorizonDays)
                     .ThenBy(p => p.CalibrationVersion, StringComparer.Ordinal)
                     .ThenBy(p => p.Id))
        {
            AppendRow(csv,
                batch.BatchNumber,
                prediction.CalibrationVersion,
                prediction.Basis,
                Format(prediction.HorizonDays),
                Format(prediction.EstimatedPsi),
                Format(prediction.EquivalentAgeDays),
                prediction.AdjustmentStage?.ToString() ?? "",
                Format(prediction.UnadjustedPsi),
                Format(prediction.IsSynthetic));
        }

        return csv.ToString();
    }

    private static void AppendRow(
        StringBuilder csv,
        params string[] values)
    {
        csv.AppendLine(string.Join(",", values.Select(EscapeCsv)));
    }

    private static string EscapeCsv(string value)
    {
        // Prevent Excel from interpreting exported text as a formula.
        if (value.Length > 0 &&
            (value[0] is '=' or '+' or '-' or '@' ||
             (char.IsWhiteSpace(value[0]) &&
              value.TrimStart().Length > 0 &&
              value.TrimStart()[0] is '=' or '+' or '-' or '@')))
        {
            value = "'" + value;
        }

        if (value.Contains(',') ||
            value.Contains('"') ||
            value.Contains('\n') ||
            value.Contains('\r'))
        {
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        return value;
    }

    private static string Format(decimal value) =>
        value.ToString(CultureInfo.InvariantCulture);

    private static string Format(decimal? value) =>
        value?.ToString(CultureInfo.InvariantCulture) ?? "";

    private static string Format(int value) =>
        value.ToString(CultureInfo.InvariantCulture);

    private static string Format(bool value) =>
        value ? "true" : "false";


    private static string FormatDate(DateTime value)
    {
        // CBAS persists timestamps in UTC.
        // SQLite may return them with DateTimeKind.Unspecified.
        // Preserve their UTC clock values rather than treating
        // them as local timestamps.
        var utc = value.Kind switch
        {
            DateTimeKind.Utc => value,

            DateTimeKind.Unspecified =>
                DateTime.SpecifyKind(value, DateTimeKind.Utc),

            DateTimeKind.Local => value.ToUniversalTime(),

            _ => throw new ArgumentOutOfRangeException(nameof(value))
        };

        return utc.ToString("O", CultureInfo.InvariantCulture);
    }
}
