using CalamityMod.Projectiles.Melee;
using CalamityOverhaul.Common;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee.Rapiers
{
    internal class MirrorBladeRapier : BaseRapiers
    {
        public override string Texture => CWRConstant.Cay_Wap_Melee + "MirrorBlade";
        public override string GlowPath => CWRConstant.Cay_Wap_Melee + "MirrorBlade";
        public override void SetRapiers() {
            PremanentToSkialthRot = 10;
            overHitModeing = 73;
            SkialithVarSpeedMode = 3;
            StabbingSpread = 0.25f;
            ShurikenOut = SoundID.Item1 with { Pitch = 0.7f };
        }

        public override void ExtraShoot() {
            if (HitNPCs.Count > 0) {
                return;
            }
            int proj = Projectile.NewProjectile(Projectile.GetSource_FromAI(), Projectile.Center, Projectile.velocity * Main.rand.NextFloat(10, 16)
                , ModContent.ProjectileType<MirrorBlast>(), Projectile.damage, Projectile.knockBack, Projectile.owner);
            Main.projectile[proj].rotation = Main.projectile[proj].velocity.ToRotation();
        }
    }
}
