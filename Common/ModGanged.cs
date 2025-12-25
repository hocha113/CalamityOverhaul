using CalamityOverhaul.Content;
using CalamityOverhaul.Content.RangedModify.Core;
using InnoVault.GameSystem;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Reflection;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent.UI.BigProgressBar;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.Config;
using Terraria.ModLoader.Core;
using Terraria.Utilities;
using static CalamityOverhaul.CWRUtils;

namespace CalamityOverhaul.Common
{
    [CWRJITEnabled]
    internal class ModGanged : ICWRLoader
    {
        #region Data
        public delegate bool On_BOOL_Dalegate();
        public delegate void On_PostAI_Dalegate(object obj, Projectile projectile);
        public delegate void On_ModPlayerDraw_Dalegate(object obj, ref PlayerDrawSet drawInfo);
        public delegate void On_Tooltips_Dalegate(Item item, List<TooltipLine> tooltips);
        public delegate void On_VoidFunc_Instance_Dalegate(object inds);
        public delegate bool On_ShouldForceUseAnim_Dalegate(Player player, Item item);
        public delegate void On_TrO_ItemPowerAttacks_Load_Dalegate(object obj);
        public delegate bool On_TrO_Broadsword_ShouldApplyItemOverhaul_Dalegate(object obj, Item item);
        public delegate bool On_AttemptPowerAttackStart_Dalegate(object obj, Item item, Player player);
        public delegate bool On_OnSpawnEnchCanAffectProjectile_Dalegate(Projectile projectile, bool allowMinions);
        public delegate void On_BossHealthBarManager_Draw_Dalegate(object obj, SpriteBatch spriteBatch, IBigProgressBar currentBar, BigProgressBarInfo info);
        public delegate int On_GetReworkedReforge_Dalegate(Item item, UnifiedRandom rand, int currentPrefix);

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

        public static Type weaponDisplayLite_ModifyDrawInfo_Type;
        public static MethodBase weaponDisplayLite_ModifyDrawInfo_Method;

        public static Type[] trOCodeTypes;
        public static Type trO_itemPowerAttacksTypes;
        public static Type trO_MuzzleflashPlayerDL_Type;
        public static Type trO_ArrowPlayerDL_Type;
        public static Type trO_PlayerHoldOutAnimation_Type;
        public static Type trO_CrosshairSystem_Type;
        public static Type trO_Broadsword_Type;
        public static MethodBase trO_MuzzleflashPlayerDL_Draw_Method;
        public static MethodBase trO_ArrowPlayerDL_Draw_Method;
        public static MethodBase trO_PlayerHoldOutAnimation_Method;
        public static MethodBase trO_Crosshair_AddImpulse_Method;
        public static MethodBase trO_itemPowerAttacksTypes_Load_Method;
        public static MethodBase trO_Broadsword_ShouldApplyItemOverhaul_Method;
        public static MethodBase trO_itemPowerAttacksTypes_AttemptPowerAttackStart_Method;

        public static Type[] fargowiltasSoulsTypes;
        public static Type FGS_Utils_Type;
        public static Type FGS_FGSGlobalProj_Type;
        public static MethodBase FGS_Utils_OnSpawnEnchCanAffectProjectile_Method;
        public static MethodBase FGS_FGSGlobalProj_PostAI_Method;

        public static Type[] coolerItemVisualEffectTypes;
        public static Type coolerItemVisualEffectPlayerType;
        public static MethodBase coolerItemVisualEffect_Method;

        public static Type MS_Config_Type;
        public static FieldInfo MS_Config_recursionCraftingDepth_FieldInfo;
        #endregion
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
                    VaultHook.Add(weaponOut_WeaponLayer_1_Method, On_MP_Draw_1_Hook);
                }
                else {
                    LogFailedLoad("weaponOut_WeaponLayer_1_Method", "WeaponLayer1.Draw");
                }

