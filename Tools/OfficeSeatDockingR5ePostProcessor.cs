using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using FamilyCompany.Presentation.Unity.OfficeRuntime;

internal static class OfficeSeatDockingR5ePostProcessor
{
    private const string MeasurementsFile = "chair-r5e-decoded-measurements-input.csv";
    private const string HumanInputFile = "chair-r5e-human-review-input.tsv";
    private const string DecodedFile = "classic-docking-r5e-decoded-frame-oracle.csv";
    private const string HumanFile = "classic-docking-r5e-human-visual-review.csv";

    public static int Main(string[] args)
    {
        try
        {
            if (args.Length != 2) throw new ArgumentException("Expected <artifact-directory> <ffprobe-path>.");
            ProcessPacket(Path.GetFullPath(args[0]), Path.GetFullPath(args[1]));
            Console.WriteLine("OFFICE_SEAT_DOCKING_R5E_POSTPROCESS: PASS");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine("OFFICE_SEAT_DOCKING_R5E_POSTPROCESS: FAIL");
            Console.Error.WriteLine(exception);
            return 1;
        }
    }

    private static void ProcessPacket(string directory, string ffprobe)
    {
        Require(Directory.Exists(directory), "artifact directory missing");
        Require(File.Exists(ffprobe), "ffprobe executable missing");
        string resultPath = Path.Combine(directory, "chair-r5e-runtime-result.txt");
        Require(File.Exists(resultPath), "runtime/static result missing");
        string resultText = File.ReadAllText(resultPath);
        bool fixture = resultText.Contains("fixtureKind=production-static", StringComparison.Ordinal);
        Require(resultText.Contains("status=PENDING_POSTPROCESS", StringComparison.Ordinal),
            "postprocessor input was not fail-closed PENDING");

        string cleanVideo = RequiredSingle(directory, "*clean*.mp4");
        string annotatedVideo = RequiredSingle(directory, "*annotated*.mp4");
        ProbeVideo(ffprobe, cleanVideo, Path.Combine(directory, "clean-ffprobe.json"));
        ProbeVideo(ffprobe, annotatedVideo, Path.Combine(directory, "annotated-ffprobe.json"));

        string measurementPath = Path.Combine(directory, MeasurementsFile);
        Dictionary<string, string> measurement = ReadSingleCsv(measurementPath);
        Require(Value(measurement, "maskAnalyzerVersion") == "r5e-mask-v1",
            "mask analyzer version mismatch");
        string maskInput = Path.Combine(directory, "chair-r5e-mask-frame-input.csv");
        string analyzerMarker = Path.Combine(directory, "chair-r5e-mask-analyzer-complete.marker");
        Require(File.Exists(maskInput) && File.Exists(analyzerMarker),
            "mask analyzer input/completion missing");
        string markerText = File.ReadAllText(analyzerMarker);
        Require(markerText.Contains(
                    "inputSha256=" + Sha256File(maskInput), StringComparison.OrdinalIgnoreCase) &&
                markerText.Contains(
                    "outputSha256=" + Sha256File(measurementPath), StringComparison.OrdinalIgnoreCase) &&
                EqualHash(measurement, "maskInputSha256", Sha256File(maskInput)),
            "mask analyzer identity/output chain mismatch");
        string cleanHash = Sha256File(cleanVideo);
        string annotatedHash = Sha256File(annotatedVideo);
        Require(EqualHash(measurement, "cleanVideoSha256", cleanHash),
            "measurement clean video hash mismatch");
        Require(EqualHash(measurement, "annotatedVideoSha256", annotatedHash),
            "measurement annotated video hash mismatch");
        int expected = PositiveInt(measurement, "expectedFrameSampleCount");
        int observed = PositiveInt(measurement, "observedFrameSampleCount");
        Require(expected == observed, "decoded expected/observed sample mismatch");
        string[] producers =
        {
            "standWhileMoving", "footOnChair", "descendRise", "bodyPop",
            "chairDeskPenetration", "ghost", "doubleBody", "headTeleport"
        };
        foreach (string producer in producers)
        {
            Require(PositiveInt(measurement, producer + "SampleCount") == observed,
                producer + " sample coverage mismatch");
            Require(Int(measurement, producer + "Count") == 0,
                producer + " violation observed");
        }
        Require(Bool(measurement, "sourceFrameIdentityValid") &&
                Bool(measurement, "frameJoinValid") &&
                Bool(measurement, "actualFurnitureMaskValid"),
            "decoded identity/join/furniture mask invalid");
        foreach (string hashField in new[]
                 {
                     "sourceFrameSha256", "cleanFrameSha256", "maskAtlasSha256",
                     "cameraMatrixHash", "actorTransformHash", "chairTransformHash",
                     "deskTransformHash"
                 })
            Require(IsSha(Value(measurement, hashField)), "decoded hash invalid: " + hashField);

        string decodedPath = Path.Combine(directory, DecodedFile);
        WriteDecoded(decodedPath, measurement, producers);
        string decodedHash = Sha256File(decodedPath);
        Dictionary<string, string> human = ReadSingleTsv(Path.Combine(directory, HumanInputFile));
        Require(EqualHash(human, "cleanVideoSha256", cleanHash) &&
                EqualHash(human, "annotatedVideoSha256", annotatedHash),
            "human review video identity mismatch");
        foreach (string field in new[]
                 {
                     "normalScale", "entryReadable", "exitReadable", "noStandWhileMoving",
                     "noFootOnChair", "noDescendRise", "noBodyPop", "noPenetration",
                     "noGhostOrDouble", "noHeadTeleport", "noStrafeOrBackward", "pass"
                 })
            Require(Bool(human, field), "human review did not pass: " + field);
        WriteHuman(
            Path.Combine(directory, HumanFile),
            human,
            cleanHash,
            annotatedHash,
            decodedHash);

        if (fixture)
        {
            File.WriteAllText(
                Path.Combine(directory, "chair-r5e-static-fixture-complete.marker"),
                "complete=true\nfixtureKind=production-static\n",
                new UTF8Encoding(false));
        }
        else
        {
            PromoteVisualMetadataReady(directory, maskInput);
            File.WriteAllText(
                resultPath,
                resultText.Replace("status=PENDING_POSTPROCESS", "status=PASS"),
                new UTF8Encoding(false));
            File.WriteAllText(
                Path.Combine(directory, "chair-r5e-complete.marker"),
                "complete=true\n",
                new UTF8Encoding(false));
        }
        WriteManifest(directory);
    }

