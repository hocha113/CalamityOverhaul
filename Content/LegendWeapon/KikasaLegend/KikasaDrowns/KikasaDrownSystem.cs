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
            KikasaPlayerDrown.UpdateAuthority();
            if (!Main.dedServ) {
                //役灵收湖：各端本地的确定性规则，先推规则再推演出
                KikasaMinionDrown.Update();
                //沉玩家：先推束缚镜像（计时/放人）再推手的演出
                KikasaPlayerDrown.UpdateClient();
                KikasaDrownFX.Update();
                KikasaScourgeFX.Update();
                KikasaMinionDrownFX.Update();
                KikasaPlayerDrownFX.Update();
                KikasaDrown.UpdateHoverOmen();
                KikasaPlayerDrown.UpdateHoverOmen();
                KikasaScourge.UpdateLocalAmbient();
            }
        }

        public override void ClearWorld() {
            KikasaDrown.Reset();
            KikasaScourge.Reset();
            KikasaPlayerDrown.Reset();
            if (!Main.dedServ) {
                KikasaMinionDrown.Reset();
                KikasaDrownFX.Clear();
                KikasaScourgeFX.Clear();
                KikasaMinionDrownFX.Clear();
                KikasaPlayerDrownFX.Clear();
            }
        }
    }
}
