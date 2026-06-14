using System;
using System.Collections.Generic;
using Terraria;

namespace CalamityOverhaul.Content.ADV.Scenarios.AcheronProtocols.Machines.Gargoyles
{
    /// <summary>
    /// 湍流鸟群算法：使用空间网格加速的 Boids 变体。 核心设计：<br/> · 去除凝聚力(Cohesion)，只保留分离+对齐，避免虫群挤成一团<br/> · 流道使用3层叠加波+时间演化，路径复杂不可预测<br/> · 每个个体拥有独立噪声种子，通过时间+位置的伪噪声产生有机差异<br/> · 湍流场以X向（飘逸顺滑）为主、Y向（上下翻腾）为辅<br/> · 使用空间哈希网格将 O(n²) 邻域搜索降至近 O(n)<br/>
    /// </summary>
    internal static class GargoyleBoids
    {
        #region 流道配置

        /// <summary>流道数量</summary>
        internal const int NumStreams = 12;

        private const int Octaves = 3;
        private static float[,] octaveAmplitude;
        private static float[,] octaveFrequency;
        private static float[,] octavePhase;
        private static float[] streamBaseY;
        private static float[,] octaveTimeDrift;
        private static bool initialized;
        private static int globalTick;

        #endregion

        #region 空间网格

        private const int CellSize = 120;
        private static readonly Dictionary<long, List<int>> grid = new(512);
        private static readonly Stack<List<int>> listPool = new(256);

        #endregion

        #region Boids 参数

        private const float ProtectedRange = 26f;
        private const float SeparationWeight = 0.06f;

        private const float VisualRange = 140f;
        private const float AlignmentWeight = 0.05f;

        /// <summary>流道Y跟踪权重：非常柔性，只做大方向引导</summary>
        private const float StreamYWeight = 0.006f;

        /// <summary>湍流场X权重：产生飘逸的水平速度变化</summary>
        private const float TurbulenceXWeight = 0.15f;
        /// <summary>湍流场Y权重：轻微的垂直扰动，远小于X</summary>
        private const float TurbulenceYWeight = 0.06f;

        /// <summary>随机噪声幅度</summary>
        private const float NoiseAmount = 0.06f;

        /// <summary>前向推力：始终有一个稳定的向左推力保持主方向</summary>
        private const float ForwardDrive = -0.08f;

        private const float MinSpeed = 13f;
        private const float MaxSpeed = 24f;

        #endregion

        private static Vector2[] newVelocities = Array.Empty<Vector2>();

        #region 初始化 / 重置

        internal static void InitializeStreams(float skyY, float spreadHeight) {
            octaveAmplitude = new float[NumStreams, Octaves];
            octaveFrequency = new float[NumStreams, Octaves];
            octavePhase = new float[NumStreams, Octaves];
            octaveTimeDrift = new float[NumStreams, Octaves];
            streamBaseY = new float[NumStreams];

            for (int i = 0; i < NumStreams; i++) {
                float norm = (i + 0.5f) / NumStreams;
                streamBaseY[i] = skyY - spreadHeight * 0.5f + spreadHeight * norm;

                for (int o = 0; o < Octaves; o++) {
                    float octaveScale = 1f / (1 + o);
                    octaveAmplitude[i, o] = (25f + Main.rand.NextFloat(45f)) * octaveScale;
                    octaveFrequency[i, o] = (0.0008f + Main.rand.NextFloat(0.0018f)) * (1 + o * 1.3f);
                    octavePhase[i, o] = Main.rand.NextFloat(MathHelper.TwoPi);
                    octaveTimeDrift[i, o] = Main.rand.NextFloat(0.003f, 0.012f)
                        * (Main.rand.NextBool() ? 1f : -1f);
                }
            }

            globalTick = 0;
            initialized = true;
        }

        internal static float GetStreamTargetY(int streamIndex, float x) {
            if (!initialized) return 0f;
            int idx = streamIndex % NumStreams;

            float y = streamBaseY[idx];
            for (int o = 0; o < Octaves; o++) {
                float timeShift = globalTick * octaveTimeDrift[idx, o];
                y += octaveAmplitude[idx, o]
                    * MathF.Sin(octaveFrequency[idx, o] * x + octavePhase[idx, o] + timeShift);
            }
            return y;
        }

        internal static void Reset() {
            initialized = false;
            globalTick = 0;
            ReturnGridLists();
        }

        #endregion

        #region 湍流场：X向飘逸为主

        /// <summary>

        /// 基于位置+时间的湍流场。X方向是主力（产生速度的飘逸感）， Y方向是辅助（轻微起伏），避免上下摇摆的傻瓜效果

        /// </summary>
        private static Vector2 GetTurbulence(float x, float y, float noiseSeed) {
            float t = globalTick * 0.006f;

            //X向：多层叠加，产生加速-减速的飘逸韵律感
            float tx1 = MathF.Sin(x * 0.0029f + y * 0.0013f + t + noiseSeed);
            float tx2 = MathF.Sin(x * 0.0061f - y * 0.0021f + t * 1.3f + noiseSeed * 2.1f) * 0.5f;
            float tx3 = MathF.Sin(x * 0.0011f + t * 0.7f + noiseSeed * 0.7f) * 0.7f;

            //Y向：更温和，低频为主
            float ty1 = MathF.Sin(y * 0.0041f + x * 0.0017f - t * 0.8f + noiseSeed * 1.3f);
            float ty2 = MathF.Cos(x * 0.0033f + y * 0.0029f + t * 0.5f + noiseSeed * 0.4f) * 0.4f;

            return new Vector2(
                (tx1 + tx2 + tx3) * TurbulenceXWeight,
                (ty1 + ty2) * TurbulenceYWeight
            );
        }

