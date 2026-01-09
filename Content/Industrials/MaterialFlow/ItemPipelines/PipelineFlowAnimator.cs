using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;

namespace CalamityOverhaul.Content.Industrials.MaterialFlow.ItemPipelines
{
    /// <summary>
    /// 管道路径流动动画管理器
    /// 负责绘制从输出端点到所有输入端点的箭头洪流动画
    /// 支持分叉路径
    /// </summary>
    internal class PipelineFlowAnimator
    {
        /// <summary>
        /// 流动粒子数据
        /// </summary>
        private struct FlowParticle
        {
            public float Progress;//0到1，沿整个路径的总进度
            public int BranchId;//所属分支ID
            public float Alpha;//透明度
        }

        /// <summary>
        /// 分支路径数据
        /// </summary>
        private struct BranchPath
        {
            public List<Point16> Path;//路径点列表
            public float Speed;//该分支的粒子速度(动态计算)
        }

        /// <summary>
        /// 所有分支路径(从输出到各个输入的路径)
        /// </summary>
        private List<BranchPath> branchPaths = [];

        /// <summary>
        /// 流动粒子列表
        /// </summary>
        private readonly List<FlowParticle> particles = [];

        /// <summary>
        /// 粒子生成计时器
        /// </summary>
        private int spawnTimer = 0;

        /// <summary>
        /// 下一个生成的分支索引(轮流生成)
        /// </summary>
        private int nextBranchIndex = 0;

        /// <summary>
        /// 路径是否有效
        /// </summary>
        public bool HasValidPath => branchPaths.Count > 0;

        /// <summary>
        /// 路径刷新间隔(帧)，与ItemPipelineTP.PathUpdateInterval保持一致
        /// </summary>
        private const int PathUpdateInterval = 120;

        /// <summary>
        /// 粒子完成动画的目标时间(帧)，留出一些缓冲时间
        /// </summary>
        private const int AnimationCycleFrames = 100;

        /// <summary>
        /// 基础粒子生成间隔(帧)
        /// </summary>
        private const int BaseSpawnInterval = 8;

        /// <summary>
        /// 每个分支最大粒子数
        /// </summary>
        private const int MaxParticlesPerBranch = 10;

        /// <summary>
        /// 更新路径缓存
        /// </summary>
        public void UpdatePath(ItemPipelineTP outputEndpoint) {
            branchPaths.Clear();
            particles.Clear();

            if (outputEndpoint == null || outputEndpoint.Mode != ItemPipelineMode.Output) {
                return;
            }

            //BFS构建从输出到所有输入端点的路径
            BuildAllPaths(outputEndpoint);
        }

        /// <summary>
        /// 构建从输出端点到所有输入端点的路径
        /// </summary>
        private void BuildAllPaths(ItemPipelineTP start) {
            //使用BFS找到所有输入端点并记录路径
            Dictionary<Point16, Point16> cameFrom = [];
            Queue<ItemPipelineTP> queue = new();
            List<ItemPipelineTP> inputEndpoints = [];

            queue.Enqueue(start);
            cameFrom[start.Position] = Point16.NegativeOne;

            while (queue.Count > 0) {
                var current = queue.Dequeue();

                //找到输入端点
                if (current.Mode == ItemPipelineMode.Input && current != start) {
                    inputEndpoints.Add(current);
                    //继续搜索其他输入端点
                }

                foreach (var side in current.SideStates) {
                    if (side.LinkType == ItemPipelineLinkType.Pipeline && side.LinkedPipeline != null) {
                        if (!cameFrom.ContainsKey(side.LinkedPipeline.Position)) {
                            cameFrom[side.LinkedPipeline.Position] = current.Position;
                            queue.Enqueue(side.LinkedPipeline);
                        }
                    }
                }
            }

            //为每个输入端点回溯构建路径
            foreach (var inputEndpoint in inputEndpoints) {
                List<Point16> reversePath = [];
                Point16 pos = inputEndpoint.Position;
                while (pos != Point16.NegativeOne) {
                    reversePath.Add(pos);
                    if (!cameFrom.TryGetValue(pos, out Point16 nextPos)) {
                        break;
                    }
                    pos = nextPos;
                }

                if (reversePath.Count >= 2) {
                    reversePath.Reverse();
                    //动态计算速度：确保粒子能在刷新间隔内完成整个路径
                    //路径段数 = Path.Count - 1
                    int segmentCount = reversePath.Count - 1;
                    //速度 = 总进度(1.0) / 可用帧数
                    float speed = 1f / AnimationCycleFrames;
                    //但速度不能太慢，设置最小速度
                    speed = Math.Max(speed, 0.01f);
                    branchPaths.Add(new BranchPath { Path = reversePath, Speed = speed });
                }
            }
        }

        /// <summary>
        /// 更新动画
        /// </summary>
        public void Update() {
            if (!HasValidPath) {
                particles.Clear();
                return;
            }

            //生成新粒子(轮流在各分支生成)
            spawnTimer++;
            if (spawnTimer >= BaseSpawnInterval) {
                spawnTimer = 0;

                //计算当前分支的粒子数
                int branchParticleCount = 0;
                foreach (var p in particles) {
                    if (p.BranchId == nextBranchIndex) {
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

                //切换到下一个分支
                nextBranchIndex = (nextBranchIndex + 1) % branchPaths.Count;
            }

            //更新粒子
            for (int i = particles.Count - 1; i >= 0; i--) {
                var p = particles[i];

                //检查分支是否仍然有效
                if (p.BranchId >= branchPaths.Count) {
                    particles.RemoveAt(i);
                    continue;
                }

                var branch = branchPaths[p.BranchId];
                //使用该分支的动态速度
                p.Progress += branch.Speed;

                //到达终点，移除粒子
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

            foreach (var particle in particles) {
                if (particle.BranchId >= branchPaths.Count) {
                    continue;
                }

                var branch = branchPaths[particle.BranchId];
                int segmentCount = branch.Path.Count - 1;
                if (segmentCount <= 0) {
                    continue;
                }

                //将总进度(0-1)映射到具体的路径段
                float totalProgress = particle.Progress * segmentCount;
                int pathIndex = (int)totalProgress;
                float segmentProgress = totalProgress - pathIndex;

                //边界检查
                if (pathIndex >= segmentCount) {
                    pathIndex = segmentCount - 1;
                    segmentProgress = 1f;
                }

                //获取当前和下一个管道位置
                Point16 currentPos = branch.Path[pathIndex];
                Point16 nextPos = branch.Path[pathIndex + 1];

                //计算世界坐标
                Vector2 currentWorld = currentPos.ToVector2() * 16 + new Vector2(8, 8);
                Vector2 nextWorld = nextPos.ToVector2() * 16 + new Vector2(8, 8);

                //插值计算粒子位置
                Vector2 particlePos = Vector2.Lerp(currentWorld, nextWorld, segmentProgress);
                Vector2 screenPos = particlePos - Main.screenPosition;

                //计算方向用于绘制小箭头
                Vector2 direction = nextWorld - currentWorld;
                float rotation = 0f;
                if (direction != Vector2.Zero) {
                    rotation = direction.ToRotation();
                }

                //使用纹理绘制流动箭头粒子
                ItemPipelineTP.DrawArrowTexture(spriteBatch, screenPos, rotation, flowColor * particle.Alpha * 0.8f, 0.6f);
            }
        }

        /// <summary>
        /// 清除缓存
        /// </summary>
        public void Clear() {
            branchPaths.Clear();
            particles.Clear();
            nextBranchIndex = 0;
        }
    }
}
