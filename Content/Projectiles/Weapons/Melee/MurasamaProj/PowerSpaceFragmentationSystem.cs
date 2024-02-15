using CalamityOverhaul.Common;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee.MurasamaProj
{
    internal class PowerSpaceFragmentation
    {
        internal static void DrawPowerProjEff(SpriteBatch sb, ref float twistStrength) {
            if (!ContentConfig.Instance.MurasamaSpaceFragmentationBool) {//这里，如果配置文件关闭了碎屏效果，那么就不执行这里的特效渲染绘制
                return;
            }
            if (!Main.LocalPlayer.CWR().EndSkillEffectStartBool) {
                return;
            }
            int targetProjType = ModContent.ProjectileType<MurasamaEndSkillOrbOnSpan>();
            foreach (Projectile proj in Main.projectile) {
                Vector2 offsetRotV = proj.rotation.ToRotationVector2() * 1500;
                if (proj.type == targetProjType && proj.active) {
                    sb.End();
                    sb.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied, SamplerState.PointWrap, DepthStencilState.None, RasterizerState.CullCounterClockwise, null, Main.Transform);
                    Texture2D texture = CWRUtils.GetT2DValue(CWRConstant.Placeholder2);
                    int length = (int)(Math.Sqrt(Main.screenWidth * Main.screenWidth + Main.screenHeight * Main.screenHeight) * 4f);
                    sb.Draw(texture,
                        proj.Center + Vector2.Normalize((proj.Left - proj.Center).RotatedBy(proj.rotation)) * length / 2 - Main.screenPosition + offsetRotV,
                        new(0, 0, 1, 1),
                        new(CWRUtils.GetCorrectRadian(proj.rotation), proj.ai[0], 0f, 0.2f),
                        proj.rotation,
                        Vector2.Zero,
                        length, SpriteEffects.None, 0);
                    sb.Draw(texture,
                        proj.Center + Vector2.Normalize((proj.Left - proj.Center).RotatedBy(proj.rotation)) * length / 2 - Main.screenPosition + offsetRotV,
                        new(0, 0, 1, 1),
                        new(CWRUtils.GetCorrectRadian(proj.rotation) + Math.Sign(proj.rotation + 0.001f) * 0.5f, proj.ai[0], 0f, 0.2f),
                        proj.rotation,
                        new(0, 1),
                        length,
                        SpriteEffects.None, 0);
                    twistStrength = 0.055f * proj.localAI[0];
                }
            }
        }
    }
}
