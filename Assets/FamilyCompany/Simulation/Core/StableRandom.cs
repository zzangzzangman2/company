using System;

namespace FamilyCompany.Simulation.Core
{
    public static class StableRandom
    {
        private const int Mask31 = 0x7fffffff;

        public static int MultiplyFnvPrime31Exact(int value)
        {
            var low = value & 0xffff;
            var high = (value >> 16) & 0xffff;
            var lowProduct = (long)low * 0x0193;
            var crossProduct = (long)low * 0x0100 + (long)high * 0x0193;
            return (int)((lowProduct + (crossProduct & 0x7fffL) * 0x10000L) & Mask31);
        }

        public static int StableHash31(string value)
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            var hash = unchecked((int)0x811c9dc5) & Mask31;
            foreach (var unit in value)
            {
                hash ^= unit;
                hash = MultiplyFnvPrime31Exact(hash);
            }

            return hash & Mask31;
        }

        public static int Multiply31Exact(int left, int right)
        {
            var leftLow = left & 0xffff;
            var leftHigh = (left >> 16) & 0x7fff;
            var rightLow = right & 0xffff;
            var rightHigh = (right >> 16) & 0x7fff;
            var lowProduct = (long)leftLow * rightLow;
            var crossProduct = (long)leftLow * rightHigh + (long)leftHigh * rightLow;
            return (int)((lowProduct + (crossProduct & 0x7fffL) * 0x10000L) & Mask31);
        }

        public static int StableRandomWord31(string key, int nonce = 0)
        {
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            var forward = StableHash31($"random-v2:{nonce}:{key}");
            var reverse = StableHash31($"{key}:{nonce}:2v-modnar");
            return Avalanche31(forward ^ RotateLeft31(reverse, 11) ^ 0x6d2b79f5);
        }

        public static int StableRandomInt(string key, int upperBound)
        {
            if (upperBound <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(upperBound));
            }

            const long range = 0x80000000L;
            var acceptedRange = range - range % upperBound;
            for (var nonce = 0; ; nonce++)
            {
                var value = StableRandomWord31(key, nonce);
                if (value < acceptedRange)
                {
                    return value % upperBound;
                }
            }
        }

        private static int Avalanche31(int value)
        {
            var mixed = value & Mask31;
            mixed ^= mixed >> 16;
            mixed = Multiply31Exact(mixed, 0x05ebca6b);
            mixed ^= mixed >> 13;
            mixed = Multiply31Exact(mixed, 0x42b2ae35);
            mixed ^= mixed >> 16;
            return mixed & Mask31;
        }

        private static int RotateLeft31(int value, int shift)
        {
            var amount = shift % 31;
            var masked = value & Mask31;
            if (amount == 0)
            {
                return masked;
            }

            return ((masked << amount) | (masked >> (31 - amount))) & Mask31;
        }
    }
}

