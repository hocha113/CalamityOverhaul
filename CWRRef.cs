using CalamityOverhaul.Common;
using CalamityOverhaul.Content.ADV;
using CalamityOverhaul.Content.LegendWeapon.MurasamaLegend.UI;
using CalamityOverhaul.Content.Players;
using InnoVault.GameSystem;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Terraria;
using Terraria.Audio;
using Terraria.Chat;
using Terraria.DataStructures;
using Terraria.GameContent.UI.BigProgressBar;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using static CalamityOverhaul.Common.ModGanged;

namespace CalamityOverhaul
{
    /// <summary>Calamity Mod 内部内容反射访问</summary>
    internal static class CWRRef
    {
        /// <summary>是否安装 CalamityMod，不校验版本；成员级空值防护，勿用版本匹配一刀切</summary>
        public static bool Has {
            get {
                _has ??= ModLoader.TryGetMod("CalamityMod", out _);
                return _has.Value;
            }
        }
        private static bool? _has = null;

        private static Type DownedBossSystemType;

        #region 反射缓存：Calamity 静态状态
        // CalamityWorld
        private static FieldInfo calWorld_death_Field;
        private static FieldInfo calWorld_revenge_Field;
        private static FieldInfo calWorld_DraedonMechToSummon_Field;
        // BossRushEvent
        private static MemberInfo bossRush_Active_M;
        // AcidRainEvent
        private static MemberInfo acidRain_Ongoing_M;
        private static FieldInfo acidRain_KillPoints_Field;
        private static MethodInfo acidRain_UpdateInvasion_Method;
        private static MemberInfo acidRain_OldDukeEncountered_M;
        // CalamityServerConfig
        private static object calamityServerConfigInstance;
        private static PropertyInfo calConfig_EarlyHardmodeRework_Prop; //EarlyHardmodeRework
        // CalamityGlobalNPC 静态方法
        private static MethodInfo calNPC_SetNewBossJustDowned_Method;
        // ArsenalTierGatedRecipe
        private static MethodInfo arsenalRecipe_ConstructCondition_Method;
        // BalancingConstants.UniversalStealthStrikeDamageFactor，Hook 实时读 double
        private static FieldInfo balancing_StealthFactor_Field;
        // DamageClasses
        private static DamageClass trueMeleeDamageClass;
        private static DamageClass trueMeleeNoSpeedDamageClass;
        #endregion

        #region 反射缓存：Calamity ModNPC 类型与字段
        // SupremeCalamitas
        private static Type supCalType;
        private static FieldInfo supCal_giveUpCounter_Field; //giveUpCounter
        // Draedon (ExoMechs)
        private static Type draedonType;
        private static MemberInfo draedon_DefeatTimer_M;   //DefeatTimer
        #endregion

        #region 反射缓存：Calamity 全局内容模板
        // ModContent.TryFind 模板，供 GetModPlayer / TryGetGlobal* 使用
        private static ModPlayer calPlayerTemplate;           //CalamityPlayer
        private static GlobalItem calGlobalItemTemplate;      //CalamityGlobalItem
        private static GlobalNPC calGlobalNPCTemplate;        //CalamityGlobalNPC
        private static GlobalProjectile calGlobalProjectileTemplate; //CalamityGlobalProjectile
        #endregion

        #region 反射缓存：CalamityPlayer / CalamityGlobalItem / CalamityGlobalNPC / CalamityGlobalProjectile 成员
        // CalamityPlayer
        private static MemberInfo calPlayer_bladeArmEnchant_M;
        private static MemberInfo calPlayer_adrenalineModeActive_M;
        private static MemberInfo calPlayer_infiniteFlight_M;
        private static MemberInfo calPlayer_ZoneSulphur_M;
        private static MemberInfo calPlayer_ZoneAbyss_M;
        private static MemberInfo calPlayer_profanedCrystalBuffs_M;
        private static MemberInfo calPlayer_DashID_M;
        private static MemberInfo calPlayer_AbleToSelectExoMech_M;
        private static MemberInfo calPlayer_rage_M;
        private static MemberInfo calPlayer_adrenaline_M;
        private static MemberInfo calPlayer_rageGainCooldown_M;
        private static MemberInfo calPlayer_rageCombatFrames_M;
        private static MemberInfo calPlayer_adrenalinePauseTimer_M;
        private static MemberInfo calPlayer_externalDefenseDamageImmunity_M;
        private static MemberInfo calPlayer_rogueStealth_M;
        private static MemberInfo calPlayer_rogueStealthMax_M;
        private static MemberInfo calPlayer_stealthUIAlpha_M;
        private static MemberInfo calPlayer_wearingRogueArmor_M;
        private static MemberInfo calPlayer_stealthDamage_M;
        private static MethodInfo calPlayer_StealthStrikeAvailable_Method;

        // CalamityGlobalItem
        private static MemberInfo calItem_ChargeRatio_M;
        private static MemberInfo calItem_Charge_M;
        private static MemberInfo calItem_MaxCharge_M;
        private static MemberInfo calItem_UsesCharge_M;
        private static MemberInfo calItem_AppliedEnchantment_M;

        // CalamityGlobalNPC
        private static MemberInfo calNPC_DR_M;

        // CalamityGlobalProjectile
        private static MemberInfo calProj_timesPierced_M;
        private static MemberInfo calProj_conditionalHomingRange_M;
        #endregion

        #region 反射缓存：灾厄附魔（炼铸）系统
        // EnchantmentManager (CalamitasEnchants)
        private static MethodInfo enchantManager_GetValidEnchantments_Method; //GetValidEnchantmentsForItem
        private static MemberInfo enchantManager_ClearEnchantment_M;
        private static MemberInfo enchantManager_ItemUpgradeRelationship_M;
        // Enchantment
        private static MemberInfo enchant_Name_M;
        private static MemberInfo enchant_Description_M;
        private static MemberInfo enchant_IconTexturePath_M;
        private static MemberInfo enchant_CreationEffect_M;
        #endregion

        #region 反射缓存：BossHealthBarManager
        // BossHealthBarManager / BossHPUI
        private static MemberInfo bossBar_Bars_M;               //Bars
        private static MethodInfo bossHPUI_Draw_Method;         //BossHPUI.Draw
        private static int bossHPUI_VerticalOffsetPerBar;       //VerticalOffsetPerBar
        #endregion

        #region 反射通用助手
        // BindingFlags 常量
        private const BindingFlags PublicStaticFlags = BindingFlags.Public | BindingFlags.Static;
        private const BindingFlags PublicInstanceFlags = BindingFlags.Public | BindingFlags.Instance;
        private const BindingFlags AnyInstanceFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        private static readonly HashSet<string> loggedReflectionFailures = new();

        // 反射失败去重日志
        private static void LogFailedReflection(string value1, string value2) {
            string logKey = $"{value1}|{value2}";
            if (loggedReflectionFailures.Add(logKey)) {
                CWRUtils.LogFailedLoad(value1, value2);
            }
        }

        private static void LogReflectionException(string context, Exception ex) {
            string logKey = $"Exception|{context}|{ex.GetType().FullName}|{ex.Message}";
            if (loggedReflectionFailures.Add(logKey)) {
                CWRMod.Instance.Logger.Warn($"CWRRef reflection failed at {context}: {ex.GetType().Name}: {ex.Message}");
            }
        }

        // Mod 类型查找
        private static Type GetModType(Mod mod, string fullName) {
            Type type = mod?.Code.GetType(fullName);
            if (type == null) {
                LogFailedReflection(fullName, fullName);
            }
            return type;
        }

        private static Type FindModType(Mod mod, string typeName) {
            Type type = mod == null ? null : CWRUtils.GetTargetTypeInStringKey(CWRUtils.GetModTypes(mod), typeName);
            if (type == null) {
                LogFailedReflection(typeName, $"{mod?.Name ?? "UnknownMod"}.{typeName}");
            }
            return type;
        }

        // 成员反射 GetField/Property/Method
        private static FieldInfo GetField(Type type, string name, BindingFlags flags) {
            if (type == null) {
                return null;
            }
            FieldInfo field = type.GetField(name, flags);
            if (field == null) {
                LogFailedReflection(name, $"{type.FullName}.{name}");
            }
            return field;
        }

        private static PropertyInfo GetProperty(Type type, string name, BindingFlags flags) {
            if (type == null) {
                return null;
            }
            PropertyInfo property = type.GetProperty(name, flags);
            if (property == null) {
                LogFailedReflection(name, $"{type.FullName}.{name}");
            }
            return property;
        }

        private static MethodInfo GetMethod(Type type, string name, BindingFlags flags) {
            if (type == null) {
                return null;
            }
            MethodInfo method = type.GetMethod(name, flags);
            if (method == null) {
                LogFailedReflection(name, $"{type.FullName}.{name}");
            }
            return method;
        }

        private static MemberInfo GetFieldOrProperty(Type type, string name, BindingFlags flags) {
            if (type == null) {
                return null;
            }
            MemberInfo member = type.GetField(name, flags);
            if (member == null) {
                member = type.GetProperty(name, flags);
            }
            if (member == null) {
                LogFailedReflection(name, $"{type.FullName}.{name}");
            }
            return member;
        }

