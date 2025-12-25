using CalamityMod;
using CalamityMod.Balancing;
using CalamityMod.CalPlayer;
using CalamityMod.CustomRecipes;
using CalamityMod.DataStructures;
using CalamityMod.Events;
using CalamityMod.Graphics.Metaballs;
using CalamityMod.Items.Weapons.Magic;
using CalamityMod.NPCs;
using CalamityMod.NPCs.ExoMechs;
using CalamityMod.NPCs.SupremeCalamitas;
using CalamityMod.NPCs.TownNPCs;
using CalamityMod.Particles;
using CalamityMod.Projectiles;
using CalamityMod.TileEntities;
using CalamityMod.Tiles.Furniture.CraftingStations;
using CalamityMod.UI;
using CalamityMod.World;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.ADV;
using CalamityOverhaul.Content.LegendWeapon.MurasamaLegend.UI;
using CalamityOverhaul.Content.PRTTypes;
using CalamityOverhaul.Content.RemakeItems;
using InnoVault.GameSystem;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.GameContent.UI.BigProgressBar;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.Utilities;
using static CalamityOverhaul.Common.ModGanged;

namespace CalamityOverhaul
{
    /// <summary>
    /// 一个用于访问Calamity Mod内部内容的静态类
    /// </summary>
    internal static class CWRRef
    {
        private static bool? _has = null;
        /// <summary>
        /// 是否安装了指定版本的Calamity Mod
        /// </summary>
        public static bool Has {
            get {
                _has ??= ModLoader.TryGetMod("CalamityMod", out Mod mod) && mod.Version == new Version(2, 0, 7, 2);
                return _has.Value;
            }
        }
        private static bool dummyBool;
        private static int dummyInt;
        private static float dummyFloat;

        internal static void UnLoad() => _has = null;

        /// <summary>
        /// 荒漠灾虫
        /// </summary>
        public static bool GetDownedDesertScourge() => Has && GetDownedDesertScourgeInner();
        [CWRJITEnabled]
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool GetDownedDesertScourgeInner() => DownedBossSystem.downedDesertScourge;

        /// <summary>
        /// 巨像蛤
        /// </summary>
        public static bool GetDownedCLAM() => Has && GetDownedCLAMInner();
        [CWRJITEnabled]
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool GetDownedCLAMInner() => DownedBossSystem.downedCLAM;

        /// <summary>
        /// 蘑菇蟹
        /// </summary>
        public static bool GetDownedCrabulon() => Has && GetDownedCrabulonInner();
        [CWRJITEnabled]
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool GetDownedCrabulonInner() => DownedBossSystem.downedCrabulon;

        /// <summary>
        /// 腐巢意志
        /// </summary>
        public static bool GetDownedHiveMind() => Has && GetDownedHiveMindInner();
        [CWRJITEnabled]
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool GetDownedHiveMindInner() => DownedBossSystem.downedHiveMind;

        /// <summary>
        /// 血肉宿主
        /// </summary>
        public static bool GetDownedPerforator() => Has && GetDownedPerforatorInner();
        [CWRJITEnabled]
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool GetDownedPerforatorInner() => DownedBossSystem.downedPerforator;

        /// <summary>
        /// 史莱姆之神
        /// </summary>
        public static bool GetDownedSlimeGod() => Has && GetDownedSlimeGodInner();
        [CWRJITEnabled]
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool GetDownedSlimeGodInner() => DownedBossSystem.downedSlimeGod;

        /// <summary>
        /// 极地冰灵
        /// </summary>
        public static bool GetDownedCryogen() => Has && GetDownedCryogenInner();
        [CWRJITEnabled]
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool GetDownedCryogenInner() => DownedBossSystem.downedCryogen;

        /// <summary>
        /// 硫磺火元素
        /// </summary>
        public static bool GetDownedBrimstoneElemental() => Has && GetDownedBrimstoneElementalInner();
        [CWRJITEnabled]
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool GetDownedBrimstoneElementalInner() => DownedBossSystem.downedBrimstoneElemental;

        /// <summary>
        /// 渊海灾虫
        /// </summary>
        public static bool GetDownedAquaticScourge() => Has && GetDownedAquaticScourgeInner();
        [CWRJITEnabled]
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool GetDownedAquaticScourgeInner() => DownedBossSystem.downedAquaticScourge;

        /// <summary>
        /// 辐射之主
        /// </summary>
        public static bool GetDownedCragmawMire() => Has && GetDownedCragmawMireInner();
        [CWRJITEnabled]
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool GetDownedCragmawMireInner() => DownedBossSystem.downedCragmawMire;

        /// <summary>
        /// 灾厄之影
        /// </summary>
        public static bool GetDownedCalamitasClone() => Has && GetDownedCalamitasCloneInner();
        [CWRJITEnabled]
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool GetDownedCalamitasCloneInner() => DownedBossSystem.downedCalamitasClone;

        /// <summary>
        /// 沙漠巨鲨
        /// </summary>
        public static bool GetDownedGSS() => Has && GetDownedGSSInner();
        [CWRJITEnabled]
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool GetDownedGSSInner() => DownedBossSystem.downedGSS;

        /// <summary>
        /// 利维坦
        /// </summary>
        public static bool GetDownedLeviathan() => Has && GetDownedLeviathanInner();
        [CWRJITEnabled]
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool GetDownedLeviathanInner() => DownedBossSystem.downedLeviathan;

        /// <summary>
        /// 白金星舰
        /// </summary>
        public static bool GetDownedAstrumAureus() => Has && GetDownedAstrumAureusInner();
        [CWRJITEnabled]
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool GetDownedAstrumAureusInner() => DownedBossSystem.downedAstrumAureus;

        /// <summary>
        /// 瘟疫使者
        /// </summary>
        public static bool GetDownedPlaguebringer() => Has && GetDownedPlaguebringerInner();
        [CWRJITEnabled]
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool GetDownedPlaguebringerInner() => DownedBossSystem.downedPlaguebringer;

        /// <summary>
        /// 毁灭魔像
        /// </summary>
        public static bool GetDownedRavager() => Has && GetDownedRavagerInner();
        [CWRJITEnabled]
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool GetDownedRavagerInner() => DownedBossSystem.downedRavager;

        /// <summary>
        /// 星神游龙
        /// </summary>
        public static bool GetDownedAstrumDeus() => Has && GetDownedAstrumDeusInner();
        [CWRJITEnabled]
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool GetDownedAstrumDeusInner() => DownedBossSystem.downedAstrumDeus;

        /// <summary>
        /// 亵渎使徒
        /// </summary>
        public static bool GetDownedGuardians() => Has && GetDownedGuardiansInner();
        [CWRJITEnabled]
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool GetDownedGuardiansInner() => DownedBossSystem.downedGuardians;

        /// <summary>
        /// 痴愚金龙
        /// </summary>
        public static bool GetDownedDragonfolly() => Has && GetDownedDragonfollyInner();
        [CWRJITEnabled]
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool GetDownedDragonfollyInner() => DownedBossSystem.downedDragonfolly;

        /// <summary>
        /// 亵渎天神
        /// </summary>
        public static bool GetDownedProvidence() => Has && GetDownedProvidenceInner();
        [CWRJITEnabled]
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool GetDownedProvidenceInner() => DownedBossSystem.downedProvidence;

