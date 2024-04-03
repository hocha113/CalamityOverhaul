using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Placeable;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.AmmoBoxs
{
    internal class HighExplosiveHeld : BaseHeldBox
    {
        public override string Texture => CWRConstant.Item + "Placeable/HESHBoxHeld";
        public override void SetBox() {
            TargetItemID = ModContent.ItemType<HighExplosiveBox>();
            AmmoBoxID = ModContent.ProjectileType<HighExplosiveBoxProj>();
            MaxCharge = 400;
        }
    }
}
