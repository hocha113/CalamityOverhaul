using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Terraria.ModLoader;

namespace CalamityOverhaul
{
    /// <summary>
    /// 所有用于引用外部模组内部内容的ID集中地
    /// </summary>
    internal static class CWRID
    {
        #region 物品ID引用
        public static int Item_SHPC => Get();
        public static int Item_HalibutCannon => Get();
        public static int Item_Murasama => Get();
        public static int Item_Starmada => Get();
        public static int Item_DraedonPowerCell => Get();
        public static int Item_AquaticScourgeBag => Get();
        public static int Item_AerialiteBar => Get();
        public static int Item_DeliciousMeat => Get();
        public static int Item_Heresy => Get();
        public static int Item_UnholyEssence => Get();
        public static int Item_YharonSoulFragment => Get();
        public static int Item_BurntSienna => Get();
        public static int Item_DesertScourgeBag => Get();
        public static int Item_DraedonBag => Get();
        public static int Item_OldDukeBag => Get();
        public static int Item_PolterghastBag => Get();
        public static int Item_DubiousPlating => Get();
        public static int Item_Rock => Get();
        public static int Item_CorrodedFossil => Get();
        public static int Item_LunarianBow => Get();
        public static int Item_PerennialBar => Get();
        public static int Item_UelibloomBar => Get();
        public static int Item_LifeAlloy => Get();
        public static int Item_AstralBar => Get();
        public static int Item_GalacticaSingularity => Get();
        public static int Item_Onyxia => Get();
        public static int Item_CryonicBar => Get();
        public static int Item_FlurrystormCannon => Get();
        public static int Item_EssenceofEleum => Get();
        public static int Item_StaticRefiner => Get();
        public static int Item_ProfanedCrucible => Get();
        public static int Item_PlagueInfuser => Get();
        public static int Item_MonolithAmalgam => Get();
        public static int Item_VoidCondenser => Get();
        public static int Item_BloodOrange => Get();
        public static int Item_MiracleFruit => Get();
        public static int Item_Elderberry => Get();
        public static int Item_Dragonfruit => Get();
        public static int Item_LoreCynosure => Get();
        public static int Item_BloodOrb => Get();
        public static int Item_Terminus => Get();
        public static int Item_Brimlish => Get();
        public static int Item_WaveSkipper => Get();
        public static int Item_TerrorBlade => Get();
        public static int Item_BansheeHook => Get();
        public static int Item_GhoulishGouger => Get();
        public static int Item_FatesReveal => Get();
        public static int Item_GhastlyVisage => Get();
        public static int Item_DaemonsFlame => Get();
        public static int Item_EtherealSubjugator => Get();
        public static int Item_Affliction => Get();
        public static int Item_Necroplasm => Get();
        public static int Item_RuinousSoul => Get();
        public static int Item_BloodstoneCore => Get();
        public static int Item_PridefulHuntersPlanarRipper => Get();
        public static int Item_ExoPrism => Get();
        public static int Item_DraedonsForge => Get();
        public static int Item_SnowRuffianMask => Get();
        public static int Item_SnowRuffianChestplate => Get();
        public static int Item_SnowRuffianGreaves => Get();
        public static int Item_PurifiedGel => Get();
        public static int Item_VulcaniteLance => Get();
        public static int Item_Brimlance => Get();
        public static int Item_ContinentalGreatbow => Get();
        public static int Item_BrimstoneFury => Get();
        public static int Item_Helstorm => Get();
        public static int Item_Hellborn => Get();
        public static int Item_AuricBar => Get();
        public static int Item_Terratomere => Get();
        public static int Item_GrandGuardian => Get();
        public static int Item_SomaPrime => Get();
        public static int Item_Infinity => Get();
        public static int Item_Contagion => Get();
        public static int Item_PlagueCellCanister => Get();
        public static int Item_PlaguebringerCarapace => Get();
        public static int Item_InfectedArmorPlating => Get();
        public static int Item_PlaguebringerVisor => Get();
        public static int Item_PlaguebringerPistons => Get();
        public static int Item_ScoriaBar => Get();
        public static int Item_BlightedGel => Get();
        public static int Item_MidasPrime => Get();
        public static int Item_CrackshotColt => Get();
        public static int Item_DivineGeode => Get();
        public static int Item_StormRuler => Get();
        public static int Item_StormlionMandible => Get();
        public static int Item_Pandemic => Get();
        public static int Item_SulphurousGrabber => Get();
        public static int Item_TheSyringe => Get();
        public static int Item_PestilentDefiler => Get();
        public static int Item_TheHive => Get();
        public static int Item_BlightSpewer => Get();
        public static int Item_Malevolence => Get();
        public static int Item_PlagueStaff => Get();
        public static int Item_SparklingEmpress => Get();
        public static int Item_SeaPrism => Get();
        public static int Item_PearlShard => Get();
        public static int Item_DragoonDrizzlefish => Get();
        public static int Item_PlasmaDriveCore => Get();
        public static int Item_MysteriousCircuitry => Get();
        public static int Item_EncryptedSchematicIce => Get();
        public static int Item_EncryptedSchematicSunkenSea => Get();
        public static int Item_EncryptedSchematicJungle => Get();
        public static int Item_EncryptedSchematicHell => Get();
        public static int Item_EncryptedSchematicPlanetoid => Get();
        public static int Item_LuxorsGift => Get();
        public static int Item_EternalBlizzard => Get();
        public static int Item_Arbalest => Get();
        public static int Item_AshesofCalamity => Get();
        public static int Item_Condemnation => Get();
        public static int Item_AshesofAnnihilation => Get();
        public static int Item_Vehemence => Get();
        public static int Item_ValkyrieRay => Get();
        public static int Item_Violence => Get();
        public static int Item_Vigilance => Get();
        public static int Item_DeathstareRod => Get();
        public static int Item_ArmoredShell => Get();
        public static int Item_DarkPlasma => Get();
        public static int Item_TwistingNether => Get();
        public static int Item_Excelsus => Get();
        public static int Item_TheObliterator => Get();
        public static int Item_Deathwind => Get();
        public static int Item_DeathhailStaff => Get();
        public static int Item_StaffoftheMechworm => Get();
        public static int Item_Eradicator => Get();
        public static int Item_StarterBag => Get();
        public static int Item_CosmicDischarge => Get();
        public static int Item_Norfleet => Get();
        public static int Item_CosmiliteBar => Get();
        public static int Item_Kingsbane => Get();
        public static int Item_ShadowspecBar => Get();
        public static int Item_EndothermicEnergy => Get();
        public static int Item_EnergyCore => Get();
        public static int Item_SuspiciousScrap => Get();
        public static int Item_WulfrumMetalScrap => Get();
        public static int Item_ChargingStationItem => Get();
        public static int Item_FireTurret => Get();
        public static int Item_IceTurret => Get();
        public static int Item_LabTurret => Get();
        public static int Item_LaserTurret => Get();
        public static int Item_OnyxTurret => Get();
        public static int Item_PlagueTurret => Get();
        public static int Item_WaterTurret => Get();
        public static int Item_HostileFireTurret => Get();
        public static int Item_HostileIceTurret => Get();
        public static int Item_HostileLabTurret => Get();
        public static int Item_HostileLaserTurret => Get();
        public static int Item_HostileOnyxTurret => Get();
        public static int Item_HostilePlagueTurret => Get();
        public static int Item_HostileWaterTurret => Get();
        #endregion
        #region NPC ID引用
        public static int NPC_Cataclysm => Get();
        public static int NPC_BrimstoneHeart => Get();
        public static int NPC_Polterghast => Get();
        public static int NPC_SepulcherHead => Get();
        public static int NPC_SepulcherBody => Get();
        public static int NPC_SepulcherTail => Get();
        public static int NPC_Yharon => Get();
        public static int NPC_SlimeGodCore => Get();
        public static int NPC_Providence => Get();
        public static int NPC_ProfanedGuardianCommander => Get();
        public static int NPC_Dragonfolly => Get();
        public static int NPC_PlaguebringerGoliath => Get();
        public static int NPC_PerforatorHive => Get();
        public static int NPC_Anahita => Get();
        public static int NPC_Leviathan => Get();
        public static int NPC_HiveMind => Get();
        public static int NPC_DevourerofGodsHead => Get();
        public static int NPC_DevourerofGodsBody => Get();
        public static int NPC_DevourerofGodsTail => Get();
        public static int NPC_Cryogen => Get();
        public static int NPC_Crabulon => Get();
        public static int NPC_CrabShroom => Get();
        public static int NPC_BrimstoneElemental => Get();
        public static int NPC_EbonianPaladin => Get();
        public static int NPC_CrimulanPaladin => Get();
        public static int NPC_SplitEbonianPaladin => Get();
        public static int NPC_SplitCrimulanPaladin => Get();
        public static int NPC_Catastrophe => Get();
        public static int NPC_Draedon => Get();
        public static int NPC_RavagerHead2 => Get();
        public static int NPC_DarkEnergy => Get();
        public static int NPC_PolterghastHook => Get();
        public static int NPC_CalamitasClone => Get();
        public static int NPC_AquaticScourgeBodyAlt => Get();
        public static int NPC_SupremeCalamitas => Get();
        public static int NPC_ThanatosHead => Get();
        public static int NPC_THIEF => Get();
        public static int NPC_WITCH => Get();
        public static int NPC_SEAHOE => Get();
        public static int NPC_DILF => Get();
        public static int NPC_DesertScourgeHead => Get();
        public static int NPC_AquaticScourgeHead => Get();
        public static int NPC_OldDuke => Get();
        public static int NPC_ScornEater => Get();
        public static int NPC_CeaselessVoid => Get();
        public static int NPC_Signus => Get();
        public static int NPC_StormWeaverHead => Get();
        public static int NPC_StormWeaverBody => Get();
        public static int NPC_StormWeaverTail => Get();
        public static int NPC_PrimordialWyrmHead => Get();
        public static int NPC_PrimordialWyrmBody => Get();
        public static int NPC_PrimordialWyrmTail => Get();
        public static int NPC_PerforatorHeadLarge => Get();
        public static int NPC_PerforatorBodyLarge => Get();
        public static int NPC_PerforatorTailLarge => Get();
        public static int NPC_PerforatorHeadMedium => Get();
        public static int NPC_PerforatorBodyMedium => Get();
        public static int NPC_PerforatorTailMedium => Get();
        public static int NPC_PerforatorHeadSmall => Get();
        public static int NPC_PerforatorBodySmall => Get();
        public static int NPC_PerforatorTailSmall => Get();
        public static int NPC_Apollo => Get();
        public static int NPC_Artemis => Get();
        public static int NPC_AresBody => Get();
        public static int NPC_AresLaserCannon => Get();
        public static int NPC_AresPlasmaFlamethrower => Get();
        public static int NPC_AresTeslaCannon => Get();
        public static int NPC_AresGaussNuke => Get();
        public static int NPC_ThanatosBody1 => Get();
        public static int NPC_ThanatosBody2 => Get();
        public static int NPC_ThanatosTail => Get();
        public static int NPC_DesertScourgeBody => Get();
        public static int NPC_DesertScourgeTail => Get();
        public static int NPC_DesertNuisanceHead => Get();
        public static int NPC_DesertNuisanceBody => Get();
        public static int NPC_DesertNuisanceTail => Get();
        public static int NPC_DesertNuisanceBodyYoung => Get();
        public static int NPC_AstrumDeusHead => Get();
        public static int NPC_AstrumDeusBody => Get();
        public static int NPC_AstrumDeusTail => Get();
        public static int NPC_AquaticScourgeBody => Get();
        public static int NPC_AquaticScourgeTail => Get();
        public static int NPC_EidolonWyrmHead => Get();
        public static int NPC_EidolonWyrmBody => Get();
        public static int NPC_EidolonWyrmBodyAlt => Get();
        public static int NPC_EidolonWyrmTail => Get();
        public static int NPC_RavagerBody => Get();
        public static int NPC_RavagerClawLeft => Get();
        public static int NPC_RavagerClawRight => Get();
        public static int NPC_RavagerHead => Get();
        public static int NPC_RavagerLegLeft => Get();
        public static int NPC_RavagerLegRight => Get();
        #endregion
        #region 弹幕ID引用
        public static int Proj_ArcZap => Get();
        public static int Proj_NastyChollaBol => Get();
        public static int Proj_CosmicDischargeFlail => Get();
        public static int Proj_CosmicIceBurst => Get();
        public static int Proj_MushBomb => Get();
        public static int Proj_MushBombFall => Get();
        public static int Proj_OverlyDramaticDukeSummoner => Get();
        public static int Proj_DesertScourgeSpit => Get();
        public static int Proj_NitroShot => Get();
        public static int Proj_FlurrystormIceChunk => Get();
        public static int Proj_TerratomereSlashCreator => Get();
        public static int Proj_SCalRitualDrama => Get();
        public static int Proj_FireShotBuffer => Get();
        public static int Proj_IceShotBuffer => Get();
        public static int Proj_DraedonLaserBuffer => Get();
        public static int Proj_LaserShotBuffer => Get();
        public static int Proj_OnyxShotBuffer => Get();
        public static int Proj_PlagueShotBuffer => Get();
        public static int Proj_WaterShotBuffer => Get();
        #endregion
        #region 物块ID引用
        public static int Tile_LaboratoryPipePlating => Get();
        public static int Tile_LaboratoryPlating => Get();
        public static int Tile_LabHologramProjector => Get();
        public static int Tile_PlagueInfuser => Get();
        public static int Tile_DraedonsForge => Get();
        public static int Tile_SCalAltar => Get();
        public static int Tile_SCalAltarLarge => Get();
        public static int Tile_SulphurousSand => Get();
        public static int Tile_SulphurousSandstone => Get();
        public static int Tile_CosmicAnvil => Get();
        public static int Tile_AncientAltar => Get();
        public static int Tile_AshenAltar => Get();
        public static int Tile_BotanicPlanter => Get();
        public static int Tile_EutrophicShelf => Get();
        public static int Tile_MonolithAmalgam => Get();
        public static int Tile_VoidCondenser => Get();
        public static int Tile_WulfrumLabstation => Get();
        public static int Tile_StaticRefiner => Get();
        public static int Tile_ProfanedCrucible => Get();
        public static int Tile_ChargingStation => Get();
        public static int Tile_PlayerFireTurret => Get();
        public static int Tile_PlayerIceTurret => Get();
        public static int Tile_PlayerLabTurret => Get();
        public static int Tile_PlayerLaserTurret => Get();
        public static int Tile_PlayerOnyxTurret => Get();
        public static int Tile_PlayerPlagueTurret => Get();
        public static int Tile_PlayerWaterTurret => Get();
        public static int Tile_HostileFireTurret => Get();
        public static int Tile_HostileIceTurret => Get();
        public static int Tile_DraedonLabTurret => Get();
        public static int Tile_HostileLaserTurret => Get();
        public static int Tile_HostileOnyxTurret => Get();
        public static int Tile_HostilePlagueTurret => Get();
        public static int Tile_HostileWaterTurret => Get();
        public static int Tile_SecurityChestTile => Get();
        public static int Tile_AgedSecurityChestTile => Get();
        #endregion
        #region 增益效果ID引用
        public static int Buff_Dragonfire => Get();
        public static int Buff_ElementalMix => Get();
        public static int Buff_VulnerabilityHex => Get();
        public static int Buff_MarkedforDeath => Get();
        public static int Buff_GodSlayerInferno => Get();
        public static int Buff_Nightwither => Get();
        public static int Buff_GlacialState => Get();
        public static int Buff_ArmorCrunch => Get();
        public static int Buff_CrushDepth => Get();
        public static int Buff_WhisperingDeath => Get();
        public static int Buff_BanishingFire => Get();
        public static int Buff_PearlAura => Get();
        public static int Buff_Eutrophication => Get();
        #endregion
        #region 粒子效果ID引用
        public readonly static int Dust_SulphurousSeaAcid = 75;
        public readonly static int Dust_Brimstone = 235;//灾厄使用夺命杖的粒子作为硫磺火焰粒子，因为这个比较特殊，就不通过反射加载了，直接写上readonly
        #endregion
        #region 稀有度ID引用
        public static int Rarity_BurnishedAuric => Get();
        public static int Rarity_Turquoise => Get();
        public static int Rarity_HotPink => Get();
        public static int Rarity_PureGreen => Get();
        public static int Rarity_CosmicPurple => Get();
        public static int Rarity_DarkOrange => Get();
        #endregion
        #region 物品组ID引用
        public readonly static int ItemGroup_RogueWeapon = 570;//盗贼武器物品组ID，因为这个比较特殊，就不通过反射加载了，直接写上readonly
        #endregion

