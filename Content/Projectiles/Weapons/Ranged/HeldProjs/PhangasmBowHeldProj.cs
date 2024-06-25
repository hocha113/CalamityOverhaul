using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.Core;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class PhangasmBowHeldProj : BaseBow
    {
        public override string Texture => "CalamityMod/Items/Weapons/Ranged/Phangasm";
        public override int targetCayItem => ModContent.ItemType<CalamityMod.Items.Weapons.Ranged.Phangasm>();
        public override int targetCWRItem => ModContent.ItemType<PhangasmEcType>();
        public override void SetRangedProperty() {
            BowArrowDrawNum = 3;
            HandFireDistance = 22;
            DrawArrowMode = -24;
        }
        public override void BowShoot() {
            for (int i = 0; i < 3; i++) {
                int proj = Projectile.NewProjectile(Source, Projectile.Center + ShootVelocity.GetNormalVector() * (-1 + i) * 15
                    , ShootVelocity.RotatedBy(Main.rand.NextFloat(-0.03f, 0.03f)) * Main.rand.NextFloat(0.8f, 1.12f)
                    , AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0, Projectile.whoAmI);
                Main.projectile[proj].noDropItem = true;
                Main.projectile[proj].CWR().SpanTypes = (byte)SpanTypesEnum.Phantom;
            }
        }
    }
}
