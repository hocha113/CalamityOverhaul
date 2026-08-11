using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.HackTimes.PvP.Protocols
{
    /// <summary>
    /// 芯片档「防守方本机结算」簇的共用 HUD 文本。
    /// 缺省串为 en-US 终端腔短语；zh-Hans 正典文案随 hjson 接线批落地
    /// （键 = Mods.CalamityOverhaul.UI.PvPDefenderText.*）
    /// </summary>
    internal class PvPDefenderText : ModSystem, ILocalizedModType
    {
        public string LocalizationCategory => "UI";

        /// <summary>信道乱码：聊天框边缘角标</summary>
        public static LocalizedText CommsGarbled { get; private set; }
        /// <summary>冷却注入：出手迟滞角标（{0} = 放缓百分数）</summary>
        public static LocalizedText ActuationLagFormat { get; private set; }
        /// <summary>隐身剥离：防守方角标</summary>
        public static LocalizedText Exposed { get; private set; }
        /// <summary>义体离线：防守方角标</summary>
        public static LocalizedText CyberOffline { get; private set; }

        public override void SetStaticDefaults() {
            CommsGarbled = this.GetLocalization(nameof(CommsGarbled),
                () => "COMMS GARBLED");
            ActuationLagFormat = this.GetLocalization(nameof(ActuationLagFormat),
                () => "ACTUATION +{0}%");
            Exposed = this.GetLocalization(nameof(Exposed),
                () => "OUTLINE EXPOSED");
            CyberOffline = this.GetLocalization(nameof(CyberOffline),
                () => "CYBERWARE OFFLINE");
        }
    }

    /// <summary>
    /// 防守方本机帐本的只读查询，供本簇的辅助钩子（图层压制、聊天拦截、
    /// UseSpeed 乘子、义体旁路）判断"效果是否在本机在册"。<br/>
    /// 只读 <see cref="PlayerHackLedger"/> 真值——帐本只在防守方自己的客户端非空，
    /// 所以这些查询在远端与服务端天然返回 false，钩子在错误的端上自动失活
    /// </summary>
    internal static class PvPDefenderLocal
    {
        /// <summary>本机玩家是否在册指定协议效果</summary>
        internal static bool HasEffect<T>() where T : PlayerHackDef {
            if (Main.dedServ || Main.gameMenu || Main.LocalPlayer?.active != true) {
                return false;
            }
            return Main.LocalPlayer.TryGetModPlayer(out PlayerHackLedger ledger)
                && ledger.HasEffect<T>();
        }

        /// <summary>取本机在册的指定协议条目（读 per-effect 状态用），无则 null</summary>
        internal static PlayerHackEffect FindEffect<T>() where T : PlayerHackDef {
            if (Main.dedServ || Main.gameMenu || Main.LocalPlayer?.active != true
                || !Main.LocalPlayer.TryGetModPlayer(out PlayerHackLedger ledger)) {
                return null;
            }
            for (int i = 0; i < ledger.ActiveEffects.Count; i++) {
                if (ledger.ActiveEffects[i].Hack is T) {
                    return ledger.ActiveEffects[i];
                }
            }
            return null;
        }
    }
}
