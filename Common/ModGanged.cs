using CalamityMod;
using CalamityMod.Events;
using CalamityMod.Items.Weapons.Melee;
using CalamityMod.UI;
using CalamityOverhaul.Content;
using CalamityOverhaul.Content.Events;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.Core;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent.Events;
using Terraria.GameContent.Prefixes;
using Terraria.GameContent.UI.BigProgressBar;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.Config;
using Terraria.ModLoader.Core;
using Terraria.Utilities;
using static Terraria.ModLoader.ModContent;

namespace CalamityOverhaul.Common
{
    internal class ModGanged
    {
        #region Data
        public delegate bool On_BOOL_Dalegate();
        public delegate void On_PostAI_Dalegate(object obj, Projectile projectile);
        public delegate void On_ModPlayerDraw_Dalegate(object obj, ref PlayerDrawSet drawInfo);
        public delegate void On_VoidFunc_Dalegate(ref PlayerDrawSet drawInfo, bool drawOnBack);
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

        public static MethodBase BossHealthBarManager_Draw_Method;

        public static Type MS_Config_Type;
        public static FieldInfo MS_Config_recursionCraftingDepth_FieldInfo;

        public static MethodBase calamityUtils_GetReworkedReforge_Method;

        internal static bool InfernumModeOpenState => 
            CWRMod.Instance.infernum == null ? false : (bool)CWRMod.Instance.infernum.Call("GetInfernumActive");
        #endregion
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

        static string text1 => CWRUtils.Translation("未成功加载", "Failed load");
        static string text2 => CWRUtils.Translation("是否是", "whether it is");
        static string text3 => CWRUtils.Translation("已经改动?", "Has it been changed?");
        static string text4 => CWRUtils.Translation("未加载模组", "The mod is not loaded");
        static void Domp1(string value1, string value2) {
            $"{text1} {value1} {text2} {value2} {text3}".DompInConsole();
        }
        static void Domp2(string value1) {
            $"{text4} {value1}".DompInConsole();
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
                    CWRHook.Add(weaponOut_WeaponLayer_1_Method, On_MP_Draw_1_Hook);
                }
                else {
                    Domp1("weaponOut_WeaponLayer_1_Method", "WeaponLayer1.Draw");
                }

