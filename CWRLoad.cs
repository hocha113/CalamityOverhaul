using CalamityOverhaul.Common;
using CalamityOverhaul.Content;
using CalamityOverhaul.Content.Items.Magic;
using CalamityOverhaul.Content.Items.Melee;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Items.Rogue;
using CalamityOverhaul.Content.Items.Tools;
using CalamityOverhaul.Content.MeleeModify.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime;
using CalamityOverhaul.Content.RangedModify.Core;
using Microsoft.Xna.Framework.Graphics;
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

        #region OtherMods
        public static int EternitySoul;
        public static int DevisCurse;
        public static int DeviatingEnergy;
        public static int AbomEnergy;
        public static int EternalEnergy;
        public static int MetanovaBar;
        #endregion

        /// <summary>
        /// 关于哪些物品应该被设置为64的最大堆叠数
        /// </summary>
        public static int[] AddMaxStackItemsIn64 { get; private set; } = [];
        /// <summary>
        /// 灾坟虫
        /// </summary>
        public static List<int> targetNpcTypes;
        /// <summary>
        /// 风暴编织者
        /// </summary>
        public static List<int> targetNpcTypes2;
        /// <summary>
        /// 幻海妖龙 
        /// </summary>
        public static List<int> targetNpcTypes3;
        /// <summary>
        /// 血肉蠕虫 
        /// </summary>
        public static List<int> targetNpcTypes4;
        /// <summary>
        /// 血肉蠕虫2 
        /// </summary>
        public static List<int> targetNpcTypes5;
        /// <summary>
        /// 装甲掘地虫 
        /// </summary>
        public static List<int> targetNpcTypes6;
        /// <summary>
        /// 星流巨械
        /// </summary>
        public static List<int> targetNpcTypes7;
        /// <summary>
        /// 星流巨械Alt
        /// </summary>
        public static List<int> targetNpcTypes7_1;
        /// <summary>
        /// 神明吞噬者
        /// </summary>
        public static List<int> targetNpcTypes8;
        /// <summary>
        /// 荒漠灾虫
        /// </summary>
        public static List<int> targetNpcTypes9;
        /// <summary>
        /// 星神游龙
        /// </summary>
        public static List<int> targetNpcTypes10;
        /// <summary>
        /// 渊海灾虫
        /// </summary>
        public static List<int> targetNpcTypes11;
        /// <summary>
        /// 幻海妖龙幼年体
        /// </summary>
        public static List<int> targetNpcTypes12;
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
        /// <summary>
        /// 血肉蠕虫3
        /// </summary>
        public static List<int> targetNpcTypes17;
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
        /// 该物品是否是一个手持挥舞类武器
        /// </summary>
        internal static Dictionary<int, bool> ItemIsHeldSwing { get; private set; } = [];
        /// <summary>
        /// 该手持挥舞类武器否是不阻断原射击方式
        /// </summary>
        internal static Dictionary<int, bool> ItemIsHeldSwingDontStopOrigShoot { get; private set; } = [];
        /// <summary>
        /// 该物品是否是一把枪械
        /// </summary>
        internal static Dictionary<int, bool> ItemIsGun { get; private set; } = [];
        /// <summary>
        /// 该物品是否是一把枪械
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
        /// <summary>
        /// 该物品是否自动装填终焉合成内容
        /// </summary>
        internal static Dictionary<int, bool> ItemAutoloadingOmigaSnyRecipe { get; private set; } = [];
        #endregion

        public static class NPCValue
        {
            /// <summary>
            /// 是否免疫冻结
            /// </summary>
            public readonly static Dictionary<int, bool> ImmuneFrozen = [];
            public static bool ISTheofSteel(NPC npc) {
                if ((npc.HitSound != SoundID.NPCHit4 && npc.HitSound != SoundID.NPCHit41 && npc.HitSound != SoundID.NPCHit2 &&
                npc.HitSound != SoundID.NPCHit5 && npc.HitSound != SoundID.NPCHit11 && npc.HitSound != SoundID.NPCHit30 &&
                npc.HitSound != SoundID.NPCHit34 && npc.HitSound != SoundID.NPCHit36 && npc.HitSound != SoundID.NPCHit42 &&
                npc.HitSound != SoundID.NPCHit49 && npc.HitSound != SoundID.NPCHit52 && npc.HitSound != SoundID.NPCHit53 &&
                npc.HitSound != SoundID.NPCHit54 && npc.HitSound != null)
                || npc.type == CWRID.NPC_Providence || npc.type == CWRID.NPC_ScornEater || npc.type == CWRID.NPC_Yharon || npc.type == CWRID.NPC_DevourerofGodsHead) {
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

        public static void Setup() {
            #region List
            targetNpcTypes = [CWRID.NPC_SepulcherHead, CWRID.NPC_SepulcherBody, CWRID.NPC_SepulcherTail];
            targetNpcTypes2 = [CWRID.NPC_StormWeaverHead, CWRID.NPC_StormWeaverBody, CWRID.NPC_StormWeaverTail];
            targetNpcTypes3 = [CWRID.NPC_PrimordialWyrmHead, CWRID.NPC_PrimordialWyrmBody, CWRID.NPC_PrimordialWyrmTail];
            targetNpcTypes4 = [CWRID.NPC_PerforatorHeadLarge, CWRID.NPC_PerforatorBodyLarge, CWRID.NPC_PerforatorTailLarge];
            targetNpcTypes5 = [CWRID.NPC_PerforatorHeadMedium, CWRID.NPC_PerforatorBodyMedium, CWRID.NPC_PerforatorTailMedium];
            targetNpcTypes6 = [CWRID.NPC_ArmoredDiggerHead, CWRID.NPC_ArmoredDiggerBody, CWRID.NPC_ArmoredDiggerTail];
            targetNpcTypes7 = [CWRID.NPC_Apollo, CWRID.NPC_Artemis, CWRID.NPC_AresBody, CWRID.NPC_ThanatosHead, CWRID.NPC_ThanatosBody1, CWRID.NPC_ThanatosBody2, CWRID.NPC_ThanatosTail];
            targetNpcTypes7_1 = [CWRID.NPC_AresBody, CWRID.NPC_AresLaserCannon, CWRID.NPC_AresPlasmaFlamethrower, CWRID.NPC_AresTeslaCannon, CWRID.NPC_AresGaussNuke];
            targetNpcTypes8 = [CWRID.NPC_DevourerofGodsHead, CWRID.NPC_DevourerofGodsBody, CWRID.NPC_DevourerofGodsTail, CWRID.NPC_CosmicGuardianHead, CWRID.NPC_CosmicGuardianBody, CWRID.NPC_CosmicGuardianTail];
            targetNpcTypes9 = [CWRID.NPC_DesertScourgeHead, CWRID.NPC_DesertScourgeBody, CWRID.NPC_DesertScourgeTail, CWRID.NPC_DesertNuisanceHead, CWRID.NPC_DesertNuisanceBody, CWRID.NPC_DesertNuisanceTail];
            targetNpcTypes10 = [CWRID.NPC_AstrumDeusHead, CWRID.NPC_AstrumDeusBody, CWRID.NPC_AstrumDeusTail];
            targetNpcTypes11 = [CWRID.NPC_AquaticScourgeHead, CWRID.NPC_AquaticScourgeBody, CWRID.NPC_AquaticScourgeTail];
            targetNpcTypes12 = [CWRID.NPC_EidolonWyrmHead, CWRID.NPC_EidolonWyrmBody, CWRID.NPC_EidolonWyrmBodyAlt, CWRID.NPC_EidolonWyrmTail];
            targetNpcTypes13 = [NPCID.MoonLordFreeEye, NPCID.MoonLordCore, NPCID.MoonLordHand, NPCID.MoonLordHead, NPCID.MoonLordLeechBlob];
            targetNpcTypes14 = [NPCID.EaterofWorldsHead, NPCID.EaterofWorldsBody, NPCID.EaterofWorldsTail];
            targetNpcTypes15 = [NPCID.TheDestroyer, NPCID.TheDestroyerBody, NPCID.TheDestroyerTail];
            targetNpcTypes16 = [CWRID.NPC_RavagerBody, CWRID.NPC_RavagerClawLeft, CWRID.NPC_RavagerClawRight, CWRID.NPC_RavagerHead, CWRID.NPC_RavagerLegLeft, CWRID.NPC_RavagerLegRight];
            targetNpcTypes17 = [CWRID.NPC_PerforatorHeadSmall, CWRID.NPC_PerforatorBodySmall, CWRID.NPC_PerforatorTailSmall];

            WormBodys = [ CWRID.NPC_AquaticScourgeBody, CWRID.NPC_StormWeaverBody, CWRID.NPC_ArmoredDiggerBody, CWRID.NPC_DesertScourgeBody, CWRID.NPC_DesertNuisanceBody,
                CWRID.NPC_DesertNuisanceBodyYoung, CWRID.NPC_CosmicGuardianBody, CWRID.NPC_PrimordialWyrmBody, CWRID.NPC_ThanatosBody1, CWRID.NPC_ThanatosBody2, CWRID.NPC_DevourerofGodsBody, CWRID.NPC_AstrumDeusBody
                , CWRID.NPC_SepulcherBody, CWRID.NPC_PerforatorBodyLarge, CWRID.NPC_PerforatorBodyMedium, CWRID.NPC_PerforatorBodySmall, NPCID.TheDestroyerBody, NPCID.EaterofWorldsBody];

            AddMaxStackItemsIn64 = [
                CWRID.Item_Rock,
                CWRID.Item_BloodOrange,
                CWRID.Item_MiracleFruit,
                CWRID.Item_Elderberry,
                CWRID.Item_Dragonfruit,
                CWRID.Item_LoreCynosure,
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
                ItemIsHeldSwing[itemType] = false;
                ItemIsHeldSwingDontStopOrigShoot[itemType] = false;
                ItemIsGun[itemType] = false;
                ItemIsShotgun[itemType] = false;
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
                ItemAutoloadingOmigaSnyRecipe[itemType] = true;
                if (item != null && item.type != ItemID.None) {//验证物品是否有效
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

                    ItemIsHeldSwing[itemType] = cwrItem.IsHeldSwing;
                    ItemIsHeldSwingDontStopOrigShoot[itemType] = cwrItem.IsHeldSwingDontStopOrigShoot;
                    ItemHasCartridgeHolder[itemType] = cwrItem.HasCartridgeHolder;

                    if (cwrItem.IsHeldSwing) {//DeBug处理，如果有的地方没有使用正确的初始化函数设置刀剑，就会在加载的时候在这里报错以提醒开发者
                        Projectile shootProj = new Projectile();
                        shootProj.SetDefaults(item.shoot);
                        if (shootProj.ModProjectile != null && shootProj.ModProjectile is BaseSwing swing) {
                            if (!cwrItem.WeaponInSetKnifeHeld) {
                                throw new InvalidOperationException($"The Sword is not initialized correctly：{item} by {swing})。" +
                                    $"Please check that the initialization function is called correctly"
                                    + "SetKnifeHeld must be used to set the BaseSwing item");
                            }
                        }
                    }

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
                            if (heldProj.ModProjectile is BaseFeederGun feederGun) {
                                ItemIsShotgun[itemType] = feederGun.LoadingAmmoAnimation == LoadingAmmoAnimationEnum.Shotgun;
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

            LogBoss();

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

        public static void LogBoss() {
            if (CWRMod.Instance.bossChecklist == null) {
                return;
            }

            CWRMod.Instance.bossChecklist.Call("LogBoss", CWRMod.Instance, "MachineRebellion", 22.1f,
                () => CWRWorld.MachineRebellionDowned,
                new List<int> { NPCID.SkeletronPrime, NPCID.Spazmatism, NPCID.Retinazer, NPCID.TheDestroyer },
                new Dictionary<string, object>() {
                    ["spawnInfo"] = CWRLocText.Instance.MachineRebellion_SpawnInfo,
                    ["despawnMessage"] = CWRLocText.Instance.MachineRebellion_DespawnMessage,
                    ["displayName"] = CWRLocText.Instance.MachineRebellion_DisplayName,
                    ["spawnItems"] = ItemType<DraedonsRemote>(),
                    ["availability"] = () => true,
                    ["collectibles"] = new List<int> {
                    ItemType<GeminisTributeEX>(),
                    ItemType<RaiderGunEX>(),
                    ItemType<CommandersChainsawEX>(),
                    ItemType<CommandersStaffEX>()
                },
                    ["customPortrait"] = (SpriteBatch sb, Rectangle rect, Color color) => {
                        Texture2D texture = HeadPrimeAI.MachineRebellionAsset.Value;
                        Vector2 centered = rect.TopLeft() + rect.Size() / 2;
                        Rectangle rectangle = texture.GetRectangle();
                        float scale = rect.Width / (float)rectangle.Width;
                        sb.Draw(texture, centered, rectangle, color, 0, rectangle.Size() / 2, scale, SpriteEffects.None, 0);
                    }
                });
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
        }
    }
}
