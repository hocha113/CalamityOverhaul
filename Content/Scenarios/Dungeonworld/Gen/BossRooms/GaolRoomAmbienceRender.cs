using CalamityOverhaul.Common;
using CalamityOverhaul.Content.PRTTypes;
using CalamityOverhaul.Content.Scenarios.Dungeonworld.NPCs;
using InnoVault.PRT;
using InnoVault.RenderHandles;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using Terraria;

namespace CalamityOverhaul.Content.Scenarios.Dungeonworld.Gen.BossRooms
{
    /// <summary>
    /// 禁室房内氛围层（B1 独占，Weight 1.640 频段）。逐房按
    /// <see cref="GaolBossRoomWatcher.Rooms"/> 镜像出演出：<br/>
    /// 物块层后：玫瑰窗/尖拱窗透光辉（TechWindow，Armed/Sealed 时窗后有暗影游走）
    /// + 窗至祭坛的落光柱（TechShaft）；<br/>
    /// 玩家层后：封门能量栅（TechGrate，随 Sealed 相自顶向下织成、解封回退）。<br/>
    /// 所有相位参数经 <see cref="GaolRoomVisualSystem"/> 的插值状态过渡，不跳变。
    /// 与全局雾（Fog\，冻结只读）不同层不同 pass，互不触碰。
    /// </summary>
    internal class GaolRoomAmbienceRender : RenderHandle
    {
        public override float Weight => 1.64f;

        public override bool CanLoad() => DeepGaolWraithGate.Enabled;

        /// <summary>封门砖据此决定跳过自绘（能量栅由本层统一出）</summary>
        internal static bool GrateShaderReady
            => !Main.dedServ && DeepGaolWraithGate.Enabled && EffectLoader.GaolRoom?.Value != null;

        //囚粉三色（数值对齐 DeepGaolWraith 的主题色，本地副本避免耦合 A1 在改文件）
        private static readonly Vector3 ColGlow = new(0.925f, 0.455f, 0.612f);
        private static readonly Vector3 ColDeep = new(0.463f, 0.133f, 0.259f);
        private static readonly Vector3 ColHot = new(1.0f, 0.96f, 0.98f);

        //房内构图常量（tile 偏移，与 GaolBossRoom 字符画对位）
        private static readonly Vector2 RoseCenter = new(31f, 16f);
        private static readonly Vector2 LancetLeft = new(18.5f, 28.5f);
        private static readonly Vector2 LancetRight = new(43.5f, 28.5f);
        private const float RoseCanvas = 260f;
        private const float LancetCanvas = 96f;
        private const float ShaftTopRow = 21f;
        private const float ShaftBottomRow = 35f;
        private const float ShaftWidth = 190f;

        private static readonly VertexPositionColorTexture[] quad = new VertexPositionColorTexture[4];
        private static readonly Point[] doorOffsets = [GaolBossRoom.LeftDoorOffset, GaolBossRoom.RightDoorOffset];

        public override void DrawAfterTiles(SpriteBatch spriteBatch, GraphicsDevice graphicsDevice,
            RenderTarget2D screenSwap) {
            if (Main.gameMenu || GaolBossRoomWatcher.Rooms.Count == 0) {
                return;
            }
            Effect fx = EffectLoader.GaolRoom?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (fx == null || noise == null) {
                return;
            }
            foreach (GaolBossRoomWatcher.RoomState room in GaolBossRoomWatcher.Rooms) {
                if (!RoomOnScreen(room.Origin)) {
                    continue;
                }
                GaolRoomVisualSystem.VisState vis = GaolRoomVisualSystem.GetState(room);
                if (vis.Glow <= 0.02f) {
                    continue;
                }
                Vector2 basePos = room.Origin.ToVector2() * 16f;
                float seed = Seed(room.Origin);

                //玫瑰窗：主辉 + 窗后暗影
                DrawWindow(fx, noise, basePos + RoseCenter * 16f, RoseCanvas,
                    vis.Glow, vis.Figure, seed);
                //尖拱窄窗：小半径辉，无暗影（东西只在大窗后走）
                DrawWindow(fx, noise, basePos + LancetLeft * 16f, LancetCanvas,
                    vis.Glow * 0.55f, 0f, seed + 0.31f);
                DrawWindow(fx, noise, basePos + LancetRight * 16f, LancetCanvas,
                    vis.Glow * 0.55f, 0f, seed + 0.57f);

                //落光柱：玫瑰窗底沿垂到祭坛背景带
                float shaftX = basePos.X + RoseCenter.X * 16f;
                DrawShaft(fx, noise,
                    new Vector2(shaftX, basePos.Y + ShaftTopRow * 16f),
                    new Vector2(shaftX, basePos.Y + ShaftBottomRow * 16f),
                    ShaftWidth, vis.Glow * 0.85f, seed);
            }
        }

