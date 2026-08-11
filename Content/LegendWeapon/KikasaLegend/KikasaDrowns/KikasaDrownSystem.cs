using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDrowns
{
    /// <summary>
    /// 沉溺调度：权威推进必须在服务器也跑，不能住在
    /// KikasaDomainSystem 那种 dedServ 早退的钩子里；演出层只在客户端推进。
    /// 拒绝文案也挂在这里注册。
    /// </summary>
    internal class KikasaDrownSystem : ModSystem, ILocalizedModType
    {
        public string LocalizationCategory => "Legend.KikasaText";

        public static LocalizedText BossRefuse { get; private set; }
        public static LocalizedText DrownBusy { get; private set; }
        public static LocalizedText DrownOutOfGrip { get; private set; }

        public override void SetStaticDefaults() {
            BossRefuse = this.GetLocalization(nameof(BossRefuse), () => "湖抱不动它");
            DrownBusy = this.GetLocalization(nameof(DrownBusy), () => "湖还在收上一个");
            DrownOutOfGrip = this.GetLocalization(nameof(DrownOutOfGrip), () => "够不到那么远");
        }

        public override void PostUpdateEverything() {
            KikasaDrown.UpdateAuthority();
            if (!Main.dedServ) {
                KikasaDrownFX.Update();
            }
        }

        public override void ClearWorld() {
            KikasaDrown.Reset();
            if (!Main.dedServ) {
                KikasaDrownFX.Clear();
            }
        }
    }
}
