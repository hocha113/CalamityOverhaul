using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.RangedModify.Core;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class PhangasmBowHeldProj : BaseBow
    {
        public override string Texture => "CalamityMod/Items/Weapons/Ranged/Phangasm";
        public override int TargetID => ModContent.ItemType<Phangasm>();
        public override void SetRangedProperty() {
            BowArrowDrawNum = 3;
            HandFireDistanceX = 22;
            DrawArrowMode = -24;
            BowstringData.DeductRectangle = new Rectangle(4, 12, 2, 58);
        }
        public override void BowShoot() {
            for (int i = 0; i < 5; i++) {
                Vector2 projRandomPos = Projectile.Center + Utils.RandomVector2(Main.rand, -15f, 15f);
                int proj = Projectile.NewProjectile(Source, projRandomPos
                    , ShootVelocity, AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI);
                Main.projectile[proj].CWR().SpanTypes = (byte)SpanTypesEnum.Phantom;
            }
        }
    }
}
