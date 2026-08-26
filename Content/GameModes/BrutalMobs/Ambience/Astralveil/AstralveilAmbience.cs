using ReLogic.Utilities;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Ambience.Astralveil
{
    /// <summary>
    /// 星辉瘟疫氛围风味表：靛/橙双色板、昼夜成分、本地在场强度，
    /// 供异星尘、双色潮、星辉矛与感染绽放共用（镜像 EvilBiomeFX 的风味表模式）
    /// </summary>
    internal static class AstralveilFX
    {
        /// <summary>亮靛（加色敷料主色）</summary>
        public static readonly Color Indigo = new(104, 92, 238);
        /// <summary>亮橙（加色敷料副色）</summary>
        public static readonly Color Orange = new(255, 152, 58);
        /// <summary>淡靛（高光）</summary>
        public static readonly Color IndigoPale = new(170, 160, 255);
        /// <summary>淡橙（高光）</summary>
        public static readonly Color OrangePale = new(255, 204, 130);
        /// <summary>暗靛底（真 alpha 暗层专用，加色批物理上压不出暗色）</summary>
        public static readonly Color IndigoDeep = new(30, 24, 74);

        /// <summary>本地氛围在场强度 0~1（进出星辉瘟疫淡入淡出，纯客户端演出量）</summary>
        public static float Presence { get; internal set; }

        /// <summary>夜色权重 0~1（昼夜平滑过渡，驱动双色成分与音量微调）</summary>
        public static float NightMix { get; internal set; }

        /// <summary>Boss 在场时氛围让位系数（纯视觉保留但减弱）</summary>
        public static float BossDim => CWRWorld.HasBoss ? 0.55f : 1f;

        /// <summary>当前靛色成分占比：夜里靛主导，白天橙略多（两色成分随昼夜微调）</summary>
        public static float IndigoFraction => 0.45f + 0.25f * NightMix;

        /// <summary>风味粉尘（统一火把系：无重力发光，行为一致）</summary>
        public static int DustFor(bool indigo) => indigo ? DustID.PurpleTorch : DustID.OrangeTorch;

        /// <summary>黑底贴图进 AlphaBlend 批的加色写法：A=0 只加光</summary>
        public static Color A0(Color color) => new(color.R, color.G, color.B, (byte)0);

        internal static void Reset() {
            Presence = 0f;
            NightMix = 0f;
        }
    }

    /// <summary>
    /// 「异星尘」常态氛围层（纯客户端）：靛/橙双色星屑浮游 + 扭曲耳鸣与异星低频脉冲双底噪。
    /// 环境声槽位管理镜像 GhostRainAmbience / OldNetAmbience：循环丢失即补挂、音量在回调里逐帧走、
    /// 离开群系随 Presence 淡出、Boss 在场整体减弱
    /// </summary>
    internal class AstralveilAmbienceSystem : ModSystem
    {
        /// <summary>星屑生成密度（满强度约 13 粒/秒，屏内预算内）</summary>
        private const float MoteChancePerFrame = 0.22f;

        private static SlotId tinnitusSlot;
        private static SlotId pulseSlot;
        //扭曲耳鸣：吹雪闷响拉高音调成细薄啸鸣；异星脉冲：以太门空鸣压到低频
        private static readonly SoundStyle TinnitusStyle =
            SoundID.BlizzardInsideBuildingLoop with { IsLooped = true, MaxInstances = 1 };
        private static readonly SoundStyle PulseStyle =
            SoundID.DD2_EtherianPortalIdleLoop with { IsLooped = true, MaxInstances = 1 };

        public override void PostUpdateEverything() {
            if (Main.dedServ) {
                return;
            }

            bool inBiome = !Main.gameMenu && GameModeSystem.BrutalActive && CWRRef.Has
                && Main.LocalPlayer.active && Main.LocalPlayer.GetPlayerZoneAstral();
            float target = inBiome ? 1f : 0f;
            float presence = AstralveilFX.Presence;
            presence = Math.Abs(target - presence) < 0.008f
                ? target : MathHelper.Lerp(presence, target, 0.045f);
            if (presence < 0.004f && target <= 0f) {
                presence = 0f;
            }
            AstralveilFX.Presence = presence;
            AstralveilFX.NightMix = MathHelper.Lerp(AstralveilFX.NightMix, Main.dayTime ? 0f : 1f, 0.02f);

            if (presence <= 0.01f) {
                return;
            }
            if (!Main.gamePaused) {
                SpawnDriftMotes();
            }
            UpdateAmbientLoops();
        }

        public override void ClearWorld() {
            if (!Main.dedServ) {
                AstralveilFX.Reset();
            }
        }

        /// <summary>星屑浮游：屏内随机点起一粒双色火把尘，缓慢上飘；偶发大颗闪星</summary>
        private static void SpawnDriftMotes() {
            if (Main.rand.NextFloat() >= MoteChancePerFrame * AstralveilFX.Presence * AstralveilFX.BossDim) {
                return;
            }
            Vector2 pos = Main.screenPosition + new Vector2(
                Main.rand.NextFloat(-40f, Main.screenWidth + 40f),
                Main.rand.NextFloat(-40f, Main.screenHeight + 40f));
            Point tile = pos.ToTileCoordinates();
            if (!WorldGen.InWorld(tile.X, tile.Y, 10) || WorldGen.SolidTile(tile.X, tile.Y)) {
                return;
            }
            bool indigo = Main.rand.NextFloat() < AstralveilFX.IndigoFraction;
            bool twinkle = Main.rand.NextBool(7);
            Dust dust = Dust.NewDustPerfect(pos, AstralveilFX.DustFor(indigo),
                new Vector2(Main.rand.NextFloat(-0.22f, 0.22f), -Main.rand.NextFloat(0.06f, 0.38f)),
                140, default, twinkle ? Main.rand.NextFloat(1.15f, 1.4f) : Main.rand.NextFloat(0.7f, 1.05f));
            dust.noGravity = true;
        }

        //循环丢失（切场景/音量档变化）就补挂；音量在回调里逐帧走
        private static void UpdateAmbientLoops() {
            if (Main.gameMenu || AstralveilFX.Presence < 0.05f) {
                return;
            }
            if (!SoundEngine.TryGetActiveSound(tinnitusSlot, out _)) {
                tinnitusSlot = SoundEngine.PlaySound(TinnitusStyle, null, UpdateTinnitus);
            }
            if (!SoundEngine.TryGetActiveSound(pulseSlot, out _)) {
                pulseSlot = SoundEngine.PlaySound(PulseStyle, null, UpdatePulse);
            }
        }

        //扭曲耳鸣：音量刻意压低并缓慢游移，不适感克制、长时间可听
        private static bool UpdateTinnitus(ActiveSound sound) {
            float presence = AstralveilFX.Presence;
            if (Main.gameMenu || presence <= 0.003f) {
                return false;
            }
            float waver = 0.5f + 0.5f * MathF.Sin(Main.GameUpdateCount * 0.011f);
            sound.Volume = presence * AstralveilFX.BossDim
                * (0.045f + 0.028f * waver) * (1f + 0.18f * AstralveilFX.NightMix);
            sound.Pitch = 0.85f;
            sound.Position = null;
            return true;
        }

        //异星低频脉冲：音量走 ~0.36Hz 呼吸包络，读作一记一记的脉动而非平铺嗡鸣
        private static bool UpdatePulse(ActiveSound sound) {
            float presence = AstralveilFX.Presence;
            if (Main.gameMenu || presence <= 0.003f) {
                return false;
            }
            float throb = 0.5f + 0.5f * MathF.Sin(Main.GameUpdateCount * 0.038f);
            throb *= throb;
            sound.Volume = presence * AstralveilFX.BossDim
                * (0.055f + 0.105f * throb) * (1f + 0.15f * AstralveilFX.NightMix);
            sound.Pitch = -0.85f;
            sound.Position = null;
            return true;
        }
    }
}
