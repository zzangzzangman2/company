param(
    [string]$GeneratedRoot = 'C:\Users\godho\.codex\generated_images\019ffdb9-2633-73f0-9d6d-76d427801eaf',
    [string]$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

if (-not ('MainNavigationV2AssetPrep' -as [type])) {
    $drawingAssembly = [System.Drawing.Bitmap].Assembly.Location
    $drawingPrimitivesAssembly = [System.Drawing.Rectangle].Assembly.Location
    $gdiAssembly = Join-Path $PSHOME 'System.Private.Windows.GdiPlus.dll'
    $windowsCoreAssembly = Join-Path $PSHOME 'System.Private.Windows.Core.dll'
    $coreAssembly = [object].Assembly.Location
    $supportAssemblies = @(
        (Join-Path $PSHOME 'System.Collections.dll'),
        (Join-Path $PSHOME 'System.Runtime.dll'),
        (Join-Path $PSHOME 'System.Runtime.InteropServices.dll'),
        (Join-Path $PSHOME 'System.IO.FileSystem.dll')
    )
    Add-Type -ReferencedAssemblies (@($coreAssembly, $drawingAssembly, $drawingPrimitivesAssembly, $gdiAssembly, $windowsCoreAssembly) + $supportAssemblies) -TypeDefinition @'
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

public static class MainNavigationV2AssetPrep
{
    public static void Prepare(string sourcePath, string destinationPath, int targetWidth, int targetHeight)
    {
        using (var source = new Bitmap(sourcePath))
        using (var argb = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppArgb))
        {
            argb.SetResolution(96f, 96f);
            using (var graphics = Graphics.FromImage(argb))
            {
                graphics.CompositingMode = CompositingMode.SourceCopy;
                graphics.DrawImageUnscaled(source, 0, 0);
            }

            var pixels = ReadPixels(argb);
            if (!HasUsableTransparency(pixels)) RemoveConnectedNeutralBackground(pixels);
            ClearInvisibleRgb(pixels);
            WritePixels(argb, pixels);

            var bounds = FindVisibleBounds(pixels, argb.Width, argb.Height);
            if (bounds.Width <= 0 || bounds.Height <= 0)
                throw new InvalidOperationException("No visible asset pixels after alpha preparation: " + sourcePath);

            var sourcePadding = Math.Max(8, Math.Min(argb.Width, argb.Height) / 80);
            bounds.Inflate(sourcePadding, sourcePadding);
            bounds.Intersect(new Rectangle(0, 0, argb.Width, argb.Height));

            using (var output = new Bitmap(targetWidth, targetHeight, PixelFormat.Format32bppArgb))
            {
                output.SetResolution(96f, 96f);
                using (var graphics = Graphics.FromImage(output))
                {
                    graphics.Clear(Color.Transparent);
                    graphics.CompositingMode = CompositingMode.SourceCopy;
                    graphics.CompositingQuality = CompositingQuality.HighQuality;
                    graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    graphics.SmoothingMode = SmoothingMode.HighQuality;

                    const int outputPadding = 8;
                    var availableWidth = targetWidth - outputPadding * 2;
                    var availableHeight = targetHeight - outputPadding * 2;
                    var scale = Math.Min(availableWidth / (double)bounds.Width, availableHeight / (double)bounds.Height);
                    var drawWidth = Math.Max(1, (int)Math.Round(bounds.Width * scale));
                    var drawHeight = Math.Max(1, (int)Math.Round(bounds.Height * scale));
                    var destination = new Rectangle(
                        (targetWidth - drawWidth) / 2,
                        (targetHeight - drawHeight) / 2,
                        drawWidth,
                        drawHeight);
                    graphics.DrawImage(argb, destination, bounds, GraphicsUnit.Pixel);
                }

                Directory.CreateDirectory(Path.GetDirectoryName(destinationPath));
                output.Save(destinationPath, ImageFormat.Png);
            }
        }
    }

    private static byte[] ReadPixels(Bitmap bitmap)
    {
        var rect = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
        var data = bitmap.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            var bytes = new byte[Math.Abs(data.Stride) * bitmap.Height];
            Marshal.Copy(data.Scan0, bytes, 0, bytes.Length);
            if (data.Stride == bitmap.Width * 4) return bytes;

            var packed = new byte[bitmap.Width * bitmap.Height * 4];
            for (var y = 0; y < bitmap.Height; y++)
                Buffer.BlockCopy(bytes, y * data.Stride, packed, y * bitmap.Width * 4, bitmap.Width * 4);
            return packed;
        }
        finally
        {
            bitmap.UnlockBits(data);
        }
    }

    private static void WritePixels(Bitmap bitmap, byte[] pixels)
    {
        var rect = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
        var data = bitmap.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
        try
        {
            if (data.Stride == bitmap.Width * 4)
            {
                Marshal.Copy(pixels, 0, data.Scan0, pixels.Length);
                return;
            }

            for (var y = 0; y < bitmap.Height; y++)
                Marshal.Copy(pixels, y * bitmap.Width * 4, data.Scan0 + y * data.Stride, bitmap.Width * 4);
        }
        finally
        {
            bitmap.UnlockBits(data);
        }
    }

    private static bool HasUsableTransparency(byte[] pixels)
    {
        for (var index = 3; index < pixels.Length; index += 4)
            if (pixels[index] < 240) return true;
        return false;
    }

    private static void RemoveConnectedNeutralBackground(byte[] pixels)
    {
        var pixelCount = pixels.Length / 4;
        var width = InferWidth(pixels);
        throw new InvalidOperationException("Width-aware overload required.");
    }

    private static int InferWidth(byte[] pixels) { return 0; }

    private static void RemoveConnectedNeutralBackground(byte[] pixels, int width, int height)
    {
        var background = new bool[width * height];
        var queue = new int[width * height];
        var head = 0;
        var tail = 0;

        for (var x = 0; x < width; x++)
        {
            TrySeed(pixels, background, queue, ref tail, x);
            TrySeed(pixels, background, queue, ref tail, (height - 1) * width + x);
        }
        for (var y = 0; y < height; y++)
        {
            TrySeed(pixels, background, queue, ref tail, y * width);
            TrySeed(pixels, background, queue, ref tail, y * width + width - 1);
        }

        while (head < tail)
        {
            var index = queue[head++];
            var x = index % width;
            var y = index / width;
            if (x > 0) TrySeed(pixels, background, queue, ref tail, index - 1);
            if (x + 1 < width) TrySeed(pixels, background, queue, ref tail, index + 1);
            if (y > 0) TrySeed(pixels, background, queue, ref tail, index - width);
            if (y + 1 < height) TrySeed(pixels, background, queue, ref tail, index + width);
        }

        for (var index = 0; index < background.Length; index++)
            if (background[index]) pixels[index * 4 + 3] = 0;
    }

    private static void TrySeed(byte[] pixels, bool[] background, int[] queue, ref int tail, int index)
    {
        if (background[index] || !IsNeutralBackgroundCandidate(pixels, index)) return;
        background[index] = true;
        queue[tail++] = index;
    }

    private static bool IsNeutralBackgroundCandidate(byte[] pixels, int pixelIndex)
    {
        var offset = pixelIndex * 4;
        var blue = pixels[offset];
        var green = pixels[offset + 1];
        var red = pixels[offset + 2];
        var min = Math.Min(red, Math.Min(green, blue));
        var max = Math.Max(red, Math.Max(green, blue));
        return min >= 205 && max - min <= 30;
    }

    private static void ClearInvisibleRgb(byte[] pixels)
    {
        for (var offset = 0; offset < pixels.Length; offset += 4)
        {
            if (pixels[offset + 3] > 8) continue;
            pixels[offset] = 0;
            pixels[offset + 1] = 0;
            pixels[offset + 2] = 0;
            pixels[offset + 3] = 0;
        }
    }

    private static Rectangle FindVisibleBounds(byte[] pixels, int width, int height)
    {
        var left = width;
        var top = height;
        var right = -1;
        var bottom = -1;
        for (var y = 0; y < height; y++)
        for (var x = 0; x < width; x++)
        {
            if (pixels[(y * width + x) * 4 + 3] <= 8) continue;
            left = Math.Min(left, x);
            top = Math.Min(top, y);
            right = Math.Max(right, x);
            bottom = Math.Max(bottom, y);
        }
        return right < left ? Rectangle.Empty : Rectangle.FromLTRB(left, top, right + 1, bottom + 1);
    }

    public static void PrepareWithDimensions(string sourcePath, string destinationPath, int targetWidth, int targetHeight)
    {
        using (var source = new Bitmap(sourcePath))
        using (var argb = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppArgb))
        {
            using (var graphics = Graphics.FromImage(argb))
            {
                graphics.CompositingMode = CompositingMode.SourceCopy;
                graphics.DrawImageUnscaled(source, 0, 0);
            }
            var pixels = ReadPixels(argb);
            if (!HasUsableTransparency(pixels)) RemoveConnectedNeutralBackground(pixels, argb.Width, argb.Height);
            ClearInvisibleRgb(pixels);
            WritePixels(argb, pixels);

            var bounds = FindVisibleBounds(pixels, argb.Width, argb.Height);
            if (bounds.Width <= 0 || bounds.Height <= 0)
                throw new InvalidOperationException("No visible asset pixels after alpha preparation: " + sourcePath);
            var sourcePadding = Math.Max(8, Math.Min(argb.Width, argb.Height) / 80);
            bounds.Inflate(sourcePadding, sourcePadding);
            bounds.Intersect(new Rectangle(0, 0, argb.Width, argb.Height));

            using (var output = new Bitmap(targetWidth, targetHeight, PixelFormat.Format32bppArgb))
            {
                using (var graphics = Graphics.FromImage(output))
                {
                    graphics.Clear(Color.Transparent);
                    graphics.CompositingMode = CompositingMode.SourceCopy;
                    graphics.CompositingQuality = CompositingQuality.HighQuality;
                    graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    graphics.SmoothingMode = SmoothingMode.HighQuality;
                    const int outputPadding = 8;
                    var scale = Math.Min(
                        (targetWidth - outputPadding * 2) / (double)bounds.Width,
                        (targetHeight - outputPadding * 2) / (double)bounds.Height);
                    var drawWidth = Math.Max(1, (int)Math.Round(bounds.Width * scale));
                    var drawHeight = Math.Max(1, (int)Math.Round(bounds.Height * scale));
                    var destination = new Rectangle((targetWidth - drawWidth) / 2, (targetHeight - drawHeight) / 2, drawWidth, drawHeight);
                    graphics.DrawImage(argb, destination, bounds, GraphicsUnit.Pixel);
                }
                var outputPixels = ReadPixels(output);
                ClearInvisibleRgb(outputPixels);
                WritePixels(output, outputPixels);
                Directory.CreateDirectory(Path.GetDirectoryName(destinationPath));
                output.Save(destinationPath, ImageFormat.Png);
            }
        }
    }

    public static string Audit(string path)
    {
        using (var bitmap = new Bitmap(path))
        {
            var pixels = ReadPixels(bitmap);
            var minAlpha = 255;
            var maxAlpha = 0;
            var transparent = 0;
            var halo = 0;
            for (var offset = 0; offset < pixels.Length; offset += 4)
            {
                var alpha = pixels[offset + 3];
                minAlpha = Math.Min(minAlpha, alpha);
                maxAlpha = Math.Max(maxAlpha, alpha);
                if (alpha <= 8)
                {
                    transparent++;
                    if (pixels[offset] > 16 || pixels[offset + 1] > 16 || pixels[offset + 2] > 16) halo++;
                }
            }
            var corners = new[]
            {
                pixels[3],
                pixels[(bitmap.Width - 1) * 4 + 3],
                pixels[((bitmap.Height - 1) * bitmap.Width) * 4 + 3],
                pixels[(bitmap.Height * bitmap.Width - 1) * 4 + 3]
            };
            var maxCorner = Math.Max(Math.Max(corners[0], corners[1]), Math.Max(corners[2], corners[3]));
            if (minAlpha > 8 || maxAlpha < 240 || maxCorner > 8 || transparent == 0 || halo > 1024)
                throw new InvalidOperationException(string.Format(
                    "Alpha audit failed for {0}: alpha={1}..{2}, corner={3}, transparent={4}, halo={5}",
                    path, minAlpha, maxAlpha, maxCorner, transparent, halo));
            return string.Format("{0}x{1} alpha={2}..{3} corner<={4} transparent={5} halo={6}",
                bitmap.Width, bitmap.Height, minAlpha, maxAlpha, maxCorner, transparent, halo);
        }
    }

    public static void TrimTransparentPadding(string path, int padding)
    {
        var temporaryPath = path + ".trim.png";
        using (var source = new Bitmap(path))
        {
            var pixels = ReadPixels(source);
            var bounds = FindVisibleBounds(pixels, source.Width, source.Height);
            if (bounds.Width <= 0 || bounds.Height <= 0)
                throw new InvalidOperationException("No visible pixels to trim: " + path);

            bounds.Inflate(padding, padding);
            bounds.Intersect(new Rectangle(0, 0, source.Width, source.Height));
            using (var output = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format32bppArgb))
            {
                using (var graphics = Graphics.FromImage(output))
                {
                    graphics.Clear(Color.Transparent);
                    graphics.CompositingMode = CompositingMode.SourceCopy;
                    graphics.DrawImage(source, new Rectangle(0, 0, bounds.Width, bounds.Height), bounds, GraphicsUnit.Pixel);
                }
                var outputPixels = ReadPixels(output);
                ClearInvisibleRgb(outputPixels);
                WritePixels(output, outputPixels);
                output.Save(temporaryPath, ImageFormat.Png);
            }
        }
        File.Delete(path);
        File.Move(temporaryPath, path);
    }
}
'@
}

