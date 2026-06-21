using CalamityOverhaul.Content.TimeFreezes;

namespace CalamityOverhaul.Content.HackTimes
{
    /// <summary>骇客时间世界冻结状态，供 GlitchWraith 等系统在骇入期间同步暂停。</summary>
    internal static class HackTimeFreeze
    {
        public static bool IsActive => HackTime.Active && WorldFreezeSystem.IsActive;
    }
}
