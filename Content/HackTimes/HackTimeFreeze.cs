using CalamityOverhaul.Content.TimeFreezes;

namespace CalamityOverhaul.Content.HackTimes
{
    /// <summary>骇客时间世界冻结，供 GlitchWraith 等在骇入期同步暂停</summary>
    internal static class HackTimeFreeze
    {
        public static bool IsActive => HackTime.Active && WorldFreezeSystem.IsActive;
    }
}
