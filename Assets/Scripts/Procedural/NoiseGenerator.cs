using UnityEngine;

namespace LandOfTheConsumers.Procedural
{
    public static class NoiseGenerator
    {
        private static readonly int[] basePermutation = {
            151,160,137,91,90,15,131,13,201,95,96,53,194,233,7,225,140,36,103,30,69,142,
            8,99,37,240,21,10,23,190,6,148,247,120,234,75,0,26,197,62,94,252,219,203,117,
            35,11,32,57,177,33,88,237,149,56,87,174,20,125,136,171,168,68,175,74,165,71,
            134,139,48,27,166,77,146,158,231,83,111,229,122,60,211,133,230,220,105,92,41,
            55,46,245,40,244,102,143,54,65,25,63,161,1,216,80,73,209,76,132,187,208,89,
            18,169,200,196,135,130,116,188,159,86,164,100,109,198,173,186,3,64,52,217,226,
            250,124,123,5,202,38,147,118,126,255,82,85,212,207,206,59,227,47,16,58,17,182,
            189,28,42,223,183,170,213,119,248,152,2,44,154,163,70,221,153,101,155,167,43,
            172,9,129,22,39,253,19,98,108,110,79,113,224,232,178,185,112,104,218,246,97,
            228,251,34,242,193,238,210,144,12,191,179,162,241,81,51,145,235,249,14,239,
            107,49,192,214,31,181,199,106,157,184,84,204,176,115,121,50,45,127,4,150,254,
            138,236,205,93,222,114,67,29,24,72,243,141,128,195,78,66,215,61,156,180
        };

        private static int[] CreatePermutationTable(int seed)
        {
            int[] permutation = new int[256];
            for (int i = 0; i < 256; i++)
            {
                permutation[i] = i;
            }

            // Shuffle using seed
            System.Random random = new System.Random(seed);
            for (int i = 255; i > 0; i--)
            {
                int swapIndex = random.Next(0, i + 1);
                int temp = permutation[i];
                permutation[i] = permutation[swapIndex];
                permutation[swapIndex] = temp;
            }

            // Create 512-length table
            int[] p = new int[512];
            for (int i = 0; i < 512; i++)
            {
                p[i] = permutation[i % 256];
            }

            return p;
        }

        private static float Get3DPerlin(float x, float y, float z, int[] p)
        {
            int xi = Mathf.FloorToInt(x) & 255;
            int yi = Mathf.FloorToInt(y) & 255;
            int zi = Mathf.FloorToInt(z) & 255;

            float xf = x - Mathf.Floor(x);
            float yf = y - Mathf.Floor(y);
            float zf = z - Mathf.Floor(z);

            float u = Fade(xf);
            float v = Fade(yf);
            float w = Fade(zf);

            int aaa = p[p[p[xi] + yi] + zi];
            int aba = p[p[p[xi] + yi + 1] + zi];
            int aab = p[p[p[xi] + yi] + zi + 1];
            int abb = p[p[p[xi] + yi + 1] + zi + 1];
            int baa = p[p[p[xi + 1] + yi] + zi];
            int bba = p[p[p[xi + 1] + yi + 1] + zi];
            int bab = p[p[p[xi + 1] + yi] + zi + 1];
            int bbb = p[p[p[xi + 1] + yi + 1] + zi + 1];

            float x1 = Mathf.Lerp(Grad(aaa, xf, yf, zf), Grad(baa, xf - 1, yf, zf), u);
            float x2 = Mathf.Lerp(Grad(aba, xf, yf - 1, zf), Grad(bba, xf - 1, yf - 1, zf), u);
            float y1 = Mathf.Lerp(x1, x2, v);

            float x3 = Mathf.Lerp(Grad(aab, xf, yf, zf - 1), Grad(bab, xf - 1, yf, zf - 1), u);
            float x4 = Mathf.Lerp(Grad(abb, xf, yf - 1, zf - 1), Grad(bbb, xf - 1, yf - 1, zf - 1), u);
            float y2 = Mathf.Lerp(x3, x4, v);

            return Mathf.Lerp(y1, y2, w);
        }

        public static float GetFractalNoise(float x, float y, float z, int octaves, float frequency, float amplitude, float lacunarity, float persistence, int seed)
        {
            int[] p = CreatePermutationTable(seed);

            float total = 0f;
            float maxValue = 0f;
            float currentAmplitude = 1f; // Start at 1 for normalization
            float currentFrequency = frequency;

            for (int i = 0; i < octaves; i++)
            {
                total += Get3DPerlin(x * currentFrequency, y * currentFrequency, z * currentFrequency, p) * currentAmplitude;
                maxValue += currentAmplitude;

                currentAmplitude *= persistence;
                currentFrequency *= lacunarity;
            }

            // Normalize to -1 to 1 range, then apply amplitude
            return (total / maxValue) * amplitude;
        }

        private static float Fade(float t)
        {
            return t * t * t * (t * (t * 6 - 15) + 10);
        }

        private static float Grad(int hash, float x, float y, float z)
        {
            int h = hash & 15;
            float u = h < 8 ? x : y;
            float v = h < 4 ? y : h == 12 || h == 14 ? x : z;
            return ((h & 1) == 0 ? u : -u) + ((h & 2) == 0 ? v : -v);
        }
    }
}
