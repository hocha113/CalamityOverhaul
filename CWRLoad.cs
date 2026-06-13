using CalamityOverhaul.Content;
using CalamityOverhaul.Content.RangedModify.Core;
using System.Collections.Generic;
using System.Reflection;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul
{
    public static class CWRLoad
    {
        public static bool OnLoadContentBool;

        #region 跨Mod物品ID
        public static int EternitySoul;
        public static int DevisCurse;
        public static int DeviatingEnergy;
        public static int AbomEnergy;
        public static int EternalEnergy;
        public static int MetanovaBar;
        #endregion

        #region Boss/蠕虫体节列表
        /// <summary>
        /// 灾坟虫
        /// </summary>
        public static List<int> SepulcherSegments;
        /// <summary>
        /// 风暴编织者
        /// </summary>
        public static List<int> StormWeaverSegments;
        /// <summary>
        /// 幻海妖龙
        /// </summary>
        public static List<int> PrimordialWyrmSegments;
        /// <summary>
        /// 血肉蠕虫（大）
        /// </summary>
        public static List<int> PerforatorLargeSegments;
        /// <summary>
        /// 血肉蠕虫（中）
        /// </summary>
        public static List<int> PerforatorMediumSegments;
        /// <summary>
        /// 装甲掘地虫
        /// </summary>
        public static List<int> ArmoredDiggerSegments;
        /// <summary>
        /// 星流巨械（全部件）
        /// </summary>
        public static List<int> ExoMechSegments;
        /// <summary>
        /// 星流巨械（Ares部件）
        /// </summary>
        public static List<int> ExoMechAresSegments;
        /// <summary>
        /// 神明吞噬者
        /// </summary>
        public static List<int> DevourerofGodsSegments;
        /// <summary>
        /// 荒漠灾虫
        /// </summary>
        public static List<int> DesertScourgeSegments;
        /// <summary>
        /// 星神游龙
        /// </summary>
        public static List<int> AstrumDeusSegments;
        /// <summary>
        /// 渊海灾虫
        /// </summary>
        public static List<int> AquaticScourgeSegments;
        /// <summary>
        /// 幻海妖龙幼年体
        /// </summary>
        public static List<int> EidolonWyrmSegments;
        /// <summary>
        /// 月球领主
        /// </summary>
        public static List<int> MoonLordSegments;
        /// <summary>
        /// 世界吞噬者
        /// </summary>
        public static List<int> EaterofWorldsSegments;
        /// <summary>
        /// 毁灭者
        /// </summary>
        public static List<int> DestroyerSegments;
        /// <summary>
        /// 毁灭魔像
        /// </summary>
        public static List<int> RavagerSegments;
        /// <summary>
        /// 血肉蠕虫（小）
        /// </summary>
        public static List<int> PerforatorSmallSegments;
        /// <summary>
        /// 所有Boss/蠕虫体节列表的集合，用于批量检查
        /// </summary>
        public static List<List<int>> AllBossSegmentLists { get; private set; }
        /// <summary>
        /// 蠕虫类体节ID集合
        /// </summary>
        public static int[] WormBodys { get; private set; }
        #endregion

        #region 物品属性映射
        /// <summary>
        /// 关于哪些物品应该被设置为64的最大堆叠数
        /// </summary>
        public static int[] AddMaxStackItemsIn64 { get; private set; } = [];
        /// <summary>
        /// 物块对应掉落物的词典
        /// </summary>
        public static Dictionary<int, int> TileToItem { get; private set; } = [];
        /// <summary>
        /// 墙体对应掉落物的词典
        /// </summary>
        public static Dictionary<int, int> WallToItem { get; private set; } = [];
        /// <summary>
        /// 该物品是否是一把枪械
        /// </summary>
        internal static Dictionary<int, bool> ItemIsGun { get; private set; } = [];
        /// <summary>
        /// 该物品是否是一把霰弹枪
        /// </summary>
        internal static Dictionary<int, bool> ItemIsShotgun { get; private set; } = [];
        /// <summary>
        /// 该物品是否是一个弓
        /// </summary>
        internal static Dictionary<int, bool> ItemIsBow { get; private set; } = [];
        /// <summary>
        /// 该物品是否是一个十字弩
        /// </summary>
        internal static Dictionary<int, bool> ItemIsCrossBow { get; private set; } = [];
        /// <summary>
        /// 该物品是否是一个基本的远程类
        /// </summary>
        internal static Dictionary<int, bool> ItemIsRanged { get; private set; } = [];
        /// <summary>
        /// 该物品是否是一个基本的远程类，并且可以右键开火使用
        /// </summary>
        internal static Dictionary<int, bool> ItemIsRangedAndCanRightClickFire { get; private set; } = [];
        /// <summary>
        /// 获取一个弓类的箭族数量
        /// </summary>
        internal static Dictionary<int, int> ItemIsBowAndArrowNum { get; private set; } = [];
        /// <summary>
        /// 该枪械是否必定消耗弹药
        /// </summary>
        internal static Dictionary<int, bool> ItemIsGunAndMustConsumeAmmunition { get; private set; } = [];
        /// <summary>
        /// 从物品id映射到对应的终焉合成内容上，如果该物品没有终焉合成则返回<see langword="null"/>
        /// </summary>
        internal static Dictionary<int, string[]> ItemIDToOmigaSnyContent { get; private set; } = [];
        /// <summary>
        /// 该物品是否自动装填终焉合成内容
        /// </summary>
        internal static Dictionary<int, bool> ItemAutoloadingOmigaSnyRecipe { get; private set; } = [];
        #endregion

        #region NPC/弹幕属性
        public static class NPCValue
        {
            /// <summary>
            /// 是否免疫冻结
            /// </summary>
            public readonly static Dictionary<int, bool> ImmuneFrozen = [];

            private static readonly HashSet<int> _nonSteelBossTypes = [
                CWRID.NPC_Providence,
                CWRID.NPC_ScornEater,
                CWRID.NPC_Yharon,
                CWRID.NPC_DevourerofGodsHead,
            ];

            private static readonly HashSet<Terraria.Audio.SoundStyle?> _steelHitSounds = [
                SoundID.NPCHit4, SoundID.NPCHit41, SoundID.NPCHit2,
                SoundID.NPCHit5, SoundID.NPCHit11, SoundID.NPCHit30,
                SoundID.NPCHit34, SoundID.NPCHit36, SoundID.NPCHit42,
                SoundID.NPCHit49, SoundID.NPCHit52, SoundID.NPCHit53,
                SoundID.NPCHit54,
            ];

            /// <summary>
            /// 判断NPC是否为金属材质（根据受击音效和特定Boss类型判断）
            /// </summary>
            public static bool ISTheofSteel(NPC npc) {
                if (_nonSteelBossTypes.Contains(npc.type)) {
                    return false;
                }
                if (npc.HitSound == null || !_steelHitSounds.Contains(npc.HitSound)) {
                    return false;
                }
                return true;
            }
        }

        public static class ProjValue
        {
            /// <summary>
            /// 是否免疫冻结
            /// </summary>
            public readonly static Dictionary<int, bool> ImmuneFrozen = [];
        }
        #endregion

        #region Setup
        public static void Setup() {
            SetupBossSegmentLists();
            SetupStaticData();
            SetupCrossModItems();
            SetupItemData();
            SetupNPCData();
            SetupProjectileData();
            OnLoadContentBool = true;
        }

        private static void SetupBossSegmentLists() {
            SepulcherSegments = [CWRID.NPC_SepulcherHead, CWRID.NPC_SepulcherBody, CWRID.NPC_SepulcherTail];
            StormWeaverSegments = [CWRID.NPC_StormWeaverHead, CWRID.NPC_StormWeaverBody, CWRID.NPC_StormWeaverTail];
            PrimordialWyrmSegments = [CWRID.NPC_PrimordialWyrmHead, CWRID.NPC_PrimordialWyrmBody, CWRID.NPC_PrimordialWyrmTail];
            PerforatorLargeSegments = [CWRID.NPC_PerforatorHeadLarge, CWRID.NPC_PerforatorBodyLarge, CWRID.NPC_PerforatorTailLarge];
            PerforatorMediumSegments = [CWRID.NPC_PerforatorHeadMedium, CWRID.NPC_PerforatorBodyMedium, CWRID.NPC_PerforatorTailMedium];
            ArmoredDiggerSegments = [];
            ExoMechSegments = [CWRID.NPC_Apollo, CWRID.NPC_Artemis, CWRID.NPC_AresBody, CWRID.NPC_ThanatosHead, CWRID.NPC_ThanatosBody1, CWRID.NPC_ThanatosBody2, CWRID.NPC_ThanatosTail];
            ExoMechAresSegments = [CWRID.NPC_AresBody, CWRID.NPC_AresLaserCannon, CWRID.NPC_AresPlasmaFlamethrower, CWRID.NPC_AresTeslaCannon, CWRID.NPC_AresGaussNuke];
            DevourerofGodsSegments = [CWRID.NPC_DevourerofGodsHead, CWRID.NPC_DevourerofGodsBody, CWRID.NPC_DevourerofGodsTail];
            DesertScourgeSegments = [CWRID.NPC_DesertScourgeHead, CWRID.NPC_DesertScourgeBody, CWRID.NPC_DesertScourgeTail, CWRID.NPC_DesertNuisanceHead, CWRID.NPC_DesertNuisanceBody, CWRID.NPC_DesertNuisanceTail];
            AstrumDeusSegments = [CWRID.NPC_AstrumDeusHead, CWRID.NPC_AstrumDeusBody, CWRID.NPC_AstrumDeusTail];
            AquaticScourgeSegments = [CWRID.NPC_AquaticScourgeHead, CWRID.NPC_AquaticScourgeBody, CWRID.NPC_AquaticScourgeTail];
            EidolonWyrmSegments = [CWRID.NPC_EidolonWyrmHead, CWRID.NPC_EidolonWyrmBody, CWRID.NPC_EidolonWyrmBodyAlt, CWRID.NPC_EidolonWyrmTail];
            MoonLordSegments = [NPCID.MoonLordFreeEye, NPCID.MoonLordCore, NPCID.MoonLordHand, NPCID.MoonLordHead, NPCID.MoonLordLeechBlob];
            EaterofWorldsSegments = [NPCID.EaterofWorldsHead, NPCID.EaterofWorldsBody, NPCID.EaterofWorldsTail];
            DestroyerSegments = [NPCID.TheDestroyer, NPCID.TheDestroyerBody, NPCID.TheDestroyerTail];
            RavagerSegments = [CWRID.NPC_RavagerBody, CWRID.NPC_RavagerClawLeft, CWRID.NPC_RavagerClawRight, CWRID.NPC_RavagerHead, CWRID.NPC_RavagerLegLeft, CWRID.NPC_RavagerLegRight];
            PerforatorSmallSegments = [CWRID.NPC_PerforatorHeadSmall, CWRID.NPC_PerforatorBodySmall, CWRID.NPC_PerforatorTailSmall];

            AllBossSegmentLists = [
                SepulcherSegments,
                StormWeaverSegments,
                PrimordialWyrmSegments,
                PerforatorLargeSegments,
                PerforatorMediumSegments,
                ArmoredDiggerSegments,
                ExoMechSegments,
                DevourerofGodsSegments,
                DesertScourgeSegments,
                AstrumDeusSegments,
                AquaticScourgeSegments,
                EidolonWyrmSegments,
                MoonLordSegments,
                EaterofWorldsSegments,
                DestroyerSegments,
            ];

            WormBodys = [
                CWRID.NPC_AquaticScourgeBody, CWRID.NPC_StormWeaverBody,
                CWRID.NPC_DesertScourgeBody, CWRID.NPC_DesertNuisanceBody,
                CWRID.NPC_DesertNuisanceBodyYoung, CWRID.NPC_PrimordialWyrmBody,
                CWRID.NPC_ThanatosBody1, CWRID.NPC_ThanatosBody2,
                CWRID.NPC_DevourerofGodsBody, CWRID.NPC_AstrumDeusBody,
                CWRID.NPC_SepulcherBody, CWRID.NPC_PerforatorBodyLarge,
                CWRID.NPC_PerforatorBodyMedium, CWRID.NPC_PerforatorBodySmall,
                NPCID.TheDestroyerBody, NPCID.EaterofWorldsBody,
            ];
        }

        private static void SetupStaticData() {
            AddMaxStackItemsIn64 = [
                CWRID.Item_Rock,
                CWRID.Item_BloodOrange,
                CWRID.Item_MiracleFruit,
                CWRID.Item_Elderberry,
                CWRID.Item_Dragonfruit,
                CWRID.Item_LoreCynosure,
                ItemID.BloodMoonStarter,
            ];
        }

        private static void SetupCrossModItems() {
            if (CWRMod.Instance.fargowiltasSouls != null) {
                EternitySoul = CWRMod.Instance.fargowiltasSouls.Find<ModItem>("EternitySoul").Type;
                DevisCurse = CWRMod.Instance.fargowiltasSouls.Find<ModItem>("DevisCurse").Type;
                DeviatingEnergy = CWRMod.Instance.fargowiltasSouls.Find<ModItem>("DeviatingEnergy").Type;
                AbomEnergy = CWRMod.Instance.fargowiltasSouls.Find<ModItem>("AbomEnergy").Type;
                EternalEnergy = CWRMod.Instance.fargowiltasSouls.Find<ModItem>("EternalEnergy").Type;
            }
            if (CWRMod.Instance.catalystMod != null) {
                MetanovaBar = CWRMod.Instance.catalystMod.Find<ModItem>("MetanovaBar").Type;
            }
        }

        private static void SetupItemData() {
            for (int itemType = 0; itemType < ItemLoader.ItemCount; itemType++) {
                Item item = ContentSamples.ItemsByType[itemType];
                ItemIsGun[itemType] = false;
                ItemIsShotgun[itemType] = false;
                ItemIsCrossBow[itemType] = false;
                ItemIsGunAndMustConsumeAmmunition[itemType] = false;
                ItemIsBow[itemType] = false;
                ItemIsBowAndArrowNum[itemType] = 1;
                ItemIsRanged[itemType] = false;
                ItemIsRangedAndCanRightClickFire[itemType] = false;
                ItemIDToOmigaSnyContent[itemType] = null;
                ItemAutoloadingOmigaSnyRecipe[itemType] = true;

                if (item == null || item.type == ItemID.None) {
                    continue;
                }

                if (item.createTile != -1 && !TileToItem.ContainsKey(item.createTile)) {
                    TileToItem.Add(item.createTile, item.type);
                }
                if (item.createWall != -1 && !WallToItem.ContainsKey(item.createWall)) {
                    WallToItem.Add(item.createWall, item.type);
                }

                CWRItem cwrItem = item.CWR();

                string[] snyOmig = cwrItem.OmigaSnyContent;
                if (snyOmig != null) {
                    ItemIDToOmigaSnyContent[itemType] = snyOmig;
                    ItemAutoloadingOmigaSnyRecipe[itemType] = cwrItem.AutoloadingOmigaSnyRecipe;
                }
            }

            PopulateHeldGunData();
        }

        /// <summary>
        /// 手持武器不使用 heldProjType 绑定，扫描 <see cref="BaseHeldGun"/> 子类按 TargetID 反向登记
        /// </summary>
        private static void PopulateHeldGunData() {
            foreach (BaseHeldGun heldGun in VaultUtils.GetDerivedInstances<BaseHeldGun>()) {
                int itemType = heldGun.TargetID;
                if (itemType <= ItemID.None || itemType >= ItemLoader.ItemCount) {
                    continue;
                }
                ItemIsGun[itemType] = true;
                ItemIsCrossBow[itemType] = heldGun.IsCrossbow;
                ItemIsGunAndMustConsumeAmmunition[itemType] = heldGun.MustConsumeAmmunition;
                ItemIsRanged[itemType] = true;
                ItemIsRangedAndCanRightClickFire[itemType] = heldGun.CanRightClick;
            }
        }

        private static void SetupNPCData() {
            for (int i = 0; i < NPCLoader.NPCCount; i++) {
                NPCValue.ImmuneFrozen.TryAdd(i, false);
            }
        }

        private static void SetupProjectileData() {
            HashSet<int> exemptSet = GetCalamityPierceExemptSet();

            for (int i = 0; i < ProjectileLoader.ProjectileCount; i++) {
                ProjValue.ImmuneFrozen.TryAdd(i, false);
                Projectile projectile = ContentSamples.ProjectilesByType[i];
                if (projectile != null && projectile.type != ProjectileID.None) {
                    CWRProjectile cwrProjectile = projectile.CWR();
                    if (exemptSet != null && cwrProjectile.PierceResist) {
                        exemptSet.Add(projectile.type);
                    }
                }
            }
        }

        private static HashSet<int> GetCalamityPierceExemptSet() {
            if (!ModLoader.TryGetMod("CalamityMod", out Mod calamity)) {
                return null;
            }

            var pierceResistNPCType = calamity.Code.GetType("CalamityMod.NPCs.PierceResistNPC");
            var field = pierceResistNPCType?.GetField("exemptProjectiles",
                BindingFlags.Static | BindingFlags.NonPublic);

            return field?.GetValue(null) as HashSet<int>;
        }
        #endregion

        #region UnLoad
        public static void UnLoad() {
            TileToItem?.Clear();
            WallToItem?.Clear();
            ItemIsGun?.Clear();
            ItemIsShotgun?.Clear();
            ItemIsBow?.Clear();
            ItemIsCrossBow?.Clear();
            ItemIsRanged?.Clear();
            ItemIsRangedAndCanRightClickFire?.Clear();
            ItemIsBowAndArrowNum?.Clear();
            ItemIsGunAndMustConsumeAmmunition?.Clear();
            ItemIDToOmigaSnyContent?.Clear();
            ItemAutoloadingOmigaSnyRecipe?.Clear();
            NPCValue.ImmuneFrozen?.Clear();
            ProjValue.ImmuneFrozen?.Clear();
            AllBossSegmentLists = null;
        }
        #endregion

        #region SetAmmoItem
        private static readonly Dictionary<int, int> _ammoShootOverrides = new() {
            [ItemID.FallenStar] = ProjectileID.StarCannonStar,
            [ItemID.RocketI] = ProjectileID.RocketI,
            [ItemID.RocketII] = ProjectileID.RocketII,
            [ItemID.RocketIII] = ProjectileID.RocketIII,
            [ItemID.RocketIV] = ProjectileID.RocketIV,
            [ItemID.ClusterRocketI] = ProjectileID.ClusterRocketI,
            [ItemID.ClusterRocketII] = ProjectileID.ClusterRocketII,
            [ItemID.DryRocket] = ProjectileID.DryRocket,
            [ItemID.WetRocket] = ProjectileID.WetRocket,
            [ItemID.HoneyRocket] = ProjectileID.HoneyRocket,
            [ItemID.LavaRocket] = ProjectileID.LavaRocket,
            [ItemID.MiniNukeI] = ProjectileID.MiniNukeRocketI,
            [ItemID.MiniNukeII] = ProjectileID.MiniNukeRocketII,
        };

        /// <summary>
        /// 修改一些原弹药设定异常的物品的shoot值
        /// </summary>
        public static void SetAmmoItem(Item ammoItem) {
            if (_ammoShootOverrides.TryGetValue(ammoItem.type, out int shootType)) {
                ammoItem.shoot = shootType;
            }
        }
        #endregion
    }
}
