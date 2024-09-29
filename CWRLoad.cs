using CalamityMod.Items;
using CalamityMod.Items.LoreItems;
using CalamityMod.Items.Materials;
using CalamityMod.Items.PermanentBoosters;
using CalamityMod.NPCs.Abyss;
using CalamityMod.NPCs.AquaticScourge;
using CalamityMod.NPCs.AstrumAureus;
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
using CalamityMod.Projectiles.Typeless;
using CalamityOverhaul.Content.Items.Materials;
using CalamityOverhaul.Content.Items.Melee;
using CalamityOverhaul.Content.Items.Placeable;
using CalamityOverhaul.Content.Items.Rogue.Extras;
using CalamityOverhaul.Content.Items.Tools;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMechanicalEye;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.MurasamaProj;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.Core;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeavenfallLongbowProj;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using static Terraria.ModLoader.ModContent;

namespace CalamityOverhaul
{
    internal static class CWRLoad {
        #region Data
        public static bool OnLoadContentBool;

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

        public static int Projectile_ArcZap;
        /// <summary>
        /// 关于哪些物品应该被设置为64的最大堆叠数
        /// </summary>
        public static int[] AddMaxStackItemsIn64 { get; private set; } = [];
        /// <summary>
        /// 无尽锭
        /// </summary>
        public static int[] MaterialsTypes { get; private set; }
        /// <summary>
        /// 湮宇星矢
        /// </summary>
        public static int[] MaterialsTypes2 { get; private set; }
        /// <summary>
        /// 天堂陨落
        /// </summary>
        public static int[] MaterialsTypes3 { get; private set; }
        /// <summary>
        /// 无尽镐
        /// </summary>
        public static int[] MaterialsTypes4 { get; private set; }
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
        /// 白金星舰
        /// </summary>
        public static int AstrumAureus;

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
        /// 毁灭魔像
        /// </summary>
        public static List<int> targetNpcTypes16;
        public static int RavagerBody;
        public static int RavagerClawLeft;
        public static int RavagerClawRight;
        public static int RavagerHead;
        public static int RavagerLegLeft;
        public static int RavagerLegRight;

        /// <summary>
        /// 蠕虫类体节
        /// </summary>
        public static int[] WormBodys { get; private set; }

        /// <summary>
        /// 物块对应掉落物的词典
        /// </summary>
        public static Dictionary<int, int> TileToItem { get; private set; } = [];
        /// <summary>
        /// 墙体对应掉落物的词典
        /// </summary>
        public static Dictionary<int, int> WallToItem { get; private set; } = [];
        /// <summary>
        /// 物品对应射弹的词典
        /// </summary>
        public static Dictionary<int, int> ItemToShootID { get; private set; } = [];
        public static Dictionary<int, List<int>> ShootToItemID { get; private set; } = [];
        public static Dictionary<int, int> ItemToHeldProjID { get; private set; } = [];
        public static Dictionary<int, Projectile> ItemToHeldProj { get; private set; } = [];
        internal static Dictionary<int, BaseGun> ItemToBaseGun { get; private set; } = [];
        internal static Dictionary<int, BaseBow> ItemToBaseBow { get; private set; } = [];
        internal static Dictionary<int, BaseHeldRanged> ItemToBaseRanged { get; private set; } = [];
        internal static Dictionary<int, int> ProjectileToSafeAmmoMap { get; private set; } = [];
        /// <summary>
        /// 对应ID的武器是否应该判定为一个手持类远程武器
        /// </summary>
        internal static Dictionary<int, bool> WeaponIsHeldRanged { get; private set; } = [];
        /// <summary>
        /// 对应ID的武器是否应该判定为一个弓
        /// </summary>
        internal static Dictionary<int, bool> WeaponIsBow { get; private set; } = [];
        /// <summary>
        /// 对应ID的武器是否应该判定为一个枪
        /// </summary>
        internal static Dictionary<int, bool> WeaponIsGun { get; private set; } = [];
        /// <summary>
        /// 对应ID的武器是否应该判定为一个装填类枪
        /// </summary>
        internal static Dictionary<int, bool> WeaponIsFeederGun { get; private set; } = [];
        /// <summary>
        /// 对应ID的武器是否应该判定为一个霰弹枪
        /// </summary>
        internal static Dictionary<int, bool> WeaponIsShotgunSkt { get; private set; } = [];
        /// <summary>
        /// 对应ID的武器是否应该判定为一个弩
        /// </summary>
        internal static Dictionary<int, bool> WeaponIsCrossbow { get; private set; } = [];
        #endregion

