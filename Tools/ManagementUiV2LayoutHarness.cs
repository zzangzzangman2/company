using System;
using FamilyCompany.Simulation.ManagementUi;

internal static class ManagementUiV2LayoutHarness
{
    private static int Main()
    {
        try
        {
            ManagementUiAccessibility.Validate();
            Console.WriteLine(
                $"MANAGEMENT_UI_V2_CONTRAST_EXTERNAL: PASS " +
                $"body/panel={ManagementUiAccessibility.ContrastRatio(ManagementUiAccessibility.SecondaryTextHex, ManagementUiAccessibility.PanelHex):0.##}:1 " +
                $"primary={ManagementUiAccessibility.ContrastRatio(ManagementUiAccessibility.CardHex, ManagementUiAccessibility.AccentHex):0.##}:1");
            foreach (var resolution in new[] { (1280, 720), (1920, 1080), (2048, 1152) })
            {
                var layout = ManagementUiLayoutMetrics.Calculate(
                    resolution.Item1,
                    resolution.Item2,
                    UiSafeInsets.None);
                ManagementUiLayoutMetrics.Validate(layout);
                Console.WriteLine(
                    $"MANAGEMENT_UI_V2_LAYOUT_EXTERNAL: {resolution.Item1}x{resolution.Item2} " +
                    $"scale={layout.ScaleFactor:0.####} top={layout.TopHud} " +
                    $"family={layout.FamilyRail} center={layout.ManagementCenter} " +
                    $"quick={layout.QuickActions} progress={layout.Progress} " +
                    $"cards={layout.OfferCards[0]}|{layout.OfferCards[1]}|{layout.OfferCards[2]}");
            }
            Console.WriteLine("MANAGEMENT_UI_V2_LAYOUT_EXTERNAL: PASS");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            Console.Error.WriteLine("MANAGEMENT_UI_V2_LAYOUT_EXTERNAL: FAIL");
            return 1;
        }
    }
}
