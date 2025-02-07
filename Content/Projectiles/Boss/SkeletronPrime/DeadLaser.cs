using CalamityMod;
using CalamityMod.Events;
using CalamityMod.Graphics.Primitives;
using CalamityOverhaul.Content.Buffs;
using CalamityOverhaul.Content.Particles;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.Graphics.Shaders;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Boss.SkeletronPrime
{
    internal class DeadLaser : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder2;
        private const float timeLeft = 900;
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
                BasePRT spark = new PRT_Spark(Projectile.Center
                    , Projectile.velocity.RotatedByRandom(0.3f) * 0.7f, false, 16, Main.rand.NextFloat(0.6f, 0.8f), Color.Gold);
                PRTLoader.AddParticle(spark);
            }
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            target.AddBuff(ModContent.BuffType<HellburnBuff>(), 60);
            for (int i = 0; i < 13; i++) {
                BasePRT spark = new PRT_Spark(Projectile.Center
                    , CWRUtils.randVr(13, 23), false, 26, Main.rand.NextFloat(2.6f, 2.8f), Color.Gold);
                PRTLoader.AddParticle(spark);
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
            Color mainColor = VaultUtils.MultiStepColorLerp((Main.GlobalTimeWrappedHourly * 2f + localIdentityOffset) % 1f
                , Color.Red, Color.Gold, Color.Goldenrod, Color.OrangeRed, Color.DarkRed);
            Color secondaryColor = VaultUtils.MultiStepColorLerp((Main.GlobalTimeWrappedHourly * 2f + localIdentityOffset + 0.2f) % 1f
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
