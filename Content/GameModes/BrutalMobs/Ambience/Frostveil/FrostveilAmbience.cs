using CalamityOverhaul.Content.GameModes.BrutalMobs.Ambience.Frostveil.Projectiles;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using ReLogic.Utilities;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Ambience.Frostveil
{
    /// <summary>
    /// 暴雪吟：残酷模式地表雪原的常态氛围主控（纯客户端表现，服务端早退）。
    /// 持两条环境声循环（暴风雪呼啸 + 风雪墙逼近风啸，镜像 OldNetAmbience 的
    /// SlotId 补挂制）、风雪流丝与晶闪的密度预算、晴夜极光带的低频调度，
    /// 以及风雪墙雪幕的本地白幕通道。所有 static 均为屏幕级演出量，非逐玩家数据
    /// </summary>
    internal class FrostveilAmbience : ModSystem
    {
        /// <summary>本地在场强度 0~1（进出雪原缓升缓降）</summary>
        internal static float Presence { get; private set; }

        /// <summary>风雪墙雪幕白化 0~1（由墙实体逐帧上报，此处包络）</summary>
        internal static float WhiteoutVeil { get; private set; }
        private static float veilTarget;

        /// <summary>极光带强度 0~1（晴夜低频事件，纯本地演出）</summary>
        internal static float AuroraIntensity { get; private set; }
        /// <summary>极光带仍需在场（含渐出尾巴）</summary>
        internal static bool AuroraVisible => AuroraIntensity > 0.004f;
        /// <summary>本次极光事件的形状种子（每场事件换一次）</summary>
        internal static float AuroraSeed { get; private set; } = 3.7f;

        private static int auroraLife;
        private static int auroraCooldown;
        private static int auroraRollTimer;

        //环境声循环槽（丢失补挂，音量在回调里逐帧走）
        private static SlotId howlSlot;
        private static SlotId waveSlot;
        private static readonly SoundStyle HowlStyle =
            SoundID.BlizzardStrongLoop with { IsLooped = true, MaxInstances = 1 };
        private static readonly SoundStyle WaveHowlStyle =
            SoundID.BlizzardInsideBuildingLoop with { IsLooped = true, MaxInstances = 1 };

        /// <summary>最近一面风雪墙到本地玩家的距离（本帧扫描缓存，声回调消费）</summary>
        private static float nearestWaveDist = float.MaxValue;

        //粒子预算累加器
        private static float flakeAcc;
        private static float glintAcc;

        /// <summary>常态粒子每秒预算上限（风雪流丝）</summary>
        private const float FlakePerSecCap = 30f;
        /// <summary>晶闪每秒预算</summary>
        private const float GlintPerSec = 4f;

        public override void ClearWorld() => HardReset();
        public override void Unload() => HardReset();

        private static void HardReset() {
            Presence = 0f;
            WhiteoutVeil = 0f;
            veilTarget = 0f;
            AuroraIntensity = 0f;
            auroraLife = 0;
            auroraCooldown = 0;
            auroraRollTimer = 0;
            nearestWaveDist = float.MaxValue;
            flakeAcc = 0f;
            glintAcc = 0f;
        }

        /// <summary>风雪墙每帧上报本地雪幕强度（取当帧最大值）</summary>
        internal static void ReportWaveVeil(float strength) {
            if (strength > veilTarget) {
                veilTarget = MathHelper.Clamp(strength, 0f, 1f);
            }
        }

        /// <summary>约 60 瓦格内有存活城镇 NPC（城镇安宁公约，多个系统共读）</summary>
        internal static bool NearTown(Vector2 pos) {
            foreach (NPC npc in Main.ActiveNPCs) {
                if (!npc.townNPC) {
                    continue;
                }
                if (Vector2.DistanceSquared(npc.Center, pos) < 960f * 960f) {
                    return true;
                }
            }
            return false;
        }

        public override void PostUpdateEverything() {
            if (Main.dedServ) {
                return;
            }

            Player player = Main.LocalPlayer;
            bool inZone = !Main.gameMenu && player != null && player.active
                && FrostveilPlayer.InZone(player);
            Presence = MathHelper.Lerp(Presence, inZone ? 1f : 0f, 0.03f);
            if (!inZone && Presence < 0.003f) {
                Presence = 0f;
            }

            //雪幕包络：快进慢出，收完本帧上报
            WhiteoutVeil = MathHelper.Lerp(WhiteoutVeil, veilTarget,
                veilTarget > WhiteoutVeil ? 0.2f : 0.07f);
            if (WhiteoutVeil < 0.003f && veilTarget <= 0f) {
                WhiteoutVeil = 0f;
            }
            veilTarget = 0f;

            ScanNearestWave(player);
            UpdateAurora(inZone);

            if (Presence < 0.02f || Main.gamePaused) {
                return;
            }
            UpdateLoops();
            SpawnFlakes();
            SpawnGlints();
        }

        //==================== 环境声 ====================

        private static void ScanNearestWave(Player player) {
            if (player == null || !player.active) {
                nearestWaveDist = float.MaxValue;
                return;
            }
            //近两帧无风雪墙盖戳且上帧不在可闻界内：跳过全表扫描
            //（时停中墙 AI 停摆时靠"上帧可闻"闩锁继续扫，逼近风啸不断音）
            if (!FrostveilGaleWallProj.PresenceStamp.ActiveWithin() && nearestWaveDist >= 1700f) {
                nearestWaveDist = float.MaxValue;
                return;
            }
            nearestWaveDist = float.MaxValue;
            int waveType = ModContent.ProjectileType<FrostveilGaleWallProj>();
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.type != waveType) {
                    continue;
                }
                float dist = MathF.Abs(proj.Center.X - player.Center.X);
                if (dist < nearestWaveDist) {
                    nearestWaveDist = dist;
                }
            }
        }

        private static void UpdateLoops() {
            if (Main.gameMenu) {
                return;
            }
            if (!SoundEngine.TryGetActiveSound(howlSlot, out _)) {
                howlSlot = SoundEngine.PlaySound(HowlStyle, null, UpdateHowl);
            }
            //风雪墙在可闻界内才补挂逼近风啸；静音即自杀，靠补挂制拉回
            if (nearestWaveDist < 1700f && !SoundEngine.TryGetActiveSound(waveSlot, out _)) {
                waveSlot = SoundEngine.PlaySound(WaveHowlStyle, null, UpdateWaveHowl);
            }
        }

        /// <summary>暴风雪呼啸：音量随风速与降雪上量，离开雪原随 Presence 淡出</summary>
        private static bool UpdateHowl(ActiveSound sound) {
            if (Main.gameMenu || Presence < 0.005f) {
                return false;
            }
            float windAbs = Math.Min(Math.Abs(Main.windSpeedCurrent), 1f);
            float vol = 0.12f + 0.5f * windAbs + (Main.raining ? 0.26f : 0f);
            sound.Volume = MathHelper.Clamp(vol, 0f, 0.78f) * Presence;
            sound.Pitch = -0.18f + windAbs * 0.12f;
            sound.Position = null;
            return true;
        }

        /// <summary>风雪墙逼近风啸：越近越响越尖，是墙的听觉预告通道</summary>
        private static bool UpdateWaveHowl(ActiveSound sound) {
            if (Main.gameMenu || nearestWaveDist > 1900f) {
                return false;
            }
            float near = 1f - MathHelper.Clamp((nearestWaveDist - 140f) / 1500f, 0f, 1f);
            sound.Volume = near * near * 0.85f;
            sound.Pitch = -0.3f + near * 0.42f;
            sound.Position = null;
            return true;
        }

        //==================== 风雪流丝 ====================

        private static void SpawnFlakes() {
            float windAbs = Math.Min(Math.Abs(Main.windSpeedCurrent), 1f);
            bool snowing = Main.raining;
            bool blizzard = snowing && windAbs > 0.4f;
            int tier = Math.Clamp(GameModeSystem.EffectiveTier, 1, 3);

            float perSec = 6f + 13f * windAbs + (snowing ? 9f : 0f) + (blizzard ? 7f : 0f);
            perSec *= 1f + 0.12f * (tier - 1);
            perSec = Math.Min(perSec, FlakePerSecCap) * Presence;

            flakeAcc += perSec / 60f;
            while (flakeAcc >= 1f) {
                flakeAcc -= 1f;
                SpawnOneFlake(windAbs);
            }
        }

        private static void SpawnOneFlake(float windAbs) {
            float wind = Main.windSpeedCurrent * (10f + windAbs * 5f);
            if (MathF.Abs(wind) < 2f) {
                wind = 2.4f * (Main.rand.NextBool() ? 1f : -1f);
            }
            Vector2 pos;
            if (Main.rand.NextFloat() < 0.6f) {
                //上风侧屏缘入场
                float edgeX = wind > 0f
                    ? Main.screenPosition.X - 50f
                    : Main.screenPosition.X + Main.screenWidth + 50f;
                pos = new Vector2(edgeX,
                    Main.screenPosition.Y + Main.rand.NextFloat(Main.screenHeight));
            }
            else {
                pos = new Vector2(Main.screenPosition.X + Main.rand.NextFloat(Main.screenWidth),
                    Main.screenPosition.Y - 40f);
            }
            Vector2 vel = new(wind * Main.rand.NextFloat(0.75f, 1.05f),
                Main.rand.NextFloat(0.6f, 2.4f));
            PRTLoader.NewParticle<PRT_FrostveilFlake>(pos, vel,
                new Color(226, 238, 250) * Main.rand.NextFloat(0.4f, 0.62f),
                Main.rand.NextFloat(0.8f, 1.5f))
                ?.Configure(Main.rand.Next(60, 110), wind);
        }

        //==================== 晶闪 ====================

        /// <summary>白天晴空的雪面星芒：随机屏内列向下找第一块雪/冰面，向阳才闪</summary>
        private static void SpawnGlints() {
            if (!Main.dayTime || Main.raining || Main.cloudAlpha > 0.05f) {
                glintAcc = 0f;
                return;
            }
            glintAcc += GlintPerSec / 60f * Presence;
            if (glintAcc < 1f) {
                return;
            }
            glintAcc -= 1f;

            int tileX = (int)(Main.screenPosition.X / 16f)
                + Main.rand.Next(Math.Max(Main.screenWidth / 16, 1));
            int startY = Math.Max((int)(Main.screenPosition.Y / 16f), 10);
            int endY = Math.Min(startY + Main.screenHeight / 16 + 2, Main.maxTilesY - 10);
            for (int tileY = startY; tileY < endY; tileY++) {
                if (!WorldGen.InWorld(tileX, tileY, 10) || !WorldGen.SolidTile(tileX, tileY)) {
                    continue;
                }
                Tile tile = Main.tile[tileX, tileY];
                if (tile.TileType != TileID.SnowBlock && tile.TileType != TileID.IceBlock
                    && tile.TileType != TileID.SnowBrick) {
                    return;//列内第一块实体面不是雪冰：这列不闪
                }
                Vector2 surface = new(tileX * 16f + Main.rand.NextFloat(2f, 14f), tileY * 16f - 2f);
                //暗处（洞口/檐影）不闪，只有向阳雪面有钻石尘
                if (Lighting.GetColor(tileX, tileY - 1).R < 140) {
                    return;
                }
                PRTLoader.NewParticle<PRT_DefFrostGlint>(surface, Vector2.Zero,
                    new Color(235, 248, 255), Main.rand.NextFloat(0.35f, 0.85f))
                    ?.Configure(Main.rand.Next(16, 26));
                return;
            }
        }

        //==================== 极光带调度 ====================

        private static void UpdateAurora(bool inZone) {
            bool eligible = inZone && !Main.dayTime && !Main.raining
                && Main.cloudAlpha <= 0f && Main.numClouds <= 120
                && Presence > 0.55f;

            if (auroraCooldown > 0) {
                auroraCooldown--;
            }
            if (auroraLife > 0) {
                auroraLife--;
                if (auroraLife == 0) {
                    auroraCooldown = 3600;
                }
            }
            else if (eligible && auroraCooldown <= 0 && !Main.gamePaused) {
                //低频掷签：期望约两分半一场
                if (--auroraRollTimer <= 0) {
                    auroraRollTimer = 30;
                    if (Main.rand.NextBool(300)) {
                        auroraLife = Main.rand.Next(2400, 4200);
                        AuroraSeed = Main.rand.NextFloat(0.5f, 40f);
                    }
                }
            }

            float target = auroraLife > 0 && eligible ? 1f : 0f;
            float step = target > AuroraIntensity ? 0.004f : 0.006f;
            AuroraIntensity = MathHelper.Clamp(
                AuroraIntensity + MathF.Sign(target - AuroraIntensity) * step, 0f, 1f);
            if (target <= 0f && AuroraIntensity < 0.005f) {
                AuroraIntensity = 0f;
            }
        }

        //==================== 光色 ====================

        /// <summary>暴雪压顶：降雪时把日光勒向冷灰蓝，档位不改配色只随在场强度</summary>
        public override void ModifySunLightColor(ref Color tileColor, ref Color backgroundColor) {
            if (Main.dedServ || Presence <= 0.001f || !Main.raining) {
                return;
            }
            float k = Presence * 0.5f;
            Color coldTile = new(172, 188, 205);
            Color coldBg = new(150, 168, 190);
            tileColor = Color.Lerp(tileColor, coldTile, k * 0.3f);
            backgroundColor = Color.Lerp(backgroundColor, coldBg, k * 0.45f);
        }
    }
}
