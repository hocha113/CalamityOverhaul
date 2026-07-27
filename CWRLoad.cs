using CalamityOverhaul.Content;
using CalamityOverhaul.Content.Projectiles;
using System.Collections.Generic;
using System.Reflection;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul
{
    public static class CWRLoad
    {
        #region Boss/蠕虫体节列表
        /// <summary>飞眼怪</summary>
        public static List<int> Creeper;
        /// <summary>灾坟虫</summary>
        public static List<int> SepulcherSegments;
        /// <summary>风暴编织者</summary>
        public static List<int> StormWeaverSegments;
        /// <summary>幻海妖龙</summary>
        public static List<int> PrimordialWyrmSegments;
        /// <summary>血肉蠕虫（大）</summary>
        public static List<int> PerforatorLargeSegments;
        /// <summary>血肉蠕虫（中）</summary>
        public static List<int> PerforatorMediumSegments;
        /// <summary>装甲掘地虫</summary>
        public static List<int> ArmoredDiggerSegments;
        /// <summary>星流巨械（全部件）</summary>
        public static List<int> ExoMechSegments;
        /// <summary>星流巨械（Ares部件）</summary>
        public static List<int> ExoMechAresSegments;
        /// <summary>神明吞噬者</summary>
        public static List<int> DevourerofGodsSegments;
        /// <summary>荒漠灾虫</summary>
        public static List<int> DesertScourgeSegments;
        /// <summary>星神游龙</summary>
        public static List<int> AstrumDeusSegments;
        /// <summary>渊海灾虫</summary>
        public static List<int> AquaticScourgeSegments;
        /// <summary>幻海妖龙幼年体</summary>
        public static List<int> EidolonWyrmSegments;
        /// <summary>月球领主</summary>
        public static List<int> MoonLordSegments;
        /// <summary>世界吞噬者</summary>
        public static List<int> EaterofWorldsSegments;
        /// <summary>毁灭者</summary>
        public static List<int> DestroyerSegments;
        /// <summary>毁灭魔像</summary>
        public static List<int> RavagerSegments;
        /// <summary>血肉蠕虫（小）</summary>
        public static List<int> PerforatorSmallSegments;
        /// <summary>石巨人(无 realLife，靠类型表)</summary>
        public static List<int> GolemSegments;
        /// <summary>骷髅王（头+双手）</summary>
        public static List<int> SkeletronSegments;
        /// <summary>机械骷髅王（头+四臂）</summary>
        public static List<int> SkeletronPrimeSegments;
        /// <summary>全部体节表</summary>
        public static List<List<int>> AllBossSegmentLists { get; private set; }
        /// <summary>蠕虫体节</summary>
        public static int[] WormBodys { get; private set; }
        #endregion

        #region 物品属性映射
        /// <summary>堆叠上限 64</summary>
        public static int[] AddMaxStackItemsIn64 { get; private set; } = [];
        /// <summary>物块→掉落</summary>
        public static Dictionary<int, int> TileToItem { get; private set; } = [];
        /// <summary>墙→掉落</summary>
        public static Dictionary<int, int> WallToItem { get; private set; } = [];
        internal static Dictionary<int, bool> ItemIsGun { get; private set; } = [];
        internal static Dictionary<int, bool> ItemIsShotgun { get; private set; } = [];
        internal static Dictionary<int, bool> ItemIsBow { get; private set; } = [];
        internal static Dictionary<int, bool> ItemIsCrossBow { get; private set; } = [];
        internal static Dictionary<int, bool> ItemIsRanged { get; private set; } = [];
        internal static Dictionary<int, bool> ItemIsRangedAndCanRightClickFire { get; private set; } = [];
        internal static Dictionary<int, int> ItemIsBowAndArrowNum { get; private set; } = [];
        internal static Dictionary<int, bool> ItemIsGunAndMustConsumeAmmunition { get; private set; } = [];
        #endregion

        #region NPC/弹幕属性
        public static class NPCValue
        {
            /// <summary>免疫冻结</summary>
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

            /// <summary>金属材质(HitSound/黑名单)</summary>
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
            /// <summary>免疫冻结</summary>
            public readonly static Dictionary<int, bool> ImmuneFrozen = [];
        }
        #endregion

        #region Setup
        public static void Setup() {
            SetupBossSegmentLists();
            SetupStaticData();
            SetupItemData();
            SetupNPCData();
            SetupProjectileData();
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
            GolemSegments = [NPCID.Golem, NPCID.GolemHead, NPCID.GolemHeadFree, NPCID.GolemFistLeft, NPCID.GolemFistRight];
            SkeletronSegments = [NPCID.SkeletronHead, NPCID.SkeletronHand];
            SkeletronPrimeSegments = [NPCID.SkeletronPrime, NPCID.PrimeCannon, NPCID.PrimeSaw, NPCID.PrimeVice, NPCID.PrimeLaser];

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
                RavagerSegments,
                PerforatorSmallSegments,
                GolemSegments,
                SkeletronSegments,
                SkeletronPrimeSegments,
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

                if (item == null || item.type == ItemID.None) {
                    continue;
                }

                if (item.createTile != -1 && !TileToItem.ContainsKey(item.createTile)) {
                    TileToItem.Add(item.createTile, item.type);
                }
                if (item.createWall != -1 && !WallToItem.ContainsKey(item.createWall)) {
                    WallToItem.Add(item.createWall, item.type);
                }
            }

            PopulateHeldGunData();
        }

        /// <summary>无 heldProjType 时扫 BaseHeldGun 按 TargetID 登记</summary>
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

        /// <summary>修正异常弹药 shoot</summary>
        public static void SetAmmoItem(Item ammoItem) {
            if (_ammoShootOverrides.TryGetValue(ammoItem.type, out int shootType)) {
                ammoItem.shoot = shootType;
            }
        }
        #endregion
    }
}
