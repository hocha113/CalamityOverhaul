using CalamityOverhaul.Content;
using System;
using System.Reflection;

namespace CalamityOverhaul.OtherMods.NoxusBoss
{
    internal class NoxusRef : ICWRLoader
    {
        public static bool Has => CWRMod.Instance.noxusBoss != null;
        private static Type marsCombatEventType;
        private static PropertyInfo marsBeingSummonedProperty;

        void ICWRLoader.LoadData() {
            if (!Has) {
                return;
            }
            try {
                Type[] types = CWRUtils.GetModTypes(CWRMod.Instance.noxusBoss);
                marsCombatEventType = CWRUtils.GetTargetTypeInStringKey(types, "MarsCombatEvent");
                if (marsCombatEventType != null) {
                    marsBeingSummonedProperty = marsCombatEventType.GetProperty("MarsBeingSummoned", BindingFlags.Static | BindingFlags.Public);
                }
            } catch (Exception ex) {
                CWRMod.Instance.Logger.Error($"NoxusRef.LoadData An Error Has Occurred: {ex.Message}");
            }
        }

        void ICWRLoader.UnLoadData() {
            marsCombatEventType = null;
            marsBeingSummonedProperty = null;
        }

        /// <summary>
        /// 检测是否启用嘉登NPC兼容模式
        /// 这个用于兼容众神之怒模组中的嘉登NPC和索林的对话场景
        /// </summary>
        /// <returns></returns>
        internal static bool DraedonNPCIsCompatible() {
            if (!Has) {
                return false;//需要安装了众神之怒
            }
            if (marsBeingSummonedProperty == null) {
                return false;//反射缓存未成功，无法识别兼容场景
            }
            if (!InWorldBossPhase.Downed29.Invoke()) {
                return false;//需要星流巨械被打败
            }
            try {
                return (bool)marsBeingSummonedProperty.GetValue(null);//如果嘉登正在被召唤，并且完成了马尔斯前置条件，则启用兼容模式
            } catch (Exception ex) {
                CWRMod.Instance.Logger.Error($"NoxusRef.DraedonNPCIsCompatible An Error Has Occurred: {ex.Message}");
                VaultUtils.Text("CWRMod Error: NoxusRef.DraedonNPCIsCompatible An Error Has Occurred! See Log For Details.", Color.Red);
                return false;
            }
        }
    }
}
