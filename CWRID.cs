using System.Linq;
using System.Reflection;
using Terraria.ModLoader;

namespace CalamityOverhaul
{
    /// <summary>
    /// 所有用于引用外部模组内部内容的ID集中地
    /// </summary>
    internal static class CWRID
    {
        #region 物品ID引用
        public static int Item_Heresy;
        public static int Item_BurntSienna;
        public static int Item_DubiousPlating;
        public static int Item_Rock;
        public static int Item_BloodOrb;
        public static int Item_Terminus;
        public static int Item_LoreAwakening;
        public static int Item_SquirrelSquireStaff;
        public static int Item_ThrowingBrick;
        public static int Item_Brimlish;
        public static int Item_WaveSkipper;
        public static int Item_ExoPrism;
        public static int Item_DraedonsForge;
        public static int Item_RogueEmblem;
        public static int Item_SnowRuffianMask;
        public static int Item_SnowRuffianChestplate;
        public static int Item_SnowRuffianGreaves;
        public static int Item_PurifiedGel;
        public static int Item_VulcaniteLance;
        public static int Item_Brimlance;
        public static int Item_ContinentalGreatbow;
        public static int Item_BrimstoneFury;
        public static int Item_Helstorm;
        public static int Item_Hellborn;
        public static int Item_AuricBar;
        public static int Item_GrandGuardian;
        public static int Item_SomaPrime;
        public static int Item_Infinity;
        public static int Item_PlagueKeeper;
        public static int Item_Hellkite;
        public static int Item_Contagion;
        public static int Item_PlagueCellCanister;
        public static int Item_PlaguebringerCarapace;
        public static int Item_InfectedArmorPlating;
        public static int Item_PlaguebringerVisor;
        public static int Item_PlaguebringerPistons;
        public static int Item_Lazhar;
        public static int Item_ScoriaBar;
        public static int Item_BloodSample;
        public static int Item_BlightedGel;
        public static int Item_RottenMatter;
        public static int Item_MidasPrime;
        public static int Item_CrackshotColt;
        public static int Item_HolyCollider;
        public static int Item_CelestialClaymore;
        public static int Item_DivineGeode;
        public static int Item_StormlionMandible;
        public static int Item_DiseasedPike;
        public static int Item_HellionFlowerSpear;
        public static int Item_Pandemic;
        public static int Item_SulphurousGrabber;
        public static int Item_TheSyringe;
        public static int Item_PestilentDefiler;
        public static int Item_TheHive;
        public static int Item_BlightSpewer;
        public static int Item_Malevolence;
        public static int Item_PlagueStaff;
        public static int Item_SparklingEmpress;
        public static int Item_SeaPrism;
        public static int Item_PearlShard;
        public static int Item_DragoonDrizzlefish;
        public static int Item_Murasama;
        public static int Item_PlasmaDriveCore;
        public static int Item_MysteriousCircuitry;
        public static int Item_EncryptedSchematicHell;
        public static int Item_LuxorsGift;
        public static int Item_EternalBlizzard;
        public static int Item_Arbalest;
        public static int Item_AshesofCalamity;
        public static int Item_CoreofEleum;
        public static int Item_Condemnation;
        public static int Item_AshesofAnnihilation;
        public static int Item_Vehemence;
        public static int Item_ValkyrieRay;
        public static int Item_Violence;
        public static int Item_Vigilance;
        public static int Item_DeathstareRod;
        public static int Item_ArmoredShell;
        public static int Item_DarkPlasma;
        public static int Item_TwistingNether;
        public static int Item_Excelsus;
        public static int Item_TheObliterator;
        public static int Item_Deathwind;
        public static int Item_DeathhailStaff;
        public static int Item_StaffoftheMechworm;
        public static int Item_Eradicator;
        public static int Item_StarterBag;
        public static int Item_CosmicDischarge;
        public static int Item_Norfleet;
        public static int Item_CosmiliteBar;
        #endregion
        #region NPC ID引用
        public static int NPC_Cataclysm;
        public static int NPC_SepulcherHead;
        public static int NPC_SepulcherBody;
        public static int NPC_SepulcherTail;
        public static int NPC_Yharon;
        public static int NPC_SlimeGodCore;
        public static int NPC_Providence;
        public static int NPC_PlaguebringerGoliath;
        public static int NPC_PerforatorHive;
        public static int NPC_Anahita;
        public static int NPC_Leviathan;
        public static int NPC_HiveMind;
        public static int NPC_DevourerofGodsHead;
        public static int NPC_DevourerofGodsBody;
        public static int NPC_DevourerofGodsTail;
        public static int NPC_Cryogen;
        public static int NPC_Crabulon;
        public static int NPC_BrimstoneElemental;
        public static int NPC_KingSlimeJewelRuby;
        public static int NPC_KingSlimeJewelSapphire;
        public static int NPC_EbonianPaladin;
        public static int NPC_CrimulanPaladin;
        public static int NPC_SplitEbonianPaladin;
        public static int NPC_SplitCrimulanPaladin;
        public static int NPC_Catastrophe;
        public static int NPC_Draedon;
        public static int NPC_RavagerHead2;
        public static int NPC_DarkEnergy;
        public static int NPC_PolterghastHook;
        public static int NPC_CalamitasClone;
        public static int NPC_AquaticScourgeBodyAlt;
        public static int NPC_SupremeCalamitas;
        public static int NPC_ThanatosHead;
        public static int NPC_THIEF;
        public static int NPC_WITCH;
        public static int NPC_SEAHOE;
        public static int NPC_DILF;
        public static int NPC_DesertScourgeHead;
        public static int NPC_AquaticScourgeHead;
        public static int NPC_OldDuke;
        #endregion
        #region 弹幕ID引用
        public static int Proj_CatastropheClaymoreSparkle;
        public static int Proj_AngelicBeam;
        public static int Proj_AstralRound;
        public static int Proj_AstrealArrow;
        public static int Proj_AuralisBullet;
        public static int Proj_CosmicIceBurst;
        public static int Proj_BarinadeArrow;
        public static int Proj_BoltArrow;
        public static int Proj_Nuke;
        public static int Proj_AegisFlame;
        public static int Proj_DarkMasterBeam;
        public static int Proj_DarkMasterClone;
        public static int Proj_OverlyDramaticDukeSummoner;
        public static int Proj_MythrilFlare;
        public static int Proj_Brimlash2;
        public static int Proj_BrimlashProj;
        public static int Proj_BalefulHarvesterProjectile;
        public static int Proj_WaveSkipperProjectile;
        public static int Proj_DeathsAscensionProjectile;
        public static int Proj_DarklightGreatswordSlashCreator;
        public static int Proj_DarkBeam;
        public static int Proj_CometQuasherMeteor;
        public static int Proj_DesertScourgeSpit;
        public static int Proj_Razorwind;
        public static int Proj_Brimblast;
        public static int Proj_IceBombFriendly;
        public static int Proj_AtaraxiaBoom;
        public static int Proj_AtaraxiaMain;
        public static int Proj_AtaraxiaSide;
        public static int Proj_BrimstoneBoom;
        public static int Proj_AftershockRock;
        public static int Proj_UniversalGenesisStar;
        public static int Proj_UniversalGenesisStarcaller;
        public static int Proj_UltimaBolt;
        public static int Proj_UltimaRay;
        public static int Proj_UltimaSpark;
        public static int Proj_Bolt;
        public static int Proj_TelluricGlareArrow;
        public static int Proj_SulphuricBlast;
        public static int Proj_StormSurgeTornado;
        public static int Proj_SputterCometBig;
        public static int Proj_PlasmaBlast;
        public static int Proj_AstralStar;
        public static int Proj_SpykerProj;
        public static int Proj_LostSoulFriendly;
        public static int Proj_Shroom;
        public static int Proj_SeasSearingBubble;
        public static int Proj_SeasSearingSecondary;
        public static int Proj_ArcherfishShot;
        public static int Proj_FishronRPG;
        public static int Proj_ImpactRound;
        public static int Proj_PristineSecondary;
        public static int Proj_PristineFire;
        public static int Proj_PlanarRipperBolt;
        public static int Proj_PlagueTaintedDrone;
        public static int Proj_PlagueTaintedProjectile;
        public static int Proj_ShockblastRound;
        public static int Proj_P90Round;
        public static int Proj_EmesisGore;
        public static int Proj_FlakKrakenProjectile;
        public static int Proj_FlakToxicannonProjectile;
        public static int Proj_FeatherLarge;
        public static int Proj_SlimeStream;
        public static int Proj_ChargedBlast;
        public static int Proj_AuricBullet;
        public static int Proj_AquaBlast;
        public static int Proj_AquaBlastToxic;
        public static int Proj_PlagueArrow;
        public static int Proj_NitroShot;
        public static int Proj_MarksmanShot;
        public static int Proj_RicoshotCoin;
        public static int Proj_MineralMortarProjectile;
        public static int Proj_MiniSharkron;
        public static int Proj_TyphoonArrow;
        public static int Proj_IcicleArrowProj;
        public static int Proj_DrataliornusFlame;
        public static int Proj_VanquisherArrowProj;
        public static int Proj_DaemonsFlameArrow;
        public static int Proj_VernalBolt;
        public static int Proj_CorrodedShell;
        public static int Proj_RealmRavagerBullet;
        public static int Proj_CorinthPrimeAirburstGrenade;
        public static int Proj_SmallCoral;
        public static int Proj_LeafArrow;
        public static int Proj_BrimstoneBolt;
        public static int Proj_ClamorRifleProj;
        public static int Proj_ClaretCannonProj;
        public static int Proj_CondemnationArrowHoming;
        public static int Proj_FlurrystormIceChunk;
        public static int Proj_SquirrelSquireAcorn;
        public static int Proj_DracoBeam;
        public static int Proj_EarthProj;
        public static int Proj_FossilShard;
        public static int Proj_GalacticaComet;
        public static int Proj_ThornBase;
        public static int Proj_Flarefrost;
        public static int Proj_FloodtideShark;
        public static int Proj_GreenWater;
        public static int Proj_DarkBall;
        public static int Proj_VolcanicFireball;
        public static int Proj_VolcanicFireballLarge;
        public static int Proj_TerratomereSwordBeam;
        public static int Proj_ExcelsusMain;
        public static int Proj_ExcelsusBlue;
        public static int Proj_ExcelsusPink;
        public static int Proj_MirrorBlast;
        public static int Proj_StormRulerProj;
        public static int Proj_StarnightBeam;
        public static int Proj_PrismaticWave;
        public static int Proj_RSSolarFlare;
        public static int Proj_ReaverHealOrb;
        public static int Proj_BloodBall;
        public static int Proj_GhastlySoulLarge;
        public static int Proj_GhastlySoulMedium;
        public static int Proj_GhastlySoulSmall;
        public static int Proj_UltimusCleaverDust;
        public static int Proj_CausticEdgeProjectile;
        public static int Proj_PrismaticBeam;
        public static int Proj_TerratomereSlashCreator;
        public static int Proj_Voidragon;
        public static int Proj_TorrentialArrow;
        public static int Proj_HallowPointRoundProj;
        public static int Proj_Aquashard;
        public static int Proj_ArcherfishRing;
        public static int Proj_CardHeart;
        public static int Proj_CardSpade;
        public static int Proj_CardDiamond;
        public static int Proj_CardClub;
        #endregion
        #region 物块ID引用
        public static int Tile_PlagueInfuser;
        public static int Tile_DraedonsForge;
        public static int Tile_SCalAltar;
        public static int Tile_SCalAltarLarge;
        public static int Tile_SulphurousSand;
        public static int Tile_SulphurousSandstone;
        #endregion
        #region 增益效果ID引用
        public static int Buff_Plague;
        public static int Buff_ElementalMix;
        public static int Buff_VulnerabilityHex;
        public static int Buff_MarkedforDeath;
        public static int Buff_BrutalCarnage;
        public static int Buff_ArmorCrunch;
        public static int Buff_GodSlayerInferno;
        public static int Buff_Shadowflame;
        public static int Buff_HolyFlames;
        public static int Buff_BrainRot;
        public static int Buff_Nightwither;
        public static int Buff_BurningBlood;
        public static int Buff_TemporalSadness;
        public static int Buff_GlacialState;
        public static int Buff_AstralInfectionDebuff;
        public static int Buff_BrimstoneFlames;
        #endregion
        #region 粒子效果ID引用
        public static int Dust_AstralOrange;
        public static int Dust_AstralBlue;
        public readonly static int Dust_Brimstone = 235;//灾厄使用夺命杖的粒子作为硫磺火焰粒子，因为这个比较特殊，就不通过反射加载了，直接写上readonly
        #endregion
        #region 物品组ID引用
        public readonly static int ItemGroup_RogueWeapon = 570;//盗贼武器物品组ID，因为这个比较特殊，就不通过反射加载了，直接写上readonly
        #endregion

