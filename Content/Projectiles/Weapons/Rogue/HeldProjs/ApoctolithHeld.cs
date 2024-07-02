using CalamityMod.Projectiles.Rogue;
using CalamityMod;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using CalamityMod.Projectiles.Melee;
using System;
using CalamityMod.Particles;
using CalamityMod.Buffs.DamageOverTime;
using CalamityMod.Buffs.StatDebuffs;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Rogue.HeldProjs
{
    internal class ApoctolithHeld : BaseThrowable
    {
        public override string Texture => CWRConstant.Cay_Wap_Rogue + "Apoctolith";
        private bool hitTheHead = false;
        public override void FlyToMovementAI() {
            Projectile.rotation += 0.4f * Projectile.direction;
            Projectile.velocity.Y += 0.3f;
            if (Projectile.velocity.Y > 16f) {
                Projectile.velocity.Y = 16f;
            }
            if (Main.rand.NextBool(13)) {
                Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, DustID.DungeonSpirit
                    , Projectile.velocity.X * 0.25f, Projectile.velocity.Y * 0.25f, 150, default, 0.9f);
            }
            if (Math.Abs(Projectile.velocity.X) < 2 && ++Projectile.ai[2] > 30) {
                Projectile.hostile = true;
            }
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            return true;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(ModContent.BuffType<CrushDepth>(), 240);

            if (hit.Crit) {
                target.Calamity().miscDefenseLoss = Math.Min(target.defense, 15);
            }
            if (stealthStrike) {
                target.AddBuff(ModContent.BuffType<Eutrophication>(), 120);
            }                
        }

        public override void ModifyHitPlayer(Player target, ref Player.HurtModifiers modifiers) {
            modifiers.SetMaxDamage(50);
            hitTheHead = true;
            Projectile.Kill();
        }

        public override void OnKill(int timeLeft) {
            SoundEngine.PlaySound(SoundID.Tink, Projectile.position);
            int dust_splash = 0;
            while (dust_splash < 9) {
                Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, DustID.DungeonSpirit
                    , -Projectile.velocity.X * 0.15f, -Projectile.velocity.Y * 0.15f, 120, default, 1.5f);
                dust_splash += 1;
            }
            if (stealthStrike || hitTheHead) {
                if (Projectile.IsOwnedByLocalPlayer()) {
                    for (int i = 0; i < 6; i++) {
                        int proj = Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, new Vector2(6 + i, 0)
                        , ModContent.ProjectileType<Spindrift>(), Projectile.damage, 0, Owner.whoAmI);
                        int proj2 = Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, new Vector2(-6 - i, 0)
                            , ModContent.ProjectileType<Spindrift>(), Projectile.damage, 0, Owner.whoAmI);
                        Main.projectile[proj].scale += i * 0.3f;
                        Main.projectile[proj2].scale += i * 0.3f;
                    }
                }
                SoundEngine.PlaySound(SoundID.Splash with { PitchVariance = 2f }, Projectile.Center);
            }
        }
    }
}
