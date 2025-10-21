using Microsoft.Xna.Framework.Graphics;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee
{
    internal class OniMachete : ModItem
    {
        public override string Texture => CWRConstant.Item_Melee + "OniMachete";
        [VaultLoaden("@CalamityMod/NPCs/SupremeCalamitas/")]
        public static Texture2D SepulcherForearm;//反射加载手臂纹理
        [VaultLoaden("@CalamityMod/NPCs/SupremeCalamitas/")]
        public static Texture2D SepulcherHand;//反射加载手掌纹理，正面朝下
    }
}
