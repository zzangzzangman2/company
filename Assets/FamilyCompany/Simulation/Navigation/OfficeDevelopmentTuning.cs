using System;

namespace FamilyCompany.Simulation.Navigation
{
    // Explicit developer-only input, not campaign/save state. Immutable snapshots prevent a
    // partially written JSON file changing half of a purchase or one member of the family.
    public sealed class OfficeDevelopmentTuning
    {
        public float MoveSpeed { get; }
        public float Stride { get; }
        public float Phase { get; }
        public float PlayerFootX { get; }
        public float PlayerFootZ { get; }
        public float FatherFootX { get; }
        public float FatherFootZ { get; }
        public long WorkstationPriceWon { get; }

        public OfficeDevelopmentTuning(float speed, float stride, float phase, float px, float pz,
            float fx, float fz, long price)
        {
            Check(speed, 0.25f, 2f); Check(stride, 0.4f, 2.5f); Check(phase, 0f, 1f);
            Check(px, -0.25f, 0.25f); Check(pz, -0.25f, 0.25f);
            Check(fx, -0.25f, 0.25f); Check(fz, -0.25f, 0.25f);
            if (price < 10000 || price > 5000000) throw new ArgumentOutOfRangeException(nameof(price));
            MoveSpeed = speed; Stride = stride; Phase = phase;
            PlayerFootX = px; PlayerFootZ = pz; FatherFootX = fx; FatherFootZ = fz;
            WorkstationPriceWon = price;
        }
        private static void Check(float value, float min, float max)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value < min || value > max)
                throw new ArgumentOutOfRangeException(nameof(value), "Developer setting outside the permitted range.");
        }
    }

    public static class OfficeDevelopmentTuningSession
    {
        public static OfficeDevelopmentTuning Current { get; private set; }
        public static int Revision { get; private set; }
        public static void Apply(OfficeDevelopmentTuning snapshot) { Current = snapshot; Revision++; }
        public static void Clear() { Current = null; Revision++; }
    }
}
