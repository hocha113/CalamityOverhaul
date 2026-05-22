using CalamityMod;
using CalamityMod.Balancing;
using CalamityMod.CalPlayer;
using CalamityMod.CustomRecipes;
using CalamityMod.UI;
using CalamityMod.World;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.ADV;
using CalamityOverhaul.Content.LegendWeapon.MurasamaLegend.UI;
using InnoVault.GameSystem;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Reflection;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.GameContent.UI.BigProgressBar;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using static CalamityOverhaul.Common.ModGanged;

namespace CalamityOverhaul
{
    /// <summary>
    /// 一个用于访问Calamity Mod内部内容的静态类
    /// </summary>
    internal static class CWRRef
    {
        /// <summary>
        /// Calamity Mod的目标版本，只有当安装了这个版本的Calamity Mod时才会启用相关功能
        /// </summary>
        public static Version TargetCalamityVersion => new(2, 1, 2);
        /// <summary>
        /// 是否安装了指定版本的Calamity Mod
        /// </summary>
        public static bool Has {
            get {
                _has ??= ModLoader.TryGetMod("CalamityMod", out Mod mod) && mod.Version == TargetCalamityVersion;
                return _has.Value;
            }
        }
        private static bool? _has = null;

        private static float dummyFloat;
        private static Type DownedBossSystemType;

        #region 反射缓存：Calamity 静态状态
        // CalamityWorld
        private static FieldInfo calWorld_death_Field;
        private static FieldInfo calWorld_revenge_Field;
        // BossRushEvent
        private static PropertyInfo bossRush_Active_Prop;
        // AcidRainEvent
        private static PropertyInfo acidRain_Ongoing_Prop;
        private static FieldInfo acidRain_KillPoints_Field;
        private static MethodInfo acidRain_UpdateInvasion_Method;
        private static PropertyInfo acidRain_OldDukeEncountered_Prop;
        // CalamityServerConfig
        private static object calamityServerConfigInstance;
        private static PropertyInfo calConfig_EarlyHardmodeRework_Prop;
        // CalamityGlobalNPC 静态方法
        private static MethodInfo calNPC_SetNewBossJustDowned_Method;
        // DamageClasses
        private static DamageClass trueMeleeDamageClass;
        private static DamageClass trueMeleeNoSpeedDamageClass;
        #endregion

        #region 反射缓存：Calamity ModNPC 类型与字段
        private static Type supCalType;
        private static FieldInfo supCal_giveUpCounter_Field;
        private static Type draedonType;
        private static MemberInfo draedon_DefeatTimer_M;
        #endregion

        #region 反射缓存：CalamityUtils 静态委托
        private static Action<Projectile, int, Color, int, Texture2D, bool> calUtils_DrawAfterimagesCenteredDel;
        private static Action<Projectile, bool, float, float, float> calUtils_HomeInOnNPCDel;
        private static Action<Projectile> calUtils_LargeFieryExplosionDel;
        #endregion

        #region 反射缓存：Calamity 全局内容模板
        private static ModPlayer calPlayerTemplate;
        private static GlobalItem calGlobalItemTemplate;
        private static GlobalNPC calGlobalNPCTemplate;
        private static GlobalProjectile calGlobalProjectileTemplate;
        #endregion

        #region 反射缓存：CalamityPlayer / CalamityGlobalItem / CalamityGlobalNPC / CalamityGlobalProjectile 成员
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

        private static MemberInfo calItem_ChargeRatio_M;
        private static MemberInfo calItem_MaxCharge_M;
        private static MemberInfo calItem_UsesCharge_M;

        private static MemberInfo calNPC_DR_M;

        private static MemberInfo calProj_timesPierced_M;
        private static MemberInfo calProj_conditionalHomingRange_M;
        #endregion

