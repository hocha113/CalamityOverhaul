using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Accessories.BrutalRelics.Skeletron
{
    /// <summary>
    /// 缚魂之腕：骷髅王残酷遗物。周身常驻诅咒领域（域内敌人受伤提高），
    /// 域内击杀收魂成环，魂魄替身格挡敌方弹幕，魂环集满凝聚幽灵巨手执行掌攫处刑。
    /// 状态与逻辑全在 <see cref="SoulbindingArmPlayer"/>，此处只做装备接线
    /// </summary>
    internal class SoulbindingArm : BaseBrutalRelic
    {
        public override void SetDefaults() {
            base.SetDefaults();
            //同期骷髅王掉落物（骷髅法杖/骨书 10~15 金）的 3~5 倍
            Item.value = Item.buyPrice(0, 45, 0, 0);
        }

        public override void UpdateAccessory(Player player, bool hideVisual) {
            SoulbindingArmPlayer mp = player.GetModPlayer<SoulbindingArmPlayer>();
            mp.DomainActive = true;
            mp.SourceItem = Item;
        }
    }
}
