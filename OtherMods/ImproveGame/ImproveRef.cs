using System;
using System.Reflection;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader.Config;

namespace CalamityOverhaul.OtherMods.ImproveGame
{
    internal class ImproveRef : ICWRLoader
    {
        internal static bool Suitableversion_improveGame => CWRMod.Instance.improveGame != null && CWRMod.Instance.improveGame.Version >= new Version(1, 7, 1, 7);
        public static ModConfig ImproveGameConfig_ConfigInstance;
        public static FieldInfo ImproveGameConfig_NoConsume_Ammo;
        public static ModConfig LuiAFKConfig_ConfigInstance;
        public static FieldInfo LuiAFKConfig_RangerAmmoInfo;

        internal static bool ImproveGameSetAmmoIsNoConsume(Item ammoItem) {
            if (CWRMod.Instance.improveGame == null) {
                return false;
            }

            if (ImproveGameConfig_NoConsume_Ammo == null) {
                return false;
            }

            try {
                ImproveGameConfig_ConfigInstance ??= CWRMod.Instance.improveGame.Find<ModConfig>("ImproveConfigs");//懒加载一下
                if ((bool)ImproveGameConfig_NoConsume_Ammo.GetValue(ImproveGameConfig_ConfigInstance)) {
                    if (ammoItem.stack >= 999 && ammoItem.type == ItemID.FallenStar) {
                        return true;
                    }
                    if (ammoItem.stack >= 3996 && ammoItem.ammo > 0) {
                        return true;
                    }
                }
            } catch {
                return false;
            }

            return false;
        }

        //LuiAFK的代码写的是真难绷，实现无限弹药的效果，不用CanConsumeAmmo，去用OnConsumeAmmo，
        //检测符合条件在OnConsumeAmmo里面给弹药数量++，以抵消原版的弹药数量消耗，玛德你是一点都不想照顾其他模组对消耗状态的判断啊，活全家了你
        //我是真无语要适配你这个效果还得专门反射，那配置也写的幽默的一笔，改动一个弹药不消耗上限还得重载模组
        internal static bool LuiAFKSetAmmoIsNoConsume(Item ammoItem) {
            if (CWRMod.Instance.luiafk == null) {
                return false;
            }

            if (LuiAFKConfig_RangerAmmoInfo == null) {
                return false;
            }

            try {
                LuiAFKConfig_ConfigInstance ??= CWRMod.Instance.luiafk.Find<ModConfig>("LuiAFKConfig");//懒加载一下
                int rangerAmmo = (int)LuiAFKConfig_RangerAmmoInfo.GetValue(LuiAFKConfig_ConfigInstance);
                //懒人的设定是为0就不启用，所以这里判断一下期望的无限弹药阈值是否大于0
                if (rangerAmmo > 0 && ammoItem.stack >= rangerAmmo) {
                    return true;
                }
            } catch {
                return false;
            }

            return false;
        }
    }
}
