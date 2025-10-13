using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.UI
{
    internal class HalibutUISystem : ModSystem
    {
        public override void PostUpdateEverything() {//逻辑更新
            if (Main.dedServ) {
                return;
            }
            DomainUI.Instance?.LogicUpdate();//领域逻辑
            StudySlot.Instance?.LogicUpdate();//研究逻辑
            ResurrectionUI.Instance?.LogicUpdate();//复苏条逻辑
        }
    }
}
