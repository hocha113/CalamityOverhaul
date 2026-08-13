using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaRains
{
    /// <summary>墨雨表现层驱动:渍斑贴花逐帧推进,世界卸载清场</summary>
    internal class KikasaRainSystem : ModSystem
    {
        public override void PostUpdateEverything() {
            if (Main.dedServ) {
                return;
            }
            KikasaInkFX.Update();
        }

        public override void OnWorldUnload() => KikasaInkFX.Clear();
    }
}
