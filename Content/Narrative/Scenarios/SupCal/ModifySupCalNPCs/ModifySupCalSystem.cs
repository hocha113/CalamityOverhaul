using CalamityOverhaul.Content.Narrative.Data;
using CalamityOverhaul.Content.Narrative.Data.Modules;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Narrative.Scenarios.SupCal.ModifySupCalNPCs
{
    internal class ModifySupCalSystem : ModSystem
    {
        public override void PostUpdateNPCs() {
            int witch = NPC.FindFirstNPC(CWRID.NPC_WITCH);
            if (witch != -1) {
                bool hasEbn = false;
                foreach (Player p in Main.ActivePlayers) {
                    if (p.GetModPlayer<StoryPlayer>().Get<SupCalStoryData>().EternalBlazingNow) {
                        hasEbn = true;
                    }
                }

                if (hasEbn) {
                    CWRRef.SetDownedCalamitas(true);
                    Main.npc[witch].active = false;
                    Main.npc[witch].netUpdate = true;
                }
            }

            if (ModifySupCalNPC.TrueBossRushStateByAI) {
                if (!NPC.AnyNPCs(CWRID.NPC_SupremeCalamitas)) {
                    ModifySupCalNPC.TrueBossRushStateByAI = false;
                }
            }

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
