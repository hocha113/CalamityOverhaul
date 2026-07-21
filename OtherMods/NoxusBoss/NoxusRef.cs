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

        /// <summary>嘉登 NPC 兼容（众神之怒对话场景）</summary>
        internal static bool DraedonNPCIsCompatible() {
            if (!Has) {
                return false;//未装众神之怒
            }
            if (marsBeingSummonedProperty == null) {
                return false;//反射未就绪
            }
            if (!InWorldBossPhase.Downed29.Invoke()) {
                return false;//需先击败星流巨械
            }
            try {
                return (bool)marsBeingSummonedProperty.GetValue(null);//嘉登召唤中则启用
            } catch (Exception ex) {
                CWRMod.Instance.Logger.Error($"NoxusRef.DraedonNPCIsCompatible An Error Has Occurred: {ex.Message}");
                VaultUtils.Text("CWRMod Error: NoxusRef.DraedonNPCIsCompatible An Error Has Occurred! See Log For Details.", Color.Red);
                return false;
            }
        }
    }
}
