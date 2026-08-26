using System;

namespace CalamityOverhaul.Content.Scenarios.Hadalworld.Gen.Core
{
    //自写噪声栈(蓝图H4:tML无内置FastNoiseLite):
    //整数哈希value noise+fBm+ridged+domain warp+Worley F1
    //全静态纯函数,种子入参,跨harness/游戏逐位一致
    internal static class HadalNoise
    {
        //整数格点哈希→[0,1),雪崩充分(splitmix64尾段)
        private static float Hash01(int x, int y, ulong seed) {
            ulong h = seed ^ ((ulong)(uint)x * 0x9E3779B97F4A7C15UL) ^ ((ulong)(uint)y * 0xC2B2AE3D27D4EB4FUL);
            h = (h ^ (h >> 30)) * 0xBF58476D1CE4E5B9UL;
            h = (h ^ (h >> 27)) * 0x94D049BB133111EBUL;
            h ^= h >> 31;
            return (h >> 40) * (1f / (1 << 24));
        }

        private static float Smooth(float t) => t * t * (3f - 2f * t);

        /// <summary>2D value noise,[0,1)</summary>
        internal static float Value2(float x, float y, ulong seed) {
            int x0 = (int)MathF.Floor(x);
            int y0 = (int)MathF.Floor(y);
            float tx = Smooth(x - x0);
            float ty = Smooth(y - y0);
            float a = Hash01(x0, y0, seed);
            float b = Hash01(x0 + 1, y0, seed);
            float c = Hash01(x0, y0 + 1, seed);
            float d = Hash01(x0 + 1, y0 + 1, seed);
            return a + (b - a) * tx + (c - a) * ty + (a - b - c + d) * tx * ty;
        }

        /// <summary>1D value noise,[0,1)</summary>
        internal static float Value1(float x, ulong seed) => Value2(x, 0.5f, seed);

        /// <summary>2D fBm,[0,1)附近(归一化)</summary>
        internal static float Fbm2(float x, float y, ulong seed, int octaves, float lacunarity = 2f, float gain = 0.5f) {
            float sum = 0f, amp = 1f, norm = 0f, freq = 1f;
            for (int i = 0; i < octaves; i++) {
                sum += Value2(x * freq, y * freq, seed + (ulong)i * 0x51_7C_C1_B7_27_22_0A_95UL) * amp;
                norm += amp;
                amp *= gain;
                freq *= lacunarity;
            }
            return sum / norm;
        }

        /// <summary>1D fBm,[0,1)附近</summary>
        internal static float Fbm1(float x, ulong seed, int octaves, float lacunarity = 2f, float gain = 0.5f)
            => Fbm2(x, 0.5f, seed, octaves, lacunarity, gain);

        /// <summary>山脊多重分形:峰线锐利,[0,1),沟壁棱脊用</summary>
        internal static float Ridged2(float x, float y, ulong seed, int octaves, float lacunarity = 2f, float gain = 0.5f) {
            float sum = 0f, amp = 1f, norm = 0f, freq = 1f;
            for (int i = 0; i < octaves; i++) {
                float v = Value2(x * freq, y * freq, seed + (ulong)i * 0x9E_37_79_B9_7F_4A_7C_15UL);
                v = 1f - MathF.Abs(v * 2f - 1f);
                sum += v * v * amp;
                norm += amp;
                amp *= gain;
                freq *= lacunarity;
            }
            return sum / norm;
        }

        /// <summary>域扭曲坐标:消灭"数学光滑边",层界/壁面侵蚀前先过一遍</summary>
        internal static (float x, float y) Warp2(float x, float y, ulong seed, float amp, float freq) {
            float wx = Fbm2(x * freq, y * freq, seed ^ 0xA5A5UL, 3) - 0.5f;
            float wy = Fbm2(x * freq, y * freq, seed ^ 0x5A5AUL, 3) - 0.5f;
            return (x + wx * 2f * amp, y + wy * 2f * amp);
        }

        /// <summary>Worley F1最近距离场,[0,1)(cellSize为特征尺寸),溶洞腔壁纹理用</summary>
        internal static float WorleyF1(float x, float y, ulong seed, float cellSize) {
            float cx = x / cellSize;
            float cy = y / cellSize;
            int ix = (int)MathF.Floor(cx);
            int iy = (int)MathF.Floor(cy);
            float best = float.MaxValue;
            for (int dy = -1; dy <= 1; dy++) {
                for (int dx = -1; dx <= 1; dx++) {
                    int gx = ix + dx;
                    int gy = iy + dy;
                    float px = gx + Hash01(gx, gy, seed);
                    float py = gy + Hash01(gx, gy, seed ^ 0xBEEFUL);
                    float ddx = px - cx;
                    float ddy = py - cy;
                    float d = ddx * ddx + ddy * ddy;
                    if (d < best) {
                        best = d;
                    }
                }
            }
            return MathF.Min(1f, MathF.Sqrt(best));
        }
    }
}
