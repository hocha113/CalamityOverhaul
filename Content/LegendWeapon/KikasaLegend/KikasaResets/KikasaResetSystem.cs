using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaResets
{
    /// <summary>
    /// 大范围重启调度：历史记录与权威/演出推进必须在服务器也跑，
    /// 不能住在 dedServ 早退的钩子里
    /// </summary>
    internal class KikasaResetSystem : ModSystem
    {
        public override void PostUpdateEverything() {
            KikasaResetHistory.Update();
            KikasaReset.Update();
        }

        public override void ClearWorld() {
            KikasaReset.Reset();
            KikasaResetHistory.Clear();
        }
    }
}