        public override void DrawAfterPlayers(SpriteBatch spriteBatch, GraphicsDevice graphicsDevice,
            RenderTarget2D screenSwap) {
            if (Main.gameMenu || GaolBossRoomWatcher.Rooms.Count == 0) {
                return;
            }
            Effect fx = EffectLoader.GaolRoom?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (fx == null || noise == null) {
                return;
            }
            foreach (GaolBossRoomWatcher.RoomState room in GaolBossRoomWatcher.Rooms) {
                if (!RoomOnScreen(room.Origin)) {
                    continue;
                }
                GaolRoomVisualSystem.VisState vis = GaolRoomVisualSystem.GetState(room);
                if (vis.Reveal <= 0.01f) {
                    continue;
                }
                Vector2 basePos = room.Origin.ToVector2() * 16f;
                float seed = Seed(room.Origin);
                foreach (Point offset in doorOffsets) {
                    Vector2 tl = basePos + offset.ToVector2() * 16f;
                    DrawGrate(fx, noise, tl, new Vector2(48f, GaolBossRoom.DoorHeight * 16f),
                        vis.Reveal, vis.Pulse, seed + offset.X * 0.013f);
                }
            }
        }

        //==================== 三种 quad ====================

        private static void DrawWindow(Effect fx, Texture2D noise, Vector2 center, float canvas,
            float glow, float figure, float seed) {
            fx.CurrentTechnique = fx.Techniques["TechWindow"];
            SetCommon(fx, seed);
            fx.Parameters["uGlow"]?.SetValue(glow);
            fx.Parameters["uFigure"]?.SetValue(figure);
            Vector2 half = new(canvas * 0.5f);
            DrawQuad(fx, noise, center - half, center + half);
        }

        private static void DrawShaft(Effect fx, Texture2D noise, Vector2 top, Vector2 bottom,
            float width, float strength, float seed) {
            fx.CurrentTechnique = fx.Techniques["TechShaft"];
            SetCommon(fx, seed);
            fx.Parameters["uStrength"]?.SetValue(strength);
            Vector2 half = new(width * 0.5f, 0f);
            DrawQuadCorners(fx, noise,
                top - half, top + half, bottom - half, bottom + half);
        }

        private static void DrawGrate(Effect fx, Texture2D noise, Vector2 topLeft, Vector2 size,
            float reveal, float pulse, float seed) {
            fx.CurrentTechnique = fx.Techniques["TechGrate"];
            SetCommon(fx, seed);
            fx.Parameters["uReveal"]?.SetValue(reveal);
            fx.Parameters["uPulse"]?.SetValue(pulse);
            DrawQuad(fx, noise, topLeft, topLeft + size);
        }

        private static void SetCommon(Effect fx, float seed) {
            fx.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            fx.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            fx.Parameters["uSeed"]?.SetValue(seed % 1f);
            fx.Parameters["uColGlow"]?.SetValue(ColGlow);
            fx.Parameters["uColDeep"]?.SetValue(ColDeep);
            fx.Parameters["uColHot"]?.SetValue(ColHot);
        }

        private static void DrawQuad(Effect fx, Texture2D noise, Vector2 tl, Vector2 br)
            => DrawQuadCorners(fx, noise, tl, new Vector2(br.X, tl.Y), new Vector2(tl.X, br.Y), br);

        /// <summary>世界坐标四角 quad（uv: tl=00 tr=10 bl=01 br=11），预乘 AlphaBlend</summary>
        private static void DrawQuadCorners(Effect fx, Texture2D noise,
            Vector2 tl, Vector2 tr, Vector2 bl, Vector2 br) {
            quad[0] = new VertexPositionColorTexture(new Vector3(tl, 0f), Color.White, new Vector2(0f, 0f));
            quad[1] = new VertexPositionColorTexture(new Vector3(bl, 0f), Color.White, new Vector2(0f, 1f));
            quad[2] = new VertexPositionColorTexture(new Vector3(tr, 0f), Color.White, new Vector2(1f, 0f));
            quad[3] = new VertexPositionColorTexture(new Vector3(br, 0f), Color.White, new Vector2(1f, 1f));

            GraphicsDevice device = Main.graphics.GraphicsDevice;
            BlendState origBlend = device.BlendState;
            RasterizerState origRaster = device.RasterizerState;
            device.BlendState = BlendState.AlphaBlend;
            device.RasterizerState = RasterizerState.CullNone;
            device.Textures[1] = noise;
            device.SamplerStates[1] = SamplerState.LinearWrap;
            foreach (EffectPass pass in fx.CurrentTechnique.Passes) {
                pass.Apply();
                device.DrawUserPrimitives(PrimitiveType.TriangleStrip, quad, 0, 2);
            }
            device.Textures[1] = null;
            device.BlendState = origBlend;
            device.RasterizerState = origRaster;
        }

        //==================== 工具 ====================

