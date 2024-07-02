using CalamityOverhaul.Content.Items.Placeable;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.AmmoBoxs
{
    internal class DragonBreathHeld : BaseHeldBox
    {
        public override string Texture => CWRConstant.Item + "Placeable/DBCBoxHeld";
        public override void SetBox() {
            TargetItemID = ModContent.ItemType<DragonBreathBox>();
            AmmoBoxID = ModContent.ProjectileType<DragonBreathBoxProj>();
            MaxCharge = 400;
            DrawBoxOffsetPos = new Microsoft.Xna.Framework.Vector2(0, 2);
        }
    }
}
