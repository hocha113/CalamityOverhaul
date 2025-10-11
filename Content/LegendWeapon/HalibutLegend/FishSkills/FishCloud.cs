using Microsoft.Xna.Framework.Graphics;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.FishSkills
{
    internal class FishCloud : FishSkill
    {
        [VaultLoaden(CWRConstant.Masking)]
        public static Texture2D Fog;//使用反射加载一个烟雾的灰度图，大小256*256，适合用于复合出云雾效果
    }
}