        private static void LogMissingCalamityContent(string name) {
            LogFailedReflection(name, $"CalamityMod/{name}");
        }

        // MemberInfo 读写与 FindMember
        private static MemberInfo FindMember(Type type, string name) {
            return GetFieldOrProperty(type, name, AnyInstanceFlags);
        }

        private static object GetMember(MemberInfo m, object obj) {
            return m switch {
                FieldInfo f => f.GetValue(obj),
                PropertyInfo p => p.GetValue(obj),
                _ => null,
            };
        }

        private static void SetMember(MemberInfo m, object obj, object value) {
            switch (m) {
                case FieldInfo f: f.SetValue(obj, value); break;
                case PropertyInfo p: p.SetValue(obj, value); break;
            }
        }

        // ModContent.GetInstance<T>
        private static object GetModContentInstance(Type t) {
            if (t == null) {
                return null;
            }
            try {
                MethodInfo open = null;
                foreach (MethodInfo mi in typeof(ModContent).GetMethods(BindingFlags.Public | BindingFlags.Static)) {
                    if (mi.Name == "GetInstance" && mi.IsGenericMethodDefinition && mi.GetParameters().Length == 0) {
                        open = mi;
                        break;
                    }
                }
                if (open == null) {
                    LogFailedReflection("ModContent.GetInstance", "Terraria.ModLoader.ModContent.GetInstance<T>()");
                    return null;
                }
                return open.MakeGenericMethod(t).Invoke(null, null);
            } catch (Exception ex) {
                LogReflectionException($"GetInstance<{t.FullName}>", ex);
                return null;
            }
        }

        // Calamity Global 模板实例访问
        private static ModPlayer GetCalPlayer(Player player) {
            if (calPlayerTemplate == null || player == null) {
                return null;
            }
            return player.GetModPlayer(calPlayerTemplate);
        }

        private static GlobalItem GetCalItem(Item item) {
            if (calGlobalItemTemplate == null || item == null || item.IsAir) {
                return null;
            }
            return item.TryGetGlobalItem(calGlobalItemTemplate, out GlobalItem g) ? g : null;
        }

        private static GlobalNPC GetCalNPC(NPC npc) {
            if (calGlobalNPCTemplate == null || npc == null) {
                return null;
            }
            return npc.TryGetGlobalNPC(calGlobalNPCTemplate, out GlobalNPC g) ? g : null;
        }

        private static GlobalProjectile GetCalProj(Projectile projectile) {
            if (calGlobalProjectileTemplate == null || projectile == null) {
                return null;
            }
            return projectile.TryGetGlobalProjectile(calGlobalProjectileTemplate, out GlobalProjectile g) ? g : null;
        }
        #endregion

        internal static void Load() {
            if (!ModLoader.TryGetMod("CalamityMod", out Mod mod)) {
                return;
            }

            LoadBossFlags(mod);
            LoadCalamityStaticState(mod);
            LoadCalamityModNPCs(mod);
            LoadCalamityGlobalTemplates();
            LoadEnchantmentSystem(mod);
        }

        private static void LoadBossFlags(Mod mod) {
            DownedBossSystemType = GetModType(mod, "CalamityMod.DownedBossSystem");
            if (DownedBossSystemType == null) {
                return;
            }
            downedDesertScourgeProp = GetProperty(DownedBossSystemType, "downedDesertScourge", PublicStaticFlags);
            downedCLAMProp = GetProperty(DownedBossSystemType, "downedCLAM", PublicStaticFlags);
            downedCrabulonProp = GetProperty(DownedBossSystemType, "downedCrabulon", PublicStaticFlags);
            downedHiveMindProp = GetProperty(DownedBossSystemType, "downedHiveMind", PublicStaticFlags);
            downedPerforatorProp = GetProperty(DownedBossSystemType, "downedPerforator", PublicStaticFlags);
            downedSlimeGodProp = GetProperty(DownedBossSystemType, "downedSlimeGod", PublicStaticFlags);
            downedCryogenProp = GetProperty(DownedBossSystemType, "downedCryogen", PublicStaticFlags);
            downedBrimstoneElementalProp = GetProperty(DownedBossSystemType, "downedBrimstoneElemental", PublicStaticFlags);
            downedAquaticScourgeProp = GetProperty(DownedBossSystemType, "downedAquaticScourge", PublicStaticFlags);
            downedCragmawMireProp = GetProperty(DownedBossSystemType, "downedCragmawMire", PublicStaticFlags);
            downedCalamitasCloneProp = GetProperty(DownedBossSystemType, "downedCalamitasClone", PublicStaticFlags);
            downedGSSProp = GetProperty(DownedBossSystemType, "downedGSS", PublicStaticFlags);
            downedLeviathanProp = GetProperty(DownedBossSystemType, "downedLeviathan", PublicStaticFlags);
            downedAstrumAureusProp = GetProperty(DownedBossSystemType, "downedAstrumAureus", PublicStaticFlags);
            downedPlaguebringerProp = GetProperty(DownedBossSystemType, "downedPlaguebringer", PublicStaticFlags);
            downedRavagerProp = GetProperty(DownedBossSystemType, "downedRavager", PublicStaticFlags);
            downedAstrumDeusProp = GetProperty(DownedBossSystemType, "downedAstrumDeus", PublicStaticFlags);
            downedGuardiansProp = GetProperty(DownedBossSystemType, "downedGuardians", PublicStaticFlags);
            downedDragonfollyProp = GetProperty(DownedBossSystemType, "downedDragonfolly", PublicStaticFlags);
            downedProvidenceProp = GetProperty(DownedBossSystemType, "downedProvidence", PublicStaticFlags);
            downedCeaselessVoidProp = GetProperty(DownedBossSystemType, "downedCeaselessVoid", PublicStaticFlags);
            downedStormWeaverProp = GetProperty(DownedBossSystemType, "downedStormWeaver", PublicStaticFlags);
            downedSignusProp = GetProperty(DownedBossSystemType, "downedSignus", PublicStaticFlags);
            downedPolterghastProp = GetProperty(DownedBossSystemType, "downedPolterghast", PublicStaticFlags);
            downedMaulerProp = GetProperty(DownedBossSystemType, "downedMauler", PublicStaticFlags);
            downedNuclearTerrorProp = GetProperty(DownedBossSystemType, "downedNuclearTerror", PublicStaticFlags);
            downedBoomerDukeProp = GetProperty(DownedBossSystemType, "downedBoomerDuke", PublicStaticFlags);
            downedDoGProp = GetProperty(DownedBossSystemType, "downedDoG", PublicStaticFlags);
            downedYharonProp = GetProperty(DownedBossSystemType, "downedYharon", PublicStaticFlags);
            downedExoMechsProp = GetProperty(DownedBossSystemType, "downedExoMechs", PublicStaticFlags);
            downedCalamitasProp = GetProperty(DownedBossSystemType, "downedCalamitas", PublicStaticFlags);
            downedPrimordialWyrmProp = GetProperty(DownedBossSystemType, "downedPrimordialWyrm", PublicStaticFlags);
            downedBossRushProp = GetProperty(DownedBossSystemType, "downedBossRush", PublicStaticFlags);
            downedThanatosProp = GetProperty(DownedBossSystemType, "downedThanatos", PublicStaticFlags);
        }

        private static void LoadCalamityStaticState(Mod mod) {
            Type calWorld = GetModType(mod, "CalamityMod.World.CalamityWorld");
            if (calWorld != null) {
                calWorld_death_Field = GetField(calWorld, "death", PublicStaticFlags);
                calWorld_revenge_Field = GetField(calWorld, "revenge", PublicStaticFlags);
                calWorld_DraedonMechToSummon_Field = GetField(calWorld, "DraedonMechToSummon", PublicStaticFlags);
            }

            Type arsenalRecipe = GetModType(mod, "CalamityMod.CustomRecipes.ArsenalTierGatedRecipe");
            arsenalRecipe_ConstructCondition_Method = GetMethod(arsenalRecipe, "ConstructRecipeCondition", PublicStaticFlags);

            Type balancingConstants = GetModType(mod, "CalamityMod.Balancing.BalancingConstants");
            balancing_StealthFactor_Field = GetField(balancingConstants, "UniversalStealthStrikeDamageFactor", PublicStaticFlags);

            Type bossRush = GetModType(mod, "CalamityMod.Events.BossRushEvent");
            bossRush_Active_M = GetFieldOrProperty(bossRush, "BossRushActive", PublicStaticFlags);

            Type acidRain = GetModType(mod, "CalamityMod.Events.AcidRainEvent");
            if (acidRain != null) {
                acidRain_Ongoing_M = GetFieldOrProperty(acidRain, "AcidRainEventIsOngoing", PublicStaticFlags);
                acidRain_KillPoints_Field = GetField(acidRain, "AccumulatedKillPoints", PublicStaticFlags);
                acidRain_UpdateInvasion_Method = GetMethod(acidRain, "UpdateInvasion", PublicStaticFlags);
                acidRain_OldDukeEncountered_M = GetFieldOrProperty(acidRain, "OldDukeHasBeenEncountered", PublicStaticFlags);
            }

            Type calConfig = GetModType(mod, "CalamityMod.CalamityServerConfig");
            if (calConfig != null) {
                calamityServerConfigInstance = GetModContentInstance(calConfig);
                calConfig_EarlyHardmodeRework_Prop = GetProperty(calConfig, "EarlyHardmodeProgressionRework", PublicInstanceFlags);
            }

            Type calGlobalNPCType = GetModType(mod, "CalamityMod.NPCs.CalamityGlobalNPC");
            calNPC_SetNewBossJustDowned_Method = GetMethod(calGlobalNPCType, "SetNewBossJustDowned", PublicStaticFlags);

            if (!ModContent.TryFind("CalamityMod", "TrueMeleeDamageClass", out trueMeleeDamageClass)) {
                LogMissingCalamityContent("TrueMeleeDamageClass");
            }
            if (!ModContent.TryFind("CalamityMod", "TrueMeleeNoSpeedDamageClass", out trueMeleeNoSpeedDamageClass)) {
                LogMissingCalamityContent("TrueMeleeNoSpeedDamageClass");
            }
        }

