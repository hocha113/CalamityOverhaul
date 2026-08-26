using InnoVault.TileProcessors;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.Industrials.ElectricPowers.HealingStations
{
    /// <summary>
    /// 治疗站地面法阵圈:塔基柔和的双环贴地椭圆+沿环巡行的光珠,刻意低调不抢戏。
    /// 压扁椭圆禁旋转(先缩放后旋转会整体掀斜),转动感交给巡环光珠;
    /// PreDrawEverything 层=物块之上实体之下,正是地面贴花该在的层
    /// </summary>
    internal class HealAuraRender : GlobalTileProcessor
    {
        //贴地透视压扁比
        private const float Squish = 0.40f;

        public override bool PreDrawEverything(SpriteBatch spriteBatch) {
            if (Main.dedServ) {
                return true;
            }
            Texture2D ringSoft = CWRUtils.GetT2DAsset(CWRConstant.Masking + "DiffusionCircle5")?.Value;
            Texture2D ringSharp = CWRUtils.GetT2DAsset(CWRConstant.Masking + "DiffusionCircle4")?.Value;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (ringSoft == null || ringSharp == null || glow == null) {
                return true;
            }

            bool begun = false;
            foreach (var tp in TileProcessorLoader.TP_InWorld) {
                if (tp is not HealStationTP heal || !heal.Active) {
                    continue;
                }
                if (heal.GlowIntensity < 0.05f) {
                    continue;
                }
                Vector2 basePos = heal.PosInWorld + new Vector2(heal.Width * 0.5f, heal.Height - 2f);
                if (!VaultUtils.IsPointOnScreen(basePos - Main.screenPosition, 320)) {
                    continue;
                }

                if (!begun) {
                    begun = true;
                    spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                        SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone,
                        null, Main.GameViewMatrix.TransformationMatrix);
                }

                float time = Main.GlobalTimeWrappedHourly;
                float breath = 0.85f + 0.15f * MathF.Sin(time * 1.7f + heal.Position.X * 0.31f);
                float visR = 104f * heal.GlowIntensity * breath;
                Color tint = HealStation.Tint;
                tint.A = 0;
                Vector2 screenPos = basePos - Main.screenPosition;

                //柔环体:DiffusionCircle5 有机热斑环,可见环带在0.39R
                float softScale = visR / (ringSoft.Width * 0.5f * 0.39f);
                spriteBatch.Draw(ringSoft, screenPos, null, tint * (0.20f * heal.GlowIntensity),
                    0f, ringSoft.Size() * 0.5f, new Vector2(softScale, softScale * Squish),
                    SpriteEffects.None, 0f);
                //锐缘:DiffusionCircle4 薄锐缘在0.95R,略大一号包在外侧
                float sharpScale = visR * 1.06f / (ringSharp.Width * 0.5f * 0.95f);
                spriteBatch.Draw(ringSharp, screenPos, null, tint * (0.15f * heal.GlowIntensity),
                    0f, ringSharp.Size() * 0.5f, new Vector2(sharpScale, sharpScale * Squish),
                    SpriteEffects.None, 0f);

                //巡环光珠:4颗沿椭圆巡行,压扁椭圆的"转动感"由它承担
                for (int i = 0; i < 4; i++) {
                    float ang = time * 0.55f + MathHelper.PiOver2 * i;
                    Vector2 dotPos = screenPos + new Vector2(MathF.Cos(ang) * visR, MathF.Sin(ang) * visR * Squish);
                    //后半程(远侧)光珠压暗,给椭圆一点前后纵深
                    float depth = 0.65f + 0.35f * MathF.Sin(ang);
                    spriteBatch.Draw(glow, dotPos, null, tint * (0.30f * heal.GlowIntensity * depth),
                        0f, glow.Size() * 0.5f, 0.085f, SpriteEffects.None, 0f);
                }
            }

            if (begun) {
                spriteBatch.End();
            }
            return true;
        }
    }
}
