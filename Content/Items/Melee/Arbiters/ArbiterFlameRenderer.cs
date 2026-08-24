using CalamityOverhaul.Common;
using InnoVault.RenderHandles;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee.Arbiters
{
    /// <summary>
    /// 断罪师贴地狱火条带渲染:收集屏内 <see cref="ArbiterGroundFire"/> 合并为连续燃段,
    /// 逐列采样地面轮廓构建世界空间 TriangleStrip,一次 Immediate 批交给
    /// ArbiterHellfire.fx TechGroundFire。火蛇头与冲击点注入前沿亮度;
    /// 显现仪式经 <see cref="PushPoint"/>/<see cref="PushFront"/> 喂纯视觉燃点。
    /// 着色器缺失时此层静默,火坑自身回退旧版粒子堆叠
    /// </summary>
    internal sealed class ArbiterFlameRenderer : RenderHandle
    {
        /// <summary>地上画布高(px),与 fx 的 uCanvasH*uGroundV 同源</summary>
        internal const float CanvasAbove = 96f;
        /// <summary>地下画布高(px),焦炭带</summary>
        internal const float RootDepth = 16f;
        internal const float CanvasH = CanvasAbove + RootDepth;
        internal const float GroundV = CanvasAbove / CanvasH;
        //列采样步长
        private const float ColumnStep = 12f;
        //火坑横向合段间隙(火坑原生间距 22px)
        private const float MergeGapX = 52f;
        //相邻燃点地面落差过此值断段(火蛇 MaxStepUp=3 tile)
        private const float CliffBreak = 56f;
        //端部包络宽度(A 通道撕散)
        private const float EndFadePx = 34f;
        //前沿光半径
        private const float FrontRadius = 90f;
        //屏幕外余量
        private const float ScreenPad = 220f;

        /// <summary>火体着色器是否可用(消费端与火坑回退共用的判据)</summary>
        internal static bool ShaderReady => !Main.dedServ && EffectLoader.ArbiterHellfire?.Value != null;

        private struct FirePoint
        {
            public float X;
            public float GroundY;
            public float Env;
            public float Scale;
        }

        //帧内收集容器(主线程绘制期独占,复用防 GC)
        private static readonly List<FirePoint> points = new(96);
        private static readonly List<(float x, float strength)> fronts = new(12);
        //外部推入的纯视觉燃点/前沿(显现仪式、冲击拔高),画完即清
        private static readonly List<FirePoint> pushedPoints = new(16);
        private static readonly List<(float x, float strength)> pushedFronts = new(8);
        private static VertexPositionColorTexture[] vertexBuf = new VertexPositionColorTexture[256];

        /// <summary>推入一帧纯视觉燃点(无判定;显现仪式落地火用),每帧要重推</summary>
        internal static void PushPoint(float x, float groundY, float env, float scale) {
            if (Main.dedServ) {
                return;
            }
            pushedPoints.Add(new FirePoint { X = x, GroundY = groundY, Env = MathHelper.Clamp(env, 0f, 1f), Scale = scale });
        }

        /// <summary>推入一帧前沿亮点(冲击点/演出拍),每帧要重推</summary>
        internal static void PushFront(float x, float strength) {
            if (Main.dedServ) {
                return;
            }
            pushedFronts.Add((x, MathHelper.Clamp(strength, 0f, 1f)));
        }

        public override void EndEntityDraw(SpriteBatch spriteBatch, Main main
            , GraphicsDevice graphicsDevice, RenderTarget2D screenSwap) {
            if (Main.gameMenu || Main.dedServ) {
                pushedPoints.Clear();
                pushedFronts.Clear();
                return;
            }

            Effect fx = EffectLoader.ArbiterHellfire?.Value;
            if (fx == null) {
                pushedPoints.Clear();
                pushedFronts.Clear();
                return;
            }

            CollectSources();
            if (points.Count == 0) {
                return;
            }

            //按 X 排序后近位合并(同点取最强)
            points.Sort(static (a, b) => a.X.CompareTo(b.X));
            MergeClosePoints();

            //设备状态
            BlendState origBlend = graphicsDevice.BlendState;
            RasterizerState origRaster = graphicsDevice.RasterizerState;
            graphicsDevice.BlendState = BlendState.AlphaBlend;
            graphicsDevice.RasterizerState = RasterizerState.CullNone;
            graphicsDevice.Textures[1] = CWRAsset.PerlinNoise.Value;
            graphicsDevice.SamplerStates[1] = SamplerState.LinearWrap;

            //GetTransfromMatrix 已含世界→屏幕平移,顶点直接给世界坐标
            fx.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            fx.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            fx.Parameters["uCanvasH"]?.SetValue(CanvasH);
            fx.Parameters["uGroundV"]?.SetValue(GroundV);
            fx.CurrentTechnique = fx.Techniques["TechGroundFire"];

            //切段绘制:横向间隙或地面落差断开
            int segStart = 0;
            for (int i = 1; i <= points.Count; i++) {
                bool split = i == points.Count
                    || points[i].X - points[i - 1].X > MergeGapX
                    || Math.Abs(points[i].GroundY - points[i - 1].GroundY) > CliffBreak;
                if (!split) {
                    continue;
                }
                DrawSegment(graphicsDevice, fx, segStart, i - 1);
                segStart = i;
            }

            graphicsDevice.BlendState = origBlend;
            graphicsDevice.RasterizerState = origRaster;
        }

        /// <summary>收集屏内火坑/火蛇头/外部推入点</summary>
        private void CollectSources() {
            points.Clear();
            fronts.Clear();

            float xMin = Main.screenPosition.X - ScreenPad;
            float xMax = Main.screenPosition.X + Main.screenWidth + ScreenPad;

            int groundFireType = ModContent.ProjectileType<ArbiterGroundFire>();
            int snakeType = ModContent.ProjectileType<ArbiterFireSnake>();

            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.type == groundFireType) {
                    if (proj.Center.X < xMin || proj.Center.X > xMax
                        || proj.ModProjectile is not ArbiterGroundFire fire || !fire.RenderReady) {
                        continue;
                    }
                    points.Add(new FirePoint {
                        X = proj.Center.X,
                        GroundY = fire.RenderGroundY,
                        Env = fire.RenderEnvelope,
                        Scale = fire.RenderScale
                    });
                }
                else if (proj.type == snakeType) {
                    if (proj.Center.X < xMin || proj.Center.X > xMax
                        || proj.ModProjectile is not ArbiterFireSnake snake || !snake.RenderMoving) {
                        continue;
                    }
                    //行进中的蛇头=燃沿前锋
                    fronts.Add((proj.Center.X, 1f));
                }
            }

            foreach (FirePoint p in pushedPoints) {
                if (p.X >= xMin && p.X <= xMax) {
                    points.Add(p);
                }
            }
            foreach ((float x, float strength) in pushedFronts) {
                if (x >= xMin && x <= xMax) {
                    fronts.Add((x, strength));
                }
            }
            pushedPoints.Clear();
            pushedFronts.Clear();
        }

        /// <summary>近位燃点合并(10px 内取最强),防同点叠画重曝</summary>
        private static void MergeClosePoints() {
            int write = 0;
            for (int i = 1; i < points.Count; i++) {
                FirePoint cur = points[i];
                FirePoint kept = points[write];
                if (cur.X - kept.X < 10f && Math.Abs(cur.GroundY - kept.GroundY) < 20f) {
                    if (cur.Env * cur.Scale > kept.Env * kept.Scale) {
                        points[write] = cur;
                    }
                    continue;
                }
                write++;
                points[write] = cur;
            }
            points.RemoveRange(write + 1, points.Count - write - 1);
        }

        /// <summary>画一个连续燃段:逐列贴地形建条带</summary>
        private static void DrawSegment(GraphicsDevice device, Effect fx, int first, int last) {
            float segMinX = points[first].X - EndFadePx;
            float segMaxX = points[last].X + EndFadePx;
            int columns = (int)((segMaxX - segMinX) / ColumnStep) + 2;
            if (columns < 2) {
                return;
            }

            int vertCount = columns * 2;
            if (vertexBuf.Length < vertCount) {
                vertexBuf = new VertexPositionColorTexture[vertCount + 64];
            }

            int cursor = first;
            for (int c = 0; c < columns; c++) {
                float x = Math.Min(segMinX + c * ColumnStep, segMaxX);

                //推进游标到 x 的左邻燃点
                while (cursor < last && points[cursor + 1].X <= x) {
                    cursor++;
                }

                //包络/规模/地面基准:燃点间线性插值,端外随距离衰减
                float env;
                float scale;
                float groundHint;
                if (x <= points[first].X) {
                    float fall = 1f - MathHelper.Clamp((points[first].X - x) / EndFadePx, 0f, 1f);
                    env = points[first].Env * fall;
                    scale = points[first].Scale;
                    groundHint = points[first].GroundY;
                }
                else if (x >= points[last].X) {
                    float fall = 1f - MathHelper.Clamp((x - points[last].X) / EndFadePx, 0f, 1f);
                    env = points[last].Env * fall;
                    scale = points[last].Scale;
                    groundHint = points[last].GroundY;
                }
                else {
                    FirePoint a = points[cursor];
                    FirePoint b = points[Math.Min(cursor + 1, last)];
                    float t = b.X > a.X ? MathHelper.Clamp((x - a.X) / (b.X - a.X), 0f, 1f) : 0f;
                    env = MathHelper.Lerp(a.Env, b.Env, t);
                    scale = MathHelper.Lerp(a.Scale, b.Scale, t);
                    groundHint = MathHelper.Lerp(a.GroundY, b.GroundY, t);
                }

                float groundY = SampleGroundY(x, groundHint);

                //端部包络:段两端撕散收口(fx 里抬阈值,不做原地淡出)
                float endEnv = Math.Min(
                    MathHelper.Clamp((x - segMinX) / EndFadePx, 0f, 1f),
                    MathHelper.Clamp((segMaxX - x) / EndFadePx, 0f, 1f));

                //前沿亮度:蛇头/冲击点附近隆起
                float front = 0f;
                for (int f = 0; f < fronts.Count; f++) {
                    float d = Math.Abs(x - fronts[f].x);
                    if (d < FrontRadius) {
                        float k = 1f - d / FrontRadius;
                        front = Math.Max(front, fronts[f].strength * k * k);
                    }
                }

                //顶点色契约:R=生命包络 G=火高系数(scale/2) B=前沿 A=端部包络
                Color data = new(env, MathHelper.Clamp(scale, 0f, 2f) * 0.5f, front, endEnv);
                float top = groundY - CanvasAbove;
                float bottom = groundY + RootDepth;
                vertexBuf[c * 2] = new VertexPositionColorTexture(
                    new Vector3(x, top, 0f), data, new Vector2(x, 0f));
                vertexBuf[c * 2 + 1] = new VertexPositionColorTexture(
                    new Vector3(x, bottom, 0f), data, new Vector2(x, 1f));
            }

            foreach (EffectPass pass in fx.CurrentTechnique.Passes) {
                pass.Apply();
                device.DrawUserPrimitives(PrimitiveType.TriangleStrip, vertexBuf, 0, vertCount - 2);
            }
        }

        /// <summary>列上以 hint 为起点吸附真实地面顶(处理半砖;斜坡取整格顶,根床厚度吸收误差)</summary>
        private static float SampleGroundY(float x, float hintY) {
            int tx = (int)(x / 16f);
            int tyStart = (int)(hintY / 16f) - 2;
            for (int dy = 0; dy < 6; dy++) {
                int ty = tyStart + dy;
                if (!WorldGen.InWorld(tx, ty)) {
                    break;
                }
                Tile t = Framing.GetTileSafely(tx, ty);
                if (t.HasTile && Main.tileSolid[t.TileType] && !Main.tileSolidTop[t.TileType]) {
                    return ty * 16f + (t.IsHalfBlock ? 8f : 0f);
                }
            }
            return hintY;
        }
    }
}
