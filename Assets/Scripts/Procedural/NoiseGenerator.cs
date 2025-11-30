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

        /// <summary>
        /// Ridged noise - creates sharp mountain ridges and cliffs (like Cube World)
        /// Returns values in range -amplitude to amplitude
        /// </summary>
        public static float GetRidgedNoise(float x, float y, float z, int octaves, float frequency, float amplitude, float lacunarity, float persistence, int seed)
        {
            int[] p = CreatePermutationTable(seed);

            float total = 0f;
            float maxValue = 0f;
            float currentAmplitude = 1f;
            float currentFrequency = frequency;

            for (int i = 0; i < octaves; i++)
            {
                // Get noise and invert + abs for ridges
                float noise = Get3DPerlin(x * currentFrequency, y * currentFrequency, z * currentFrequency, p);
                noise = 1f - Mathf.Abs(noise); // Create ridges by inverting absolute value
                noise = noise * noise; // Square it for sharper ridges

                total += noise * currentAmplitude;
                maxValue += currentAmplitude;

                currentAmplitude *= persistence;
                currentFrequency *= lacunarity;
            }

            // Normalize and apply amplitude
            return (total / maxValue) * amplitude;
        }

        /// <summary>
        /// Domain warping - offsets sampling position based on other noise
        /// Creates more interesting, less grid-aligned terrain features
        /// </summary>
        public static Vector2 DomainWarp2D(float x, float z, float warpStrength, int seed)
        {
            int[] p = CreatePermutationTable(seed + 1000); // Different seed for warping

            float offsetX = Get3DPerlin(x * 0.02f, z * 0.02f, 100f, p) * warpStrength;
            float offsetZ = Get3DPerlin(x * 0.02f, z * 0.02f, 200f, p) * warpStrength;

            return new Vector2(x + offsetX, z + offsetZ);
        }

        /// <summary>
        /// Combined terrain noise - blends smooth and ridged noise based on a blend factor
        /// blendFactor 0 = pure smooth, 1 = pure ridged
        /// </summary>
        public static float GetTerrainNoise(float x, float y, float z,
            int octaves, float frequency, float amplitude, float lacunarity, float persistence,
            float ridgedBlend, int seed)
        {
            float smoothNoise = GetFractalNoise(x, y, z, octaves, frequency, amplitude, lacunarity, persistence, seed);

            if (ridgedBlend <= 0.001f)
            {
                return smoothNoise; // Optimization: skip ridged if not needed
            }

            float ridgedNoise = GetRidgedNoise(x, y, z, octaves, frequency, amplitude, lacunarity, persistence, seed + 5000);

            return Mathf.Lerp(smoothNoise, ridgedNoise, ridgedBlend);
        }

        /// <summary>
        /// Generate a random point in a grid cell based on cell coordinates and seed
        /// </summary>
        private static Vector2 GetCellPoint(int cellX, int cellY, int seed)
        {
            System.Random random = new System.Random(seed + cellX * 374761393 + cellY * 668265263);
            float offsetX = (float)random.NextDouble();
            float offsetY = (float)random.NextDouble();
            return new Vector2(cellX + offsetX, cellY + offsetY);
        }

        /// <summary>
        /// Cellular/Worley noise - returns distance to nearest cell point
        /// Returns value in range 0 to 1 (0 = at point, 1 = far from points)
        /// scale controls the size of cells (larger = bigger cells)
        /// </summary>
        public static float GetCellularNoise(float x, float y, float scale, int seed)
        {
            // Scale the coordinates
            x *= scale;
            y *= scale;

            // Find which cell we're in
            int cellX = Mathf.FloorToInt(x);
            int cellY = Mathf.FloorToInt(y);

            float minDistance = float.MaxValue;

            // Check this cell and all 8 neighboring cells
            for (int offsetX = -1; offsetX <= 1; offsetX++)
            {
                for (int offsetY = -1; offsetY <= 1; offsetY++)
                {
                    int neighborX = cellX + offsetX;
                    int neighborY = cellY + offsetY;

                    // Get the random point in this cell
                    Vector2 cellPoint = GetCellPoint(neighborX, neighborY, seed);

                    // Calculate distance to this point
                    float dx = x - cellPoint.x;
                    float dy = y - cellPoint.y;
                    float distance = Mathf.Sqrt(dx * dx + dy * dy);

                    minDistance = Mathf.Min(minDistance, distance);
                }
            }

            // Normalize distance to roughly 0-1 range
            // The max distance to a point in a cell is roughly sqrt(2) ≈ 1.414
            return Mathf.Clamp01(minDistance / 1.414f);
        }

        /// <summary>
        /// Cellular noise that returns both closest and second closest distance
        /// Useful for creating cell borders
        /// </summary>
        public static void GetCellularNoiseDistances(float x, float y, float scale, int seed,
            out float closest, out float secondClosest)
        {
            // Scale the coordinates
            x *= scale;
            y *= scale;

            // Find which cell we're in
            int cellX = Mathf.FloorToInt(x);
            int cellY = Mathf.FloorToInt(y);

            closest = float.MaxValue;
            secondClosest = float.MaxValue;

            // Check this cell and all 8 neighboring cells
            for (int offsetX = -1; offsetX <= 1; offsetX++)
            {
                for (int offsetY = -1; offsetY <= 1; offsetY++)
                {
                    int neighborX = cellX + offsetX;
                    int neighborY = cellY + offsetY;

                    // Get the random point in this cell
                    Vector2 cellPoint = GetCellPoint(neighborX, neighborY, seed);

                    // Calculate distance to this point
                    float dx = x - cellPoint.x;
                    float dy = y - cellPoint.y;
                    float distance = Mathf.Sqrt(dx * dx + dy * dy);

                    if (distance < closest)
                    {
                        secondClosest = closest;
                        closest = distance;
                    }
                    else if (distance < secondClosest)
                    {
                        secondClosest = distance;
                    }
                }
            }

            // Normalize distances
            closest = Mathf.Clamp01(closest / 1.414f);
            secondClosest = Mathf.Clamp01(secondClosest / 1.414f);
        }

        /// <summary>
        /// Get the ID of the closest cell point (for Voronoi diagrams)
        /// Returns a unique value for each cell
        /// </summary>
        public static int GetCellularVoronoiID(float x, float y, float scale, int seed)
        {
            // Scale the coordinates
            x *= scale;
            y *= scale;

            // Find which cell we're in
            int cellX = Mathf.FloorToInt(x);
            int cellY = Mathf.FloorToInt(y);

            float minDistance = float.MaxValue;
            int closestCellX = cellX;
            int closestCellY = cellY;

            // Check this cell and all 8 neighboring cells
            for (int offsetX = -1; offsetX <= 1; offsetX++)
            {
                for (int offsetY = -1; offsetY <= 1; offsetY++)
                {
                    int neighborX = cellX + offsetX;
                    int neighborY = cellY + offsetY;

                    // Get the random point in this cell
                    Vector2 cellPoint = GetCellPoint(neighborX, neighborY, seed);

                    // Calculate distance to this point
                    float dx = x - cellPoint.x;
                    float dy = y - cellPoint.y;
                    float distance = Mathf.Sqrt(dx * dx + dy * dy);

                    if (distance < minDistance)
                    {
                        minDistance = distance;
                        closestCellX = neighborX;
                        closestCellY = neighborY;
                    }
                }
            }

            // Return a unique ID for this cell (combine coordinates)
            return closestCellX * 73856093 + closestCellY * 19349663;
        }

        /// <summary>
        /// Generate a random point within a chunk based on chunk coordinates and seed
        /// Each chunk has a 90% chance of having ONE random point, 10% chance of no point
        /// Returns null if chunk has no point
        /// </summary>
        private static Vector2? GetChunkPoint(int chunkX, int chunkY, int chunkSize, int seed)
        {
            // Create a better seed by combining chunk coordinates with the base seed
            // Use prime numbers and XOR for better distribution
            int chunkSeed = seed;
            chunkSeed ^= chunkX.GetHashCode();
            chunkSeed = (chunkSeed << 5) + chunkSeed + chunkY.GetHashCode(); // Hash combination

            System.Random random = new System.Random(chunkSeed);

            // 10% chance this chunk has no point
            if (random.NextDouble() < 0.1)
            {
                return null;
            }

            // Generate random point within the chunk
            float offsetX = (float)random.NextDouble() * chunkSize;
            float offsetY = (float)random.NextDouble() * chunkSize;
            return new Vector2(chunkX * chunkSize + offsetX, chunkY * chunkSize + offsetY);
        }

        /// <summary>
        /// Chunk-based cellular noise - each chunk has a 90% chance of ONE random point
        /// Returns distance to nearest chunk point (0 to 1)
        /// Perfect for biome-like generation where each chunk is a region
        /// </summary>
        public static float GetChunkCellularNoise(float x, float y, int chunkSize, int seed)
        {
            // Find which chunk we're in
            int chunkX = Mathf.FloorToInt(x / chunkSize);
            int chunkY = Mathf.FloorToInt(y / chunkSize);

            float minDistance = float.MaxValue;

            // Check this chunk and all 8 neighboring chunks (3x3 grid)
            // Need to check neighbors because closest point might be in adjacent chunk
            for (int offsetX = -1; offsetX <= 1; offsetX++)
            {
                for (int offsetY = -1; offsetY <= 1; offsetY++)
                {
                    int neighborX = chunkX + offsetX;
                    int neighborY = chunkY + offsetY;

                    // Get the random point in this chunk (might be null if no point)
                    Vector2? chunkPoint = GetChunkPoint(neighborX, neighborY, chunkSize, seed);

                    // Skip this chunk if it has no point
                    if (!chunkPoint.HasValue)
                        continue;

                    // Calculate distance to this point
                    float dx = x - chunkPoint.Value.x;
                    float dy = y - chunkPoint.Value.y;
                    float distance = Mathf.Sqrt(dx * dx + dy * dy);

                    minDistance = Mathf.Min(minDistance, distance);
                }
            }

            // Normalize distance based on chunk size
            // Max possible distance is roughly sqrt(2) * chunkSize (diagonal across chunk)
            float maxDistance = chunkSize * 1.414f;
            return Mathf.Clamp01(minDistance / maxDistance);
        }

        /// <summary>
        /// Chunk-based cellular noise that returns both closest and second closest distances
        /// Useful for creating chunk/biome borders
        /// </summary>
        public static void GetChunkCellularNoiseDistances(float x, float y, int chunkSize, int seed,
            out float closest, out float secondClosest)
        {
            // Find which chunk we're in
            int chunkX = Mathf.FloorToInt(x / chunkSize);
            int chunkY = Mathf.FloorToInt(y / chunkSize);

            closest = float.MaxValue;
            secondClosest = float.MaxValue;

            // Check this chunk and all 8 neighboring chunks
            for (int offsetX = -1; offsetX <= 1; offsetX++)
            {
                for (int offsetY = -1; offsetY <= 1; offsetY++)
                {
                    int neighborX = chunkX + offsetX;
                    int neighborY = chunkY + offsetY;

                    // Get the random point in this chunk (might be null if no point)
                    Vector2? chunkPoint = GetChunkPoint(neighborX, neighborY, chunkSize, seed);

                    // Skip this chunk if it has no point
                    if (!chunkPoint.HasValue)
                        continue;

                    // Calculate distance to this point
                    float dx = x - chunkPoint.Value.x;
                    float dy = y - chunkPoint.Value.y;
                    float distance = Mathf.Sqrt(dx * dx + dy * dy);

                    if (distance < closest)
                    {
                        secondClosest = closest;
                        closest = distance;
                    }
                    else if (distance < secondClosest)
                    {
                        secondClosest = distance;
                    }
                }
            }

            // Normalize distances based on chunk size
            float maxDistance = chunkSize * 1.414f;
            closest = Mathf.Clamp01(closest / maxDistance);
            secondClosest = Mathf.Clamp01(secondClosest / maxDistance);
        }

        /// <summary>
        /// Get the chunk coordinates of the nearest chunk point (for Voronoi diagrams)
        /// Returns chunk X and Y coordinates, or current chunk if no points found
        /// </summary>
        public static Vector2Int GetChunkVoronoiID(float x, float y, int chunkSize, int seed)
        {
            // Find which chunk we're in
            int chunkX = Mathf.FloorToInt(x / chunkSize);
            int chunkY = Mathf.FloorToInt(y / chunkSize);

            float minDistance = float.MaxValue;
            int closestChunkX = chunkX;
            int closestChunkY = chunkY;

            // Check this chunk and all 8 neighboring chunks
            for (int offsetX = -1; offsetX <= 1; offsetX++)
            {
                for (int offsetY = -1; offsetY <= 1; offsetY++)
                {
                    int neighborX = chunkX + offsetX;
                    int neighborY = chunkY + offsetY;

                    // Get the random point in this chunk (might be null if no point)
                    Vector2? chunkPoint = GetChunkPoint(neighborX, neighborY, chunkSize, seed);

                    // Skip this chunk if it has no point
                    if (!chunkPoint.HasValue)
                        continue;

                    // Calculate distance to this point
                    float dx = x - chunkPoint.Value.x;
                    float dy = y - chunkPoint.Value.y;
                    float distance = Mathf.Sqrt(dx * dx + dy * dy);

                    if (distance < minDistance)
                    {
                        minDistance = distance;
                        closestChunkX = neighborX;
                        closestChunkY = neighborY;
                    }
                }
            }

            return new Vector2Int(closestChunkX, closestChunkY);
        }
    }
}
