using CalamityOverhaul.Common;
using CalamityOverhaul.Content;
using System;
using System.Reflection;

namespace CalamityOverhaul.OtherMods.NoxusBoss
{
    internal class NoxusRef
    {
        /// <summary>
        /// 检测是否启用嘉登NPC兼容模式
        /// 这个用于兼容众神之怒模组中的嘉登NPC和索林的对话场景
        /// </summary>
        /// <returns></returns>
        internal static bool DraedonNPCIsCompatible() {
            try {
                if (CWRMod.Instance.noxusBoss == null) {
                    return false;//需要安装了众神之怒
                }
                if (!InWorldBossPhase.Downed29.Invoke()) {
                    return false;//需要星流巨械被打败
                }
                Type type = ModGanged.GetTargetTypeInStringKey(ModGanged.GetModTypes(CWRMod.Instance.noxusBoss), "MarsCombatEvent");
                if (type == null) {
                    return false;
                }
                PropertyInfo marsBeingSummonedProperty = type.GetProperty("MarsBeingSummoned", BindingFlags.Static | BindingFlags.Public);
                if (marsBeingSummonedProperty == null) {
                    return false;
                }
                return (bool)marsBeingSummonedProperty.GetValue(null);//如果嘉登正在被召唤，并且完成了马尔斯前置条件，则启用兼容模式
            } catch (Exception ex) {
                CWRMod.Instance.Logger.Error($"NoxusRef.DraedonNPCIsCompatible An Error Has Cccurred: {ex.Message}");
                VaultUtils.Text("CWRMod Error: NoxusRef.DraedonNPCIsCompatible An Error Has Occurred! See Log For Details.", Color.Red);
                return false;
            }
        }
    }
}