        private static void LoadCalamityModNPCs(Mod mod) {
            supCalType = GetModType(mod, "CalamityMod.NPCs.SupremeCalamitas.SupremeCalamitas");
            supCal_giveUpCounter_Field = GetField(supCalType, "giveUpCounter", AnyInstanceFlags);

            draedonType = GetModType(mod, "CalamityMod.NPCs.ExoMechs.Draedon");
            draedon_DefeatTimer_M = FindMember(draedonType, "DefeatTimer");
        }

        private static void LoadCalamityGlobalTemplates() {
            if (!ModContent.TryFind("CalamityMod", "CalamityPlayer", out calPlayerTemplate)) {
                LogMissingCalamityContent("CalamityPlayer");
            }
            if (!ModContent.TryFind("CalamityMod", "CalamityGlobalItem", out calGlobalItemTemplate)) {
                LogMissingCalamityContent("CalamityGlobalItem");
            }
            if (!ModContent.TryFind("CalamityMod", "CalamityGlobalNPC", out calGlobalNPCTemplate)) {
                LogMissingCalamityContent("CalamityGlobalNPC");
            }
            if (!ModContent.TryFind("CalamityMod", "CalamityGlobalProjectile", out calGlobalProjectileTemplate)) {
                LogMissingCalamityContent("CalamityGlobalProjectile");
            }

            Type calPlayerType = calPlayerTemplate?.GetType();
            Type calItemType = calGlobalItemTemplate?.GetType();
            Type calNPCType = calGlobalNPCTemplate?.GetType();
            Type calProjType = calGlobalProjectileTemplate?.GetType();

            calPlayer_bladeArmEnchant_M = FindMember(calPlayerType, "bladeArmEnchant");
            calPlayer_adrenalineModeActive_M = FindMember(calPlayerType, "adrenalineModeActive");
            calPlayer_infiniteFlight_M = FindMember(calPlayerType, "infiniteFlight");
            calPlayer_ZoneSulphur_M = FindMember(calPlayerType, "ZoneSulphur");
            calPlayer_ZoneAbyss_M = FindMember(calPlayerType, "ZoneAbyss");
            calPlayer_profanedCrystalBuffs_M = FindMember(calPlayerType, "profanedCrystalBuffs");
            calPlayer_DashID_M = FindMember(calPlayerType, "DashID");
            calPlayer_AbleToSelectExoMech_M = FindMember(calPlayerType, "AbleToSelectExoMech");
            calPlayer_rage_M = FindMember(calPlayerType, "rage");
            calPlayer_adrenaline_M = FindMember(calPlayerType, "adrenaline");
            calPlayer_rageGainCooldown_M = FindMember(calPlayerType, "rageGainCooldown");
            calPlayer_rageCombatFrames_M = FindMember(calPlayerType, "rageCombatFrames");
            calPlayer_adrenalinePauseTimer_M = FindMember(calPlayerType, "adrenalinePauseTimer");
            calPlayer_externalDefenseDamageImmunity_M = FindMember(calPlayerType, "externalDefenseDamageImmunity");
            calPlayer_rogueStealth_M = FindMember(calPlayerType, "rogueStealth");
            calPlayer_rogueStealthMax_M = FindMember(calPlayerType, "rogueStealthMax");
            calPlayer_stealthUIAlpha_M = FindMember(calPlayerType, "stealthUIAlpha");
            calPlayer_wearingRogueArmor_M = FindMember(calPlayerType, "wearingRogueArmor");
            calPlayer_stealthDamage_M = FindMember(calPlayerType, "stealthDamage");
            calPlayer_StealthStrikeAvailable_Method = GetMethod(calPlayerType, "StealthStrikeAvailable", PublicInstanceFlags);

            calItem_ChargeRatio_M = FindMember(calItemType, "ChargeRatio");
            calItem_Charge_M = FindMember(calItemType, "Charge");
            calItem_MaxCharge_M = FindMember(calItemType, "MaxCharge");
            calItem_UsesCharge_M = FindMember(calItemType, "UsesCharge");
            calItem_AppliedEnchantment_M = FindMember(calItemType, "AppliedEnchantment");

            calNPC_DR_M = FindMember(calNPCType, "DR");

            calProj_timesPierced_M = FindMember(calProjType, "timesPierced");
            calProj_conditionalHomingRange_M = FindMember(calProjType, "conditionalHomingRange");
        }

        private static void LoadEnchantmentSystem(Mod mod) {
            Type enchantManagerType = GetModType(mod, "CalamityMod.UI.CalamitasEnchants.EnchantmentManager");
            if (enchantManagerType != null) {
                enchantManager_GetValidEnchantments_Method = GetMethod(enchantManagerType, "GetValidEnchantmentsForItem", PublicStaticFlags);
                enchantManager_ClearEnchantment_M = GetFieldOrProperty(enchantManagerType, "ClearEnchantment", PublicStaticFlags);
                enchantManager_ItemUpgradeRelationship_M = GetFieldOrProperty(enchantManagerType, "ItemUpgradeRelationship", PublicStaticFlags);
            }

            Type enchantType = GetModType(mod, "CalamityMod.UI.CalamitasEnchants.Enchantment");
            if (enchantType != null) {
                enchant_Name_M = GetFieldOrProperty(enchantType, "Name", AnyInstanceFlags);
                enchant_Description_M = GetFieldOrProperty(enchantType, "Description", AnyInstanceFlags);
                enchant_IconTexturePath_M = GetFieldOrProperty(enchantType, "IconTexturePath", AnyInstanceFlags);
                enchant_CreationEffect_M = GetFieldOrProperty(enchantType, "CreationEffect", AnyInstanceFlags);
            }
        }

        internal static void UnLoad() {
            _has = null;
            loggedReflectionFailures.Clear();
            DownedBossSystemType = null;
            downedDesertScourgeProp = null;
            downedCLAMProp = null;
            downedCrabulonProp = null;
            downedHiveMindProp = null;
            downedPerforatorProp = null;
            downedSlimeGodProp = null;
            downedCryogenProp = null;
            downedBrimstoneElementalProp = null;
            downedAquaticScourgeProp = null;
            downedCragmawMireProp = null;
            downedCalamitasCloneProp = null;
            downedGSSProp = null;
            downedLeviathanProp = null;
            downedAstrumAureusProp = null;
            downedPlaguebringerProp = null;
            downedRavagerProp = null;
            downedAstrumDeusProp = null;
            downedGuardiansProp = null;
            downedDragonfollyProp = null;
            downedProvidenceProp = null;
            downedCeaselessVoidProp = null;
            downedStormWeaverProp = null;
            downedSignusProp = null;
            downedPolterghastProp = null;
            downedMaulerProp = null;
            downedNuclearTerrorProp = null;
            downedBoomerDukeProp = null;
            downedDoGProp = null;
            downedYharonProp = null;
            downedExoMechsProp = null;
            downedCalamitasProp = null;
            downedPrimordialWyrmProp = null;
            downedBossRushProp = null;
            downedThanatosProp = null;

            calWorld_death_Field = null;
            calWorld_revenge_Field = null;
            calWorld_DraedonMechToSummon_Field = null;
            arsenalRecipe_ConstructCondition_Method = null;
            balancing_StealthFactor_Field = null;
            bossRush_Active_M = null;
            acidRain_Ongoing_M = null;
            acidRain_KillPoints_Field = null;
            acidRain_UpdateInvasion_Method = null;
            acidRain_OldDukeEncountered_M = null;
            calamityServerConfigInstance = null;
            calConfig_EarlyHardmodeRework_Prop = null;
            calNPC_SetNewBossJustDowned_Method = null;
            trueMeleeDamageClass = null;
            trueMeleeNoSpeedDamageClass = null;

            supCalType = null;
            supCal_giveUpCounter_Field = null;
            draedonType = null;
            draedon_DefeatTimer_M = null;

            calPlayerTemplate = null;
            calGlobalItemTemplate = null;
            calGlobalNPCTemplate = null;
            calGlobalProjectileTemplate = null;

            calPlayer_bladeArmEnchant_M = null;
            calPlayer_adrenalineModeActive_M = null;
            calPlayer_infiniteFlight_M = null;
            calPlayer_ZoneSulphur_M = null;
            calPlayer_ZoneAbyss_M = null;
            calPlayer_profanedCrystalBuffs_M = null;
            calPlayer_DashID_M = null;
            calPlayer_AbleToSelectExoMech_M = null;
            calPlayer_rage_M = null;
            calPlayer_adrenaline_M = null;
            calPlayer_rageGainCooldown_M = null;
            calPlayer_rageCombatFrames_M = null;
            calPlayer_adrenalinePauseTimer_M = null;
            calPlayer_externalDefenseDamageImmunity_M = null;
            calPlayer_rogueStealth_M = null;
            calPlayer_rogueStealthMax_M = null;
            calPlayer_stealthUIAlpha_M = null;
            calPlayer_wearingRogueArmor_M = null;
            calPlayer_stealthDamage_M = null;
            calPlayer_StealthStrikeAvailable_Method = null;

            calItem_ChargeRatio_M = null;
            calItem_Charge_M = null;
            calItem_MaxCharge_M = null;
            calItem_UsesCharge_M = null;
            calItem_AppliedEnchantment_M = null;

            calNPC_DR_M = null;

            calProj_timesPierced_M = null;
            calProj_conditionalHomingRange_M = null;

            enchantManager_GetValidEnchantments_Method = null;
            enchantManager_ClearEnchantment_M = null;
            enchantManager_ItemUpgradeRelationship_M = null;
            enchant_Name_M = null;
            enchant_Description_M = null;
            enchant_IconTexturePath_M = null;
            enchant_CreationEffect_M = null;

            BossHealthBarManager_Draw_Method = null;
            calamityUtils_GetReworkedReforge_Method = null;
            bossBar_Bars_M = null;
            bossHPUI_Draw_Method = null;
        }

