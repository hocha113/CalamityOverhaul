﻿using CalamityMod.Buffs.DamageOverTime;
using CalamityMod.Items.Weapons.Rogue;
using CalamityMod.NPCs.Yharon;
using CalamityMod.Projectiles.Melee;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee.DawnshatterAzureProj
{
    internal class TheDaybreak : ModProjectile
    {
        public override string Texture => CWRConstant.Projectile_Melee + "DawnshatterAzureFire";
        public override void SetDefaults() {
            Projectile.width = 32;
            Projectile.height = 62;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.MaxUpdates = 2;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 150;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 20;
        }

        public override bool? CanDamage() => false;

        public override void AI() {
            VaultUtils.ClockFrame(ref Projectile.frame, 6, 3);
            Lighting.AddLight(Projectile.Center, Color.Red.ToVector3() * 1.7f);
            Projectile.rotation = Projectile.velocity.ToRotation() - MathHelper.PiOver2;
            Projectile.velocity *= 0.95f;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (Projectile.numHits == 0) {
                target.CWR().TheEndSunOnHitNum = true;
                _ = SoundEngine.PlaySound(Yharon.ShortRoarSound, target.position);
            }
            target.AddBuff(ModContent.BuffType<HolyFlames>(), 240);
        }

        public override void OnKill(int timeLeft) {
            float spread = 180f * 0.0174f;
            double startAngle = Math.Atan2(Projectile.velocity.X, Projectile.velocity.Y) - (spread / 2);
            double deltaAngle = spread / 8f;
            double offsetAngle;

            if (Projectile.IsOwnedByLocalPlayer() && Projectile.ai[2] == 0) {
                offsetAngle = startAngle + deltaAngle + 32f;
                _ = Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center.X, Projectile.Center.Y
                    , (float)(Math.Sin(offsetAngle) * 5f), (float)(Math.Cos(offsetAngle) * 5f)
                    , ModContent.ProjectileType<SandFire>(), Projectile.damage, Projectile.knockBack, Projectile.owner, 0f, 0f);
                _ = Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center.X, Projectile.Center.Y
                    , (float)(-Math.Sin(offsetAngle) * 5f), (float)(-Math.Cos(offsetAngle) * 5f)
                    , ModContent.ProjectileType<SandFire>(), Projectile.damage, Projectile.knockBack, Projectile.owner, 0f, 0f);
            }

            Lighting.AddLight(Projectile.Center, Color.Red.ToVector3() * 17f);
            Projectile.Explode(1220, Supernova.ExplosionSound with { Pitch = 0.8f });

            for (int i = 0; i < Projectile.ai[1]; i++) {
                BasePRT particle = new PRT_Light(Projectile.Center, VaultUtils.RandVr(3, 116), Main.rand.NextFloat(0.3f, 0.7f), Color.OrangeRed, 12, 0.2f);
                //不要在屏幕外面就消除了，否则玩家什么都看不到
                particle.ShouldKillWhenOffScreen = false;
                PRTLoader.AddParticle(particle);
                BasePRT particle2 = new PRT_Smoke(Projectile.Center + Projectile.velocity * Main.rand.NextFloat(0.3f, 1.7f), VaultUtils.RandVr(3, 16)
                    , VaultUtils.MultiStepColorLerp(Main.rand.NextFloat(), Color.Red, Color.DarkRed)
                    , 13, Main.rand.NextFloat(0.2f, 1.1f), 0.5f, 0.1f);
                particle2.ShouldKillWhenOffScreen = false;
                PRTLoader.AddParticle(particle2);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D texture = TextureAssets.Projectile[Type].Value;
            float rot = Projectile.rotation;
            Vector2 drawPosition = Projectile.Center - Main.screenPosition;
            Vector2 origin = VaultUtils.GetOrig(texture, 4);
            Main.EntitySpriteDraw(texture, drawPosition, texture.GetRectangle(Projectile.frame, 4), Projectile.GetAlpha(lightColor)
                , rot, origin, Projectile.scale * 0.7f, 0, 0);
            return false;
        }
    }
}
