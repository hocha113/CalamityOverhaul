using CalamityMod.Buffs.DamageOverTime;
using CalamityMod.Particles;
using CalamityOverhaul.Common;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Utilities;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged
{
    internal class NurgleBee : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;

        protected List<Bee> bees = new List<Bee>();

        private SlotId beesSound = SlotId.Invalid;

        private int Time {
            get => (int)Projectile.ai[0];
            set => Projectile.ai[0] = value;
        }

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
            if (Time == 0) {
                beesSound = SoundEngine.PlaySound(ModSound.Bees with { SoundLimitBehavior = SoundLimitBehavior.IgnoreNew }, Projectile.Center);
            }

            float toPlayerLeng = Projectile.Center.Distance(Main.player[Projectile.owner].Center);
            if (!CWRUtils.isServer) {//因为蜜蜂云是纯视觉效果，因此不需要在服务器上运行相关代码，因为服务器看不见这些
                if (Projectile.timeLeft > 60 && Projectile.numHits == 0 && toPlayerLeng <= 1800) {
                    for (int i = 0; i < Main.rand.Next(2, 3); i++) {
                        bees.Add(new Bee(Projectile, Projectile.Center + CWRUtils.randVr(Projectile.width + 10), Projectile.velocity, Main.rand.Next(37, 60)
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
                bool LowVel = Main.rand.NextBool() ? false : true;
                FlameParticle fire = new FlameParticle(Projectile.Center + CWRUtils.randVr(13), 20, Main.rand.NextFloat(0.1f, 0.3f), 0.05f
                    , Color.YellowGreen * (LowVel ? 1.2f : 0.5f), Color.DarkGreen * (LowVel ? 1.2f : 0.5f));
                fire.Velocity = new Vector2(Projectile.velocity.X * 0.8f, -10).RotatedByRandom(0.005f)
                    * (LowVel ? Main.rand.NextFloat(0.4f, 0.65f) : Main.rand.NextFloat(0.8f, 1f));
                GeneralParticleHandler.SpawnParticle(fire);
            }
            if (Projectile.timeLeft < 330) {
                NPC target = Projectile.Center.InPosClosestNPC(450);
                if (target != null) {
                    Projectile.ChasingBehavior2(target.Center, 0.995f, 0.12f);
                }
            }

            if (SoundEngine.TryGetActiveSound(beesSound, out var sound)) {
                sound.Position = Projectile.position;
            }

            Time++;
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            if (Math.Abs(oldVelocity.Y) < 1.2f)
                oldVelocity.Y = Math.Sign(oldVelocity.Y) * 1.2f;
            Projectile.velocity = new Vector2(Main.rand.NextFloat(-0.5f, 0.5f), -oldVelocity.Y);
            return false;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(ModContent.BuffType<Plague>(), 6000);
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            target.AddBuff(ModContent.BuffType<Plague>(), 600);
        }

        public override void OnKill(int timeLeft) {
            if (!CWRUtils.isServer) {
                bees.Clear();
            }

            if (SoundEngine.TryGetActiveSound(beesSound, out var sound)) {
                sound.Stop();
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