    private static void PromoteVisualMetadataReady(string directory, string maskInput)
    {
        string path = Path.Combine(directory, "visual-capture-metadata-r5e.csv");
        Require(File.Exists(path), "visual metadata file missing");
        List<Dictionary<string, string>> masks = ReadCsvRows(maskInput);
        string[] lines = File.ReadAllLines(path);
        Require(lines.Length > 1, "visual metadata denominator is zero");
        string[] header = lines[0].Split(',');
        int clean = Array.IndexOf(header, "cleanFrameObserved");
        int atlas = Array.IndexOf(header, "evidenceAtlasObserved");
        int status = Array.IndexOf(header, "postProcessStatus");
        Require(clean >= 0 && atlas >= 0 && status >= 0, "visual metadata schema incomplete");
        for (var index = 1; index < lines.Length; index++)
        {
            string[] values = lines[index].Split(',');
            Require(values.Length == header.Length, "visual metadata row width mismatch");
            var row = new Dictionary<string, string>(StringComparer.Ordinal);
            for (var column = 0; column < header.Length; column++) row.Add(header[column], values[column]);
            bool joined = masks.Any(mask =>
                Value(mask, "runId") == Value(row, "runId") &&
                Value(mask, "scenarioId") == Value(row, "scenarioId") &&
                Value(mask, "actorId") == Value(row, "actorId") &&
                Value(mask, "renderFrame") == Value(row, "frame"));
            Require(joined, "visual metadata has no decoded mask/frame join");
            values[clean] = "true";
            values[atlas] = "true";
            values[status] = "READY";
            lines[index] = string.Join(",", values.Select(Escape));
        }
        File.WriteAllLines(path, lines, new UTF8Encoding(false));
    }

