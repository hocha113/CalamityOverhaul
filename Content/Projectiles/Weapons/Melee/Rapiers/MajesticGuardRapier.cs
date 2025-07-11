using CalamityOverhaul.Common;
using CalamityOverhaul.Content.MeleeModify.Core;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee.Rapiers
{
    internal class MajesticGuardRapier : BaseRapiers
    {
        public override string Texture => CWRConstant.Cay_Wap_Melee + "MajesticGuard";
        public override string GlowPath => CWRConstant.Placeholder;
        public override void SetRapiers() {
            overHitModeing = 73;
            SkialithVarSpeedMode = 3;
            drawOrig = new Vector2(0, 100);
        }

        public override void ExtraShoot() {
            if (HitNPCs.Count > 0) {
                if (Main.rand.NextBool(2)) {
                    Owner.HealEffect(1);
                }
                CWRUtils.SplashDust(Projectile, 31, DustID.FireworkFountain_Yellow, DustID.FireworkFountain_Yellow
                    , 13, Color.Gold, EffectLoader.StreamerDust);
                return;
            }
            if (stabIndex % 2 != 0 || stabIndex < 1) {
                return;
            }
            int proj = Projectile.NewProjectile(Projectile.GetSource_FromAI(), Projectile.Center, Projectile.velocity.UnitVector() * 13
                , ModContent.ProjectileType<MajesticGuardBeam>(), Projectile.damage, Projectile.knockBack, Projectile.owner);
            Main.projectile[proj].rotation = Main.projectile[proj].velocity.ToRotation();
        }
    }
}
