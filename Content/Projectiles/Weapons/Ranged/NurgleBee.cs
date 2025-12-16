using CalamityOverhaul.Content.Projectiles.Others;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged
{
    internal class NurgleBee : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;

        protected List<Bee> bees = [];

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 32;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.friendly = true;
            Projectile.MaxUpdates = 3;
            Projectile.timeLeft = 390;
            Projectile.penetrate = 6;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 12;
        }

        public override void AI() {
            float toPlayerLeng = Projectile.Center.Distance(Main.player[Projectile.owner].Center);
            if (!VaultUtils.isServer) {//因为蜜蜂云是纯视觉效果，因此不需要在服务器上运行相关代码，因为服务器看不见这些
                if (Projectile.timeLeft > 60 && Projectile.numHits == 0 && toPlayerLeng <= 1800) {
                    for (int i = 0; i < Main.rand.Next(2, 3); i++) {
                        bees.Add(new Bee(Projectile, Projectile.Center + VaultUtils.RandVr(Projectile.width + 10), Projectile.velocity, Main.rand.Next(37, 60)
                            , Color.White, Projectile.rotation, Main.rand.NextFloat(0.9f, 1.3f), 1, Main.rand.Next(4)));
                    }
                }
                bees.RemoveAll((Bee b) => !b.Active);
                foreach (Bee bee in bees) {
                    bee.Update();
                    if (Main.rand.NextBool(13)) {
                        int dustType = 89;
                        int plague = Dust.NewDust(bee.Center, 1, 1, dustType, bee.Velocity.X * 0.2f, bee.Velocity.Y * 0.2f, 100, default, bee.Scale);
                        Dust dust = Main.dust[plague];
                        dust.scale *= 0.6f;
                        dust.noGravity = true;
                    }
                }
            }
            if (Main.rand.NextBool(3)) {
                CWRRef.NurgleBeeEffect(Projectile, Main.rand.NextBool());
            }
            if (Projectile.timeLeft < 330) {
                NPC target = Projectile.Center.FindClosestNPC(450);
                if (target != null) {
                    Projectile.SmoothHomingBehavior(target.Center, 0.995f, 0.12f);
                }
            }
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            if (Math.Abs(oldVelocity.Y) < 1.2f)
                oldVelocity.Y = Math.Sign(oldVelocity.Y) * 1.2f;
            Projectile.velocity = new Vector2(Main.rand.NextFloat(-0.5f, 0.5f), -oldVelocity.Y);
            return false;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(CWRID.Buff_Plague, 6000);
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            target.AddBuff(CWRID.Buff_Plague, 600);
        }

        public override void OnKill(int timeLeft) {
            if (!VaultUtils.isServer) {
                bees.Clear();
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D value = CWRUtils.GetT2DValue(CWRConstant.Projectile + "Bee");
            foreach (Bee bee in bees) {
                bee.Draw(Main.spriteBatch, value);
            }
            return false;
        }
    }
}