        /// <summary>
        /// 无尽虚空
        /// </summary>
        public static bool GetDownedCeaselessVoid() => Has && GetDownedCeaselessVoidInner();
        [CWRJITEnabled]
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool GetDownedCeaselessVoidInner() => DownedBossSystem.downedCeaselessVoid;

        /// <summary>
        /// 风暴编织者
        /// </summary>
        public static bool GetDownedStormWeaver() => Has && GetDownedStormWeaverInner();
        [CWRJITEnabled]
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool GetDownedStormWeaverInner() => DownedBossSystem.downedStormWeaver;

        /// <summary>
        /// 西格纳斯
        /// </summary>
        public static bool GetDownedSignus() => Has && GetDownedSignusInner();
        [CWRJITEnabled]
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool GetDownedSignusInner() => DownedBossSystem.downedSignus;

        /// <summary>
        /// 噬魂幽花
        /// </summary>
        public static bool GetDownedPolterghast() => Has && GetDownedPolterghastInner();
        [CWRJITEnabled]
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool GetDownedPolterghastInner() => DownedBossSystem.downedPolterghast;

        /// <summary>
        /// 酸雨二
        /// </summary>
        public static bool GetDownedMauler() => Has && GetDownedMaulerInner();
        [CWRJITEnabled]
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool GetDownedMaulerInner() => DownedBossSystem.downedMauler;

        /// <summary>
        /// 生化恐惧
        /// </summary>
        public static bool GetDownedNuclearTerror() => Has && GetDownedNuclearTerrorInner();
        [CWRJITEnabled]
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool GetDownedNuclearTerrorInner() => DownedBossSystem.downedNuclearTerror;

        /// <summary>
        /// 老核弹
        /// </summary>
        public static bool GetDownedBoomerDuke() => Has && GetDownedBoomerDukeInner();
        [CWRJITEnabled]
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool GetDownedBoomerDukeInner() => DownedBossSystem.downedBoomerDuke;

        /// <summary>
        /// 神明吞噬者
        /// </summary>
        public static bool GetDownedDoG() => Has && GetDownedDoGInner();
        [CWRJITEnabled]
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool GetDownedDoGInner() => DownedBossSystem.downedDoG;

        /// <summary>
        /// 丛林龙
        /// </summary>
        public static bool GetDownedYharon() => Has && GetDownedYharonInner();
        [CWRJITEnabled]
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool GetDownedYharonInner() => DownedBossSystem.downedYharon;

        /// <summary>
        /// 星流巨械
        /// </summary>
        public static bool GetDownedExoMechs() => Has && GetDownedExoMechsInner();
        [CWRJITEnabled]
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool GetDownedExoMechsInner() => DownedBossSystem.downedExoMechs;

        /// <summary>
        /// 至尊灾厄
        /// </summary>
        public static bool GetDownedCalamitas() => Has && GetDownedCalamitasInner();
        [CWRJITEnabled]
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool GetDownedCalamitasInner() => DownedBossSystem.downedCalamitas;

        /// <summary>
        /// 始源妖龙
        /// </summary>
        public static bool GetDownedPrimordialWyrm() => Has && GetDownedPrimordialWyrmInner();
        [CWRJITEnabled]
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool GetDownedPrimordialWyrmInner() => DownedBossSystem.downedPrimordialWyrm;

        /// <summary>
        /// 终焉之战
        /// </summary>
        public static bool GetDownedBossRush() => Has && GetDownedBossRushInner();
        [CWRJITEnabled]
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool GetDownedBossRushInner() => DownedBossSystem.downedBossRush;

        public static void SetDownedPrimordialWyrm(bool value) {
            if (!Has) return;
            SetDownedPrimordialWyrmInner(value);
        }
        [CWRJITEnabled]
        private static void SetDownedPrimordialWyrmInner(bool value) => DownedBossSystem.downedPrimordialWyrm = value;

        public static bool GetDeathMode() => Has && GetDeathModeInner();
        [CWRJITEnabled]
        private static bool GetDeathModeInner() => CalamityWorld.death;

        public static bool GetRevengeMode() => Has && GetRevengeModeInner();
        [CWRJITEnabled]
        private static bool GetRevengeModeInner() => CalamityWorld.revenge;

        public static bool GetBossRushActive() => Has && GetBossRushActiveInner();
        [CWRJITEnabled]
        private static bool GetBossRushActiveInner() => BossRushEvent.BossRushActive;

        public static void SetBossRushActive(bool value) {
            if (!Has) return;
            SetBossRushActiveInner(value);
        }
        [CWRJITEnabled]
        private static void SetBossRushActiveInner(bool value) => BossRushEvent.BossRushActive = value;

        public static bool GetAcidRainEventIsOngoing() => Has && GetAcidRainEventIsOngoingInner();
        [CWRJITEnabled]
        private static bool GetAcidRainEventIsOngoingInner() => AcidRainEvent.AcidRainEventIsOngoing;

        public static DamageClass GetTrueMeleeDamageClass() => Has ? GetTrueMeleeDamageClassInner() : DamageClass.Default;
        [CWRJITEnabled]
        private static DamageClass GetTrueMeleeDamageClassInner() => ModContent.GetInstance<TrueMeleeDamageClass>();

        public static DamageClass GetTrueMeleeNoSpeedDamageClass() => Has ? GetTrueMeleeNoSpeedDamageClassInner() : DamageClass.Default;
        [CWRJITEnabled]
        private static DamageClass GetTrueMeleeNoSpeedDamageClassInner() => ModContent.GetInstance<TrueMeleeNoSpeedDamageClass>();

        public static DamageClass GetMeleeRangedHybridDamageClass() => Has ? GetMeleeRangedHybridDamageClassInner() : DamageClass.Default;
        [CWRJITEnabled]
        private static DamageClass GetMeleeRangedHybridDamageClassInner() => ModContent.GetInstance<MeleeRangedHybridDamageClass>();

        public static float ChargeRatio(Item item) => Has ? ChargeRatioInner(item) : 0f;
        [CWRJITEnabled]
        private static float ChargeRatioInner(Item item) => item.Calamity().ChargeRatio;

        public static bool GetNPCIsAnEnemy(this NPC npc) => Has && GetNPCIsAnEnemyInner(npc);
        [CWRJITEnabled]
        private static bool GetNPCIsAnEnemyInner(NPC npc) => npc.IsAnEnemy();

        public static void SetPlayerWarbannerOfTheSun(this Player player, bool value) {
            if (!Has) return;
            SetPlayerWarbannerOfTheSunInner(player, value);
        }
        [CWRJITEnabled]
        private static void SetPlayerWarbannerOfTheSunInner(Player player, bool value) => player.Calamity().warbannerOfTheSun = value;

        public static bool GetPlayerBladeArmEnchant(this Player player) => Has && GetPlayerBladeArmEnchantInner(player);
        [CWRJITEnabled]
        private static bool GetPlayerBladeArmEnchantInner(Player player) => player.Calamity().bladeArmEnchant;

        public static ref int RefPlayerEvilSmasherBoost(this Player player) {
            if (!Has) {
                return ref dummyInt;
            }
            return ref RefPlayerEvilSmasherBoostInner(player);
        }
        [CWRJITEnabled]
        private static ref int RefPlayerEvilSmasherBoostInner(Player player) => ref player.Calamity().evilSmasherBoost;

        public static bool GetPlayerAdrenalineMode(this Player player) => Has && GetPlayerAdrenalineModeInner(player);
        [CWRJITEnabled]
        private static bool GetPlayerAdrenalineModeInner(Player player) => player.Calamity().adrenalineModeActive;

