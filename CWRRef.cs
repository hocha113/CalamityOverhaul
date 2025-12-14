using CalamityMod;
using CalamityMod.CalPlayer;
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
using Terraria.ModLoader;

namespace CalamityOverhaul
{
    /// <summary>
    /// 一个用于访问Calamity Mod内部内容的静态类
    /// </summary>
    internal static class CWRRef
    {
        public static bool GetDownedPrimordialWyrm() => DownedBossSystem.downedPrimordialWyrm;
        public static void SetDownedPrimordialWyrm(bool value) => DownedBossSystem.downedPrimordialWyrm = value;
        public static bool GetBossRushActive() => BossRushEvent.BossRushActive;
        public static void SetBossRushActive(bool value) => BossRushEvent.BossRushActive = value;
        public static bool GetAcidRainEventIsOngoing() => AcidRainEvent.AcidRainEventIsOngoing;
        public static DamageClass GetTrueMeleeDamageClass() => ModContent.GetInstance<TrueMeleeDamageClass>();
        public static DamageClass GetTrueMeleeNoSpeedDamageClass() => ModContent.GetInstance<TrueMeleeNoSpeedDamageClass>();
        public static DamageClass GetMeleeRangedHybridDamageClass() => ModContent.GetInstance<MeleeRangedHybridDamageClass>();
        public static float ChargeRatio(Item item) => item.Calamity().ChargeRatio;
        public static bool BladeArmEnchant(this Player player) => player.Calamity().bladeArmEnchant;
        public static bool AdrenalineMode(this Player player) => player.Calamity().adrenalineModeActive;
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
    }
}
