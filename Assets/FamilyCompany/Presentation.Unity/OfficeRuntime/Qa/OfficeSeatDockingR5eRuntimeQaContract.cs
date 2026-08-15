namespace FamilyCompany.Presentation.Unity.OfficeRuntime.Qa
{
    /// <summary>Frozen command/artifact contract consumed by the serialized Unity gate.</summary>
    public static class OfficeSeatDockingR5eRuntimeQaContract
    {
        public const string ObserverFlag = "-familyCompanyChairR5eQa";
        public const string VisualRunnerFlag = "-familyCompanyChairR5eVisualQa";
        public const string FourTimesFlag = "-familyCompanyChairR5e4xQa";
        public const string ArtifactDirectoryArgument = "-familyCompanyChairR5eQaArtifacts";
        public const string ScenarioCatalogArgument = "-familyCompanyChairR5eQaScenarioCatalog";
        public const string ScenarioCatalogResource = "OfficeSeatDockingR5eScenarioCatalog";
        public const string CompletionMarker = "chair-r5e-complete.marker";
        public const string RuntimeResultFile = "chair-r5e-runtime-result.txt";
        public const string RuntimeManifestFile = "chair-r5e-runtime-artifact-manifest.tsv";
        public const string StartupBoundaryFile = "chair-r5e-startup-boundaries.csv";
        public const string PerformanceFrameFile = "chair-r5e-performance-frames.csv";
        public const string TransitionFile = "seat-transition-events-r5e.csv";
        public const string SeatedFile = "seat-session-samples-r5e.csv";
        public const string LocomotionFile = "locomotion-step-adapter-r5e.csv";
        public const string DecodedFrameFile = "classic-docking-r5e-decoded-frame-oracle.csv";
        public const string HumanReviewFile = "classic-docking-r5e-human-visual-review.csv";

        public static bool IsRequested(string[] arguments)
        {
            if (arguments == null) return false;
            for (var index = 0; index < arguments.Length; index++)
            {
                string argument = arguments[index];
                if (argument == ObserverFlag || argument == VisualRunnerFlag) return true;
            }
            return false;
        }
    }
}
