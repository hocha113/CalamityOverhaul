using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes
{
    /// <summary>
    /// 游戏模式共享文本（模式名/切换台词/标签悬停说明/拒绝原因）。
    /// 标签 HUD、切换演出、聊天广播共用，别再往各处散写同一份词
    /// </summary>
    internal class GameModeText : ModSystem, ILocalizedModType
    {
        public string LocalizationCategory => "GameModes";

        /// <summary>残酷模式名</summary>
        internal static LocalizedText BrutalName;
        /// <summary>修罗模式名</summary>
        internal static LocalizedText AsuraName;

        /// <summary>残酷模式开启台词</summary>
        internal static LocalizedText BrutalOnLine;
        /// <summary>残酷模式关闭台词</summary>
        internal static LocalizedText BrutalOffLine;
        /// <summary>修罗模式开启台词</summary>
        internal static LocalizedText AsuraOnLine;
        /// <summary>修罗模式关闭台词</summary>
        internal static LocalizedText AsuraOffLine;

        /// <summary>残酷模式悬停说明</summary>
        internal static LocalizedText BrutalDesc;
        /// <summary>修罗模式悬停说明</summary>
        internal static LocalizedText AsuraDesc;

        /// <summary>状态词：已开启</summary>
        internal static LocalizedText StateOn;
        /// <summary>状态词：未开启</summary>
        internal static LocalizedText StateOff;
        /// <summary>操作提示：点击开启</summary>
        internal static LocalizedText HintEnable;
        /// <summary>操作提示：点击关闭</summary>
        internal static LocalizedText HintDisable;
        /// <summary>拒绝原因：Boss 在场</summary>
        internal static LocalizedText BossRefuse;

        public override void SetStaticDefaults() {
            BrutalName = this.GetLocalization(nameof(BrutalName), () => "Brutal Mode");
            AsuraName = this.GetLocalization(nameof(AsuraName), () => "Asura Mode");
            BrutalOnLine = this.GetLocalization(nameof(BrutalOnLine),
                () => "This world is cruel, and yet you love it still.");
            BrutalOffLine = this.GetLocalization(nameof(BrutalOffLine),
                () => "The world sheathes its fangs. Its mercy is only a pause.");
            AsuraOnLine = this.GetLocalization(nameof(AsuraOnLine),
                () => "The Asura path opens. Every pain you deal shall be repaid in full.");
            AsuraOffLine = this.GetLocalization(nameof(AsuraOffLine),
                () => "The Asura path closes. All debts are settled.");
            BrutalDesc = this.GetLocalization(nameof(BrutalDesc),
                () => "Unleashes the reworked, merciless AI of every brutal foe");
            AsuraDesc = this.GetLocalization(nameof(AsuraDesc),
                () => "Foes adapt to repeated damage of the same kind, and any blow you deal sets the least they deal back");
            StateOn = this.GetLocalization(nameof(StateOn), () => "Active");
            StateOff = this.GetLocalization(nameof(StateOff), () => "Dormant");
            HintEnable = this.GetLocalization(nameof(HintEnable), () => "Click to awaken");
            HintDisable = this.GetLocalization(nameof(HintDisable), () => "Click to seal");
            BossRefuse = this.GetLocalization(nameof(BossRefuse),
                () => "A boss still lives. The pact cannot be altered now");
        }

        /// <summary>指定模式在指定开关方向下的台词</summary>
        internal static LocalizedText ToggleLine(GameModeKind kind, bool enabled) {
            if (kind == GameModeKind.Brutal) {
                return enabled ? BrutalOnLine : BrutalOffLine;
            }
            return enabled ? AsuraOnLine : AsuraOffLine;
        }

        /// <summary>模式名</summary>
        internal static LocalizedText Name(GameModeKind kind)
            => kind == GameModeKind.Brutal ? BrutalName : AsuraName;

        /// <summary>模式悬停说明</summary>
        internal static LocalizedText Desc(GameModeKind kind)
            => kind == GameModeKind.Brutal ? BrutalDesc : AsuraDesc;
    }
}
