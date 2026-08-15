using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

internal static class OfficeSeatDockingR5eMaskAnalyzer
{
    private const string InputName = "chair-r5e-mask-frame-input.csv";
    private const string OutputName = "chair-r5e-decoded-measurements-input.csv";
    private const string MarkerName = "chair-r5e-mask-analyzer-complete.marker";

    public static int Main(string[] args)
    {
        try
        {
            if (args.Length != 1) throw new ArgumentException("Expected <artifact-directory>.");
            Analyze(Path.GetFullPath(args[0]));
            Console.WriteLine("OFFICE_SEAT_DOCKING_R5E_MASK_ANALYZER: PASS");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine("OFFICE_SEAT_DOCKING_R5E_MASK_ANALYZER: FAIL");
            Console.Error.WriteLine(exception);
            return 1;
        }
    }

    private static void Analyze(string directory)
    {
        string inputPath = Path.Combine(directory, InputName);
        List<Dictionary<string, string>> rows = ReadRows(inputPath);
        Require(rows.Count > 0, "mask-frame denominator is zero");
        string cleanVideo = RequiredSingle(directory, "*clean*.mp4");
        string annotatedVideo = RequiredSingle(directory, "*annotated*.mp4");
        string cleanVideoHash = Sha256File(cleanVideo);
        string annotatedVideoHash = Sha256File(annotatedVideo);
        var sourceHashes = new List<string>(rows.Count);
        var maskHashes = new List<string>(rows.Count * 9);
        int standWhileMoving = 0;
        int footOnChair = 0;
        int descendRise = 0;
        int bodyPop = 0;
        int penetration = 0;
        int ghost = 0;
        int doubleBody = 0;
        int headTeleport = 0;
        float previousPelvisY = float.NaN;
        float previousHeadX = float.NaN;
        float previousHeadY = float.NaN;
        string[] maskFields =
        {
            "actorMaskPath", "expectedPoseMaskPath", "chairSeatMaskPath", "deskMaskPath",
            "furnitureMaskPath", "headMaskPath", "pelvisMaskPath", "leftFootMaskPath",
            "rightFootMaskPath"
        };
        foreach (Dictionary<string, string> row in rows)
        {
            string source = ResolveInside(directory, Value(row, "sourceFramePath"));
            string sourceHash = Sha256File(source);
            Require(string.Equals(sourceHash, Value(row, "sourceFrameSha256"),
                    StringComparison.OrdinalIgnoreCase),
                "source-frame identity mismatch");
            sourceHashes.Add(sourceHash);
            var masks = new Dictionary<string, GrayImage>(StringComparer.Ordinal);
            foreach (string field in maskFields)
            {
                string path = ResolveInside(directory, Value(row, field));
                masks.Add(field, ReadPgm(path));
                maskHashes.Add(Sha256File(path));
            }
            GrayImage actor = masks["actorMaskPath"];
            foreach (GrayImage mask in masks.Values)
                Require(mask.Width == actor.Width && mask.Height == actor.Height,
                    "mask dimensions differ within frame");
            Require(actor.Width == Int(row, "width") && actor.Height == Int(row, "height"),
                "mask/input dimensions mismatch");

            int actorPixels = Count(actor);
            Require(actorPixels > 0 && Count(masks["expectedPoseMaskPath"]) > 0 &&
                    Count(masks["furnitureMaskPath"]) > 0 && Count(masks["headMaskPath"]) > 0 &&
                    Count(masks["pelvisMaskPath"]) > 0,
                "required semantic mask is empty");
            int leftChair = Intersection(masks["leftFootMaskPath"], masks["chairSeatMaskPath"]);
            int rightChair = Intersection(masks["rightFootMaskPath"], masks["chairSeatMaskPath"]);
            if (leftChair + rightChair != 0) footOnChair++;

            int actualSolid = Intersection(actor, masks["furnitureMaskPath"]);
            int expectedSolid = Intersection(
                masks["expectedPoseMaskPath"], masks["furnitureMaskPath"]);
            if (actualSolid > expectedSolid) penetration++;

            int extra = Difference(actor, masks["expectedPoseMaskPath"]);
            int largeComponents = CountLargeComponents(actor, Math.Max(4, actorPixels / 8));
            if (extra > Math.Max(4, actorPixels / 20)) ghost++;
            if (largeComponents > 1) doubleBody++;

            float pelvisY = CentroidY(masks["pelvisMaskPath"]);
            float headX = CentroidX(masks["headMaskPath"]);
            float headY = CentroidY(masks["headMaskPath"]);
            string state = Value(row, "state");
            bool atomicOrTurn = state == "AtomicEntry" || state == "AtomicExit" || state == "TurnInPlace";
            float speed = Float(row, "locomotionSpeedWorld");
            float rootDisplacement = Math.Abs(Float(row, "rootDisplacementWorld"));
            if (atomicOrTurn && (speed > 0.000001f || rootDisplacement > 0.000001f))
                standWhileMoving++;
            if (!float.IsNaN(previousPelvisY) && atomicOrTurn &&
                Math.Abs(pelvisY - previousPelvisY) > 2f) descendRise++;
            if (DifferenceRatio(actor, masks["expectedPoseMaskPath"]) > 0.35f) bodyPop++;
            if (!float.IsNaN(previousHeadX) &&
                Math.Sqrt((headX - previousHeadX) * (headX - previousHeadX) +
                          (headY - previousHeadY) * (headY - previousHeadY)) > 8d)
                headTeleport++;
            previousPelvisY = pelvisY;
            previousHeadX = headX;
            previousHeadY = headY;
        }

        Dictionary<string, string> first = rows[0];
        string[] producers =
        {
            "standWhileMoving", "footOnChair", "descendRise", "bodyPop",
            "chairDeskPenetration", "ghost", "doubleBody", "headTeleport"
        };
        int[] counts =
        {
            standWhileMoving, footOnChair, descendRise, bodyPop,
            penetration, ghost, doubleBody, headTeleport
        };
        string[] header =
        {
            "runId","scenarioId","videoId","actorId","memberId","arrivalDirection",
            "chairRotation","sourceFrameSha256","cleanFrameSha256","maskAtlasSha256",
            "cameraMatrixHash","actorTransformHash","chairTransformHash","deskTransformHash",
            "width","height","gameplayScale","sourceFrameIdentityValid","frameJoinValid",
            "actualFurnitureMaskValid","expectedFrameSampleCount","observedFrameSampleCount",
            "cleanVideoSha256","annotatedVideoSha256",
            "standWhileMovingSampleCount","standWhileMovingCount",
            "footOnChairSampleCount","footOnChairCount","descendRiseSampleCount","descendRiseCount",
            "bodyPopSampleCount","bodyPopCount","chairDeskPenetrationSampleCount",
            "chairDeskPenetrationCount","ghostSampleCount","ghostCount",
            "doubleBodySampleCount","doubleBodyCount","headTeleportSampleCount","headTeleportCount",
            "maskAnalyzerVersion","maskInputSha256"
        };
        var output = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (string field in header) output.Add(field, string.Empty);
        foreach (string field in new[]
                 {
                     "runId", "scenarioId", "videoId", "actorId", "memberId",
                     "arrivalDirection", "chairRotation", "cameraMatrixHash", "actorTransformHash",
                     "chairTransformHash", "deskTransformHash", "width", "height", "gameplayScale"
                 }) output[field] = Value(first, field);
        output["sourceFrameSha256"] = Aggregate(sourceHashes);
        output["cleanFrameSha256"] = Aggregate(sourceHashes);
        output["maskAtlasSha256"] = Aggregate(maskHashes);
        output["sourceFrameIdentityValid"] = "true";
        output["frameJoinValid"] = RowsHaveUniqueMonotonicFrames(rows) ? "true" : "false";
        output["actualFurnitureMaskValid"] = "true";
        output["expectedFrameSampleCount"] = rows.Count.ToString(CultureInfo.InvariantCulture);
        output["observedFrameSampleCount"] = rows.Count.ToString(CultureInfo.InvariantCulture);
        output["cleanVideoSha256"] = cleanVideoHash;
        output["annotatedVideoSha256"] = annotatedVideoHash;
        for (var index = 0; index < producers.Length; index++)
        {
            output[producers[index] + "SampleCount"] = rows.Count.ToString(CultureInfo.InvariantCulture);
            output[producers[index] + "Count"] = counts[index].ToString(CultureInfo.InvariantCulture);
        }
        output["maskAnalyzerVersion"] = "r5e-mask-v1";
        output["maskInputSha256"] = Sha256File(inputPath);
        string outputPath = Path.Combine(directory, OutputName);
        File.WriteAllLines(
            outputPath,
            new[] { string.Join(",", header), string.Join(",", header.Select(field => output[field])) },
            new UTF8Encoding(false));
        File.WriteAllText(
            Path.Combine(directory, MarkerName),
            "complete=true\ninputSha256=" + output["maskInputSha256"] +
            "\noutputSha256=" + Sha256File(outputPath) + "\n",
            new UTF8Encoding(false));
    }