        private static bool RoomOnScreen(Point origin) {
            Rectangle world = new(origin.X * 16, origin.Y * 16,
                GaolBossRoom.Width * 16, GaolBossRoom.Height * 16);
            Rectangle view = new((int)Main.screenPosition.X - 200, (int)Main.screenPosition.Y - 200,
                Main.screenWidth + 400, Main.screenHeight + 400);
            return world.Intersects(view);
        }

        private static float Seed(Point origin)
            => (origin.X * 0.137f + origin.Y * 0.291f) % 1f;
    }

    /// <summary>
    /// 禁室演出状态机（客户端）：相位参数插值（辉度/暗影/栅织成/开战脉冲）
    /// 与狱火余烬 PRT 定率播撒。服务器与单人共用巡检结果，客户端吃网络镜像。
    /// </summary>
    internal class GaolRoomVisualSystem : GaolModSystem
    {
        internal sealed class VisState
        {
            internal float Glow;
            internal float Figure;
            internal float Reveal;
            internal float Pulse;
            internal GaolRoomPhase LastPhase;
            internal int EmberTimer;
        }

        private static readonly Dictionary<Point, VisState> states = [];

        internal static VisState GetState(GaolBossRoomWatcher.RoomState room) {
            if (!states.TryGetValue(room.Origin, out VisState vis)) {
                vis = new VisState { Glow = TargetGlow(room.Phase), LastPhase = room.Phase };
                states[room.Origin] = vis;
            }
            return vis;
        }

        public override void ClearWorld() => states.Clear();

        public override void PostUpdateDusts() {
            if (Main.dedServ || GaolBossRoomWatcher.Rooms.Count == 0) {
                return;
            }
            foreach (GaolBossRoomWatcher.RoomState room in GaolBossRoomWatcher.Rooms) {
                VisState vis = GetState(room);

                //相位切换沿：开战一记栅面脉冲
                if (vis.LastPhase != room.Phase) {
                    if (room.Phase == GaolRoomPhase.Sealed) {
                        vis.Pulse = 1f;
                    }
                    vis.LastPhase = room.Phase;
                }

                //参数插值：辉度慢渡、栅织成约 1 秒、脉冲指数衰减
                vis.Glow = MathHelper.Lerp(vis.Glow, TargetGlow(room.Phase), 0.03f);
                vis.Figure = MathHelper.Lerp(vis.Figure,
                    room.Phase is GaolRoomPhase.Armed or GaolRoomPhase.Sealed ? 1f : 0f, 0.02f);
                vis.Reveal = MathHelper.Lerp(vis.Reveal,
                    room.Phase == GaolRoomPhase.Sealed ? 1f : 0f,
                    room.Phase == GaolRoomPhase.Sealed ? 0.045f : 0.07f);
                vis.Pulse *= 0.93f;

                SpawnEmbers(room, vis);
            }
        }

        private static float TargetGlow(GaolRoomPhase phase) => phase switch {
            GaolRoomPhase.Sealed => 1.15f,
            GaolRoomPhase.Cleared => 0.25f,
            GaolRoomPhase.Armed => 1f,
            _ => 0.85f,
        };

        /// <summary>狱火余烬播撒：房态定率，玩家在场才出（纯演出，Main.rand 客户端自决）</summary>
        private static void SpawnEmbers(GaolBossRoomWatcher.RoomState room, VisState vis) {
            Vector2 altar = GaolBossRoom.AltarWorldPos(room.Origin);
            if (Vector2.Distance(Main.LocalPlayer.Center, altar) > 1800f) {
                return;
            }
            int interval = room.Phase switch {
                GaolRoomPhase.Sealed => 6,
                GaolRoomPhase.Cleared => 50,
                GaolRoomPhase.Armed => 14,
                _ => 24,
            };
            if (++vis.EmberTimer < interval) {
                return;
            }
            vis.EmberTimer = 0;

            //六成散布在内膛下半场，四成聚在祭坛口（火从祭坛漏出来）
            Vector2 basePos = room.Origin.ToVector2() * 16f;
            Vector2 pos = Main.rand.NextFloat() < 0.6f
                ? basePos + new Vector2(Main.rand.NextFloat(4f, 58f), Main.rand.NextFloat(20f, 37f)) * 16f
                : altar + new Vector2(Main.rand.NextFloat(-60f, 60f), Main.rand.NextFloat(-10f, 40f));

            var ember = PRTLoader.NewParticle<PRT_GaolRoomEmber>(pos,
                new Vector2(Main.rand.NextFloat(-0.2f, 0.2f), Main.rand.NextFloat(-0.35f, -0.1f)),
                new Color(236, 116, 156), Main.rand.NextFloat(0.35f, 0.7f));
            if (ember != null) {
                ember.Lifetime = Main.rand.Next(110, 190);
                ember.Sway = Main.rand.NextFloat();
            }
        }
    }
}