        #region 保底加载

        /// <summary>
        /// 在Setup阶段调用，强制访问所有ID属性以预填充缓存
        /// 加载失败的条目会通过日志输出，便于在调试阶段排查失效内容
        /// </summary>
        public static void PreloadAll() {
            var logger = CWRMod.Instance.Logger;
            int total = 0;
            int failed = 0;

            foreach (PropertyInfo prop in typeof(CWRID).GetProperties(BindingFlags.Public | BindingFlags.Static)) {
                if (prop.PropertyType != typeof(int) || !prop.CanRead) {
                    continue;
                }

                total++;
                int id = (int)prop.GetValue(null)!;
                if (id == 0) {
                    failed++;
                    logger.Warn($"[CWRID] Preload failed: {prop.Name} resolved to 0");
                }
            }

            if (failed > 0) {
                logger.Warn($"[CWRID] Preload complete: {failed}/{total} IDs failed to resolve");
            }
            else {
                logger.Info($"[CWRID] Preload complete: all {total} IDs resolved successfully");
            }
        }

        #endregion

        #region 数据加载逻辑
        private readonly static Dictionary<string, int> _idCache = [];
        private static int Get([CallerMemberName] string name = "") {
            if (_idCache.TryGetValue(name, out int value)) {
                return value;
            }

            string[] parts = name.Split('_');
            if (parts.Length < 2) {
                CWRMod.Instance.Logger.Warn($"[CWRID] Invalid ID format: {name}");
                return 0;
            }

            string prefix = parts[0];
            string typeName = string.Join("", parts.Skip(1));
            const string calamityModName = "CalamityMod";
            int result = 0;
            bool found = false;

            switch (prefix) {
                case "Item":
                    if (ModContent.TryFind(calamityModName, typeName, out ModItem modItem)) {
                        result = modItem.Type;
                        found = true;
                    }
                    break;
                case "NPC":
                    if (ModContent.TryFind(calamityModName, typeName, out ModNPC modNPC)) {
                        result = modNPC.Type;
                        found = true;
                    }
                    break;
                case "Proj":
                    if (ModContent.TryFind(calamityModName, typeName, out ModProjectile modProjectile)) {
                        result = modProjectile.Type;
                        found = true;
                    }
                    break;
                case "Tile":
                    if (ModContent.TryFind(calamityModName, typeName, out ModTile modTile)) {
                        result = modTile.Type;
                        found = true;
                    }
                    break;
                case "Buff":
                    if (ModContent.TryFind(calamityModName, typeName, out ModBuff modBuff)) {
                        result = modBuff.Type;
                        found = true;
                    }
                    break;
                case "Dust":
                    if (ModContent.TryFind(calamityModName, typeName, out ModDust modDust)) {
                        result = modDust.Type;
                        found = true;
                    }
                    break;
                case "Rarity":
                    if (ModContent.TryFind(calamityModName, typeName, out ModRarity modRarity)) {
                        result = modRarity.Type;
                        found = true;
                    }
                    break;
            }

            if (found && result != 0) {
                _idCache[name] = result;
                return result;
            }
            else {
                if (CWRRef.Has) {
                    //如果没找到，可能是因为模组还没加载完，或者ID真的错了
                    ModLoader.GetMod("CalamityOverhaul").Logger.Warn($"[CWRID] Failed to find {name} in CalamityMod. It might be too early to access, or the ID is incorrect.");
                }
                return 0;//不要返回0，否则容易发生各种意料之外的情况
                //不，必须返回0，这样才能知道那里错了
            }
        }
        #endregion

        #region 卸载数据
        public static void UnLoad() => _idCache.Clear();
        #endregion
    }
}
