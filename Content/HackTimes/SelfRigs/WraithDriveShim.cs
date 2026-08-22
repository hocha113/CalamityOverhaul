using CalamityOverhaul.Content.Wraiths.Runtime;
using System;
using System.Collections;
using System.Reflection;

namespace CalamityOverhaul.Content.HackTimes.SelfRigs
{
    /// <summary>
    /// 役鬼强驱对 <see cref="WraithPlayer"/> 私有成员的反射垫片。<br/>
    /// 公开 API 只有涨账入口（<c>TryChargeAuthority</c> 要求 revivalGain &gt; 0，
    /// <c>AddErosionInternal</c> 私有），免费窗口的逐帧退款与到期的侵蚀账单都需要向下写，
    /// 所以此处以反射顶住；一等成员的补丁提案见
    /// <c>Doc/patches/HACK32-SelfRig.md</c>（<c>AddErosionAuthority</c> / 复苏回写），
    /// 落地后本垫片整体退役。<br/>
    /// 任一句柄取不到即 <see cref="Available"/> = false，协议侧据此整条拒用，宁可不可用，不要半生效
    /// </summary>
    internal static class WraithDriveShim
    {
        private const float Epsilon = 0.0001f;

        private static readonly FieldInfo erosionField;
        private static readonly FieldInfo revivalField;
        private static readonly FieldInfo revivalValueField;
        private static readonly MethodInfo addErosionMethod;
        private static readonly MethodInfo markChangedMethod;

        internal static bool Available { get; }

        static WraithDriveShim() {
            try {
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
                Type wraithType = typeof(WraithPlayer);
                erosionField = wraithType.GetField("erosion", flags);
                revivalField = wraithType.GetField("revival", flags);
                addErosionMethod = wraithType.GetMethod("AddErosionInternal", flags);
                markChangedMethod = wraithType.GetMethod("MarkResourceChanged", flags);
                //revival 是 Dictionary<string, RevivalState>，RevivalState 为私有嵌套类
                Type stateType = revivalField?.FieldType.GenericTypeArguments is { Length: 2 } args
                    ? args[1] : null;
                revivalValueField = stateType?.GetField("Value",
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            } catch (Exception ex) {
                CWRMod.Instance?.Logger.Warn($"[SelfRig] WraithDriveShim 反射失败: {ex.Message}");
            }

            Available = erosionField != null && revivalField != null
                && revivalValueField != null && addErosionMethod != null
                && markChangedMethod != null;
            if (!Available) {
                CWRMod.Instance?.Logger.Warn(
                    "[SelfRig] WraithPlayer 私有成员缺失，役鬼强驱整条禁用");
            }
        }

        /// <summary>
        /// 免费窗口逐帧退款：基线随自然衰减下调，任何高出基线的涨账
        /// （役使结算的复苏与侵蚀）当帧压回。仅权威端调用
        /// </summary>
        internal static void RefundWindow(WraithPlayer wraith, string key,
            ref float revivalBaseline, ref float erosionBaseline) {
            if (!Available || wraith == null || string.IsNullOrEmpty(key)) return;

            bool changed = false;
            float revivalNow = wraith.GetRevival(key);
            revivalBaseline = Math.Min(revivalBaseline, revivalNow);
            if (revivalNow > revivalBaseline + Epsilon
                && TryWriteRevival(wraith, key, revivalBaseline)) {
                changed = true;
            }

            float erosionNow = wraith.Erosion;
            erosionBaseline = Math.Min(erosionBaseline, erosionNow);
            if (erosionNow > erosionBaseline + Epsilon) {
                erosionField.SetValue(wraith, erosionBaseline);
                changed = true;
            }

            if (changed) {
                //走既有的资源脏标记，让 WraithNet 按常规节奏把退款同步下去
                MarkChanged(wraith, immediate: false);
            }
        }

        /// <summary>窗口到期的一次性侵蚀账单；走原生入账（含阶跃提示与 idle 重置）并立即同步</summary>
        internal static void AddErosion(WraithPlayer wraith, float amount) {
            if (!Available || wraith == null || amount <= 0f) return;
            addErosionMethod.Invoke(wraith, [amount]);
            MarkChanged(wraith, immediate: true);
        }

        private static bool TryWriteRevival(WraithPlayer wraith, string key, float value) {
            if (revivalField.GetValue(wraith) is not IDictionary dict
                || !dict.Contains(key)) {
                return false;
            }
            object state = dict[key];
            if (state == null) return false;
            revivalValueField.SetValue(state, MathHelper.Clamp(value, 0f, 1f));
            return true;
        }

        private static void MarkChanged(WraithPlayer wraith, bool immediate)
            => markChangedMethod.Invoke(wraith, [immediate]);
    }
}
