using System;
using Terraria;
using static CalamityOverhaul.CWRMod;
namespace CalamityOverhaul
{
    internal static class ModCall
    {
        internal enum CallType
        {
            SupertableRecipeDate,
            SupertableSetItem,
            SetNoRecipeHasFrme,
            Config_ForceReplaceResetContent,
            Config_AddExtrasContent,
            SupertableRecipeDate_ZenithWorld,
            SetSupertableRecipeDate,
        }

        public static object Hander(params object[] args) {
            int contentCount = args.Length;
            if (contentCount <= 0) {
                Instance.Logger.Info("Call was made with no parameters.");
                return null;
            }

            CallType callType = default;
            //如果第一个类型选择参数都不对，那么直接返回
            if (Enum.IsDefined(typeof(CallType), args[0])) {
                callType = (CallType)args[0];
            }
            else {
                Instance.Logger.Info("Call was made without the correct CallType.");
                return null;
            }

            //超级工作台系统已移除，这些call保留枚举值仅为兼容旧模组调用，均为无操作
            if (callType is CallType.SupertableRecipeDate
                or CallType.SupertableSetItem
                or CallType.SupertableRecipeDate_ZenithWorld
                or CallType.SetSupertableRecipeDate
                or CallType.SetNoRecipeHasFrme) {
                Instance.Logger.Info($"Call-{callType} is deprecated: the Supertable system has been removed. This call does nothing.");
                if (callType == CallType.SetNoRecipeHasFrme) {
                    return args.Length > 1 ? args[1] as Recipe : null;
                }
                return null;
            }
            //获取设置内容，是否开启强制内容替换
            else if (callType == CallType.Config_ForceReplaceResetContent) {
                return true;
            }
            //已弃用，将始终返回true，因为已经有一些模组在使用这个call，为了保证适配性暂时不要删除它
            else if (callType == CallType.Config_AddExtrasContent) {
                return true;
            }

            return null;
        }
    }
}
