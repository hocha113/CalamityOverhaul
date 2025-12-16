using CalamityMod;
using CalamityMod.CalPlayer;
using CalamityMod.CustomRecipes;
using CalamityMod.DataStructures;
using CalamityMod.Events;
using CalamityMod.Graphics.Metaballs;
using CalamityMod.Items.Weapons.Magic;
using CalamityMod.NPCs;
using CalamityMod.NPCs.ExoMechs;
using CalamityMod.NPCs.ExoMechs.Apollo;
using CalamityMod.NPCs.ExoMechs.Ares;
using CalamityMod.NPCs.ExoMechs.Artemis;
using CalamityMod.NPCs.OldDuke;
using CalamityMod.NPCs.SupremeCalamitas;
using CalamityMod.NPCs.TownNPCs;
using CalamityMod.Particles;
using CalamityMod.Projectiles;
using CalamityMod.TileEntities;
using CalamityMod.Tiles.Furniture.CraftingStations;
using CalamityMod.World;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.RemakeItems;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul
{
    /// <summary>
    /// 一个用于访问Calamity Mod内部内容的静态类
    /// </summary>
    [CWRJITEnabled]
    internal static class CWRRef
    {
        /// <summary>
        /// 待用，检查Calamity Mod是否已加载，或者版本是否合适
        /// </summary>
        public static bool Has => ModLoader.HasMod("CalamityMod");
        public static bool GetDownedPrimordialWyrm() => DownedBossSystem.downedPrimordialWyrm;
        public static void SetDownedPrimordialWyrm(bool value) => DownedBossSystem.downedPrimordialWyrm = value;
        public static bool GetDeathMode() => CalamityWorld.death;
        public static bool GetRevengeMode() => CalamityWorld.revenge;
        public static bool GetBossRushActive() => BossRushEvent.BossRushActive;
        public static void SetBossRushActive(bool value) => BossRushEvent.BossRushActive = value;
        public static bool GetAcidRainEventIsOngoing() => AcidRainEvent.AcidRainEventIsOngoing;
        public static DamageClass GetTrueMeleeDamageClass() => ModContent.GetInstance<TrueMeleeDamageClass>();
        public static DamageClass GetTrueMeleeNoSpeedDamageClass() => ModContent.GetInstance<TrueMeleeNoSpeedDamageClass>();
        public static DamageClass GetMeleeRangedHybridDamageClass() => ModContent.GetInstance<MeleeRangedHybridDamageClass>();
        public static float ChargeRatio(Item item) => item.Calamity().ChargeRatio;
        public static bool GetNPCIsAnEnemy(this NPC npc) => npc.IsAnEnemy();
        public static void SetPlayerWarbannerOfTheSun(this Player player, bool value) => player.Calamity().warbannerOfTheSun = value;
        public static bool GetPlayerBladeArmEnchant(this Player player) => player.Calamity().bladeArmEnchant;
        public static bool GetPlayerAdrenalineMode(this Player player) => player.Calamity().adrenalineModeActive;
        public static void SetProjPointBlankShotDuration(this Projectile projectile, int value) => projectile.Calamity().pointBlankShotDuration = value;
        public static int GetRandomProjectileType() {
            return Main.rand.Next(4) switch {
                0 => CWRID.Proj_SwordsplosionBlue,
                1 => CWRID.Proj_SwordsplosionGreen,
                2 => CWRID.Proj_SwordsplosionPurple,
                3 => CWRID.Proj_SwordsplosionRed,
                _ => CWRID.Proj_SwordsplosionBlue,
            };
        }
        public static int GetRandomProjectileType2() {
            return Main.rand.Next(6) switch {
                1 => CWRID.Proj_GalaxyBlast,
                2 => CWRID.Proj_GalaxyBlastType2,
                3 => CWRID.Proj_GalaxyBlastType3,
                _ => CWRID.Proj_GalaxyBlast,
            };
        }
        public static int GetProjectileTypeByIndex(int index) {
            return index switch {
                0 => CWRID.Proj_GalaxyBlast,
                1 => CWRID.Proj_GalaxyBlastType2,
                2 => CWRID.Proj_GalaxyBlastType3,
                _ => CWRID.Proj_GalaxyBlast,
            };
        }
        public static void UpdateRogueStealth(Player player) {
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
            SoundEngine.PlaySound(SCalAltar.SummonSound, spawnPos);
            Projectile.NewProjectile(new EntitySource_WorldEvent(), spawnPos, Vector2.Zero
                , CWRID.Proj_SCalRitualDrama, 0, 0f, Main.myPlayer, 0, 0);
        }
        public static void SummonExo(int exoType, Player player) {
            if (CWRMod.Instance.calamity == null) {
                return;
            }
            CalamityWorld.DraedonMechToSummon = (ExoMech)exoType;
            if (VaultUtils.isClient) {//客户端发送网络数据到服务器
                var netMessage = CWRMod.Instance.calamity.GetPacket();
                netMessage.Write((byte)CalamityModMessageType.ExoMechSelection);
                netMessage.Write((int)CalamityWorld.DraedonMechToSummon);
                netMessage.Send();
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
                    CalamityUtils.SpawnBossBetter(aresSpawnPosition, ModContent.NPCType<AresBody>());
                    break;

                case ExoMech.Twins:
                    Vector2 artemisSpawnPosition = player.Center + new Vector2(-1100f, -1600f);
                    Vector2 apolloSpawnPosition = player.Center + new Vector2(1100f, -1600f);
                    CalamityUtils.SpawnBossBetter(artemisSpawnPosition, ModContent.NPCType<Artemis>());
                    CalamityUtils.SpawnBossBetter(apolloSpawnPosition, ModContent.NPCType<Apollo>());
                    break;
            }
        }
        public static int GetCurrentSeason() {
            DateTime date = DateTime.Now;
            int day = date.DayOfYear - Convert.ToInt32(DateTime.IsLeapYear(date.Year) && date.DayOfYear > 59);

            if (day < 80 || day >= 355) {
                return 0;
            }

            else if (day >= 80 && day < 172) {
                return 1;
            }

            else if (day >= 172 && day < 266) {
                return 2;
            }

            else {
                return 3;
            }
        }
        public static void SpawnMediumMistParticle(Vector2 smokePos, Vector2 smokeVel, bool Smoketype) {
            Particle smoke = new MediumMistParticle(smokePos, smokeVel, new Color(255, 110, 50), Color.OrangeRed
                    , Smoketype ? Main.rand.NextFloat(0.4f, 0.75f) : Main.rand.NextFloat(1.5f, 2f), 220 - Main.rand.Next(50), 0.1f);
            GeneralParticleHandler.SpawnParticle(smoke);
        }
        public static void DrawAfterimagesCentered(Projectile proj, int mode, Color lightColor, int typeOneIncrement = 1, Texture2D texture = null, bool drawCentered = true) => CalamityUtils.DrawAfterimagesCentered(proj, mode, lightColor, typeOneIncrement, texture, drawCentered);
        public static void HomeInOnNPC(Projectile projectile, bool ignoreTiles, float distanceRequired, float homingVelocity, float inertia) => CalamityUtils.HomeInOnNPC(projectile, ignoreTiles, distanceRequired, homingVelocity, inertia);
        public static void SpawnLifeStealProjectile(Projectile projectile, Player player, float healAmount, int healProjectileType, float distanceRequired, float cooldownMultiplier = 1f)
            => CalamityGlobalProjectile.SpawnLifeStealProjectile(projectile, player, healAmount, healProjectileType, distanceRequired, cooldownMultiplier);
        public static Projectile ProjectileBarrage(IEntitySource source, Vector2 originVec, Vector2 targetPos, bool fromRight, float xOffsetMin, float xOffsetMax
            , float yOffsetMin, float yOffsetMax, float projSpeed, int projType, int damage, float knockback, int owner, bool clamped = false, float inaccuracyOffset = 5f)
            => CalamityUtils.ProjectileBarrage(source, originVec, targetPos, fromRight, xOffsetMin, xOffsetMax
                , yOffsetMin, yOffsetMax, projSpeed, projType, damage, knockback, owner, clamped, inaccuracyOffset);
        public static void SetDraedonDefeatTimer(NPC npc, float value) {
            if (npc.ModNPC is Draedon draedon) {
                draedon.DefeatTimer = value;
            }
        }
        public static float GetDraedonDefeatTimer(NPC npc) {
            if (npc.ModNPC is Draedon draedon) {
                return draedon.DefeatTimer;
            }
            return 0f;
        }
        public static List<int> GetPierceResistExceptionList() => CalamityLists.projectileDestroyExceptionList;
        public static bool HasExo() => Draedon.ExoMechIsPresent;
        public static int GetCalItemID(this string key) => CWRItemOverride.GetCalItemID(key);
        public static void SetAbleToSelectExoMech(Player player, bool value) {
            player.Calamity().AbleToSelectExoMech = value;
        }
        public static void SetProjtimesPierced(this Projectile projectile, int value) => projectile.Calamity().timesPierced = value;
        public static ref int GetMurasamaHitCooldown(this Player player) => ref player.Calamity().murasamaHitCooldown;
        public static void SetBrimstoneBullets(this Projectile projectile, bool value) => projectile.Calamity().brimstoneBullets = value;
        public static void SetDeepcoreBullet(this Projectile projectile, bool value) => projectile.Calamity().deepcoreBullet = value;
        public static void SetAllProjectilesHome(this Projectile projectile, bool value) => projectile.Calamity().allProjectilesHome = value;
        public static void SetBetterLifeBullet1(this Projectile projectile, bool value) => projectile.Calamity().betterLifeBullet1 = value;
        public static void SetBetterLifeBullet2(this Projectile projectile, bool value) => projectile.Calamity().betterLifeBullet2 = value;
        public static Vector2 GetCoinTossVelocity(Player player) => player.GetCoinTossVelocity();
        public static bool GetAlchFlask(this Player player) => player.Calamity().alchFlask;
        public static bool GetSpiritOrigin(this Player player) => player.Calamity().spiritOrigin;
        public static void Spawn_PristineFury_Effect(Vector2 spawnPos, Vector2 vel) {
            CritSpark spark = new(spawnPos, vel, Main.rand.NextBool() ? Color.DarkOrange : Color.OrangeRed, Color.OrangeRed, 0.9f, 18, 2f, 1.9f);
            GeneralParticleHandler.SpawnParticle(spark);
        }
        public static void SetProjCGP(int proj) {
            CalamityGlobalProjectile cgp = Main.projectile[proj].Calamity();
            cgp.supercritHits = -1;
            cgp.appliesSomaShred = true;
        }
        public static void Spawn_Effect_1(Vector2 spawnPos, Vector2 vel) {
            Particle spark2 = new LineParticle(spawnPos, vel, false, Main.rand.Next(15, 25 + 1), Main.rand.NextFloat(1.5f, 2f), Main.rand.NextBool() ? Color.MediumOrchid : Color.DarkViolet);
            GeneralParticleHandler.SpawnParticle(spark2);
        }
        public static void Spawn_Effect_2(Vector2 spawnPos, Vector2 vel, int sparkLifetime, float sparkScale, Color sparkColor) {
            SparkParticle spark = new SparkParticle(spawnPos, vel, false, sparkLifetime, sparkScale, sparkColor);
            GeneralParticleHandler.SpawnParticle(spark);
        }
        public static bool GetDownedCalamitas() {
            return DownedBossSystem.downedCalamitas;
        }
        public static void SetDownedCalamitas(bool value) {
            DownedBossSystem.downedCalamitas = value;
        }
        public static void SetDownedBoomerDuke(bool value) => DownedBossSystem.downedBoomerDuke = value;
        public static bool GetDownedBoomerDuke() => DownedBossSystem.downedBoomerDuke;
        public static bool GetSupCalPermafrost(NPC npc) {
            if (npc.ModNPC is SupremeCalamitas supCal) {
                return supCal.permafrost;
            }
            return false;
        }
        public static SoundStyle GetSound(this string path) {
            if (ModContent.HasAsset(path)) {
                return new SoundStyle(path);
            }
            return CWRSound.None;
        }
        public static bool GetDownedThanatos() => DownedBossSystem.downedThanatos;
        public static void SetSupCalPermafrost(NPC npc, bool value) {
            if (npc.ModNPC is SupremeCalamitas supCal) {
                supCal.permafrost = value;
            }
        }
        public static int GetSupCalGiveUpCounter(NPC npc) {
            if (npc.ModNPC is SupremeCalamitas supCal) {
                return supCal.giveUpCounter;
            }
            return 0;
        }
        public static void SetSupCalGiveUpCounter(NPC npc, int value) {
            if (npc.ModNPC is SupremeCalamitas supCal) {
                supCal.giveUpCounter = value;
            }
        }
        public static Type GetItem_SHPC_Type() => typeof(SHPC);
        public static Type GetNPC_WITCH_Type() => typeof(WITCH);
        public static Type GetNPC_SupCal_Type() => typeof(SupremeCalamitas);
        public static bool GetEarlyHardmodeProgressionReworkBool() => CalamityServerConfig.Instance.EarlyHardmodeProgressionRework;
        public static bool GetAfterimages() => CalamityClientConfig.Instance.Afterimages;
        public static int GetProjectileDamage(NPC npc, int projType) => npc.GetProjectileDamage(projType);
        public static void SetPlayerInfiniteFlight(this Player player, bool value) => player.Calamity().infiniteFlight = value;
        public static bool GetPlayerStealthStrikeAvailable(this Player player) => player.Calamity().StealthStrikeAvailable();
        public static void SetProjStealthStrike(this Projectile projectile, bool value) => projectile.Calamity().stealthStrike = value;
        public static void HorsemansBladeOnHit(Player player, int targetIdx, int damage, float knockback
            , int extraUpdateAmt = 0, int type = ProjectileID.FlamingJack)
            => CalamityPlayer.HorsemansBladeOnHit(player, targetIdx, damage, knockback, extraUpdateAmt, type);
        public static void SetItemCanFirePointBlankShots(this Item item, bool value) => item.Calamity().canFirePointBlankShots = value;
        public static bool GetProjStealthStrike(this Projectile projectile) => projectile.Calamity().stealthStrike;
        public static void OldDukeOnKill(NPC npc) {
            StopAcidRain();
            CalamityGlobalNPC.SetNewBossJustDowned(npc);
            DownedBossSystem.downedBoomerDuke = true;
            AcidRainEvent.OldDukeHasBeenEncountered = true;
            if (npc.ModNPC is not null && npc.ModNPC is OldDuke oldDuke) {
                oldDuke.OnKill();
            }
        }
        public static void StopAcidRain() {
            AcidRainEvent.AccumulatedKillPoints = 0;
            AcidRainEvent.UpdateInvasion(win: true);
        }
        public static void StarRT(Projectile projectile, Entity target) {
            if (!VaultUtils.isServer) {
                Color color = Color.Lerp(Color.Cyan, Color.White, Main.rand.NextFloat(0.3f, 0.64f));
                GeneralParticleHandler.SpawnParticle(new ImpactParticle(Vector2.Lerp(projectile.Center, target.Center, 0.65f), 0.1f, 20, Main.rand.NextFloat(0.4f, 0.5f), color));
                for (int i = 0; i < 20; i++) {
                    Vector2 spawnPosition = target.Center + Main.rand.NextVector2Circular(30f, 30f);
                    StreamGougeMetaball.SpawnParticle(spawnPosition, Main.rand.NextVector2Circular(3f, 3f), 60f);

                    float scale = MathHelper.Lerp(24f, 64f, CalamityUtils.Convert01To010(i / 19f));
                    spawnPosition = target.Center + projectile.velocity.SafeNormalize(Vector2.UnitY) * MathHelper.Lerp(-40f, 90f, i / 19f);
                    Vector2 particleVelocity = projectile.velocity.SafeNormalize(Vector2.UnitY).RotatedByRandom(0.23f) * Main.rand.NextFloat(2.5f, 9f);
                    StreamGougeMetaball.SpawnParticle(spawnPosition, particleVelocity, scale);
                }
            }
        }
        public static void SpanFire(Entity entity) {
            bool LowVel = Main.rand.NextBool() ? false : true;
            FlameParticle ballFire = new FlameParticle(entity.Center + VaultUtils.RandVr(entity.width / 2)
                , Main.rand.Next(13, 22), Main.rand.NextFloat(0.1f, 0.22f), Main.rand.NextFloat(0.02f, 0.07f), Color.Gold, Color.DarkRed) {
                Velocity = new Vector2(entity.velocity.X * 0.8f, -10).RotatedByRandom(0.005f)
                * (LowVel ? Main.rand.NextFloat(0.4f, 0.65f) : Main.rand.NextFloat(0.8f, 1f))
            };
            GeneralParticleHandler.SpawnParticle(ballFire);
        }
        public static ref float RefItemCharge(this Item item) => ref item.Calamity().Charge;
        public static float GetItemMaxCharge(this Item item) => item.Calamity().MaxCharge;
        public static bool GetItemUsesCharge(this Item item) => item.Calamity().UsesCharge;
        public static RogueDamageClass GetRogueDamageClass() => ModContent.GetInstance<RogueDamageClass>();
        public static float GetPlayerRogueStealth(this Player player) => player.Calamity().rogueStealth;
        public static float SetPlayerRogueStealth(this Player player, float value) => player.Calamity().rogueStealth = value;
        public static float GetPlayerRogueStealthMax(this Player player) => player.Calamity().rogueStealthMax;
        public static ref float RefPlayerRogueStealthMax(this Player player) => ref player.Calamity().rogueStealthMax;
        public static bool GetPlayerZoneSulphur(this Player player) => player.Calamity().ZoneSulphur;
        public static bool GetPlayerZoneAbyss(this Player player) => player.Calamity().ZoneAbyss;
        public static bool GetPlayerProfanedCrystalBuffs(this Player player) => player.Calamity().profanedCrystalBuffs;
        public static void SetPlayerDashID(this Player player, string value) => player.Calamity().DashID = value;
        public static void SetNSMBPlayer(Player player) {
            CalamityPlayer calPlayer = player.Calamity();
            calPlayer.rangedAmmoCost *= 0.8f;
            calPlayer.deadshotBrooch = true;
            calPlayer.dynamoStemCells = true;
            calPlayer.MiniSwarmers = true;
            calPlayer.eleResist = true;
            calPlayer.voidField = true;
        }
        public static LocalizedText ConstructRecipeCondition(int tier, out Func<bool> condition) => ArsenalTierGatedRecipe.ConstructRecipeCondition(tier, out condition);
        public static IList<Type> GetTEBaseTurretTypes() => VaultUtils.GetDerivedTypes<TEBaseTurret>();
        public static int GetSeasonDustID() {
            return CalamityMod.CalamityMod.CurrentSeason switch {
                Season.Spring => Utils.SelectRandom(Main.rand, 245, 157, 107), //春季：绿色系尘埃
                Season.Summer => Utils.SelectRandom(Main.rand, 247, 228, 57),  //夏季：黄色系尘埃
                Season.Fall => Utils.SelectRandom(Main.rand, 6, 259, 158),     //秋季：橙色系尘埃
                Season.Winter => Utils.SelectRandom(Main.rand, 67, 229, 185),  //冬季：蓝色系尘埃
                _ => 0                                                         //默认值：无效尘埃类型
            };
        }
        public static void DrawStarTrail(Projectile projectile, Color outer, Color inner, float auraHeight = 10f) => CalamityUtils.DrawStarTrail(projectile, outer, inner, auraHeight);
        public static int GetProjectileDamage(Projectile projectile, int projType) => projectile.GetProjectileDamage(projType);
        public static void SpawnDestroyerPRTEffect(NPC npc, float value, float value2, int idleTime) {
            if (value != 0 || Main.dedServ) {
                return;
            }
            Color telegraphColor;
            Particle spark;

            switch (value2 % 3) {
                case 0:
                    telegraphColor = Color.Red;
                    spark = new DestroyerReticleTelegraph(npc, telegraphColor, 1.5f, 0.15f, idleTime + 20);
                    GeneralParticleHandler.SpawnParticle(spark);
                    break;
                case 1:
                    telegraphColor = Color.Green;
                    spark = new DestroyerSparkTelegraph(npc, telegraphColor * 2f, Color.White, 3f, idleTime + 20,
                            Main.rand.NextFloat(MathHelper.ToRadians(3f)) * Main.rand.NextBool().ToDirectionInt());
                    GeneralParticleHandler.SpawnParticle(spark);
                    break;
                case 2:
                    telegraphColor = Color.Cyan;
                    spark = new DestroyerSparkTelegraph(npc, telegraphColor * 2f, Color.White, 3f, idleTime + 20,
                            Main.rand.NextFloat(MathHelper.ToRadians(3f)) * Main.rand.NextBool().ToDirectionInt());
                    GeneralParticleHandler.SpawnParticle(spark);
                    break;
            }
        }
        public static void SyncVanillaLocalAI(NPC npc) => CalamityUtils.SyncVanillaLocalAI(npc);
        public static ref float[] RefNPCNewAI(this NPC npc) => ref npc.Calamity().newAI;
        public static ref bool RefNPCCurrentlyEnraged(this NPC npc) => ref npc.Calamity().CurrentlyEnraged;
        public static ref bool RefNPCCurrentlyIncreasingDefenseOrDR(this NPC npc) => ref npc.Calamity().CurrentlyIncreasingDefenseOrDR;
        public static void DarkIceBombEffect1(Projectile Projectile, float Time, float targetDist) {
            if (Projectile.timeLeft % 2 == 0 && Time > 5f && targetDist < 1400f) {
                AltSparkParticle spark = new(Projectile.Center, Projectile.velocity * 0.05f, false, 8, 2.3f, Color.DarkBlue);
                GeneralParticleHandler.SpawnParticle(spark);
            }

            if (Main.rand.NextBool(3) && Time > 5f && targetDist < 1400f) {
                Particle orb = new GenericBloom(Projectile.Center + Main.rand.NextVector2Circular(10, 10)
                    , Projectile.velocity * Main.rand.NextFloat(0.05f, 0.5f), Color.WhiteSmoke, Main.rand.NextFloat(0.2f, 0.45f), Main.rand.Next(6, 9), true, false);
                GeneralParticleHandler.SpawnParticle(orb);
            }

            if (Projectile.timeLeft % 2 == 0 && Time > 5f && targetDist < 1400f) {
                LineParticle spark2 = new(Projectile.Center, -Projectile.velocity * 0.05f, false, 10, 1.7f, Color.AliceBlue);
                GeneralParticleHandler.SpawnParticle(spark2);
            }
        }
        public static void DarkIceBombEffect2(Projectile Projectile, Vector2 randVr) {
            AltSparkParticle spark = new(Projectile.Center, randVr, true, 12, Main.rand.NextFloat(1.3f, 2.2f), Color.Blue);
            GeneralParticleHandler.SpawnParticle(spark);
            AltSparkParticle spark2 = new(Projectile.Center, randVr, false, 9, Main.rand.NextFloat(1.1f, 1.5f), Color.AntiqueWhite);
            GeneralParticleHandler.SpawnParticle(spark2);
        }
        public static void StellarStrikerBeamEffect(Projectile Projectile, float Time, float targetDist) {
            if (Projectile.timeLeft % 2 == 0 && Time > 5f && targetDist < 1400f) {
                AltSparkParticle spark = new(Projectile.Center, Projectile.velocity * 0.05f, false, 4, 2.3f, new Color(68, 153, 112));
                GeneralParticleHandler.SpawnParticle(spark);
            }

            if (Projectile.timeLeft % 2 == 0 && Time > 5f && targetDist < 1400f) {
                LineParticle spark2 = new(Projectile.Center, -Projectile.velocity * 0.05f, false, 6, 1.7f, new Color(95, 200, 157));
                GeneralParticleHandler.SpawnParticle(spark2);
            }
        }
        public static void NurgleBeeEffect(Projectile Projectile, bool LowVel) {
            FlameParticle fire = new FlameParticle(Projectile.Center + VaultUtils.RandVr(13), 20, Main.rand.NextFloat(0.1f, 0.3f), 0.05f
            , Color.YellowGreen * (LowVel ? 1.2f : 0.5f), Color.DarkGreen * (LowVel ? 1.2f : 0.5f)) {
                Velocity = new Vector2(Projectile.velocity.X * 0.8f, -10).RotatedByRandom(0.005f)
            * (LowVel ? Main.rand.NextFloat(0.4f, 0.65f) : Main.rand.NextFloat(0.8f, 1f))
            };
            GeneralParticleHandler.SpawnParticle(fire);
        }
        public static void CosmicFireEffect(Projectile Projectile) {
            StreamGougeMetaball.SpawnParticle(Projectile.Center + VaultUtils.RandVr(13), Projectile.velocity, Main.rand.NextFloat(11.3f, 21.5f));
        }
        public static void DragonsWordEffect(bool alt, Projectile Projectile, float Time, float targetDist) {
            if (alt) {
                float OrbSize = Main.rand.NextFloat(0.5f, 0.8f);
                Particle orb = new GenericBloom(Projectile.Center, Vector2.Zero, Color.OrangeRed, OrbSize + 0.6f, 8, true);
                GeneralParticleHandler.SpawnParticle(orb);
                Particle orb2 = new GenericBloom(Projectile.Center, Vector2.Zero, Color.White, OrbSize + 0.2f, 8, true);
                GeneralParticleHandler.SpawnParticle(orb2);
            }
            else {
                if (Time % 5 == 0 && Time > 35f && targetDist < 1400f) {
                    SparkParticle spark = new SparkParticle(Projectile.Center + Main.rand.NextVector2Circular(1 + Time * 0.1f, 1 + Time * 0.1f)
                        , -Projectile.velocity * 0.5f, false, 15, Main.rand.NextFloat(0.4f, 0.7f), Main.rand.NextBool() ? Color.DarkOrange : Color.OrangeRed);
                    GeneralParticleHandler.SpawnParticle(spark);
                }
                if (targetDist < 1400f) {
                    ModContent.GetInstance<DragonsBreathFlameMetaball2>().SpawnParticle(Projectile.Center, Time * 0.1f + 0.2f);
                    ModContent.GetInstance<DragonsBreathFlameMetaball>().SpawnParticle(Projectile.Center + Projectile.velocity, Time * 0.09f + 0.15f);
                }
            }
        }
        public static void AstralPikeBeamEffect(Projectile Projectile) {
            LineParticle spark2 = new LineParticle(Projectile.Center, -Projectile.velocity * 0.05f, false, 17, 1.7f, Color.Goldenrod);
            GeneralParticleHandler.SpawnParticle(spark2);
        }
        public static void FadingGloryRapierHitDustEffect(Projectile Projectile, NPC npc) {
            Vector2 bloodSpawnPosition = npc.Center + Main.rand.NextVector2Circular(npc.width, npc.height) * 0.04f;
            Vector2 splatterDirection = (Projectile.Center - bloodSpawnPosition).SafeNormalize(Vector2.UnitY);
            if (CWRLoad.NPCValue.ISTheofSteel(npc)) {
                for (int j = 0; j < 3; j++) {
                    float sparkScale = Main.rand.NextFloat(1.2f, 2.33f);
                    int sparkLifetime = Main.rand.Next(22, 36);
                    Color sparkColor = Color.Lerp(Color.Silver, Color.Gold, Main.rand.NextFloat(0.7f));
                    Vector2 sparkVelocity = splatterDirection.RotatedByRandom(0.9f) * Main.rand.NextFloat(19f, 34.5f);
                    SparkParticle spark = new SparkParticle(bloodSpawnPosition, sparkVelocity, true, sparkLifetime, sparkScale, sparkColor);
                    GeneralParticleHandler.SpawnParticle(spark);
                }
            }
            else {
                for (int i = 0; i < 6; i++) {
                    int bloodLifetime = Main.rand.Next(22, 36);
                    float bloodScale = Main.rand.NextFloat(0.6f, 0.8f);
                    Color bloodColor = Color.Lerp(Color.Red, Color.DarkRed, Main.rand.NextFloat());
                    bloodColor = Color.Lerp(bloodColor, new Color(51, 22, 94), Main.rand.NextFloat(0.65f));

                    if (Main.rand.NextBool(20))
                        bloodScale *= 2f;

                    Vector2 bloodVelocity = splatterDirection.RotatedByRandom(0.81f) * Main.rand.NextFloat(11f, 23f);
                    bloodVelocity.Y -= 12f;
                    BloodParticle blood = new BloodParticle(bloodSpawnPosition, bloodVelocity, bloodLifetime, bloodScale, bloodColor);
                    GeneralParticleHandler.SpawnParticle(blood);
                }
                for (int i = 0; i < 3; i++) {
                    float bloodScale = Main.rand.NextFloat(0.2f, 0.33f);
                    Color bloodColor = Color.Lerp(Color.Red, Color.DarkRed, Main.rand.NextFloat(0.5f, 1f));
                    Vector2 bloodVelocity = splatterDirection.RotatedByRandom(0.9f) * Main.rand.NextFloat(9f, 14.5f);
                    BloodParticle2 blood = new BloodParticle2(bloodSpawnPosition, bloodVelocity, 20, bloodScale, bloodColor);
                    GeneralParticleHandler.SpawnParticle(blood);
                }
            }
        }
        public static Projectile ProjectileRain(IEntitySource source, Vector2 targetPos, float xLimit
            , float xVariance, float yLimitLower, float yLimitUpper, float projSpeed, int projType, int damage, float knockback, int owner)
            => CalamityUtils.ProjectileRain(source, targetPos, xLimit, xVariance, yLimitLower
                , yLimitUpper, projSpeed, projType, damage, knockback, owner);
        public static List<Vector2> BezierCurveGetPoints(int count, params Vector2[] pos) => new BezierCurve(pos).GetPoints(count);
    }
}