$assetRoot = Join-Path $ProjectRoot 'Assets\Art\UI\Resources\MainNavigationV2'
$assets = @(
    @{ Source='exec-f66c5671-ea0a-4c8d-a2a6-197636d2c081.png'; Target='Frames\top_hud_backplate_v2.png'; W=2048; H=160 },
    @{ Source='exec-e1109f6a-ea0f-4ed9-ade4-d490ee3f82d9.png'; Target='Frames\company_badge_v2.png'; W=1024; H=256 },
    @{ Source='exec-32435064-3fe0-4e58-aefe-6bc2d4d72e7d.png'; Target='Frames\time_badge_v2.png'; W=1024; H=256 },
    @{ Source='exec-7ffbe945-6924-4262-8769-28b6388927e4.png'; Target='Frames\speed_normal_v2.png'; W=384; H=192 },
    @{ Source='exec-ed00e072-8169-406e-aa9b-71dec96d6bfa.png'; Target='Frames\speed_hover_v2.png'; W=384; H=192 },
    @{ Source='exec-70a8f00f-275a-4fb6-a032-880255e28d92.png'; Target='Frames\speed_selected_v2.png'; W=384; H=192 },
    @{ Source='exec-575fada8-5787-4393-8b2d-bdf7d500abfb.png'; Target='Frames\speed_pressed_v2.png'; W=384; H=192 },
    @{ Source='exec-a4421d1e-b051-437e-8f36-c7ec0a1c027a.png'; Target='Frames\bottom_dock_v2.png'; W=2048; H=256 },
    @{ Source='exec-3b5510e9-07c7-4236-a796-df020de99c1f.png'; Target='Frames\tab_normal_v2.png'; W=512; H=256 },
    @{ Source='exec-0fcb2743-2676-4521-893c-88bc1a168480.png'; Target='Frames\tab_hover_v2.png'; W=512; H=256 },
    @{ Source='exec-93e5a917-1a2c-4aa3-82a3-3c8c006357f7.png'; Target='Frames\tab_selected_v2.png'; W=512; H=256 },
    @{ Source='exec-cfe9b455-d291-4a7f-af7a-4c7246182571.png'; Target='Frames\tab_pressed_v2.png'; W=512; H=256 },
    @{ Source='exec-5756e741-3981-4f91-b876-b8ca3aae55de.png'; Target='Frames\modal_frame_v2.png'; W=2048; H=1280 },
    @{ Source='exec-b4755cc0-f7e5-4cd0-8285-858754c72b5b.png'; Target='Frames\modal_header_v2.png'; W=2048; H=256 },
    @{ Source='exec-ab574d2b-8de8-4734-a523-4d4a737bed5e.png'; Target='Frames\card_normal_v2.png'; W=1024; H=384 },
    @{ Source='exec-b9ebc205-74e3-4174-945c-7b2f4f9e830e.png'; Target='Frames\card_hover_v2.png'; W=1024; H=384 },
    @{ Source='exec-ab6d4d2a-bce8-461b-ab72-26158ccdbf8e.png'; Target='Frames\card_disabled_v2.png'; W=1024; H=384 },
    @{ Source='exec-36f2824b-d873-4af7-8f47-47da6785cf6f.png'; Target='Frames\card_featured_v2.png'; W=2048; H=448 },
    @{ Source='exec-b2f1521f-8a18-46fd-bb45-4119d5ce1fcf.png'; Target='Frames\card_featured_hover_v2.png'; W=2048; H=448 },
    @{ Source='exec-5b234e77-6c0a-47f2-a0cc-cec8139dfc0c.png'; Target='Frames\close_normal_v2.png'; W=256; H=256 },
    @{ Source='exec-108c4e93-c11b-4fab-9f87-3c4e9d0499ea.png'; Target='Frames\close_hover_v2.png'; W=256; H=256 },
    @{ Source='exec-cf69776a-1e5c-4e9c-8016-1bcb8677751c.png'; Target='Frames\close_pressed_v2.png'; W=256; H=256 },
    @{ Source='exec-dd181c5d-6716-4c78-bb5c-7ab6189a11d1.png'; Target='Icons\Bottom\company_v2.png'; W=256; H=256 },
    @{ Source='exec-431c5959-f91f-45b8-842c-cf4b8acd604e.png'; Target='Icons\Bottom\people_v2.png'; W=256; H=256 },
    @{ Source='exec-ddccf432-687f-478d-b45f-46e0ce7445ff.png'; Target='Icons\Bottom\projects_v2.png'; W=256; H=256 },
    @{ Source='exec-58304c13-9ba3-4385-aaed-5375fe11ece1.png'; Target='Icons\Bottom\research_v2.png'; W=256; H=256 },
    @{ Source='exec-203ba296-6ca1-4ce4-b8ed-709bce686969.png'; Target='Icons\Bottom\investment_v2.png'; W=256; H=256 },
    @{ Source='exec-e4cf4833-2953-4411-bbdf-d4f63e730dae.png'; Target='Icons\Investment\stock_market_v2.png'; W=320; H=320 },
    @{ Source='exec-96c8e489-d832-4866-92dd-edc0e626d74f.png'; Target='Icons\Investment\bank_loan_v2.png'; W=320; H=320 },
    @{ Source='exec-b926ed17-326b-45fc-b06d-6ac623df1c39.png'; Target='Icons\Investment\real_estate_v2.png'; W=320; H=320 },
    @{ Source='exec-b5569dab-7ee2-46c9-b137-d3fc96f5da80.png'; Target='Icons\Investment\angel_v2.png'; W=320; H=320 },
    @{ Source='exec-d9303c84-7ed9-4a8a-bb11-39fca718b2ff.png'; Target='Icons\Investment\mergers_v2.png'; W=320; H=320 },
    @{ Source='exec-88a7de1b-49b7-47e0-a76f-6b1b33d9005a.png'; Target='Markers\notification_badge_v2.png'; W=384; H=160 },
    @{ Source='exec-5a80c8dd-4c37-421f-8f78-181b932b6e04.png'; Target='Markers\coming_soon_ribbon_v2.png'; W=512; H=160 }
)

