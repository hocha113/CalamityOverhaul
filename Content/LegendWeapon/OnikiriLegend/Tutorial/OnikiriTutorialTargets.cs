using Microsoft.Xna.Framework;
using Terraria;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.Tutorial
{
    /// <summary>
    /// 短寿命 HUD 焦点快照。
    /// 各 HUD 控件在 Update/LogicUpdate 末尾发布本帧真实命中区，教程渲染层消费；
    /// GameUpdateCount 不一致则快照过期，高亮消失而不显示错位框。
    /// </summary>
    internal sealed class HudFocusSnapshot
    {
        /// <summary>发布帧</summary>
        internal uint Frame;
        /// <summary>UI 空间命中矩形</summary>
        internal Rectangle Rect;
        /// <summary>语义标签，用于渲染层决定高亮样式</summary>
        internal string Tag;

        internal bool IsValid => Frame == Main.GameUpdateCount;
    }

    /// <summary>
    /// 教程 HUD 焦点快照注册表。
    /// HUD 控件调用 <c>Publish</c> 写入；渲染层调用 <c>Get</c> 读取。
    /// 过期快照（超过 1 帧）读出时自动返回 null，确保对齐最新布局。
    /// </summary>
    internal static class OnikiriTutorialTargets
    {
        private static readonly System.Collections.Generic.Dictionary<string, HudFocusSnapshot> _snapshots = [];

        //====已知 Tag 常量====
        internal const string Tag_VigorStroke = "vigor";
        internal const string Tag_StanceSheath = "stance";
        internal const string Tag_DomainEye = "eye";
        internal const string Tag_TalismanStrip = "talisman";
        internal const string Tag_Register = "register";
        internal const string Tag_RegisterEntry = "register_entry";
        internal const string Tag_MeiSlotNakago = "mei_nakago";
        internal const string Tag_MeiSlotHi = "mei_hi";
        internal const string Tag_MeiSlotHorimono = "mei_horimono";
        internal const string Tag_MeiFan = "mei_fan";

        /// <summary>HUD 控件每帧调用，发布当前命中区</summary>
        internal static void Publish(string tag, Rectangle rect)
        {
            if (!_snapshots.TryGetValue(tag, out HudFocusSnapshot snap))
            {
                snap = new HudFocusSnapshot { Tag = tag };
                _snapshots[tag] = snap;
            }
            snap.Frame = Main.GameUpdateCount;
            snap.Rect = rect;
        }

        /// <summary>获取当前帧有效快照；过期返回 null</summary>
        internal static HudFocusSnapshot Get(string tag)
            => _snapshots.TryGetValue(tag, out HudFocusSnapshot s) && s.IsValid ? s : null;

        internal static void Clear() => _snapshots.Clear();
    }
}