        private static bool GetDownedProp(PropertyInfo prop) => prop != null && (bool)prop.GetValue(null);
        private static void SetDownedProp(PropertyInfo prop, bool value) => prop?.SetValue(null, value);
        private static PropertyInfo downedDesertScourgeProp;
        private static PropertyInfo downedCLAMProp;
        private static PropertyInfo downedCrabulonProp;
        private static PropertyInfo downedHiveMindProp;
        private static PropertyInfo downedPerforatorProp;
        private static PropertyInfo downedSlimeGodProp;
        private static PropertyInfo downedCryogenProp;
        private static PropertyInfo downedBrimstoneElementalProp;
        private static PropertyInfo downedAquaticScourgeProp;
        private static PropertyInfo downedCragmawMireProp;
        private static PropertyInfo downedCalamitasCloneProp;
        private static PropertyInfo downedGSSProp;
        private static PropertyInfo downedLeviathanProp;
        private static PropertyInfo downedAstrumAureusProp;
        private static PropertyInfo downedPlaguebringerProp;
        private static PropertyInfo downedRavagerProp;
        private static PropertyInfo downedAstrumDeusProp;
        private static PropertyInfo downedGuardiansProp;
        private static PropertyInfo downedDragonfollyProp;
        private static PropertyInfo downedProvidenceProp;
        private static PropertyInfo downedCeaselessVoidProp;
        private static PropertyInfo downedStormWeaverProp;
        private static PropertyInfo downedSignusProp;
        private static PropertyInfo downedPolterghastProp;
        private static PropertyInfo downedMaulerProp;
        private static PropertyInfo downedNuclearTerrorProp;
        private static PropertyInfo downedBoomerDukeProp;
        private static PropertyInfo downedDoGProp;
        private static PropertyInfo downedYharonProp;
        private static PropertyInfo downedExoMechsProp;
        private static PropertyInfo downedCalamitasProp;
        private static PropertyInfo downedPrimordialWyrmProp;
        private static PropertyInfo downedBossRushProp;
        private static PropertyInfo downedThanatosProp;

        /// <summary>
        /// 荒漠灾虫
        /// </summary>
        public static bool GetDownedDesertScourge() => GetDownedProp(downedDesertScourgeProp);

        /// <summary>
        /// 巨像蛤
        /// </summary>
        public static bool GetDownedCLAM() => GetDownedProp(downedCLAMProp);

        /// <summary>
        /// 蘑菇蟹
        /// </summary>
        public static bool GetDownedCrabulon() => GetDownedProp(downedCrabulonProp);

        /// <summary>
        /// 腐巢意志
        /// </summary>
        public static bool GetDownedHiveMind() => GetDownedProp(downedHiveMindProp);

        /// <summary>
        /// 血肉宿主
        /// </summary>
        public static bool GetDownedPerforator() => GetDownedProp(downedPerforatorProp);

        /// <summary>
        /// 史莱姆之神
        /// </summary>
        public static bool GetDownedSlimeGod() => GetDownedProp(downedSlimeGodProp);

        /// <summary>
        /// 极地冰灵
        /// </summary>
        public static bool GetDownedCryogen() => GetDownedProp(downedCryogenProp);

        /// <summary>
        /// 硫磺火元素
        /// </summary>
        public static bool GetDownedBrimstoneElemental() => GetDownedProp(downedBrimstoneElementalProp);

        /// <summary>
        /// 渊海灾虫
        /// </summary>
        public static bool GetDownedAquaticScourge() => GetDownedProp(downedAquaticScourgeProp);

        /// <summary>
        /// 辐射之主
        /// </summary>
        public static bool GetDownedCragmawMire() => GetDownedProp(downedCragmawMireProp);

        /// <summary>
        /// 灾厄之影
        /// </summary>
        public static bool GetDownedCalamitasClone() => GetDownedProp(downedCalamitasCloneProp);

        /// <summary>
        /// 沙漠巨鲨
        /// </summary>
        public static bool GetDownedGSS() => GetDownedProp(downedGSSProp);

        /// <summary>
        /// 利维坦
        /// </summary>
        public static bool GetDownedLeviathan() => GetDownedProp(downedLeviathanProp);

        /// <summary>
        /// 白金星舰
        /// </summary>
        public static bool GetDownedAstrumAureus() => GetDownedProp(downedAstrumAureusProp);

        /// <summary>
        /// 瘟疫使者
        /// </summary>
        public static bool GetDownedPlaguebringer() => GetDownedProp(downedPlaguebringerProp);

        /// <summary>
        /// 毁灭魔像
        /// </summary>
        public static bool GetDownedRavager() => GetDownedProp(downedRavagerProp);

        /// <summary>
        /// 星神游龙
        /// </summary>
        public static bool GetDownedAstrumDeus() => GetDownedProp(downedAstrumDeusProp);

        /// <summary>
        /// 亵渎使徒
        /// </summary>
        public static bool GetDownedGuardians() => GetDownedProp(downedGuardiansProp);

        /// <summary>
        /// 痴愚金龙
        /// </summary>
        public static bool GetDownedDragonfolly() => GetDownedProp(downedDragonfollyProp);

        /// <summary>
        /// 亵渎天神
        /// </summary>
        public static bool GetDownedProvidence() => GetDownedProp(downedProvidenceProp);

        /// <summary>
        /// 无尽虚空
        /// </summary>
        public static bool GetDownedCeaselessVoid() => GetDownedProp(downedCeaselessVoidProp);

        /// <summary>
        /// 风暴编织者
        /// </summary>
        public static bool GetDownedStormWeaver() => GetDownedProp(downedStormWeaverProp);

        /// <summary>
        /// 西格纳斯
        /// </summary>
        public static bool GetDownedSignus() => GetDownedProp(downedSignusProp);

        /// <summary>
        /// 噬魂幽花
        /// </summary>
        public static bool GetDownedPolterghast() => GetDownedProp(downedPolterghastProp);

        /// <summary>
        /// 酸雨二
        /// </summary>
        public static bool GetDownedMauler() => GetDownedProp(downedMaulerProp);

        /// <summary>
        /// 生化恐惧
        /// </summary>
        public static bool GetDownedNuclearTerror() => GetDownedProp(downedNuclearTerrorProp);

        /// <summary>
        /// 老核弹
        /// </summary>
        public static bool GetDownedBoomerDuke() => GetDownedProp(downedBoomerDukeProp);

        /// <summary>
        /// 神明吞噬者
        /// </summary>
        public static bool GetDownedDoG() => GetDownedProp(downedDoGProp);

        /// <summary>
        /// 丛林龙
        /// </summary>
        public static bool GetDownedYharon() => GetDownedProp(downedYharonProp);

        /// <summary>
        /// 星流巨械
        /// </summary>
        public static bool GetDownedExoMechs() => GetDownedProp(downedExoMechsProp);

        /// <summary>
        /// 至尊灾厄
        /// </summary>
        public static bool GetDownedCalamitas() => GetDownedProp(downedCalamitasProp);

        /// <summary>
        /// 始源妖龙
        /// </summary>
        public static bool GetDownedPrimordialWyrm() => GetDownedProp(downedPrimordialWyrmProp);

