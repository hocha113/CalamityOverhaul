using CalamityMod;
using System;
using System.Linq;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee.HeldProjectiles
{
    internal class WraithBeam : ModProjectile
    {
        public override string Texture => CWRConstant.Item_Rogue + "WraithKunai";
        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 10;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 1;
        }

        public override void SetDefaults() {
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = 13;
            Projectile.alpha = 255;
            Projectile.timeLeft = 500;
            Projectile.light = 1f;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 10;
            Projectile.extraUpdates = 1;
        }

        private void SpawnTerrorBlast() {
            int type = ModContent.ProjectileType<TerrorExplode>();
            int damage = (int)(Projectile.damage * 0.3f);
            Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, Vector2.Zero, type, damage, Projectile.knockBack, Projectile.owner);
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            if (Projectile.ai[0] == 0) {
                Projectile.numHits++;
                if (Projectile.IsOwnedByLocalPlayer() && Projectile.numHits < 6 && Projectile.ai[0] == 0) {
                    SpawnTerrorBlast();
                }

                if (Projectile.velocity.X != oldVelocity.X)
                    Projectile.velocity.X = -oldVelocity.X;

                if (Projectile.velocity.Y != oldVelocity.Y)
                    Projectile.velocity.Y = -oldVelocity.Y;

                if (Projectile.numHits > 12) {
                    Projectile.Kill();
                }

                return false;
            }
            else {
                for (int i = 0; i < 3; i++) {
                    Vector2 pos = Main.rand.NextFloat(MathHelper.TwoPi).ToRotationVector2() * Main.rand.Next(-200, 200) + Projectile.position;
                    int num = Dust.NewDust(pos, 1, 1, DustID.RedTorch, 0f, 0f, 0, default, 2.5f);
                    Main.dust[num].noGravity = true;
                    Main.dust[num].velocity *= 3f;
                    num = Dust.NewDust(pos, 2, 2, DustID.RedTorch, 0f, 0f, 100, default, 1.5f);
                    Main.dust[num].velocity *= 2f;
                    Main.dust[num].noGravity = true;
                }

                if (Math.Abs(oldVelocity.Y) > Math.Abs(oldVelocity.X)) {
                    Projectile.velocity.X = Projectile.velocity.X > 0 ? 20 : -20;
                }
                else {
                    Projectile.velocity.Y = Projectile.velocity.Y > 0 ? 20 : -20;
                }

                return false;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (Projectile.IsOwnedByLocalPlayer()) {
                switch (Projectile.ai[0]) {
                    case 0:
                        Projectile.localAI[0] = 1f;
                        SpawnTerrorBlast();
                        break;
                    case 1:
                        if (Projectile.numHits == 0) {
                            Vector2 toTarget = Main.player[Projectile.owner].Center.To(target.Center);
                            float rot = toTarget.ToRotation();
                            float offsetrot = MathHelper.ToRadians(35);
                            float lengs = toTarget.Length() / 2;
                            if (lengs > 160) {
                                lengs = 160;
                            }
                            Vector2 pos = Main.rand.NextFloat(rot - offsetrot, rot + offsetrot)
                                    .ToRotationVector2() * lengs + Main.player[Projectile.owner].Center;
                            Projectile.NewProjectileDirect(Projectile.GetSource_FromAI(), pos, pos.To(target.Center).UnitVector() * 17,
                                Type, Projectile.damage, 0, Projectile.owner, 2, Main.rand.Next(2)).timeLeft = 60;
                        }
                        break;
                }
            }
        }

        public override void AI() {
            Projectile.alpha -= 40;
            if (Projectile.alpha < 0) {
                Projectile.alpha = 0;
            }

            if (Projectile.ai[0] == 1) {
                Projectile.velocity *= 1.002f;
            }
            if (Projectile.ai[0] == 2) {
                if (Projectile.tileCollide) {
                    Projectile.tileCollide = false;
                }
                if (Projectile.numHits > 2) {
                    Projectile.Kill();
                }

                NPC target = Projectile.Center.FindClosestNPC(600);
                if (target != null && Projectile.timeLeft < 470) {
                    Projectile.ChasingBehavior(target.Center, Projectile.velocity.Length());
                    Projectile.velocity *= 1.007f;
                }
                if (Projectile.numHits > 0) {
                    Projectile.Kill();
                }
            }

            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            if (CWRLoad.WormBodys.Contains(target.type)) {
                modifiers.FinalDamage /= 10;
            }
        }

        public override Color? GetAlpha(Color lightColor) {
            switch (Projectile.ai[0]) {
                case 0:
                    return new Color(255, 0, 0, Projectile.alpha);
                case 1:
                    return new Color(225, 140, 150, Projectile.alpha);
                case 2:
                    switch (Projectile.ai[1]) {
                        case 0:
                            return new Color(25, 140, 250, Projectile.alpha);
                        case 1:
                            return new Color(125, 10, 150, Projectile.alpha);
                        case 2:
                            return new Color(255, 140, 0, Projectile.alpha);
                    }
                    break;
            }
            return new Color(255, 0, 0, Projectile.alpha);

        }

        public override bool PreDraw(ref Color lightColor) {
            if (Projectile.timeLeft > 595) {
                return false;
            }

            CalamityUtils.DrawAfterimagesCentered(Projectile, ProjectileID.Sets.TrailingMode[Projectile.type], lightColor, 2);
            return false;
        }

        public override void OnKill(int timeLeft) {
            if (Projectile.localAI[0] == 0f && Projectile.ai[0] == 0) {
                SpawnTerrorBlast();
            }
        }
    }
}
