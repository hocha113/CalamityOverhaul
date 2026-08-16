using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaResets
{
    /// <summary>
    /// 大范围重启调度：历史记录与权威/演出推进必须在服务器也跑，
    /// 不能住在 dedServ 早退的钩子里；沙漏是纯本机表现，只在客户端推进
    /// </summary>
    internal class KikasaResetSystem : ModSystem
    {
        public override void PostUpdateEverything() {
            KikasaResetHistory.Update();
            KikasaReset.Update();
            if (!Main.dedServ) {
                //与 show.Timer 同拍：沙漏读的是本帧刚推进完的进度与脉冲
                KikasaResetHourglass.Update();
            }
        }

        public override void ClearWorld() {
            KikasaReset.Reset();
            KikasaResetHistory.Clear();
            KikasaResetHourglass.Clear();
        }
    }
}
