using CalamityMod;
using CalamityMod.Items.LoreItems;
using CalamityMod.Items.Materials;
using CalamityMod.Items.PermanentBoosters;
using CalamityMod.NPCs.Abyss;
using CalamityMod.NPCs.AquaticScourge;
using CalamityMod.NPCs.AstrumAureus;
using CalamityMod.NPCs.AstrumDeus;
using CalamityMod.NPCs.DesertScourge;
using CalamityMod.NPCs.DevourerofGods;
using CalamityMod.NPCs.DraedonLabThings;
using CalamityMod.NPCs.ExoMechs.Apollo;
using CalamityMod.NPCs.ExoMechs.Ares;
using CalamityMod.NPCs.ExoMechs.Artemis;
using CalamityMod.NPCs.ExoMechs.Thanatos;
using CalamityMod.NPCs.HiveMind;
using CalamityMod.NPCs.NormalNPCs;
using CalamityMod.NPCs.Perforator;
using CalamityMod.NPCs.PlaguebringerGoliath;
using CalamityMod.NPCs.Polterghast;
using CalamityMod.NPCs.PrimordialWyrm;
using CalamityMod.NPCs.Providence;
using CalamityMod.NPCs.Ravager;
using CalamityMod.NPCs.StormWeaver;
using CalamityMod.NPCs.SupremeCalamitas;
using CalamityMod.NPCs.Yharon;
using CalamityMod.Projectiles.Typeless;
using CalamityOverhaul.Content;
using CalamityOverhaul.Content.Items.Materials;
using CalamityOverhaul.Content.Items.Placeable;
using CalamityOverhaul.Content.Items.Rogue.Extras;
using CalamityOverhaul.Content.Items.Tools;
using CalamityOverhaul.Content.LegendWeapon.MurasamaLegend;
using CalamityOverhaul.Content.LegendWeapon.MurasamaLegend.MurasamaProj;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.Core;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeavenfallLongbowProj;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using static Terraria.ModLoader.ModContent;

namespace CalamityOverhaul
{
    public static class CWRLoad
    {
        #region Data
        public static bool OnLoadContentBool;

        public static int DarkMatterBall;
        public static int NeutronStarIngot;
        public static int DubiousPlating;
        public static int FoodStallChair;
        public static int FoodStallChairTile;
        public static int Longinus;

        #region OtherMods
        public static int EternitySoul;
        public static int DevisCurse;
        public static int DeviatingEnergy;
        public static int AbomEnergy;
        public static int EternalEnergy;
        public static int MetanovaBar;
        #endregion

        public static RogueDamageClass RogueDamageClass => GetInstance<RogueDamageClass>();

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
        /// 亵渎天神
        /// </summary>
        public static int Providence;
        /// <summary>
        /// 丛林龙
        /// </summary>
        public static int Yharon;
        /// <summary>
        /// 吞噬兽
        /// </summary>
        public static int ScornEater;
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
        public static int DesertNuisanceHeadYoung;
        public static int DesertNuisanceBodyYoung;
        public static int DesertNuisanceTailYoung;
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
        /// 血肉蠕虫3
        /// </summary>
        public static List<int> targetNpcTypes17;
        public static int PerforatorHeadSmall;
        public static int PerforatorBodySmall;
        public static int PerforatorTailSmall;

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
        /// 该物品是否是一把枪械
        /// </summary>
        internal static Dictionary<int, bool> ItemIsGun { get; private set; } = [];
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
        /// 该枪械是否拥有弹匣
        /// </summary>
        internal static Dictionary<int, bool> ItemHasCartridgeHolder { get; private set; } = [];
        /// <summary>
        /// 获取一个枪械的后坐力数值
        /// </summary>
        internal static Dictionary<int, float> ItemIsGunAndGetRecoilValue { get; private set; } = [];
        /// <summary>
        /// 获取一个枪械的后坐力数值所对于的本地化描述键
        /// </summary>
        internal static Dictionary<int, string> ItemIsGunAndGetRecoilLocKey { get; private set; } = [];
        /// <summary>
        /// 从物品id映射到对应的终焉合成内容上，如果该物品没有终焉合成则返回<see langword="null"/>
        /// </summary>
        internal static Dictionary<int, string[]> ItemIDToOmigaSnyContent { get; private set; } = [];

        #endregion

