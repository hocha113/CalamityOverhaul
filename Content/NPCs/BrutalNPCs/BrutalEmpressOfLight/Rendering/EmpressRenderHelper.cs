using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEmpressOfLight.Core;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEmpressOfLight.Rendering
{
    /// <summary>本体附加层：底层辉光/顶层蓄力星芒与昼形态过驱（A=0加色技巧走AlphaBlend批）</summary>
    internal static class EmpressRenderHelper
    {
        /// <summary>底层：身后柔光大晕，昼形态渐白金；蓄力时腾起呼吸</summary>
        public static void DrawUnderGlow(SpriteBatch spriteBatch, NPC npc, EmpressStateContext context) {
            if (context == null || npc.Opacity <= 0.05f) {
                return;
            }
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Vector2 drawPos = npc.Center - Main.screenPosition;
            Vector2 origin = glow.Size() / 2f;

            float time = Main.GlobalTimeWrappedHourly;
            float breath = 1f + 0.06f * (float)Math.Sin(time * 2.2f);
            float charge = context.IsCharging ? context.ChargeProgress : 0f;
            float day = context.DayFormBlend;

            //夜：虹彩缓移底晕；昼：白金炽感
            float hue = (time * 0.06f) % 1f;
            Color nightGlow = Main.hslToRgb(hue, 0.7f, 0.6f);
            Color dayGlow = new(255, 240, 205);
            Color aura = Color.Lerp(nightGlow, dayGlow, day) with { A = 0 };

            float baseScale = (3.4f + charge * 1.2f) * breath * npc.Opacity;
            spriteBatch.Draw(glow, drawPos, null, aura * (0.26f + day * 0.14f + charge * 0.2f) * npc.Opacity,
                0f, origin, baseScale, SpriteEffects.None, 0f);
            spriteBatch.Draw(glow, drawPos, null, Color.White with { A = 0 } * (0.1f + day * 0.12f) * npc.Opacity,
                0f, origin, baseScale * 0.55f, SpriteEffects.None, 0f);
        }

        /// <summary>顶层：手部蓄力星芒+昼形态轮廓过驱错相残像</summary>
        public static void DrawOverGlow(SpriteBatch spriteBatch, NPC npc, EmpressStateContext context) {
            if (context == null || npc.Opacity <= 0.05f) {
                return;
            }

            float time = Main.GlobalTimeWrappedHourly;

            //蓄力手星芒
            if (context.IsCharging && context.ChargeProgress > 0.04f) {
                Texture2D star = CWRAsset.StarTexture_White.Value;
                Vector2 starOrigin = star.Size() / 2f;
                float p = context.ChargeProgress;
                float flicker = 0.85f + 0.15f * (float)Math.Sin(time * 26f);
                float scale = (0.06f + p * 0.14f) * flicker;
                float hue = (time * 0.35f) % 1f;
                Color prism = Main.hslToRgb(hue, 1f, 0.7f) with { A = 0 };

                if (context.ChargeHand is 1 or 3) {
                    Vector2 hand = context.LeftHand - Main.screenPosition;
                    spriteBatch.Draw(star, hand, null, prism * p, time * 2.4f, starOrigin, scale, SpriteEffects.None, 0f);
                    spriteBatch.Draw(star, hand, null, Color.White with { A = 0 } * (p * 0.8f), -time * 1.7f,
                        starOrigin, scale * 0.55f, SpriteEffects.None, 0f);
                }
                if (context.ChargeHand is 2 or 3) {
                    Vector2 hand = context.RightHand - Main.screenPosition;
                    spriteBatch.Draw(star, hand, null, prism * p, -time * 2.2f, starOrigin, scale, SpriteEffects.None, 0f);
                    spriteBatch.Draw(star, hand, null, Color.White with { A = 0 } * (p * 0.8f), time * 1.5f,
                        starOrigin, scale * 0.55f, SpriteEffects.None, 0f);
                }
            }

            //昼形态过驱：本体贴图三向错相彩虹残像（处刑形态的视觉宣告）
            float day = context.DayFormBlend;
            if (day > 0.05f) {
                Texture2D body = Terraria.GameContent.TextureAssets.Npc[npc.type].Value;
                Vector2 drawPos = npc.Center - Main.screenPosition;
                Vector2 origin = npc.frame.Size() / 2f;
                float pulse = 0.5f + 0.5f * (float)Math.Sin(time * MathHelper.TwoPi * 0.8f);
                for (int i = 0; i < 3; i++) {
                    float ang = time * 1.9f + MathHelper.TwoPi / 3f * i;
                    Vector2 offset = ang.ToRotationVector2() * MathHelper.Lerp(2.5f, 7f, pulse) * day;
                    Color ghost = Main.hslToRgb((i / 3f + time * 0.22f) % 1f, 1f, 0.62f) with { A = 0 };
                    spriteBatch.Draw(body, drawPos + offset, npc.frame, ghost * (0.3f * day * npc.Opacity),
                        npc.rotation, origin, npc.scale, SpriteEffects.None, 0f);
                }
            }
        }
    }
}
