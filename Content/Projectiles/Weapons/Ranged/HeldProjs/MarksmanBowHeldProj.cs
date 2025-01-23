using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.Core;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class MarksmanBowHeldProj : BaseBow
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "MarksmanBow";
        public override int targetCayItem => ModContent.ItemType<MarksmanBow>();
        public override int targetCWRItem => ModContent.ItemType<MarksmanBowEcType>();
        public override void SetRangedProperty() {
            DrawArrowMode = -26;
            BowstringData.DeductRectangle = new Rectangle(6, 36, 2, 38);
        }
        public override void BowShoot() {
            for (int i = 0; i < 3; i++) {
                int proj = Projectile.NewProjectile(Source, Projectile.Center, ShootVelocity * (0.7f + i * 0.1f), AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
                Main.projectile[proj].MaxUpdates = 5;
                Main.projectile[proj].CWR().SpanTypes = (byte)SpanTypesEnum.Marksman;
                NetMessage.SendData(MessageID.SyncProjectile, -1, Owner.whoAmI, null, proj);
            }
        }
    }
}
