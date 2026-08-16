using System;
using System.IO;
using FamilyCompany.Presentation.Unity.OfficeRuntime.Qa;

internal static class OfficeSeatDockingR5eProductionFixtureRunner
{
    public static int Main(string[] args)
    {
        try
        {
            if (args.Length != 2)
                throw new ArgumentException("Expected <catalog-path> <artifact-directory>.");
            string result = OfficeSeatDockingR5eProductionStaticFixture.Run(
                Path.GetFullPath(args[0]),
                Path.GetFullPath(args[1]));
            Console.WriteLine("OFFICE_SEAT_DOCKING_R5E_PRODUCTION_FIXTURE: PASS " + result);
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine("OFFICE_SEAT_DOCKING_R5E_PRODUCTION_FIXTURE: FAIL");
            Console.Error.WriteLine(exception);
            return 1;
        }
    }
}
