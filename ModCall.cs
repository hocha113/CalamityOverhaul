using CalamityOverhaul.Common;
using CalamityOverhaul.Content.UIs.SupertableUIs;
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

            //如果要使用这个call，那么它最好在Load环节就调用一次，这样才能保证欧米茄能正常获取到值
            if (callType == CallType.SupertableRecipeDate) {
                if (contentCount < 2) {
                    Instance.Logger.Info("Call-SupertableRecipeDate was made without additional parameters.");
                    return null;
                }

                if (args[1] is string[] addPms) {
                    SupertableUI.ModCall_OtherRpsData_StringList.Add(addPms);
                }
                else {
                    Instance.Logger.Info("Call-SupertableRecipeDate was made with incorrect parameter types.");
                    return null;
                }
            }
            //如果要使用这个call，在指定物品类的SD函数中调用一次，这样才能进行设置
            else if (callType == CallType.SupertableSetItem) {
                if (contentCount < 3) {
                    Instance.Logger.Info("Call-SupertableSetItem was made without additional parameters.");
                    return null;
                }

                Item item = args[1] as Item;
                if (item == null) {
                    Instance.Logger.Info("Call-SupertableSetItem this aig[1] not Item type instance");
                    return null;
                }

                string[] pms = args[2] as string[];
                if (item == null) {
                    Instance.Logger.Info("Call-SupertableSetItem this aig[2] not string[] type instance");
                    return null;
                }

                item.CWR().OmigaSnyContent = pms;
            }
            //在配方函数中调用这个Call，这个Call用于设置特殊的合成事件
            else if (callType == CallType.SetNoRecipeHasFrme) {
                if (contentCount < 2) {
                    Instance.Logger.Info("Call-SetNoRecipeHasFrme was made without additional parameters.");
                    return null;
                }

                Recipe recipe = args[1] as Recipe;
                if (recipe == null) {
                    Instance.Logger.Info("Call-SetNoRecipeHasFrme this aig[1] not Recipe type instance");
                    return null;
                }

                return recipe.AddBlockingSynthesisEvent();
            }
            //获取设置内容，是否开启强制内容替换
            else if (callType == CallType.Config_ForceReplaceResetContent) {
                return CWRServerConfig.Instance.ForceReplaceResetContent;
            }
            //已弃用，将始终返回true，因为已经有一些模组在使用这个call，为了保证适配性暂时不要删除它
            else if (callType == CallType.Config_AddExtrasContent) {
                return true;
            }
            //如果要使用这个call，那么它最好在Load环节就调用一次，这样才能保证欧米茄能正常获取到值
            else if (callType == CallType.SupertableRecipeDate_ZenithWorld) {
                if (contentCount < 2) {
                    Instance.Logger.Info("Call-SupertableRecipeDate_ZenithWorld was made without additional parameters.");
                    return null;
                }

                if (args[1] is string[] addPms) {
                    SupertableUI.OtherRpsData_ZenithWorld_StringList.Add(addPms);
                }
                else {
                    Instance.Logger.Info("Call-SupertableRecipeDate_ZenithWorld was made with incorrect parameter types.");
                    return null;
                }
            }

            return null;
        }
    }
}