    private static void WriteDecoded(
        string path,
        Dictionary<string, string> input,
        string[] producers)
    {
        string[] header = OfficeSeatDockingTraceSchemas.DecodedFrameHeader.Split(',');
        var row = header.ToDictionary(value => value, _ => string.Empty, StringComparer.Ordinal);
        Set(row, "schemaVersion", OfficeSeatDockingTraceSchemas.SchemaVersion);
        Copy(row, input,
            "runId", "scenarioId", "videoId", "actorId", "memberId", "arrivalDirection",
            "chairRotation", "sourceFrameSha256", "cleanFrameSha256", "maskAtlasSha256",
            "cameraMatrixHash", "actorTransformHash", "chairTransformHash", "deskTransformHash",
            "width", "height", "gameplayScale", "sourceFrameIdentityValid", "frameJoinValid",
            "actualFurnitureMaskValid", "expectedFrameSampleCount", "observedFrameSampleCount");
        Set(row, "rowKind", "ActorDirectionSummary");
        Set(row, "videoKind", "normal-scale");
        Set(row, "missingFrameSampleCount", "0");
        Set(row, "defaultOnlyMask", "0");
        foreach (string producer in producers)
        {
            Copy(row, input, producer + "Count", producer + "SampleCount");
            Set(row, producer + "ProducerValid", "true");
        }
        File.WriteAllLines(
            path,
            new[] { string.Join(",", header), CsvLine(header, row) },
            new UTF8Encoding(false));
    }

    private static void WriteHuman(
        string path,
        Dictionary<string, string> input,
        string cleanHash,
        string annotatedHash,
        string decodedHash)
    {
        string[] header = OfficeSeatDockingTraceSchemas.HumanReviewHeader.Split(',');
        var row = header.ToDictionary(value => value, _ => string.Empty, StringComparer.Ordinal);
        Set(row, "schemaVersion", OfficeSeatDockingTraceSchemas.SchemaVersion);
        Copy(row, input,
            "runId", "reviewerId", "reviewedAtUtc", "normalScale", "entryReadable",
            "exitReadable", "noStandWhileMoving", "noFootOnChair", "noDescendRise",
            "noBodyPop", "noPenetration", "noGhostOrDouble", "noHeadTeleport",
            "noStrafeOrBackward", "pass", "notes");
        Set(row, "cleanVideoSha256", cleanHash);
        Set(row, "annotatedVideoSha256", annotatedHash);
        Set(row, "decodedOracleSha256", decodedHash);
        File.WriteAllLines(
            path,
            new[] { string.Join(",", header), CsvLine(header, row) },
            new UTF8Encoding(false));
    }

    private static void ProbeVideo(string ffprobe, string video, string output)
    {
        var start = new ProcessStartInfo
        {
            FileName = ffprobe,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        start.ArgumentList.Add("-v");
        start.ArgumentList.Add("error");
        start.ArgumentList.Add("-show_streams");
        start.ArgumentList.Add("-show_format");
        start.ArgumentList.Add("-of");
        start.ArgumentList.Add("json");
        start.ArgumentList.Add(video);
        using Process process = Process.Start(start) ?? throw new InvalidOperationException("ffprobe did not start");
        string stdout = process.StandardOutput.ReadToEnd();
        string stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();
        Require(process.ExitCode == 0 && stdout.Contains("\"streams\"", StringComparison.Ordinal) &&
                stdout.Contains("\"video\"", StringComparison.Ordinal),
            "ffprobe failed: " + Path.GetFileName(video) + ":" + stderr);
        File.WriteAllText(output, stdout, new UTF8Encoding(false));
    }

    private static void WriteManifest(string directory)
    {
        string manifest = Path.Combine(directory, "chair-r5e-runtime-artifact-manifest.tsv");
        string[] files = Directory.GetFiles(directory)
            .Where(path => !string.Equals(path, manifest, StringComparison.OrdinalIgnoreCase) &&
                           !path.EndsWith(".marker", StringComparison.OrdinalIgnoreCase))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
        using var writer = new StreamWriter(manifest, false, new UTF8Encoding(false));
        writer.WriteLine("file\tlength\tsha256");
        foreach (string file in files)
            writer.WriteLine(Path.GetFileName(file) + "\t" + new FileInfo(file).Length + "\t" + Sha256File(file));
    }

    private static Dictionary<string, string> ReadSingleCsv(string path)
    {
        Require(File.Exists(path), "measurement input missing");
        string[] lines = File.ReadAllLines(path);
        Require(lines.Length == 2, "measurement input must contain exactly one row");
        string[] header = lines[0].Split(',');
        string[] values = lines[1].Split(',');
        Require(header.Length == values.Length && header.Distinct(StringComparer.Ordinal).Count() == header.Length,
            "measurement input schema invalid");
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var index = 0; index < header.Length; index++) result.Add(header[index], values[index]);
        return result;
    }

