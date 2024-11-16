using CalamityMod.NPCs.Providence;
using CalamityOverhaul.Content.Buffs;
using CalamityOverhaul.Content.Particles;
using InnoVault.PRT;
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

        private void SpanDragonsFireEffect(float maxNum) {
            for (int i = 0; i < maxNum; i++) {
                Vector2 spanPos = (MathHelper.TwoPi / maxNum * i + Projectile.ai[0] * 0.1f).ToRotationVector2() * Projectile.ai[1] + Projectile.Center;
                PRT_LavaFire lavaFire = new PRT_LavaFire {
                    Velocity = new Vector2(0, -3),
                    Position = spanPos,
                    Scale = Main.rand.NextFloat(0.2f, 0.3f) * (1 + Projectile.ai[1] * 0.006f),
                    maxLifeTime = 15,
                    minLifeTime = 10
                };
                PRTLoader.AddParticle(lavaFire);
            }
        }

        private void SpanDragonsWordCut() {
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

        private void InOwner() {
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
        }

        private void UpdateSakura() {
            if (DownRight && Owner.CheckMana(Owner.GetItem())) {
                if (Owner.name == "Sakura") {
                    Owner.AddBuff(ModContent.BuffType<EXHellfire>(), 60);
                    if (Main.rand.NextBool(300)) {
                        Owner.AddBuff(BuffID.Darkness, 60);
                    }
                }

                Owner.statMana -= 1;
                Owner.manaRegenDelay = 6;
                if (Projectile.ai[1] < 660) {
                    Projectile.ai[1] += 2;
                }
            }
            else {
                Projectile.ai[1] -= 6;
                if (Projectile.ai[1] <= 0) {
                    Projectile.Kill();
                }
            }
        }

        public override void AI() {
            InOwner();
            UpdateSakura();

            if (Projectile.ai[1] >= 0) {
                SpanDragonsFireEffect(300);
                SpanDragonsWordCut();
            }

            Projectile.ai[0]++;
        }
    }
}
