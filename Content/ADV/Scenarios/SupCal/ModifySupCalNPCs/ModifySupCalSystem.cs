using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.SupCal.ModifySupCalNPCs
{
    internal class ModifySupCalSystem : ModSystem
    {
        public override void PostUpdateNPCs() {
            if (NPC.AnyNPCs(CWRID.NPC_WITCH)) {
                int witch = NPC.FindFirstNPC(CWRID.NPC_WITCH);
                if (witch == -1) {
                    return;
                }
                bool hasEbn = false;
                foreach (var p in Main.ActivePlayers) {
                    //如果已经有人达成了永恒燃烧的现在结局，说明女巫已死，玩家替换女巫的位置
                    if (p.TryGetADVSave(out var save) && save.EternalBlazingNow) {
                        hasEbn = true;
                        //p.Teleport(Main.npc[witch].Center, 999);
                    }
                }
                if (hasEbn) {
                    Main.npc[witch].active = false;
                    Main.npc[witch].netUpdate = true;
                }
            }
            //全局生命周期更新，避免因为一些意外情况导致BossRush状态无法重置
            if (ModifySupCalNPC.TrueBossRushStateByAI) {
                if (!NPC.AnyNPCs(CWRID.NPC_SupremeCalamitas)) {//BossRush状态下SupCal消失后，重置状态
                    ModifySupCalNPC.TrueBossRushStateByAI = false;//BossRush状态结束
                }
            }
            //设置灾厄被击败
            if (TraceSupCalDeath.SupCalDefeated && !NPC.AnyNPCs(CWRID.NPC_SupremeCalamitas)) {
                TraceSupCalDeath.SupCalDefeated = false;
                CWRRef.SetDownedCalamitas(true);
                if (VaultUtils.isServer) {
                    NetMessage.SendData(MessageID.WorldData);
                }
            }
        }
    }
}
