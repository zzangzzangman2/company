using System;
using FamilyCompany.Editor;

internal static class OfficeNavigationRegressionExternalHarness
{
    public static int Main()
    {
        try
        {
            OfficeNavigationRegressionReport report = OfficeNavigationRegressionSuite.Run(128);
            OfficeSharedLocomotionStrictReport strict =
                OfficeSharedLocomotionStrictValidation.Run();
            Console.WriteLine(
                "OFFICE_NAVIGATION_REGRESSION_EXTERNAL: PASS " +
                $"seeds={report.Seeds} " +
                $"paths={report.Paths} " +
                $"facing={report.FacingPresentationChecks} " +
                $"gait={report.GaitPresentationChecks} " +
                $"slides={report.CollisionSlideChecks} " +
                $"partition={report.MotionPartitionChecks} " +
                $"deadlockTicks={report.DeadlockTicks} strict=({strict})");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
    }
}
