using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.Core;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee.Rapiers
{
    internal class GrandGuardianRapier : BaseRapiers
    {
        public override string Texture => CWRConstant.Cay_Wap_Melee + "GrandGuardian";
        public override string GlowPath => CWRConstant.Item_Melee + "GrandGuardianGlow";
        public override void SetRapiers() {
            overHitModeing = 113;
            drawOrig = new Vector2(0, 130);
            StabbingSpread = 0.22f;
            ShurikenOut = CWRSound.ShurikenOut with { Pitch = -0.14f };
        }

        public override void ExtraShoot() {
            if (HitNPCs.Count > 0) {
                for (int i = 0; i < 3; i++) {
                    Vector2 spanPos = Projectile.Center;
                    spanPos += Projectile.velocity.UnitVector() * -526;
                    spanPos.Y += Main.rand.Next(-186, 166);
                    int proj2 = Projectile.NewProjectile(Projectile.GetSource_FromAI(), spanPos, Projectile.velocity.UnitVector() * 13
                    , ModContent.ProjectileType<GrandGuardianBeam>(), Projectile.damage / 2, Projectile.knockBack, Projectile.owner, ai2: 1);
                    Main.projectile[proj2].rotation = Main.projectile[proj2].velocity.ToRotation();
                }
                return;
            }
            Item.initialize();
            if (++Item.CWR().ai[0] > 1) {
                int proj = Projectile.NewProjectile(Projectile.GetSource_FromAI(), Projectile.Center, Projectile.velocity.UnitVector() * 13
                , ModContent.ProjectileType<GrandGuardianBeam>(), Projectile.damage, Projectile.knockBack, Projectile.owner);
                Main.projectile[proj].rotation = Main.projectile[proj].velocity.ToRotation();
                Item.CWR().ai[0] = 0;
            }
        }
    }
}