        public static class NPCValue
        {
            /// <summary>
            /// 是否免疫冻结
            /// </summary>
            public static Dictionary<int, bool> ImmuneFrozen = [];
            public static bool ISTheofSteel(NPC npc) {
                if ((npc.HitSound != SoundID.NPCHit4 && npc.HitSound != SoundID.NPCHit41 && npc.HitSound != SoundID.NPCHit2 &&
                npc.HitSound != SoundID.NPCHit5 && npc.HitSound != SoundID.NPCHit11 && npc.HitSound != SoundID.NPCHit30 &&
                npc.HitSound != SoundID.NPCHit34 && npc.HitSound != SoundID.NPCHit36 && npc.HitSound != SoundID.NPCHit42 &&
                npc.HitSound != SoundID.NPCHit49 && npc.HitSound != SoundID.NPCHit52 && npc.HitSound != SoundID.NPCHit53 &&
                npc.HitSound != SoundID.NPCHit54 && npc.HitSound != null)
                || npc.type == Providence || npc.type == ScornEater || npc.type == Yharon) {
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
            public static Dictionary<int, bool> ImmuneFrozen = [];
        }

        public static void Setup() {
            #region ID
            InfiniteArrow = ProjectileType<InfiniteArrow>();
            InfiniteRune = ProjectileType<InfiniteArrow>();
            ParadiseArrow = ProjectileType<InfiniteArrow>();
            HeavenLightning = ProjectileType<InfiniteArrow>();

            DubiousPlating = ItemType<DubiousPlating>();
            DarkMatterBall = ItemType<DarkMatterBall>();
            NeutronStarIngot = ItemType<NeutronStarIngot>();
            FoodStallChair = ItemType<FoodStallChair>();
            FoodStallChairTile = TileType<Content.Tiles.FoodStallChair>();
            Longinus = ItemType<SpearOfLonginus>();

            Androomba = NPCType<Androomba>();
            Polterghast = NPCType<Polterghast>();
            Providence = NPCType<Providence>();
            Yharon = NPCType<Yharon>();
            ScornEater = NPCType<ScornEater>();
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

            ArmoredDiggerHead = NPCType<ArmoredDiggerHead>();
            ArmoredDiggerBody = NPCType<ArmoredDiggerBody>();
            ArmoredDiggerTail = NPCType<ArmoredDiggerTail>();

            Apollo = NPCType<Apollo>();
            Artemis = NPCType<Artemis>();
            AresBody = NPCType<AresBody>();
            ThanatosHead = NPCType<ThanatosHead>();
            ThanatosBody1 = NPCType<ThanatosBody1>();
            ThanatosBody2 = NPCType<ThanatosBody2>();
            ThanatosTail = NPCType<ThanatosTail>();

            DevourerofGodsHead = NPCType<DevourerofGodsHead>();
            DevourerofGodsBody = NPCType<DevourerofGodsBody>();
            DevourerofGodsTail = NPCType<DevourerofGodsTail>();
            CosmicGuardianHead = NPCType<CosmicGuardianHead>();
            CosmicGuardianBody = NPCType<CosmicGuardianBody>();
            CosmicGuardianTail = NPCType<CosmicGuardianTail>();

            DesertScourgeHead = NPCType<DesertScourgeHead>();
            DesertScourgeBody = NPCType<DesertScourgeBody>();
            DesertScourgeTail = NPCType<DesertScourgeTail>();
            DesertNuisanceHead = NPCType<DesertNuisanceHead>();
            DesertNuisanceBody = NPCType<DesertNuisanceBody>();
            DesertNuisanceTail = NPCType<DesertNuisanceTail>();
            DesertNuisanceHeadYoung = NPCType<DesertNuisanceHeadYoung>();
            DesertNuisanceBodyYoung = NPCType<DesertNuisanceBodyYoung>();
            DesertNuisanceTailYoung = NPCType<DesertNuisanceTailYoung>();

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

            PerforatorHeadSmall = NPCType<PerforatorHeadSmall>();
            PerforatorBodySmall = NPCType<PerforatorBodySmall>();
            PerforatorTailSmall = NPCType<PerforatorTailSmall>();

            Projectile_ArcZap = ProjectileType<ArcZap>();

            MurasamaEcType.heldProjType = ProjectileType<MurasamaHeld>();
            #endregion

            #region List
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
            targetNpcTypes17 = [PerforatorHeadSmall, PerforatorBodySmall, PerforatorTailSmall];

            WormBodys = [ AquaticScourgeBody, StormWeaverBody, ArmoredDiggerBody, DesertScourgeBody, DesertNuisanceBody,
                DesertNuisanceBodyYoung, CosmicGuardianBody, PrimordialWyrmBody, ThanatosBody1, ThanatosBody2, DevourerofGodsBody, AstrumDeusBody
                , SepulcherBody, PerforatorBodyLarge, PerforatorBodyMedium, PerforatorBodySmall, NPCID.TheDestroyerBody, NPCID.EaterofWorldsBody];

            AddMaxStackItemsIn64 = [
                ItemType<BloodOrange>(),
                ItemType<MiracleFruit>(),
                ItemType<Elderberry>(),
                ItemType<Dragonfruit>(),
                ItemType<LoreCynosure>(),
                ItemID.BloodMoonStarter,
            ];
            #endregion

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

            for (int itemType = 0; itemType < ItemLoader.ItemCount; itemType++) {
                Item item = new Item(itemType);
                ItemIsGun[itemType] = false;
                ItemIsCrossBow[itemType] = false;
                ItemIsGunAndMustConsumeAmmunition[itemType] = false;
                ItemIsGunAndGetRecoilValue[itemType] = 1.2f;
                ItemIsGunAndGetRecoilLocKey[itemType] = "";
                ItemHasCartridgeHolder[itemType] = false;
                ItemIsBow[itemType] = false;
                ItemIsBowAndArrowNum[itemType] = 1;
                ItemIsRanged[itemType] = false;
                ItemIsRangedAndCanRightClickFire[itemType] = false;
                ItemIDToOmigaSnyContent[itemType] = null;
                if (item != null && item.type != ItemID.None) {//验证物品是否有效
                    if (item.createTile != -1 && !TileToItem.ContainsKey(item.createTile)) {
                        TileToItem.Add(item.createTile, item.type);
                    }
                    if (item.createWall != -1 && !WallToItem.ContainsKey(item.createWall)) {
                        WallToItem.Add(item.createWall, item.type);
                    }

                    CWRItems cwrItem = item.CWR();

                    string[] snyOmig = cwrItem.OmigaSnyContent;
                    if (snyOmig != null) {
                        ItemIDToOmigaSnyContent[itemType] = snyOmig;
                    }

                    ItemHasCartridgeHolder[itemType] = cwrItem.HasCartridgeHolder;

                    int heldProjType = cwrItem.heldProjType;
                    if (heldProjType > 0) {
                        Projectile heldProj = new Projectile();
                        heldProj.SetDefaults(heldProjType);

                        if (heldProj.ModProjectile != null) {
                            if (heldProj.ModProjectile is BaseGun gun) {
                                ItemIsGun[itemType] = true;
                                ItemIsCrossBow[itemType] = gun.IsCrossbow;
                                ItemIsGunAndMustConsumeAmmunition[itemType] = gun.MustConsumeAmmunition;
                                ItemIsGunAndGetRecoilValue[itemType] = gun.Recoil;
                                ItemIsGunAndGetRecoilLocKey[itemType] = GetLckRecoilKey(gun.Recoil);
                            }

                            if (heldProj.ModProjectile is BaseBow bow) {
                                ItemIsBow[itemType] = true;
                                ItemIsBowAndArrowNum[itemType] = bow.BowArrowDrawNum;
                            }

                            if (heldProj.ModProjectile is BaseHeldRanged ranged) {
                                ItemIsRanged[itemType] = true;
                                ItemIsRangedAndCanRightClickFire[itemType] = ranged.CanRightClick;
                            }
                        }
                    }
                }
            }

            for (int i = 0; i < NPCLoader.NPCCount; i++) {
                NPC npc = new NPC();
                npc.SetDefaults(i);
                NPCValue.ImmuneFrozen.TryAdd(i, false);
            }

            for (int i = 0; i < ProjectileLoader.ProjectileCount; i++) {
                ProjValue.ImmuneFrozen.TryAdd(i, false);
            }


            OnLoadContentBool = true;
        }

        public static void UnLoad() {
            TileToItem?.Clear();
            WallToItem?.Clear();
            ItemIsGun?.Clear();
            ItemIsBow?.Clear();
            ItemIsRanged?.Clear();
            ItemIsRangedAndCanRightClickFire?.Clear();
            ItemIsBowAndArrowNum?.Clear();
            ItemIsGunAndMustConsumeAmmunition?.Clear();
            ItemIsGunAndGetRecoilValue?.Clear();
            ItemIsGunAndGetRecoilLocKey?.Clear();
            ProjValue.ImmuneFrozen?.Clear();
        }

        public static string GetLckRecoilKey(float recoil) {
            float recoilValue = Math.Abs(recoil);

            if (recoilValue == 0) {
                return "CWRGun_Recoil_Level_0";
            }
            else if (recoilValue < 0.1f) {
                return "CWRGun_Recoil_Level_1";
            }
            else if (recoilValue < 0.5f) {
                return "CWRGun_Recoil_Level_2";
            }
            else if (recoilValue < 1.5f) {
                return "CWRGun_Recoil_Level_3";
            }
            else if (recoilValue < 2.2f) {
                return "CWRGun_Recoil_Level_4";
            }
            else if (recoilValue < 3.2f) {
                return "CWRGun_Recoil_Level_5";
            }
            return "CWRGun_Recoil_Level_6";
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
            else if (ammoItem.type == ItemID.TungstenBullet) {
                ammoTypes = ProjectileType<TungstenBulletProj>();
            }
        }
    }
}
