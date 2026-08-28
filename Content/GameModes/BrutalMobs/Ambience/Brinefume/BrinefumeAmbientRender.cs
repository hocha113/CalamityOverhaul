using InnoVault.RenderHandles;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Ambience.Brinefume
{
    /// <summary>
    /// 「腐澜雾」毒气泡层：硫磺水体里升起的小气泡，摇摆上浮，触到水面破裂成酸沫。
    /// 池化数组客户端自管（镜像 DungeonworldAmbientRender 的光丝池），
    /// 加色批只画薄缘泡圈；密度随氛围让位系数自动减量
    /// </summary>
    internal sealed class BrinefumeAmbientRender : RenderHandle
    {
        /// <summary>槽位分配权重 1.81</summary>
        public override float Weight => 1.81f;

        //DiffusionCircle4 黑底薄锐缘环（0.95R 处一圈亮缘），加色批当泡膜
        [VaultLoaden(CWRConstant.Masking)]
        private static Asset<Texture2D> DiffusionCircle4 = null;

        private const int MaxBubbles = 24;

        private struct Bubble
        {
            internal bool Active;
            internal Vector2 Pos;
            internal float Rise;
            internal float Phase;
            internal float Size;
            internal int Life;
        }

        private static readonly Bubble[] bubbles = new Bubble[MaxBubbles];
        private static int spawnIn;

        internal static void ClearBubbles() {
            for (int i = 0; i < bubbles.Length; i++) {
                bubbles[i].Active = false;
            }
        }

        //==================== 逻辑更新 ====================

        public override void UpdateBySystem(int index) {
            if (Main.dedServ || Main.gameMenu || Main.gamePaused) {
                return;
            }
            float density = BrinefumeAmbience.EffectDensity;
            if (density < 0.05f) {
                ClearBubbles();
                return;
            }

            //推进在途气泡
            for (int i = 0; i < bubbles.Length; i++) {
                if (!bubbles[i].Active) {
                    continue;
                }
                bubbles[i].Life++;
                bubbles[i].Pos.Y -= bubbles[i].Rise;
                bubbles[i].Pos.X += MathF.Sin(bubbles[i].Life * 0.11f + bubbles[i].Phase) * 0.3f;
                Point pt = bubbles[i].Pos.ToTileCoordinates();
                if (!WorldGen.InWorld(pt.X, pt.Y, 40) || WorldGen.SolidTile(pt.X, pt.Y)
                    || bubbles[i].Life > 900) {
                    bubbles[i].Active = false;
                    continue;
                }
                if (Framing.GetTileSafely(pt.X, pt.Y).LiquidAmount == 0) {
                    PopBubble(i);
                }
            }

            //补充：在附近水体里找一个淹没点（满密度约 8 泡/秒，破裂酸沫计入常态粉尘预算）
            if (--spawnIn > 0) {
                return;
            }
            spawnIn = 6 + (int)(8f * (1f - density)) + Main.rand.Next(3);

            Player localPlayer = Main.LocalPlayer;
            int tileX = (int)(localPlayer.Center.X / 16f) + Main.rand.Next(-52, 53);
            if (!BrinefumeAmbience.TryFindWaterSurface(
                new Point(tileX, (int)(localPlayer.Center.Y / 16f) - 24), 56, out Vector2 surface)) {
                return;
            }
            int spotY = (int)(surface.Y / 16f) + Main.rand.Next(2, 15);
            if (!WorldGen.InWorld(tileX, spotY, 40) || WorldGen.SolidTile(tileX, spotY)) {
                return;
            }
            Tile spot = Framing.GetTileSafely(tileX, spotY);
            if (spot.LiquidAmount < 200 || spot.LiquidType != LiquidID.Water) {
                return;
            }
            for (int i = 0; i < bubbles.Length; i++) {
                if (bubbles[i].Active) {
                    continue;
                }
                bubbles[i] = new Bubble {
                    Active = true,
                    Pos = new Vector2(tileX * 16f + Main.rand.NextFloat(2f, 14f), spotY * 16f + 8f),
                    Rise = Main.rand.NextFloat(0.5f, 1.1f),
                    Phase = Main.rand.NextFloat(MathHelper.TwoPi),
                    Size = Main.rand.NextFloat(3.5f, 7.5f),
                    Life = 0,
                };
                return;
            }
        }

        //破裂：水面留酸沫，偶发一记轻响
        private static void PopBubble(int i) {
            bubbles[i].Active = false;
            Vector2 pos = bubbles[i].Pos;
            int count = Main.rand.Next(1, 3);
            for (int k = 0; k < count; k++) {
                Dust foam = Dust.NewDustPerfect(pos + new Vector2(Main.rand.NextFloat(-4f, 4f), -2f),
                    DustID.TintableDust,
                    new Vector2(Main.rand.NextFloat(-0.4f, 0.4f), -Main.rand.NextFloat(0.3f, 0.9f)),
                    160, BrinefumeAmbience.FoamPale, Main.rand.NextFloat(0.6f, 0.95f));
                foam.noGravity = true;
            }
            if (Main.rand.NextBool(4)) {
                SoundEngine.PlaySound(SoundID.Drip with {
                    Volume = 0.16f,
                    Pitch = 0.55f,
                    MaxInstances = 3,
                }, pos);
            }
        }

        //==================== 绘制 ====================

        public override void EndEntityDraw(SpriteBatch spriteBatch, Main main,
            GraphicsDevice graphicsDevice, RenderTarget2D screenSwap) {
            if (Main.dedServ || Main.gameMenu) {
                return;
            }
            float density = BrinefumeAmbience.EffectDensity;
            if (density < 0.05f) {
                return;
            }
            Texture2D ring = DiffusionCircle4?.Value;
            if (ring == null || ring.IsDisposed) {
                return;
            }
            bool any = false;
            for (int i = 0; i < bubbles.Length; i++) {
                if (bubbles[i].Active) {
                    any = true;
                    break;
                }
            }
            if (!any) {
                return;
            }

            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null,
                Main.GameViewMatrix.TransformationMatrix);
            Vector2 origin = ring.Size() * 0.5f;
            //亮缘落在 0.95R，按内容宽折算：可见泡径 = Size×2
            float contentW = ring.Width * 0.95f;
            for (int i = 0; i < bubbles.Length; i++) {
                if (!bubbles[i].Active) {
                    continue;
                }
                float fadeIn = Math.Min(bubbles[i].Life / 12f, 1f);
                //加色批染色：A 随强度走（黑底贴图铁律，A=0 在这条批里=什么都不画）
                Color rim = new Color(184, 214, 120) * (0.5f * fadeIn * density);
                float scale = bubbles[i].Size * 2f / contentW;
                spriteBatch.Draw(ring, bubbles[i].Pos - Main.screenPosition, null, rim,
                    0f, origin, scale, SpriteEffects.None, 0f);
            }
            spriteBatch.End();
        }
    }
}