        public static class NPCValue
        {
            /// <summary>
            /// 是否是一个金属性质的存在
            /// </summary>
            internal static Dictionary<int, bool> TheofSteel;
            public static bool ISTheofSteel(int type) {
                if (type == NPCID.Spazmatism && SpazmatismAI.Accompany) {
                    return true;
                }
                if (type == NPCID.Retinazer && RetinazerAI.Accompany) {
                    return true;
                }
                return TheofSteel[type];
            }
        }

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

            AstrumAureus = NPCType<AstrumAureus>();

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

            Projectile_ArcZap = ModContent.ProjectileType<ArcZap>();

            targetNpcTypes = [SepulcherHead, SepulcherBody, SepulcherTail];
            targetNpcTypes2 = [StormWeaverHead, StormWeaverBody, StormWeaverTail];
            targetNpcTypes3 = [PrimordialWyrmHead, PrimordialWyrmBody, PrimordialWyrmTail];
            targetNpcTypes4 = [PerforatorHeadLarge, PerforatorBodyLarge, PerforatorTailLarge];
            targetNpcTypes5 = [PerforatorHeadMedium, PerforatorBodyMedium, PerforatorTailMedium];
            targetNpcTypes6 = [ArmoredDiggerHead, ArmoredDiggerBody, ArmoredDiggerTail];
            targetNpcTypes7 = [Apollo, Artemis, AresBody, ThanatosHead, ThanatosBody1, ThanatosBody2, ThanatosTail];
            targetNpcTypes7_1 = [AresBody, AresLaserCannon, AresPlasmaFlamethrower, AresTeslaCannon, AresGaussNuke];
            targetNpcTypes8 = [DevourerofGodsHead, DevourerofGodsBody, DevourerofGodsTail, CosmicGuardianHead, CosmicGuardianBody, CosmicGuardianTail];
            targetNpcTypes9 = [DesertScourgeHead, DesertScourgeBody, DesertScourgeTail, DesertNuisanceHead, DesertNuisanceBody, DesertNuisanceTail];
            targetNpcTypes10 = [AstrumDeusHead, AstrumDeusBody, AstrumDeusTail];
            targetNpcTypes11 = [AquaticScourgeHead, AquaticScourgeBody, AquaticScourgeTail];
            targetNpcTypes12 = [EidolonWyrmHead, EidolonWyrmBody, EidolonWyrmBodyAlt, EidolonWyrmTail];
            targetNpcTypes13 = [NPCID.MoonLordFreeEye, NPCID.MoonLordCore, NPCID.MoonLordHand, NPCID.MoonLordHead, NPCID.MoonLordLeechBlob];
            targetNpcTypes14 = [NPCID.EaterofWorldsHead, NPCID.EaterofWorldsBody, NPCID.EaterofWorldsTail];
            targetNpcTypes15 = [NPCID.TheDestroyer, NPCID.TheDestroyerBody, NPCID.TheDestroyerTail];
            targetNpcTypes16 = [RavagerBody, RavagerClawLeft, RavagerClawRight, RavagerHead, RavagerLegLeft, RavagerLegRight];

            WormBodys = [ AquaticScourgeBody, StormWeaverBody, ArmoredDiggerBody, DesertScourgeBody, DesertNuisanceBody
                , CosmicGuardianBody, PrimordialWyrmBody, ThanatosBody1, ThanatosBody2, DevourerofGodsBody, AstrumDeusBody
                , SepulcherBody, PerforatorBodyLarge, PerforatorBodyMedium, NPCID.TheDestroyerBody, NPCID.EaterofWorldsBody];

            MaterialsTypes = [
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
                //ItemType<Polterplasm>(),//幻魂
                ItemType<RuinousSoul>(),//幽花之魂
                ItemType<DarkPlasma>(),//暗物质
                ItemType<UnholyEssence>(),//灼火精华
                ItemType<TwistingNether>(),//扭曲虚空
                ItemType<ArmoredShell>(),//装甲心脏
                ItemType<YharonSoulFragment>(),//龙魂
                ItemType<Rock>()//古恒石
            ];

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

