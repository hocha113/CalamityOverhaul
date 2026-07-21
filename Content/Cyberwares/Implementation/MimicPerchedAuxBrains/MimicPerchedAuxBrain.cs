using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.Cyberwares.Implementation.MimicPerchedAuxBrains
{
    /// <summary>
    /// 拟态栖置副脑，额叶槽
    /// <br/>四幻象环绕，致命攻击借幻象 FreeDodge，冲撞自爆=袭击者攻击×DamageScaling
    /// </summary>
    internal class MimicPerchedAuxBrain : BaseCyberware
    {
        public override CyberwareSlotCategory SlotCategory => CyberwareSlotCategory.FrontalCortex;

        public override int CapacityCost => 4;

        /// <summary>触发冷却帧</summary>
        public virtual int TriggerCooldown => 600;

        /// <summary>幻象自爆伤害倍率</summary>
        public virtual float DamageScaling => 2.5f;

        /// <summary>幻象环绕半径</summary>
        public virtual float OrbitRadius => 64f;

        public override void SetDefaults() {
            base.SetDefaults();
            Item.rare = ItemRarityID.Cyan;
            Item.value = Item.sellPrice(0, 8, 0, 0);
        }

        public override void OnEquip(Player player) {
            if (player.whoAmI == Main.myPlayer) {
                MimicPerchedAuxBrainPlayer.RequestRespawnPhantoms(player);
            }
        }

        public override void OnUnequip(Player player) {
            if (player.whoAmI == Main.myPlayer) {
                MimicPerchedAuxBrainPlayer.ClearPhantoms(player);
            }
        }
    }
}
