using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;

namespace CalamityOverhaul.Content.Industrials.MaterialFlow.ItemPipelines
{
    /// <summary>
    /// 管道路径流动动画管理器
    /// <para>从输出端到所有可达输入端的箭头粒子洪流。</para>
    /// <para>路径不再独立做 BFS：直接复用 <see cref="ItemPipelineNetwork"/> 的下一跳路由表，
    /// 仅当全局拓扑版本变化时重建路径，开销极低。</para>
    /// </summary>
    internal class PipelineFlowAnimator
    {
        /// <summary>流动粒子(沿整段路径推进)</summary>
        private struct FlowParticle
        {
            public float Progress;//0~1, 沿整段路径的总进度
            public int BranchId;
            public float Alpha;
        }

        /// <summary>分支路径</summary>
        private struct BranchPath
        {
            public List<Point16> Path;//从输出端到某输入端的路径(包含两端)
            public float Speed;//每帧进度增量
        }

        private readonly List<BranchPath> branchPaths = [];
        private readonly List<FlowParticle> particles = [];

        private int spawnTimer;
        private int nextBranchIndex;

        /// <summary>缓存的拓扑版本(变化即重建)</summary>
        private int cachedTopologyVersion = -1;
        /// <summary>上次重建的输出端位置(用于检测同一动画器复用错位)</summary>
        private Point16 cachedOutputPos = Point16.NegativeOne;

        public bool HasValidPath => branchPaths.Count > 0;

        /// <summary>路径周期(帧)：粒子从起点到终点完成的目标帧数</summary>
        private const int AnimationCycleFrames = 100;
        /// <summary>粒子生成间隔(帧)</summary>
        private const int BaseSpawnInterval = 8;
        /// <summary>每个分支的最大粒子数</summary>
        private const int MaxParticlesPerBranch = 10;
        /// <summary>路径最大跳数(防御保护, 抵挡环网或异常拓扑)</summary>
        private const int MaxPathLength = 1024;

        /// <summary>
        /// 由所有者(输出端 TP)每帧调用一次：自动按需重建路径并推进粒子
        /// </summary>
        public void Tick(ItemPipelineTP outputEndpoint) {
            if (outputEndpoint == null || outputEndpoint.Mode != ItemPipelineMode.Output) {
                Clear();
                return;
            }

            int currentTopology = ItemPipelineNetwork.CurrentTopologyVersion;
            if (currentTopology != cachedTopologyVersion || cachedOutputPos != outputEndpoint.Position) {
                RebuildPaths(outputEndpoint);
                cachedTopologyVersion = currentTopology;
                cachedOutputPos = outputEndpoint.Position;
            }

            UpdateParticles();
        }

        /// <summary>
        /// 利用网络路由表"反推"路径：从输出端开始沿"指向输入端"的下一跳走，直到到达输入端。
        /// 复杂度 O(I × L)，I=输入端数, L=路径长度，远小于原本的全网 BFS。
        /// </summary>
        private void RebuildPaths(ItemPipelineTP output) {
            branchPaths.Clear();
            particles.Clear();

            var inputs = ItemPipelineNetwork.GetReachableInputs(output.Position);
            if (inputs == null || inputs.Count == 0) {
                return;
            }

            for (int i = 0; i < inputs.Count; i++) {
                var inputPos = inputs[i];
                if (inputPos == output.Position) {
                    continue;
                }
                List<Point16> path = TraceRoute(output, inputPos);
                if (path == null || path.Count < 2) {
                    continue;
                }
                float speed = System.Math.Max(1f / AnimationCycleFrames, 0.01f);
                branchPaths.Add(new BranchPath { Path = path, Speed = speed });
            }
        }

