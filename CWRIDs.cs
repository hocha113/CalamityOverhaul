using CalamityMod.Items;
using CalamityMod.Items.Materials;
using CalamityMod.NPCs.Abyss;
using CalamityMod.NPCs.AquaticScourge;
using CalamityMod.NPCs.AstrumDeus;
using CalamityMod.NPCs.DesertScourge;
using CalamityMod.NPCs.DraedonLabThings;
using CalamityMod.NPCs.ExoMechs.Apollo;
using CalamityMod.NPCs.ExoMechs.Ares;
using CalamityMod.NPCs.ExoMechs.Artemis;
using CalamityMod.NPCs.ExoMechs.Thanatos;
using CalamityMod.NPCs.HiveMind;
using CalamityMod.NPCs.Perforator;
using CalamityMod.NPCs.PlaguebringerGoliath;
using CalamityMod.NPCs.Polterghast;
using CalamityMod.NPCs.PrimordialWyrm;
using CalamityMod.NPCs.Ravager;
using CalamityMod.NPCs.StormWeaver;
using CalamityMod.NPCs.SupremeCalamitas;
using CalamityMod.NPCs.Yharon;
using CalamityOverhaul.Content;
using CalamityOverhaul.Content.Items.Materials;
using CalamityOverhaul.Content.Items.Melee;
using CalamityOverhaul.Content.Items.Placeable;
using CalamityOverhaul.Content.Items.Rogue.Extras;
using CalamityOverhaul.Content.Items.Tools;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.MurasamaProj;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeavenfallLongbowProj;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using static Terraria.ModLoader.ModContent;

namespace CalamityOverhaul
{
    public static class CWRIDs
    {
        public static bool OnLoadContentBool = true;

        public static int DarkMatterBall;
        public static int BlackMatterStick;
        public static int InfiniteStick;
        public static int EndlessStabilizer;
        public static int DubiousPlating;
        public static int FoodStallChair;
        public static int FoodStallChairTile;
        public static int Gangarus;
        public static int StarMyriadChanges;

        #region 法狗魔石
        public static int EternitySoul;
        public static int DevisCurse;
        public static int DeviatingEnergy;
        public static int AbomEnergy;
        public static int EternalEnergy;
        #endregion

        #region 灾劫
        public static int MetanovaBar;
        #endregion

        public static int InfiniteArrow;
        public static int InfiniteRune;
        public static int ParadiseArrow;
        public static int HeavenLightning;

        /// <summary>
        /// 无尽锭
        /// </summary>
        public static int[] MaterialsTypes;
        /// <summary>
        /// 湮宇星矢
        /// </summary>
        public static int[] MaterialsTypes2;
        /// <summary>
        /// 天堂陨落
        /// </summary>
        public static int[] MaterialsTypes3;
        /// <summary>
        /// 无尽镐
        /// </summary>
        public static int[] MaterialsTypes4;
        /// <summary>
        /// 鬼妖，CWR
        /// </summary>
        public static int MurasamaItem;
        /// <summary>
        /// 鬼妖，Calamity
        /// </summary>
        public static int MurasamaItem2;
        /// <summary>
        /// 升龙半月
        /// </summary>
        public static int MurasamaBreakSwing;

