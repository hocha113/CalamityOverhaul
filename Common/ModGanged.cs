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
using Terraria.ModLoader.Core;
using Terraria.Utilities;
using static CalamityOverhaul.CWRUtils;

namespace CalamityOverhaul.Common
{
    /// <summary>
    /// 跨Mod兼容性Hook管理器，负责对其他Mod的方法进行Hook以实现兼容性处理
    /// </summary>
    internal class ModGanged
    {
        #region 委托类型
        public delegate void On_PostAI_Dalegate(object obj, Projectile projectile);
        public delegate void On_ModPlayerDraw_Dalegate(object obj, ref PlayerDrawSet drawInfo);
        public delegate bool On_ShouldForceUseAnim_Dalegate(Player player, Item item);
        public delegate bool On_AttemptPowerAttackStart_Dalegate(object obj, Item item, Player player);
        public delegate bool On_OnSpawnEnchCanAffectProjectile_Dalegate(Projectile projectile, bool allowMinions);
        public delegate void On_BossHealthBarManager_Draw_Dalegate(object obj, SpriteBatch spriteBatch, IBigProgressBar currentBar, BigProgressBarInfo info);
        public delegate int On_GetReworkedReforge_Dalegate(Item item, UnifiedRandom rand, int currentPrefix);
        public delegate void On_ProvideStealthStatBonuses_Dalegate(ModPlayer calamityPlayer);
        #endregion

        #region 加载入口
        public static void Load() {
            HookWeaponOut();
            HookWeaponDisplay();
            HookWeaponDisplayLite();
            HookTerrariaOverhaul();
            HookFargowiltasSouls();
            HookCoolerItemVisualEffect();
            CWRRef.LoadComders();
        }
        #endregion

        #region 反射Hook辅助方法
        /// <summary>
        /// 在Mod的类型集合中查找指定类名并获取其方法，然后注册Hook
        /// </summary>
        private static bool TryHookMethod<TDelegate>(
            Mod mod, string typeName, string methodName,
            BindingFlags flags, TDelegate hookDelegate,
            string logContext = null) where TDelegate : Delegate {
            Type[] types = AssemblyManager.GetLoadableTypes(mod.Code);
            Type targetType = GetTargetTypeInStringKey(types, typeName);
            if (targetType == null) {
                LogFailedLoad(logContext ?? typeName, $"{typeName}");
                return false;
            }

            MethodBase method = targetType.GetMethod(methodName, flags);
            if (method == null) {
                LogFailedLoad(logContext ?? methodName, $"{typeName}.{methodName}");
                return false;
            }

            VaultHook.Add(method, hookDelegate);
            return true;
        }

        /// <summary>
        /// 在已获取的类型集合中查找指定类名的方法，然后注册Hook
        /// </summary>
        private static bool TryHookMethod<TDelegate>(
            Type[] types, string typeName, string methodName,
            BindingFlags flags, TDelegate hookDelegate,
            string logContext = null) where TDelegate : Delegate {
            Type targetType = GetTargetTypeInStringKey(types, typeName);
            if (targetType == null) {
                LogFailedLoad(logContext ?? typeName, $"{typeName}");
                return false;
            }

            MethodBase method = targetType.GetMethod(methodName, flags);
            if (method == null) {
                LogFailedLoad(logContext ?? methodName, $"{typeName}.{methodName}");
                return false;
            }

            VaultHook.Add(method, hookDelegate);
            return true;
        }
        #endregion

        #region WeaponOut
        private static void HookWeaponOut() {
            Mod mod = CWRMod.Instance.weaponOut;
            if (mod == null) {
                LogModNotLoaded("WeaponOut");
                return;
            }

            Type[] types = AssemblyManager.GetLoadableTypes(mod.Code);
            BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            TryHookMethod(types, "WeaponLayer1", "Draw", flags, On_DrawHeldHook);
            TryHookMethod(types, "WeaponLayer2", "Draw", flags, On_DrawHeldHook);
        }
        #endregion

        #region WeaponDisplay / WeaponDisplayLite
        private static void HookWeaponDisplay() {
            Mod mod = CWRMod.Instance.weaponDisplay;
            if (mod == null) {
                LogModNotLoaded("WeaponDisplay");
                return;
            }

            TryHookMethod(mod, "WeaponDisplayPlayer", "ModifyDrawInfo",
                BindingFlags.Instance | BindingFlags.Public, On_DrawHeldHook);
        }

        private static void HookWeaponDisplayLite() {
            Mod mod = CWRMod.Instance.weaponDisplayLite;
            if (mod == null) {
                LogModNotLoaded("WeaponDisplayLite");
                return;
            }

            TryHookMethod(mod, "WeaponDisplayPlayer", "ModifyDrawInfo",
                BindingFlags.Instance | BindingFlags.Public, On_DrawHeldHook);
        }
        #endregion

        #region TerrariaOverhaul
        private static void HookTerrariaOverhaul() {
            Mod mod = CWRMod.Instance.terrariaOverhaul;
            if (mod == null) {
                LogModNotLoaded("TerrariaOverhaul");
                return;
            }

            Type[] types = AssemblyManager.GetLoadableTypes(mod.Code);

            TryHookMethod(types, "PlayerHoldOutAnimation", "ShouldForceUseAnim",
                BindingFlags.Static | BindingFlags.NonPublic, On_ShouldForceUseAnim_Hook);

            TryHookMethod(types, "ItemPowerAttacks", "AttemptPowerAttackStart",
                BindingFlags.Instance | BindingFlags.Public, On_AttemptPowerAttackStart_Hook);
        }
        #endregion

