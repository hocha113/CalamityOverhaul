using CalamityMod.Graphics.Primitives;
using CalamityMod;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Graphics.Shaders;
using Terraria.ModLoader;
using Terraria.ID;
using System;
using CalamityOverhaul.Content.Particles.Core;
using CalamityOverhaul.Content.Particles;
using CalamityMod.Events;
using CalamityOverhaul.Content.Buffs;
using Terraria.Audio;

namespace CalamityOverhaul.Content.Projectiles.Boss.SkeletronPrime
{
    internal class DeadLaser : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder2;
        private float timeLeft => 900;
        private bool onSound;
        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 33;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }
        public override void SetDefaults() {
            Projectile.width = Projectile.height = 22;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.timeLeft = (int)timeLeft;
            Projectile.extraUpdates = 2;
            if (BossRushEvent.BossRushActive || Main.zenithWorld || Main.getGoodWorld) {
                Projectile.extraUpdates += 1;
            }
            Projectile.tileCollide = false;
            Projectile.maxPenetrate = Projectile.penetrate = 1;
            CooldownSlot = ImmunityCooldownID.Bosses;
        }

        public override void AI() {
            if (!onSound) {
                SoundEngine.PlaySound(SoundID.Item33 with { Pitch = 0.3f }, Projectile.Center);
                onSound = true;
            }
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
            Lighting.AddLight(Projectile.Center, Color.Gold.ToVector3());
            if (Main.rand.NextBool(6)) {
                CWRParticle spark = new HeavenfallStarParticle(Projectile.Center
                    , Projectile.velocity.RotatedByRandom(0.3f) * 0.7f, false, 16, Main.rand.NextFloat(0.6f, 0.8f), Color.Gold);
                CWRParticleHandler.AddParticle(spark);
            }
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            target.AddBuff(ModContent.BuffType<HellfireExplosion>(), 120);
            for (int i = 0; i < 13; i++) {
                CWRParticle spark = new HeavenfallStarParticle(Projectile.Center
                    , CWRUtils.randVr(13, 23), false, 26, Main.rand.NextFloat(1.6f, 2.8f), Color.Gold);
                CWRParticleHandler.AddParticle(spark);
            }
            Projectile.timeLeft = 30;
            Projectile.netUpdate = true;
        }

        public float PrimitiveWidthFunction(float completionRatio) {
            float sengs = 1f;
            if (Projectile.timeLeft < (timeLeft / 3f)) {
                sengs = Projectile.timeLeft / (timeLeft / 3f);
            }
            return (float)Math.Sin(completionRatio * Math.PI) * 30f * sengs;
        }

        public Color PrimitiveColorFunction(float _) => Color.DarkRed * Projectile.Opacity;

        public override bool PreDraw(ref Color lightColor) {
            float localIdentityOffset = Projectile.identity * 0.1372f;
            Color mainColor = CalamityUtils.MulticolorLerp((Main.GlobalTimeWrappedHourly * 2f + localIdentityOffset) % 1f
                , Color.Red, Color.Gold, Color.Goldenrod, Color.OrangeRed, Color.DarkRed);
            Color secondaryColor = CalamityUtils.MulticolorLerp((Main.GlobalTimeWrappedHourly * 2f + localIdentityOffset + 0.2f) % 1f
                , Color.Red, Color.Red, Color.Gold, Color.OrangeRed, Color.DarkRed);

            mainColor = Color.Lerp(Color.DarkRed, mainColor, Projectile.timeLeft / timeLeft);
            secondaryColor = Color.Lerp(Color.DarkRed, secondaryColor, Projectile.timeLeft / timeLeft);

            GameShaders.Misc["CalamityMod:HeavenlyGaleTrail"].SetMiscShaderAsset_1(ModContent.Request<Texture2D>(CWRConstant.Placeholder2));
            GameShaders.Misc["CalamityMod:HeavenlyGaleTrail"].UseImage2("Images/Extra_189");
            GameShaders.Misc["CalamityMod:HeavenlyGaleTrail"].UseColor(mainColor);
            GameShaders.Misc["CalamityMod:HeavenlyGaleTrail"].UseSecondaryColor(secondaryColor);
            GameShaders.Misc["CalamityMod:HeavenlyGaleTrail"].Apply();
            //非常好的改动，PrimitiveTrail的绘制非常烦杂，使用这种形式会是一个绝佳的选择
            PrimitiveRenderer.RenderTrail(Projectile.oldPos, new PrimitiveSettings(PrimitiveWidthFunction, PrimitiveColorFunction
                , (float _) => Projectile.Size / 2f, smoothen: true, pixelate: false, GameShaders.Misc["CalamityMod:HeavenlyGaleTrail"]), 33);
            return true;
        }
    }
}
