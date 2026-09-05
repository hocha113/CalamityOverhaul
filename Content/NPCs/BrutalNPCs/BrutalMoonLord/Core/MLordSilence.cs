using CalamityOverhaul.Common;
using InnoVault.GameSystem;
using Microsoft.Xna.Framework.Audio;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMoonLord.Core
{
    /// <summary>
    /// 黑闪残血底牌拍的死寂：蓄力一秒后整个世界的声音被黑球一点点吸走。
    /// 音效/环境音量按 <see cref="FadeOutFrames"/> 渐落到 0（存取还原，用户中途动滑条即改记基准），
    /// 音乐经 <see cref="MusicDirector"/> 的音量倍率认领同步淡出；
    /// 世界越安静，黑球自己的嗡鸣越浮上来：绕开 SoundEngine 音量链的裸 <see cref="SoundEffectInstance"/>
    /// （两条失谐低鸣互相拍频出搏动 + 静电嘶声 + 电噼啪），音高、音量、搏动频率随张力爬升，
    /// 直到爆点一声 <see cref="Release"/> 把全部声音即刻放回（音乐留白一拍后淡入）。
    /// 持有制：持有者（状态/黑洞弹体）每帧 <see cref="Hold"/>，断供超宽限自动还音；
    /// 全程只是本地表现，各端各自吞自己的声音；守护栏防演出异常中断后卡成永久静音
    /// </summary>
    internal static class MLordSilence
    {
        /// <summary>断供宽限帧：掷出交棒时弹体生成包可能晚到数帧，此窗内不还音</summary>
        private const int NoHoldGrace = 24;
        /// <summary>守护栏：持续压声超此帧数强制还音</summary>
        private const int MaxActiveFrames = 15 * 60;
        /// <summary>音量淡出帧数：从开始持有到全静</summary>
        private const float FadeOutFrames = 80f;
        /// <summary>爆点还音后音乐留白的帧数：让爆炸独占一拍</summary>
        private const int MusicTailFrames = 48;
        /// <summary>留白后音乐淡回的帧数</summary>
        private const float MusicReturnFrames = 70f;

        /// <summary>正在压声</summary>
        internal static bool Active { get; private set; }
        /// <summary>音乐认领是否仍应在场（淡出 + 死寂 + 爆后留白 + 淡回）</summary>
        internal static bool MusicClaimed => Active || musicTail > 0 || musicReturn < 1f;
        /// <summary>音乐音量倍率：淡出期 1→0，留白 0，淡回 0→1</summary>
        internal static float MusicLevel {
            get {
                if (Active) {
                    return 1f - muteLevel;
                }
                if (musicTail > 0) {
                    return 0f;
                }
                return MathHelper.Clamp(musicReturn, 0f, 1f);
            }
        }

        private static uint lastHoldTick;
        private static uint releasedTick;
        private static float tension;
        private static int activeFrames;
        /// <summary>压声进度 0~1：0 全响，1 全静</summary>
        private static float muteLevel;
        private static int musicTail;
        private static float musicReturn = 1f;
        private static float savedSound = -1f;
        private static float savedAmbient = -1f;
        private static float lastAppliedSound = -1f;
        private static float lastAppliedAmbient = -1f;
        private static bool stopRequested;
        private static bool wasPaused;

        //裸音源：不走 SoundEngine，所以 Main.soundVolume 压 0 时仍可闻
        private static SoundEffectInstance droneLow;
        private static SoundEffectInstance droneHigh;
        private static SoundEffectInstance hiss;
        private static SoundEffect crackle;
        private static bool humBroken;
        private static bool humPaused;
        private static int crackleIn;
        private static float throbPhase;

        /// <summary>持有者每帧调用（仅非服务端）。张力 0~1 决定嗡鸣的音高、音量与搏动频率</summary>
        public static void Hold(float tension01) {
            if (VaultUtils.isServer || Main.gameMenu) {
                return;
            }
            uint now = Main.GameUpdateCount;
            //同帧已被爆点/失手放行：不许再压回去
            if (releasedTick != 0 && releasedTick == now) {
                return;
            }
            lastHoldTick = now;
            tension = MathHelper.Clamp(tension01, 0f, 1f);
        }

        /// <summary>立即还音（爆点/失手的音效之前调用）：音效环境音即刻回满，音乐留白一拍再淡回</summary>
        public static void Release() {
            if (VaultUtils.isServer) {
                return;
            }
            releasedTick = Main.GameUpdateCount;
            lastHoldTick = 0;
            if (Active) {
                Disengage(tail: true);
            }
        }

        /// <summary>每帧驱动（UpdateAudio 之后，暂停与菜单期同样运行）</summary>
        internal static void Tick() {
            if (Main.dedServ) {
                return;
            }
            if (stopRequested) {
                stopRequested = false;
                StopHum();
            }
            if (Main.gameMenu) {
                if (Active) {
                    Disengage(tail: false);
                }
                musicTail = 0;
                musicReturn = 1f;
                lastHoldTick = 0;
                return;
            }
            //暂停期冻结：持有者不跑，不能按断供处理；嗡鸣随游戏一起停。
            //解除暂停的首帧把持有时间戳刷到当下，暂停期间累积的帧差不算断供
            if (Main.gamePaused) {
                SetHumPaused(true);
                wasPaused = true;
                return;
            }
            if (wasPaused) {
                wasPaused = false;
                if (Active) {
                    lastHoldTick = Main.GameUpdateCount;
                }
            }
            SetHumPaused(false);

            if (!Active) {
                if (musicTail > 0) {
                    musicTail--;
                }
                else if (musicReturn < 1f) {
                    musicReturn = Math.Min(1f, musicReturn + 1f / MusicReturnFrames);
                }
            }

            uint now = Main.GameUpdateCount;
            bool held = lastHoldTick != 0 && now >= lastHoldTick && now - lastHoldTick <= NoHoldGrace;
            if (held && !Active) {
                Engage();
            }
            else if (!held && Active) {
                Disengage(tail: true);
            }
            if (!Active) {
                return;
            }

            if (++activeFrames > MaxActiveFrames) {
                CWRMod.Instance?.Logger?.Warn("[MLordSilence] 死寂持续超过守护栏，强制还音");
                lastHoldTick = 0;
                Disengage(tail: false);
                return;
            }
            ApplyFade();
            UpdateHum();
        }

        /// <summary>世界收场/卸载：还原音量、忘掉持有；裸音源的停止推给主线程的下一次 Tick</summary>
        internal static void Clear() {
            if (Active) {
                RestoreVolumes();
                Active = false;
            }
            activeFrames = 0;
            muteLevel = 0f;
            musicTail = 0;
            musicReturn = 1f;
            lastHoldTick = 0;
            releasedTick = 0;
            tension = 0f;
            stopRequested = true;
        }

        /// <summary>模组卸载：释放裸音源</summary>
        internal static void Unload() {
            Clear();
            try {
                droneLow?.Dispose();
                droneHigh?.Dispose();
                hiss?.Dispose();
            }
            catch (Exception) {
                //音频设备已随游戏退出时 Dispose 可能抛，卸载路径不追究
            }
            droneLow = null;
            droneHigh = null;
            hiss = null;
            crackle = null;
            humBroken = false;
        }

        #region 音量闸

        private static void Engage() {
            savedSound = Main.soundVolume;
            savedAmbient = Main.ambientVolume;
            lastAppliedSound = savedSound;
            lastAppliedAmbient = savedAmbient;
            muteLevel = 0f;
            Active = true;
            activeFrames = 0;
            musicTail = 0;
            musicReturn = 1f;
            crackleIn = 30;
            throbPhase = 0f;
            StartHum();
        }

        private static void Disengage(bool tail) {
            RestoreVolumes();
            Active = false;
            activeFrames = 0;
            muteLevel = 0f;
            musicTail = tail ? MusicTailFrames : 0;
            musicReturn = tail ? 0f : 1f;
            StopHum();
        }

        /// <summary>压声期逐帧推进淡出：用户动了滑条就把新值记成基准（还音时还他改过的值），再按当前进度压下去</summary>
        private static void ApplyFade() {
            muteLevel = Math.Min(1f, muteLevel + 1f / FadeOutFrames);
            float keep = 1f - muteLevel;

            if (Main.soundVolume != lastAppliedSound) {
                savedSound = Main.soundVolume;
            }
            lastAppliedSound = savedSound * keep;
            Main.soundVolume = lastAppliedSound;

            if (Main.ambientVolume != lastAppliedAmbient) {
                savedAmbient = Main.ambientVolume;
            }
            lastAppliedAmbient = savedAmbient * keep;
            Main.ambientVolume = lastAppliedAmbient;
        }

        private static void RestoreVolumes() {
            if (savedSound >= 0f) {
                Main.soundVolume = savedSound;
                savedSound = -1f;
                lastAppliedSound = -1f;
            }
            if (savedAmbient >= 0f) {
                Main.ambientVolume = savedAmbient;
                savedAmbient = -1f;
                lastAppliedAmbient = -1f;
            }
        }

        #endregion

        #region 嗡鸣

        private static void StartHum() {
            if (humBroken) {
                return;
            }
            try {
                droneLow ??= CreateLoop(SoundID.DD2_EtherianPortalIdleLoop);
                droneHigh ??= CreateLoop(SoundID.DD2_EtherianPortalIdleLoop);
                hiss ??= CreateLoop(SoundID.BlizzardInsideBuildingLoop);
                crackle ??= LoadEffect(SoundID.Item93);
            }
            catch (Exception ex) {
                humBroken = true;
                CWRMod.Instance?.Logger?.Warn($"[MLordSilence] 嗡鸣音源加载失败，死寂期改为纯静音：{ex.Message}");
                return;
            }
            Restart(droneLow);
            Restart(droneHigh);
            Restart(hiss);
            humPaused = false;
        }

        private static void StopHum() {
            StopInstance(droneLow);
            StopInstance(droneHigh);
            StopInstance(hiss);
            humPaused = false;
        }

        private static void StopInstance(SoundEffectInstance instance) {
            if (instance != null && !instance.IsDisposed) {
                instance.Stop(true);
            }
        }

        private static void SetHumPaused(bool paused) {
            if (!Active || humBroken || humPaused == paused) {
                return;
            }
            humPaused = paused;
            if (paused) {
                droneLow?.Pause();
                droneHigh?.Pause();
                hiss?.Pause();
            }
            else {
                droneLow?.Resume();
                droneHigh?.Resume();
                hiss?.Resume();
            }
        }

        /// <summary>
        /// 嗡鸣随世界淡出而浮现（压声过 15% 起浮，75% 时满）：
        /// 两条同源低鸣失谐叠放，拍频天然搏动，再叠一层随张力加快的音量搏动；静电嘶声铺面；电噼啪越到后段越密。
        /// 用户的音效音量当总闸：他调多小，嗡鸣就多小
        /// </summary>
        private static void UpdateHum() {
            if (humBroken || droneLow == null || droneHigh == null || hiss == null || crackle == null) {
                return;
            }
            float emerge = MathHelper.Clamp((muteLevel - 0.15f) / 0.6f, 0f, 1f);
            float master = MathHelper.Clamp(savedSound, 0f, 1f) * emerge;
            float t = tension;

            //搏动：约 0.7Hz 爬到 2.4Hz，深度随张力加大（黑洞的心跳）
            throbPhase += 0.07f + 0.18f * t;
            float throb = 0.5f + 0.5f * MathF.Sin(throbPhase);
            float depth = 0.25f + 0.25f * t;
            float lowGain = 1f - depth * (1f - throb);
            float highGain = 1f - depth * throb;
            float tremor = MathF.Sin((float)Main.timeForVisualEffects * 0.31f) * 0.02f * t;

            droneLow.Volume = MathHelper.Clamp(master * (0.55f + 0.40f * t) * lowGain, 0f, 1f);
            droneLow.Pitch = MathHelper.Clamp(-0.70f + 0.50f * t + tremor, -1f, 1f);
            droneHigh.Volume = MathHelper.Clamp(master * (0.35f + 0.40f * t) * highGain, 0f, 1f);
            droneHigh.Pitch = MathHelper.Clamp(-0.38f + 0.58f * t - tremor, -1f, 1f);
            hiss.Volume = MathHelper.Clamp(master * (0.14f + 0.30f * t), 0f, 1f);
            hiss.Pitch = MathHelper.Clamp(0.05f + 0.40f * t, -1f, 1f);

            //电噼啪：越到后段越密，音高压低读作空间在崩
            if (--crackleIn <= 0) {
                crackleIn = Main.rand.Next(9, 48 - (int)(26f * t));
                crackle.Play(MathHelper.Clamp(master * (0.20f + 0.32f * t), 0f, 1f),
                    Main.rand.NextFloat(-0.7f, -0.2f), Main.rand.NextFloat(-0.5f, 0.5f));
            }
        }

        private static void Restart(SoundEffectInstance instance) {
            if (instance == null || instance.IsDisposed) {
                return;
            }
            if (instance.State != SoundState.Stopped) {
                instance.Stop(true);
            }
            instance.Volume = 0f;
            instance.Play();
        }

        private static SoundEffectInstance CreateLoop(SoundStyle style) {
            SoundEffectInstance instance = LoadEffect(style).CreateInstance();
            instance.IsLooped = true;
            return instance;
        }

        /// <summary>取原版音源本体（有变体则取首个），走 Terraria/ 资源域</summary>
        private static SoundEffect LoadEffect(SoundStyle style) {
            string path = style.SoundPath;
            if (style.Variants is { Length: > 0 }) {
                path += style.Variants[0];
            }
            return ModContent.Request<SoundEffect>(path, AssetRequestMode.ImmediateLoad).Value;
        }

        #endregion
    }

    /// <summary>每帧驱动壳：UpdateAudio 之后，暂停/菜单同样运行</summary>
    internal sealed class MLordSilenceAudio : IUpdateAudio
    {
        void IUpdateAudio.PostUpdateAudio() => MLordSilence.Tick();
    }

    /// <summary>
    /// 死寂期音乐认领：只干预音量不写曲目（倍率随 <see cref="MLordSilence.MusicLevel"/> 淡出、留白、淡回），
    /// 仪式档最高子权重，不给 Boss 曲让位（月总本身就是 Boss）；音量存取还原由仲裁器统一做
    /// </summary>
    internal sealed class MLordSilenceMusicClaim : MusicClaim
    {
        public override MusicTier Tier => MusicTier.Ceremony;
        public override int SubWeight => 200;
        public override float VolumeScale => MLordSilence.MusicLevel;
        public override int HardTimeoutFrames => 20 * 60;
        public override bool ShouldPlay() => MLordSilence.MusicClaimed;
        public override int GetMusicSlot() => -1;
    }

    /// <summary>生命周期壳：世界收场/退出前还原音量，卸载释放音源</summary>
    internal sealed class MLordSilenceSystem : ModSystem
    {
        public override void Unload() => MLordSilence.Unload();
        public override void ClearWorld() => MLordSilence.Clear();
        public override void OnWorldUnload() => MLordSilence.Clear();
        public override void PreSaveAndQuit() => MLordSilence.Clear();
    }
}
