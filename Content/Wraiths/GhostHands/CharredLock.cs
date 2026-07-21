using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Wraiths.GhostHands
{
    /// <summary>
    /// 长命锁，可攥之物。喂食判定在 GhostHandActor；投放见 GhostHandSite
    /// </summary>
    internal sealed class CharredLock : ModItem
    {
        //占位贴图
        public override string Texture => "Terraria/Images/Item_" + ItemID.ShadowKey;

        //上线闸关不加载
        public override bool IsLoadingEnabled(Mod mod) => Runtime.WraithDirector.LiveContentEnabled;

        public override void SetDefaults() {
            Item.width = 26;
            Item.height = 26;
            Item.maxStack = 1;
            Item.rare = ItemRarityID.Blue;
            Item.value = 0;
        }
    }
}