        #region 反射通用助手
        private static MemberInfo FindMember(Type type, string name) {
            if (type == null) {
                return null;
            }
            const BindingFlags bf = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            MemberInfo m = type.GetField(name, bf);
            if (m != null) {
                return m;
            }
            return type.GetProperty(name, bf);
        }

        private static MemberInfo FindStaticMember(Type type, string name) {
            if (type == null) {
                return null;
            }
            const BindingFlags bf = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
            MemberInfo m = type.GetField(name, bf);
            if (m != null) {
                return m;
            }
            return type.GetProperty(name, bf);
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

        private static T TryCreateStaticDelegate<T>(Type type, string methodName) where T : Delegate {
            if (type == null) {
                return null;
            }
            MethodInfo method = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static);
            if (method == null) {
                return null;
            }
            try {
                return (T)method.CreateDelegate(typeof(T));
            } catch {
                return null;
            }
        }

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
                return open?.MakeGenericMethod(t).Invoke(null, null);
            } catch {
                return null;
            }
        }

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
            LoadCalamityUtilsDelegates(mod);
            LoadCalamityModNPCs(mod);
            LoadCalamityGlobalTemplates();
        }

        private static void LoadBossFlags(Mod mod) {
            DownedBossSystemType = mod.Code.GetType("CalamityMod.DownedBossSystem");
            if (DownedBossSystemType == null) {
                return;
            }
            const BindingFlags bf = BindingFlags.Public | BindingFlags.Static;
            downedDesertScourgeProp = DownedBossSystemType.GetProperty("downedDesertScourge", bf);
            downedCLAMProp = DownedBossSystemType.GetProperty("downedCLAM", bf);
            downedCrabulonProp = DownedBossSystemType.GetProperty("downedCrabulon", bf);
            downedHiveMindProp = DownedBossSystemType.GetProperty("downedHiveMind", bf);
            downedPerforatorProp = DownedBossSystemType.GetProperty("downedPerforator", bf);
            downedSlimeGodProp = DownedBossSystemType.GetProperty("downedSlimeGod", bf);
            downedCryogenProp = DownedBossSystemType.GetProperty("downedCryogen", bf);
            downedBrimstoneElementalProp = DownedBossSystemType.GetProperty("downedBrimstoneElemental", bf);
            downedAquaticScourgeProp = DownedBossSystemType.GetProperty("downedAquaticScourge", bf);
            downedCragmawMireProp = DownedBossSystemType.GetProperty("downedCragmawMire", bf);
            downedCalamitasCloneProp = DownedBossSystemType.GetProperty("downedCalamitasClone", bf);
            downedGSSProp = DownedBossSystemType.GetProperty("downedGSS", bf);
            downedLeviathanProp = DownedBossSystemType.GetProperty("downedLeviathan", bf);
            downedAstrumAureusProp = DownedBossSystemType.GetProperty("downedAstrumAureus", bf);
            downedPlaguebringerProp = DownedBossSystemType.GetProperty("downedPlaguebringer", bf);
            downedRavagerProp = DownedBossSystemType.GetProperty("downedRavager", bf);
            downedAstrumDeusProp = DownedBossSystemType.GetProperty("downedAstrumDeus", bf);
            downedGuardiansProp = DownedBossSystemType.GetProperty("downedGuardians", bf);
            downedDragonfollyProp = DownedBossSystemType.GetProperty("downedDragonfolly", bf);
            downedProvidenceProp = DownedBossSystemType.GetProperty("downedProvidence", bf);
            downedCeaselessVoidProp = DownedBossSystemType.GetProperty("downedCeaselessVoid", bf);
            downedStormWeaverProp = DownedBossSystemType.GetProperty("downedStormWeaver", bf);
            downedSignusProp = DownedBossSystemType.GetProperty("downedSignus", bf);
            downedPolterghastProp = DownedBossSystemType.GetProperty("downedPolterghast", bf);
            downedMaulerProp = DownedBossSystemType.GetProperty("downedMauler", bf);
            downedNuclearTerrorProp = DownedBossSystemType.GetProperty("downedNuclearTerror", bf);
            downedBoomerDukeProp = DownedBossSystemType.GetProperty("downedBoomerDuke", bf);
            downedDoGProp = DownedBossSystemType.GetProperty("downedDoG", bf);
            downedYharonProp = DownedBossSystemType.GetProperty("downedYharon", bf);
            downedExoMechsProp = DownedBossSystemType.GetProperty("downedExoMechs", bf);
            downedCalamitasProp = DownedBossSystemType.GetProperty("downedCalamitas", bf);
            downedPrimordialWyrmProp = DownedBossSystemType.GetProperty("downedPrimordialWyrm", bf);
            downedBossRushProp = DownedBossSystemType.GetProperty("downedBossRush", bf);
            downedThanatosProp = DownedBossSystemType.GetProperty("downedThanatos", bf);
        }

        private static void LoadCalamityStaticState(Mod mod) {
            Type calWorld = mod.Code.GetType("CalamityMod.World.CalamityWorld");
            if (calWorld != null) {
                const BindingFlags bf = BindingFlags.Public | BindingFlags.Static;
                calWorld_death_Field = calWorld.GetField("death", bf);
                calWorld_revenge_Field = calWorld.GetField("revenge", bf);
            }

            Type bossRush = mod.Code.GetType("CalamityMod.Events.BossRushEvent");
            bossRush_Active_Prop = bossRush?.GetProperty("BossRushActive", BindingFlags.Public | BindingFlags.Static);

            Type acidRain = mod.Code.GetType("CalamityMod.Events.AcidRainEvent");
            if (acidRain != null) {
                const BindingFlags bf = BindingFlags.Public | BindingFlags.Static;
                acidRain_Ongoing_Prop = acidRain.GetProperty("AcidRainEventIsOngoing", bf);
                acidRain_KillPoints_Field = acidRain.GetField("AccumulatedKillPoints", bf);
                acidRain_UpdateInvasion_Method = acidRain.GetMethod("UpdateInvasion", bf);
                acidRain_OldDukeEncountered_Prop = acidRain.GetProperty("OldDukeHasBeenEncountered", bf);
            }

            Type calConfig = mod.Code.GetType("CalamityMod.CalamityServerConfig");
            if (calConfig != null) {
                calamityServerConfigInstance = GetModContentInstance(calConfig);
                calConfig_EarlyHardmodeRework_Prop = calConfig.GetProperty("EarlyHardmodeProgressionRework", BindingFlags.Public | BindingFlags.Instance);
            }

            Type calGlobalNPCType = mod.Code.GetType("CalamityMod.NPCs.CalamityGlobalNPC");
            calNPC_SetNewBossJustDowned_Method = calGlobalNPCType?.GetMethod("SetNewBossJustDowned", BindingFlags.Public | BindingFlags.Static);

            ModContent.TryFind("CalamityMod", "TrueMeleeDamageClass", out trueMeleeDamageClass);
            ModContent.TryFind("CalamityMod", "TrueMeleeNoSpeedDamageClass", out trueMeleeNoSpeedDamageClass);
        }

        private static void LoadCalamityUtilsDelegates(Mod mod) {
            Type calUtils = mod.Code.GetType("CalamityMod.CalamityUtils");
            if (calUtils == null) {
                return;
            }
            calUtils_DrawAfterimagesCenteredDel = TryCreateStaticDelegate<Action<Projectile, int, Color, int, Texture2D, bool>>(calUtils, "DrawAfterimagesCentered");
            calUtils_HomeInOnNPCDel = TryCreateStaticDelegate<Action<Projectile, bool, float, float, float>>(calUtils, "HomeInOnNPC");
            calUtils_LargeFieryExplosionDel = TryCreateStaticDelegate<Action<Projectile>>(calUtils, "LargeFieryExplosion");
        }

        private static void LoadCalamityModNPCs(Mod mod) {
            supCalType = mod.Code.GetType("CalamityMod.NPCs.SupremeCalamitas.SupremeCalamitas");
            supCal_giveUpCounter_Field = supCalType?.GetField("giveUpCounter",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            draedonType = mod.Code.GetType("CalamityMod.NPCs.ExoMechs.Draedon");
            draedon_DefeatTimer_M = FindMember(draedonType, "DefeatTimer");
        }

        private static void LoadCalamityGlobalTemplates() {
            ModContent.TryFind("CalamityMod", "CalamityPlayer", out calPlayerTemplate);
            ModContent.TryFind("CalamityMod", "CalamityGlobalItem", out calGlobalItemTemplate);
            ModContent.TryFind("CalamityMod", "CalamityGlobalNPC", out calGlobalNPCTemplate);
            ModContent.TryFind("CalamityMod", "CalamityGlobalProjectile", out calGlobalProjectileTemplate);

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

            calItem_ChargeRatio_M = FindMember(calItemType, "ChargeRatio");
            calItem_MaxCharge_M = FindMember(calItemType, "MaxCharge");
            calItem_UsesCharge_M = FindMember(calItemType, "UsesCharge");

            calNPC_DR_M = FindMember(calNPCType, "DR");

            calProj_timesPierced_M = FindMember(calProjType, "timesPierced");
            calProj_conditionalHomingRange_M = FindMember(calProjType, "conditionalHomingRange");
        }

        internal static void UnLoad() {
            _has = null;
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
            bossRush_Active_Prop = null;
            acidRain_Ongoing_Prop = null;
            acidRain_KillPoints_Field = null;
            acidRain_UpdateInvasion_Method = null;
            acidRain_OldDukeEncountered_Prop = null;
            calamityServerConfigInstance = null;
            calConfig_EarlyHardmodeRework_Prop = null;
            calNPC_SetNewBossJustDowned_Method = null;
            trueMeleeDamageClass = null;
            trueMeleeNoSpeedDamageClass = null;

            supCalType = null;
            supCal_giveUpCounter_Field = null;
            draedonType = null;
            draedon_DefeatTimer_M = null;

            calUtils_DrawAfterimagesCenteredDel = null;
            calUtils_HomeInOnNPCDel = null;
            calUtils_LargeFieryExplosionDel = null;

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

            calItem_ChargeRatio_M = null;
            calItem_MaxCharge_M = null;
            calItem_UsesCharge_M = null;

            calNPC_DR_M = null;

            calProj_timesPierced_M = null;
            calProj_conditionalHomingRange_M = null;

            BossHealthBarManager_Draw_Method = null;
            calamityUtils_GetReworkedReforge_Method = null;
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
            return bossRush_Active_Prop != null && (bool)bossRush_Active_Prop.GetValue(null);
        }

        public static void SetBossRushActive(bool value) {
            bossRush_Active_Prop?.SetValue(null, value);
        }

        public static bool GetAcidRainEventIsOngoing() {
            return acidRain_Ongoing_Prop != null && (bool)acidRain_Ongoing_Prop.GetValue(null);
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

        public static void LargeFieryExplosion(Projectile projectile) {
            calUtils_LargeFieryExplosionDel?.Invoke(projectile);
        }

        public static void UpdateRogueStealth(Player player) {
            if (!Has) return;
            UpdateRogueStealthInner(player);
        }
        [CWRJITEnabled]
        private static void UpdateRogueStealthInner(Player player) {
            bool noAvailable = false;
            CalamityPlayer calPlayer = player.Calamity();
            if (CWRMod.Instance.narakuEye != null) {
                noAvailable = (bool)CWRMod.Instance.narakuEye.Call(player);
                if (calPlayer.StealthStrikeAvailable()) {
                    noAvailable = false;
                }
            }
            if (!noAvailable) {
                calPlayer.rogueStealth = 0;
                if (calPlayer.stealthUIAlpha > 0.02f) {
                    calPlayer.stealthUIAlpha -= 0.02f;
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
            SummonExoInner(exoType, player);
        }
        [CWRJITEnabled]
        public static void SummonExoInner(int exoType, Player player) {
            CalamityWorld.DraedonMechToSummon = (ExoMech)exoType;
            if (VaultUtils.isClient) {//客户端发送网络数据到服务器
                //通过反射直接调用 ExoMechSelectionPacket.Send()
                var calMod = ModLoader.GetMod("CalamityMod");
                var packetType = calMod.Code.GetType("CalamityMod.Packets.ExoMechSelectionPacket");
                var sendMethod = packetType.GetMethod("Send", BindingFlags.Public | BindingFlags.Static);
                sendMethod.Invoke(null, [/* toClient */ -1, /* ignoreClient */ -1]);
                return;
            }
            switch (CalamityWorld.DraedonMechToSummon) {
                case ExoMech.Destroyer:
                    Vector2 thanatosSpawnPosition = player.Center + Vector2.UnitY * 2100f;
                    NPC thanatos = CalamityUtils.SpawnBossBetter(thanatosSpawnPosition, CWRID.NPC_ThanatosHead);
                    if (thanatos != null)
                        thanatos.velocity = thanatos.SafeDirectionTo(player.Center) * 40f;
                    break;

                case ExoMech.Prime:
                    Vector2 aresSpawnPosition = player.Center - Vector2.UnitY * 1400f;
                    CalamityUtils.SpawnBossBetter(aresSpawnPosition, CWRID.NPC_AresBody);
                    break;

                case ExoMech.Twins:
                    Vector2 artemisSpawnPosition = player.Center + new Vector2(-1100f, -1600f);
                    Vector2 apolloSpawnPosition = player.Center + new Vector2(1100f, -1600f);
                    CalamityUtils.SpawnBossBetter(artemisSpawnPosition, CWRID.NPC_Artemis);
                    CalamityUtils.SpawnBossBetter(apolloSpawnPosition, CWRID.NPC_Apollo);
                    break;
            }
        }

        public static void DrawAfterimagesCentered(Projectile proj, int mode, Color lightColor, int typeOneIncrement = 1, Texture2D texture = null, bool drawCentered = true) {
            if (calUtils_DrawAfterimagesCenteredDel == null) {
                Main.spriteBatch.Draw(TextureAssets.Projectile[proj.type].Value, proj.Center - Main.screenPosition
                    , null, lightColor, proj.rotation, TextureAssets.Projectile[proj.type].Value.Size() / 2, proj.scale, SpriteEffects.None, 0);
                return;
            }
            calUtils_DrawAfterimagesCenteredDel(proj, mode, lightColor, typeOneIncrement, texture, drawCentered);
        }

        public static void HomeInOnNPC(Projectile projectile, bool ignoreTiles, float distanceRequired, float homingVelocity, float inertia) {
            calUtils_HomeInOnNPCDel?.Invoke(projectile, ignoreTiles, distanceRequired, homingVelocity, inertia);
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
            if (CWRID.NPC_ArtemisBoss > NPCID.None && NPC.AnyNPCs(CWRID.NPC_ArtemisBoss))
                return true;
            if (CWRID.NPC_ApolloBoss > NPCID.None && NPC.AnyNPCs(CWRID.NPC_ApolloBoss))
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

        public static SoundStyle GetSound(this string path) {
            if (ModContent.HasAsset(path)) {
                return new SoundStyle(path);
            }
            return CWRSound.None;
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
                return CWRMod.Instance.calamity.Code.GetType(key);
            }
            return null;
        }

        public static Type GetItem_SHPC_Type() => FindCalamityType("CalamityMod.Items.Weapons.Magic.SHPC");
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

        public static int GetProjectileDamage(NPC npc, int projType) {
            int num = npc.defDamage / 2;//暂时使用这个，原来的方法在某些情况下会返回1或者0
            if (Main.expertMode) {
                num = (int)(num * 0.75f);
            }
            if (Main.masterMode) {
                num = (int)(num * 0.75f);
            }
            return num;
        }

        public static void SetPlayerInfiniteFlight(this Player player, bool value) {
            ModPlayer cp = GetCalPlayer(player);
            if (cp == null || calPlayer_infiniteFlight_M == null) {
                return;
            }
            SetMember(calPlayer_infiniteFlight_M, cp, value);
        }

        public static void OldDukeOnKill(NPC npc) {
            StopAcidRain();
            calNPC_SetNewBossJustDowned_Method?.Invoke(null, new object[] { npc });
            SetDownedProp(downedBoomerDukeProp, true);
            acidRain_OldDukeEncountered_Prop?.SetValue(null, true);
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

        public static ref float RefItemCharge(this Item item) {
            if (!Has) {
                return ref dummyFloat;
            }
            return ref RefItemChargeInner(item);
        }
        [CWRJITEnabled]
        private static ref float RefItemChargeInner(Item item) => ref item.Calamity().Charge;

        public static float GetItemMaxCharge(this Item item) {
            GlobalItem cgi = GetCalItem(item);
            if (cgi == null || calItem_MaxCharge_M == null) {
                return 0f;
            }
            return (float)GetMember(calItem_MaxCharge_M, cgi);
        }

        public static ref float RefItemMaxCharge(this Item item) {
            if (!Has) {
                return ref dummyFloat;
            }
            return ref RefItemMaxChargeInner(item);
        }
        [CWRJITEnabled]
        private static ref float RefItemMaxChargeInner(Item item) => ref item.Calamity().MaxCharge;

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

        public static ref float RefPlayerRogueStealthMax(this Player player) {
            if (!Has) {
                return ref dummyFloat;
            }
            return ref RefPlayerRogueStealthMaxInner(player);
        }
        [CWRJITEnabled]
        private static ref float RefPlayerRogueStealthMaxInner(Player player) => ref player.Calamity().rogueStealthMax;

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
            condition = null;
            return Has ? ConstructRecipeConditionInner(tier, out condition) : null;
        }
        [CWRJITEnabled]
        private static LocalizedText ConstructRecipeConditionInner(int tier, out Func<bool> condition) => ArsenalTierGatedRecipe.ConstructRecipeCondition(tier, out condition);

        #region 炼铸系统包装器
        /// <summary>
        /// 附魔包装器结构体，用于安全地封装CalamityMod的Enchantment
        /// </summary>
        public struct EnchantmentWrapper
        {
            /// <summary>
            /// 附魔名称
            /// </summary>
            public LocalizedText Name { get; set; }

            /// <summary>
            /// 附魔描述
            /// </summary>
            public LocalizedText Description { get; set; }

            /// <summary>
            /// 附魔图标路径
            /// </summary>
            public string IconTexturePath { get; set; }

            /// <summary>
            /// 内部标识符（用于比较）
            /// </summary>
            internal int InternalId { get; set; }

            /// <summary>
            /// 是否是清除附魔
            /// </summary>
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

        /// <summary>
        /// 获取物品的有效附魔列表
        /// </summary>
        public static List<EnchantmentWrapper> GetValidEnchantmentsForItem(Item item) {
            if (!Has || item == null || item.IsAir)
                return new List<EnchantmentWrapper>();
            return GetValidEnchantmentsForItemInner(item);
        }
        [CWRJITEnabled]
        private static List<EnchantmentWrapper> GetValidEnchantmentsForItemInner(Item item) {
            var result = new List<EnchantmentWrapper>();
            var enchantments = CalamityMod.UI.CalamitasEnchants.EnchantmentManager.GetValidEnchantmentsForItem(item);

            int id = 0;
            foreach (var enchantment in enchantments) {
                result.Add(new EnchantmentWrapper {
                    Name = enchantment.Name,
                    Description = enchantment.Description,
                    IconTexturePath = enchantment.IconTexturePath,
                    InternalId = id++,
                    IsClearEnchantment = enchantment.Equals(CalamityMod.UI.CalamitasEnchants.EnchantmentManager.ClearEnchantment)
                });
            }

            return result;
        }

        /// <summary>
        /// 应用附魔到物品
        /// </summary>
        public static void ApplyEnchantmentToItem(Item item, EnchantmentWrapper wrapper, Action<Item> creationEffect = null) {
            if (!Has || item == null || item.IsAir)
                return;
            ApplyEnchantmentToItemInner(item, wrapper, creationEffect);
        }
        [CWRJITEnabled]
        private static void ApplyEnchantmentToItemInner(Item item, EnchantmentWrapper wrapper, Action<Item> creationEffect) {
            int oldPrefix = item.prefix;
            item.SetDefaults(item.type);
            item.Prefix(oldPrefix);

            if (wrapper.IsClearEnchantment) {
                item.Calamity().AppliedEnchantment = null;
                item.Prefix(oldPrefix);
            }
            else {
                //通过Name和Description重新匹配Enchantment
                var allEnchantments = CalamityMod.UI.CalamitasEnchants.EnchantmentManager.GetValidEnchantmentsForItem(item);
                CalamityMod.UI.CalamitasEnchants.Enchantment? targetEnchant = null;

                foreach (var ench in allEnchantments) {
                    if (ench.Name.Value == wrapper.Name.Value && ench.Description.Value == wrapper.Description.Value) {
                        targetEnchant = ench;
                        break;
                    }
                }

                if (targetEnchant.HasValue) {
                    item.Calamity().AppliedEnchantment = targetEnchant.Value;
                    creationEffect?.Invoke(item);
                    targetEnchant.Value.CreationEffect?.Invoke(item);

                    if (CalamityMod.UI.CalamitasEnchants.EnchantmentManager.ItemUpgradeRelationship.TryGetValue(item.type, out var newID)) {
                        item.SetDefaults(newID);
                        item.Prefix(oldPrefix);
                    }
                }
            }
        }
        #endregion

        #region 加载联动修改内容
        public static MethodBase BossHealthBarManager_Draw_Method;
        public static MethodBase calamityUtils_GetReworkedReforge_Method;
        internal delegate void On_DisplayLocalizedText_Dalegate(string key, Color? textColor = null);

        internal static void LoadComders() {
            Mod mod = CWRMod.Instance?.calamity;
            if (mod == null) {
                return;
            }
            try {
                //这一切不该发生，灾厄没有在这里留下任何可扩展的接口，如果想要那该死血条的为第三方事件靠边站，只能这么做，至少这是我目前能想到的方法
                Type bossHealthBarManagerType = mod.Code.GetType("CalamityMod.UI.BossHealthBarManager");
                BossHealthBarManager_Draw_Method = bossHealthBarManagerType?.GetMethod("Draw", BindingFlags.Instance | BindingFlags.Public);
                if (BossHealthBarManager_Draw_Method != null) {
                    VaultHook.Add(BossHealthBarManager_Draw_Method, On_BossHealthBarManager_Draw_Hook);
                }
                else {
                    CWRUtils.LogFailedLoad("BossHealthBarManager_Draw_Method", "CalamityMod.UI.BossHealthBarManager");
                }

                Type calUtilsType = mod.Code.GetType("CalamityMod.CalamityUtils");
                MethodInfo methodInfo = calUtilsType?.GetMethod("BroadcastLocalizedText", BindingFlags.Static | BindingFlags.Public);
                if (methodInfo != null) {
                    VaultHook.Add(methodInfo, OnDisplayLocalizedTextHook);
                }

                //我鸡巴的还能说什么？为什么这么多人喜欢改同一个东西？Fuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuck
                if (CWRMod.Instance.luminance != null) {
                    Type utType = CWRUtils.GetTargetTypeInStringKey(CWRUtils.GetModTypes(CWRMod.Instance.luminance), "Utilities");
                    methodInfo = utType?.GetMethod("BroadcastLocalizedText", BindingFlags.Static | BindingFlags.Public);
                    if (methodInfo != null) {
                        VaultHook.Add(methodInfo, OnDisplayLocalizedTextHook);
                    }
                }

                //OnProvideStealthStatBonusesHook 的签名携带 CalamityPlayer 类型，
                //一旦在此处通过 method group 转换得到 Delegate，JIT 必须解析 CalamityPlayer，
                //所以把这一段挪到独立的 [CWRJITEnabled] 方法里，确保 Calamity 未安装时整个 LoadComders 仍可被 JIT
                HookProvideStealthStatBonuses(mod);
            } catch { }
        }

        [CWRJITEnabled]
        private static void HookProvideStealthStatBonuses(Mod mod) {
            Type calPlayerType = calPlayerTemplate?.GetType() ?? mod.Code.GetType("CalamityMod.CalPlayer.CalamityPlayer");
            MethodInfo provideStealthMethod = calPlayerType?.GetMethod("ProvideStealthStatBonuses", BindingFlags.Instance | BindingFlags.NonPublic);
            if (provideStealthMethod != null) {
                VaultHook.Add(provideStealthMethod, OnProvideStealthStatBonusesHook);
            }
        }

        [CWRJITEnabled]
        private static void On_BossHealthBarManager_Draw_Hook(On_BossHealthBarManager_Draw_Dalegate orig, object obj, SpriteBatch spriteBatch, IBigProgressBar currentBar, BigProgressBarInfo info) {
            int startHeight = 100;
            int x = Main.screenWidth - 420;
            int y = Main.screenHeight - startHeight;
            if (Main.playerInventory || VaultUtils.IsInvasion()) {
                x -= 250;
            }
            Vector2 modifyPos = MuraChargeUI.Instance.ModifyBossHealthBarManagerPositon(x, y);
            x = (int)modifyPos.X;
            y = (int)modifyPos.Y;
            //谢天谢地BossHealthBarManager.Bars和BossHealthBarManager.BossHPUI是公开的
            foreach (BossHealthBarManager.BossHPUI ui in BossHealthBarManager.Bars) {
                ui.Draw(spriteBatch, x, y);
                y -= BossHealthBarManager.BossHPUI.VerticalOffsetPerBar;
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

        [CWRJITEnabled]
        private static void OnProvideStealthStatBonusesHook(Action<CalamityPlayer> orig, CalamityPlayer calamityPlayer) {
            if (calamityPlayer.Player.CWR().IsUnsunghero) {
                if (!calamityPlayer.wearingRogueArmor || calamityPlayer.rogueStealthMax <= 0) {
                    return;
                }

                Item item = calamityPlayer.Player.GetItem();
                int realUseTime = Math.Max(item.useTime, item.useAnimation);
                double useTimeFactor = 0.75 + 0.75 * Math.Log(realUseTime + 2D, 4D);
                //直接使用固定的基础时间，固定为 4 秒
                double stealthGenFactor = Math.Max(Math.Pow(4f, 2D / 3D), 1.5);

                double stealthAddedDamage = calamityPlayer.rogueStealth * BalancingConstants.UniversalStealthStrikeDamageFactor * useTimeFactor * stealthGenFactor;
                calamityPlayer.stealthDamage += (float)stealthAddedDamage;

                calamityPlayer.Player.aggro -= (int)(calamityPlayer.rogueStealth * 300f);

                return;
            }

            orig.Invoke(calamityPlayer);
        }
        #endregion
    }
}