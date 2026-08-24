using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDrowns
{
    /// <summary>
    /// 沉溺调度：权威推进必须在服务器也跑，不能住在
    /// KikasaDomainSystem 那种 dedServ 早退的钩子里；演出层只在客户端推进
    /// </summary>
    internal class KikasaDrownSystem : ModSystem
    {
        public override void PostUpdateEverything() {
            KikasaDrown.UpdateAuthority();
            KikasaScourge.UpdateAuthority();
            if (!Main.dedServ) {
                KikasaDrownFX.Update();
                KikasaScourgeFX.Update();
                KikasaDrown.UpdateHoverOmen();
                KikasaScourge.UpdateLocalAmbient();
            }
        }

        public override void ClearWorld() {
            KikasaDrown.Reset();
            KikasaScourge.Reset();
            if (!Main.dedServ) {
                KikasaDrownFX.Clear();
                KikasaScourgeFX.Clear();
            }
        }
    }
}
