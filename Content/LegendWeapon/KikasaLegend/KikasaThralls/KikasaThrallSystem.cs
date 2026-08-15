using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaThralls
{
    /// <summary>伞奴调度：转化闸门计时全端推进，化水演出只在客户端推进；换世界清场</summary>
    internal class KikasaThrallSystem : ModSystem
    {
        public override void PostUpdateEverything() {
            KikasaThrall.Update();
            if (!Main.dedServ) {
                KikasaThrallMeltFX.Update();
            }
        }

        public override void ClearWorld() {
            KikasaThrall.ResetLocal();
            if (!Main.dedServ) {
                KikasaThrallMeltFX.Clear();
            }
        }
    }
}
