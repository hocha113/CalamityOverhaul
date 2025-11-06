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
        public override bool AI() {
            return true;
        }

        public override void PostAI() {
            Main.player[npc.target].Calamity().AbleToSelectExoMech = false;
            if (npc.ai[0] == 60 && !VaultUtils.isServer) {
                ScenarioManager.Reset<ExoMechdusaSum>();
                ScenarioManager.Start<ExoMechdusaSum>();
            }
        }
    }
}
