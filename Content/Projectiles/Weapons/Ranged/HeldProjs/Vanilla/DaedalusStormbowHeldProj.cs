using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.Core;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs.Vanilla
{
    internal class DaedalusStormbowHeldProj : BaseBow
    {
        public override string Texture => CWRConstant.Placeholder;
        public override Texture2D TextureValue => TextureAssets.Item[ItemID.DaedalusStormbow].Value;
        public override int TargetID => ItemID.DaedalusStormbow;
        public override void SetRangedProperty() {
            CanRightClick = true;
            BowstringData.DeductRectangle = new Rectangle(6, 10, 2, 42);
        }
        public override void PostInOwner() {
            if (onFire || onFireR) {
                LimitingAngle();
            }
        }

        public override void SetShootAttribute() {
            if (onFire) {
                Item.useTime = 19;
            }
            else if (onFireR) {
                Item.useTime = 6;
            }
        }

        public override void BowShoot() => OrigItemShoot();

        public override void BowShootR() {
            Vector2 spanPos = Projectile.Center + new Vector2(Main.rand.Next(-20, 20) - InMousePos.To(Owner.Center).X / 2, Main.rand.Next(-832, -683));
            Vector2 vr = spanPos.To(Main.MouseWorld).UnitVector().RotatedBy(Main.rand.NextFloat(-0.12f, 0.12f)) * Main.rand.NextFloat(0.6f, 1.52f) * 13;
            Projectile p = Projectile.NewProjectileDirect(Source, spanPos, vr, AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
            p.tileCollide = false;
            p.extraUpdates += 1;
        }
    }
}
