using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.OldNet
{
    //旧网玩家可见文本集中登记（键 Mods.CalamityOverhaul.UI.OldNet*）
    internal class OldNetTexts : ModSystem, ILocalizedModType
    {
        public string LocalizationCategory => "UI";

        public static LocalizedText OldNetHarvest { get; private set; }
        public static LocalizedText OldNetNodeHint { get; private set; }
        public static LocalizedText OldNetTerminalHint { get; private set; }
        public static LocalizedText OldNetSettleDone { get; private set; }
        public static LocalizedText OldNetSettleEmpty { get; private set; }
        public static LocalizedText OldNetEjectRam { get; private set; }
        public static LocalizedText OldNetEjectDeath { get; private set; }
        public static LocalizedText OldNetLedgerFull { get; private set; }
        public static LocalizedText OldNetEncryptHint { get; private set; }
        public static LocalizedText OldNetEventHint { get; private set; }
        public static LocalizedText OldNetEventPulled { get; private set; }
        public static LocalizedText OldNetRelayHint { get; private set; }
        public static LocalizedText OldNetRelayDone { get; private set; }
        public static LocalizedText OldNetRelayEmpty { get; private set; }

        public override void SetStaticDefaults() {
            OldNetHarvest = this.GetLocalization(nameof(OldNetHarvest), () => "+{0} 模具碎片（未铭刻）");
            OldNetNodeHint = this.GetLocalization(nameof(OldNetNodeHint), () => "回收数据");
            OldNetTerminalHint = this.GetLocalization(nameof(OldNetTerminalHint), () => "登出并铭刻收获");
            OldNetSettleDone = this.GetLocalization(nameof(OldNetSettleDone), () => "已铭刻 {0} 枚模具碎片，链路安全断开");
            OldNetSettleEmpty = this.GetLocalization(nameof(OldNetSettleEmpty), () => "链路安全断开，本次没有收获");
            OldNetEjectRam = this.GetLocalization(nameof(OldNetEjectRam), () => "RAM耗尽——链路烧断，未铭刻的收获已丢失");
            OldNetEjectDeath = this.GetLocalization(nameof(OldNetEjectDeath), () => "构念崩解——链路烧断，未铭刻的收获已丢失");
            OldNetLedgerFull = this.GetLocalization(nameof(OldNetLedgerFull), () => "账本已满——先去中继站或登出");
            OldNetEncryptHint = this.GetLocalization(nameof(OldNetEncryptHint), () => "引导破解（站桩约3秒，动静很大）");
            OldNetEventHint = this.GetLocalization(nameof(OldNetEventHint), () => "拉闸：解除全图封锁，惊动整张网");
            OldNetEventPulled = this.GetLocalization(nameof(OldNetEventPulled), () => "封锁已解除——清剿波正在路上");
            OldNetRelayHint = this.GetLocalization(nameof(OldNetRelayHint), () => "中继上行：铭刻当前账本（上行有噪音）");
            OldNetRelayDone = this.GetLocalization(nameof(OldNetRelayDone), () => "已铭刻 {0} 枚模具碎片，链路保持");
            OldNetRelayEmpty = this.GetLocalization(nameof(OldNetRelayEmpty), () => "账本为空，无可上行");
        }
    }
}