foreach ($asset in $assets) {
    $source = Join-Path $GeneratedRoot $asset.Source
    if (-not (Test-Path -LiteralPath $source)) { throw "Missing generated source: $source" }
    $destination = Join-Path $assetRoot $asset.Target
    [MainNavigationV2AssetPrep]::PrepareWithDimensions($source, $destination, $asset.W, $asset.H)
    if ($asset.Target.StartsWith('Frames\') -or $asset.Target.StartsWith('Markers\')) {
        # Runtime 9-slice textures keep a small real alpha gutter. Large generation padding
        # would otherwise become part of Unity's immutable border and erase small controls.
        [MainNavigationV2AssetPrep]::TrimTransparentPadding($destination, 8)
    }
    $audit = [MainNavigationV2AssetPrep]::Audit($destination)
    Write-Host "Prepared $($asset.Target) <- $($asset.Source) [$audit]"
}

$artifactRoot = Join-Path $ProjectRoot 'Artifacts\MainNavigationHudV2'
New-Item -ItemType Directory -Force -Path $artifactRoot | Out-Null
$rejected = 'C:\Users\godho\AppData\Local\Temp\codex-clipboard-25d23518-c4f6-4074-97fb-aea9b5a76e4a.png'
if (Test-Path -LiteralPath $rejected) {
    Copy-Item -LiteralPath $rejected -Destination (Join-Path $artifactRoot 'before-rejected-1920x1080.png') -Force
}
