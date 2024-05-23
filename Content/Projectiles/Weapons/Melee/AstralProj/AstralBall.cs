using CalamityMod;
using CalamityMod.Buffs.DamageOverTime;
using CalamityMod.Dusts;
using Microsoft.Xna.Framework;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee.AstralProj
{
    internal class AstralBall : ModProjectile
    {
        public new string LocalizationCategory => "Projectiles.Melee";
        public override string Texture => "CalamityMod/Projectiles/Enemy/MantisRing";

        private int tileCounter = 5;

        private Player player => Main.player[Projectile.owner];

        private Item astralBlade => player.ActiveItem();

        public override void SetStaticDefaults() {
            Main.projFrames[Projectile.type] = 3;
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 4;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 0;
        }

        public override void SetDefaults() {
            Projectile.width = 72;
            Projectile.height = 30;
            Projectile.friendly = true;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.timeLeft = 300;
            Projectile.penetrate = 1;
            Projectile.MaxUpdates = 2;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 7 * Projectile.MaxUpdates;
            Projectile.tileCollide = false;
        }

        public override void AI() {
            if (tileCounter > 0)
                tileCounter--;
            if (tileCounter <= 0)
                Projectile.tileCollide = true;

            Projectile.rotation = Projectile.velocity.ToRotation();
            CWRUtils.ClockFrame(ref Projectile.frame, 4, 2);
            Lighting.AddLight(Projectile.Center, Color.LightYellow.ToVector3());
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(ModContent.BuffType<AstralInfectionDebuff>(), 240);
            float angleStart = Main.rand.NextFloat(MathHelper.TwoPi);
            for (float angle = 0f; angle < MathHelper.TwoPi; angle += 0.01f) {
                Vector2 velocity = angle.ToRotationVector2() * (2f + (float)(Math.Sin(angleStart + angle * 3f) + 1) * 2.5f) * Main.rand.NextFloat(0.95f, 1.05f);
                Dust d = Dust.NewDustPerfect(target.Center, Main.rand.NextBool() ? ModContent.DustType<AstralBlue>() : ModContent.DustType<AstralOrange>(), velocity);
                d.customData = 0.025f;
                d.scale = 1.25f;
                d.noLight = false;
            }
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            if (target.lifeMax <= 0)
                return;

            float lifeRatio = MathHelper.Clamp(target.life / (float)target.lifeMax, 0f, 1f);
            float multiplier = MathHelper.Lerp(1f, 2f, lifeRatio);

            modifiers.SourceDamage *= multiplier;
            modifiers.Knockback *= multiplier;

            if (Main.rand.NextBool((int)MathHelper.Clamp((astralBlade.crit + player.GetCritChance<MeleeDamageClass>()) * multiplier, 0f, 99f), 100))
                modifiers.SetCrit();

            if (multiplier > 1.5f) {
                SoundEngine.PlaySound(SoundID.Item105, player.Center);
                bool blue = Main.rand.NextBool();
                float angleStart = Main.rand.NextFloat(0f, MathHelper.TwoPi);
                float var = 0.05f + (2f - multiplier);
                for (float angle = 0f; angle < MathHelper.TwoPi; angle += var) {
                    blue = !blue;
                    Vector2 velocity = angle.ToRotationVector2() * (2f + (float)(Math.Sin(angleStart + angle * 3f) + 1) * 2.5f) * Main.rand.NextFloat(0.95f, 1.05f);
                    Dust d = Dust.NewDustPerfect(target.Center, blue ? ModContent.DustType<AstralBlue>() : ModContent.DustType<AstralOrange>(), velocity);
                    d.customData = 0.025f;
                    d.scale = multiplier - 0.75f;
                    d.noLight = false;
                }
            }
        }

        public override void OnKill(int timeLeft) {
            SoundEngine.PlaySound(SoundID.Item1, Projectile.Center);

            for (int i = 0; i < 60; i++) {
                float angle = MathHelper.TwoPi * Main.rand.NextFloat(0f, 1f);
                Vector2 angleVec = angle.ToRotationVector2();
                float distance = Main.rand.NextFloat(14f, 36f);
                Vector2 off = angleVec * distance;
                off.Y *= (float)Projectile.height / Projectile.width;
                Vector2 pos = Projectile.Center + off;
                Dust d = Dust.NewDustPerfect(pos, ModContent.DustType<AstralBlue>(), angleVec * Main.rand.NextFloat(2f, 4f));
                d.customData = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            CalamityUtils.DrawAfterimagesCentered(Projectile, ProjectileID.Sets.TrailingMode[Projectile.type], lightColor, 1);
            return false;
        }
    }
}
