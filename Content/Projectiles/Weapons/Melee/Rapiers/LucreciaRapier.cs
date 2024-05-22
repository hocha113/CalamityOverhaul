using CalamityOverhaul.Common;
using Terraria.ModLoader;
using Terraria;
using Microsoft.Xna.Framework;
using CalamityMod.Projectiles.Melee;
using Terraria.ID;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee.Rapiers
{
    internal class LucreciaRapier : BaseRapiers
    {
        public override string Texture => CWRConstant.Cay_Wap_Melee + "Lucrecia";
        public override string GlowPath => CWRConstant.Cay_Wap_Melee + "Lucrecia";
        public override void SetRapiers() {
            PremanentToSkialthRot = 16;
            overHitModeing = 73;
            SkialithVarSpeedMode = 3;
            StabbingSpread = 0.2f;
            StabAmplitudeMin = 65;
            StabAmplitudeMax = 165;
            ShurikenOut = SoundID.Item1 with { Pitch = 0.3f };
        }

        public override void ExtraShoot() {
            if (HitNPCs.Count > 0) {
                return;
            }
            float overmode = Main.rand.NextFloat(0.8f, 1.15f);
            if (Main.rand.NextBool(5)) {
                overmode *= 3;
            }
            int proj = Projectile.NewProjectile(Projectile.GetSource_FromAI(), Projectile.Center, Projectile.velocity * 16 * overmode
                , ModContent.ProjectileType<DNA>(), Projectile.damage, Projectile.knockBack, Projectile.owner);
            Main.projectile[proj].rotation = Main.projectile[proj].velocity.ToRotation();
        }
    }
}