        public static void SetProjPointBlankShotDuration(this Projectile projectile, int value) {
            if (!Has) return;
            SetProjPointBlankShotDurationInner(projectile, value);
        }
        [CWRJITEnabled]
        private static void SetProjPointBlankShotDurationInner(Projectile projectile, int value) => projectile.Calamity().pointBlankShotDuration = value;

        public static void LargeFieryExplosion(Projectile projectile) {
            if (!Has) return;
            LargeFieryExplosionInner(projectile);
        }
        [CWRJITEnabled]
        private static void LargeFieryExplosionInner(Projectile projectile) => projectile.LargeFieryExplosion();

        public static bool DrawBeam(Projectile projectile, float length, float spacer, Color lightColor, Texture2D texture = null, bool curve = false) => Has && DrawBeamInner(projectile, length, spacer, lightColor, texture, curve);
        [CWRJITEnabled]
        private static bool DrawBeamInner(Projectile projectile, float length, float spacer, Color lightColor, Texture2D texture, bool curve) => projectile.DrawBeam(length, spacer, lightColor, texture, curve);

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
            if (!Has) return;
            UpdateRogueStealthInner(player);
        }
        [CWRJITEnabled]
        private static void UpdateRogueStealthInner(Player player) {
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
            if (!Has) return;
            SummonSupCalInner(spawnPos);
        }
        [CWRJITEnabled]
        private static void SummonSupCalInner(Vector2 spawnPos) {
            SoundEngine.PlaySound(SCalAltar.SummonSound, spawnPos);
            Projectile.NewProjectile(new EntitySource_WorldEvent(), spawnPos, Vector2.Zero
                , CWRID.Proj_SCalRitualDrama, 0, 0f, Main.myPlayer, 0, 0);
        }

        public static void SummonExo(int exoType, Player player) {
            if (!Has) {
                return;
            }
            SummonExoInner(exoType, player);
        }
        [CWRJITEnabled]
        public static void SummonExoInner(int exoType, Player player) {
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
                    CalamityUtils.SpawnBossBetter(aresSpawnPosition, CWRID.NPC_AresBody);
                    break;

                case ExoMech.Twins:
                    Vector2 artemisSpawnPosition = player.Center + new Vector2(-1100f, -1600f);
                    Vector2 apolloSpawnPosition = player.Center + new Vector2(1100f, -1600f);
                    CalamityUtils.SpawnBossBetter(artemisSpawnPosition, CWRID.NPC_Artemis);
                    CalamityUtils.SpawnBossBetter(apolloSpawnPosition, CWRID.NPC_Apollo);
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
            if (!Has) return;
            SpawnMediumMistParticleInner(smokePos, smokeVel, Smoketype);
        }
        [CWRJITEnabled]
        private static void SpawnMediumMistParticleInner(Vector2 smokePos, Vector2 smokeVel, bool Smoketype) {
            Particle smoke = new MediumMistParticle(smokePos, smokeVel, new Color(255, 110, 50), Color.OrangeRed
                    , Smoketype ? Main.rand.NextFloat(0.4f, 0.75f) : Main.rand.NextFloat(1.5f, 2f), 220 - Main.rand.Next(50), 0.1f);
            GeneralParticleHandler.SpawnParticle(smoke);
        }

        public static void DrawAfterimagesCentered(Projectile proj, int mode, Color lightColor, int typeOneIncrement = 1, Texture2D texture = null, bool drawCentered = true) {
            if (!Has) {
                Main.spriteBatch.Draw(TextureAssets.Projectile[proj.type].Value, proj.Center - Main.screenPosition
                    , null, lightColor, proj.rotation, TextureAssets.Projectile[proj.type].Value.Size() / 2, proj.scale, SpriteEffects.None, 0);
                return;
            }
            DrawAfterimagesCenteredInner(proj, mode, lightColor, typeOneIncrement, texture, drawCentered);
        }
        [CWRJITEnabled]
        private static void DrawAfterimagesCenteredInner(Projectile proj, int mode, Color lightColor, int typeOneIncrement, Texture2D texture, bool drawCentered) => CalamityUtils.DrawAfterimagesCentered(proj, mode, lightColor, typeOneIncrement, texture, drawCentered);

        public static void HomeInOnNPC(Projectile projectile, bool ignoreTiles, float distanceRequired, float homingVelocity, float inertia) {
            if (!Has) return;
            HomeInOnNPCInner(projectile, ignoreTiles, distanceRequired, homingVelocity, inertia);
        }
        [CWRJITEnabled]
        private static void HomeInOnNPCInner(Projectile projectile, bool ignoreTiles, float distanceRequired, float homingVelocity, float inertia) => CalamityUtils.HomeInOnNPC(projectile, ignoreTiles, distanceRequired, homingVelocity, inertia);

        public static void SpawnLifeStealProjectile(Projectile projectile, Player player, float healAmount, int healProjectileType, float distanceRequired, float cooldownMultiplier = 1f) {
            if (!Has) return;
            SpawnLifeStealProjectileInner(projectile, player, healAmount, healProjectileType, distanceRequired, cooldownMultiplier);
        }
        [CWRJITEnabled]
        private static void SpawnLifeStealProjectileInner(Projectile projectile, Player player, float healAmount, int healProjectileType, float distanceRequired, float cooldownMultiplier) => CalamityGlobalProjectile.SpawnLifeStealProjectile(projectile, player, healAmount, healProjectileType, distanceRequired, cooldownMultiplier);

        public static Projectile ProjectileBarrage(IEntitySource source, Vector2 originVec, Vector2 targetPos, bool fromRight, float xOffsetMin, float xOffsetMax
            , float yOffsetMin, float yOffsetMax, float projSpeed, int projType, int damage, float knockback, int owner, bool clamped = false, float inaccuracyOffset = 5f)
            => Has ? ProjectileBarrageInner(source, originVec, targetPos, fromRight, xOffsetMin, xOffsetMax, yOffsetMin, yOffsetMax, projSpeed, projType, damage, knockback, owner, clamped, inaccuracyOffset) : null;
        [CWRJITEnabled]
        private static Projectile ProjectileBarrageInner(IEntitySource source, Vector2 originVec, Vector2 targetPos, bool fromRight, float xOffsetMin, float xOffsetMax
            , float yOffsetMin, float yOffsetMax, float projSpeed, int projType, int damage, float knockback, int owner, bool clamped, float inaccuracyOffset)
            => CalamityUtils.ProjectileBarrage(source, originVec, targetPos, fromRight, xOffsetMin, xOffsetMax
                , yOffsetMin, yOffsetMax, projSpeed, projType, damage, knockback, owner, clamped, inaccuracyOffset);

        public static void SetDraedonDefeatTimer(NPC npc, float value) {
            if (!Has) return;
            SetDraedonDefeatTimerInner(npc, value);
        }
        [CWRJITEnabled]
        private static void SetDraedonDefeatTimerInner(NPC npc, float value) {
            if (npc.ModNPC is Draedon draedon) {
                draedon.DefeatTimer = value;
            }
        }

        public static float GetDraedonDefeatTimer(NPC npc) => Has ? GetDraedonDefeatTimerInner(npc) : 0f;
        [CWRJITEnabled]
        private static float GetDraedonDefeatTimerInner(NPC npc) {
            if (npc.ModNPC is Draedon draedon) {
                return draedon.DefeatTimer;
            }
            return 0f;
        }