        /// <summary>
        /// 终焉之战
        /// </summary>
        public static bool GetDownedBossRush() => GetDownedProp(downedBossRushProp);

        public static void SetDownedPrimordialWyrm(bool value) => SetDownedProp(downedPrimordialWyrmProp, value);

        public static bool GetDeathMode() {
            return calWorld_death_Field != null && (bool)calWorld_death_Field.GetValue(null);
        }

        public static bool GetRevengeMode() {
            return calWorld_revenge_Field != null && (bool)calWorld_revenge_Field.GetValue(null);
        }

        public static bool GetBossRushActive() {
            return bossRush_Active_M != null && (bool)GetMember(bossRush_Active_M, null);
        }

        public static void SetBossRushActive(bool value) {
            SetMember(bossRush_Active_M, null, value);
        }

        public static bool GetAcidRainEventIsOngoing() {
            return acidRain_Ongoing_M != null && (bool)GetMember(acidRain_Ongoing_M, null);
        }

        public static DamageClass GetTrueMeleeDamageClass() => trueMeleeDamageClass ?? DamageClass.Default;

        public static DamageClass GetTrueMeleeNoSpeedDamageClass() => trueMeleeNoSpeedDamageClass ?? DamageClass.Default;

        public static float ChargeRatio(Item item) {
            GlobalItem cgi = GetCalItem(item);
            if (cgi == null || calItem_ChargeRatio_M == null) {
                return 0f;
            }
            return (float)GetMember(calItem_ChargeRatio_M, cgi);
        }

        public static bool GetPlayerBladeArmEnchant(this Player player) {
            ModPlayer cp = GetCalPlayer(player);
            if (cp == null || calPlayer_bladeArmEnchant_M == null) {
                return false;
            }
            return (bool)GetMember(calPlayer_bladeArmEnchant_M, cp);
        }

        public static bool GetPlayerAdrenalineMode(this Player player) {
            ModPlayer cp = GetCalPlayer(player);
            if (cp == null || calPlayer_adrenalineModeActive_M == null) {
                return false;
            }
            return (bool)GetMember(calPlayer_adrenalineModeActive_M, cp);
        }

        /// <summary>
        /// 抓取玩家的怒气与肾上腺素相关字段快照，仅在Calamity安装时生效
        /// </summary>
        public static void SnapshotRippers(Player player, ref float rage, ref float adrenaline
            , ref int rageGainCooldown, ref int rageCombatFrames, ref int adrenalinePauseTimer) {
            ModPlayer cp = GetCalPlayer(player);
            if (cp == null) {
                return;
            }
            if (calPlayer_rage_M != null) rage = (float)GetMember(calPlayer_rage_M, cp);
            if (calPlayer_adrenaline_M != null) adrenaline = (float)GetMember(calPlayer_adrenaline_M, cp);
            if (calPlayer_rageGainCooldown_M != null) rageGainCooldown = (int)GetMember(calPlayer_rageGainCooldown_M, cp);
            if (calPlayer_rageCombatFrames_M != null) rageCombatFrames = (int)GetMember(calPlayer_rageCombatFrames_M, cp);
            if (calPlayer_adrenalinePauseTimer_M != null) adrenalinePauseTimer = (int)GetMember(calPlayer_adrenalinePauseTimer_M, cp);
        }

        /// <summary>
        /// 将怒气与肾上腺素相关字段还原为快照值，仅在Calamity安装时生效
        /// </summary>
        public static void RestoreRippers(Player player, float rage, float adrenaline
            , int rageGainCooldown, int rageCombatFrames, int adrenalinePauseTimer) {
            ModPlayer cp = GetCalPlayer(player);
            if (cp == null) {
                return;
            }
            if (calPlayer_rage_M != null) SetMember(calPlayer_rage_M, cp, rage);
            if (calPlayer_adrenaline_M != null) SetMember(calPlayer_adrenaline_M, cp, adrenaline);
            if (calPlayer_rageGainCooldown_M != null) SetMember(calPlayer_rageGainCooldown_M, cp, rageGainCooldown);
            if (calPlayer_rageCombatFrames_M != null) SetMember(calPlayer_rageCombatFrames_M, cp, rageCombatFrames);
            if (calPlayer_adrenalinePauseTimer_M != null) SetMember(calPlayer_adrenalinePauseTimer_M, cp, adrenalinePauseTimer);
        }

        public static void UpdateRogueStealth(Player player) {
            ModPlayer calPlayer = GetCalPlayer(player);
            if (calPlayer == null) {
                return;
            }
            bool noAvailable = false;
            if (CWRMod.Instance.narakuEye != null) {
                noAvailable = (bool)CWRMod.Instance.narakuEye.Call(player);
                if (calPlayer_StealthStrikeAvailable_Method != null
                    && (bool)calPlayer_StealthStrikeAvailable_Method.Invoke(calPlayer, null)) {
                    noAvailable = false;
                }
            }
            if (!noAvailable) {
                if (calPlayer_rogueStealth_M != null) {
                    SetMember(calPlayer_rogueStealth_M, calPlayer, 0f);
                }
                if (calPlayer_stealthUIAlpha_M != null) {
                    float alpha = (float)GetMember(calPlayer_stealthUIAlpha_M, calPlayer);
                    if (alpha > 0.02f) {
                        SetMember(calPlayer_stealthUIAlpha_M, calPlayer, alpha - 0.02f);
                    }
                }
            }
        }

        public static void SummonSupCal(Vector2 spawnPos) {
            SoundEngine.PlaySound("CalamityMod/Sounds/Custom/SCalAltarSummon".GetSound(), spawnPos);
            Projectile.NewProjectile(new EntitySource_WorldEvent(), spawnPos, Vector2.Zero
                , CWRID.Proj_SCalRitualDrama, 0, 0f, Main.myPlayer, 0, 0);
        }

        public static void SummonExo(int exoType, Player player) {
            if (!Has) {
                return;
            }
            //写入 CalamityWorld.DraedonMechToSummon（枚举字段，使用 Enum.ToObject 转换）
            if (calWorld_DraedonMechToSummon_Field != null) {
                try {
                    calWorld_DraedonMechToSummon_Field.SetValue(null
                        , Enum.ToObject(calWorld_DraedonMechToSummon_Field.FieldType, exoType));
                } catch (Exception ex) {
                    LogReflectionException("DraedonMechToSummon", ex);
                    return;
                }
            }
            if (VaultUtils.isClient) {//客户端发送网络数据到服务器
                //通过反射直接调用 ExoMechSelectionPacket.Send()
                var calMod = CWRMod.Instance.calamity;
                var packetType = GetModType(calMod, "CalamityMod.Packets.ExoMechSelectionPacket");
                var sendMethod = GetMethod(packetType, "Send", PublicStaticFlags);
                if (sendMethod == null) {
                    return;
                }
                sendMethod.Invoke(null, [/* toClient */ -1, /* ignoreClient */ -1]);
                return;
            }
            //枚举值与灾厄 ExoMech 一致：1=Destroyer(塔纳托斯) 2=Prime(阿瑞斯) 3=Twins(双子)
            switch (exoType) {
                case 1:
                    Vector2 thanatosSpawnPosition = player.Center + Vector2.UnitY * 2100f;
                    NPC thanatos = SpawnBoss(thanatosSpawnPosition, CWRID.NPC_ThanatosHead);
                    if (thanatos != null)
                        thanatos.velocity = thanatos.Center.To(player.Center).UnitVector() * 40f;
                    break;

                case 2:
                    Vector2 aresSpawnPosition = player.Center - Vector2.UnitY * 1400f;
                    SpawnBoss(aresSpawnPosition, CWRID.NPC_AresBody);
                    break;

                case 3:
                    Vector2 artemisSpawnPosition = player.Center + new Vector2(-1100f, -1600f);
                    Vector2 apolloSpawnPosition = player.Center + new Vector2(1100f, -1600f);
                    SpawnBoss(artemisSpawnPosition, CWRID.NPC_Artemis);
                    SpawnBoss(apolloSpawnPosition, CWRID.NPC_Apollo);
                    break;
            }
        }

        /// <summary>
        /// 在指定世界坐标生成Boss并同步，行为等价于灾厄的SpawnBossBetter，独立实现以避免类型引用
        /// </summary>
        private static NPC SpawnBoss(Vector2 spawnPos, int npcType) {
            if (npcType <= NPCID.None || VaultUtils.isClient) {
                return null;
            }
            int closestPlayer = Player.FindClosest(spawnPos, 1, 1);
            int index = NPC.NewNPC(NPC.GetBossSpawnSource(closestPlayer), (int)spawnPos.X, (int)spawnPos.Y, npcType, 1);
            if (index == Main.maxNPCs) {
                return null;
            }
            NPC boss = Main.npc[index];
            boss.timeLeft *= 20;
            if (VaultUtils.isServer) {
                NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, index);
                ChatHelper.BroadcastChatMessage(NetworkText.FromKey("Announcement.HasAwoken", boss.GetTypeNetName()), new Color(175, 75, 255));
            }
            else {
                Main.NewText(Language.GetTextValue("Announcement.HasAwoken", boss.TypeName), 175, 75, 255);
            }
            return boss;
        }

