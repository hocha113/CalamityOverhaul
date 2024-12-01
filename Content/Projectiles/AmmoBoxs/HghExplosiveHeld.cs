using CalamityOverhaul.Content.Items.Placeable;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.AmmoBoxs
{
    internal class HghExplosiveHeld : BaseHeldBox
    {
        public override string Texture => CWRConstant.Item + "Placeable/HghExplosiveBox";
        public override void SetBox() {
            TargetItemID = ModContent.ItemType<HghExplosiveBox>();
            AmmoBoxID = ModContent.ProjectileType<HghExplosiveProj>();
            MaxCharge = 60;
            DrawBoxOffsetPos = new Vector2(0, 2);
        }
    }
}
