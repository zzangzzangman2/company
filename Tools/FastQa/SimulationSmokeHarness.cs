using System;
using System.Linq;
using FamilyCompany.Editor;
using FamilyCompany.Simulation.Prototype;

namespace FamilyCompany.Tools.FastQa
{
    public static class SimulationSmokeHarness
    {
        public static int Main(string[] args)
        {
            try
            {
                var first = PrototypeStateFactory.Create(20000103);
                var second = PrototypeStateFactory.Create(20000103);
                new SimulationRunner(first).AdvanceMinutes(240);
                new SimulationRunner(second).AdvanceMinutes(120);
                new SimulationRunner(second).AdvanceMinutes(120);
                Require(first.Time.ElapsedMinutes == second.Time.ElapsedMinutes, "partitioned clock");
                Require(first.Family.Members.Select(item => item.Energy)
                    .SequenceEqual(second.Family.Members.Select(item => item.Energy)), "partitioned energy");
                StaminaSimulationValidation.RunAll();
                Console.WriteLine(StarterProductValidation.RunAll());
                Console.WriteLine("FAST_QA_SIMULATION_HARNESS: PASS");
                return 0;
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine(exception);
                Console.Error.WriteLine("FAST_QA_SIMULATION_HARNESS: FAIL");
                return 1;
            }
        }

        private static void Require(bool condition, string label)
        {
            if (!condition) throw new InvalidOperationException("Failed: " + label);
        }
    }
}