                if (weaponOut_WeaponLayer_2_Type != null) {
                    weaponOut_WeaponLayer_2_Method = weaponOut_WeaponLayer_2_Type.GetMethod("Draw", BindingFlags.Instance | BindingFlags.NonPublic);
                }
                if (weaponOut_WeaponLayer_2_Method != null) {
                    CWRHook.Add(weaponOut_WeaponLayer_2_Method, On_MP_Draw_2_Hook);
                }
                else {
                    Domp1("weaponOut_WeaponLayer_2_Method", "WeaponLayer12.Draw");
                }
            }
            else {
                Domp2("WeaponOut");
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
                    Domp1("weaponDisplay_ModifyDrawInfo_Method", "WeaponDisplayPlayer.ModifyDrawInfo");
                }
                if (weaponDisplay_ModifyDrawInfo_Method != null) {
                    CWRHook.Add(weaponDisplay_ModifyDrawInfo_Method, On_MP_Draw_3_Hook);
                }
            }
            else {
                Domp2("WeaponDisplay");
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
                        CWRHook.Add(trO_PlayerHoldOutAnimation_Method, On_ShouldForceUseAnim_Hook);
                    }
                    else {
                        Domp1("trO_PlayerHoldOutAnimation_Method", "PlayerHoldOutAnimation.ShouldForceUseAnim");
                    }
                }
                else {
                    Domp1("trO_PlayerHoldOutAnimation_Type", "TerrariaOverhaul.PlayerHoldOutAnimation");
                }

                if (trO_CrosshairSystem_Type != null) {
                    trO_Crosshair_AddImpulse_Method = trO_CrosshairSystem_Type.GetMethod("AddImpulse", BindingFlags.Static | BindingFlags.Public);
                    if (trO_Crosshair_AddImpulse_Method != null) {

                    }
                    else {
                        Domp1("trO_Crosshair_AddImpulse_Method", "TerrariaOverhaul.CrosshairSystem.AddImpulse");
                    }
                }
                else {
                    Domp1("trO_CrosshairSystem_Type", "TerrariaOverhaul.CrosshairSystem");
                }

                if (trO_itemPowerAttacksTypes != null) {
                    trO_itemPowerAttacksTypes_AttemptPowerAttackStart_Method = trO_itemPowerAttacksTypes.GetMethod("AttemptPowerAttackStart", BindingFlags.Instance | BindingFlags.Public);
                    if (trO_itemPowerAttacksTypes_AttemptPowerAttackStart_Method != null) {
                        CWRHook.Add(trO_itemPowerAttacksTypes_AttemptPowerAttackStart_Method, On_AttemptPowerAttackStart_Hook);
                    }
                    else {
                        Domp1("trO_itemPowerAttacksTypes_AttemptPowerAttackStart_Method", "TerrariaOverhaul.ItemPowerAttacks.AttemptPowerAttackStart");
                    }
                }
                else {
                    Domp1("trO_itemPowerAttacksTypes", "TerrariaOverhaul.ItemPowerAttacks");
                }

                if (trO_Broadsword_Type != null) {
                    trO_Broadsword_ShouldApplyItemOverhaul_Method = trO_Broadsword_Type.GetMethod("ShouldApplyItemOverhaul", BindingFlags.Instance | BindingFlags.Public);
                    if (trO_Broadsword_ShouldApplyItemOverhaul_Method != null) {
                        CWRHook.Add(trO_Broadsword_ShouldApplyItemOverhaul_Method, On_ShouldApplyItemOverhaul_Hook);
                    }
                    else {
                        Domp1("trO_Broadsword_ShouldApplyItemOverhaul_Method", "TerrariaOverhaul.Broadsword.ShouldApplyItemOverhaul");
                    }
                }
                else {
                    Domp1("trO_Broadsword_Type", "TerrariaOverhaul.Broadsword");
                }
            }
            else {
                Domp2("TerrariaOverhaul");
            }

            #endregion

            #region catalystMod

            if (CWRMod.Instance.catalystMod != null) {

            }
            else {
                Domp2("CatalystMod");
            }

            #endregion

            #region fargowiltasSouls

            if (CWRMod.Instance.fargowiltasSouls != null) {
                fargowiltasSoulsTypes = GetModType(CWRMod.Instance.fargowiltasSouls);
                FGS_FGSGlobalProj_Type = GetTargetTypeInStringKey(fargowiltasSoulsTypes, "FargoSoulsGlobalProjectile");
                FGS_Utils_Type = GetTargetTypeInStringKey(fargowiltasSoulsTypes, "FargoSoulsUtil");

                if (FGS_Utils_Type != null) {
                    FGS_Utils_OnSpawnEnchCanAffectProjectile_Method = FGS_Utils_Type.GetMethod("OnSpawnEnchCanAffectProjectile", BindingFlags.Static | BindingFlags.Public);
                }
                if (FGS_FGSGlobalProj_Type != null) {
                    FGS_FGSGlobalProj_PostAI_Method = FGS_FGSGlobalProj_Type.GetMethod("PostAI", BindingFlags.Instance | BindingFlags.Public);
                }

                if (FGS_Utils_OnSpawnEnchCanAffectProjectile_Method != null) {
                    CWRHook.Add(FGS_Utils_OnSpawnEnchCanAffectProjectile_Method, On_OnSpawnEnchCanAffectProjectile_Hook);
                }
                else {
                    Domp1("FGS_Utils_OnSpawnEnchCanAffectProjectile_Method", "FargoSoulsUtil.OnSpawnEnchCanAffectProjectile");
                }

                if (FGS_FGSGlobalProj_PostAI_Method != null) {
                    CWRHook.Add(FGS_FGSGlobalProj_PostAI_Method, On_FGS_FGSGlobalProj_PostAI_Hook);
                }
                else {
                    Domp1("FGS_FGSGlobalProj_PostAI_Method", "FargoSoulsGlobalProjectile.PostAI");
                }
            }
            else {
                Domp2("FargowiltasSouls");
            }

            #endregion

            #region coolerItemVisualEffect

            if (CWRMod.Instance.coolerItemVisualEffect != null) {
                coolerItemVisualEffectTypes = AssemblyManager.GetLoadableTypes(CWRMod.Instance.coolerItemVisualEffect.Code);
                foreach (Type type in coolerItemVisualEffectTypes) {
                    if (type.Name == "CoolerItemVisualEffectPlayer") {
                        coolerItemVisualEffectPlayerType = type;
                    }
                }
                if (coolerItemVisualEffectPlayerType != null) {
                    coolerItemVisualEffect_Method = coolerItemVisualEffectPlayerType.GetMethod("ModifyDrawInfo", BindingFlags.Instance | BindingFlags.Public);
                }
                if (coolerItemVisualEffect_Method != null) {
                    CWRHook.Add(coolerItemVisualEffect_Method, On_MP_Draw_4_Hook);
                }
                else {
                    Domp1("coolerItemVisualEffect_Method", "CoolerItemVisualEffectPlayer.ModifyDrawInfo");
                }
            }
            else {
                Domp2("CoolerItemVisualEffect");
            }

            #endregion

            #region MagicStorage

            if (CWRMod.Instance.magicStorage != null) {
                MS_Config_Type = GetTargetTypeInStringKey(GetModType(CWRMod.Instance.magicStorage), "MagicStorageConfig");
                if (MS_Config_Type != null) {
                    MS_Config_recursionCraftingDepth_FieldInfo = MS_Config_Type.GetField("recursionCraftingDepth", BindingFlags.Public | BindingFlags.Instance);
                }
                else {
                    Domp1("MagicStorage_MagicStorageConfig_Typ", "MagicStorage.MagicStorageConfig");
                }
            }
            else {
                Domp2("MagicStorage");
            }

            #endregion

            #region calamityMod_noumenon

            //这一切不该发生，灾厄没有在这里留下任何可扩展的接口，如果想要那该死血条的为第三方事件靠边站，只能这么做，至少这是我目前能想到的方法
            BossHealthBarManager_Draw_Method = typeof(BossHealthBarManager).GetMethod("Draw", BindingFlags.Instance | BindingFlags.Public);
            if (BossHealthBarManager_Draw_Method != null) {
                CWRHook.Add(BossHealthBarManager_Draw_Method, On_BossHealthBarManager_Draw_Hook);
            }
            else {
                Domp1("BossHealthBarManager_Draw_Method", "CalamityMod.BossHealthBarManager");
            }

            calamityUtils_GetReworkedReforge_Method = typeof(CalamityUtils).GetMethod("GetReworkedReforge", BindingFlags.Static | BindingFlags.NonPublic);
            if (calamityUtils_GetReworkedReforge_Method != null) {
                CWRHook.Add(calamityUtils_GetReworkedReforge_Method, OnGetReworkedReforgeHook);
            }
            else {
                Domp1("calamityUtils_GetReworkedReforge_Method", "CalamityUtils.GetReworkedReforge");
            }

            #endregion
        }

        public static void UnLoad() {
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
            BossHealthBarManager_Draw_Method = null;
            MS_Config_Type = null;
            MS_Config_recursionCraftingDepth_FieldInfo = null;
            calamityUtils_GetReworkedReforge_Method = null;
        }

        internal static int OnGetReworkedReforgeHook(On_GetReworkedReforge_Dalegate orig
            , Item item, UnifiedRandom rand, int currentPrefix) {
            int reset = orig.Invoke(item, rand, currentPrefix);
            reset = OnCalamityReforgeEvent.HandleCalamityReforgeModificationDueToMissingItemLoader(item, rand, currentPrefix);
            return reset;
        }

        internal static void On_FGS_FGSGlobalProj_PostAI_Hook(On_PostAI_Dalegate orig, object instance, Projectile projectile) {
            if (projectile.hide) {
                if (projectile.ModProjectile is BaseHeldRanged ranged && !ranged.CanFire) {
                    return;
                }
            }
            orig.Invoke(instance, projectile);
        }

        internal static bool Set_MS_Config_recursionCraftingDepth() {
            if (CWRMod.Instance.magicStorage == null) {
                return false;
            }
            if (MS_Config_recursionCraftingDepth_FieldInfo == null) {
                return false;
            }

            ModConfig modConfig = CWRMod.Instance.magicStorage.Find<ModConfig>("MagicStorageConfig");
            int recursionCraftingDepthNum = ((int)MS_Config_recursionCraftingDepth_FieldInfo.GetValue(modConfig));
            if (recursionCraftingDepthNum == 0) {
                return false;
            }
            
            MS_Config_recursionCraftingDepth_FieldInfo.SetValue(modConfig, 0);
            return true;
        }

        private static bool On_ShouldApplyItemOverhaul_Hook(On_TrO_Broadsword_ShouldApplyItemOverhaul_Dalegate orig, object obj, Item item) {
            int[] noEffect = new int[] { ItemType<TrueBiomeBlade>(), ItemType<OmegaBiomeBlade>(), ItemType<BrokenBiomeBlade>(), };
            return noEffect.Contains(item.type) ? false : orig.Invoke(obj, item);
        }

        private static bool On_AttemptPowerAttackStart_Hook(On_AttemptPowerAttackStart_Dalegate orig, object obj, Item item, Player player) {
            return item.IsAir || item.type == ItemID.None ? false : orig.Invoke(obj, item, player);
        }

        private static void On_BossHealthBarManager_Draw_Hook(On_BossHealthBarManager_Draw_Dalegate orig, object obj, SpriteBatch spriteBatch, IBigProgressBar currentBar, BigProgressBarInfo info) {
            int startHeight = 100;
            int x = Main.screenWidth - 420;
            int y = Main.screenHeight - startHeight;
            if (Main.playerInventory || Main.invasionType > 0 || Main.pumpkinMoon
                || Main.snowMoon || DD2Event.Ongoing || AcidRainEvent.AcidRainEventIsOngoing
                || TungstenRiot.Instance.TungstenRiotIsOngoing) {
                x -= 250;
            }
            //谢天谢地BossHealthBarManager.Bars和BossHealthBarManager.BossHPUI是公开的
            foreach (BossHealthBarManager.BossHPUI ui in BossHealthBarManager.Bars) {
                ui.Draw(spriteBatch, x, y);
                y -= BossHealthBarManager.BossHPUI.VerticalOffsetPerBar;
            }
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
                if (modPlayer.TryGetInds_BaseHeldRanged(out BaseHeldRanged ranged)) {
                    if (ranged.OnHandheldDisplayBool) {
                        return false;
                    }
                }
                /*这些代码是不必要的
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
                */
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
                if (modPlayer.TryGetInds_BaseHeldRanged(out BaseHeldRanged ranged)) {
                    if (ranged.OnHandheldDisplayBool) {
                        result = false;
                    }
                }
                /*这些代码是不必要的
                //if (modPlayer.TryGetInds_BaseFeederGun(out BaseFeederGun gun)) {
                //    if (gun.OnHandheldDisplayBool) {
                //        result = false;
                //    }
                //}
                //if (modPlayer.TryGetInds_BaseGun(out BaseGun gun2)) {
                //    gun2.OnHandheldDisplayBool.Domp();
                //    if (gun2.OnHandheldDisplayBool) {
                //        result = false;
                //    }
                //}
                //if (modPlayer.TryGetInds_BaseBow(out BaseBow bow)) {
                //    if (bow.OnHandheldDisplayBool) {
                //        result = false;
                //    }
                //}
                */
            }

            return orig.Invoke(player, item) && result;
        }
    }
}