        /// <summary>
        /// 物块对应掉落物的词典
        /// </summary>
        public static Dictionary<int, int> TileToItem = new();
        /// <summary>
        /// 墙体对应掉落物的词典
        /// </summary>
        public static Dictionary<int, int> WallToItem = new();
        /// <summary>
        /// 物品对应射弹的词典
        /// </summary>
        public static Dictionary<int, int> ItemToShootID = new();
        public static Dictionary<int, List<int>> ShootToItemID = new();
        public static Dictionary<int, int> ItemToHeldProjID = new();
        public static Dictionary<int, Projectile> ItemToHeldProj = new();
        internal static Dictionary<int, BaseGun> ItemToBaseGun = new();
        internal static Dictionary<int, BaseBow> ItemToBaseBow = new();
        internal static Dictionary<int, BaseHeldRanged> ItemToBaseRanged = new();
        internal static Dictionary<int, int> OverProjID_To_Safe_Shoot_Ammo_Item_Target = new();
        /// <summary>
        /// 扫地机器人
        /// </summary>
        public static int Androomba;
        /// <summary>
        /// 瘟疫使者
        /// </summary>
        public static int PlaguebringerGoliath;
        /// <summary>
        /// 噬魂幽花
        /// </summary>
        public static int Polterghast;
        /// <summary>
        /// 丛林龙
        /// </summary>
        public static int Yharon;
        /// <summary>
        /// 灾坟虫
        /// </summary>
        public static List<int> targetNpcTypes;
        public static int SepulcherHead;
        public static int SepulcherBody;
        public static int SepulcherTail;
        /// <summary>
        /// 风暴吞噬者
        /// </summary>
        public static List<int> targetNpcTypes2;
        public static int StormWeaverHead;
        public static int StormWeaverBody;
        public static int StormWeaverTail;
        /// <summary>
        /// 幻海妖龙 
        /// </summary>
        public static List<int> targetNpcTypes3;
        public static int PrimordialWyrmHead;
        public static int PrimordialWyrmBody;
        public static int PrimordialWyrmTail;
        /// <summary>
        /// 血肉宿主
        /// </summary>
        public static int PerforatorHive;
        /// <summary>
        /// 腐巢意志
        /// </summary>
        public static int HiveMind;
        /// <summary>
        /// 血肉蠕虫 
        /// </summary>
        public static List<int> targetNpcTypes4;
        public static int PerforatorHeadLarge;
        public static int PerforatorBodyLarge;
        public static int PerforatorTailLarge;
        /// <summary>
        /// 血肉蠕虫2 
        /// </summary>
        public static List<int> targetNpcTypes5;
        public static int PerforatorHeadMedium;
        public static int PerforatorBodyMedium;
        public static int PerforatorTailMedium;
        /// <summary>
        /// 血肉蠕虫3
        /// </summary>
        public static List<int> targetNpcTypes18;
        public static int PerforatorHeadSmall;
        public static int PerforatorBodySmall;
        public static int PerforatorTailSmall;
        /// <summary>
        /// 装甲掘地虫 
        /// </summary>
        public static List<int> targetNpcTypes6;
        public static int ArmoredDiggerHead;
        public static int ArmoredDiggerBody;
        public static int ArmoredDiggerTail;
        /// <summary>
        /// 星流巨械
        /// </summary>
        public static List<int> targetNpcTypes7;
        public static List<int> targetNpcTypes7_1;
        public static int Apollo;
        public static int Artemis;
        public static int AresBody;
        public static int AresLaserCannon;
        public static int AresPlasmaFlamethrower;
        public static int AresTeslaCannon;
        public static int AresGaussNuke;
        public static int ThanatosHead;
        public static int ThanatosBody1;
        public static int ThanatosBody2;
        public static int ThanatosTail;
        /// <summary>
        /// 神明吞噬者
        /// </summary>
        public static List<int> targetNpcTypes8;
        public static int DevourerofGodsHead;
        public static int DevourerofGodsBody;
        public static int DevourerofGodsTail;
        public static int CosmicGuardianHead;
        public static int CosmicGuardianBody;
        public static int CosmicGuardianTail;
        /// <summary>
        /// 荒漠灾虫
        /// </summary>
        public static List<int> targetNpcTypes9;
        public static int DesertScourgeHead;
        public static int DesertScourgeBody;
        public static int DesertScourgeTail;
        public static int DesertNuisanceHead;
        public static int DesertNuisanceBody;
        public static int DesertNuisanceTail;
        /// <summary>
        /// 星神游龙
        /// </summary>
        public static List<int> targetNpcTypes10;
        public static int AstrumDeusHead;
        public static int AstrumDeusBody;
        public static int AstrumDeusTail;
        /// <summary>
        /// 渊海灾虫
        /// </summary>
        public static List<int> targetNpcTypes11;
        public static int AquaticScourgeHead;
        public static int AquaticScourgeBody;
        public static int AquaticScourgeTail;
        /// <summary>
        /// 幻海妖龙幼年体
        /// </summary>
        public static List<int> targetNpcTypes12;
        public static int EidolonWyrmHead;
        public static int EidolonWyrmBody;
        public static int EidolonWyrmBodyAlt;
        public static int EidolonWyrmTail;

