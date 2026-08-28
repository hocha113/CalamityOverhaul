using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaRains;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaTalismans.Roster
{
    /// <summary>
    /// 潦的演出与伴生弹幕集中处。涨潦事件只有所有者端观测得到（timeLeft 不入同步包），
    /// 故涨圈/漫溢/浮泡演出为 owner 端本地；溢流波是真弹幕，旁观端凭生成包照见
    /// </summary>
    internal static class FuLaoFX
    {
        /// <summary>沼沫青白：漫出来的水泛的那点浅</summary>
        internal static readonly Color FoamPale = new(206, 232, 214);

        /// <summary>洼的当前满铺半宽（px），镜像洼身口径</summary>
        private static float HalfWidth(Projectile puddle) {
            float radiusMul = puddle.ai[0] > 0.01f ? puddle.ai[0] : 1f;
            return KikasaInkPuddle.WidthPx * radiusMul * 0.5f;
        }

        /// <summary>一注到账的涨圈：洼心荡开一圈涟漪+几粒上浮墨珠，水声随水位升调</summary>
        internal static void RiseRipple(Projectile puddle, int stage, Color accent) {
            if (Main.dedServ) {
                return;
            }
            KikasaInk.Play(KikasaInk.InkSplash, puddle.Center, 0.32f, -0.35f + 0.22f * stage, 3);
            PRTLoader.NewParticle<PRT_HeartcarverPulseRing>(
                puddle.Center - Vector2.UnitY * 3f, Vector2.Zero,
                accent * 0.45f, 0.05f)?.Configure(0.05f, 0.34f + 0.08f * stage, 11);
            for (int i = 0; i < 3; i++) {
                float xOff = Main.rand.NextFloat(-0.5f, 0.5f) * HalfWidth(puddle);
                PRTLoader.NewParticle<PRT_KikasaInkBead>(
                    puddle.Center + new Vector2(xOff, -3f),
                    new Vector2(xOff * 0.02f, -Main.rand.NextFloat(1.2f, 2.2f)),
                    Main.rand.NextBool(3) ? FoamPale : accent,
                    Main.rand.NextFloat(0.18f, 0.28f))?.Configure(Main.rand.Next(14, 22));
            }
        }

        /// <summary>满潦漫溢：两缘白线外涌+一圈大涟漪+低沉水涌声，读作「水从边上漫出去了」</summary>
        internal static void OverflowCrash(Projectile puddle, Color accent) {
            if (Main.dedServ) {
                return;
            }
            KikasaInk.Play(KikasaInk.InkSplash, puddle.Center, 0.55f, -0.7f, 3);
            PRTLoader.NewParticle<PRT_HeartcarverPulseRing>(
                puddle.Center - Vector2.UnitY * 3f, Vector2.Zero,
                Color.Lerp(accent, FoamPale, 0.5f) * 0.5f, 0.07f)?.Configure(0.07f, 0.62f, 14);
            float half = HalfWidth(puddle);
            for (int side = -1; side <= 1; side += 2) {
                Vector2 rim = puddle.Center + new Vector2(side * half, -3f);
                //缘口外涌白线：水漫过洼沿的那一撇
                for (int i = 0; i < 3; i++) {
                    PRTLoader.NewParticle<PRT_Line>(rim + Main.rand.NextVector2Circular(4f, 2f),
                        new Vector2(side * Main.rand.NextFloat(3.5f, 6f), -Main.rand.NextFloat(0.4f, 1.4f)),
                        Color.Lerp(accent, FoamPale, 0.6f) * 0.7f,
                        Main.rand.NextFloat(0.4f, 0.6f))?.Configure(false, Main.rand.Next(8, 12));
                }
                for (int i = 0; i < 3; i++) {
                    PRTLoader.NewParticle<PRT_KikasaInkBead>(rim,
                        new Vector2(side * Main.rand.NextFloat(1.6f, 3.4f), -Main.rand.NextFloat(0.8f, 1.8f)),
                        Main.rand.NextBool(3) ? KikasaInk.InkDeep : accent,
                        Main.rand.NextFloat(0.2f, 0.32f))?.Configure(Main.rand.Next(16, 26));
                }
            }
            PRTLoader.NewParticle<PRT_KikasaInkMist>(puddle.Center - Vector2.UnitY * 4f,
                -Vector2.UnitY * 0.5f, accent * 0.6f,
                Main.rand.NextFloat(0.7f, 1f))?.Configure(Main.rand.Next(20, 30));
        }

        /// <summary>洼面浮泡：水位越高泡越稠，涨过水的洼看得出「满」（owner 端本地低频泵）</summary>
        internal static void StageBubbles(Projectile puddle, int stage, Color accent) {
            //干透尾段与空位不冒泡；频率一档一级：1/8 → 1/5
            if (stage <= 0 || puddle.timeLeft <= 30 || !Main.rand.NextBool(11 - 3 * stage)) {
                return;
            }
            float xOff = Main.rand.NextFloat(-0.6f, 0.6f) * HalfWidth(puddle);
            PRTLoader.NewParticle<PRT_KikasaInkBead>(
                puddle.Center + new Vector2(xOff, -2f),
                new Vector2(0f, -Main.rand.NextFloat(0.5f, 1.1f)),
                Main.rand.NextBool(3) ? FoamPale : accent,
                Main.rand.NextFloat(0.16f, 0.24f + 0.05f * stage))?.Configure(Main.rand.Next(12, 20));
        }
    }

    /// <summary>
    /// 潦·溢流波：满潦时自洼身向两侧拍出的低平涌水，短寿贴地滑行，
    /// 对撞上的敌人拍一记半份洼伤。低而阔，区别于汐的立浪；各端本地绘制
    /// </summary>
    internal class FuLaoOverflowSurge : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private const int LifeFrames = 30;

        private float life;

        /// <summary>确定性相位：波身抖动各端一致</summary>
        private float Seed => Projectile.identity * 0.7391f % 3.71f;

        public override void SetDefaults() {
            Projectile.width = 52;
            Projectile.height = 20;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.penetrate = -1;
            Projectile.timeLeft = LifeFrames;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override void AI() {
            life++;
            //漫出来的水只摊不冲：比浪更快耗散
            Projectile.velocity *= 0.93f;
            Projectile.velocity.Y = 0f;

            if (Main.dedServ) {
                return;
            }
            //前缘细沫：低平的水线，不起浪冠
            if (Main.rand.NextBool(3)) {
                float dir = Projectile.velocity.X >= 0f ? 1f : -1f;
                PRTLoader.NewParticle<PRT_KikasaInkBead>(
                    Projectile.Center + new Vector2(dir * 16f, -2f) + Main.rand.NextVector2Circular(5f, 2f),
                    new Vector2(dir * Main.rand.NextFloat(0.6f, 1.6f), -Main.rand.NextFloat(0.2f, 0.8f)),
                    FuLaoFX.FoamPale, Main.rand.NextFloat(0.13f, 0.2f))?.Configure(Main.rand.Next(9, 14));
            }
        }

        /// <summary>低平涌水：沼深垫底、沼青水身、一线浅沫贴面——水在漫，不在扑</summary>
        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = CWRAsset.Extra_98?.Value;
            if (tex == null) {
                return false;
            }
            float t = life / (float)LifeFrames;
            float alpha = MathF.Sin(MathHelper.Pi * MathF.Min(t * 1.5f, 1f));
            if (alpha <= 0.02f) {
                return false;
            }
            float dir = Projectile.velocity.X >= 0f ? 1f : -1f;
            float wob = 1f + 0.04f * MathF.Sin(life * 0.45f + Seed * 4f);
            Vector2 pos = Projectile.Center - Main.screenPosition;
            Vector2 origin = tex.Size() * 0.5f;

            Main.EntitySpriteDraw(tex, pos, null, new Color(20, 40, 32) * (alpha * 0.7f),
                0f, origin, new Vector2(50f, 14f) * wob / tex.Width, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(tex, pos + new Vector2(dir * 4f, -2f), null,
                new Color(92, 156, 134) * (alpha * 0.75f), 0f, origin,
                new Vector2(38f, 10f) * wob / tex.Width, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(tex, pos + new Vector2(dir * 8f, -5f), null,
                FuLaoFX.FoamPale * (alpha * 0.7f), 0f, origin,
                new Vector2(24f, 4f) * wob / tex.Width, SpriteEffects.None, 0);
            return false;
        }
    }
}
