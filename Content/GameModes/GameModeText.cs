using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes
{
    /// <summary>
    /// 游戏模式共享文本（文艺名/切换台词/标签悬停说明/拒绝原因）。
    /// 标签 HUD、切换演出、聊天广播共用，按表现脸（<see cref="GameModeFace"/>）取值，
    /// 别再往各处散写同一份词
    /// </summary>
    internal class GameModeText : ModSystem, ILocalizedModType
    {
        public string LocalizationCategory => "GameModes";

        /// <summary>残酷的文艺名：残酷世界</summary>
        internal static LocalizedText BrutalName;
        /// <summary>修罗的文艺名：修罗地狱</summary>
        internal static LocalizedText AsuraName;
        /// <summary>毁灭的文艺名：死神永生</summary>
        internal static LocalizedText AnnihilationName;

        /// <summary>残酷开启台词</summary>
        internal static LocalizedText BrutalOnLine;
        /// <summary>残酷关闭台词</summary>
        internal static LocalizedText BrutalOffLine;
        /// <summary>修罗开启台词</summary>
        internal static LocalizedText AsuraOnLine;
        /// <summary>修罗关闭台词</summary>
        internal static LocalizedText AsuraOffLine;
        /// <summary>毁灭开启台词</summary>
        internal static LocalizedText AnnihilationOnLine;
        /// <summary>毁灭关闭台词</summary>
        internal static LocalizedText AnnihilationOffLine;

        /// <summary>残酷悬停说明</summary>
        internal static LocalizedText BrutalDesc;
        /// <summary>修罗悬停说明</summary>
        internal static LocalizedText AsuraDesc;
        /// <summary>毁灭悬停说明</summary>
        internal static LocalizedText AnnihilationDesc;

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
            BrutalName = this.GetLocalization(nameof(BrutalName), () => "Cruel World");
            AsuraName = this.GetLocalization(nameof(AsuraName), () => "Asura Hell");
            AnnihilationName = this.GetLocalization(nameof(AnnihilationName), () => "Death's End");
            BrutalOnLine = this.GetLocalization(nameof(BrutalOnLine),
                () => "This world is cruel, and yet you love it still.");
            BrutalOffLine = this.GetLocalization(nameof(BrutalOffLine),
                () => "The world sheathes its fangs. Its mercy is only a pause.");
            AsuraOnLine = this.GetLocalization(nameof(AsuraOnLine),
                () => "The Asura path opens. Every pain you deal shall be repaid in full.");
            AsuraOffLine = this.GetLocalization(nameof(AsuraOffLine),
                () => "The Asura path closes. All debts are settled.");
            AnnihilationOnLine = this.GetLocalization(nameof(AnnihilationOnLine),
                () => "All shall pass; only Death lives forever.");
            AnnihilationOffLine = this.GetLocalization(nameof(AnnihilationOffLine),
                () => "Death rests a while; the living steal a breath.");
            BrutalDesc = this.GetLocalization(nameof(BrutalDesc),
                () => "Unleashes the reworked, merciless AI of brutal foes, their stats anchored no lower than Master Mode; all other enemies gain 50% more life and damage, and lesser fiends turn swift and frenzied");
            AsuraDesc = this.GetLocalization(nameof(AsuraDesc),
                () => "The enhancement rises to 100%. Foes adapt to repeated damage of the same kind, and any blow you deal sets the least they deal back");
            AnnihilationDesc = this.GetLocalization(nameof(AnnihilationDesc),
                () => "The final form of Asura Hell in a zenith world. Every terror doubles again: foes adapt twice as fast, and your pain is repaid twofold");
            StateOn = this.GetLocalization(nameof(StateOn), () => "Active");
            StateOff = this.GetLocalization(nameof(StateOff), () => "Dormant");
            HintEnable = this.GetLocalization(nameof(HintEnable), () => "Click to awaken");
            HintDisable = this.GetLocalization(nameof(HintDisable), () => "Click to seal");
            BossRefuse = this.GetLocalization(nameof(BossRefuse),
                () => "A boss still lives. The pact cannot be altered now");
        }

        /// <summary>指定表现脸在指定开关方向下的台词</summary>
        internal static LocalizedText ToggleLine(GameModeFace face, bool enabled) => face switch {
            GameModeFace.Brutal => enabled ? BrutalOnLine : BrutalOffLine,
            GameModeFace.Annihilation => enabled ? AnnihilationOnLine : AnnihilationOffLine,
            _ => enabled ? AsuraOnLine : AsuraOffLine,
        };

        /// <summary>表现脸的文艺名</summary>
        internal static LocalizedText Name(GameModeFace face) => face switch {
            GameModeFace.Brutal => BrutalName,
            GameModeFace.Annihilation => AnnihilationName,
            _ => AsuraName,
        };

        /// <summary>表现脸的悬停说明</summary>
        internal static LocalizedText Desc(GameModeFace face) => face switch {
            GameModeFace.Brutal => BrutalDesc,
            GameModeFace.Annihilation => AnnihilationDesc,
            _ => AsuraDesc,
        };
    }
}
