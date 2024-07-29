using CalamityMod;
using CalamityMod.NPCs.Providence;
using CalamityOverhaul.Content.Buffs;
using CalamityOverhaul.Content.Particles;
using CalamityOverhaul.Content.Particles.Core;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Magic.DragonsWordProj
{
    internal class DragonsWordMouse : BaseHeldProj
    {
        public override string Texture => CWRConstant.Placeholder;
        private Vector2 targetPos;
        public override void SetDefaults() {
            Projectile.width = Projectile.height = 122;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
        }

        public override void AI() {
            Owner.heldProj = Projectile.whoAmI;
            Owner.itemTime = 2;
            Owner.itemAnimation = 2;
            SetDirection();
            if (Projectile.ai[0] == 0) {
                SoundEngine.PlaySound(Providence.HolyRaySound);
                targetPos = InMousePos;
            }
            targetPos = Vector2.Lerp(targetPos, InMousePos, 0.1f);
            Projectile.Center = targetPos;
            if (DownRight && Owner.CheckMana(Owner.ActiveItem())) {
                if (Owner.name == "Sakura") {
                    Owner.AddBuff(ModContent.BuffType<HellfireExplosion>(), 60);
                    if (Main.rand.NextBool(300)) {
                        Owner.AddBuff(BuffID.Darkness, 60);
                    }
                }

                Owner.statMana -= 1;
                Owner.manaRegenDelay = 6;
                if (Projectile.ai[1] < 660) {
                    Projectile.ai[1]+=2;
                }
            }
            else {
                Projectile.ai[1]-=6;
                if (Projectile.ai[1] <= 0) {
                    Projectile.Kill();
                }
            }

            if (Projectile.ai[1] >= 0) {
                for (int i = 0; i < 300; i++) {
                    Vector2 spanPos = (MathHelper.TwoPi / 300f * i + Projectile.ai[0] * 0.1f).ToRotationVector2() * Projectile.ai[1] + Projectile.Center;
                    DRK_LavaFire lavaFire = new DRK_LavaFire();
                    lavaFire.Velocity = new Vector2(0, -3);
                    lavaFire.Position = spanPos;
                    lavaFire.Scale = Main.rand.NextFloat(0.2f, 0.3f) * (1 + Projectile.ai[1] * 0.006f);
                    lavaFire.maxLifeTime = 15;
                    lavaFire.minLifeTime = 10;
                    DRKLoader.AddParticle(lavaFire);
                }
                int num = 255;
                foreach (var npc in Main.npc) {
                    if (num <= 0) {
                        break;
                    }
                    if (!npc.Alives()) {
                        continue;
                    }
                    if (npc.friendly) {
                        continue;
                    }
                    if (npc.Distance(Projectile.Center) > Projectile.ai[1]) {
                        continue;
                    }
                    if (Projectile.ai[0] % 15 == 0) {
                        if (Owner.name == "Sakura") {
                            num *= 5;
                        }
                        int newDmg = (int)(Projectile.damage * (0.2f + num / 55f));
                        Projectile.NewProjectile(Projectile.GetSource_FromAI(), npc.Center, Vector2.Zero
                            , ModContent.ProjectileType<DragonsWordCut>(), newDmg, 2, Owner.whoAmI, 0f, 0.03f);
                        num--;
                    }
                }
            }

            Projectile.ai[0]++;
        }
    }
}
