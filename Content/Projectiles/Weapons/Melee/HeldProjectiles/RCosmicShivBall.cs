using CalamityMod;
using CalamityMod.Buffs.DamageOverTime;
using CalamityMod.Projectiles.Melee;
using CalamityOverhaul.Common;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee.HeldProjectiles
{
    internal class RCosmicShivBall : ModProjectile
    {
        public NPC target;

        public const float maxDistanceToTarget = 540f;

        public bool initialized;

        public float startingVelocityY;

        public float randomAngleDelta;

        public const float explosionDamageMultiplier = 1.8f;

        public new string LocalizationCategory => "Projectiles.Melee";

        public override string Texture => CWRConstant.Projectile_Melee + "CosmicShivBlade";

        public override void SetDefaults() {
            Projectile.width = 12;
            Projectile.height = 12;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = 1;
            Projectile.tileCollide = false;
            Projectile.timeLeft = 220;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 10;
        }

        public int Status {
            set => Projectile.ai[2] = value;
            get => (int)Projectile.ai[2];
        }

        public override void AI() {
            Projectile.rotation = Projectile.velocity.ToRotation();
            if (Projectile.timeLeft < 190) Status = 1;

            if (Status == 0) {
                if (!initialized) {
                    target = Projectile.Center.ClosestNPCAt(540f);
                    startingVelocityY = Projectile.velocity.Y;
                    randomAngleDelta = Main.rand.NextFloat(0f, MathF.PI * 2f);
                    initialized = true;
                }

                Projectile.localAI[0] += 1f;
                if (Projectile.localAI[0] > 4f) {
                    for (int i = 0; i < 3; i++) {
                        int num = Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, DustID.ShadowbeamStaff, Projectile.direction * 2, 0f, 115, Color.White, 1.3f);
                        Main.dust[num].noGravity = true;
                        Main.dust[num].velocity *= 0f;
                    }

                    Projectile.velocity.Y *= 0.965f;
                }

                if (Projectile.localAI[0] % 30f == 0f) {
                    target = Projectile.Center.ClosestNPCAt(540f);
                }

                if (target != null) {
                    float num2 = 35f;
                    float num3 = 33.5f;
                    Vector2 vector = Projectile.SafeDirectionTo(target.Center, Vector2.UnitX) * num3;
                    Projectile.velocity = (Projectile.velocity * (num2 - 1f) + vector) / num2;
                }
                else {
                    Projectile.ai[0] += 1f;
                    Projectile.velocity.Y = startingVelocityY + (float)(Math.Cos(Projectile.ai[0] / 12.0 + randomAngleDelta) * 7.0);
                }
            }
            if (Status == 1) {
                NPC newTarget = Projectile.Center.FindClosestNPC(9900);
                if (newTarget != null) {
                    Projectile.ChasingBehavior(newTarget.Center, 27f, 16);
                }
            }
        }

        public void ShootCosmicShivBlade(NPC target) {
            for (int i = 0; i < 6; i++) {
                Vector2 spanPos = target.Center + CWRUtils.GetRandomVevtor(0, 360, CWRUtils.rands.Next(220, 300));
                Vector2 vr = spanPos.To(Main.MouseWorld).SafeNormalize(Vector2.Zero) * 38f;
                int proj = Projectile.NewProjectile(Projectile.GetSource_FromThis(), spanPos, vr, ModContent.ProjectileType<CosmicShivBlade>(), Projectile.damage, Projectile.knockBack * 0.1f, Projectile.owner);
                Main.projectile[proj].penetrate = 1;
                Main.projectile[proj].rotation = vr.ToRotation();
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            Player Owner = Main.player[Projectile.owner];
            if (Owner.Alives() == false) return;

            if (Owner.ownedProjectileCounts[ModContent.ProjectileType<CosmicRay>()] <= 12) {
                float mode = Owner.Center.To(target.Center).Length() * 0.5f;
                if (mode > 260) mode = 260;
                Vector2 spanPos = Owner.Center + (Owner.Center.To(target.Center).ToRotation() + MathHelper.ToRadians(CWRUtils.rands.Next(-45, 45))).ToRotationVector2() * mode;
                int proj = Projectile.NewProjectile(Projectile.parent(), spanPos, Vector2.Zero, ModContent.ProjectileType<CosmicRay>(), Projectile.damage * 2, Projectile.knockBack, Owner.whoAmI);
                Main.projectile[proj].rotation = Main.projectile[proj].Center.To(target.Center).ToRotation();
                Main.projectile[proj].netUpdate = true;
                Projectile.NewProjectile(Projectile.parent(), Owner.Center, Vector2.Zero, ModContent.ProjectileType<StarDust>(), 0, Projectile.knockBack, Owner.whoAmI, ai1: proj);
            }

            target.AddBuff(ModContent.BuffType<GodSlayerInferno>(), 60);
            ShootCosmicShivBlade(target);
        }

        private void CircularDamage(float radius) {
            if (Projectile.owner != Main.myPlayer) {
                return;
            }

            Player player = Main.player[Projectile.owner];
            for (int i = 0; i < Main.npc.Length; i++) {
                NPC nPC = Main.npc[i];
                if (!nPC.active || nPC.dontTakeDamage || nPC.friendly) {
                    continue;
                }

                float value = Vector2.Distance(Projectile.Center, nPC.Hitbox.TopLeft());
                float value2 = Vector2.Distance(Projectile.Center, nPC.Hitbox.TopRight());
                float value3 = Vector2.Distance(Projectile.Center, nPC.Hitbox.BottomLeft());
                if (MathHelper.Min(value2: Vector2.Distance(Projectile.Center, nPC.Hitbox.BottomRight()), value1: MathHelper.Min(MathHelper.Min(value, value2), value3)) <= radius) {
                    int num = (int)(Projectile.damage * 1.8f);
                    bool flag = Main.rand.Next(100) <= player.GetCritChance<MeleeDamageClass>() + 4f;
                    nPC.StrikeNPC(nPC.CalculateHitInfo(num, 0, flag));
                }
            }
        }

        public override void OnKill(int timeLeft) {
            float num = Main.rand.NextFloat(MathF.PI * 2f);
            int num2 = 5;
            float num3 = 12f;
            float scale = Main.rand.NextFloat(1f, 1.35f);
            for (float num4 = 0f; num4 < MathF.PI * 2f; num4 += 0.05f) {
                Vector2 value = num4.ToRotationVector2() * (2f + (float)(Math.Sin(num + num4 * num2) + 1.0) * num3) * Main.rand.NextFloat(0.95f, 1.05f);
                Dust.NewDustPerfect(Projectile.Center, 173, value, 0, default, scale).customData = 0.025f;
            }

            CircularDamage(80f);
        }

        public override bool PreDraw(ref Color lightColor) {
            Main.EntitySpriteDraw(
                CWRUtils.GetT2DValue(Texture),
                CWRUtils.WDEpos(Projectile.Center),
                CWRUtils.GetRec(CWRUtils.GetT2DValue(Texture)),
                Color.White,
                Projectile.rotation + MathHelper.PiOver4,
                CWRUtils.GetOrig(CWRUtils.GetT2DValue(Texture)),
                Projectile.scale,
                SpriteEffects.None,
                0
                );
            return false;
        }
    }
}
