using CalamityMod.Graphics.Renderers;
using CalamityOverhaul.Content;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged;
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
        public delegate bool On_ShouldForceUseAnim_Dalegate(Player player, Item item);
        public delegate bool On_OnSpawnEnchCanAffectProjectile_Dalegate(Projectile projectile, bool allowMinions);

        public static Type[] weaponOutCodeTypes;

        public static Type weaponOut_DrawToolType;
        public static MethodBase on_weaponOut_DrawTool_Method;

        public static Type weaponOut_WeaponLayer_1_Type;
        public static MethodBase weaponOut_WeaponLayer_1_Method;

        public static Type weaponOut_WeaponLayer_2_Type;
        public static MethodBase weaponOut_WeaponLayer_2_Method;

        public static Type[] weaponDisplayCodeTypes;
        public static Type weaponDisplay_ModifyDrawInfo_Type;
        public static MethodBase weaponDisplay_ModifyDrawInfo_Method;

        public static Type[] trOCodeTypes;
        public static Type trO_MuzzleflashPlayerDL_Type;
        public static Type trO_ArrowPlayerDL_Type;
        public static Type trO_PlayerHoldOutAnimation_Type;
        public static Type trO_CrosshairSystem_Type;
        public static MethodBase trO_MuzzleflashPlayerDL_Draw_Method;
        public static MethodBase trO_ArrowPlayerDL_Draw_Method;
        public static MethodBase trO_PlayerHoldOutAnimation_Method;
        public static MethodBase trO_Crosshair_AddImpulse_Method;

        public static Type[] fargowiltasSoulsTypes;
        public static Type fargowiltasSouls_Utils_Type;
        public static MethodBase fS_Utils_OnSpawnEnchCanAffectProjectile_Method;

        public static Type[] GetModType(Mod mod) {
            return AssemblyManager.GetLoadableTypes(mod.Code);
        }

        public static Type GetTargetTypeInStringKey(Type[] types, string key) {
            Type reset = null;
            foreach (Type type in types) {
                if (type.Name == key) {
                    reset = type;
                }
            }
            return reset;
        }

        public static void Load() {
            #region weaponOut
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
            #endregion

            #region weaponDisplay
            if (CWRMod.Instance.weaponDisplay != null) {
                weaponDisplayCodeTypes = AssemblyManager.GetLoadableTypes(CWRMod.Instance.weaponDisplay.Code);
                foreach (Type type in weaponDisplayCodeTypes) {
                    if (type.Name == "WeaponDisplayPlayer") {
                        weaponDisplay_ModifyDrawInfo_Type = type;
                    }
                }

                if (weaponDisplay_ModifyDrawInfo_Type != null) {
                    weaponDisplay_ModifyDrawInfo_Method = weaponDisplay_ModifyDrawInfo_Type.GetMethod("ModifyDrawInfo", BindingFlags.Instance | BindingFlags.Public);
                }
                else {
                    "未成功加载 weaponDisplay_ModifyDrawInfo_Method 是否是WeaponDisplayPlayer.ModifyDrawInfo已经改动?".DompInConsole();
                }
                if (weaponDisplay_ModifyDrawInfo_Method != null) {
                    MonoModHooks.Add(weaponDisplay_ModifyDrawInfo_Method, On_MP_Draw_3_Hook);
                }
            }
            else {
                "未加载模组 WeaponDisplay".DompInConsole();
            }
            #endregion

            #region terrariaOverhaul

            if (CWRMod.Instance.terrariaOverhaul != null) {
                trOCodeTypes = AssemblyManager.GetLoadableTypes(CWRMod.Instance.terrariaOverhaul.Code);
                foreach (Type type in trOCodeTypes) {
                    if (type.Name == "MuzzleflashPlayerDrawLayer") {
                        trO_MuzzleflashPlayerDL_Type = type;
                    }
                    if (type.Name == "ArrowPlayerDrawLayer") {
                        trO_ArrowPlayerDL_Type = type;
                    }
                    if (type.Name == "PlayerHoldOutAnimation") {
                        trO_PlayerHoldOutAnimation_Type = type;
                    }
                    if (type.Name == "CrosshairSystem") {
                        trO_CrosshairSystem_Type = type;
                    }
                }
                if (trO_PlayerHoldOutAnimation_Type != null) {
                    trO_PlayerHoldOutAnimation_Method = trO_PlayerHoldOutAnimation_Type.GetMethod("ShouldForceUseAnim", BindingFlags.Static | BindingFlags.NonPublic);
                }
                if (trO_PlayerHoldOutAnimation_Method != null) {
                    MonoModHooks.Add(trO_PlayerHoldOutAnimation_Method, On_ShouldForceUseAnim_Hook);
                }
                else {
                    "未成功加载 trO_PlayerHoldOutAnimation_Method 是否是 PlayerHoldOutAnimation.ShouldForceUseAnim 已经改动?".DompInConsole();
                }

                if (trO_CrosshairSystem_Type != null) {
                    trO_Crosshair_AddImpulse_Method = trO_CrosshairSystem_Type.GetMethod("AddImpulse", BindingFlags.Static | BindingFlags.Public);
                }
                else {
                    "未成功加载 trO_CrosshairSystem_Type 是否是 TerrariaOverhaul.CrosshairSystem 已经改动?".DompInConsole();
                }

                if (trO_Crosshair_AddImpulse_Method != null) {

                }
                else {
                    "未成功加载 trO_Crosshair_AddImpulse_Method 是否是 TerrariaOverhaul.CrosshairSystem.AddImpulse 已经改动?".DompInConsole();
                }
            }
            else {
                "未加载模组 TerrariaOverhaul".DompInConsole();
            }

            #endregion

            #region catalystMod

            if (CWRMod.Instance.catalystMod != null) {

            }
            else {
                "未加载模组 CatalystMod".DompInConsole();
            }

            #endregion

            #region fargowiltasSouls

            if (CWRMod.Instance.fargowiltasSouls != null) {
                fargowiltasSoulsTypes = GetModType(CWRMod.Instance.fargowiltasSouls);
                fargowiltasSouls_Utils_Type = GetTargetTypeInStringKey(fargowiltasSoulsTypes, "FargoSoulsUtil");
                if (fargowiltasSouls_Utils_Type != null) {
                    fS_Utils_OnSpawnEnchCanAffectProjectile_Method = fargowiltasSouls_Utils_Type.GetMethod("OnSpawnEnchCanAffectProjectile", BindingFlags.Static | BindingFlags.Public);
                }
                if (fS_Utils_OnSpawnEnchCanAffectProjectile_Method != null) {
                    MonoModHooks.Add(fS_Utils_OnSpawnEnchCanAffectProjectile_Method, On_OnSpawnEnchCanAffectProjectile_Hook);
                }
                else {
                    "未成功加载 fS_Utils_OnSpawnEnchCanAffectProjectile_Method FargoSoulsUtil.OnSpawnEnchCanAffectProjectile已经改动?".DompInConsole();
                }
            }
            else {
                "未加载模组 FargowiltasSouls".DompInConsole();
            }

            #endregion
        }

        private static bool On_OnSpawnEnchCanAffectProjectile_Hook(On_OnSpawnEnchCanAffectProjectile_Dalegate orig, Projectile projectile, bool allowMinions) {
            return !projectile.CWR().NotSubjectToSpecialEffects && orig.Invoke(projectile, allowMinions);
        }

        private static bool IFDrawHeld(On_ModPlayerDraw_Dalegate orig, PlayerDrawSet drawInfo) {
            if (EqualityComparer<PlayerDrawSet>.Default.Equals(drawInfo, default)) {
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

            CWRItems ritem = heldItem.CWR();
            CWRPlayer modPlayer = drawPlayer.CWR();
            bool itemHasHeldProj = ritem.heldProjType > 0;
            if (ritem.hasHeldNoCanUseBool && itemHasHeldProj) {
                if (modPlayer.TryGetInds_BaseFeederGun(out BaseFeederGun gun)) {
                    if (gun.OnHandheldDisplayBool) {
                        return false;
                    }
                }
                if (modPlayer.TryGetInds_BaseGun(out BaseGun gun2)) {
                    if (gun2.OnHandheldDisplayBool) {
                        return false;
                    }
                }
                if (modPlayer.TryGetInds_BaseBow(out BaseBow bow)) {
                    if (bow.OnHandheldDisplayBool) {
                        return false;
                    }
                }
            }

            if (!CWRServerConfig.Instance.WeaponHandheldDisplay) {
                return true;
            }

            bool isHeld = ritem.isHeldItem || itemHasHeldProj;
            return !isHeld && orig != null;
        }

        public static void On_MP_Draw_1_Hook(On_ModPlayerDraw_Dalegate orig, object obj, ref PlayerDrawSet drawInfo) {
            if (!IFDrawHeld(orig, drawInfo)) {
                return;
            }
            orig.Invoke(obj, ref drawInfo);
        }

        public static void On_MP_Draw_2_Hook(On_ModPlayerDraw_Dalegate orig, object obj, ref PlayerDrawSet drawInfo) {
            if (!IFDrawHeld(orig, drawInfo)) {
                return;
            }
            orig.Invoke(obj, ref drawInfo);
        }

        public static void On_MP_Draw_3_Hook(On_ModPlayerDraw_Dalegate orig, object obj, ref PlayerDrawSet drawInfo) {
            if (!IFDrawHeld(orig, drawInfo)) {
                return;
            }
            orig.Invoke(obj, ref drawInfo);
        }

        public static bool On_ShouldForceUseAnim_Hook(On_ShouldForceUseAnim_Dalegate orig, Player player, Item item) {
            bool result = true;

            if (item == null) {
                result = false;
            }
            if (item.type == ItemID.None) {
                result = false;
            }

            Item heldItem = player.inventory[player.selectedItem];
            if (heldItem == null) {
                return false;
            }
            if (heldItem.type == ItemID.None) {
                return false;
            }

            CWRItems ritem = heldItem.CWR();
            CWRPlayer modPlayer = player.CWR();
            bool isHeld = ritem.isHeldItem || ritem.heldProjType > 0;
            if (isHeld) {
                result = false;
            }

            if (!CWRServerConfig.Instance.WeaponHandheldDisplay) {
                result = true;
            }

            if (ritem.hasHeldNoCanUseBool && ritem.heldProjType > 0) {
                if (modPlayer.TryGetInds_BaseFeederGun(out BaseFeederGun gun)) {
                    if (gun.OnHandheldDisplayBool) {
                        result = false;
                    }
                }
                if (modPlayer.TryGetInds_BaseGun(out BaseGun gun2)) {
                    if (gun2.OnHandheldDisplayBool) {
                        result = false;
                    }
                }
                if (modPlayer.TryGetInds_BaseBow(out BaseBow bow)) {
                    if (bow.OnHandheldDisplayBool) {
                        result = false;
                    }
                }
            }

            return orig.Invoke(player, item) && result;
        }
    }
}