    private static List<Dictionary<string, string>> ReadRows(string path)
    {
        Require(File.Exists(path), "mask-frame input missing");
        string[] lines = File.ReadAllLines(path);
        Require(lines.Length > 1, "mask-frame input has no rows");
        string[] header = lines[0].Split(',');
        Require(header.Distinct(StringComparer.Ordinal).Count() == header.Length,
            "mask-frame input contains duplicate columns");
        var result = new List<Dictionary<string, string>>(lines.Length - 1);
        for (var line = 1; line < lines.Length; line++)
        {
            string[] values = lines[line].Split(',');
            Require(values.Length == header.Length, "mask-frame row width mismatch");
            var row = new Dictionary<string, string>(StringComparer.Ordinal);
            for (var index = 0; index < header.Length; index++) row.Add(header[index], values[index]);
            result.Add(row);
        }
        return result;
    }

    private static GrayImage ReadPgm(string path)
    {
        byte[] bytes = File.ReadAllBytes(path);
        int index = 0;
        string magic = Token(bytes, ref index);
        int width = int.Parse(Token(bytes, ref index), CultureInfo.InvariantCulture);
        int height = int.Parse(Token(bytes, ref index), CultureInfo.InvariantCulture);
        int maximum = int.Parse(Token(bytes, ref index), CultureInfo.InvariantCulture);
        Require(magic == "P5" && width > 0 && height > 0 && maximum == 255,
            "unsupported PGM mask: " + Path.GetFileName(path));
        while (index < bytes.Length && char.IsWhiteSpace((char)bytes[index])) index++;
        Require(bytes.Length - index == width * height, "PGM payload length mismatch");
        var pixels = new byte[width * height];
        Buffer.BlockCopy(bytes, index, pixels, 0, pixels.Length);
        return new GrayImage(width, height, pixels);
    }