        /// <summary>
        /// 从输出端按下一跳路由走到目标输入端，返回路径点序列（含两端）
        /// </summary>
        private static List<Point16> TraceRoute(ItemPipelineTP start, Point16 inputPos) {
            List<Point16> path = [start.Position];
            ItemPipelineTP current = start;
            for (int hop = 0; hop < MaxPathLength; hop++) {
                if (current.Position == inputPos) {
                    return path;
                }
                if (!ItemPipelineNetwork.TryGetRouting(current.Position, inputPos, out var entry)) {
                    return null;
                }
                int dir = entry.NextDir;
                if (dir > 3) {
                    //哨兵: 自身就是输入端 - 已经处理
                    break;
                }
                var sides = current.SideStates;
                if (sides == null) {
                    return null;
                }
                var side = sides[dir];
                if (side.LinkType != ItemPipelineLinkType.Pipeline) {
                    return null;
                }
                var nbr = side.LinkedPipeline;
                if (nbr == null || !nbr.Active) {
                    return null;
                }
                path.Add(nbr.Position);
                current = nbr;
            }
            return current.Position == inputPos ? path : null;
        }

        /// <summary>
        /// 推进粒子(生成与移动)
        /// </summary>
        private void UpdateParticles() {
            if (!HasValidPath) {
                particles.Clear();
                return;
            }

            spawnTimer++;
            if (spawnTimer >= BaseSpawnInterval) {
                spawnTimer = 0;

                int branchParticleCount = 0;
                for (int i = 0; i < particles.Count; i++) {
                    if (particles[i].BranchId == nextBranchIndex) {
                        branchParticleCount++;
                    }
                }
                if (branchParticleCount < MaxParticlesPerBranch) {
                    particles.Add(new FlowParticle {
                        Progress = 0f,
                        BranchId = nextBranchIndex,
                        Alpha = 1f
                    });
                }
                nextBranchIndex = (nextBranchIndex + 1) % branchPaths.Count;
            }

            for (int i = particles.Count - 1; i >= 0; i--) {
                var p = particles[i];
                if (p.BranchId >= branchPaths.Count) {
                    particles.RemoveAt(i);
                    continue;
                }
                var branch = branchPaths[p.BranchId];
                p.Progress += branch.Speed;
                if (p.Progress >= 1f) {
                    particles.RemoveAt(i);
                    continue;
                }
                particles[i] = p;
            }
        }

        /// <summary>
        /// 绘制流动动画
        /// </summary>
        public void Draw(SpriteBatch spriteBatch, Color flowColor) {
            if (!HasValidPath || particles.Count == 0) {
                return;
            }

            for (int idx = 0; idx < particles.Count; idx++) {
                var particle = particles[idx];
                if (particle.BranchId >= branchPaths.Count) {
                    continue;
                }
                var branch = branchPaths[particle.BranchId];
                int segmentCount = branch.Path.Count - 1;
                if (segmentCount <= 0) {
                    continue;
                }

                float totalProgress = particle.Progress * segmentCount;
                int pathIndex = (int)totalProgress;
                float segmentProgress = totalProgress - pathIndex;
                if (pathIndex >= segmentCount) {
                    pathIndex = segmentCount - 1;
                    segmentProgress = 1f;
                }

                Point16 currentPos = branch.Path[pathIndex];
                Point16 nextPos = branch.Path[pathIndex + 1];

                Vector2 currentWorld = currentPos.ToVector2() * 16 + new Vector2(8, 8);
                Vector2 nextWorld = nextPos.ToVector2() * 16 + new Vector2(8, 8);

                Vector2 particlePos = Vector2.Lerp(currentWorld, nextWorld, segmentProgress);
                Vector2 screenPos = particlePos - Main.screenPosition;

                Vector2 direction = nextWorld - currentWorld;
                float rotation = direction != Vector2.Zero ? direction.ToRotation() : 0f;

                ItemPipelineTP.DrawArrowTexture(spriteBatch, screenPos, rotation, flowColor * particle.Alpha * 0.4f, 0.6f);
            }
        }

        /// <summary>
        /// 清空缓存(动画停止 / 输出端被破坏)
        /// </summary>
        public void Clear() {
            branchPaths.Clear();
            particles.Clear();
            nextBranchIndex = 0;
            spawnTimer = 0;
            cachedTopologyVersion = -1;
            cachedOutputPos = Point16.NegativeOne;
        }
    }
}
