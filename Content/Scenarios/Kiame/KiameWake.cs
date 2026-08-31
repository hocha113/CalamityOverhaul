using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaResets;
using InnoVault.Cinematics;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Kiame
{
    /// <summary>
    /// 鬼雨里的死亡语义：HP 归零一律不死，由沈幽的「重启」把人从死亡拉回主世界
    /// （承诺见 <see cref="Shenyo.ShenyoDerivedUmbrella"/>）。不掉落、不留墓碑。<br/>
    /// 链路：<see cref="KiamePlayer"/>.PreKill 拦截 → 各端锁血 1 + 无敌 →
    /// 被害端本机重启演出：快门定格(24t，拷屏照片+白闪+推镜) → 雨痕冲刷(76t，照片被刷掉+沙漏成形) →
    /// 时间倒带(110t，冷调+雨滴倒飞+沙漏升沙) → 结算拍(清减益+回满血蓝) →
    /// 白闪峰值调 <see cref="KiameWorld.ExitWorld"/> → 主世界余辉睁眼。
    /// 全屏合成与沙漏复用 KikasaResets 的着色器（见 <see cref="KiameWakeResetRender"/>）。<br/>
    /// 联机：PreKill 是 per-player hook，在每个跑到 KillMe 的端各自取消（原版死亡包根本不发）；
    /// 演出、结算治疗与 SubworldSystem.Exit 全在被害端本机，全程零新包
    /// </summary>
    internal class KiameWake : ModSystem, ILocalizedModType
    {
        public string LocalizationCategory => "UI";

        internal static LocalizedText WakeOni { get; private set; }
        internal static LocalizedText WakeDrowned { get; private set; }
        internal static LocalizedText WakeGeneric { get; private set; }

        //══════ 时间轴（帧，与 KikasaReset 同构但整体收短：这是死亡拍点，不是十秒回放） ══════
        /// <summary>快门段末：拷屏定格、白闪速退、推镜一格</summary>
        internal const int ShutterEnd = 24;
        /// <summary>冲刷段末：雨痕自上而下刷掉照片，沙漏成形</summary>
        internal const int WashEnd = 100;
        /// <summary>倒带段末 = 结算帧：清减益、落定重震；回满晚两帧（摆脱削上限效果）</summary>
        internal const int RewindEnd = 210;
        /// <summary>出雨帧：落定白闪的峰值处切世界，加载屏被白光盖住</summary>
        internal const int ExitFrame = RewindEnd + 3;
        /// <summary>主世界余辉：白光让开、湿青一闪即隐</summary>
        private const int AfterglowTicks = 42;
        /// <summary>倒带脉冲拍数（纯表现，无实体回放）</summary>
        private const int RewindPulses = 3;
        /// <summary>拦截后基础无敌（tick）；演出期间另有逐帧续帧</summary>
        private const int WakeImmuneTicks = 180;

        private static readonly Rectangle PixelSrc = new(0, 0, 1, 1);
        //湿骨灰小字，与加载屏正文同族
        private static readonly Color AshText = new(150, 166, 168);
        //落定白闪的冷白，随鬼雨色温
        private static readonly Color FlashWhite = new(226, 236, 238);

        //本地演出进度，非 per-player 游戏状态，static 合法；
        //刻意不在 OnWorldUnload 兜零——收尾段要跨过世界切换在主世界睁眼
        private static int phase;
        private static int timer;
        private static LocalizedText wakeLine;
        //死亡锚点：演出期间把人钉在这，倒带只是演出、人不真回放
        private static Vector2 deathAnchor;

        /// <summary>子世界内演出进行中（快门帧→出雨帧，被害端本机为真）</summary>
        internal static bool ShowActive => phase == 1;
        /// <summary>演出计时，<see cref="ShowActive"/> 时有效</summary>
        internal static int ShowTimer => timer;
        /// <summary>本场种子，喂给照片/沙漏着色器</summary>
        internal static float ShowSeed { get; private set; }
        /// <summary>沙漏视差锚点（世界坐标，死亡点）</summary>
        internal static Vector2 ShowAnchor => deathAnchor;
        /// <summary>倒带段进行中，雨滴据此倒飞（<see cref="Content.PRTTypes.PRT_GhostRainDrop"/>）</summary>
        internal static bool RainRewindActive => phase == 1 && timer > WashEnd && timer <= RewindEnd;
        /// <summary>当帧回卷速率 0~1，沙漏与雨滴共用的脉冲节拍</summary>
        internal static float RewindPulseRate { get; private set; }
        /// <summary>倒带进度 0~1（脉冲曲线），沙漏升沙比例</summary>
        internal static float RewindProgress01 { get; private set; }

        public override void SetStaticDefaults() {
            WakeOni = this.GetLocalization(nameof(WakeOni), () => "伞下的东西松了手，你醒了。");
            WakeDrowned = this.GetLocalization(nameof(WakeDrowned), () => "黑水漫过头顶，你醒了。");
            WakeGeneric = this.GetLocalization(nameof(WakeGeneric), () => "你从雨里醒来。");
        }

        /// <summary>
        /// 死亡拦截（KiamePlayer.PreKill 转发，调用方已判 KiameWorld.Active）。
        /// 恒返回 true=拦下这次死亡；锁血在每个跑到 KillMe 的端各自执行，
        /// 重启演出只在被害端本机启动
        /// </summary>
        internal static bool InterceptDeath(Player player, PlayerDeathReason source) {
            if (player.statLife < 1) {
                player.statLife = 1;
            }
            player.GivePlayerImmuneState(WakeImmuneTicks);
            if (player.whoAmI == Main.myPlayer && !Main.dedServ && phase == 0) {
                wakeLine = ResolveWakeLine(player, source);
                phase = 1;
                timer = 0;
                ShowSeed = Main.rand.NextFloat(1000f);
                deathAnchor = player.Center;
                RewindPulseRate = 0f;
                RewindProgress01 = 0f;
                KiameWakeResetRender.RequestSnapshot();
                //快门一声；运镜失败不致命，演出照走
                SoundEngine.PlaySound(SoundID.Camera with { Volume = 0.7f, Pitch = -0.2f });
                CutsceneDirector.Play<KiameWakeCutscene>(player);
            }
            return true;
        }

        //死因选文案：近期被伞鬼打中 / 溺水（原版 ByOther(1)）/ 其余一律「你从雨里醒来。」
        private static LocalizedText ResolveWakeLine(Player player, PlayerDeathReason source) {
            if (player.TryGetModPlayer(out Overlay.OniRainWorldPlayer orp)
                && orp.OniHitFrames > 0) {
                return WakeOni;
            }
            if (source != null && source.SourceOtherIndex == 1) {
                return WakeDrowned;
            }
            return WakeGeneric;
        }

        public override void OnWorldUnload() {
            //演出段被外力送出雨（SubLib Return 键等）：收自己的片子、跳到余辉，
            //别把 Exit 再补调一遍；正常路径此时 phase 已是 2，不进这支
            if (phase == 1) {
                if (CutsceneDirector.CurrentClip is KiameWakeCutscene) {
                    CutsceneDirector.Stop();
                }
                phase = 2;
                timer = 0;
                RewindPulseRate = 0f;
            }
        }

        public override void PostUpdateEverything() {
            if (phase == 0) {
                return;
            }
            timer++;
            if (phase == 1) {
                UpdateShow();
            }
            else if (timer >= AfterglowTicks) {
                phase = 0;
                wakeLine = null;
            }
        }

        private static void UpdateShow() {
            UpdateRewindCurve();
            PinLocalPlayer();

            if (timer == RewindEnd) {
                //结算第一拍：清全部减益 + 落定无敌缓冲（复用 KikasaReset 的本机结算口径）
                Player player = Main.LocalPlayer;
                KikasaReset.ApplyLocalCleanse(player);
                SoundEngine.PlaySound(SoundID.Splash with { Volume = 0.8f, Pitch = -0.35f },
                    player?.Center);
                //文案进聊天栏（跨世界留档可回看）
                if (wakeLine != null) {
                    Main.NewText(wakeLine.Value, AshText.R, AshText.G, AshText.B);
                }
            }
            else if (timer == RewindEnd + 2) {
                //结算第二拍：回满生命法力（晚两帧，statLifeMax2 已摆脱削上限效果；联机自行上报）
                KikasaReset.ApplyLocalHeal(Main.LocalPlayer);
            }

            if (timer >= ExitFrame) {
                //先显式收自己的片子（时长恰到此帧，但别赌世界切换与收尾的先后），再出雨
                if (CutsceneDirector.CurrentClip is KiameWakeCutscene) {
                    CutsceneDirector.Stop();
                }
                if (KiameWorld.Active) {
                    KiameWorld.ExitWorld();
                }
                phase = 2;
                timer = 0;
                RewindPulseRate = 0f;
            }
        }

        /// <summary>
        /// 倒带节拍：smoothstep 主干混三重脉冲波（与 KikasaReset.RewindEase 同式），
        /// 进度喂沙漏升沙，帧间差归一成脉冲率喂雨滴倒飞
        /// </summary>
        private static void UpdateRewindCurve() {
            float age = RewindAgeAt(timer);
            float prev = RewindAgeAt(timer - 1);
            RewindProgress01 = age;
            //smoothstep 导数峰 1.5，除段长即单帧最大步进
            RewindPulseRate = MathHelper.Clamp(
                (age - prev) * (RewindEnd - WashEnd) / 1.5f, 0f, 1f);
        }

        private static float RewindAgeAt(int t) {
            if (t <= WashEnd) {
                return 0f;
            }
            float x = MathHelper.Clamp((t - WashEnd) / (float)(RewindEnd - WashEnd), 0f, 1f);
            float spine = x * x * (3f - 2f * x);
            float seg = x * RewindPulses;
            int index = Math.Min((int)seg, RewindPulses - 1);
            float f = seg - index;
            float pulses = (index + f * f * (3f - 2f * f)) / RewindPulses;
            return 0.35f * spine + 0.65f * pulses;
        }

        /// <summary>演出期间钉住被害玩家：逐帧续无敌、锚点定身、竖向大位移不算坠落</summary>
        private static void PinLocalPlayer() {
            Player player = Main.LocalPlayer;
            if (player?.active != true || player.dead) {
                return;
            }
            player.immune = true;
            player.immuneTime = Math.Max(player.immuneTime, 2);
            player.Center = deathAnchor;
            player.velocity = Vector2.Zero;
            player.fallStart = (int)(player.position.Y / 16f);
        }

        //余辉（主世界睁眼）：白光快速让开 + 湿青一闪即隐 + 惊醒文案。
        //子世界内的照片/冲刷/倒带/白闪由 KiameWakeResetRender 在世界合成层画；
        //过场期间 gameMenu=true 不画也不走帧，由加载屏接管，落地后余下的收尾继续
        public override void PostDrawInterface(SpriteBatch spriteBatch) {
            if (phase != 2 || Main.dedServ || Main.gameMenu) {
                return;
            }
            Texture2D px = VaultAsset.placeholder2?.Value;
            if (px == null || px.IsDisposed) {
                return;
            }
            var full = new Rectangle(0, 0, Main.screenWidth, Main.screenHeight);
            float t = MathHelper.Clamp(timer / (float)AfterglowTicks, 0f, 1f);
            float white = (1f - t) * (1f - t);
            float damp = MathF.Sin(MathHelper.Pi * t) * 0.30f;
            //文案等白光退过半再浮现，别灰字压白底
            float lineAlpha = MathHelper.Clamp((timer - AfterglowTicks * 0.35f) / 8f, 0f, 1f)
                * (1f - t * t);
            spriteBatch.Draw(px, full, PixelSrc, FlashWhite * white);
            spriteBatch.Draw(px, full, PixelSrc, new Color(38, 52, 56) * damp);
            if (lineAlpha > 0f && wakeLine != null) {
                Utils.DrawBorderString(spriteBatch, wakeLine.Value,
                    new Vector2(Main.screenWidth * 0.5f, Main.screenHeight * 0.44f),
                    AshText * lineAlpha, 0.95f, 0.5f, 0.5f);
            }
        }
    }

    /// <summary>
    /// 死亡重启的运镜：全程锁输入、快门推近一格盯住定格、倒带末段回拉，
    /// 快门与结算各一记震屏。仅被害端本机播放；时长恰到出雨帧，世界切换时片子已收
    /// </summary>
    internal sealed class KiameWakeCutscene : CutsceneClip
    {
        public override int Priority => 46;

        public override bool CanPlay(Player player)
            => base.CanPlay(player) && KiameWake.ShowActive
                && player.whoAmI == Main.myPlayer;

        protected override void BuildTimeline(CutsceneTimeline timeline) {
            int total = KiameWake.ExitFrame;
            timeline.Duration = total;

            timeline
                //全程锁输入：被拉回的人插不上手
                .Add(new InputLockTrack(0, total, CutsceneInputLockFlags.All))
                //快门推近一格盯住定格；倒带收束前回拉，末拍交给白闪
                .Add(new CameraZoomTrack(0, KiameWake.ShutterEnd,
                    1f, 1.06f, 0.05f, CutsceneEase.CubicOut))
                .Add(new CameraZoomTrack(KiameWake.RewindEnd - 24, 24,
                    1.06f, 1f, 0.05f, CutsceneEase.CubicOut))
                //快门一记轻震，结算落定一记重些
                .Add(new CameraShakeTrack(0, Vector2.Zero, 4f, 0.85f, 12))
                .Add(new CameraShakeTrack(KiameWake.RewindEnd, Vector2.Zero, 6f, 0.9f, 24));
        }
    }
}
