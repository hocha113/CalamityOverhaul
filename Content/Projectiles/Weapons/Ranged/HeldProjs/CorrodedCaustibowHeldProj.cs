using CalamityMod.Items.Weapons.Ranged;
using CalamityMod.Projectiles.Ranged;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.Core;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class CorrodedCaustibowHeldProj : BaseBow
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "CorrodedCaustibow";
        public override int targetCayItem => ModContent.ItemType<CorrodedCaustibow>();
        public override int targetCWRItem => ModContent.ItemType<CorrodedCaustibowEcType>();
        public override void SetShootAttribute() {
            if (AmmoTypes == ProjectileID.WoodenArrowFriendly) {
                AmmoTypes = ModContent.ProjectileType<CorrodedShell>();
            }
        }
    }
}
