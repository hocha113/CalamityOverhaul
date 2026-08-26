using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.ChargeBows
{
    /// <summary>
    /// 放血（族内失血减益，NPC 用）：约 7 点每秒的持续失血。
    /// 肌腱弓 T2 血栓箭与暗影木弓 T3 共用。图标复用原版流血
    /// </summary>
    internal class GsChargeBleedBuff : ModBuff
    {
        public override string Texture => "Terraria/Images/Buff_" + BuffID.Bleeding;
        public override string LocalizationCategory => "GodSmithChargeBows";

        public override void SetStaticDefaults() {
            Main.debuff[Type] = true;
            Main.buffNoSave[Type] = true;
        }

        public override void Update(NPC npc, ref int buffIndex)
            => npc.GetGlobalNPC<GsChargeBowNPC>().bleeding = true;
    }

    /// <summary>族内 NPC 状态载体：放血失血结算（服务端权威，lifeRegen 标准管线）</summary>
    internal class GsChargeBowNPC : GlobalNPC
    {
        public override bool InstancePerEntity => true;

        /// <summary>本帧带放血（由减益 Update 置位，ResetEffects 清零）</summary>
        internal bool bleeding;

        public override void ResetEffects(NPC npc) => bleeding = false;

        public override void UpdateLifeRegen(NPC npc, ref int damage) {
            if (!bleeding) {
                return;
            }
            if (npc.lifeRegen > 0) {
                npc.lifeRegen = 0;
            }
            //lifeRegen 每 2 点折 1 血每秒：14 即约 7 血每秒
            npc.lifeRegen -= 14;
            if (damage < 2) {
                damage = 2;
            }
        }
    }
}
