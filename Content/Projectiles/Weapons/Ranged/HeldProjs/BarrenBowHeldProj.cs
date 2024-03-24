using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Ranged.Extras;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class BarrenBowHeldProj : BaseBow
    {
        public override string Texture => CWRConstant.Item_Ranged + "BarrenBow";
        public override int targetCayItem => ModContent.ItemType<BarrenBow>();
        public override int targetCWRItem => ModContent.ItemType<BarrenBow>();
        public override void SetRangedProperty() {
            base.SetRangedProperty();
        }
        public override void BowShoot() {
            int proj = Projectile.NewProjectile(Source, Projectile.Center, ShootVelocity.UnitVector() * 17
                    , AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
            Main.projectile[proj].CWR().SpanTypes = (byte)SpanTypesEnum.BarrenBow;
        }
    }
}