    private static List<Dictionary<string, string>> ReadCsvRows(string path)
    {
        Require(File.Exists(path), "CSV input missing: " + Path.GetFileName(path));
        string[] lines = File.ReadAllLines(path);
        Require(lines.Length > 1, "CSV input denominator is zero: " + Path.GetFileName(path));
        string[] header = lines[0].Split(',');
        Require(header.Distinct(StringComparer.Ordinal).Count() == header.Length,
            "CSV input duplicate columns: " + Path.GetFileName(path));
        var rows = new List<Dictionary<string, string>>(lines.Length - 1);
        for (var line = 1; line < lines.Length; line++)
        {
            string[] values = lines[line].Split(',');
            Require(values.Length == header.Length, "CSV input row width mismatch: " + Path.GetFileName(path));
            var row = new Dictionary<string, string>(StringComparer.Ordinal);
            for (var index = 0; index < header.Length; index++) row.Add(header[index], values[index]);
            rows.Add(row);
        }
        return rows;
    }

    private static Dictionary<string, string> ReadSingleTsv(string path)
    {
        Require(File.Exists(path), "human review input missing");
        string[] lines = File.ReadAllLines(path);
        Require(lines.Length == 2, "human review input must contain exactly one row");
        string[] header = lines[0].Split('\t');
        string[] values = lines[1].Split('\t');
        Require(header.Length == values.Length && header.Distinct(StringComparer.Ordinal).Count() == header.Length,
            "human review input schema invalid");
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var index = 0; index < header.Length; index++) result.Add(header[index], values[index]);
        return result;
    }

    private static void Copy(
        IDictionary<string, string> target,
        IDictionary<string, string> source,
        params string[] fields)
    {
        foreach (string field in fields) Set(target, field, Value(source, field));
    }

    private static string CsvLine(string[] header, IDictionary<string, string> row) =>
        string.Join(",", header.Select(column => Escape(row[column])));

    private static string Escape(string value) =>
        value.IndexOfAny(new[] { ',', '"', '\r', '\n' }) < 0
            ? value
            : "\"" + value.Replace("\"", "\"\"") + "\"";

    private static void Set(IDictionary<string, string> row, string field, string value)
    {
        if (!row.ContainsKey(field)) throw new InvalidOperationException("output field missing: " + field);
        row[field] = value;
    }

    private static string Value(IDictionary<string, string> row, string field) =>
        row.TryGetValue(field, out string value)
            ? value
            : throw new InvalidOperationException("input field missing: " + field);

    private static bool Bool(IDictionary<string, string> row, string field) =>
        bool.TryParse(Value(row, field), out bool value) && value;

    private static int Int(IDictionary<string, string> row, string field) =>
        int.Parse(Value(row, field), NumberStyles.Integer, CultureInfo.InvariantCulture);

    private static int PositiveInt(IDictionary<string, string> row, string field)
    {
        int value = Int(row, field);
        Require(value > 0, field + " denominator is zero");
        return value;
    }

    private static bool EqualHash(IDictionary<string, string> row, string field, string expected) =>
        string.Equals(Value(row, field), expected, StringComparison.OrdinalIgnoreCase);

    private static bool IsSha(string value) =>
        value.Length == 64 && value.All(character =>
            (character >= '0' && character <= '9') ||
            (character >= 'a' && character <= 'f') ||
            (character >= 'A' && character <= 'F'));

    private static string RequiredSingle(string directory, string pattern)
    {
        string[] paths = Directory.GetFiles(directory, pattern);
        Require(paths.Length == 1 && new FileInfo(paths[0]).Length > 1024,
            "expected exactly one nonempty artifact: " + pattern);
        return paths[0];
    }

    private static string Sha256File(string path)
    {
        using SHA256 sha = SHA256.Create();
        using FileStream stream = File.OpenRead(path);
        return string.Concat(sha.ComputeHash(stream).Select(value => value.ToString("X2")));
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
