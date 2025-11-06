using CalamityMod;
using CalamityMod.NPCs.ExoMechs;
using InnoVault.GameSystem;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.Draedons
{
    internal class ModifyDraedonNPC : NPCOverride
    {
        public override int TargetID => ModContent.NPCType<Draedon>();
        private int timer;
        private bool defeat;
        private bool killend;
        public override bool AI() {
            timer++;
            return true;
        }

        public override void PostAI() {
            Main.player[npc.target].Calamity().AbleToSelectExoMech = false;
            if (!VaultUtils.isServer && Main.myPlayer == npc.target) {
                if (timer == 90) {
                    ScenarioManager.Reset<ExoMechdusaSum>();
                    ScenarioManager.Start<ExoMechdusaSum>();
                }

                if (npc.ModNPC is Draedon draedon) {
                    if (draedon.DefeatTimer > 0 && !defeat) {
                        defeat = true;
                        ExoMechEndingDialogue.TriggerEndingDialogue();//常规战败CG
                    }
                    if (draedon.KillReappearTextCountdown == 20 && !killend) {
                        if (!killend) {//结束掉可能存在的场景
                            DialogueUIRegistry.Current.BeginClose();
                        }
                        killend = true;
                        ExoMechEndingDialogue.TriggerKillAttemptDialogue();//击杀CG
                    }
                }
            }
        }
    }
}