        #region 加载数据
        internal static void Setup() {
            const string calamityModName = "CalamityMod";
            FieldInfo[] fields = typeof(CWRID).GetFields(BindingFlags.Public | BindingFlags.Static);
            foreach (FieldInfo field in fields) {
                if (field.FieldType != typeof(int)) {
                    continue;
                }
                if (field.IsInitOnly) {
                    continue;
                }
                string fieldName = field.Name;
                string[] parts = fieldName.Split('_');
                if (parts.Length < 2) {
                    continue;
                }
                string prefix = parts[0];
                string typeName = string.Join("", parts.Skip(1));
                int value = 0;
                bool found = false;
                switch (prefix) {
                    case "Item":
                        if (ModContent.TryFind(calamityModName, typeName, out ModItem modItem)) {
                            value = modItem.Type;
                            found = true;
                        }
                        break;
                    case "NPC":
                        if (ModContent.TryFind(calamityModName, typeName, out ModNPC modNPC)) {
                            value = modNPC.Type;
                            found = true;
                        }
                        break;
                    case "Proj":
                        if (ModContent.TryFind(calamityModName, typeName, out ModProjectile modProjectile)) {
                            value = modProjectile.Type;
                            found = true;
                        }
                        break;
                    case "Tile":
                        if (ModContent.TryFind(calamityModName, typeName, out ModTile modTile)) {
                            value = modTile.Type;
                            found = true;
                        }
                        break;
                    case "Buff":
                        if (ModContent.TryFind(calamityModName, typeName, out ModBuff modBuff)) {
                            value = modBuff.Type;
                            found = true;
                        }
                        break;
                    case "Dust":
                        if (ModContent.TryFind(calamityModName, typeName, out ModDust modDust)) {
                            value = modDust.Type;
                            found = true;
                        }
                        break;
                }
                if (found) {
                    field.SetValue(null, value);
                }
                else {
                    CWRMod.Instance.Logger.Warn($"[CWRID:SetupData] Unknown typeName '{typeName}' in field '{fieldName}'.");
                }
            }
        }
        #endregion

        #region 卸载数据
        public static void UnLoad() {
            FieldInfo[] fields = typeof(CWRID).GetFields(BindingFlags.Public | BindingFlags.Static);
            foreach (FieldInfo field in fields) {
                if (field.FieldType != typeof(int)) {
                    continue;
                }
                if (field.IsInitOnly) {
                    continue;
                }
                field.SetValue(null, 0);
            }
        }
        #endregion
    }
}
