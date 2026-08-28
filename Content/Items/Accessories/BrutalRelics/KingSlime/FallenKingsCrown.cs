using Terraria;

namespace CalamityOverhaul.Content.Items.Accessories.BrutalRelics.KingSlime
{
    /// <summary>
    /// 坠王之冕：史莱姆王残酷遗物。把王冠天坠反转成玩家能力——<br/>
    /// 免疫摔落伤害；空中按住下键(或自然坠落超25格)进入王冠坠击，
    /// 落地释放随坠落高度无上限成长的凝胶震荡波，落点留存减速凝胶领域。<br/>
    /// 状态机在 <see cref="FallenKingsCrownPlayer"/>，弹幕产物见同目录三弹幕
    /// </summary>
    internal class FallenKingsCrown : BaseBrutalRelic
    {
        public override void SetDefaults() {
            base.SetDefaults();
            //同期(史莱姆王段位)掉落物约2~4金，按系列基准取3-5倍
            Item.value = Item.buyPrice(0, 12, 0, 0);
        }

        public override void UpdateAccessory(Player player, bool hideVisual) {
            //免摔伤常驻；王冠现身是攻击信息而非纯时装，不吃可见性开关
            player.noFallDmg = true;
            player.GetModPlayer<FallenKingsCrownPlayer>().Equipped = true;
        }
    }
}
