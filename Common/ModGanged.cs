using System;
using System.Collections.Generic;
using System.Reflection;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.Core;

namespace CalamityOverhaul.Common
{
    internal class ModGanged
    {
        public delegate void On_ModPlayerDraw_Dalegate(object obj, ref PlayerDrawSet drawInfo);
        public delegate void On_VoidFunc_Dalegate(ref PlayerDrawSet drawInfo, bool drawOnBack);
        public delegate void On_VoidFunc_Instance_Dalegate(object inds);

        public static Type[] weaponOutCodeTypes;

        public static Type weaponOut_DrawToolType;
        public static MethodBase on_weaponOut_DrawTool_Method;

        public static Type weaponOut_WeaponLayer_1_Type;
        public static MethodBase weaponOut_WeaponLayer_1_Method;

        public static Type weaponOut_WeaponLayer_2_Type;
        public static MethodBase weaponOut_WeaponLayer_2_Method;

        public static void Load() {
            if (CWRMod.Instance.weaponOut != null) {
                weaponOutCodeTypes = AssemblyManager.GetLoadableTypes(CWRMod.Instance.weaponOut.Code);
                foreach (Type type in weaponOutCodeTypes) {
                    if (type.Name == "DrawTool") {
                        weaponOut_DrawToolType = type;
                    }
                    if (type.Name == "WeaponLayer1") {
                        weaponOut_WeaponLayer_1_Type = type;
                    }
                    if (type.Name == "WeaponLayer2") {
                        weaponOut_WeaponLayer_2_Type = type;
                    }
                }

                if (weaponOut_WeaponLayer_1_Type != null) {
                    weaponOut_WeaponLayer_1_Method = weaponOut_WeaponLayer_1_Type.GetMethod("Draw", BindingFlags.Instance | BindingFlags.NonPublic);
                }
                if (weaponOut_WeaponLayer_1_Method != null) {
                    MonoModHooks.Add(weaponOut_WeaponLayer_1_Method, On_MP_Draw_1_Hook);
                }
                else {
                    "未成功加载 weaponOut_WeaponLayer_1_Method 是否是WeaponLayer1.Draw已经改动?".DompInConsole();
                }

                if (weaponOut_WeaponLayer_2_Type != null) {
                    weaponOut_WeaponLayer_2_Method = weaponOut_WeaponLayer_2_Type.GetMethod("Draw", BindingFlags.Instance | BindingFlags.NonPublic);
                }
                if (weaponOut_WeaponLayer_2_Method != null) {
                    MonoModHooks.Add(weaponOut_WeaponLayer_2_Method, On_MP_Draw_2_Hook);
                }
                else {
                    "未成功加载 weaponOut_WeaponLayer_2_Method 是否是WeaponLayer12.Draw已经改动?".DompInConsole();
                }
            }
            else {
                "未加载模组 WeaponOut".DompInConsole();
            }
        }

        private static bool IFDrawHeld(On_ModPlayerDraw_Dalegate orig, PlayerDrawSet drawInfo) {
            if (EqualityComparer<PlayerDrawSet>.Default.Equals(drawInfo, default(PlayerDrawSet))) {
                return false;
            }
            if (drawInfo.DrawDataCache == null) {
                return false;
            }
            if (drawInfo.DustCache == null) {
                return false;
            }
            Player drawPlayer = drawInfo.drawPlayer;
            Item heldItem = drawPlayer.inventory[drawPlayer.selectedItem];
            if (heldItem == null) {
                return false;
            }
            if (heldItem.type == ItemID.None) {
                return false;
            }
            bool isHeld = heldItem.CWR().isHeldItem || heldItem.CWR().heldProjType > 0;
            if (isHeld) {
                return false;
            }
            if (orig == null) {
                return false;
            }
            return true;
        }

        public static void On_MP_Draw_1_Hook(On_ModPlayerDraw_Dalegate orig, object obj, ref PlayerDrawSet drawInfo) {
            if (!IFDrawHeld(orig, drawInfo)) {
                return;
            }
            orig.Invoke(obj, ref  drawInfo);
        }

        public static void On_MP_Draw_2_Hook(On_ModPlayerDraw_Dalegate orig, object obj, ref PlayerDrawSet drawInfo) {
            if (!IFDrawHeld(orig, drawInfo)) {
                return;
            }
            orig.Invoke(obj, ref drawInfo);
        }
    }
}
