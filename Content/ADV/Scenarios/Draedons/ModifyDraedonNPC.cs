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
        public override bool AI() {
            timer++;
            return true;
        }

        public override void PostAI() {
            Main.player[npc.target].Calamity().AbleToSelectExoMech = false;

            if (!VaultUtils.isServer && Main.myPlayer == npc.target) {
                //召唤机甲对话
                if (timer == 90) {
                    ScenarioManager.Reset<ExoMechdusaSum>();
                    ScenarioManager.Start<ExoMechdusaSum>();
                }

                if (npc.ModNPC is Draedon draedon) {
                    //正常战败对话
                    if (draedon.DefeatTimer > 0 && !defeat) {
                        defeat = true;
                        ScenarioManager.Reset<ExoMechEndingDialogue>();
                        ScenarioManager.Start<ExoMechEndingDialogue>();
                    }

                    if (DraedonEffect.IsActive) {//哔哔完后再退场
                        if (draedon.DefeatTimer > 30 + 150 * 8f + 120f) {
                            draedon.DefeatTimer = 30 + 150 * 8f + 120f;//200 - 120 = 80，给这个老逼登80帧的时间过渡到退场动画
                        }
                    }
                }
            }
        }
    }
}
