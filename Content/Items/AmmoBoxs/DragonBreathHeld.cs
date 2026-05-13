using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.AmmoBoxs
{
    internal class DragonBreathHeld : BaseHeldBox
    {
        public override string Texture => CWRConstant.Item + "Placeable/DBCBox";
        public override void SetBox() {
            TargetItemID = ModContent.ItemType<DragonBreathBox>();
            AmmoBoxID = ModContent.ProjectileType<DragonBreathBoxProj>();
            MaxCharge = 80;
            DrawBoxOffsetPos = new Vector2(0, 2);
        }
    }
}
