using CalamityMod.Items.Fishing.BrimstoneCragCatches;
using Microsoft.Xna.Framework.Graphics;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.FishSkills
{
    internal class FishDrizzle : FishSkill
    {
        [VaultLoaden(CWRConstant.Masking)]
        public static Texture2D Fire = null;//火焰的纹理灰度图，总共4*4帧，也就是四行四列的帧图
        public override int UnlockFishID => ModContent.ItemType<DragoonDrizzlefish>();
    }
}