        public static void SetDraedonDefeatTimer(NPC npc, float value) {
            if (draedon_DefeatTimer_M == null || draedonType == null) {
                return;
            }
            ModNPC modNPC = npc?.ModNPC;
            if (modNPC == null || modNPC.GetType() != draedonType) {
                return;
            }
            SetMember(draedon_DefeatTimer_M, modNPC, value);
        }

        public static float GetDraedonDefeatTimer(NPC npc) {
            if (draedon_DefeatTimer_M == null || draedonType == null) {
                return 0f;
            }
            ModNPC modNPC = npc?.ModNPC;
            if (modNPC == null || modNPC.GetType() != draedonType) {
                return 0f;
            }
            return (float)GetMember(draedon_DefeatTimer_M, modNPC);
        }

        public static bool HasExo() {
            if (CWRID.NPC_ThanatosHead > NPCID.None && NPC.AnyNPCs(CWRID.NPC_ThanatosHead))
                return true;
            if (CWRID.NPC_AresBody > NPCID.None && NPC.AnyNPCs(CWRID.NPC_AresBody))
                return true;
            if (CWRID.NPC_Artemis > NPCID.None && NPC.AnyNPCs(CWRID.NPC_Artemis))
                return true;
            if (CWRID.NPC_Apollo > NPCID.None && NPC.AnyNPCs(CWRID.NPC_Apollo))
                return true;
            return false;
        }

        public static void SetAbleToSelectExoMech(Player player, bool value) {
            ModPlayer cp = GetCalPlayer(player);
            if (cp == null || calPlayer_AbleToSelectExoMech_M == null) {
                return;
            }
            SetMember(calPlayer_AbleToSelectExoMech_M, cp, value);
        }

        public static void SetProjtimesPierced(this Projectile projectile, int value) {
            GlobalProjectile cgp = GetCalProj(projectile);
            if (cgp == null || calProj_timesPierced_M == null) {
                return;
            }
            SetMember(calProj_timesPierced_M, cgp, value);
        }

        public static void SetAllProjectilesHome(this Projectile projectile, bool value) {
            GlobalProjectile cgp = GetCalProj(projectile);
            if (cgp == null || calProj_conditionalHomingRange_M == null) {
                return;
            }
            SetMember(calProj_conditionalHomingRange_M, cgp, value ? 450 : 0);
        }

        public static void SetDownedCalamitas(bool value) => SetDownedProp(downedCalamitasProp, value);

        public static SoundStyle GetSound(this string path, SoundStyle backupSound = default) {
            if (ModContent.HasAsset(path)) {
                return new SoundStyle(path);
            }
            if (backupSound == default) {
                backupSound = CWRSound.None;
            }
            return backupSound;
        }

        public static bool GetDownedThanatos() => GetDownedProp(downedThanatosProp);

        //将所有灾厄Boss击杀标志批量写入emit回调（key为短键名，value为当前标志值）
        internal static void BulkCopyCalamityFlags(Action<string, bool> emit) {
            if (DownedBossSystemType is null) return;
            emit("ds", GetDownedProp(downedDesertScourgeProp));
            emit("clam", GetDownedProp(downedCLAMProp));
            emit("crab", GetDownedProp(downedCrabulonProp));
            emit("hm", GetDownedProp(downedHiveMindProp));
            emit("perf", GetDownedProp(downedPerforatorProp));
            emit("sg", GetDownedProp(downedSlimeGodProp));
            emit("cryo", GetDownedProp(downedCryogenProp));
            emit("brim", GetDownedProp(downedBrimstoneElementalProp));
            emit("aq", GetDownedProp(downedAquaticScourgeProp));
            emit("crag", GetDownedProp(downedCragmawMireProp));
            emit("cc", GetDownedProp(downedCalamitasCloneProp));
            emit("gss", GetDownedProp(downedGSSProp));
            emit("lev", GetDownedProp(downedLeviathanProp));
            emit("aa", GetDownedProp(downedAstrumAureusProp));
            emit("pb", GetDownedProp(downedPlaguebringerProp));
            emit("rav", GetDownedProp(downedRavagerProp));
            emit("ade", GetDownedProp(downedAstrumDeusProp));
            emit("grd", GetDownedProp(downedGuardiansProp));
            emit("df", GetDownedProp(downedDragonfollyProp));
            emit("prov", GetDownedProp(downedProvidenceProp));
            emit("cv", GetDownedProp(downedCeaselessVoidProp));
            emit("sw", GetDownedProp(downedStormWeaverProp));
            emit("sig", GetDownedProp(downedSignusProp));
            emit("pol", GetDownedProp(downedPolterghastProp));
            emit("maul", GetDownedProp(downedMaulerProp));
            emit("nuke", GetDownedProp(downedNuclearTerrorProp));
            emit("bd", GetDownedProp(downedBoomerDukeProp));
            emit("dog", GetDownedProp(downedDoGProp));
            emit("yha", GetDownedProp(downedYharonProp));
            emit("exo", GetDownedProp(downedExoMechsProp));
            emit("scal", GetDownedProp(downedCalamitasProp));
            emit("pw", GetDownedProp(downedPrimordialWyrmProp));
            emit("br", GetDownedProp(downedBossRushProp));
            emit("than", GetDownedProp(downedThanatosProp));
        }

        //将快照中值为true的灾厄Boss标志以OR方式写回（只补true，不抹除已有的true）
        internal static void BulkRestoreCalamityFlagsOr(Func<string, bool> read) {
            if (DownedBossSystemType is null) return;
            if (read("ds")) SetDownedProp(downedDesertScourgeProp, true);
            if (read("clam")) SetDownedProp(downedCLAMProp, true);
            if (read("crab")) SetDownedProp(downedCrabulonProp, true);
            if (read("hm")) SetDownedProp(downedHiveMindProp, true);
            if (read("perf")) SetDownedProp(downedPerforatorProp, true);
            if (read("sg")) SetDownedProp(downedSlimeGodProp, true);
            if (read("cryo")) SetDownedProp(downedCryogenProp, true);
            if (read("brim")) SetDownedProp(downedBrimstoneElementalProp, true);
            if (read("aq")) SetDownedProp(downedAquaticScourgeProp, true);
            if (read("crag")) SetDownedProp(downedCragmawMireProp, true);
            if (read("cc")) SetDownedProp(downedCalamitasCloneProp, true);
            if (read("gss")) SetDownedProp(downedGSSProp, true);
            if (read("lev")) SetDownedProp(downedLeviathanProp, true);
            if (read("aa")) SetDownedProp(downedAstrumAureusProp, true);
            if (read("pb")) SetDownedProp(downedPlaguebringerProp, true);
            if (read("rav")) SetDownedProp(downedRavagerProp, true);
            if (read("ade")) SetDownedProp(downedAstrumDeusProp, true);
            if (read("grd")) SetDownedProp(downedGuardiansProp, true);
            if (read("df")) SetDownedProp(downedDragonfollyProp, true);
            if (read("prov")) SetDownedProp(downedProvidenceProp, true);
            if (read("cv")) SetDownedProp(downedCeaselessVoidProp, true);
            if (read("sw")) SetDownedProp(downedStormWeaverProp, true);
            if (read("sig")) SetDownedProp(downedSignusProp, true);
            if (read("pol")) SetDownedProp(downedPolterghastProp, true);
            if (read("maul")) SetDownedProp(downedMaulerProp, true);
            if (read("nuke")) SetDownedProp(downedNuclearTerrorProp, true);
            if (read("bd")) SetDownedProp(downedBoomerDukeProp, true);
            if (read("dog")) SetDownedProp(downedDoGProp, true);
            if (read("yha")) SetDownedProp(downedYharonProp, true);
            if (read("exo")) SetDownedProp(downedExoMechsProp, true);
            if (read("scal")) SetDownedProp(downedCalamitasProp, true);
            if (read("pw")) SetDownedProp(downedPrimordialWyrmProp, true);
            if (read("br")) SetDownedProp(downedBossRushProp, true);
            if (read("than")) SetDownedProp(downedThanatosProp, true);
        }

        public static int GetSupCalGiveUpCounter(NPC npc) {
            if (supCal_giveUpCounter_Field == null || supCalType == null) {
                return 0;
            }
            ModNPC modNPC = npc?.ModNPC;
            if (modNPC == null || modNPC.GetType() != supCalType) {
                return 0;
            }
            return (int)supCal_giveUpCounter_Field.GetValue(modNPC);
        }

        public static void SetSupCalGiveUpCounter(NPC npc, int value) {
            if (supCal_giveUpCounter_Field == null || supCalType == null) {
                return;
            }
            ModNPC modNPC = npc?.ModNPC;
            if (modNPC == null || modNPC.GetType() != supCalType) {
                return;
            }
            supCal_giveUpCounter_Field.SetValue(modNPC, value);
        }

        public static Type FindCalamityType(string key) {
            if (CWRMod.Instance.calamity != null) {
                return GetModType(CWRMod.Instance.calamity, key);
            }
            return null;
        }

