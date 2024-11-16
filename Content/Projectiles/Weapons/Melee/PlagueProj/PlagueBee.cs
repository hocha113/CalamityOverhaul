using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee.PlagueProj
{
    internal class PlagueBee : ModProjectile
    {
        public override string Texture => CWRConstant.Projectile + "Bee";

        protected List<Bee> bees = [];

        private int Time {
            get => (int)Projectile.ai[0];
            set => Projectile.ai[0] = value;
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 32;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.friendly = true;
            Projectile.timeLeft = 390;
            Projectile.penetrate = 6;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 12;
        }

        public override void AI() {
            if (!VaultUtils.isServer) {//因为蜜蜂云是纯视觉效果，因此不需要在服务器上运行相关代码，因为服务器看不见这些
                if (Projectile.timeLeft > 60 && Projectile.numHits == 0) {
                    for (int i = 0; i < Main.rand.Next(2, 3); i++) {
                        bees.Add(new Bee(Projectile, Projectile.Center + CWRUtils.randVr(Projectile.width + 10), Projectile.velocity, Main.rand.Next(37, 60)
                            , Color.White, Projectile.rotation, Main.rand.NextFloat(0.9f, 1.3f), 1, Main.rand.Next(4)));
                    }
                }
                bees.RemoveAll((Bee b) => !b.Active);
                //List<Bee> safeBees = bees.Select(bee => bee.Clone()).ToList();
                foreach (Bee bee in bees) {
                    bee.Update();
                    if (Main.rand.NextBool(3)) {
                        int dustType = 89;
                        int plague = Dust.NewDust(bee.Center, 1, 1, dustType, bee.Velocity.X * 0.2f, bee.Velocity.Y * 0.2f, 100, default, bee.Scale);
                        Dust dust = Main.dust[plague];
                        dust.scale *= 0.6f;
                        dust.noGravity = true;
                    }
                }
            }
            if (Projectile.timeLeft < 330) {
                NPC target = Projectile.Center.FindClosestNPC(450);
                if (target != null) {
                    Projectile.ChasingBehavior2(target.Center, 0.995f, 0.12f);
                }
            }
            if (Projectile.velocity.LengthSquared() < 184) {
                Projectile.velocity *= 1.01f;
            }
            Time++;
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            if (Math.Abs(oldVelocity.Y) < 1.2f)
                oldVelocity.Y = Math.Sign(oldVelocity.Y) * 1.2f;
            Projectile.velocity = new Vector2(Main.rand.NextFloat(-0.5f, 0.5f), -oldVelocity.Y);
            return false;
        }

        public override void OnKill(int timeLeft) {
            if (!VaultUtils.isServer) {
                bees.Clear();
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D value = CWRUtils.GetT2DValue(Texture);
            foreach (Bee bee in bees) {
                bee.Draw(Main.spriteBatch, value);
            }
            return false;
        }
    }
}
