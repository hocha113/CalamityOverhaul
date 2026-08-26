using CalamityOverhaul.Common;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Magic.Elysiums
{
    /// <summary>
    /// 神圣天雷：预兆的天光引线自穹顶垂落，随后圣雷劈落，
    /// 落点荡开贴地光环，余辉光柱缓缓散去。
    /// 四相：引线预兆(6f)→落雷拍→伤害窗(4f)→余辉(至寿命尽)
    /// </summary>
    internal class ElysiumThunder : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private const int OmenTime = 6;
        private const int DamageWindow = 4;
        private const int TotalLife = 42;
        private const float HitRadius = 112f;

        private int Timer => TotalLife - Projectile.timeLeft;
        private bool Struck => Timer >= OmenTime;

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 900;

        public override void SetDefaults() {
            Projectile.width = 40;
            Projectile.height = 40;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = TotalLife;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            if (Timer == 0) {
                SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.6f, Pitch = 0.6f }, Projectile.Center);
            }

            if (Timer == OmenTime) {
                //落雷拍
                SoundEngine.PlaySound(SoundID.Thunder with { Volume = 0.9f, Pitch = 0.3f }, Projectile.Center);
                SoundEngine.PlaySound(SoundID.Item122 with { Volume = 0.9f, Pitch = -0.1f }, Projectile.Center);
                Main.player[Projectile.owner].CWR().ScreenShakeValue =
                    Math.Max(Main.player[Projectile.owner].CWR().ScreenShakeValue, 3f);

                if (!Main.dedServ) {
                    Color boltColor = new(255, 240, 180);
                    //双股落雷叠出厚度
                    PRTLoader.NewParticle<PRT_SkyBolt>(Projectile.Center, Vector2.Zero, boltColor, 1f)
                        ?.Configure(Projectile.Center - new Vector2(0f, 760f), Projectile.Center, 26);
                    PRTLoader.NewParticle<PRT_SkyBolt>(Projectile.Center, Vector2.Zero, Color.White, 0.7f)
                        ?.Configure(Projectile.Center - new Vector2(30f, 720f), Projectile.Center, 20);

                    for (int i = 0; i < 10; i++) {
                        float angle = MathHelper.TwoPi * i / 10f;
                        PRTLoader.NewParticle<PRT_HeavenfallStar>(Projectile.Center
                            , angle.ToRotationVector2() * Main.rand.NextFloat(3f, 8f)
                            , boltColor, Main.rand.NextFloat(0.7f, 1.2f))?.Configure(false, Main.rand.Next(14, 24));
                    }
                    for (int i = 0; i < 6; i++) {
                        Vector2 vel = new(Main.rand.NextFloat(-1.2f, 1.2f), -Main.rand.NextFloat(2f, 6f));
                        PRTLoader.NewParticle<PRT_Light>(Projectile.Center + Main.rand.NextVector2Circular(30f, 10f)
                            , vel, boltColor, Main.rand.NextFloat(0.26f, 0.44f))?.Configure(Main.rand.Next(24, 40), 0.95f);
                    }
                }
            }

            float glow = Struck ? 1.1f * (1f - (Timer - OmenTime) / (float)(TotalLife - OmenTime)) : 0.3f;
            Lighting.AddLight(Projectile.Center, glow, glow * 0.92f, glow * 0.7f);
        }

        /// <summary>落点圆域，只在落雷后短窗内结算</summary>
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (Timer < OmenTime || Timer > OmenTime + DamageWindow) {
                return false;
            }
            Vector2 nearest = new(MathHelper.Clamp(Projectile.Center.X, targetHitbox.Left, targetHitbox.Right)
                , MathHelper.Clamp(Projectile.Center.Y, targetHitbox.Top, targetHitbox.Bottom));
            return Vector2.Distance(Projectile.Center, nearest) <= HitRadius;
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Texture2D px = VaultAsset.placeholder2?.Value;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (px == null || glow == null) {
                return false;
            }

            if (!Struck) {
                //预兆引线：自穹顶垂到落点的一缕光，快速增亮
                float omen = Timer / (float)OmenTime;
                Color lineColor = new Color(255, 240, 200) with { A = 0 } * (0.25f + omen * 0.5f);
                sb.Draw(px, drawPos - new Vector2(0.75f, 760f), new Rectangle(0, 0, 1, 1)
                    , lineColor, 0f, Vector2.Zero, new Vector2(1.5f + omen * 1.5f, 760f), SpriteEffects.None, 0f);
                sb.Draw(glow, drawPos, null, lineColor * 0.8f, 0f, glow.Size() / 2f
                    , 0.2f + omen * 0.2f, SpriteEffects.None, 0f);
                return false;
            }

            //落雷后：贴地冲击环 + 余辉光柱
            float prog = (Timer - OmenTime) / (float)(TotalLife - OmenTime);
            float fade = 1f - prog;

            ShockRingDraw.Draw(sb, Projectile.Center, MathHelper.Lerp(30f, HitRadius + 24f, VaultUtils.EaseOutCubic(Math.Min(prog * 2.2f, 1f)))
                , 8f, new Color(255, 246, 210), new Color(250, 220, 96), new Color(140, 100, 40)
                , fade * 0.85f, squish: 0.45f, innerGlow: 0.3f, timeSeed: Projectile.identity * 0.137f);

            //余辉光柱：向上渐窄渐淡
            Color pillarColor = new Color(255, 236, 170) with { A = 0 } * (0.4f * fade);
            sb.Draw(glow, drawPos + new Vector2(0f, -130f), null, pillarColor, 0f
                , glow.Size() / 2f, new Vector2(0.5f, 2.6f) * fade, SpriteEffects.None, 0f);
            sb.Draw(glow, drawPos, null, pillarColor * 1.2f, 0f
                , glow.Size() / 2f, 0.7f * fade + 0.15f, SpriteEffects.None, 0f);
            return false;
        }
    }
}
