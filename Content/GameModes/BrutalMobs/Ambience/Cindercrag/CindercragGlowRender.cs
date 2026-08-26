using InnoVault.RenderHandles;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Ambience.Cindercrag
{
    /// <summary>
    /// 硫火之崖屏幕光效层（Weight 1.83）：
    /// 「烬羽」的崖壁红光——沿崖面采样锚点，呼吸式脉动的暗红辉光贴在岩面上；
    /// 「硫辉」——崖底熔湖反光把洞顶染成脉动暗红（不规则双正弦，读作火光晃动）。
    /// 锚点是屏幕级视觉私产（逐客户端一份），加色批绘制，无 RT 槽；Boss 在场整体收敛
    /// </summary>
    internal sealed class CindercragGlowRender : RenderHandle
    {
        public override float Weight => 1.83f;

        private struct WallAnchor
        {
            internal bool Active;
            internal Vector2 Pos;
            internal Vector2 Normal;
            internal float Phase;
            internal int Life;
            internal int MaxLife;
            internal float Span;
        }

        private struct CeilAnchor
        {
            internal bool Active;
            internal Vector2 Pos;
            internal float Phase;
            internal int Life;
            internal int MaxLife;
            internal float Strength;
            internal float Span;
        }

        private const int MaxWalls = 14;
        private const int MaxCeils = 6;
        private static readonly WallAnchor[] walls = new WallAnchor[MaxWalls];
        private static readonly CeilAnchor[] ceils = new CeilAnchor[MaxCeils];
        private static int wallScanIn;
        private static int ceilScanIn;

        /// <summary>崖壁辉光暗红底</summary>
        private static readonly Color WallDeep = new(188, 40, 28);
        /// <summary>崖壁辉光暖芯（不走纯白）</summary>
        private static readonly Color WallCore = new(255, 92, 44);
        /// <summary>硫辉洞顶暗红</summary>
        private static readonly Color CeilWash = new(200, 46, 24);

        //==================== 锚点采样与推进 ====================

        public override void UpdateBySystem(int index) {
            if (Main.gameMenu || Main.gamePaused || Main.dedServ) {
                return;
            }
            float presence = CindercragAmbience.Presence;
            if (presence < 0.02f) {
                for (int i = 0; i < walls.Length; i++) {
                    walls[i].Active = false;
                }
                for (int i = 0; i < ceils.Length; i++) {
                    ceils[i].Active = false;
                }
                return;
            }

            AgeAnchors();

            if (--wallScanIn <= 0) {
                wallScanIn = 20;
                TrySampleWall();
            }
            if (--ceilScanIn <= 0) {
                ceilScanIn = 42;
                TrySampleCeiling();
            }

            EmitLight(presence);
        }

        private static void AgeAnchors() {
            //离屏太远或寿终的锚点回收
            Vector2 screenCenter = Main.screenPosition + new Vector2(Main.screenWidth, Main.screenHeight) * 0.5f;
            float cullDist = Main.screenWidth * 1.2f;
            for (int i = 0; i < walls.Length; i++) {
                if (!walls[i].Active) {
                    continue;
                }
                walls[i].Life++;
                if (walls[i].Life >= walls[i].MaxLife || Vector2.Distance(walls[i].Pos, screenCenter) > cullDist) {
                    walls[i].Active = false;
                }
            }
            for (int i = 0; i < ceils.Length; i++) {
                if (!ceils[i].Active) {
                    continue;
                }
                ceils[i].Life++;
                if (ceils[i].Life >= ceils[i].MaxLife || Vector2.Distance(ceils[i].Pos, screenCenter) > cullDist) {
                    ceils[i].Active = false;
                }
            }
        }

        /// <summary>崖壁锚点：屏内随机点固到带开阔面的实心瓦，辉光贴面而生</summary>
        private static void TrySampleWall() {
            Vector2 pos = Main.screenPosition + new Vector2(
                Main.rand.NextFloat(Main.screenWidth), Main.rand.NextFloat(Main.screenHeight));
            Point tp = pos.ToTileCoordinates();
            if (!WorldGen.InWorld(tp.X, tp.Y, 10) || !WorldGen.SolidTile(tp.X, tp.Y)) {
                return;
            }
            //找开阔面法向（只取一个）
            Vector2 normal = default;
            if (OpenAt(tp.X + 1, tp.Y)) {
                normal = Vector2.UnitX;
            }
            else if (OpenAt(tp.X - 1, tp.Y)) {
                normal = -Vector2.UnitX;
            }
            else if (OpenAt(tp.X, tp.Y - 1)) {
                normal = -Vector2.UnitY;
            }
            else if (OpenAt(tp.X, tp.Y + 1)) {
                normal = Vector2.UnitY;
            }
            else {
                return;
            }

            Vector2 anchorPos = new Vector2(tp.X * 16f + 8f, tp.Y * 16f + 8f) + normal * 10f;
            //与既有锚点保持间距，避免辉光堆団
            for (int i = 0; i < walls.Length; i++) {
                if (walls[i].Active && Vector2.Distance(walls[i].Pos, anchorPos) < 130f) {
                    return;
                }
            }
            for (int i = 0; i < walls.Length; i++) {
                if (walls[i].Active) {
                    continue;
                }
                walls[i] = new WallAnchor {
                    Active = true,
                    Pos = anchorPos,
                    Normal = normal,
                    Phase = Main.rand.NextFloat(MathHelper.TwoPi),
                    Life = 0,
                    MaxLife = Main.rand.Next(480, 780),
                    Span = Main.rand.NextFloat(46f, 96f),
                };
                return;
            }
        }

        /// <summary>
        /// 硫辉锚点：本列向下探到熔岩液面（实心阻断），再向上探洞顶。
        /// 熔湖越近反光越强；找不到熔岩就没有硫辉（光有来源）
        /// </summary>
        private static void TrySampleCeiling() {
            Player player = Main.LocalPlayer;
            if (player == null || !player.active) {
                return;
            }
            int col = (int)((Main.screenPosition.X + Main.rand.NextFloat(Main.screenWidth)) / 16f);
            int rowStart = (int)(player.Center.Y / 16f);
            if (!WorldGen.InWorld(col, rowStart, 10)) {
                return;
            }

            int lavaRow = -1;
            for (int dy = 2; dy <= 46; dy++) {
                int y = rowStart + dy;
                if (!WorldGen.InWorld(col, y, 10)) {
                    break;
                }
                Tile tile = Main.tile[col, y];
                if (tile.LiquidAmount > 64 && tile.LiquidType == Terraria.ID.LiquidID.Lava) {
                    lavaRow = y;
                    break;
                }
                if (tile.HasTile && Main.tileSolid[tile.TileType]) {
                    break;
                }
            }
            if (lavaRow < 0) {
                return;
            }

            int ceilRow = -1;
            for (int dy = 1; dy <= 40; dy++) {
                int y = rowStart - dy;
                if (!WorldGen.InWorld(col, y, 10)) {
                    break;
                }
                if (WorldGen.SolidTile(col, y)) {
                    ceilRow = y;
                    break;
                }
            }
            if (ceilRow < 0) {
                return;
            }

            Vector2 anchorPos = new(col * 16f + 8f, ceilRow * 16f + 16f);
            for (int i = 0; i < ceils.Length; i++) {
                if (ceils[i].Active && Vector2.Distance(ceils[i].Pos, anchorPos) < 260f) {
                    return;
                }
            }
            float lavaDist = (lavaRow - ceilRow) * 16f;
            for (int i = 0; i < ceils.Length; i++) {
                if (ceils[i].Active) {
                    continue;
                }
                ceils[i] = new CeilAnchor {
                    Active = true,
                    Pos = anchorPos,
                    Phase = Main.rand.NextFloat(MathHelper.TwoPi),
                    Life = 0,
                    MaxLife = Main.rand.Next(540, 840),
                    Strength = MathHelper.Clamp(1.35f - lavaDist / 760f, 0.35f, 1f),
                    Span = Main.rand.NextFloat(130f, 230f),
                };
                return;
            }
        }

        private static bool OpenAt(int x, int y) {
            if (!WorldGen.InWorld(x, y, 10)) {
                return false;
            }
            Tile tile = Main.tile[x, y];
            return !(tile.HasTile && Main.tileSolid[tile.TileType]);
        }

        /// <summary>锚点实际照明（视觉与照明同源，红光是真光）</summary>
        private static void EmitLight(float presence) {
            float bossDim = CWRWorld.HasBoss ? 0.6f : 1f;
            float t = Main.GlobalTimeWrappedHourly;
            for (int i = 0; i < walls.Length; i++) {
                if (!walls[i].Active) {
                    continue;
                }
                float breathe = 0.62f + 0.38f * MathF.Sin(t * 2.2f + walls[i].Phase);
                Lighting.AddLight(walls[i].Pos, new Vector3(0.30f, 0.075f, 0.05f) * (breathe * presence * bossDim));
            }
            for (int i = 0; i < ceils.Length; i++) {
                if (!ceils[i].Active) {
                    continue;
                }
                float pulse = LavaPulse(t, ceils[i].Phase);
                Lighting.AddLight(ceils[i].Pos + new Vector2(0f, 8f),
                    new Vector3(0.22f, 0.05f, 0.035f) * (pulse * ceils[i].Strength * presence * bossDim));
            }
        }

        /// <summary>熔湖反光的不规则脉动：双正弦异频叠加，避免机械匀速呼吸</summary>
        private static float LavaPulse(float t, float phase)
            => 0.55f + 0.28f * MathF.Sin(t * 0.9f + phase) + 0.17f * MathF.Sin(t * 2.31f + phase * 1.7f);

        //==================== 绘制 ====================

        public override void EndEntityDraw(SpriteBatch spriteBatch, Main main
            , GraphicsDevice graphicsDevice, RenderTarget2D screenSwap) {
            if (Main.dedServ || Main.gameMenu) {
                return;
            }
            float presence = CindercragAmbience.Presence;
            if (presence < 0.02f) {
                return;
            }
            bool any = false;
            for (int i = 0; i < walls.Length && !any; i++) {
                any = walls[i].Active;
            }
            for (int i = 0; i < ceils.Length && !any; i++) {
                any = ceils[i].Active;
            }
            if (!any) {
                return;
            }

            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow == null || glow.IsDisposed) {
                return;
            }
            float bossDim = CWRWorld.HasBoss ? 0.6f : 1f;
            float t = Main.GlobalTimeWrappedHourly;

            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null,
                Main.GameViewMatrix.TransformationMatrix);

            Vector2 origin = glow.Size() * 0.5f;
            //崖壁红光：沿崖面拉长的呼吸辉光，宽底 + 暖芯双层
            for (int i = 0; i < walls.Length; i++) {
                if (!walls[i].Active) {
                    continue;
                }
                float lt = walls[i].Life / (float)walls[i].MaxLife;
                float env = MathF.Min(lt / 0.12f, 1f) * MathHelper.Clamp((1f - lt) / 0.2f, 0f, 1f);
                float breathe = 0.62f + 0.38f * MathF.Sin(t * 2.2f + walls[i].Phase);
                float alpha = 0.13f * presence * breathe * env * bossDim;
                if (alpha < 0.004f) {
                    continue;
                }
                Vector2 pos = walls[i].Pos - Main.screenPosition;
                //长轴贴崖面（垂直于法向）
                float rot = walls[i].Normal.ToRotation() + MathHelper.PiOver2;
                var wide = new Vector2(walls[i].Span / 26f, 0.62f);
                spriteBatch.Draw(glow, pos, null, WallDeep * alpha, rot, origin, wide, SpriteEffects.None, 0f);
                spriteBatch.Draw(glow, pos, null, WallCore * (alpha * 0.55f), rot, origin, wide * 0.52f, SpriteEffects.None, 0f);
            }

            //硫辉：洞顶横向宽幅暗红洗光，脉动读作熔湖火光晃动
            for (int i = 0; i < ceils.Length; i++) {
                if (!ceils[i].Active) {
                    continue;
                }
                float lt = ceils[i].Life / (float)ceils[i].MaxLife;
                float env = MathF.Min(lt / 0.15f, 1f) * MathHelper.Clamp((1f - lt) / 0.22f, 0f, 1f);
                float pulse = LavaPulse(t, ceils[i].Phase);
                float alpha = 0.085f * presence * pulse * ceils[i].Strength * env * bossDim;
                if (alpha < 0.004f) {
                    continue;
                }
                Vector2 pos = ceils[i].Pos - Main.screenPosition;
                var scale = new Vector2(ceils[i].Span / 26f, 0.85f);
                spriteBatch.Draw(glow, pos, null, CeilWash * alpha, 0f, origin, scale, SpriteEffects.None, 0f);
                spriteBatch.Draw(glow, pos, null, WallCore * (alpha * 0.35f), 0f, origin, scale * new Vector2(0.6f, 0.55f), SpriteEffects.None, 0f);
            }

            spriteBatch.End();
        }
    }
}