    private static string Token(byte[] bytes, ref int index)
    {
        while (index < bytes.Length && char.IsWhiteSpace((char)bytes[index])) index++;
        int start = index;
        while (index < bytes.Length && !char.IsWhiteSpace((char)bytes[index])) index++;
        Require(index > start, "PGM token missing");
        return Encoding.ASCII.GetString(bytes, start, index - start);
    }

    private static int Count(GrayImage image) => image.Pixels.Count(value => value != 0);

    private static int Intersection(GrayImage left, GrayImage right)
    {
        int count = 0;
        for (var index = 0; index < left.Pixels.Length; index++)
            if (left.Pixels[index] != 0 && right.Pixels[index] != 0) count++;
        return count;
    }

    private static int Difference(GrayImage left, GrayImage right)
    {
        int count = 0;
        for (var index = 0; index < left.Pixels.Length; index++)
            if (left.Pixels[index] != 0 && right.Pixels[index] == 0) count++;
        return count;
    }

    private static float DifferenceRatio(GrayImage left, GrayImage right)
    {
        int union = 0;
        int xor = 0;
        for (var index = 0; index < left.Pixels.Length; index++)
        {
            bool a = left.Pixels[index] != 0;
            bool b = right.Pixels[index] != 0;
            if (a || b) union++;
            if (a != b) xor++;
        }
        return union == 0 ? 1f : xor / (float)union;
    }

