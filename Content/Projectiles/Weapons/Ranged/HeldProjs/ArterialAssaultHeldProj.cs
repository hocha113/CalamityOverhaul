using CalamityMod;
using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.Core;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class ArterialAssaultHeldProj : BaseBow
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "ArterialAssault";
        public override int targetCayItem => ModContent.ItemType<ArterialAssault>();
        public override int targetCWRItem => ModContent.ItemType<ArterialAssaultEcType>();
        private int fireIndex;
        public override void SetShootAttribute() {
            Item.useTime = 3;
            if (++fireIndex > 6) {
                Item.useTime = 30;
                fireIndex = 0;
            }
        }
        public override void BowShoot() {
            _ = Owner.RotatedRelativePoint(Owner.GetPlayerStabilityCenter(), true);
            Vector2 realPlayerPos = new Vector2(Owner.position.X + Owner.width * 0.5f + -(float)Owner.direction + (Main.mouseX + Main.screenPosition.X - Owner.position.X), Owner.MountedCenter.Y - 600f);
            realPlayerPos.X = (realPlayerPos.X + Owner.Center.X) / 2f;
            realPlayerPos.Y -= 100f;
            Vector2 vr = realPlayerPos.To(Main.MouseWorld).UnitVector() * ShootSpeedModeFactor;

            int proj = Projectile.NewProjectile(Source, realPlayerPos, vr
                , AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
            Main.projectile[proj].CWR().SpanTypes = (byte)ShootSpanTypeValue;
            Main.projectile[proj].Calamity().allProjectilesHome = true;
            Main.projectile[proj].rotation = Main.projectile[proj].velocity.ToRotation() + MathHelper.PiOver2;
        }
    }
}
