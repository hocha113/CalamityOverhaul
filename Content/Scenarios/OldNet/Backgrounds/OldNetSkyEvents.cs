using CalamityOverhaul.Common;
using CalamityOverhaul.Content.RAMSystems;
using CalamityOverhaul.Content.Scenarios.OldNet.Gen;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.OldNet.Backgrounds
{
    /// <summary>
    /// 旧网天幕事件状态板与导演（纯视觉，client-only）：
    /// <see cref="Surge"/> 黑墙涌动脉冲（OldNetSky/Blackwall 消费），
    /// <see cref="GiantPos"/>/<see cref="GiantScale"/>/<see cref="GiantMix"/>
    /// 巨物剪影（OldNetSky uGiant 消费）。
    /// 全部状态本机确定性推进，不落存档不发包，事件是氛围不是玩法
    /// </summary>
    internal class OldNetSkyEvents : ModSystem
    {
        /// <summary>黑墙涌动 0~1</summary>
        internal static float Surge { get; private set; }
        /// <summary>巨物剪影中心（屏幕uv）</summary>
        internal static Vector2 GiantPos { get; private set; }
        /// <summary>巨物剪影尺度（uv 半径量级）</summary>
        internal static float GiantScale { get; private set; } = 0.2f;
        /// <summary>巨物在场强度 0~1</summary>
        internal static float GiantMix { get; private set; }
        /// <summary>疯域故障脉冲 0~1（OldNetGrade 消费；衰减区带门控）</summary>
        internal static float Glitch { get; private set; }

        //───── ⑥ 黑墙大潮状态（一潜至多一次的敬畏事件，25s 三幕：吸气/涨潮/退潮）─────

        /// <summary>潮汐总包络 0~1（幕一升满、幕三落回；锋面强度/墙鸣加成共用）</summary>
        internal static float TidePhase { get; private set; }
        /// <summary>潮锋世界 X（px）；无潮时为大负值，消费端 step 比较自然归零</summary>
        internal static float TideFrontWorldX { get; private set; } = -1e7f;
        /// <summary>吸气强度 0~1（幕一/幕二）：竖尘获得西向加速度，被吸向墙</summary>
        internal static float TideSuck { get; private set; }
        /// <summary>退潮呼出窗口 0~1（幕三头 3s）：尘以涌动横波姿态自西向东呼出一轮</summary>
        internal static float TideExhale { get; private set; }
        /// <summary>锋面过境玩家列的冲击拍 0~1（BlackwallRender 画贴地椭圆环）</summary>
        internal static float TideCrossFlash { get; private set; }
        /// <summary>过境拍环心（过境帧的玩家脚底世界坐标）</summary>
        internal static Vector2 TideCrossPos { get; private set; }
        /// <summary>天幕/黑墙的涌动合成值：常规涌动与大潮前奏取 max（uSurge 消费端换用此值）</summary>
        internal static float SurgeComposed => MathF.Max(Surge, tideSurgeBoost);

        //───── 涌动：低频随机起搏，一次 8~14s 包络 ─────
        private static int surgeTimer;
        private static int surgeDuration;
        private static int surgeCooldown;

        //───── 巨物：极低频（每次深潜 0~2 次量级），横穿远幕 ─────
        private static int giantTimer;
        private static int giantDuration;
        private static int giantCooldown;
        private static float giantStartX;
        private static float giantEndX;
        private static float giantY;

        //───── 疯域故障：衰减区限定，≥60s 随机节律的短促尖峰 ─────
        private static int glitchTimer;
        private static int glitchDuration;
        private static int glitchCooldown;
        private static float glitchAmp;

        //───── ⑥ 黑墙大潮：三幕计时与旗标 ─────
        private static int tideTimer;
        private static int tideCooldown;
        private static bool tideDoneThisDive;
        private static bool tideCrossFired;
        private static int tideCrossTimer;
        private static float tideSurgeBoost;
        private const int TideTotalFrames = 25 * 60;
        private const int TideCrossFrames = 36;

        public override void ClearWorld() => ResetAll();

        /// <summary>验收辅助：立刻起一段涌动（TestItem 触发用）</summary>
        internal static void DebugTriggerSurge() {
            surgeDuration = 60 * 10;
            surgeTimer = surgeDuration;
            surgeCooldown = 60 * 90;
        }

        /// <summary>验收辅助：立刻起一次疯域故障尖峰（TestItem 触发用，无视带门控）</summary>
        internal static void DebugTriggerGlitch() {
            glitchDuration = 16;
            glitchTimer = glitchDuration;
            glitchAmp = 0.9f;
            glitchCooldown = 60 * 60;
        }

        /// <summary>验收辅助：立刻起一次黑墙大潮（TestItem 触发用，无视一潜一次旗标与起潮条件）</summary>
        internal static void DebugTriggerTide() {
            tideTimer = TideTotalFrames;
            tideDoneThisDive = true;
            tideCrossFired = false;
            tideCooldown = 60 * 300;
            SoundEngine.PlaySound(SoundID.DD2_EtherianPortalOpen
                with { Volume = 0.5f, Pitch = -0.7f });
        }

        /// <summary>验收辅助：立刻放一次巨物横穿（TestItem 触发用）</summary>
        internal static void DebugTriggerGiant() {
            giantDuration = 60 * 45;
            giantTimer = giantDuration;
            giantCooldown = 60 * 150;
            giantStartX = -0.35f;
            giantEndX = 1.35f;
            giantY = 0.24f;
            GiantScale = 0.26f;
        }

        internal static void ResetAll() {
            Surge = 0f;
            GiantMix = 0f;
            Glitch = 0f;
            surgeTimer = surgeDuration = 0;
            giantTimer = giantDuration = 0;
            glitchTimer = glitchDuration = 0;
            //⑥ 大潮：每潜旗标复位，重新可遇
            TidePhase = 0f;
            TideFrontWorldX = -1e7f;
            TideSuck = 0f;
            TideExhale = 0f;
            TideCrossFlash = 0f;
            tideTimer = 0;
            tideDoneThisDive = false;
            tideCrossFired = false;
            tideCrossTimer = 0;
            tideSurgeBoost = 0f;
            //进世界后先各给一段静默期：氛围事件不抢开场
            surgeCooldown = 60 * 90;
            giantCooldown = 60 * 150;
            glitchCooldown = 60 * 60;
            tideCooldown = 60 * 240;
        }

        public override void PostUpdateEverything() {
            if (Main.dedServ || Main.gameMenu) {
                return;
            }
            if (!OldNetWorld.Active) {
                if (Surge > 0f || GiantMix > 0f || TidePhase > 0f) {
                    ResetAll();
                }
                return;
            }
            UpdateSurge();
            UpdateGiant();
            UpdateGlitch();
            UpdateTide();
        }

        //──── ⑥ 黑墙大潮：三幕导演（吸气 0~6s / 涨潮 6~16s / 退潮 16~25s）────
        private static void UpdateTide() {
            //过境拍包络独立走完（潮体结束不掐断余波）
            if (tideCrossTimer > 0) {
                tideCrossTimer--;
                TideCrossFlash = tideCrossTimer / (float)TideCrossFrames;
            }
            else {
                TideCrossFlash = 0f;
            }

            if (tideTimer > 0) {
                tideTimer--;
                float t = (TideTotalFrames - tideTimer) / 60f;
                float wallX = OldNetMetrics.WallCols * 16f;
                const float pushPx = 300f * 16f;

                if (t < 6f) {
                    //幕一·吸气：墙鸣涨满、竖尘倒卷向墙、天幕以既有涌动语汇当前奏（缓升至 0.5）
                    float k = t / 6f;
                    TidePhase = k;
                    TideSuck = k;
                    TideExhale = 0f;
                    tideSurgeBoost = k * 0.5f;
                    TideFrontWorldX = wallX;
                }
                else if (t < 16f) {
                    //幕二·涨潮：锋面 EaseInOut 自西缘推进 300 列
                    float k = (t - 6f) / 10f;
                    float ease = k * k * (3f - 2f * k);
                    TidePhase = 1f;
                    TideSuck = 1f - k * 0.4f;
                    TideExhale = 0f;
                    tideSurgeBoost = 0.5f;
                    TideFrontWorldX = wallX + pushPx * ease;

                    //过境拍：锋面首次扫过玩家列的那一帧（贴地环+轻屏震+一声故障过渡）
                    Player lp = Main.LocalPlayer;
                    if (!tideCrossFired && TideFrontWorldX >= lp.Center.X) {
                        tideCrossFired = true;
                        tideCrossTimer = TideCrossFrames;
                        TideCrossPos = new Vector2(lp.Center.X, lp.Bottom.Y);
                        SoundEngine.PlaySound(CWRSound.FaultTransition
                            with { Volume = 0.4f, Pitch = -0.3f });
                        //克制屏震（低调风格约定，幅度 ≤3）
                        lp.CWR().GetScreenShake(3f);
                    }
                }
                else {
                    //幕三·退潮：锋面 EaseIn 缓缓退回墙内；头 3s 尘自西向东呼出一轮
                    float k = MathHelper.Clamp((t - 16f) / 9f, 0f, 1f);
                    TidePhase = 1f - k;
                    TideSuck = 0f;
                    TideExhale = MathHelper.Clamp(1f - (t - 16f) / 3f, 0f, 1f);
                    tideSurgeBoost = 0.5f * (1f - k);
                    TideFrontWorldX = wallX + pushPx * (1f - k * k);
                }

                if (tideTimer == 0) {
                    //收潮：状态归位 + 低调收尾声（音量组合未确认-待实机试听）
                    TidePhase = 0f;
                    TideSuck = 0f;
                    TideExhale = 0f;
                    tideSurgeBoost = 0f;
                    TideFrontWorldX = -1e7f;
                    SoundEngine.PlaySound(SoundID.WormDigQuiet
                        with { Volume = 0.4f, Pitch = -0.6f });
                }
                return;
            }

            //每潜至多一次：错过条件窗口就这一潜无潮（稀有度=敬畏）
            if (tideDoneThisDive) {
                return;
            }
            if (--tideCooldown > 0) {
                return;
            }
            tideCooldown = 60 * Main.rand.Next(240, 421);

            //起潮条件：在场满 + 非清剿波（不抢 T4 可读性）+ 非烧断边缘 + 玩家列 <1200
            //（潮是墙侧奇观，衰减区看不见就不放）+ 与涌动/巨物互斥
            if (OldNetAmbience.Presence <= 0.9f) {
                return;
            }
            Player player = Main.LocalPlayer;
            if (OldNetPlayer.Get(player).NoiseTier >= 4) {
                return;
            }
            //"非弹出倒数"的同义近似：RAM 余量 <10% 时不起潮（ejectDelay 是 OldNetPlayer 私有态）
            RAMPlayer ram = player.GetModPlayer<RAMPlayer>();
            if (ram.ProfileInitialized && ram.MaxRam > 0
                && ram.CurrentRam / ram.MaxRam < 0.10f) {
                return;
            }
            if (player.Center.X / 16f >= 1200f) {
                return;
            }
            if (Surge > 0.05f || GiantMix > 0.05f) {
                return;
            }

            tideTimer = TideTotalFrames;
            tideDoneThisDive = true;
            tideCrossFired = false;
            //敬畏事件不撞车：把涌动/巨物的下一次起搏推后
            surgeCooldown = Math.Max(surgeCooldown, 60 * Main.rand.Next(40, 80));
            giantCooldown = Math.Max(giantCooldown, 60 * Main.rand.Next(60, 120));
            SoundEngine.PlaySound(SoundID.DD2_EtherianPortalOpen
                with { Volume = 0.5f, Pitch = -0.7f });
            CWRMod.Instance.Logger.Info("[OldNet] 黑墙大潮事件起潮");
        }

        private static void UpdateGlitch() {
            if (glitchTimer > 0) {
                glitchTimer--;
                //前 30% 快攻至峰值，其后指数式退潮
                float t = 1f - glitchTimer / (float)glitchDuration;
                float env = t < 0.3f ? t / 0.3f : (1f - t) / 0.7f;
                Glitch = env * glitchAmp;
                return;
            }
            Glitch = 0f;
            if (--glitchCooldown > 0) {
                return;
            }
            //疯域限定：腐化 <0.5（衰减区之外）只重掷冷却不发作
            float corrupt = OldNetMetrics.CorruptionAt((int)(Main.LocalPlayer.Center.X / 16f));
            glitchCooldown = 60 * Main.rand.Next(60, 150);
            if (corrupt < 0.5f) {
                return;
            }
            //越深尖峰越烈：8~18 帧短促脉冲
            glitchDuration = Main.rand.Next(8, 19);
            glitchTimer = glitchDuration;
            glitchAmp = 0.35f + (corrupt - 0.5f) * 1.1f + Main.rand.NextFloat(0.15f);
        }

        private static void UpdateSurge() {
            if (surgeTimer > 0) {
                surgeTimer--;
                //起 20% 快攻，中段驻留，末 40% 缓退
                float t = 1f - surgeTimer / (float)surgeDuration;
                float env = MathHelper.Clamp(t / 0.2f, 0f, 1f)
                    * MathHelper.Clamp((1f - t) / 0.4f, 0f, 1f);
                Surge = MathHelper.Lerp(Surge, env, 0.2f);
                return;
            }
            Surge = MathHelper.Lerp(Surge, 0f, 0.05f);
            if (--surgeCooldown > 0) {
                return;
            }
            //越靠近墙越常涌：离墙远时冷却重掷更长
            surgeDuration = 60 * Main.rand.Next(8, 15);
            surgeTimer = surgeDuration;
            surgeCooldown = 60 * Main.rand.Next(70, 160);
            //起涌远响：低哑的门户开启声当作墙体呼气
            Terraria.Audio.SoundEngine.PlaySound(Terraria.ID.SoundID.DD2_EtherianPortalOpen
                with { Volume = 0.35f, Pitch = -0.55f });
        }

        private static void UpdateGiant() {
            if (giantTimer > 0) {
                giantTimer--;
                float t = 1f - giantTimer / (float)giantDuration;
                //在场包络：两端各 18% 淡入淡出
                float env = MathHelper.Clamp(t / 0.18f, 0f, 1f)
                    * MathHelper.Clamp((1f - t) / 0.18f, 0f, 1f);
                GiantMix = env;
                GiantPos = new Vector2(MathHelper.Lerp(giantStartX, giantEndX, t), giantY);
                return;
            }
            GiantMix = MathHelper.Lerp(GiantMix, 0f, 0.1f);
            if (--giantCooldown > 0) {
                return;
            }
            //腐化越深巨物越可能现身；墙脚带不出（敬畏属于深处）
            float corrupt = OldNetMetrics.CorruptionAt(Main.LocalPlayer.Center.ToTileCoordinates().X);
            giantCooldown = 60 * Main.rand.Next(120, 260);
            if (corrupt < 0.1f || !Main.rand.NextBool(2)) {
                return;
            }
            //一次横穿：60~100s 极慢掠过远幕，天越腐越大
            giantDuration = 60 * Main.rand.Next(60, 100);
            giantTimer = giantDuration;
            //入场低鸣：蠕虫掘进声压低当作远处的位移轰鸣
            Terraria.Audio.SoundEngine.PlaySound(Terraria.ID.SoundID.WormDigQuiet
                with { Volume = 0.55f, Pitch = -0.85f });
            bool leftToRight = Main.rand.NextBool();
            giantStartX = leftToRight ? -0.35f : 1.35f;
            giantEndX = leftToRight ? 1.35f : -0.35f;
            giantY = Main.rand.NextFloat(0.16f, 0.34f);
            GiantScale = 0.16f + corrupt * 0.14f + Main.rand.NextFloat(0.05f);
            CWRMod.Instance.Logger.Info($"[OldNet] 巨物远景事件 dur={giantDuration / 60}s scale={GiantScale:0.00}");
        }
    }
}