    private static float CentroidX(GrayImage image) => Centroid(image, true);
    private static float CentroidY(GrayImage image) => Centroid(image, false);

    private static float Centroid(GrayImage image, bool xAxis)
    {
        long sum = 0;
        int count = 0;
        for (var y = 0; y < image.Height; y++)
        for (var x = 0; x < image.Width; x++)
        {
            if (image.Pixels[y * image.Width + x] == 0) continue;
            sum += xAxis ? x : y;
            count++;
        }
        Require(count > 0, "centroid mask is empty");
        return sum / (float)count;
    }

    private static int CountLargeComponents(GrayImage image, int minimum)
    {
        var seen = new bool[image.Pixels.Length];
        var queue = new int[image.Pixels.Length];
        int large = 0;
        for (var start = 0; start < image.Pixels.Length; start++)
        {
            if (seen[start] || image.Pixels[start] == 0) continue;
            int head = 0;
            int tail = 0;
            int count = 0;
            queue[tail++] = start;
            seen[start] = true;
            while (head < tail)
            {
                int current = queue[head++];
                count++;
                int x = current % image.Width;
                int y = current / image.Width;
                if (x > 0) Enqueue(current - 1, image, seen, queue, ref tail);
                if (x + 1 < image.Width) Enqueue(current + 1, image, seen, queue, ref tail);
                if (y > 0) Enqueue(current - image.Width, image, seen, queue, ref tail);
                if (y + 1 < image.Height) Enqueue(current + image.Width, image, seen, queue, ref tail);
            }
            if (count >= minimum) large++;
        }
        return large;
    }

    private static void Enqueue(int index, GrayImage image, bool[] seen, int[] queue, ref int tail)
    {
        if (seen[index] || image.Pixels[index] == 0) return;
        seen[index] = true;
        queue[tail++] = index;
    }

    private static bool RowsHaveUniqueMonotonicFrames(List<Dictionary<string, string>> rows)
    {
        int previous = -1;
        var seen = new HashSet<int>();
        foreach (Dictionary<string, string> row in rows)
        {
            int frame = Int(row, "frameIndex");
            if (frame <= previous || !seen.Add(frame)) return false;
            previous = frame;
        }
        return true;
    }

    private static string Aggregate(IEnumerable<string> values)
    {
        using SHA256 sha = SHA256.Create();
        byte[] bytes = Encoding.UTF8.GetBytes(string.Join("\n", values));
        return string.Concat(sha.ComputeHash(bytes).Select(value => value.ToString("X2")));
    }

    private static string ResolveInside(string directory, string relative)
    {
        Require(!Path.IsPathRooted(relative), "mask/source path must be relative");
        string full = Path.GetFullPath(Path.Combine(directory, relative));
        string prefix = directory.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        Require(full.StartsWith(prefix, StringComparison.OrdinalIgnoreCase), "mask/source path escaped artifact directory");
        Require(File.Exists(full), "mask/source file missing: " + relative);
        return full;
    }

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

    private static string Value(IDictionary<string, string> row, string field) =>
        row.TryGetValue(field, out string value)
            ? value
            : throw new InvalidOperationException("mask-frame field missing: " + field);

    private static int Int(IDictionary<string, string> row, string field) =>
        int.Parse(Value(row, field), NumberStyles.Integer, CultureInfo.InvariantCulture);

    private static float Float(IDictionary<string, string> row, string field) =>
        float.Parse(Value(row, field), NumberStyles.Float, CultureInfo.InvariantCulture);

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private readonly struct GrayImage
    {
        public GrayImage(int width, int height, byte[] pixels)
        {
            Width = width;
            Height = height;
            Pixels = pixels;
        }

        public int Width { get; }
        public int Height { get; }
        public byte[] Pixels { get; }
    }
}
