using System;

namespace FamilyCompany.Presentation.Unity
{
    // One presentation scale for the entire attempt, not each file or folder. Download receives
    // 0..85 (aggregate compressed bytes), final verification 85..99 (aggregate verified bytes).
    // Only a validated worker result earns 100. These are stage weights, not an ETA or a byte ratio
    // between compressed and expanded data. Interleaved expand/reuse events never reset the value.
    public sealed class PatchDisplayProgress
    {
        public double Percent { get; private set; }
        public void Reset() => Percent = 0;
        public double Observe(string phase, long done, long total)
        {
            double fraction = total > 0 ? Math.Max(0d, Math.Min(1d, done / (double)total)) : 0d;
            double candidate = Percent;
            if (phase == "download" && total > 0) candidate = fraction * 85d;
            if (phase == "verify" && total > 0) candidate = 85d + fraction * 14d;
            Percent = Math.Max(Percent, Math.Min(99d, candidate));
            return Percent;
        }
        public double Complete() => Percent = 100d;
    }
}