            ProjectileToSafeAmmoMap = new Dictionary<int, int>() {
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
                    WeaponIsShotgunSkt.TryAdd(item.type, false);
                    WeaponIsCrossbow.TryAdd(item.type, false);
                    WeaponIsFeederGun.TryAdd(item.type, false);
                    WeaponIsGun.TryAdd(item.type, false);
                    WeaponIsBow.TryAdd(item.type, false);
                    WeaponIsHeldRanged.TryAdd(item.type, false);

                    if (item.createTile != -1 && !TileToItem.ContainsKey(item.createTile)) {
                        TileToItem.Add(item.createTile, item.type);
                    }
                    if (item.createWall != -1 && !WallToItem.ContainsKey(item.createWall)) {
                        WallToItem.Add(item.createWall, item.type);
                    }

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
                                WeaponIsGun[item.type] = true;
                                if (gun.IsCrossbow) {
                                    WeaponIsCrossbow[item.type] = true;
                                }
                            }
                            BaseFeederGun baseFeederGun = projectile.ModProjectile as BaseFeederGun;
                            if (baseFeederGun != null) {
                                WeaponIsFeederGun[item.type] = true;
                                if (baseFeederGun.LoadingAmmoAnimation == BaseFeederGun.LoadingAmmoAnimationEnum.Shotgun) {
                                    WeaponIsShotgunSkt[item.type] = true;
                                }
                            }
                        }
                        if (!ItemToBaseBow.ContainsKey(item.type)) {
                            BaseBow bow = projectile.ModProjectile as BaseBow;
                            if (bow != null) {
                                WeaponIsBow[item.type] = true;
                                ItemToBaseBow.Add(item.type, bow);
                            }
                        }
                        if (!ItemToBaseRanged.ContainsKey(item.type)) {
                            BaseHeldRanged ranged = projectile.ModProjectile as BaseHeldRanged;
                            if (ranged != null) {
                                WeaponIsHeldRanged[item.type] = true;
                                ItemToBaseRanged.Add(item.type, ranged);
                            }
                        }
                    }

                    if (!ItemToShootID.ContainsKey(item.type)) {
                        ItemToShootID.Add(item.type, item.shoot);
                        if (ShootToItemID.ContainsKey(item.shoot)) {
                            List<int> shoots = ShootToItemID[item.shoot];
                            shoots.Add(item.type);
                        }
                        else {
                            ShootToItemID.Add(item.shoot, [item.type]);
                        }
                    }
                }
            }

            NPCValue.TheofSteel = [];
            for (int i = 0; i < NPCLoader.NPCCount; i++) {
                NPC npc = new NPC();
                npc.SetDefaults(i);
                bool isSteel = false;
                if (npc.HitSound == SoundID.NPCHit2
                    || npc.HitSound == SoundID.NPCHit3 || npc.HitSound == SoundID.NPCHit4
                    || npc.HitSound == SoundID.NPCHit41 || npc.HitSound == SoundID.NPCHit42
                    || targetNpcTypes10.Contains(npc.type) || targetNpcTypes15.Contains(npc.type)
                    || targetNpcTypes7.Contains(npc.type) || targetNpcTypes7_1.Contains(npc.type)
                    || targetNpcTypes6.Contains(npc.type) || targetNpcTypes7_1.Contains(npc.type)
                    || npc.type == AstrumAureus) {
                    isSteel = true;
                }
                NPCValue.TheofSteel.Add(i, isSteel);
            }

            AddMaxStackItemsIn64 = [
                ItemType<BloodOrange>(),
                ItemType<MiracleFruit>(),
                ItemType<Elderberry>(),
                ItemType<Dragonfruit>(),
                ItemType<LoreCynosure>(),
            ];

            OnLoadContentBool = true;
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
            else if (ammoItem.type == ItemID.RocketI) {
                ammoTypes = ProjectileID.RocketI;
            }
            else if (ammoItem.type == ItemID.RocketII) {
                ammoTypes = ProjectileID.RocketII;
            }
            else if (ammoItem.type == ItemID.RocketIII) {
                ammoTypes = ProjectileID.RocketIII;
            }
            else if (ammoItem.type == ItemID.RocketIV) {
                ammoTypes = ProjectileID.RocketIV;
            }
            else if (ammoItem.type == ItemID.ClusterRocketI) {
                ammoTypes = ProjectileID.ClusterRocketI;
            }
            else if (ammoItem.type == ItemID.ClusterRocketII) {
                ammoTypes = ProjectileID.ClusterRocketII;
            }
            else if (ammoItem.type == ItemID.DryRocket) {
                ammoTypes = ProjectileID.DryRocket;
            }
            else if (ammoItem.type == ItemID.WetRocket) {
                ammoTypes = ProjectileID.WetRocket;
            }
            else if (ammoItem.type == ItemID.HoneyRocket) {
                ammoTypes = ProjectileID.HoneyRocket;
            }
            else if (ammoItem.type == ItemID.LavaRocket) {
                ammoTypes = ProjectileID.LavaRocket;
            }
            else if (ammoItem.type == ItemID.MiniNukeI) {
                ammoTypes = ProjectileID.MiniNukeRocketI;
            }
            else if (ammoItem.type == ItemID.MiniNukeII) {
                ammoTypes = ProjectileID.MiniNukeRocketII;
            }
        }
    }
}
