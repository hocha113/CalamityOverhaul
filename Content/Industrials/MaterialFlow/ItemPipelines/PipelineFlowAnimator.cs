using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;

namespace CalamityOverhaul.Content.Industrials.MaterialFlow.ItemPipelines
{
    /// <summary>
    /// 管道路径流动动画管理器
    /// 负责绘制从输出端点到输入端点的箭头洪流动画
    /// </summary>
    internal class PipelineFlowAnimator
    {
        /// <summary>
        /// 流动粒子数据
        /// </summary>
        private struct FlowParticle
        {
            public float Progress;//0到1，沿路径的进度
            public int PathIndex;//当前在路径中的索引
            public float Alpha;//透明度
        }

        /// <summary>
        /// 缓存的路径(管道位置列表)
        /// </summary>
        private List<Point16> cachedPath = [];

        /// <summary>
        /// 流动粒子列表
        /// </summary>
        private readonly List<FlowParticle> particles = [];

        /// <summary>
        /// 动画计时器
        /// </summary>
        private float animationTimer = 0f;

        /// <summary>
        /// 粒子生成计时器
        /// </summary>
        private int spawnTimer = 0;

        /// <summary>
        /// 路径是否有效
        /// </summary>
        public bool HasValidPath => cachedPath.Count >= 2;

        /// <summary>
        /// 粒子生成间隔(帧)
        /// </summary>
        private const int SpawnInterval = 8;

        /// <summary>
        /// 粒子移动速度
        /// </summary>
        private const float ParticleSpeed = 0.08f;

        /// <summary>
        /// 最大粒子数
        /// </summary>
        private const int MaxParticles = 20;

        /// <summary>
        /// 更新路径缓存
        /// </summary>
        public void UpdatePath(ItemPipelineTP outputEndpoint) {
            cachedPath.Clear();

            if (outputEndpoint == null || outputEndpoint.Mode != ItemPipelineMode.Output) {
                return;
            }

            //BFS构建从输出到输入的路径
            BuildPath(outputEndpoint);
        }

        /// <summary>
        /// 构建从输出端点到输入端点的路径
        /// </summary>
        private void BuildPath(ItemPipelineTP start) {
            Dictionary<Point16, Point16> cameFrom = [];
            Queue<ItemPipelineTP> queue = new();
            ItemPipelineTP inputEndpoint = null;

            queue.Enqueue(start);
            cameFrom[start.Position] = Point16.NegativeOne;

            while (queue.Count > 0) {
                var current = queue.Dequeue();

                //找到输入端点
                if (current.Mode == ItemPipelineMode.Input && current != start) {
                    inputEndpoint = current;
                    break;
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

            //没找到输入端点
            if (inputEndpoint == null) {
                return;
            }

            //回溯构建路径
            List<Point16> reversePath = [];
            Point16 pos = inputEndpoint.Position;
            while (pos != Point16.NegativeOne) {
                reversePath.Add(pos);
                pos = cameFrom.GetValueOrDefault(pos, Point16.NegativeOne);
            }

            //反转得到从输出到输入的路径
            reversePath.Reverse();
            cachedPath = reversePath;
        }

        /// <summary>
        /// 更新动画
        /// </summary>
        public void Update() {
            if (!HasValidPath) {
                particles.Clear();
                return;
            }

            animationTimer += 0.016f;

            //生成新粒子
            spawnTimer++;
            if (spawnTimer >= SpawnInterval && particles.Count < MaxParticles) {
                spawnTimer = 0;
                particles.Add(new FlowParticle {
                    Progress = 0f,
                    PathIndex = 0,
                    Alpha = 1f
                });
            }

            //更新粒子
            for (int i = particles.Count - 1; i >= 0; i--) {
                var p = particles[i];
                p.Progress += ParticleSpeed;

                //到达当前管道末端，移动到下一个
                if (p.Progress >= 1f) {
                    p.Progress = 0f;
                    p.PathIndex++;

                    //到达终点，移除粒子
                    if (p.PathIndex >= cachedPath.Count - 1) {
                        particles.RemoveAt(i);
                        continue;
                    }
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
                if (particle.PathIndex >= cachedPath.Count - 1) {
                    continue;
                }

                //获取当前和下一个管道位置
                Point16 currentPos = cachedPath[particle.PathIndex];
                Point16 nextPos = cachedPath[particle.PathIndex + 1];

                //计算世界坐标
                Vector2 currentWorld = currentPos.ToVector2() * 16 + new Vector2(8, 8);
                Vector2 nextWorld = nextPos.ToVector2() * 16 + new Vector2(8, 8);

                //插值计算粒子位置
                Vector2 particlePos = Vector2.Lerp(currentWorld, nextWorld, particle.Progress);
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
            cachedPath.Clear();
            particles.Clear();
        }
    }
}
