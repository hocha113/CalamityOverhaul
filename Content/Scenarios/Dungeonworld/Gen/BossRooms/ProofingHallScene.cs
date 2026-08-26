using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Melee.DawnshatterAzures;
using CalamityOverhaul.Content.PRTTypes;
using CalamityOverhaul.Content.Scenarios.Dungeonworld.NPCs;
using InnoVault.PRT;
using InnoVault.RenderHandles;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Dungeonworld.Gen.BossRooms
{
    /// <summary>
    /// 验收堂房内视觉复合层（纯表现，零裁决；权威裁决全在看守与 A3 监工侧）。
    /// 职责：三条浇注坑的常驻熔浴（ProofingHallMelt 着色器，房态驱动热度）、
    /// 天轨铁梁 CPU 绘制（断轨后现真缺口+撕口余温）、检修位余次灯的灯罩壳
    /// （A3 在 NPC 层画灯珠，这里补壳框免得灯珠悬空）、炉膛余烬上飘与熔池光照、
    /// EndCapture 复用 ThermalHeatHaze 给熔池与监工加热浪扭曲。
    /// 房间来源：生成/钥匙侧 NoteRoom 直登；联机客户端不跑 Place，靠观察携带
    /// 房间坐标的吊臂/监工 NPC 补登（同一来源=A3 的 SendExtraAI，天然同步）。
    /// 对冲活塞演出归 A3（FoundryOverseer.PistonPose 全套），此处不重画。
    /// </summary>
    internal sealed class ProofingHallScene : RenderHandle
    {
        /// <summary>B3 频段 1.660–1.669 取 1.664（邻位 1.06=热力热浪，1.17=地牢环境）</summary>
        public override float Weight => 1.664f;

        public override bool CanLoad() => FoundryOverseerGate.Enabled;

        //==================== 房态视图（客户端表现状态，非权威）====================

        private sealed class RoomView
        {
            internal Point Origin;
            /// <summary>平滑热度：0=冷炉结壳（清剿后/未布防）0.45=蛰伏文火 1=战斗沸腾</summary>
            internal float Heat = 0.45f;
            /// <summary>已见断轨（轨梁画缺口）；吊臂复位=轨修好，随观察复位</summary>
            internal bool RailBroken;
            internal int EmberTick;
        }

        private static readonly List<RoomView> views = [];

        /// <summary>登记房间（生成期/钥匙侧调用；客户端由 NPC 观察补登）</summary>
        internal static void NoteRoom(Point origin) {
            foreach (RoomView view in views) {
                if (view.Origin == origin) {
                    return;
                }
            }
            views.Add(new RoomView { Origin = origin });
        }

        internal static void ClearViews() => views.Clear();

        /// <summary>观察聚合：本帧各房的目标热度与断轨事实（-1=无 NPC 在房）</summary>
        private static readonly Dictionary<Point, (float heat, bool broken, bool rig)> observed = [];

        private const float ViewCullDistance = 3200f;

        //==================== 逻辑更新：观察 NPC → 定房态 → 撒余烬 + 打光 ====================

        public override void UpdateBySystem(int index) {
            if (Main.dedServ || Main.gameMenu || Main.gamePaused) {
                return;
            }
            //观察即补登：联机客户端不跑 Place，首见携带房间坐标的 NPC 时 views 由此长出
            Observe();
            if (views.Count == 0) {
                return;
            }

            Player player = Main.LocalPlayer;
            foreach (RoomView view in views) {
                float target = 0.05f;
                if (observed.TryGetValue(view.Origin, out (float heat, bool broken, bool rig) fact)) {
                    target = fact.heat;
                    if (fact.broken) {
                        view.RailBroken = true;
                    }
                    if (fact.rig) {
                        //吊臂在位=轨已修好（看守复位事务的表现镜像）
                        view.RailBroken = false;
                    }
                }
                //热惯性：铁水升温快降温慢
                float rate = target > view.Heat ? 0.03f : 0.006f;
                view.Heat += (target - view.Heat) * rate;

                if (player == null || !player.active
                    || Vector2.Distance(player.Center, ProofingHallWatcher.RoomCenterWorld(view.Origin))
                        > ViewCullDistance) {
                    continue;
                }
                LightAndEmbers(view);
            }
        }

        private static void Observe() {
            observed.Clear();
            int rigType = ModContent.NPCType<OverseerDormantRig>();
            int bossType = ModContent.NPCType<FoundryOverseer>();
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (!npc.active) {
                    continue;
                }
                if (npc.type == rigType && npc.ModNPC is OverseerDormantRig rig && rig.roomOriginX >= 0) {
                    Point origin = new(rig.roomOriginX, rig.roomOriginY);
                    NoteRoom(origin);
                    observed[origin] = (0.45f, false, true);
                }
                else if (npc.type == bossType && npc.ModNPC is FoundryOverseer ov && ov.roomOriginX >= 0) {
                    Point origin = new(ov.roomOriginX, ov.roomOriginY);
                    NoteRoom(origin);
                    bool broken = ov.State == FoundryOverseer.StatePendulum
                        || ov.State == FoundryOverseer.StateBreakRail && npc.ai[1] >= 40f;
                    float heat = ov.State == FoundryOverseer.StateDeath ? 0.55f : 1f;
                    observed[origin] = (heat, broken, false);
                }
            }
        }

        /// <summary>熔池打光 + 余烬上飘（表现层 Main.rand 合法；率随热度）</summary>
        private static void LightAndEmbers(RoomView view) {
            float heat = view.Heat;
            if (heat < 0.12f) {
                return;
            }
            for (int i = 0; i < ProofingHallRoom.GutterLeftCols.Length; i++) {
                Vector2 pool = ProofingHallRoom.GutterPoolCenterWorld(view.Origin, i);
                Lighting.AddLight(pool, FoundryOverseer.FurnaceOrange.ToVector3() * (0.28f + 0.5f * heat));
            }

            //余烬：热度定节拍（沸腾≈每 5t 一粒，文火≈每 16t）
            int interval = (int)MathHelper.Lerp(24f, 5f, heat);
            if (++view.EmberTick < interval) {
                return;
            }
            view.EmberTick = 0;
            int pick = Main.rand.Next(ProofingHallRoom.GutterLeftCols.Length);
            Vector2 at = ProofingHallRoom.GutterPoolCenterWorld(view.Origin, pick)
                + new Vector2(Main.rand.NextFloat(-26f, 26f), -4f);
            PRTLoader.NewParticle<PRT_DawnEmber>(at,
                new Vector2(Main.rand.NextFloat(-0.25f, 0.25f), -Main.rand.NextFloat(0.6f, 1.7f)),
                FoundryOverseer.FurnaceOrange, Main.rand.NextFloat(0.28f, 0.5f))
                ?.Configure(Main.rand.Next(26, 44), 0.05f);
            //沸腾期偶发渣火星（池面炸点）
            if (heat > 0.75f && Main.rand.NextBool(3)) {
                PRTLoader.NewParticle<PRT_Spark>(at,
                    new Vector2(Main.rand.NextFloat(-1.4f, 1.4f), -Main.rand.NextFloat(2f, 4.5f)),
                    Color.Lerp(FoundryOverseer.SlagHot, Color.White, Main.rand.NextFloat(0.4f)),
                    Main.rand.NextFloat(0.35f, 0.6f))?.Configure(true, Main.rand.Next(12, 22));
            }
        }

        //==================== 实体层绘制：轨梁 / 灯罩壳 / 熔浴 ====================

        public override void EndEntityDraw(SpriteBatch spriteBatch, Main main,
            GraphicsDevice graphicsDevice, RenderTarget2D screenSwap) {
            if (Main.dedServ || Main.gameMenu || views.Count == 0) {
                return;
            }
            Rectangle screenWorld = new(
                (int)Main.screenPosition.X - 200, (int)Main.screenPosition.Y - 200,
                Main.screenWidth + 400, Main.screenHeight + 400);

            foreach (RoomView view in views) {
                Rectangle roomWorld = new(view.Origin.X * 16, view.Origin.Y * 16,
                    ProofingHallRoom.Width * 16, ProofingHallRoom.Height * 16);
                if (!roomWorld.Intersects(screenWorld)) {
                    continue;
                }
                DrawIronwork(spriteBatch, view);
                DrawMeltPools(spriteBatch, graphicsDevice, view);
            }
        }

        /// <summary>轨梁与灯罩壳：乘色批，逐段取样场内光照（黑房里不自发光）</summary>
        private static void DrawIronwork(SpriteBatch sb, RoomView view) {
            Texture2D pixel = TextureAssets.MagicPixel.Value;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            Rectangle px = new(0, 0, 1, 1);
            Point o = view.Origin;
            float railY = ProofingHallRoom.RailWorldY(o);

            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
                DepthStencilState.None, RasterizerState.CullNone, null,
                Main.GameViewMatrix.TransformationMatrix);

            //轨梁：内区贯通铁梁，3 格一段取光；断轨后 36..41 列现缺口
            const int SegTiles = 3;
            for (int col = ProofingHallRoom.InteriorLeft; col < ProofingHallRoom.InteriorRight; col += SegTiles) {
                if (view.RailBroken && col + SegTiles > ProofingHallRoom.BreakCol - 2
                    && col < ProofingHallRoom.BreakCol + 4) {
                    continue;
                }
                int segW = Math.Min(SegTiles, ProofingHallRoom.InteriorRight - col) * 16;
                Color light = Lighting.GetColor(o.X + col + 1, o.Y + ProofingHallRoom.RailRel);
                Vector2 at = new(o.X * 16 + col * 16 - Main.screenPosition.X,
                    railY - 3f - Main.screenPosition.Y);
                //梁体（暗铁）+ 顶缘一线（受光更亮的沿）
                sb.Draw(pixel, at, px, FoundryOverseer.IronDeep.MultiplyRGB(light), 0f,
                    Vector2.Zero, new Vector2(segW, 6f), SpriteEffects.None, 0f);
                sb.Draw(pixel, at, px, FoundryOverseer.IronMul.MultiplyRGB(light) * 0.8f, 0f,
                    Vector2.Zero, new Vector2(segW, 1.5f), SpriteEffects.None, 0f);
            }
            //断轨撕口：缺口两端余温（战斗热度越高越亮）
            if (view.RailBroken && glow != null) {
                float hotK = 0.25f + 0.55f * view.Heat;
                Vector2 gOrigin = glow.Size() * 0.5f;
                foreach (int col in new[] { ProofingHallRoom.BreakCol - 2, ProofingHallRoom.BreakCol + 4 }) {
                    Vector2 tip = new(o.X * 16 + col * 16 - Main.screenPosition.X,
                        railY - Main.screenPosition.Y);
                    sb.Draw(glow, tip, null, (FoundryOverseer.SlagHot with { A = 0 }) * hotK, 0f,
                        gOrigin, new Vector2(14f * 2f / glow.Width), SpriteEffects.None, 0f);
                }
            }

            //检修位灯罩壳：A3 灯珠画在 (zone.Center.X, zone.Top-10)±7，这里补壳框不压珠——
            //顶板压珠上沿、两颊立柱框侧沿，珠体窗口留空
            for (int side = 0; side < 2; side++) {
                Rectangle zone = ProofingHallRoom.BayZoneWorld(o, side == 0);
                Vector2 baseAt = new(zone.Center.X, zone.Top - 10);
                Color light = Lighting.GetColor((int)(baseAt.X / 16f), (int)(baseAt.Y / 16f));
                Vector2 top = baseAt + new Vector2(-16f, -9f) - Main.screenPosition;
                sb.Draw(pixel, top, px, FoundryOverseer.IronDeep.MultiplyRGB(light), 0f,
                    Vector2.Zero, new Vector2(32f, 4f), SpriteEffects.None, 0f);
                sb.Draw(pixel, top + new Vector2(0f, 4f), px,
                    FoundryOverseer.IronDeep.MultiplyRGB(light), 0f,
                    Vector2.Zero, new Vector2(3f, 10f), SpriteEffects.None, 0f);
                sb.Draw(pixel, top + new Vector2(29f, 4f), px,
                    FoundryOverseer.IronDeep.MultiplyRGB(light), 0f,
                    Vector2.Zero, new Vector2(3f, 10f), SpriteEffects.None, 0f);
                //顶板受光沿
                sb.Draw(pixel, top, px, FoundryOverseer.IronMul.MultiplyRGB(light) * 0.7f, 0f,
                    Vector2.Zero, new Vector2(32f, 1f), SpriteEffects.None, 0f);
            }

            sb.End();
        }

        /// <summary>三条熔浴：ProofingHallMelt TechBath，quad 液面比例=0.42 上辉下液</summary>
        private static void DrawMeltPools(SpriteBatch sb, GraphicsDevice gd, RoomView view) {
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow == null) {
                return;
            }
            bool shaderOn = EffectLoader.ProofingHallMelt?.IsLoaded == true
                && CWRAsset.PerlinNoise?.IsLoaded == true;
            //quad 总高：坑深 16px 对应 v∈[0.42,1] → 27.6px，液面上留 11.6px 辉光带
            const float QuadH = 16f / 0.58f;
            const float SurfaceLift = QuadH * 0.42f;

            if (!shaderOn) {
                DrawMeltFallback(sb, glow, view);
                return;
            }

            Effect fx = EffectLoader.ProofingHallMelt.Value;
            fx.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            fx.Parameters["uHeat"]?.SetValue(view.Heat);

            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null,
                Main.GameViewMatrix.TransformationMatrix);
            gd.Textures[1] = CWRAsset.PerlinNoise.Value;
            gd.SamplerStates[1] = SamplerState.LinearWrap;

            for (int i = 0; i < ProofingHallRoom.GutterLeftCols.Length; i++) {
                Rectangle pit = ProofingHallRoom.GutterWorldRect(view.Origin, i);
                fx.Parameters["uSeed"]?.SetValue(i * 1.73f + view.Origin.X * 0.013f);
                fx.CurrentTechnique = fx.Techniques["TechBath"];
                fx.CurrentTechnique.Passes[0].Apply();
                sb.Draw(glow, new Vector2(pit.X, pit.Y - SurfaceLift) - Main.screenPosition, null,
                    Color.White, 0f, Vector2.Zero,
                    new Vector2(pit.Width / (float)glow.Width, QuadH / glow.Height),
                    SpriteEffects.None, 0f);
            }

            gd.Textures[1] = null;
            sb.End();
        }

        /// <summary>无 shader 降级：暗渣底 + 液面亮带 + 上浮辉光（两层乘/加色）</summary>
        private static void DrawMeltFallback(SpriteBatch sb, Texture2D glow, RoomView view) {
            Texture2D pixel = TextureAssets.MagicPixel.Value;
            Rectangle px = new(0, 0, 1, 1);
            float heat = view.Heat;
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null,
                Main.GameViewMatrix.TransformationMatrix);
            for (int i = 0; i < ProofingHallRoom.GutterLeftCols.Length; i++) {
                Rectangle pit = ProofingHallRoom.GutterWorldRect(view.Origin, i);
                Vector2 at = new(pit.X - Main.screenPosition.X, pit.Y - Main.screenPosition.Y);
                sb.Draw(pixel, at, px, FoundryOverseer.SlagDark, 0f, Vector2.Zero,
                    new Vector2(pit.Width, pit.Height), SpriteEffects.None, 0f);
                sb.Draw(pixel, at, px,
                    (FoundryOverseer.SlagHot with { A = 0 }) * (0.3f + 0.6f * heat), 0f,
                    Vector2.Zero, new Vector2(pit.Width, 3f), SpriteEffects.None, 0f);
                sb.Draw(glow, new Vector2(at.X + pit.Width * 0.5f, at.Y), null,
                    (FoundryOverseer.FurnaceOrange with { A = 0 }) * (0.25f + 0.45f * heat), 0f,
                    glow.Size() * 0.5f, new Vector2(pit.Width * 1.1f / glow.Width, 30f / glow.Height),
                    SpriteEffects.None, 0f);
            }
            sb.End();
        }

        //==================== EndCapture：熔池与监工的热浪扭曲（复用 ThermalHeatHaze）====================

        private const int MaxHazeSources = 8;
        private static readonly Vector4[] hazeSources = new Vector4[MaxHazeSources];

        public override void EndCaptureDraw(SpriteBatch sb, GraphicsDevice gd, RenderTarget2D screenSwap) {
            if (Main.dedServ || Main.gameMenu || views.Count == 0
                || screenSwap == null || Main.screenTarget == null
                || EffectLoader.ThermalHeatHaze?.IsLoaded != true
                || CWRAsset.Extra_193?.IsLoaded != true) {
                return;
            }
            int count = CollectHazeSources();
            if (count == 0) {
                return;
            }

            Effect shader = EffectLoader.ThermalHeatHaze.Value;

            gd.SetRenderTarget(screenSwap);
            gd.Clear(Color.Transparent);
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);
            sb.Draw(Main.screenTarget, Vector2.Zero, Color.White);
            sb.End();

            shader.Parameters["screenSize"]?.SetValue(new Vector2(Main.screenWidth, Main.screenHeight));
            shader.Parameters["sources"]?.SetValue(hazeSources);
            shader.Parameters["sourceCount"]?.SetValue(count);
            shader.Parameters["globalTime"]?.SetValue((float)Main.timeForVisualEffects * 0.018f);
            shader.Parameters["uNoise"]?.SetValue(CWRAsset.Extra_193.Value);

            gd.SetRenderTarget(Main.screenTarget);
            gd.Clear(Color.Transparent);
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend);
            shader.CurrentTechnique.Passes[0].Apply();
            sb.Draw(screenSwap, Vector2.Zero, Color.White);
            sb.End();
        }

        /// <summary>热源=屏内熔池（热度门槛 0.15）；世界→归一化屏幕坐标（镜像 ThermalHeatHazeRender）</summary>
        private static int CollectHazeSources() {
            int count = 0;
            Vector2 zoom = Main.GameViewMatrix.Zoom;
            if (zoom.X <= 0f) {
                zoom.X = 1f;
            }
            if (zoom.Y <= 0f) {
                zoom.Y = 1f;
            }
            float screenW = Main.screenWidth;
            float screenH = Main.screenHeight;
            Vector2 screenCenterPx = new(screenW * 0.5f, screenH * 0.5f);
            Vector2 viewWorldHalf = new(screenW * 0.5f / zoom.X, screenH * 0.5f / zoom.Y);
            Vector2 viewWorldCenter = Main.screenPosition + screenCenterPx;
            Rectangle screenRect = new(
                (int)(viewWorldCenter.X - viewWorldHalf.X) - 200,
                (int)(viewWorldCenter.Y - viewWorldHalf.Y) - 200,
                (int)(viewWorldHalf.X * 2) + 400,
                (int)(viewWorldHalf.Y * 2) + 400);

            foreach (RoomView view in views) {
                if (view.Heat < 0.15f || count >= MaxHazeSources) {
                    continue;
                }
                for (int i = 0; i < ProofingHallRoom.GutterLeftCols.Length && count < MaxHazeSources; i++) {
                    Vector2 world = ProofingHallRoom.GutterPoolCenterWorld(view.Origin, i)
                        + new Vector2(0f, -24f);
                    if (!screenRect.Contains(world.ToPoint())) {
                        continue;
                    }
                    Vector2 screenPx = screenCenterPx + (world - viewWorldCenter) * zoom;
                    float intensity = 0.18f + 0.42f * view.Heat;
                    float radiusNorm = MathHelper.Lerp(70f, 150f, view.Heat) * zoom.Y / screenH;
                    hazeSources[count++] = new Vector4(
                        screenPx.X / screenW, screenPx.Y / screenH, intensity, radiusNorm);
                }
            }
            return count;
        }
    }
}
