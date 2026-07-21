using CalamityOverhaul.Content.Scenarios.Draedon.ExoMechdusaSums;
using CalamityOverhaul.OtherMods.NoxusBoss;
using InnoVault.GameSystem;
using Terraria;

namespace CalamityOverhaul.Content.Scenarios.Draedon
{
    internal class ModifyDraedonNPC : NPCOverride
    {
        public override int TargetID => CWRID.NPC_Draedon;

        private static int timer;
        private static bool defeat;
        private static int battleStartTime;

        /// <summary>等机甲选择UI</summary>
        public static bool AwaitSummonUIbeenGenerated;

        public override bool CanOverride() {
            if (NoxusRef.DraedonNPCIsCompatible()) {
                return false;
            }
            return true;
        }

        public override void SetProperty() {
            timer = 0;
            defeat = false;
            battleStartTime = 0;
            AwaitSummonUIbeenGenerated = false;
        }

        public override bool AI() {
            timer++;
            return true;
        }

        public static void DefeatEvent() {
            if (defeat) {
                return;
            }
            defeat = true;

            if (CWRRef.GetBossRushActive()) {
                return;
            }

            int battleDuration = timer - battleStartTime;
            float healthPercent = Main.LocalPlayer.statLife / (float)Main.LocalPlayer.statLifeMax2;
            DraedonTriggerService.NotifyExoMechDefeat(battleDuration, healthPercent);
        }

        public override void PostAI() {
            if (!ExoMechdusaSum.CompatibleMode) {
                CWRRef.SetAbleToSelectExoMech(Main.player[npc.target], false);
            }

            if (timer == 80) {
                AwaitSummonUIbeenGenerated = true;
            }

            if (!VaultUtils.isServer && Main.myPlayer == npc.target) {
                if (timer == 90) {
                    DraedonTriggerService.BeginExoMechdusaSummon();
                    battleStartTime = timer;
                }

                if (CWRRef.GetDraedonDefeatTimer(npc) > 0) {
                    DefeatEvent();
                }
            }

            if (timer > 210 && CWRRef.GetDraedonDefeatTimer(npc) > 0) {
                AwaitSummonUIbeenGenerated = false;
            }

            if (DraedonEffect.IsActive) {
                float maxTime = 30 + 150 * 8f + 120f;
                if (CWRRef.GetDraedonDefeatTimer(npc) > maxTime) {
                    CWRRef.SetDraedonDefeatTimer(npc, maxTime);
                }
            }
            else if (timer > 220 && !AwaitSummonUIbeenGenerated && !CWRRef.HasExo()) {
                float maxTime = 30 + 150 * 8f + 120f;
                if (CWRRef.GetDraedonDefeatTimer(npc) < maxTime) {
                    CWRRef.SetDraedonDefeatTimer(npc, maxTime);
                }
            }
        }
    }
}