        public static List<int> GetPierceResistExceptionList() => Has ? GetPierceResistExceptionListInner() : new List<int>();
        [CWRJITEnabled]
        private static List<int> GetPierceResistExceptionListInner() => CalamityLists.projectileDestroyExceptionList;

        public static bool HasExo() => Has && HasExoInner();
        [CWRJITEnabled]
        private static bool HasExoInner() => Draedon.ExoMechIsPresent;

        public static int GetCalItemID(this string key) => CWRItemOverride.GetCalItemID(key);

        public static void SetAbleToSelectExoMech(Player player, bool value) {
            if (!Has) return;
            SetAbleToSelectExoMechInner(player, value);
        }
        [CWRJITEnabled]
        private static void SetAbleToSelectExoMechInner(Player player, bool value) {
            player.Calamity().AbleToSelectExoMech = value;
        }

        public static void SetProjtimesPierced(this Projectile projectile, int value) {
            if (!Has) return;
            SetProjtimesPiercedInner(projectile, value);
        }
        [CWRJITEnabled]
        private static void SetProjtimesPiercedInner(Projectile projectile, int value) => projectile.Calamity().timesPierced = value;

        public static ref int GetMurasamaHitCooldown(this Player player) {
            if (!Has) {
                return ref dummyInt;
            }
            return ref GetMurasamaHitCooldownInner(player);
        }
        [CWRJITEnabled]
        private static ref int GetMurasamaHitCooldownInner(Player player) => ref player.Calamity().murasamaHitCooldown;

        public static void SetBrimstoneBullets(this Projectile projectile, bool value) {
            if (!Has) return;
            SetBrimstoneBulletsInner(projectile, value);
        }
        [CWRJITEnabled]
        private static void SetBrimstoneBulletsInner(Projectile projectile, bool value) => projectile.Calamity().brimstoneBullets = value;

        public static void SetDeepcoreBullet(this Projectile projectile, bool value) {
            if (!Has) return;
            SetDeepcoreBulletInner(projectile, value);
        }
        [CWRJITEnabled]
        private static void SetDeepcoreBulletInner(Projectile projectile, bool value) => projectile.Calamity().deepcoreBullet = value;

        public static void SetAllProjectilesHome(this Projectile projectile, bool value) {
            if (!Has) return;
            SetAllProjectilesHomeInner(projectile, value);
        }
        [CWRJITEnabled]
        private static void SetAllProjectilesHomeInner(Projectile projectile, bool value) => projectile.Calamity().allProjectilesHome = value;

        public static void SetBetterLifeBullet1(this Projectile projectile, bool value) {
            if (!Has) return;
            SetBetterLifeBullet1Inner(projectile, value);
        }
        [CWRJITEnabled]
        private static void SetBetterLifeBullet1Inner(Projectile projectile, bool value) => projectile.Calamity().betterLifeBullet1 = value;

        public static void SetBetterLifeBullet2(this Projectile projectile, bool value) {
            if (!Has) return;
            SetBetterLifeBullet2Inner(projectile, value);
        }
        [CWRJITEnabled]
        private static void SetBetterLifeBullet2Inner(Projectile projectile, bool value) => projectile.Calamity().betterLifeBullet2 = value;

        public static Vector2 GetCoinTossVelocity(Player player) => Has ? GetCoinTossVelocityInner(player) : Vector2.Zero;
        [CWRJITEnabled]
        private static Vector2 GetCoinTossVelocityInner(Player player) => player.GetCoinTossVelocity();

        public static bool GetAlchFlask(this Player player) => Has && GetAlchFlaskInner(player);
        [CWRJITEnabled]
        private static bool GetAlchFlaskInner(Player player) => player.Calamity().alchFlask;

        public static bool GetSpiritOrigin(this Player player) => Has && GetSpiritOriginInner(player);
        [CWRJITEnabled]
        private static bool GetSpiritOriginInner(Player player) => player.Calamity().spiritOrigin;

        public static void Spawn_PristineFury_Effect(Vector2 spawnPos, Vector2 vel) {
            if (!Has) return;
            Spawn_PristineFury_EffectInner(spawnPos, vel);
        }
        [CWRJITEnabled]
        private static void Spawn_PristineFury_EffectInner(Vector2 spawnPos, Vector2 vel) {
            CritSpark spark = new(spawnPos, vel, Main.rand.NextBool() ? Color.DarkOrange : Color.OrangeRed, Color.OrangeRed, 0.9f, 18, 2f, 1.9f);
            GeneralParticleHandler.SpawnParticle(spark);
        }

        public static void SetProjCGP(int proj) {
            if (!Has) return;
            SetProjCGPInner(proj);
        }
        [CWRJITEnabled]
        private static void SetProjCGPInner(int proj) {
            CalamityGlobalProjectile cgp = Main.projectile[proj].Calamity();
            cgp.supercritHits = -1;
            cgp.appliesSomaShred = true;
        }

        public static void Spawn_Effect_1(Vector2 spawnPos, Vector2 vel) {
            if (!Has) return;
            Spawn_Effect_1Inner(spawnPos, vel);
        }
        [CWRJITEnabled]
        private static void Spawn_Effect_1Inner(Vector2 spawnPos, Vector2 vel) {
            Particle spark2 = new LineParticle(spawnPos, vel, false, Main.rand.Next(15, 25 + 1), Main.rand.NextFloat(1.5f, 2f), Main.rand.NextBool() ? Color.MediumOrchid : Color.DarkViolet);
            GeneralParticleHandler.SpawnParticle(spark2);
        }

        public static void Spawn_Effect_2(Vector2 spawnPos, Vector2 vel, int sparkLifetime, float sparkScale, Color sparkColor) {
            if (!Has) return;
            Spawn_Effect_2Inner(spawnPos, vel, sparkLifetime, sparkScale, sparkColor);
        }
        [CWRJITEnabled]
        private static void Spawn_Effect_2Inner(Vector2 spawnPos, Vector2 vel, int sparkLifetime, float sparkScale, Color sparkColor) {
            SparkParticle spark = new SparkParticle(spawnPos, vel, false, sparkLifetime, sparkScale, sparkColor);
            GeneralParticleHandler.SpawnParticle(spark);
        }

        public static void SetDownedCalamitas(bool value) {
            if (!Has) return;
            SetDownedCalamitasInner(value);
        }
        [CWRJITEnabled]
        private static void SetDownedCalamitasInner(bool value) {
            DownedBossSystem.downedCalamitas = value;
        }

        public static void SetDownedBoomerDuke(bool value) {
            if (!Has) return;
            SetDownedBoomerDukeInner(value);
        }
        [CWRJITEnabled]
        private static void SetDownedBoomerDukeInner(bool value) => DownedBossSystem.downedBoomerDuke = value;

