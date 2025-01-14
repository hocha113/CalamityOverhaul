using CalamityMod;
using CalamityMod.Items.Weapons.Ranged;
using CalamityMod.Projectiles.Ranged;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Magic;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.Core;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class DaemonsFlameHeldProj : BaseBow
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "DaemonsFlame";
        public override LocalizedText DisplayName => CWRUtils.SafeGetItemName<DaemonsFlameEcType>();
        public override int targetCayItem => ModContent.ItemType<DaemonsFlame>();
        public override int targetCWRItem => ModContent.ItemType<DaemonsFlameEcType>();
        public override void SetRangedProperty() {
            HandDistance = 28;
            HandFireDistance = 28;
            DrawArrowMode = -30;
            BowstringData.TopBowOffset = new Vector2(8, 4);
            BowstringData.DeductRectangle = new Rectangle(4, 4, 4, 114);
        }

        public override void BowShoot() {
            if (CalamityUtils.CheckWoodenAmmo(AmmoTypes, Owner)) {
                int types = ModContent.ProjectileType<FateCluster>();
                for (int i = 0; i < 4; i++) {
                    Vector2 vr = (Projectile.rotation + Main.rand.NextFloat(-0.1f, 0.1f)).ToRotationVector2() * Main.rand.Next(8, 18);
                    Vector2 pos = Projectile.Center + Main.rand.NextVector2Unit() * 8;
                    int doms = Projectile.NewProjectile(Source, pos, vr, types, WeaponDamage, WeaponKnockback, Owner.whoAmI);
                    Projectile newDoms = Main.projectile[doms];
                    newDoms.DamageType = DamageClass.Ranged;
                    newDoms.timeLeft = 120;
                    newDoms.ai[0] = 1;
                }
            }
            else {
                for (int i = 0; i < 4; i++) {
                    Vector2 vr = Projectile.rotation.ToRotationVector2() * Main.rand.Next(20, 30);
                    Vector2 pos = Projectile.Center + Main.rand.NextVector2Unit() * 8;
                    Projectile.NewProjectile(Source2, pos, vr, AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI);
                }
                Projectile.NewProjectile(Source, Projectile.Center, Projectile.rotation.ToRotationVector2() * 18
                    , ModContent.ProjectileType<DaemonsFlameArrow>(), WeaponDamage, WeaponKnockback, Owner.whoAmI);
            }
        }
    }
}