        /// <summary>
        /// 月球领主
        /// </summary>
        public static List<int> targetNpcTypes13;
        /// <summary>
        /// 世界吞噬者
        /// </summary>
        public static List<int> targetNpcTypes14;
        /// <summary>
        /// 毁灭者
        /// </summary>
        public static List<int> targetNpcTypes15;

        /// <summary>
        /// 天堂吞噬者
        /// </summary>
        //public static List<int> targetNpcTypes16;
        //public static int HEHead;
        //public static int HEBody;
        //public static int HEBodyAlt;
        //public static int HETail;

        /// <summary>
        /// 毁灭魔像
        /// </summary>
        public static List<int> targetNpcTypes17;
        public static int RavagerBody;
        public static int RavagerClawLeft;
        public static int RavagerClawRight;
        public static int RavagerHead;
        public static int RavagerLegLeft;
        public static int RavagerLegRight;
        /// <summary>
        /// 蠕虫类体节
        /// </summary>
        public static int[] WormBodys;

        public static void Load() {
            InfiniteArrow = ProjectileType<InfiniteArrow>();
            InfiniteRune = ProjectileType<InfiniteArrow>();
            ParadiseArrow = ProjectileType<InfiniteArrow>();
            HeavenLightning = ProjectileType<InfiniteArrow>();

            DubiousPlating = ItemType<DubiousPlating>();
            DarkMatterBall = ItemType<DarkMatterBall>();
            EndlessStabilizer = ItemType<EndlessStabilizer>();
            InfiniteStick = ItemType<InfiniteStick>();
            BlackMatterStick = ItemType<BlackMatterStick>();
            FoodStallChair = ItemType<FoodStallChair>();
            FoodStallChairTile = TileType<Content.Tiles.FoodStallChair>();
            Gangarus = ItemType<Gangarus>();
            StarMyriadChanges = ItemType<StarMyriadChanges>();

            MurasamaItem = ItemType<MurasamaEcType>();
            MurasamaItem2 = ItemType<CalamityMod.Items.Weapons.Melee.Murasama>();
            MurasamaBreakSwing = ProjectileType<MurasamaBreakSwing>();

            Androomba = NPCType<Androomba>();
            Polterghast = NPCType<Polterghast>();
            Yharon = NPCType<Yharon>();
            PlaguebringerGoliath = NPCType<PlaguebringerGoliath>();

            PerforatorHive = NPCType<PerforatorHive>();
            HiveMind = NPCType<HiveMind>();

            SepulcherHead = NPCType<SepulcherHead>();
            SepulcherBody = NPCType<SepulcherBody>();
            SepulcherTail = NPCType<SepulcherTail>();

            StormWeaverHead = NPCType<StormWeaverHead>();
            StormWeaverBody = NPCType<StormWeaverBody>();
            StormWeaverTail = NPCType<StormWeaverTail>();

            PrimordialWyrmHead = NPCType<PrimordialWyrmHead>();
            PrimordialWyrmBody = NPCType<PrimordialWyrmBody>();
            PrimordialWyrmTail = NPCType<PrimordialWyrmTail>();

            PerforatorHeadLarge = NPCType<PerforatorHeadLarge>();
            PerforatorBodyLarge = NPCType<PerforatorBodyLarge>();
            PerforatorTailLarge = NPCType<PerforatorTailLarge>();

            PerforatorHeadMedium = NPCType<PerforatorHeadMedium>();
            PerforatorBodyMedium = NPCType<PerforatorBodyMedium>();
            PerforatorTailMedium = NPCType<PerforatorTailMedium>();

            ArmoredDiggerHead = NPCType<CalamityMod.NPCs.NormalNPCs.ArmoredDiggerHead>();
            ArmoredDiggerBody = NPCType<CalamityMod.NPCs.NormalNPCs.ArmoredDiggerBody>();
            ArmoredDiggerTail = NPCType<CalamityMod.NPCs.NormalNPCs.ArmoredDiggerTail>();

            Apollo = NPCType<Apollo>();
            Artemis = NPCType<Artemis>();
            AresBody = NPCType<AresBody>();
            ThanatosHead = NPCType<ThanatosHead>();
            ThanatosBody1 = NPCType<ThanatosBody1>();
            ThanatosBody2 = NPCType<ThanatosBody2>();
            ThanatosTail = NPCType<ThanatosTail>();

            DevourerofGodsHead = NPCType<CalamityMod.NPCs.DevourerofGods.DevourerofGodsHead>();
            DevourerofGodsBody = NPCType<CalamityMod.NPCs.DevourerofGods.DevourerofGodsBody>();
            DevourerofGodsTail = NPCType<CalamityMod.NPCs.DevourerofGods.DevourerofGodsTail>();
            CosmicGuardianHead = NPCType<CalamityMod.NPCs.DevourerofGods.CosmicGuardianHead>();
            CosmicGuardianBody = NPCType<CalamityMod.NPCs.DevourerofGods.CosmicGuardianBody>();
            CosmicGuardianTail = NPCType<CalamityMod.NPCs.DevourerofGods.CosmicGuardianTail>();

            DesertScourgeHead = NPCType<DesertScourgeHead>();
            DesertScourgeBody = NPCType<DesertScourgeBody>();
            DesertScourgeTail = NPCType<DesertScourgeTail>();
            DesertNuisanceHead = NPCType<DesertNuisanceHead>();
            DesertNuisanceBody = NPCType<DesertNuisanceBody>();
            DesertNuisanceTail = NPCType<DesertNuisanceTail>();

            AstrumDeusHead = NPCType<AstrumDeusHead>();
            AstrumDeusBody = NPCType<AstrumDeusBody>();
            AstrumDeusTail = NPCType<AstrumDeusTail>();

            AquaticScourgeHead = NPCType<AquaticScourgeHead>();
            AquaticScourgeBody = NPCType<AquaticScourgeBody>();
            AquaticScourgeTail = NPCType<AquaticScourgeTail>();

            EidolonWyrmHead = NPCType<EidolonWyrmHead>();
            EidolonWyrmBody = NPCType<EidolonWyrmBody>();
            EidolonWyrmBodyAlt = NPCType<EidolonWyrmBodyAlt>();
            EidolonWyrmTail = NPCType<EidolonWyrmTail>();

            RavagerBody = NPCType<RavagerBody>();
            RavagerClawLeft = NPCType<RavagerClawLeft>();
            RavagerClawRight = NPCType<RavagerClawRight>();
            RavagerHead = NPCType<RavagerHead>();
            RavagerLegLeft = NPCType<RavagerLegLeft>();
            RavagerLegRight = NPCType<RavagerLegRight>();

            AresLaserCannon = NPCType<AresLaserCannon>();
            AresPlasmaFlamethrower = NPCType<AresPlasmaFlamethrower>();
            AresTeslaCannon = NPCType<AresTeslaCannon>();
            AresGaussNuke = NPCType<AresGaussNuke>();

            targetNpcTypes = new List<int> { SepulcherHead, SepulcherBody, SepulcherTail };
            targetNpcTypes2 = new List<int> { StormWeaverHead, StormWeaverBody, StormWeaverTail };
            targetNpcTypes3 = new List<int> { PrimordialWyrmHead, PrimordialWyrmBody, PrimordialWyrmTail };
            targetNpcTypes4 = new List<int> { PerforatorHeadLarge, PerforatorBodyLarge, PerforatorTailLarge };
            targetNpcTypes5 = new List<int> { PerforatorHeadMedium, PerforatorBodyMedium, PerforatorTailMedium };
            targetNpcTypes6 = new List<int> { ArmoredDiggerHead, ArmoredDiggerBody, ArmoredDiggerTail };
            targetNpcTypes7 = new List<int> { Apollo, Artemis, AresBody, ThanatosHead, ThanatosBody1, ThanatosBody2, ThanatosTail };
            targetNpcTypes7_1 = new List<int> { AresBody, AresLaserCannon, AresPlasmaFlamethrower, AresTeslaCannon, AresGaussNuke };
            targetNpcTypes8 = new List<int> { DevourerofGodsHead, DevourerofGodsBody, DevourerofGodsTail, CosmicGuardianHead, CosmicGuardianBody, CosmicGuardianTail };
            targetNpcTypes9 = new List<int> { DesertScourgeHead, DesertScourgeBody, DesertScourgeTail, DesertNuisanceHead, DesertNuisanceBody, DesertNuisanceTail };
            targetNpcTypes10 = new List<int> { AstrumDeusHead, AstrumDeusBody, AstrumDeusTail };
            targetNpcTypes11 = new List<int> { AquaticScourgeHead, AquaticScourgeBody, AquaticScourgeTail };
            targetNpcTypes12 = new List<int> { EidolonWyrmHead, EidolonWyrmBody, EidolonWyrmBodyAlt, EidolonWyrmTail };
            targetNpcTypes13 = new List<int> { NPCID.MoonLordFreeEye, NPCID.MoonLordCore, NPCID.MoonLordHand, NPCID.MoonLordHead, NPCID.MoonLordLeechBlob };
            targetNpcTypes14 = new List<int> { NPCID.EaterofWorldsHead, NPCID.EaterofWorldsBody, NPCID.EaterofWorldsTail };
            targetNpcTypes15 = new List<int> { NPCID.TheDestroyer, NPCID.TheDestroyerBody, NPCID.TheDestroyerTail };
            targetNpcTypes17 = new List<int> { RavagerBody, RavagerClawLeft, RavagerClawRight, RavagerHead, RavagerLegLeft, RavagerLegRight };
            WormBodys = new int[] { AquaticScourgeBody, ArmoredDiggerBody, StormWeaverBody, ArmoredDiggerBody
                , CosmicGuardianBody, PrimordialWyrmBody, ThanatosBody1, ThanatosBody2, DevourerofGodsBody, AstrumDeusBody
                , AquaticScourgeBody, NPCID.TheDestroyerBody};

            MaterialsTypes = new int[]{
                ItemType<AerialiteBar>(),//水华锭
                ItemType<AuricBar>(),//圣金源锭
                ItemType<ShadowspecBar>(),//影魔锭
                ItemType<AstralBar>(),//彗星锭
                ItemType<CosmiliteBar>(),//宇宙锭
                ItemType<CryonicBar>(),//极寒锭
                ItemType<PerennialBar>(),//龙篙锭
                ItemType<ScoriaBar>(),//岩浆锭
                ItemType<MolluskHusk>(),//生物质
                ItemType<MurkyPaste>(),//泥浆杂草混合物质
                ItemType<DepthCells>(),//深渊生物组织
                ItemType<DivineGeode>(),//圣神晶石
                ItemType<DubiousPlating>(),//废弃装甲
                ItemType<EffulgentFeather>(),//闪耀金羽
                ItemType<ExoPrism>(),//星流棱晶
                ItemType<BloodstoneCore>(),//血石核心
                ItemType<CoreofCalamity>(),//灾劫精华
                ItemType<AscendantSpiritEssence>(),//化神精魄
                ItemType<AshesofCalamity>(),//灾厄尘
                ItemType<AshesofAnnihilation>(),//至尊灾厄精华
                ItemType<LifeAlloy>(),//生命合金
                ItemType<LivingShard>(),//生命物质
                ItemType<Lumenyl>(),//流明晶
                ItemType<MeldConstruct>(),//幻塔物质
                ItemType<MiracleMatter>(),//奇迹物质
                ItemType<Polterplasm>(),//幻魂
                ItemType<RuinousSoul>(),//幽花之魂
                ItemType<DarkPlasma>(),//暗物质
                ItemType<UnholyEssence>(),//灼火精华
                ItemType<TwistingNether>(),//扭曲虚空
                ItemType<ArmoredShell>(),//装甲心脏
                ItemType<YharonSoulFragment>(),//龙魂
                ItemType<Rock>()//古恒石
            };

            MaterialsTypes2 = new int[]{
                ItemType<CalamityMod.Items.Weapons.Ranged.Deathwind>(),
                ItemType<CalamityMod.Items.Weapons.Ranged.Alluvion>(),
                ItemType<CalamityMod.Items.Weapons.Magic.Apotheosis>(),
                ItemType<Rock>(),
                ItemType<CosmiliteBar>()
            };

            MaterialsTypes3 = new int[]{
                ItemType<CalamityMod.Items.Weapons.Ranged.Drataliornus>(),
                ItemType<CalamityMod.Items.Weapons.Ranged.HeavenlyGale>(),
                ItemType<CalamityMod.Items.Weapons.Magic.Eternity>(),
                ItemType<InfiniteIngot>()
            };

            MaterialsTypes4 = new int[]{
                ItemType<CalamityMod.Items.Tools.CrystylCrusher>(),
                ItemType<CalamityMod.Items.Tools.AbyssalWarhammer>(),
                ItemType<CalamityMod.Items.Tools.AerialHamaxe>(),
                ItemType<CalamityMod.Items.Tools.AstralHamaxe>(),
                ItemType<CalamityMod.Items.Tools.AstralPickaxe>(),
                ItemType<CalamityMod.Items.Tools.AxeofPurity>(),
                ItemType<CalamityMod.Items.Tools.BeastialPickaxe>(),
                ItemType<CalamityMod.Items.Tools.BerserkerWaraxe>(),
                ItemType<CalamityMod.Items.Tools.BlossomPickaxe>(),
                ItemType<CalamityMod.Items.Tools.FellerofEvergreens>(),
                ItemType<CalamityMod.Items.Tools.Gelpick>(),
                ItemType<CalamityMod.Items.Tools.GenesisPickaxe>(),
                ItemType<CalamityMod.Items.Tools.Grax>(),
                ItemType<CalamityMod.Items.Tools.GreatbayPickaxe>(),
                ItemType<CalamityMod.Items.Tools.InfernaCutter>(),
                ItemType<CalamityMod.Items.Tools.ReefclawHamaxe>(),
                ItemType<CalamityMod.Items.Tools.SeismicHampick>(),
                ItemType<CalamityMod.Items.Tools.ShardlightPickaxe>(),
                ItemType<CalamityMod.Items.Tools.SkyfringePickaxe>(),
                ItemType<CalamityMod.Items.Tools.TectonicTruncator>(),
                ItemType<InfiniteIngot>()
            };

            OverProjID_To_Safe_Shoot_Ammo_Item_Target = new Dictionary<int, int>() {
                { ProjectileID.BoneArrow, ItemID.BoneArrow},
                { ProjectileID.MoonlordArrow, ItemID.MoonlordArrow},
                { ProjectileID.ChlorophyteArrow, ItemID.ChlorophyteArrow},
                { ProjectileID.CursedArrow, ItemID.CursedArrow},
                { ProjectileID.FlamingArrow, ItemID.FlamingArrow},
                { ProjectileID.FrostburnArrow, ItemID.FrostburnArrow},
                { ProjectileID.HellfireArrow, ItemID.HellfireArrow},
                { ProjectileID.HolyArrow, ItemID.HolyArrow},
                { ProjectileID.IchorArrow, ItemID.IchorArrow},
                { ProjectileID.JestersArrow, ItemID.JestersArrow},
                { ProjectileID.ShimmerArrow, ItemID.ShimmerArrow},
                { ProjectileID.UnholyArrow, ItemID.UnholyArrow},
                { ProjectileID.VenomArrow, ItemID.VenomArrow},
                { ProjectileID.WoodenArrowFriendly, ItemID.WoodenArrow},
                { ProjectileID.ChumBucket, ItemID.ChumBucket},
                { ProjectileID.ChlorophyteBullet, ItemID.ChlorophyteBullet},
                { ProjectileID.CrystalBullet, ItemID.CrystalBullet},
                { ProjectileID.CursedBullet, ItemID.CursedBullet},
                { ProjectileID.ExplosiveBullet, ItemID.ExplodingBullet},
                { ProjectileID.GoldenBullet, ItemID.GoldenBullet},
                { ProjectileID.BulletHighVelocity, ItemID.HighVelocityBullet},
                { ProjectileID.IchorBullet, ItemID.IchorBullet},
                { ProjectileID.MoonlordBullet, ItemID.MoonlordBullet},
                { ProjectileID.MeteorShot, ItemID.MeteorShot},
                { ProjectileID.Bullet, ItemID.MusketBall},
                { ProjectileID.NanoBullet, ItemID.NanoBullet},
                { ProjectileID.PartyBullet, ItemID.PartyBullet},
                { ProjectileID.SilverBullet, ItemID.SilverBullet},
                { ProjectileID.VenomBullet, ItemID.VenomBullet},
                { ProjectileID.SnowBallFriendly, ItemID.Snowball},
            };

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

            MurasamaEcType.heldProjType = ProjectileType<MurasamaHeldProj>();

            for (int i = 0; i < ItemLoader.ItemCount; i++) {
                Item item = new Item(i);
                if (item != null && item.type != ItemID.None) {//验证物品是否有效
                    if (!ItemToHeldProjID.ContainsKey(item.type)) {
                        ItemToHeldProjID.Add(item.type, item.CWR().heldProjType);
                    }

                    if (!ItemToHeldProj.ContainsKey(item.type) && ItemToHeldProjID.ContainsKey(item.type)) {
                        Projectile projectile = new Projectile();
                        projectile.SetDefaults(ItemToHeldProjID[item.type]);
                        ItemToHeldProj.Add(item.type, projectile);

                        if (!ItemToBaseGun.ContainsKey(item.type)) {
                            BaseGun gun = projectile.ModProjectile as BaseGun;
                            if (gun != null) {
                                ItemToBaseGun.Add(item.type, gun);
                            }
                        }
                        if (!ItemToBaseBow.ContainsKey(item.type)) {
                            BaseBow bow = projectile.ModProjectile as BaseBow;
                            if (bow != null) {
                                ItemToBaseBow.Add(item.type, bow);
                            }
                        }
                        if (!ItemToBaseRanged.ContainsKey(item.type)) {
                            BaseHeldRanged ranged = projectile.ModProjectile as BaseHeldRanged;
                            if (ranged != null) {
                                ItemToBaseRanged.Add(item.type, ranged);
                            }
                        }
                    }

                    if (!ItemToShootID.ContainsKey(item.type)) {
                        //int newShot = CWRUtils.RocketAmmo(item);
                        //("添加射弹与物品对应词典: ItemID" + i + "-----" + item + "----- shootID:" + item.shoot).DompInConsole();
                        ItemToShootID.Add(item.type, item.shoot);
                        if (ShootToItemID.ContainsKey(item.shoot)) {
                            List<int> shoots = ShootToItemID[item.shoot];
                            shoots.Add(item.type);
                        }
                        else {
                            ShootToItemID.Add(item.shoot, new List<int>() { item.type });
                        }
                    }
                }
            }

            /*
            "————————————————————————————————————————————————————".DompInConsole();
            $"装载完毕，ItemToShootID共装填入 {ItemToShootID.Count} 个对照索引".DompInConsole();
            "————————————————————————————————————————————————————".DompInConsole();
            */

            List<ISetupData> setupDatas = CWRUtils.GetSubInterface<ISetupData>("ISetupData");
            foreach (var i in setupDatas) { i.SetupData(); }

            OnLoadContentBool = false;
        }
        /// <summary>
        /// 修改一些原弹药设定异常的物品的shoot值
        /// </summary>
        /// <param name="ammoItem"></param>
        public static void SetAmmoItem(Item ammoItem) {
            ref int ammoTypes = ref ammoItem.shoot;
            if (ammoItem.type == ItemID.FallenStar) {
                ammoTypes = ProjectileID.StarCannonStar;
            }
            if (ammoItem.type == ItemID.RocketI) {
                ammoTypes = ProjectileID.RocketI;
            }
            if (ammoItem.type == ItemID.RocketII) {
                ammoTypes = ProjectileID.RocketII;
            }
            if (ammoItem.type == ItemID.RocketIII) {
                ammoTypes = ProjectileID.RocketIII;
            }
            if (ammoItem.type == ItemID.RocketIV) {
                ammoTypes = ProjectileID.RocketIV;
            }
            if (ammoItem.type == ItemID.ClusterRocketI) {
                ammoTypes = ProjectileID.ClusterRocketI;
            }
            if (ammoItem.type == ItemID.ClusterRocketII) {
                ammoTypes = ProjectileID.ClusterRocketII;
            }
            if (ammoItem.type == ItemID.DryRocket) {
                ammoTypes = ProjectileID.DryRocket;
            }
            if (ammoItem.type == ItemID.WetRocket) {
                ammoTypes = ProjectileID.WetRocket;
            }
            if (ammoItem.type == ItemID.HoneyRocket) {
                ammoTypes = ProjectileID.HoneyRocket;
            }
            if (ammoItem.type == ItemID.LavaRocket) {
                ammoTypes = ProjectileID.LavaRocket;
            }
            if (ammoItem.type == ItemID.MiniNukeI) {
                ammoTypes = ProjectileID.MiniNukeRocketI;
            }
            if (ammoItem.type == ItemID.MiniNukeII) {
                ammoTypes = ProjectileID.MiniNukeRocketII;
            }
        }
    }
}