        #region FargowiltasSouls
        private static void HookFargowiltasSouls() {
            Mod mod = CWRMod.Instance.fargowiltasSouls;
            if (mod == null) {
                LogModNotLoaded("FargowiltasSouls");
                return;
            }

            Type[] types = GetModTypes(mod);

            TryHookMethod(types, "FargoSoulsUtil", "OnSpawnEnchCanAffectProjectile",
                BindingFlags.Static | BindingFlags.Public, On_OnSpawnEnchCanAffectProjectile_Hook);

            TryHookMethod(types, "FargoSoulsGlobalProjectile", "PostAI",
                BindingFlags.Instance | BindingFlags.Public, On_FGS_PostAI_Hook);
        }
        #endregion

        #region CoolerItemVisualEffect
        private static void HookCoolerItemVisualEffect() {
            Mod mod = CWRMod.Instance.coolerItemVisualEffect;
            if (mod == null) {
                LogModNotLoaded("CoolerItemVisualEffect");
                return;
            }

            TryHookMethod(mod, "MeleeModifyPlayer", "ModifyDrawInfo",
                BindingFlags.Instance | BindingFlags.Public, On_DrawHeldHook);
        }
        #endregion

        #region Hook回调方法

        /// <summary>
        /// 统一的武器手持绘制Hook，阻止CWR管理的手持武器被其他Mod重复绘制
        /// </summary>
        private static void On_DrawHeldHook(On_ModPlayerDraw_Dalegate orig, object obj, ref PlayerDrawSet drawInfo) {
            if (!ShouldDrawHeld(orig, drawInfo)) {
                return;
            }
            orig.Invoke(obj, ref drawInfo);
        }

        /// <summary>
        /// FargowiltasSouls弹幕PostAI Hook，阻止隐藏的手持弹幕被FGS处理
        /// </summary>
        private static void On_FGS_PostAI_Hook(On_PostAI_Dalegate orig, object instance, Projectile projectile) {
            if (projectile.hide && projectile.ModProjectile is BaseHeldRanged ranged && !ranged.CanFire) {
                return;
            }
            if (projectile.hide && projectile.ModProjectile is BaseHeldGun heldGun && !heldGun.CanFire) {
                return;
            }
            orig.Invoke(instance, projectile);
        }

        /// <summary>
        /// TerrariaOverhaul蓄力攻击Hook，阻止空物品触发蓄力攻击导致报错
        /// </summary>
        private static bool On_AttemptPowerAttackStart_Hook(On_AttemptPowerAttackStart_Dalegate orig, object obj, Item item, Player player) {
            return !item.IsAir && item.type != ItemID.None && orig.Invoke(obj, item, player);
        }

        /// <summary>
        /// FargowiltasSouls附魔效果Hook，阻止标记为不受特殊效果影响的弹幕被附魔处理
        /// </summary>
        private static bool On_OnSpawnEnchCanAffectProjectile_Hook(On_OnSpawnEnchCanAffectProjectile_Dalegate orig, Projectile projectile, bool allowMinions) {
            return !projectile.CWR().NotSubjectToSpecialEffects && orig.Invoke(projectile, allowMinions);
        }

        /// <summary>
        /// TerrariaOverhaul强制使用动画Hook，阻止CWR管理的手持武器触发TrO的使用动画
        /// </summary>
        private static bool On_ShouldForceUseAnim_Hook(On_ShouldForceUseAnim_Dalegate orig, Player player, Item item) {
            if (item == null || item.type == ItemID.None) {
                return orig.Invoke(player, item);
            }

            Item heldItem = player.inventory[player.selectedItem];
            if (heldItem == null || heldItem.type == ItemID.None) {
                return false;
            }

            bool shouldApply = ShouldApplyHeldOverride(heldItem, player);
            return orig.Invoke(player, item) && shouldApply;
        }

        #endregion

        #region 内部辅助方法

        /// <summary>
        /// 判断是否允许其他Mod绘制当前手持武器
        /// </summary>
        private static bool ShouldDrawHeld(On_ModPlayerDraw_Dalegate orig, PlayerDrawSet drawInfo) {
            if (orig == null) {
                return false;
            }
            if (EqualityComparer<PlayerDrawSet>.Default.Equals(drawInfo, default)
                || drawInfo.DrawDataCache == null
                || drawInfo.DustCache == null) {
                return false;
            }

            Player drawPlayer = drawInfo.drawPlayer;
            Item heldItem = drawPlayer.inventory[drawPlayer.selectedItem];
            if (heldItem == null || heldItem.type == ItemID.None) {
                return false;
            }

            CWRItem ritem = heldItem.CWR();
            bool hasHeldProj = ritem.heldProjType > 0;

            // 当前正在手持显示武器弹幕时（无论新旧绑定模式），不让其他Mod绘制
            CWRPlayer modPlayer = drawPlayer.CWR();
            if (modPlayer.HeldWeaponInDisplay()) {
                return false;
            }

            bool isHeld = ritem.isHeldItem || hasHeldProj;
            return !isHeld;
        }

        /// <summary>
        /// 判断是否应该对手持武器应用覆盖逻辑（用于TerrariaOverhaul的ShouldForceUseAnim）
        /// </summary>
        private static bool ShouldApplyHeldOverride(Item heldItem, Player player) {
            CWRItem ritem = heldItem.CWR();
            bool isHeld = ritem.isHeldItem || ritem.heldProjType > 0;

            if (isHeld) {
                return false;
            }

            //新一代手持武器弹幕活跃期间同样屏蔽覆盖逻辑
            if (player.CWR().HeldWeaponInDisplay()) {
                return false;
            }

            return true;
        }

        #endregion
    }
}