        #endregion

        #region 每帧集群更新

        internal static void UpdateFlock(List<GargoyleActor> flock) {
            int count = flock.Count;
            if (count == 0) return;

            globalTick++;
            BuildGrid(flock, count);

            if (newVelocities.Length < count)
                newVelocities = new Vector2[count + 256];

            float protectedSq = ProtectedRange * ProtectedRange;
            float visualSq = VisualRange * VisualRange;

            for (int i = 0; i < count; i++) {
                GargoyleActor self = flock[i];
                Vector2 pos = self.Position;
                Vector2 vel = self.BoidVelocity;

                Vector2 separation = Vector2.Zero;
                Vector2 alignSum = Vector2.Zero;
                int neighbors = 0;

                int cx = (int)MathF.Floor(pos.X / CellSize);
                int cy = (int)MathF.Floor(pos.Y / CellSize);

                for (int dx = -1; dx <= 1; dx++) {
                    for (int dy = -1; dy <= 1; dy++) {
                        long key = PackKey(cx + dx, cy + dy);
                        if (!grid.TryGetValue(key, out var cell)) continue;

                        for (int ci = 0; ci < cell.Count; ci++) {
                            int j = cell[ci];
                            if (i == j) continue;

                            GargoyleActor other = flock[j];
                            float ddx = other.Position.X - pos.X;
                            float ddy = other.Position.Y - pos.Y;
                            float distSq = ddx * ddx + ddy * ddy;

                            if (distSq < protectedSq && distSq > 0.001f) {
                                float invDist = 1f / MathF.Sqrt(distSq);
                                separation.X -= ddx * invDist;
                                separation.Y -= ddy * invDist;
                            }

                            if (distSq < visualSq) {
                                alignSum += other.BoidVelocity;
                                neighbors++;
                            }
                        }
                    }
                }

                //── 合成转向力 ──
                Vector2 steer = separation * SeparationWeight;

                if (neighbors > 0) {
                    steer += (alignSum / neighbors - vel) * AlignmentWeight;
                }

                //流道跟踪：极其柔性，仅做大范围引导
                if (initialized) {
                    float targetY = GetStreamTargetY(self.SwarmGroup, pos.X);
                    float yError = targetY - pos.Y;
                    //距离远时力稍强，近时几乎不干预
                    float pull = StreamYWeight * MathHelper.Clamp(MathF.Abs(yError) / 200f, 0.1f, 1f);
                    steer.Y += yError * pull;
                }

                //湍流：每个个体用自身 NoiseSeed 获得差异化的力场采样
                Vector2 turb = GetTurbulence(pos.X, pos.Y, self.NoiseSeed);
                steer += turb;

                //前向推力：稳定的向左驱动，保持主方向不散
                steer.X += ForwardDrive;

                //微噪声
                steer.X += (Main.rand.NextFloat() - 0.5f) * NoiseAmount * 2f;
                steer.Y += (Main.rand.NextFloat() - 0.5f) * NoiseAmount * 2f;

                //── 应用转向，限制速度 ──
                Vector2 newVel = vel + steer;

                //Y速度衰减：抑制过大的垂直速度，让飞行更水平流畅
                newVel.Y *= 0.97f;

                float depthMod = MathHelper.Lerp(0.8f, 1.15f,
                    MathHelper.Clamp((self.DepthScale - 0.35f) / 0.8f, 0f, 1f));

                float speed = newVel.Length();
                float cMin = MinSpeed * depthMod;
                float cMax = MaxSpeed * depthMod;

                if (speed > cMax) newVel *= cMax / speed;
                else if (speed > 0.01f && speed < cMin) newVel *= cMin / speed;

                newVelocities[i] = newVel;
            }

            for (int i = 0; i < count; i++) {
                flock[i].BoidVelocity = newVelocities[i];
            }

            ReturnGridLists();
        }

        #endregion

        #region 空间网格

        private static void BuildGrid(List<GargoyleActor> flock, int count) {
            ReturnGridLists();
            for (int i = 0; i < count; i++) {
                long key = GridKey(flock[i].Position);
                if (!grid.TryGetValue(key, out var cell)) {
                    cell = listPool.Count > 0 ? listPool.Pop() : new List<int>(16);
                    grid[key] = cell;
                }
                cell.Add(i);
            }
        }

        private static void ReturnGridLists() {
            foreach (var kvp in grid) {
                kvp.Value.Clear();
                listPool.Push(kvp.Value);
            }
            grid.Clear();
        }

        private static long GridKey(Vector2 pos) {
            int cx = (int)MathF.Floor(pos.X / CellSize);
            int cy = (int)MathF.Floor(pos.Y / CellSize);
            return PackKey(cx, cy);
        }

        private static long PackKey(int cx, int cy) {
            return (long)cx << 32 | (uint)cy;
        }

        #endregion
    }
}