                if (weaponOut_WeaponLayer_2_Type != null) {
                    weaponOut_WeaponLayer_2_Method = weaponOut_WeaponLayer_2_Type.GetMethod("Draw", BindingFlags.Instance | BindingFlags.NonPublic);
                }
                if (weaponOut_WeaponLayer_2_Method != null) {
                    VaultHook.Add(weaponOut_WeaponLayer_2_Method, On_MP_Draw_2_Hook);
                }
                else {
                    LogFailedLoad("weaponOut_WeaponLayer_2_Method", "WeaponLayer12.Draw");
                }
            }
            else {
                LogModNotLoaded("WeaponOut");
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
                    weaponDisplay_ModifyDrawInfo_Method = weaponDisplay_ModifyDrawInfo_Type
                        .GetMethod("ModifyDrawInfo", BindingFlags.Instance | BindingFlags.Public);
                }
                else {
                    LogFailedLoad("weaponDisplay_ModifyDrawInfo_Method", "WeaponDisplayPlayer.ModifyDrawInfo");
                }
                if (weaponDisplay_ModifyDrawInfo_Method != null) {
                    VaultHook.Add(weaponDisplay_ModifyDrawInfo_Method, On_MP_Draw_3_Hook);
                }
            }
            else {
                LogModNotLoaded("WeaponDisplay");
            }
            #endregion

            #region weaponDisplayLite

            if (CWRMod.Instance.weaponDisplayLite != null) {
                var codes = AssemblyManager.GetLoadableTypes(CWRMod.Instance.weaponDisplayLite.Code);
                foreach (Type type in codes) {
                    if (type.Name == "WeaponDisplayPlayer") {
                        weaponDisplayLite_ModifyDrawInfo_Type = type;
                    }
                }

                if (weaponDisplayLite_ModifyDrawInfo_Type != null) {
                    weaponDisplayLite_ModifyDrawInfo_Method = weaponDisplayLite_ModifyDrawInfo_Type
                        .GetMethod("ModifyDrawInfo", BindingFlags.Instance | BindingFlags.Public);
                }
                else {
                    LogFailedLoad("weaponDisplayLite_ModifyDrawInfo_Method", "WeaponDisplayPlayerLite.ModifyDrawInfo");
                }
                if (weaponDisplayLite_ModifyDrawInfo_Method != null) {
                    VaultHook.Add(weaponDisplayLite_ModifyDrawInfo_Method, On_MP_Draw_5_Hook);
                }
            }
            else {
                LogModNotLoaded("WeaponDisplayLite");
            }

            #endregion

            #region terrariaOverhaul

            if (CWRMod.Instance.terrariaOverhaul != null) {
                trOCodeTypes = AssemblyManager.GetLoadableTypes(CWRMod.Instance.terrariaOverhaul.Code);
                foreach (Type type in trOCodeTypes) {
                    if (type.Name == "MuzzleflashPlayerDrawLayer") {
                        trO_MuzzleflashPlayerDL_Type = type;
                    }
                    else if (type.Name == "ArrowPlayerDrawLayer") {
                        trO_ArrowPlayerDL_Type = type;
                    }
                    else if (type.Name == "PlayerHoldOutAnimation") {
                        trO_PlayerHoldOutAnimation_Type = type;
                    }
                    else if (type.Name == "CrosshairSystem") {
                        trO_CrosshairSystem_Type = type;
                    }
                    else if (type.Name == "ItemPowerAttacks") {
                        trO_itemPowerAttacksTypes = type;
                    }
                    else if (type.Name == "Broadsword") {
                        trO_Broadsword_Type = type;
                    }
                }

                if (trO_PlayerHoldOutAnimation_Type != null) {
                    trO_PlayerHoldOutAnimation_Method = trO_PlayerHoldOutAnimation_Type.GetMethod("ShouldForceUseAnim", BindingFlags.Static | BindingFlags.NonPublic);
                    if (trO_PlayerHoldOutAnimation_Method != null) {
                        VaultHook.Add(trO_PlayerHoldOutAnimation_Method, On_ShouldForceUseAnim_Hook);
                    }
                    else {
                        LogFailedLoad("trO_PlayerHoldOutAnimation_Method", "PlayerHoldOutAnimation.ShouldForceUseAnim");
                    }
                }
                else {
                    LogFailedLoad("trO_PlayerHoldOutAnimation_Type", "TerrariaOverhaul.PlayerHoldOutAnimation");
                }

                if (trO_CrosshairSystem_Type != null) {
                    trO_Crosshair_AddImpulse_Method = trO_CrosshairSystem_Type.GetMethod("AddImpulse", BindingFlags.Static | BindingFlags.Public);
                    if (trO_Crosshair_AddImpulse_Method != null) {

                    }
                    else {
                        LogFailedLoad("trO_Crosshair_AddImpulse_Method", "TerrariaOverhaul.CrosshairSystem.AddImpulse");
                    }
                }
                else {
                    LogFailedLoad("trO_CrosshairSystem_Type", "TerrariaOverhaul.CrosshairSystem");
                }

                if (trO_itemPowerAttacksTypes != null) {
                    trO_itemPowerAttacksTypes_AttemptPowerAttackStart_Method = trO_itemPowerAttacksTypes.GetMethod("AttemptPowerAttackStart", BindingFlags.Instance | BindingFlags.Public);
                    if (trO_itemPowerAttacksTypes_AttemptPowerAttackStart_Method != null) {
                        VaultHook.Add(trO_itemPowerAttacksTypes_AttemptPowerAttackStart_Method, On_AttemptPowerAttackStart_Hook);
                    }
                    else {
                        LogFailedLoad("trO_itemPowerAttacksTypes_AttemptPowerAttackStart_Method", "TerrariaOverhaul.ItemPowerAttacks.AttemptPowerAttackStart");
                    }
                }
                else {
                    LogFailedLoad("trO_itemPowerAttacksTypes", "TerrariaOverhaul.ItemPowerAttacks");
                }
            }
            else {
                LogModNotLoaded("TerrariaOverhaul");
            }

            #endregion

            #region catalystMod

            if (CWRMod.Instance.catalystMod != null) {

            }
            else {
                LogModNotLoaded("CatalystMod");
            }

            #endregion

            #region fargowiltasSouls

            if (CWRMod.Instance.fargowiltasSouls != null) {
                fargowiltasSoulsTypes = GetModTypes(CWRMod.Instance.fargowiltasSouls);
                FGS_FGSGlobalProj_Type = GetTargetTypeInStringKey(fargowiltasSoulsTypes, "FargoSoulsGlobalProjectile");
                FGS_Utils_Type = GetTargetTypeInStringKey(fargowiltasSoulsTypes, "FargoSoulsUtil");

                if (FGS_Utils_Type != null) {
                    FGS_Utils_OnSpawnEnchCanAffectProjectile_Method = FGS_Utils_Type
                        .GetMethod("OnSpawnEnchCanAffectProjectile", BindingFlags.Static | BindingFlags.Public);
                }
                if (FGS_FGSGlobalProj_Type != null) {
                    FGS_FGSGlobalProj_PostAI_Method = FGS_FGSGlobalProj_Type.GetMethod("PostAI", BindingFlags.Instance | BindingFlags.Public);
                }

                if (FGS_Utils_OnSpawnEnchCanAffectProjectile_Method != null) {
                    VaultHook.Add(FGS_Utils_OnSpawnEnchCanAffectProjectile_Method, On_OnSpawnEnchCanAffectProjectile_Hook);
                }
                else {
                    LogFailedLoad("FGS_Utils_OnSpawnEnchCanAffectProjectile_Method", "FargoSoulsUtil.OnSpawnEnchCanAffectProjectile");
                }

                if (FGS_FGSGlobalProj_PostAI_Method != null) {
                    VaultHook.Add(FGS_FGSGlobalProj_PostAI_Method, On_FGS_FGSGlobalProj_PostAI_Hook);
                }
                else {
                    LogFailedLoad("FGS_FGSGlobalProj_PostAI_Method", "FargoSoulsGlobalProjectile.PostAI");
                }
            }
            else {
                LogModNotLoaded("FargowiltasSouls");
            }

            #endregion

            #region coolerItemVisualEffect

            if (CWRMod.Instance.coolerItemVisualEffect != null) {
                coolerItemVisualEffectTypes = AssemblyManager.GetLoadableTypes(CWRMod.Instance.coolerItemVisualEffect.Code);
                foreach (Type type in coolerItemVisualEffectTypes) {
                    if (type.Name == "MeleeModifyPlayer") {
                        coolerItemVisualEffectPlayerType = type;
                    }
                }
                if (coolerItemVisualEffectPlayerType != null) {
                    coolerItemVisualEffect_Method = coolerItemVisualEffectPlayerType
                        .GetMethod("ModifyDrawInfo", BindingFlags.Instance | BindingFlags.Public);
                }
                if (coolerItemVisualEffect_Method != null) {
                    VaultHook.Add(coolerItemVisualEffect_Method, On_MP_Draw_4_Hook);
                }
                else {
                    LogFailedLoad("coolerItemVisualEffect_Method", "MeleeModifyPlayer.ModifyDrawInfo");
                }
            }
            else {
                LogModNotLoaded("CoolerItemVisualEffect");
            }

            #endregion

            #region MagicStorage

            if (CWRMod.Instance.magicStorage != null) {
                MS_Config_Type = GetTargetTypeInStringKey(GetModTypes(CWRMod.Instance.magicStorage), "MagicStorageConfig");
                if (MS_Config_Type != null) {
                    MS_Config_recursionCraftingDepth_FieldInfo = MS_Config_Type
                        .GetField("recursionCraftingDepth", BindingFlags.Public | BindingFlags.Instance);
                }
                else {
                    LogFailedLoad("MagicStorage_MagicStorageConfig_Typ", "MagicStorage.MagicStorageConfig");
                }
            }
            else {
                LogModNotLoaded("MagicStorage");
            }

            #endregion

            CWRRef.LoadComders();
        }

        void ICWRLoader.UnLoadData() {
            weaponOutCodeTypes = null;
            weaponOut_DrawToolType = null;
            on_weaponOut_DrawTool_Method = null;
            weaponOut_WeaponLayer_1_Type = null;
            weaponOut_WeaponLayer_1_Method = null;
            weaponOut_WeaponLayer_2_Type = null;
            weaponOut_WeaponLayer_2_Method = null;
            weaponDisplayCodeTypes = null;
            weaponDisplay_ModifyDrawInfo_Type = null;
            weaponDisplay_ModifyDrawInfo_Method = null;
            trOCodeTypes = null;
            trO_itemPowerAttacksTypes = null;
            trO_MuzzleflashPlayerDL_Type = null;
            trO_ArrowPlayerDL_Type = null;
            trO_PlayerHoldOutAnimation_Type = null;
            trO_CrosshairSystem_Type = null;
            trO_Broadsword_Type = null;
            trO_MuzzleflashPlayerDL_Draw_Method = null;
            trO_ArrowPlayerDL_Draw_Method = null;
            trO_PlayerHoldOutAnimation_Method = null;
            trO_Crosshair_AddImpulse_Method = null;
            trO_itemPowerAttacksTypes_Load_Method = null;
            trO_Broadsword_ShouldApplyItemOverhaul_Method = null;
            trO_itemPowerAttacksTypes_AttemptPowerAttackStart_Method = null;
            fargowiltasSoulsTypes = null;
            FGS_Utils_Type = null;
            FGS_Utils_OnSpawnEnchCanAffectProjectile_Method = null;
            coolerItemVisualEffectTypes = null;
            coolerItemVisualEffectPlayerType = null;
            coolerItemVisualEffect_Method = null;
            MS_Config_Type = null;
            MS_Config_recursionCraftingDepth_FieldInfo = null;
        }

        internal static void On_EditEnrageTooltips_Hook(On_Tooltips_Dalegate orig, Item item, List<TooltipLine> tooltips) {
            orig.Invoke(item, tooltips);
        }

        internal static void On_FGS_FGSGlobalProj_PostAI_Hook(On_PostAI_Dalegate orig, object instance, Projectile projectile) {
            if (projectile.hide) {
                if (projectile.ModProjectile is BaseHeldRanged ranged && !ranged.CanFire) {
                    return;
                }
            }
            orig.Invoke(instance, projectile);
        }

        internal static bool Has_MS_Config_recursionCraftingDepth(out ModConfig modConfig) {
            modConfig = null;
            if (CWRMod.Instance.magicStorage == null) {
                return false;
            }
            if (MS_Config_recursionCraftingDepth_FieldInfo == null) {
                return false;
            }

            try {
                modConfig = CWRMod.Instance.magicStorage.Find<ModConfig>("MagicStorageConfig");
                int recursionCraftingDepthNum = (int)MS_Config_recursionCraftingDepth_FieldInfo.GetValue(modConfig);
                if (recursionCraftingDepthNum == 0) {
                    return false;
                }
            } catch {
                return false;
            }

            return true;
        }

        private static bool On_AttemptPowerAttackStart_Hook(On_AttemptPowerAttackStart_Dalegate orig, object obj, Item item, Player player) {
            return item.IsAir || item.type == ItemID.None ? false : orig.Invoke(obj, item, player);
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

            CWRItem ritem = heldItem.CWR();
            CWRPlayer modPlayer = drawPlayer.CWR();
            bool itemHasHeldProj = ritem.heldProjType > 0;
            if (ritem.hasHeldNoCanUseBool && itemHasHeldProj) {
                if (modPlayer.TryGetInds_BaseHeldRanged(out BaseHeldRanged ranged)) {
                    if (ranged.OnHandheldDisplayBool) {
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

        private static void On_MP_Draw_1_Hook(On_ModPlayerDraw_Dalegate orig, object obj, ref PlayerDrawSet drawInfo) {
            if (!IFDrawHeld(orig, drawInfo)) {
                return;
            }
            orig.Invoke(obj, ref drawInfo);
        }

        private static void On_MP_Draw_2_Hook(On_ModPlayerDraw_Dalegate orig, object obj, ref PlayerDrawSet drawInfo) {
            if (!IFDrawHeld(orig, drawInfo)) {
                return;
            }
            orig.Invoke(obj, ref drawInfo);
        }

        private static void On_MP_Draw_3_Hook(On_ModPlayerDraw_Dalegate orig, object obj, ref PlayerDrawSet drawInfo) {
            if (!IFDrawHeld(orig, drawInfo)) {
                return;
            }
            orig.Invoke(obj, ref drawInfo);
        }

        private static void On_MP_Draw_4_Hook(On_ModPlayerDraw_Dalegate orig, object obj, ref PlayerDrawSet drawInfo) {
            if (!IFDrawHeld(orig, drawInfo)) {
                return;
            }
            orig.Invoke(obj, ref drawInfo);
        }

        private static void On_MP_Draw_5_Hook(On_ModPlayerDraw_Dalegate orig, object obj, ref PlayerDrawSet drawInfo) {
            if (!IFDrawHeld(orig, drawInfo)) {
                return;
            }
            orig.Invoke(obj, ref drawInfo);
        }

        private static bool On_ShouldForceUseAnim_Hook(On_ShouldForceUseAnim_Dalegate orig, Player player, Item item) {
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

            CWRItem ritem = heldItem.CWR();
            CWRPlayer modPlayer = player.CWR();
            bool isHeld = ritem.isHeldItem || ritem.heldProjType > 0;
            if (isHeld) {
                result = false;
            }

            if (!CWRServerConfig.Instance.WeaponHandheldDisplay) {
                result = true;
            }

            if (ritem.hasHeldNoCanUseBool && ritem.heldProjType > 0) {
                if (modPlayer.TryGetInds_BaseHeldRanged(out BaseHeldRanged ranged)) {
                    if (ranged.OnHandheldDisplayBool) {
                        result = false;
                    }
                }
            }

            return orig.Invoke(player, item) && result;
        }
    }
}