        public static bool GetSupCalPermafrost(NPC npc) => Has && GetSupCalPermafrostInner(npc);
        [CWRJITEnabled]
        private static bool GetSupCalPermafrostInner(NPC npc) {
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

        public static bool GetDownedThanatos() => Has && GetDownedThanatosInner();
        [CWRJITEnabled]
        private static bool GetDownedThanatosInner() => DownedBossSystem.downedThanatos;

        public static void SetSupCalPermafrost(NPC npc, bool value) {
            if (!Has) return;
            SetSupCalPermafrostInner(npc, value);
        }
        [CWRJITEnabled]
        private static void SetSupCalPermafrostInner(NPC npc, bool value) {
            if (npc.ModNPC is SupremeCalamitas supCal) {
                supCal.permafrost = value;
            }
        }

        public static int GetSupCalGiveUpCounter(NPC npc) => Has ? GetSupCalGiveUpCounterInner(npc) : 0;
        [CWRJITEnabled]
        private static int GetSupCalGiveUpCounterInner(NPC npc) {
            if (npc.ModNPC is SupremeCalamitas supCal) {
                return supCal.giveUpCounter;
            }
            return 0;
        }

        public static void SetSupCalGiveUpCounter(NPC npc, int value) {
            if (!Has) return;
            SetSupCalGiveUpCounterInner(npc, value);
        }
        [CWRJITEnabled]
        private static void SetSupCalGiveUpCounterInner(NPC npc, int value) {
            if (npc.ModNPC is SupremeCalamitas supCal) {
                supCal.giveUpCounter = value;
            }
        }

        public static Type GetItem_SHPC_Type() => Has ? GetItem_SHPC_TypeInner() : null;
        public static Type GetNPC_WITCH_Type() => Has ? GetNPC_WITCH_TypeInner() : null;
        public static Type GetNPC_SupCal_Type() => Has ? GetNPC_SupCal_TypeInner() : null;
        [CWRJITEnabled]
        public static Type GetItem_SHPC_TypeInner() => typeof(SHPC);
        [CWRJITEnabled]
        public static Type GetNPC_WITCH_TypeInner() => typeof(WITCH);
        [CWRJITEnabled]
        public static Type GetNPC_SupCal_TypeInner() => typeof(SupremeCalamitas);

        public static bool GetEarlyHardmodeProgressionReworkBool() => Has && GetEarlyHardmodeProgressionReworkBoolInner();
        [CWRJITEnabled]
        private static bool GetEarlyHardmodeProgressionReworkBoolInner() => CalamityServerConfig.Instance.EarlyHardmodeProgressionRework;

        public static bool GetAfterimages() => Has && GetAfterimagesInner();
        [CWRJITEnabled]
        private static bool GetAfterimagesInner() => CalamityClientConfig.Instance.Afterimages;

        public static int GetProjectileDamage(NPC npc, int projType) => Has ? GetProjectileDamageInner(npc, projType) : npc.defDamage / 2;
        [CWRJITEnabled]
        private static int GetProjectileDamageInner(NPC npc, int projType) => npc.GetProjectileDamage(projType);

        public static void SetPlayerInfiniteFlight(this Player player, bool value) {
            if (!Has) return;
            SetPlayerInfiniteFlightInner(player, value);
        }
        [CWRJITEnabled]
        private static void SetPlayerInfiniteFlightInner(Player player, bool value) => player.Calamity().infiniteFlight = value;

        public static bool GetPlayerStealthStrikeAvailable(this Player player) => Has && GetPlayerStealthStrikeAvailableInner(player);
        [CWRJITEnabled]
        private static bool GetPlayerStealthStrikeAvailableInner(Player player) => player.Calamity().StealthStrikeAvailable();

        public static void SetProjStealthStrike(this Projectile projectile, bool value) {
            if (!Has) return;
            SetProjStealthStrikeInner(projectile, value);
        }
        [CWRJITEnabled]
        private static void SetProjStealthStrikeInner(Projectile projectile, bool value) => projectile.Calamity().stealthStrike = value;

        public static void HorsemansBladeOnHit(Player player, int targetIdx, int damage, float knockback
            , int extraUpdateAmt = 0, int type = ProjectileID.FlamingJack) {
            if (!Has) return;
            HorsemansBladeOnHitInner(player, targetIdx, damage, knockback, extraUpdateAmt, type);
        }
        [CWRJITEnabled]
        private static void HorsemansBladeOnHitInner(Player player, int targetIdx, int damage, float knockback
            , int extraUpdateAmt, int type)
            => CalamityPlayer.HorsemansBladeOnHit(player, targetIdx, damage, knockback, extraUpdateAmt, type);

        public static void SetItemCanFirePointBlankShots(this Item item, bool value) {
            if (!Has) return;
            SetItemCanFirePointBlankShotsInner(item, value);
        }
        [CWRJITEnabled]
        private static void SetItemCanFirePointBlankShotsInner(Item item, bool value) => item.Calamity().canFirePointBlankShots = value;

        public static bool GetProjStealthStrike(this Projectile projectile) => Has && GetProjStealthStrikeInner(projectile);
        [CWRJITEnabled]
        private static bool GetProjStealthStrikeInner(Projectile projectile) => projectile.Calamity().stealthStrike;

        public static void OldDukeOnKill(NPC npc) {
            if (!Has) return;
            OldDukeOnKillInner(npc);
        }
        [CWRJITEnabled]
        private static void OldDukeOnKillInner(NPC npc) {
            StopAcidRainInner();
            CalamityGlobalNPC.SetNewBossJustDowned(npc);
            DownedBossSystem.downedBoomerDuke = true;
            AcidRainEvent.OldDukeHasBeenEncountered = true;
            NPCLoader.OnKill(npc);
        }

        public static void StopAcidRain() {
            if (!Has) return;
            StopAcidRainInner();
        }
        [CWRJITEnabled]
        private static void StopAcidRainInner() {
            AcidRainEvent.AccumulatedKillPoints = 0;
            AcidRainEvent.UpdateInvasion(win: true);
        }

        public static void StarRT(Projectile projectile, Entity target) {
            if (!Has) return;
            StarRTInner(projectile, target);
        }
        [CWRJITEnabled]
        private static void StarRTInner(Projectile projectile, Entity target) {
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
            if (!Has) return;
            SpanFireInner(entity);
        }
        [CWRJITEnabled]
        private static void SpanFireInner(Entity entity) {
            bool LowVel = Main.rand.NextBool() ? false : true;
            FlameParticle ballFire = new FlameParticle(entity.Center + VaultUtils.RandVr(entity.width / 2)
                , Main.rand.Next(13, 22), Main.rand.NextFloat(0.1f, 0.22f), Main.rand.NextFloat(0.02f, 0.07f), Color.Gold, Color.DarkRed) {
                Velocity = new Vector2(entity.velocity.X * 0.8f, -10).RotatedByRandom(0.005f)
                * (LowVel ? Main.rand.NextFloat(0.4f, 0.65f) : Main.rand.NextFloat(0.8f, 1f))
            };
            GeneralParticleHandler.SpawnParticle(ballFire);
        }

        public static ref float RefItemCharge(this Item item) {
            if (!Has) {
                return ref dummyFloat;
            }
            return ref RefItemChargeInner(item);
        }
        [CWRJITEnabled]
        private static ref float RefItemChargeInner(Item item) => ref item.Calamity().Charge;

        public static float GetItemMaxCharge(this Item item) => Has ? GetItemMaxChargeInner(item) : 0f;
        [CWRJITEnabled]
        private static float GetItemMaxChargeInner(Item item) => item.Calamity().MaxCharge;

        public static ref float RefItemMaxCharge(this Item item) {
            if (!Has) {
                return ref dummyFloat;
            }
            return ref RefItemMaxChargeInner(item);
        }
        [CWRJITEnabled]
        private static ref float RefItemMaxChargeInner(Item item) => ref item.Calamity().MaxCharge;

        public static bool GetItemUsesCharge(this Item item) => Has && GetItemUsesChargeInner(item);
        [CWRJITEnabled]
        private static bool GetItemUsesChargeInner(Item item) => item.Calamity().UsesCharge;

        public static ref bool RefItemUsesCharge(this Item item) {
            if (!Has) {
                return ref dummyBool;
            }
            return ref RefItemUsesChargeInner(item);
        }
        [CWRJITEnabled]
        private static ref bool RefItemUsesChargeInner(Item item) => ref item.Calamity().UsesCharge;

        public static DamageClass GetRogueDamageClass() {
            if (!Has) {
                return DamageClass.Default;
            }
            return GetRogueDamageClassInner();
        }
        [CWRJITEnabled]
        private static DamageClass GetRogueDamageClassInner() => ModContent.GetInstance<RogueDamageClass>();

        public static float GetPlayerRogueStealth(this Player player) => Has ? GetPlayerRogueStealthInner(player) : 0f;
        [CWRJITEnabled]
        private static float GetPlayerRogueStealthInner(Player player) => player.Calamity().rogueStealth;

        public static float SetPlayerRogueStealth(this Player player, float value) => Has ? SetPlayerRogueStealthInner(player, value) : 0f;
        [CWRJITEnabled]
        private static float SetPlayerRogueStealthInner(Player player, float value) => player.Calamity().rogueStealth = value;

        public static float GetPlayerRogueStealthMax(this Player player) => Has ? GetPlayerRogueStealthMaxInner(player) : 0f;
        [CWRJITEnabled]
        private static float GetPlayerRogueStealthMaxInner(Player player) => player.Calamity().rogueStealthMax;

        public static ref float RefPlayerRogueStealthMax(this Player player) {
            if (!Has) {
                return ref dummyFloat;
            }
            return ref RefPlayerRogueStealthMaxInner(player);
        }
        [CWRJITEnabled]
        private static ref float RefPlayerRogueStealthMaxInner(Player player) => ref player.Calamity().rogueStealthMax;

        public static bool GetPlayerZoneSulphur(this Player player) => Has && GetPlayerZoneSulphurInner(player);
        [CWRJITEnabled]
        private static bool GetPlayerZoneSulphurInner(Player player) => player.Calamity().ZoneSulphur;

        public static bool GetPlayerZoneAbyss(this Player player) => Has && GetPlayerZoneAbyssInner(player);
        [CWRJITEnabled]
        private static bool GetPlayerZoneAbyssInner(Player player) => player.Calamity().ZoneAbyss;

        public static bool GetPlayerProfanedCrystalBuffs(this Player player) => Has && GetPlayerProfanedCrystalBuffsInner(player);
        [CWRJITEnabled]
        private static bool GetPlayerProfanedCrystalBuffsInner(Player player) => player.Calamity().profanedCrystalBuffs;

        public static void SetPlayerDashID(this Player player, string value) {
            if (!Has) return;
            SetPlayerDashIDInner(player, value);
        }
        [CWRJITEnabled]
        private static void SetPlayerDashIDInner(Player player, string value) => player.Calamity().DashID = value;

        public static void SetNSMBPlayer(Player player) {
            if (!Has) return;
            SetNSMBPlayerInner(player);
        }
        [CWRJITEnabled]
        private static void SetNSMBPlayerInner(Player player) {
            CalamityPlayer calPlayer = player.Calamity();
            calPlayer.rangedAmmoCost *= 0.8f;
            calPlayer.deadshotBrooch = true;
            calPlayer.dynamoStemCells = true;
            calPlayer.MiniSwarmers = true;
            calPlayer.eleResist = true;
            calPlayer.voidField = true;
        }

        public static LocalizedText ConstructRecipeCondition(int tier, out Func<bool> condition) {
            condition = null;
            return Has ? ConstructRecipeConditionInner(tier, out condition) : null;
        }
        [CWRJITEnabled]
        private static LocalizedText ConstructRecipeConditionInner(int tier, out Func<bool> condition) => ArsenalTierGatedRecipe.ConstructRecipeCondition(tier, out condition);

        public static IList<Type> GetTEBaseTurretTypes() => Has ? GetTEBaseTurretTypesInner() : null;
        [CWRJITEnabled]
        public static IList<Type> GetTEBaseTurretTypesInner() => VaultUtils.GetDerivedTypes<TEBaseTurret>();

        public static int GetSeasonDustID() => Has ? GetSeasonDustIDInner() : 0;
        [CWRJITEnabled]
        private static int GetSeasonDustIDInner() {
            return CalamityMod.CalamityMod.CurrentSeason switch {
                Season.Spring => Utils.SelectRandom(Main.rand, 245, 157, 107),
                Season.Summer => Utils.SelectRandom(Main.rand, 247, 228, 57),
                Season.Fall => Utils.SelectRandom(Main.rand, 6, 259, 158),
                Season.Winter => Utils.SelectRandom(Main.rand, 67, 229, 185),
                _ => 0
            };
        }

        public static void DrawStarTrail(Projectile projectile, Color outer, Color inner, float auraHeight = 10f) {
            if (!Has) return;
            DrawStarTrailInner(projectile, outer, inner, auraHeight);
        }
        [CWRJITEnabled]
        private static void DrawStarTrailInner(Projectile projectile, Color outer, Color inner, float auraHeight) => CalamityUtils.DrawStarTrail(projectile, outer, inner, auraHeight);

        public static int GetProjectileDamage(Projectile projectile, int projType) => Has ? GetProjectileDamageInner(projectile, projType) : 0;
        [CWRJITEnabled]
        private static int GetProjectileDamageInner(Projectile projectile, int projType) => projectile.GetProjectileDamage(projType);

        public static void SpawnDestroyerPRTEffect(NPC npc, float value, float value2, int idleTime) {
            if (!Has) return;
            SpawnDestroyerPRTEffectInner(npc, value, value2, idleTime);
        }
        [CWRJITEnabled]
        private static void SpawnDestroyerPRTEffectInner(NPC npc, float value, float value2, int idleTime) {
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

        public static void CosmicFireEffect(Projectile Projectile) {
            if (!Has) return;
            CosmicFireEffectInner(Projectile);
        }
        [CWRJITEnabled]
        private static void CosmicFireEffectInner(Projectile Projectile) {
            StreamGougeMetaball.SpawnParticle(Projectile.Center + VaultUtils.RandVr(13), Projectile.velocity, Main.rand.NextFloat(11.3f, 21.5f));
        }

        public static void FadingGloryRapierHitDustEffect(Projectile Projectile, NPC npc) {
            if (!Has) {
                return;
            }
            FadingGloryRapierHitDustEffectInner(Projectile, npc);
        }
        [CWRJITEnabled]
        private static void FadingGloryRapierHitDustEffectInner(Projectile Projectile, NPC npc) {
            Vector2 bloodSpawnPosition = npc.Center + Main.rand.NextVector2Circular(npc.width, npc.height) * 0.04f;
            Vector2 splatterDirection = (Projectile.Center - bloodSpawnPosition).SafeNormalize(Vector2.UnitY);
            if (CWRLoad.NPCValue.ISTheofSteel(npc)) {
                for (int j = 0; j < 3; j++) {
                    float sparkScale = Main.rand.NextFloat(1.2f, 2.33f);
                    int sparkLifetime = Main.rand.Next(22, 36);
                    Color sparkColor = Color.Lerp(Color.Silver, Color.Gold, Main.rand.NextFloat(0.7f));
                    Vector2 sparkVelocity = splatterDirection.RotatedByRandom(0.9f) * Main.rand.NextFloat(19f, 34.5f);
                    if (Has) {
                        PRT_Spark spark = new PRT_Spark(bloodSpawnPosition, sparkVelocity, true, sparkLifetime, sparkScale, sparkColor);
                        PRTLoader.AddParticle(spark);
                    }
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
                    if (Has) {
                        BloodParticle blood = new BloodParticle(bloodSpawnPosition, bloodVelocity, bloodLifetime, bloodScale, bloodColor);
                        GeneralParticleHandler.SpawnParticle(blood);
                    }
                }
                for (int i = 0; i < 3; i++) {
                    float bloodScale = Main.rand.NextFloat(0.2f, 0.33f);
                    Color bloodColor = Color.Lerp(Color.Red, Color.DarkRed, Main.rand.NextFloat(0.5f, 1f));
                    Vector2 bloodVelocity = splatterDirection.RotatedByRandom(0.9f) * Main.rand.NextFloat(9f, 14.5f);
                    if (Has) {
                        BloodParticle2 blood = new BloodParticle2(bloodSpawnPosition, bloodVelocity, 20, bloodScale, bloodColor);
                        GeneralParticleHandler.SpawnParticle(blood);
                    }
                }
            }
        }

        public static void UpdateDestroyerBodyDRIncrease(NPC npc) {
            if (!Has) return;
            UpdateDestroyerBodyDRIncreaseInner(npc);
        }
        [CWRJITEnabled]
        private static void UpdateDestroyerBodyDRIncreaseInner(NPC npc) {
            npc.Calamity().newAI[1] = 1200;
            npc.Calamity().CurrentlyIncreasingDefenseOrDR = false;
        }

        public static Projectile ProjectileRain(IEntitySource source, Vector2 targetPos, float xLimit
            , float xVariance, float yLimitLower, float yLimitUpper, float projSpeed, int projType, int damage, float knockback, int owner)
            => Has ? ProjectileRainInner(source, targetPos, xLimit, xVariance, yLimitLower, yLimitUpper, projSpeed, projType, damage, knockback, owner) : null;
        [CWRJITEnabled]
        private static Projectile ProjectileRainInner(IEntitySource source, Vector2 targetPos, float xLimit
            , float xVariance, float yLimitLower, float yLimitUpper, float projSpeed, int projType, int damage, float knockback, int owner)
            => CalamityUtils.ProjectileRain(source, targetPos, xLimit, xVariance, yLimitLower
                , yLimitUpper, projSpeed, projType, damage, knockback, owner);

        public static List<Vector2> BezierCurveGetPoints(int count, params Vector2[] pos) => Has ? BezierCurveGetPointsInner(count, pos) : new List<Vector2>();
        [CWRJITEnabled]
        private static List<Vector2> BezierCurveGetPointsInner(int count, Vector2[] pos) => new BezierCurve(pos).GetPoints(count);

        #region 炼铸系统包装器
        /// <summary>
        /// 附魔包装器结构体，用于安全地封装CalamityMod的Enchantment
        /// </summary>
        public struct EnchantmentWrapper
        {
            /// <summary>
            /// 附魔名称
            /// </summary>
            public LocalizedText Name { get; set; }

            /// <summary>
            /// 附魔描述
            /// </summary>
            public LocalizedText Description { get; set; }

            /// <summary>
            /// 附魔图标路径
            /// </summary>
            public string IconTexturePath { get; set; }

            /// <summary>
            /// 内部标识符（用于比较）
            /// </summary>
            internal int InternalId { get; set; }

            /// <summary>
            /// 是否是清除附魔
            /// </summary>
            public bool IsClearEnchantment { get; set; }

            public override bool Equals(object obj) {
                if (obj is EnchantmentWrapper other)
                    return InternalId == other.InternalId;
                return false;
            }

            public override int GetHashCode() => InternalId;

            public static bool operator ==(EnchantmentWrapper left, EnchantmentWrapper right)
                => left.InternalId == right.InternalId;

            public static bool operator !=(EnchantmentWrapper left, EnchantmentWrapper right)
                => !(left == right);
        }

        /// <summary>
        /// 获取物品的有效附魔列表
        /// </summary>
        public static List<EnchantmentWrapper> GetValidEnchantmentsForItem(Item item) {
            if (!Has || item == null || item.IsAir)
                return new List<EnchantmentWrapper>();
            return GetValidEnchantmentsForItemInner(item);
        }
        [CWRJITEnabled]
        private static List<EnchantmentWrapper> GetValidEnchantmentsForItemInner(Item item) {
            var result = new List<EnchantmentWrapper>();
            var enchantments = CalamityMod.UI.CalamitasEnchants.EnchantmentManager.GetValidEnchantmentsForItem(item);

            int id = 0;
            foreach (var enchantment in enchantments) {
                result.Add(new EnchantmentWrapper {
                    Name = enchantment.Name,
                    Description = enchantment.Description,
                    IconTexturePath = enchantment.IconTexturePath,
                    InternalId = id++,
                    IsClearEnchantment = enchantment.Equals(CalamityMod.UI.CalamitasEnchants.EnchantmentManager.ClearEnchantment)
                });
            }

            return result;
        }

        /// <summary>
        /// 获取清除附魔的包装器
        /// </summary>
        public static EnchantmentWrapper GetClearEnchantment() {
            if (!Has)
                return default;
            return GetClearEnchantmentInner();
        }
        [CWRJITEnabled]
        private static EnchantmentWrapper GetClearEnchantmentInner() {
            var clearEnchant = CalamityMod.UI.CalamitasEnchants.EnchantmentManager.ClearEnchantment;
            return new EnchantmentWrapper {
                Name = clearEnchant.Name,
                Description = clearEnchant.Description,
                IconTexturePath = clearEnchant.IconTexturePath,
                InternalId = -1,
                IsClearEnchantment = true
            };
        }

        /// <summary>
        /// 应用附魔到物品
        /// </summary>
        public static void ApplyEnchantmentToItem(Item item, EnchantmentWrapper wrapper, Action<Item> creationEffect = null) {
            if (!Has || item == null || item.IsAir)
                return;
            ApplyEnchantmentToItemInner(item, wrapper, creationEffect);
        }
        [CWRJITEnabled]
        private static void ApplyEnchantmentToItemInner(Item item, EnchantmentWrapper wrapper, Action<Item> creationEffect) {
            int oldPrefix = item.prefix;
            item.SetDefaults(item.type);
            item.Prefix(oldPrefix);

            if (wrapper.IsClearEnchantment) {
                item.Calamity().AppliedEnchantment = null;
                item.Prefix(oldPrefix);
            }
            else {
                //通过Name和Description重新匹配Enchantment
                var allEnchantments = CalamityMod.UI.CalamitasEnchants.EnchantmentManager.GetValidEnchantmentsForItem(item);
                CalamityMod.UI.CalamitasEnchants.Enchantment? targetEnchant = null;

                foreach (var ench in allEnchantments) {
                    if (ench.Name.Value == wrapper.Name.Value && ench.Description.Value == wrapper.Description.Value) {
                        targetEnchant = ench;
                        break;
                    }
                }

                if (targetEnchant.HasValue) {
                    item.Calamity().AppliedEnchantment = targetEnchant.Value;
                    creationEffect?.Invoke(item);
                    targetEnchant.Value.CreationEffect?.Invoke(item);

                    if (CalamityMod.UI.CalamitasEnchants.EnchantmentManager.ItemUpgradeRelationship.TryGetValue(item.type, out var newID)) {
                        item.SetDefaults(newID);
                        item.Prefix(oldPrefix);
                    }
                }
            }
        }

        /// <summary>
        /// 获取物品当前的附魔
        /// </summary>
        public static EnchantmentWrapper? GetItemEnchantment(Item item) {
            if (!Has || item == null || item.IsAir)
                return null;
            return GetItemEnchantmentInner(item);
        }
        [CWRJITEnabled]
        private static EnchantmentWrapper? GetItemEnchantmentInner(Item item) {
            var appliedEnchant = item.Calamity().AppliedEnchantment;
            if (!appliedEnchant.HasValue)
                return null;

            var ench = appliedEnchant.Value;
            return new EnchantmentWrapper {
                Name = ench.Name,
                Description = ench.Description,
                IconTexturePath = ench.IconTexturePath,
                InternalId = 0,
                IsClearEnchantment = ench.Equals(CalamityMod.UI.CalamitasEnchants.EnchantmentManager.ClearEnchantment)
            };
        }

        /// <summary>
        /// 检查附魔是否可用于物品
        /// </summary>
        public static bool IsEnchantmentValidForItem(Item item, EnchantmentWrapper wrapper) {
            if (!Has || item == null || item.IsAir)
                return false;
            return IsEnchantmentValidForItemInner(item, wrapper);
        }
        [CWRJITEnabled]
        private static bool IsEnchantmentValidForItemInner(Item item, EnchantmentWrapper wrapper) {
            var validEnchantments = CalamityMod.UI.CalamitasEnchants.EnchantmentManager.GetValidEnchantmentsForItem(item);

            foreach (var ench in validEnchantments) {
                if (ench.Name.Value == wrapper.Name.Value && ench.Description.Value == wrapper.Description.Value)
                    return true;
            }

            return false;
        }
        #endregion

        #region 加载联动修改内容
        public static MethodBase BossHealthBarManager_Draw_Method;
        public static MethodBase calamityUtils_GetReworkedReforge_Method;
        internal delegate void On_DisplayLocalizedText_Dalegate(string key, Color? textColor = null);

        internal static void LoadComders() {
            if (!Has) {
                return;
            }
            try {
                LoadComdersInner();
            } catch { }
        }
        [CWRJITEnabled]
        internal static void LoadComdersInner() {
            //这一切不该发生，灾厄没有在这里留下任何可扩展的接口，如果想要那该死血条的为第三方事件靠边站，只能这么做，至少这是我目前能想到的方法
            BossHealthBarManager_Draw_Method = typeof(BossHealthBarManager)
                .GetMethod("Draw", BindingFlags.Instance | BindingFlags.Public);
            if (BossHealthBarManager_Draw_Method != null) {
                VaultHook.Add(BossHealthBarManager_Draw_Method, On_BossHealthBarManager_Draw_Hook);
            }
            else {
                CWRUtils.LogFailedLoad("BossHealthBarManager_Draw_Method", "CalamityMod.BossHealthBarManager");
            }

            calamityUtils_GetReworkedReforge_Method = typeof(CalamityUtils)
                .GetMethod("GetReworkedReforge", BindingFlags.Static | BindingFlags.NonPublic);
            if (calamityUtils_GetReworkedReforge_Method != null) {
                VaultHook.Add(calamityUtils_GetReworkedReforge_Method, OnGetReworkedReforgeHook);
            }
            else {
                CWRUtils.LogFailedLoad("calamityUtils_GetReworkedReforge_Method", "CalamityUtils.GetReworkedReforge");
            }

            MethodInfo methodInfo = typeof(CalamityUtils).GetMethod("DisplayLocalizedText", BindingFlags.Static | BindingFlags.Public);
            VaultHook.Add(methodInfo, OnDisplayLocalizedTextHook);

            //我鸡巴的还能说什么？为什么这么多人喜欢改同一个东西？Fuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuck
            if (CWRMod.Instance.luminance != null) {
                var utType = CWRUtils.GetTargetTypeInStringKey(CWRUtils.GetModTypes(CWRMod.Instance.luminance), "Utilities");
                methodInfo = utType.GetMethod("BroadcastLocalizedText", BindingFlags.Static | BindingFlags.Public);
                VaultHook.Add(methodInfo, OnDisplayLocalizedTextHook);
            }

            var math = typeof(CalamityPlayer).GetMethod("ProvideStealthStatBonuses", BindingFlags.Instance | BindingFlags.NonPublic);
            VaultHook.Add(math, OnProvideStealthStatBonusesHook);
        }

        [CWRJITEnabled]
        private static void On_BossHealthBarManager_Draw_Hook(On_BossHealthBarManager_Draw_Dalegate orig, object obj, SpriteBatch spriteBatch, IBigProgressBar currentBar, BigProgressBarInfo info) {
            int startHeight = 100;
            int x = Main.screenWidth - 420;
            int y = Main.screenHeight - startHeight;
            if (Main.playerInventory || VaultUtils.IsInvasion()) {
                x -= 250;
            }
            Vector2 modifyPos = MuraChargeUI.Instance.ModifyBossHealthBarManagerPositon(x, y);
            x = (int)modifyPos.X;
            y = (int)modifyPos.Y;
            //谢天谢地BossHealthBarManager.Bars和BossHealthBarManager.BossHPUI是公开的
            foreach (BossHealthBarManager.BossHPUI ui in BossHealthBarManager.Bars) {
                ui.Draw(spriteBatch, x, y);
                y -= BossHealthBarManager.BossHPUI.VerticalOffsetPerBar;
            }
        }

        [CWRJITEnabled]
        internal static int OnGetReworkedReforgeHook(On_GetReworkedReforge_Dalegate orig
            , Item item, UnifiedRandom rand, int currentPrefix) {
            int reset = orig.Invoke(item, rand, currentPrefix);
            reset = OnCalamityReforgeEvent.HandleCalamityReforgeModificationDueToMissingItemLoader(item, rand, currentPrefix);
            return reset;
        }

        [CWRJITEnabled]
        internal static void OnDisplayLocalizedTextHook(On_DisplayLocalizedText_Dalegate orig, string key, Color? textColor = null) {
            Color color = textColor ?? Color.White;
            if (VaultLoad.LoadenContent) {
                bool result = true;
                foreach (var d in ModifyDisplayText.Instances) {
                    if (!d.Alive(Main.LocalPlayer)) {
                        continue;
                    }
                    bool newResult = d.Handle(ref key, ref color);
                    if (!newResult) {
                        result = false;
                    }
                }
                if (!result) {
                    return;
                }
            }

            orig.Invoke(key, color);
        }

        [CWRJITEnabled]
        private static void OnProvideStealthStatBonusesHook(Action<CalamityPlayer> orig, CalamityPlayer calamityPlayer) {
            if (calamityPlayer.Player.CWR().IsUnsunghero) {
                if (!calamityPlayer.wearingRogueArmor || calamityPlayer.rogueStealthMax <= 0) {
                    return;
                }

                Item item = calamityPlayer.Player.GetItem();
                int realUseTime = Math.Max(item.useTime, item.useAnimation);
                double useTimeFactor = 0.75 + 0.75 * Math.Log(realUseTime + 2D, 4D);
                //直接使用固定的基础时间，固定为 4 秒
                double stealthGenFactor = Math.Max(Math.Pow(4f, 2D / 3D), 1.5);

                double stealthAddedDamage = calamityPlayer.rogueStealth * BalancingConstants.UniversalStealthStrikeDamageFactor * useTimeFactor * stealthGenFactor;
                calamityPlayer.stealthDamage += (float)stealthAddedDamage;

                calamityPlayer.Player.aggro -= (int)(calamityPlayer.rogueStealth * 300f);

                return;
            }

            orig.Invoke(calamityPlayer);
        }
        #endregion
    }
}