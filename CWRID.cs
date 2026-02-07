using System.Collections.Generic;
using System.Linq;
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
        public static int Item_SpeedBlaster => Get();
        public static int Item_AcidGun => Get();
        public static int Item_AirSpinner => Get();
        public static int Item_ForbiddenOathblade => Get();
        public static int Item_EutrophicScimitar => Get();
        public static int Item_PlasmaRifle => Get();
        public static int Item_NanoPurge => Get();
        public static int Item_EidolicWail => Get();
        public static int Item_Cryophobia => Get();
        public static int Item_Effervescence => Get();
        public static int Item_SuperradiantSlaughterer => Get();
        public static int Item_DraedonPowerCell => Get();
        public static int Item_AquaticScourgeBag => Get();
        public static int Item_AerialiteBar => Get();
        public static int Item_DeliciousMeat => Get();
        public static int Item_Heresy => Get();
        public static int Item_DevilsDevastation => Get();
        public static int Item_LunarKunai => Get();
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
        public static int Item_LoreAwakening => Get();
        public static int Item_SquirrelSquireStaff => Get();
        public static int Item_ThrowingBrick => Get();
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
        public static int Item_RogueEmblem => Get();
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
        public static int Item_PlagueKeeper => Get();
        public static int Item_Hellkite => Get();
        public static int Item_Contagion => Get();
        public static int Item_PlagueCellCanister => Get();
        public static int Item_PlaguebringerCarapace => Get();
        public static int Item_InfectedArmorPlating => Get();
        public static int Item_PlaguebringerVisor => Get();
        public static int Item_PlaguebringerPistons => Get();
        public static int Item_Lazhar => Get();
        public static int Item_ScoriaBar => Get();
        public static int Item_BlightedGel => Get();
        public static int Item_MidasPrime => Get();
        public static int Item_CrackshotColt => Get();
        public static int Item_HolyCollider => Get();
        public static int Item_CelestialClaymore => Get();
        public static int Item_DivineGeode => Get();
        public static int Item_StormRuler => Get();
        public static int Item_StormlionMandible => Get();
        public static int Item_HellionFlowerSpear => Get();
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
        public static int Item_Murasama => Get();
        public static int Item_PlasmaDriveCore => Get();
        public static int Item_MysteriousCircuitry => Get();
        public static int Item_EncryptedSchematicHell => Get();
        public static int Item_LuxorsGift => Get();
        public static int Item_WarbanneroftheRighteous => Get();
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
        public static int Item_YharonBag => Get();
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
        public static int NPC_KingSlimeJewelRuby => Get();
        public static int NPC_KingSlimeJewelSapphire => Get();
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
        public static int NPC_Androomba => Get();
        public static int NPC_ScornEater => Get();
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
        public static int NPC_DesertNuisanceHeadYoung => Get();
        public static int NPC_DesertNuisanceBodyYoung => Get();
        public static int NPC_DesertNuisanceTailYoung => Get();
        public static int NPC_AstrumDeusHead => Get();
        public static int NPC_AstrumDeusBody => Get();
        public static int NPC_AstrumDeusTail => Get();
        public static int NPC_AquaticScourgeBody => Get();
        public static int NPC_AquaticScourgeTail => Get();
        public static int NPC_EidolonWyrmHead => Get();
        public static int NPC_EidolonWyrmBody => Get();
        public static int NPC_EidolonWyrmBodyAlt => Get();
        public static int NPC_EidolonWyrmTail => Get();
        public static int NPC_AstrumAureus => Get();
        public static int NPC_RavagerBody => Get();
        public static int NPC_RavagerClawLeft => Get();
        public static int NPC_RavagerClawRight => Get();
        public static int NPC_RavagerHead => Get();
        public static int NPC_RavagerLegLeft => Get();
        public static int NPC_RavagerLegRight => Get();
        #endregion
        #region 弹幕ID引用
        public static int Proj_ArcZap => Get();
        public static int Proj_DNA => Get();
        public static int Proj_SeashineSwordProj => Get();
        public static int Proj_EldritchTentacle => Get();
        public static int Proj_DrataliornusExoArrow => Get();
        public static int Proj_Valaricicle => Get();
        public static int Proj_Valaricicle2 => Get();
        public static int Proj_GelWave => Get();
        public static int Proj_VirulentWave => Get();
        public static int Proj_SandBlade => Get();
        public static int Proj_StormBeam => Get();
        public static int Proj_ForbiddenOathbladeProjectile => Get();
        public static int Proj_EutrophicScimitarProj => Get();
        public static int Proj_PlasmaBolt => Get();
        public static int Proj_PlasmaShot => Get();
        public static int Item_DragonRage => Get();
        public static int Proj_SepticSkewerHarpoon => Get();
        public static int Proj_BrimstoneSwordExplosion => Get();
        public static int Proj_BansheeHookScythe => Get();
        public static int Proj_NastyChollaBol => Get();
        public static int Proj_MourningSkull => Get();
        public static int Proj_TinyFlare => Get();
        public static int Proj_NanoPurgeLaser => Get();
        public static int Proj_NeedlerProj => Get();
        public static int Proj_PlasmaExplosion => Get();
        public static int Proj_TheMaelstromExplosion => Get();
        public static int Proj_TheMaelstromShark => Get();
        public static int Proj_SepticSkewerBacteria => Get();
        public static int Proj_SandstormBullet => Get();
        public static int Proj_SicknessRound => Get();
        public static int Proj_ScorchedEarthRocket => Get();
        public static int Proj_BrinyTyphoonBubble => Get();
        public static int Proj_CosmicDischargeFlail => Get();
        public static int Proj_CatastropheClaymoreSparkle => Get();
        public static int Proj_DestroyerCursedLaser => Get();
        public static int Proj_DestroyerElectricLaser => Get();
        public static int Proj_AngelicBeam => Get();
        public static int Proj_AstralRound => Get();
        public static int Proj_AstrealArrow => Get();
        public static int Proj_AuralisBullet => Get();
        public static int Proj_CosmicIceBurst => Get();
        public static int Proj_BarinadeArrow => Get();
        public static int Proj_BoltArrow => Get();
        public static int Proj_Nuke => Get();
        public static int Proj_MushBomb => Get();
        public static int Proj_MushBombFall => Get();
        public static int Proj_AegisFlame => Get();
        public static int Proj_DarkMasterBeam => Get();
        public static int Proj_DarkMasterClone => Get();
        public static int Proj_OverlyDramaticDukeSummoner => Get();
        public static int Proj_MythrilFlare => Get();
        public static int Proj_Brimlash2 => Get();
        public static int Proj_BrimlashProj => Get();
        public static int Proj_BalefulHarvesterProjectile => Get();
        public static int Proj_WaveSkipperProjectile => Get();
        public static int Proj_DeathsAscensionProjectile => Get();
        public static int Proj_DarklightGreatswordSlashCreator => Get();
        public static int Proj_DarkBeam => Get();
        public static int Proj_CometQuasherMeteor => Get();
        public static int Proj_DesertScourgeSpit => Get();
        public static int Proj_Razorwind => Get();
        public static int Proj_Brimblast => Get();
        public static int Proj_IceBombFriendly => Get();
        public static int Proj_AtaraxiaBoom => Get();
        public static int Proj_AtaraxiaMain => Get();
        public static int Proj_AtaraxiaSide => Get();
        public static int Proj_BrimstoneBoom => Get();
        public static int Proj_AftershockRock => Get();
        public static int Proj_UniversalGenesisStar => Get();
        public static int Proj_UniversalGenesisStarcaller => Get();
        public static int Proj_UltimaBolt => Get();
        public static int Proj_UltimaRay => Get();
        public static int Proj_UltimaSpark => Get();
        public static int Proj_TheStormLightningShot => Get();
        public static int Proj_TelluricGlareArrow => Get();
        public static int Proj_AcidRocket => Get();
        public static int Proj_StormSurgeTornado => Get();
        public static int Proj_SputterCometBig => Get();
        public static int Proj_PlasmaBlast => Get();
        public static int Proj_AstralStar => Get();
        public static int Proj_SpykerProj => Get();
        public static int Proj_LostSoulFriendly => Get();
        public static int Proj_Shroom => Get();
        public static int Proj_SeasSearingBubble => Get();
        public static int Proj_SeasSearingSecondary => Get();
        public static int Proj_ArcherfishShot => Get();
        public static int Proj_FishronRPG => Get();
        public static int Proj_ImpactRound => Get();
        public static int Proj_PristineSecondary => Get();
        public static int Proj_PristineFire => Get();
        public static int Proj_PlanarRipperBolt => Get();
        public static int Proj_PlagueTaintedDrone => Get();
        public static int Proj_PlagueTaintedProjectile => Get();
        public static int Proj_ShockblastRound => Get();
        public static int Proj_P90Round => Get();
        public static int Proj_EmesisGore => Get();
        public static int Proj_FlakKrakenProjectile => Get();
        public static int Proj_FlakToxicannonProjectile => Get();
        public static int Proj_FeatherLarge => Get();
        public static int Proj_SlimeStream => Get();
        public static int Proj_ChargedBlast => Get();
        public static int Proj_AuricBullet => Get();
        public static int Proj_AquaBlast => Get();
        public static int Proj_AquaBlastToxic => Get();
        public static int Proj_PlagueArrow => Get();
        public static int Proj_NitroShot => Get();
        public static int Proj_MarksmanShot => Get();
        public static int Proj_RicoshotCoin => Get();
        public static int Proj_MineralMortarProjectile => Get();
        public static int Proj_MiniSharkron => Get();
        public static int Proj_TyphoonArrow => Get();
        public static int Proj_IcicleArrowProj => Get();
        public static int Proj_DrataliornusFlame => Get();
        public static int Proj_VanquisherArrowProj => Get();
        public static int Proj_DaemonsFlameArrow => Get();
        public static int Proj_VernalBolt => Get();
        public static int Proj_CorrodedShell => Get();
        public static int Proj_RealmRavagerBullet => Get();
        public static int Proj_CorinthPrimeAirburstGrenade => Get();
        public static int Proj_SmallCoral => Get();
        public static int Proj_LeafArrow => Get();
        public static int Proj_BrimstoneBolt => Get();
        public static int Proj_ClamorRifleProj => Get();
        public static int Proj_ClaretCannonProj => Get();
        public static int Proj_CondemnationArrowHoming => Get();
        public static int Proj_FlurrystormIceChunk => Get();
        public static int Proj_SquirrelSquireAcorn => Get();
        public static int Proj_DracoBeam => Get();
        public static int Proj_EarthProj => Get();
        public static int Proj_FossilShard => Get();
        public static int Proj_GalacticaComet => Get();
        public static int Proj_ThornBase => Get();
        public static int Proj_Flarefrost => Get();
        public static int Proj_FloodtideShark => Get();
        public static int Proj_GreenWater => Get();
        public static int Proj_DarkBall => Get();
        public static int Proj_VolcanicFireball => Get();
        public static int Proj_VolcanicFireballLarge => Get();
        public static int Proj_TerratomereSwordBeam => Get();
        public static int Proj_ExcelsusMain => Get();
        public static int Proj_ExcelsusBlue => Get();
        public static int Proj_ExcelsusPink => Get();
        public static int Proj_MirrorBlast => Get();
        public static int Proj_StormRulerProj => Get();
        public static int Proj_StarnightBeam => Get();
        public static int Proj_PrismaticWave => Get();
        public static int Proj_RSSolarFlare => Get();
        public static int Proj_ReaverHealOrb => Get();
        public static int Proj_BloodBall => Get();
        public static int Proj_GhastlySoulLarge => Get();
        public static int Proj_GhastlySoulMedium => Get();
        public static int Proj_GhastlySoulSmall => Get();
        public static int Proj_UltimusCleaverDust => Get();
        public static int Proj_CausticEdgeProjectile => Get();
        public static int Proj_PrismaticBeam => Get();
        public static int Proj_TerratomereSlashCreator => Get();
        public static int Proj_Voidragon => Get();
        public static int Proj_TorrentialArrow => Get();
        public static int Proj_HallowPointRoundProj => Get();
        public static int Proj_Aquashard => Get();
        public static int Proj_ArcherfishRing => Get();
        public static int Proj_CardHeart => Get();
        public static int Proj_CardSpade => Get();
        public static int Proj_CardDiamond => Get();
        public static int Proj_CardClub => Get();
        public static int Proj_SwordsplosionBlue => Get();
        public static int Proj_SwordsplosionGreen => Get();
        public static int Proj_SwordsplosionPurple => Get();
        public static int Proj_SwordsplosionRed => Get();
        public static int Proj_GalaxyBlast => Get();
        public static int Proj_GalaxyBlastType2 => Get();
        public static int Proj_GalaxyBlastType3 => Get();
        public static int Proj_SCalRitualDrama => Get();
        public static int Proj_VoidFieldGenerator => Get();
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
        #endregion
        #region 增益效果ID引用
        public static int Buff_Plague => Get();
        public static int Buff_SulphuricPoisoning => Get();
        public static int Buff_Dragonfire => Get();
        public static int Buff_Irradiated => Get();
        public static int Buff_ElementalMix => Get();
        public static int Buff_VulnerabilityHex => Get();
        public static int Buff_MarkedforDeath => Get();
        public static int Buff_BrutalCarnage => Get();
        public static int Buff_ArmorCrunch => Get();
        public static int Buff_GodSlayerInferno => Get();
        public static int Buff_Shadowflame => Get();
        public static int Buff_HolyFlames => Get();
        public static int Buff_BrainRot => Get();
        public static int Buff_Nightwither => Get();
        public static int Buff_BurningBlood => Get();
        public static int Buff_TemporalSadness => Get();
        public static int Buff_GlacialState => Get();
        public static int Buff_AstralInfectionDebuff => Get();
        public static int Buff_BrimstoneFlames => Get();
        #endregion
        #region 粒子效果ID引用
        public static int Dust_AstralOrange => Get();
        public static int Dust_AstralBlue => Get();
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
