using ReLogic.Utilities;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent.Events;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Ambience.DuneStorm
{
    /// <summary>
    /// 「扬沙」常态氛围与「沙暴强化」压迫层（纯客户端演出）：
    /// 贴地风带沙粒（密度与方向随 Main.windSpeedCurrent）、呼啸风声循环
    /// （镜像 GhostRainAmbience/OldNetAmbience 的槽位管理，音量随风速/沙暴/风堑涌起）、
    /// 沙暴期间的尘幕日色压迫（氛围级，不做真黑屏遮挡）。
    /// 进入淡入、离开淡出，Main.gamePaused 时 PostUpdateEverything 天然不推进
    /// </summary>
    internal class DuneStormAmbience : ModSystem
    {
        /// <summary>本地玩家的扬沙在场强度 0~1（观察者本机演出量）</summary>
        internal static float Presence { get; private set; }

        /// <summary>沙暴压迫强度 0~1（在场 × 原版 Sandstorm.Severity 平滑）</summary>
        internal static float StormPressure { get; private set; }

        /// <summary>风堑涌起量 0~1：本帧视野内所有风堑波上报的峰值，喂给风声与沙线层</summary>
        internal static float GustSwell { get; private set; }

        //风堑波逐帧上报的待结算峰值（弹幕 AI 先跑，PostUpdateEverything 后收）
        private static float pendingSwell;

        //环境声循环槽（镜像 OldNetAmbience 的 SlotId+回调惯例）
        private static SlotId windHowlSlot;
        private static readonly SoundStyle WindHowlStyle =
            SoundID.BlizzardStrongLoop with { IsLooped = true, MaxInstances = 1 };

        /// <summary>风堑波每帧上报涌起量（客户端演出）</summary>
        internal static void ReportGustSwell(float value) {
            if (value > pendingSwell) {
                pendingSwell = value;
            }
        }

        public override void PostUpdateEverything() {
            if (Main.dedServ) {
                return;
            }

            //在场包络：残酷模式 + 地表沙漠，离开有淡出不硬切
            bool inBiome = !Main.gameMenu && GameModeSystem.BrutalActive
                && DuneStorm.InSurfaceDesert(Main.LocalPlayer);
            float target = inBiome ? 1f : 0f;
            Presence = Math.Abs(target - Presence) < 0.004f
                ? target : MathHelper.Lerp(Presence, target, 0.045f);

            //沙暴压迫：跟原版事件强度走，平滑避免硬跳
            float stormTarget = inBiome && Sandstorm.Happening
                ? MathHelper.Clamp(Sandstorm.Severity, 0f, 1f) : 0f;
            StormPressure = Math.Abs(stormTarget - StormPressure) < 0.004f
                ? stormTarget : MathHelper.Lerp(StormPressure, stormTarget, 0.03f);

            //收本帧风堑上报
            GustSwell = pendingSwell;
            pendingSwell = 0f;

            if (Presence <= 0.004f) {
                return;
            }
            UpdateWindLoop();
            UpdateAmbientSand();
        }

        //循环丢失（切场景/音量档变化）就补挂；音量在回调里逐帧走
        private void UpdateWindLoop() {
            if (Main.gameMenu) {
                return;
            }
            if (!SoundEngine.TryGetActiveSound(windHowlSlot, out _)) {
                windHowlSlot = SoundEngine.PlaySound(WindHowlStyle, null, UpdateWindHowl);
            }
        }

        //呼啸风声：音量随风速与沙暴事件，风堑预告期渐强构成听觉预告通道
        private static bool UpdateWindHowl(ActiveSound sound) {
            if (Main.gameMenu || Presence <= 0.004f) {
                return false;
            }
            float wind = DuneStorm.WindStrength01();
            sound.Volume = MathHelper.Clamp(
                Presence * (0.10f + 0.40f * wind + 0.30f * StormPressure + 0.38f * GustSwell), 0f, 0.85f);
            sound.Pitch = -0.50f + 0.28f * wind + 0.22f * GustSwell;
            sound.Position = null;
            return true;
        }

        /// <summary>
        /// 扬沙粒子：贴地风带沙粒流，密度与方向随风速，沙暴期间加密。
        /// 常态预算约 ≤0.6 粒/帧（36/s），沙暴事件短时上探约 70/s
        /// </summary>
        private static void UpdateAmbientSand() {
            if (Main.gamePaused) {
                return;
            }
            float wind = DuneStorm.WindStrength01();
            //无风也留 0.15 底密度让群系可读，随风与沙暴上量
            float chance = Presence * (0.15f + 0.45f * wind) * (1f + 1.4f * StormPressure);
            chance = Math.Min(chance, 1.15f);

            while (chance > 0f) {
                if (chance < 1f && !Main.rand.NextBool(Math.Max(1, (int)(1f / chance)))) {
                    break;
                }
                chance -= 1f;
                SpawnGroundGrain(wind);
            }
        }

        //在屏内随机列贴地起一粒风沙（仅沙系地表；找不到地则本帧作罢）
        private static void SpawnGroundGrain(float wind) {
            Player player = Main.LocalPlayer;
            float worldX = Main.screenPosition.X + Main.rand.NextFloat(-80f, Main.screenWidth + 80f);
            int tileX = (int)(worldX / 16f);
            int startY = (int)(player.Bottom.Y / 16f) - 14;
            if (!DuneStorm.TryFindGround(tileX, startY, out Vector2 ground)) {
                return;
            }
            Tile tile = Framing.GetTileSafely(tileX, (int)(ground.Y / 16f));
            if (!DuneStorm.IsSandFamily(tile.TileType)) {
                return;
            }

            float dir = Main.windSpeedCurrent >= 0f ? 1f : -1f;
            float speed = 2.5f + 6.5f * wind + 4f * StormPressure + 3f * GustSwell;
            Dust dust = Dust.NewDustPerfect(
                ground + new Vector2(0f, -Main.rand.NextFloat(2f, 14f)),
                DustID.Sand,
                new Vector2(dir * speed * Main.rand.NextFloat(0.7f, 1.15f), -Main.rand.NextFloat(0.1f, 0.7f)),
                Main.rand.Next(90, 140), default, Main.rand.NextFloat(0.8f, 1.35f));
            dust.noGravity = true;
            dust.fadeIn = 0.4f;
        }

        /// <summary>沙暴压迫：日色勒向尘沙的浑浊暖暗（氛围级压迫，禁真黑屏）</summary>
        public override void ModifySunLightColor(ref Color tileColor, ref Color backgroundColor) {
            float veil = Presence * StormPressure;
            if (veil <= 0.002f) {
                return;
            }
            Color duskTile = new(150, 124, 86);
            Color duskBg = new(112, 90, 58);
            tileColor = Color.Lerp(tileColor, duskTile, veil * 0.30f);
            backgroundColor = Color.Lerp(backgroundColor, duskBg, veil * 0.45f);
        }

        public override void ClearWorld() {
            Presence = 0f;
            StormPressure = 0f;
            GustSwell = 0f;
            pendingSwell = 0f;
        }
    }
}
