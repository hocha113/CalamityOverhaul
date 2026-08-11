using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.HackTimes
{
    /// <summary>
    /// 弹幕侧的骇入标记。<br/>
    /// 即时协议改完弹幕属性就结束了，没有"到期还原"的落点，
    /// 所以能不能再改一次只能记在弹幕自己身上——
    /// 靠外挂字典按槽位记账会在槽位复用后认错弹幕
    /// </summary>
    internal class HackEffectProjectile : GlobalProjectile
    {
        public override bool InstancePerEntity => true;

        /// <summary>已被弹道超频加过料</summary>
        internal bool BallisticOverclocked;

        internal static bool IsOverclocked(Projectile projectile)
            => projectile != null
                && projectile.TryGetGlobalProjectile(out HackEffectProjectile marks)
                && marks.BallisticOverclocked;

        internal static void MarkOverclocked(Projectile projectile) {
            if (projectile != null
                && projectile.TryGetGlobalProjectile(out HackEffectProjectile marks)) {
                marks.BallisticOverclocked = true;
            }
        }
    }
}