        public static Type GetNPC_WITCH_Type() => FindCalamityType("CalamityMod.NPCs.TownNPCs.BrimstoneWitch");
        public static Type GetNPC_SupCal_Type() => FindCalamityType("CalamityMod.NPCs.SupremeCalamitas.SupremeCalamitas");

        public static bool GetEarlyHardmodeProgressionReworkBool() {
            if (calConfig_EarlyHardmodeRework_Prop == null || calamityServerConfigInstance == null) {
                return false;
            }
            return (bool)calConfig_EarlyHardmodeRework_Prop.GetValue(calamityServerConfigInstance);
        }

        public static float GetNPCDR(NPC npc) {
            GlobalNPC cgn = GetCalNPC(npc);
            if (cgn == null || calNPC_DR_M == null) {
                return 0f;
            }
            return (float)GetMember(calNPC_DR_M, cgn);
        }

        public static int GetProjectileDamage(NPC npc, int projType) => 40;

        public static void SetPlayerInfiniteFlight(this Player player, bool value) {
            ModPlayer cp = GetCalPlayer(player);
            if (cp == null || calPlayer_infiniteFlight_M == null) {
                return;
            }
            SetMember(calPlayer_infiniteFlight_M, cp, value);
        }

        public static void SetPlayerDefenseDamageImmunity(this Player player, bool value) {
            ModPlayer cp = GetCalPlayer(player);
            if (cp == null || calPlayer_externalDefenseDamageImmunity_M == null) {
                return;
            }
            SetMember(calPlayer_externalDefenseDamageImmunity_M, cp, value);
        }

        public static void OldDukeOnKill(NPC npc) {
            StopAcidRain();
            calNPC_SetNewBossJustDowned_Method?.Invoke(null, new object[] { npc });
            SetDownedProp(downedBoomerDukeProp, true);
            SetMember(acidRain_OldDukeEncountered_M, null, true);
            NPCLoader.OnKill(npc);
        }

        public static void StopAcidRain() {
            if (acidRain_KillPoints_Field == null || acidRain_UpdateInvasion_Method == null) {
                return;
            }
            acidRain_KillPoints_Field.SetValue(null, 0);
            try {
                acidRain_UpdateInvasion_Method.Invoke(null, new object[] { true });
            } catch (TargetParameterCountException) {
                acidRain_UpdateInvasion_Method.Invoke(null, null);
            }
        }

        public static float GetItemCharge(this Item item) {
            GlobalItem cgi = GetCalItem(item);
            if (cgi == null || calItem_Charge_M == null) {
                return 0f;
            }
            return (float)GetMember(calItem_Charge_M, cgi);
        }

        public static void SetItemCharge(this Item item, float value) {
            GlobalItem cgi = GetCalItem(item);
            if (cgi == null || calItem_Charge_M == null) {
                return;
            }
            SetMember(calItem_Charge_M, cgi, value);
        }

        public static float GetItemMaxCharge(this Item item) {
            GlobalItem cgi = GetCalItem(item);
            if (cgi == null || calItem_MaxCharge_M == null) {
                return 0f;
            }
            return (float)GetMember(calItem_MaxCharge_M, cgi);
        }

        public static void SetItemMaxCharge(this Item item, float value) {
            GlobalItem cgi = GetCalItem(item);
            if (cgi == null || calItem_MaxCharge_M == null) {
                return;
            }
            SetMember(calItem_MaxCharge_M, cgi, value);
        }

        public static bool GetItemUsesCharge(this Item item) {
            GlobalItem cgi = GetCalItem(item);
            if (cgi == null || calItem_UsesCharge_M == null) {
                return false;
            }
            return (bool)GetMember(calItem_UsesCharge_M, cgi);
        }

        public static bool SetItemUsesCharge(this Item item, bool value) {
            GlobalItem cgi = GetCalItem(item);
            if (cgi == null || calItem_UsesCharge_M == null) {
                return false;
            }
            SetMember(calItem_UsesCharge_M, cgi, value);
            return value;
        }

        public static void AddPlayerRogueStealthMax(this Player player, float add) {
            ModPlayer cp = GetCalPlayer(player);
            if (cp == null || calPlayer_rogueStealthMax_M == null) {
                return;
            }
            SetMember(calPlayer_rogueStealthMax_M, cp, (float)GetMember(calPlayer_rogueStealthMax_M, cp) + add);
        }

        public static bool GetPlayerZoneSulphur(this Player player) {
            ModPlayer cp = GetCalPlayer(player);
            if (cp == null || calPlayer_ZoneSulphur_M == null) {
                return false;
            }
            return (bool)GetMember(calPlayer_ZoneSulphur_M, cp);
        }

        public static bool GetPlayerZoneAbyss(this Player player) {
            ModPlayer cp = GetCalPlayer(player);
            if (cp == null || calPlayer_ZoneAbyss_M == null) {
                return false;
            }
            return (bool)GetMember(calPlayer_ZoneAbyss_M, cp);
        }

        public static bool GetPlayerProfanedCrystalBuffs(this Player player) {
            ModPlayer cp = GetCalPlayer(player);
            if (cp == null || calPlayer_profanedCrystalBuffs_M == null) {
                return false;
            }
            return (bool)GetMember(calPlayer_profanedCrystalBuffs_M, cp);
        }

        public static void SetPlayerDashID(this Player player, string value) {
            ModPlayer cp = GetCalPlayer(player);
            if (cp == null || calPlayer_DashID_M == null) {
                return;
            }
            SetMember(calPlayer_DashID_M, cp, value);
        }

        public static LocalizedText ConstructRecipeCondition(int tier, out Func<bool> condition) {
            if (arsenalRecipe_ConstructCondition_Method != null) {
                try {
                    object[] args = [tier, null];
                    LocalizedText text = (LocalizedText)arsenalRecipe_ConstructCondition_Method.Invoke(null, args);
                    condition = (Func<bool>)args[1];
                    if (text != null && condition != null) {
                        return text;
                    }
                } catch (Exception ex) {
                    LogReflectionException(nameof(ConstructRecipeCondition), ex);
                }
            }
            //灾厄缺失或反射失败时退化为恒真条件，保证配方注册不会因null条件而崩溃
            condition = () => true;
            return LocalizedText.Empty;
        }

        #region 炼铸系统包装器
        /// CalamityMod Enchantment 安全包装
        public struct EnchantmentWrapper
        {
            /// 附魔名
            public LocalizedText Name { get; set; }

            /// 附魔描述
            public LocalizedText Description { get; set; }

            /// 图标路径
            public string IconTexturePath { get; set; }

            /// 内部比较 ID
            internal int InternalId { get; set; }

            /// 是否清除附魔项
            public bool IsClearEnchantment { get; set; }

            public override bool Equals(object obj) {
                if (obj is EnchantmentWrapper other)
                    return InternalId == other.InternalId;
                return false;
            }

            public override int GetHashCode() => InternalId;

            public static bool operator ==(EnchantmentWrapper left, EnchantmentWrapper right)
                => left.InternalId == right.InternalId;

            public static bool operator !=(EnchantmentWrapper left, EnchantmentWrapper right)
                => !(left == right);
        }

        // 反射调用 GetValidEnchantmentsForItem
        private static IEnumerable GetRawEnchantmentsForItem(Item item) {
            if (enchantManager_GetValidEnchantments_Method == null) {
                return null;
            }
            try {
                return enchantManager_GetValidEnchantments_Method.Invoke(null, [item]) as IEnumerable;
            } catch (Exception ex) {
                LogReflectionException("GetValidEnchantmentsForItem", ex);
                return null;
            }
        }

        /// 获取物品有效附魔列表
        public static List<EnchantmentWrapper> GetValidEnchantmentsForItem(Item item) {
            var result = new List<EnchantmentWrapper>();
            if (item == null || item.IsAir) {
                return result;
            }
            IEnumerable enchantments = GetRawEnchantmentsForItem(item);
            if (enchantments == null || enchant_Name_M == null || enchant_Description_M == null || enchant_IconTexturePath_M == null) {
                return result;
            }

            object clearEnchantment = enchantManager_ClearEnchantment_M != null
                ? GetMember(enchantManager_ClearEnchantment_M, null) : null;

            int id = 0;
            foreach (object enchantment in enchantments) {
                result.Add(new EnchantmentWrapper {
                    Name = GetMember(enchant_Name_M, enchantment) as LocalizedText,
                    Description = GetMember(enchant_Description_M, enchantment) as LocalizedText,
                    IconTexturePath = GetMember(enchant_IconTexturePath_M, enchantment) as string,
                    InternalId = id++,
                    IsClearEnchantment = enchantment.Equals(clearEnchantment)
                });
            }

            return result;
        }

