using CalamityMod.NPCs.SupremeCalamitas;
using CalamityOverhaul.Content.ADV.Scenarios.SupCal.End.EternalBlazingNows;
using InnoVault.GameSystem;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.SupCal
{
    internal class ModifySupCalNPC : NPCOverride
    {
        public override int TargetID => ModContent.NPCType<SupremeCalamitas>();

        public override bool AI() {
            return base.AI();
        }

        public override void PostAI() {
            if (npc.ModNPC is SupremeCalamitas supCal) {
                //保持天空特效激活状态
                if (supCal.giveUpCounter <= 120 && EbnEffect.IsActive) {
                    supCal.giveUpCounter = 120;
                }
            }
        }
    }
}
