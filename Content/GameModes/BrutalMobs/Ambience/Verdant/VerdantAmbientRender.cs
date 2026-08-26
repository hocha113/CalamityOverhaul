using CalamityOverhaul.Content.GameModes.BrutalMobs.Ambience.Verdant.Projectiles;
using InnoVault.RenderHandles;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Ambience.Verdant
{
    /// <summary>
    /// 沼雾伏影的屏幕层绘制：雾团遮蔽画在实体层之上（人走进雾里被雾罩住，
    /// 轻微遮蔽视野即机制卖点），荆棘合拢圈压在雾之上保证全程可读。
    /// 自开自收 AlphaBlend 批（Fog/Chain26/Extra_98 皆真 alpha，可承载暗形），无 RT 槽
    /// </summary>
    internal sealed class VerdantAmbientRender : RenderHandle
    {
        /// <summary>槽位分配权重 1.62</summary>
        public override float Weight => 1.62f;

        /// <summary>荆棘环影段数</summary>
        private const int ThornSegments = 24;

        public override void EndEntityDraw(SpriteBatch spriteBatch, Main main,
            GraphicsDevice graphicsDevice, RenderTarget2D screenSwap) {
            if (Main.dedServ || Main.gameMenu) {
                return;
            }
            int fogType = ModContent.ProjectileType<VerdantMireFogProj>();
            int thornType = ModContent.ProjectileType<VerdantThornRingProj>();
            bool anyFog = false;
            bool anyThorn = false;
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.type == fogType) {
                    anyFog = true;
                }
                else if (proj.type == thornType) {
                    anyThorn = true;
                }
                if (anyFog && anyThorn) {
                    break;
                }
            }
            if (!anyFog && !anyThorn) {
                return;
            }

            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null,
                Main.GameViewMatrix.TransformationMatrix);
            //先雾后棘：荆棘影必须盖在雾上，可读性优先
            if (anyFog) {
                foreach (Projectile proj in Main.ActiveProjectiles) {
                    if (proj.type == fogType && proj.ModProjectile is VerdantMireFogProj fog
                        && OnScreen(proj.Center, VerdantMireFogProj.FogRadius + 260f)) {
                        DrawFogCloud(spriteBatch, proj, fog);
                    }
                }
            }
            if (anyThorn) {
                foreach (Projectile proj in Main.ActiveProjectiles) {
                    if (proj.type == thornType && proj.ModProjectile is VerdantThornRingProj thorn
                        && OnScreen(proj.Center, 480f)) {
                        DrawThornRing(spriteBatch, proj, thorn);
                    }
                }
            }
            spriteBatch.End();
        }

        private static bool OnScreen(Vector2 worldPos, float fluff) {
            return worldPos.X > Main.screenPosition.X - fluff
                && worldPos.X < Main.screenPosition.X + Main.screenWidth + fluff
                && worldPos.Y > Main.screenPosition.Y - fluff
                && worldPos.Y < Main.screenPosition.Y + Main.screenHeight + fluff;
        }

        /// <summary>雾团遮蔽：中心厚幕 + 内环游移团 + 外缘散絮，浓度由弹幕包络同源驱动</summary>
        private static void DrawFogCloud(SpriteBatch sb, Projectile proj, VerdantMireFogProj fog) {
            float density = fog.Density;
            if (density < 0.02f) {
                return;
            }
            Texture2D tex = CWRAsset.Fog?.Value;
            if (tex == null) {
                return;
            }
            Vector2 origin = tex.Size() * 0.5f;
            float time = Main.GlobalTimeWrappedHourly;
            float seed = proj.identity * 1.37f;
            Color baseCol = Main.raining ? new Color(134, 148, 134) : new Color(148, 164, 140);
            if (!Main.dayTime) {
                baseCol = new Color((int)(baseCol.R * 0.72f), (int)(baseCol.G * 0.76f), (int)(baseCol.B * 0.74f));
            }
            //Fog 内容约占画布 8 成，按可见像素折算缩放
            float unit = tex.Width * 0.8f;
            const float R = VerdantMireFogProj.FogRadius;
            Vector2 center = proj.Center - Main.screenPosition;

            //中心厚幕
            sb.Draw(tex, center, null, baseCol * (0.30f * density),
                time * 0.05f + seed, origin, R * 2.3f / unit, SpriteEffects.None, 0f);

            //内环游移团（遮蔽主力）
            for (int i = 0; i < 6; i++) {
                float ang = time * 0.09f + seed + i * MathHelper.TwoPi / 6f;
                Vector2 off = ang.ToRotationVector2() * R * 0.55f;
                float wobble = 0.85f + 0.18f * MathF.Sin(time * 0.7f + seed + i * 1.31f);
                sb.Draw(tex, center + off, null, baseCol * (0.22f * density),
                    -time * 0.07f + i, origin, R * 1.15f / unit * wobble, SpriteEffects.None, 0f);
            }

            //外缘散絮
            for (int i = 0; i < 5; i++) {
                float ang = -time * 0.05f + seed * 2.1f + i * MathHelper.TwoPi / 5f;
                Vector2 off = ang.ToRotationVector2() * R * 0.95f;
                sb.Draw(tex, center + off, null, baseCol * (0.12f * density),
                    time * 0.04f + i * 2f, origin, R * 0.7f / unit, SpriteEffects.None, 0f);
            }
        }

        /// <summary>
        /// 荆棘合拢圈：环上藤段（原版藤链贴图）+ 内指荆刺（Extra_98 梭形），
        /// 藤隙扇区留空（绘制缺口略窄于判定豁免，绝不视觉夸大安全区），
        /// 蓄势颤动→合拢实体→枯落垂散，几何全部与判定同源
        /// </summary>
        private static void DrawThornRing(SpriteBatch sb, Projectile proj, VerdantThornRingProj thorn) {
            float alpha = thorn.VisualAlpha;
            if (alpha < 0.02f) {
                return;
            }
            Texture2D vine = TextureAssets.Chain26.Value;
            Texture2D spike = CWRAsset.Extra_98?.Value;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (spike == null || glow == null) {
                return;
            }
            float R = thorn.CurrentRadius;
            float witherT = thorn.WitherT;
            float tremble = thorn.TrembleAmp;
            float time = Main.GlobalTimeWrappedHourly;
            Vector2 center = proj.Center - Main.screenPosition;

            //湿绿荫影 → 枯落转褐
            Color vineCol = Color.Lerp(new Color(40, 52, 34), new Color(96, 78, 44), witherT) * alpha;
            Color spikeCol = Color.Lerp(new Color(26, 34, 22), new Color(88, 70, 40), witherT) * alpha;

            for (int i = 0; i < ThornSegments; i++) {
                float ang = i * MathHelper.TwoPi / ThornSegments
                    + MathF.Sin(proj.identity * 2.3f + i * 5.7f) * 0.055f;
                //藤隙：绘制缺口 0.44 rad < 判定豁免 0.5 rad，看见的空处必然安全
                if (Math.Abs(MathHelper.WrapAngle(ang - thorn.GapCenter)) < VerdantThornRingProj.GapHalfAngle - 0.06f) {
                    continue;
                }
                Vector2 dir = ang.ToRotationVector2();
                //蓄势期径向颤动，枯落期向下垂散
                float jig = tremble > 0.01f ? MathF.Sin(time * 7f + i * 2.1f) * tremble : 0f;
                Vector2 pos = center + dir * (R + jig);
                pos.Y += witherT * 14f;
                float segScale = 1f - 0.35f * witherT;

                //藤段：沿切向铺两节藤链
                float tangent = ang + MathHelper.PiOver2;
                Vector2 tanDir = tangent.ToRotationVector2();
                for (int k = -1; k <= 0; k++) {
                    sb.Draw(vine, pos + tanDir * (k * 14f + 7f), null, vineCol,
                        tangent, new Vector2(vine.Width * 0.5f, 0f), 0.78f * segScale, SpriteEffects.None, 0f);
                }

                //荆刺：梭形长轴对准圆心（内指）
                sb.Draw(spike, pos - dir * 9f, null, spikeCol,
                    ang + MathHelper.Pi + MathHelper.PiOver2, spike.Size() * 0.5f,
                    new Vector2(0.30f, 0.55f) * segScale, SpriteEffects.None, 0f);

                //合拢期刺尖冷光（AlphaBlend 批内 A=0 即纯加色）
                if (witherT <= 0f && tremble <= 0.01f && (i & 1) == 0) {
                    sb.Draw(glow, pos - dir * 16f, null, new Color(116, 158, 76, 0) * (0.22f * alpha),
                        0f, glow.Size() * 0.5f, 0.16f, SpriteEffects.None, 0f);
                }
            }

            //藤隙两缘的暗标（提示缺口方位，不照亮安全区本身）
            if (witherT < 0.5f) {
                for (int s = -1; s <= 1; s += 2) {
                    float edgeAng = thorn.GapCenter + s * (VerdantThornRingProj.GapHalfAngle - 0.05f);
                    Vector2 pos = center + edgeAng.ToRotationVector2() * R;
                    sb.Draw(glow, pos, null, new Color(140, 180, 90, 0) * (0.16f * alpha),
                        0f, glow.Size() * 0.5f, 0.22f, SpriteEffects.None, 0f);
                }
            }
        }
    }
}