        /// 应用附魔到物品
        public static void ApplyEnchantmentToItem(Item item, EnchantmentWrapper wrapper, Action<Item> creationEffect = null) {
            if (item == null || item.IsAir || calItem_AppliedEnchantment_M == null) {
                return;
            }

            int oldPrefix = item.prefix;
            item.SetDefaults(item.type);
            item.Prefix(oldPrefix);

            GlobalItem cgi = GetCalItem(item);
            if (cgi == null) {
                return;
            }

            if (wrapper.IsClearEnchantment) {
                SetMember(calItem_AppliedEnchantment_M, cgi, null);
                item.Prefix(oldPrefix);
                return;
            }

            //按 Name+Description 回匹配 Enchantment
            IEnumerable allEnchantments = GetRawEnchantmentsForItem(item);
            if (allEnchantments == null || enchant_Name_M == null || enchant_Description_M == null) {
                return;
            }

            object targetEnchant = null;
            foreach (object ench in allEnchantments) {
                LocalizedText name = GetMember(enchant_Name_M, ench) as LocalizedText;
                LocalizedText description = GetMember(enchant_Description_M, ench) as LocalizedText;
                if (name?.Value == wrapper.Name?.Value && description?.Value == wrapper.Description?.Value) {
                    targetEnchant = ench;
                    break;
                }
            }

            if (targetEnchant == null) {
                return;
            }

            try {
                //装箱 Enchantment 赋给 AppliedEnchantment? 成员
                SetMember(calItem_AppliedEnchantment_M, cgi, targetEnchant);
                creationEffect?.Invoke(item);
                if (enchant_CreationEffect_M != null
                    && GetMember(enchant_CreationEffect_M, targetEnchant) is Action<Item> enchantCreation) {
                    enchantCreation.Invoke(item);
                }

                if (enchantManager_ItemUpgradeRelationship_M != null
                    && GetMember(enchantManager_ItemUpgradeRelationship_M, null) is IDictionary upgradeRelationship
                    && upgradeRelationship.Contains(item.type)) {
                    item.SetDefaults((int)upgradeRelationship[item.type]);
                    item.Prefix(oldPrefix);
                }
            } catch (Exception ex) {
                LogReflectionException(nameof(ApplyEnchantmentToItem), ex);
            }
        }
        #endregion

        #region 加载联动修改内容
        // VaultHook 反射目标
        public static MethodBase BossHealthBarManager_Draw_Method;
        public static MethodBase calamityUtils_GetReworkedReforge_Method; //UnLoad 占位

        internal delegate void On_DisplayLocalizedText_Dalegate(string key, Color? textColor = null);

        internal static void LoadComders() {
            Mod mod = CWRMod.Instance?.calamity;
            if (mod == null) {
                return;
            }
            try {
                //无扩展接口，Hook Draw 让村正充能 UI 改血条位置
                Type bossHealthBarManagerType = GetModType(mod, "CalamityMod.UI.BossHealthBarManager");
                BossHealthBarManager_Draw_Method = GetMethod(bossHealthBarManagerType, "Draw", PublicInstanceFlags);
                Type bossHPUIType = GetModType(mod, "CalamityMod.UI.BossHealthBarManager+BossHPUI");
                if (bossHealthBarManagerType != null && bossHPUIType != null) {
                    bossBar_Bars_M = GetFieldOrProperty(bossHealthBarManagerType, "Bars", PublicStaticFlags);
                    bossHPUI_Draw_Method = GetMethod(bossHPUIType, "Draw", PublicInstanceFlags);
                    FieldInfo verticalOffsetField = GetField(bossHPUIType, "VerticalOffsetPerBar", PublicStaticFlags);
                    if (verticalOffsetField != null) {
                        bossHPUI_VerticalOffsetPerBar = (int)verticalOffsetField.GetValue(null);
                    }
                }
                if (BossHealthBarManager_Draw_Method != null && bossBar_Bars_M != null && bossHPUI_Draw_Method != null) {
                    VaultHook.Add(BossHealthBarManager_Draw_Method, On_BossHealthBarManager_Draw_Hook);
                }

                //Hook BroadcastLocalizedText → ModifyDisplayText
                Type calUtilsType = GetModType(mod, "CalamityMod.CalamityUtils");
                MethodInfo methodInfo = GetMethod(calUtilsType, "BroadcastLocalizedText", PublicStaticFlags);
                if (methodInfo != null) {
                    VaultHook.Add(methodInfo, OnDisplayLocalizedTextHook);
                }

                //Luminance 同名 BroadcastLocalizedText 也需 Hook
                if (CWRMod.Instance.luminance != null) {
                    Type utType = FindModType(CWRMod.Instance.luminance, "Utilities");
                    methodInfo = GetMethod(utType, "BroadcastLocalizedText", PublicStaticFlags);
                    if (methodInfo != null) {
                        VaultHook.Add(methodInfo, OnDisplayLocalizedTextHook);
                    }
                }

                Type playerType = GetModType(mod, "CalamityMod.CalPlayer.CalamityPlayer");
                MethodInfo method = GetMethod(playerType, "KillPlayer", PublicInstanceFlags);
                if (method != null) {
                    VaultHook.Add(method, On_KillPlayer_Hook);
                }

                MethodInfo provideStealthMethod = GetMethod(playerType, "ProvideStealthStatBonuses", BindingFlags.Instance | BindingFlags.NonPublic);
                if (provideStealthMethod != null) {
                    VaultHook.Add(provideStealthMethod, OnProvideStealthStatBonusesHook);
                }
            } catch (Exception ex) {
                LogReflectionException(nameof(LoadComders), ex);
            }
        }

        public static void On_KillPlayer_Hook(Action<ModPlayer> orig, ModPlayer modPlayer) {
            if (modPlayer?.Player?.TryGetOverride<PlayerDeath>(out var playerDeath) == true) {
                bool pvp = false;
                bool playSound = false;
                PlayerDeathReason damageSource = null;
                if (playerDeath.On_PreKill(9999, 1, false, ref pvp, ref playSound, ref damageSource) == false) {
                    return;
                }
            }
            orig.Invoke(modPlayer);
        }

        private static void On_BossHealthBarManager_Draw_Hook(On_BossHealthBarManager_Draw_Dalegate orig, object obj, SpriteBatch spriteBatch, IBigProgressBar currentBar, BigProgressBarInfo info) {
            //Bars/BossHPUI 为 public，迭代自定义绘制
            if (GetMember(bossBar_Bars_M, null) is not IEnumerable bars || bossHPUI_Draw_Method == null) {
                orig.Invoke(obj, spriteBatch, currentBar, info);
                return;
            }
            int startHeight = 100;
            int x = Main.screenWidth - 420;
            int y = Main.screenHeight - startHeight;
            if (Main.playerInventory || VaultUtils.IsInvasion()) {
                x -= 250;
            }
            Vector2 modifyPos = MuraChargeUI.Instance.ModifyBossHealthBarManagerPositon(x, y);
            x = (int)modifyPos.X;
            y = (int)modifyPos.Y;
            foreach (object ui in bars) {
                bossHPUI_Draw_Method.Invoke(ui, [spriteBatch, x, y]);
                y -= bossHPUI_VerticalOffsetPerBar;
            }
        }

        internal static void OnDisplayLocalizedTextHook(On_DisplayLocalizedText_Dalegate orig, string key, Color? textColor = null) {
            Color color = textColor ?? Color.White;
            if (VaultLoad.LoadenContent) {
                bool result = true;
                foreach (var d in ModifyDisplayText.Instances) {
                    if (!d.Alive(Main.LocalPlayer)) {
                        continue;
                    }
                    bool newResult = d.Handle(ref key, ref color);
                    if (!newResult) {
                        result = false;
                    }
                }
                if (!result) {
                    return;
                }
            }

            orig.Invoke(key, color);
        }

        private static void OnProvideStealthStatBonusesHook(On_ProvideStealthStatBonuses_Dalegate orig, ModPlayer calamityPlayer) {
            Player player = calamityPlayer?.Player;
            if (player != null && player.CWR().IsUnsunghero
                && calPlayer_wearingRogueArmor_M != null && calPlayer_rogueStealthMax_M != null
                && calPlayer_rogueStealth_M != null && calPlayer_stealthDamage_M != null
                && balancing_StealthFactor_Field != null) {
                if (!(bool)GetMember(calPlayer_wearingRogueArmor_M, calamityPlayer)
                    || (float)GetMember(calPlayer_rogueStealthMax_M, calamityPlayer) <= 0) {
                    return;
                }

                Item item = player.GetItem();
                int realUseTime = Math.Max(item.useTime, item.useAnimation);
                double useTimeFactor = 0.75 + 0.75 * Math.Log(realUseTime + 2D, 4D);
                //Unsunghero 固定 4 秒潜行 generation
                double stealthGenFactor = Math.Max(Math.Pow(4f, 2D / 3D), 1.5);

                float rogueStealth = (float)GetMember(calPlayer_rogueStealth_M, calamityPlayer);
                //double 可变静态，Convert 兼容类型变动
                double stealthStrikeFactor = Convert.ToDouble(balancing_StealthFactor_Field.GetValue(null));
                double stealthAddedDamage = rogueStealth * stealthStrikeFactor * useTimeFactor * stealthGenFactor;
                SetMember(calPlayer_stealthDamage_M, calamityPlayer
                    , (float)GetMember(calPlayer_stealthDamage_M, calamityPlayer) + (float)stealthAddedDamage);

                player.aggro -= (int)(rogueStealth * 300f);

                return;
            }

            orig.Invoke(calamityPlayer);
        }
        #endregion
    }
}