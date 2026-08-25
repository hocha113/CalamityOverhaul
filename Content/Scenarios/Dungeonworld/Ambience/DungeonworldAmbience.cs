using CalamityOverhaul.Content.Scenarios.Dungeonworld.Fog;
using ReLogic.Utilities;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Dungeonworld.Ambience
{
    /// <summary>
    /// 深度音景与光色分级（WAVE2-ATMOSPHERE E-2）：层带声床（≤2 条循环）+
    /// 一次性点缀调度 + 层界仪式（闷钟/雾呼吸/色温抖动）+ ModifySunLightColor 光色分级
    /// （含 L6 末 200 行的色温坠落）。LocalPlayer 本地采样，零网络包，服务端早退。
    /// presence 包络与循环补挂制逐行镜像 OldNetAmbience。<br/>
    /// 对外接口（C 路 Boss 房，Wave-2.5 对齐）：<see cref="PushStinger"/> / <see cref="PushGradePulse"/>
    /// </summary>
    internal class DungeonworldAmbience : ModSystem
    {
        //==== Debug 静态口 ====
        /// <summary>伪深度（行）：≥0 时替代玩家真实行，轮层验收用；-1=关闭</summary>
        internal static float FakeRow = -1f;
        /// <summary>分级力度热调（0=对照关闭）</summary>
        internal static float GradeMul = 1f;
        /// <summary>只留点缀、静掉声床（循环声事故降级口）</summary>
        internal static bool MuteBeds;

        private static float presence;
        internal static float Presence => presence;

        //声床循环槽（SlotId + TryGetActiveSound 丢失补挂 + 回调逐帧调参）
        private static SlotId windSlot;
        private static SlotId furnaceSlot;
        private static readonly SoundStyle WindStyle =
            SoundID.BlizzardInsideBuildingLoop with { IsLooped = true, MaxInstances = 1 };
        private static readonly SoundStyle FurnaceStyle =
            SoundID.DD2_EtherianPortalIdleLoop with { IsLooped = true, MaxInstances = 1 };

        //Boss 重音闪避包络
        private static float duckFactor = 1f;
        private static float duckAmount = 1f;
        private static int duckTimer;

        //全屏色倾脉冲（Boss 阶段切换用）
        private static Color pulseColor;
        private static float pulseStrength;
        private static int pulseFrames;
        private static int pulseLife;

        //层界仪式
        private static readonly bool[] sepArmed = new bool[6];
        private static int ceremonyCooldown;
        private static int boostTimer;
        private static float boostEnv;

        //点缀调度
        private static int[] accentTimers;
        private static int accentGlobalCd;

        //连响延迟队列（骨响/纸声双连）
        private struct PendingHit
        {
            internal bool Active;
            internal SoundStyle Style;
            internal Vector2 Pos;
            internal int Delay;
        }

        private static readonly PendingHit[] pendingHits = new PendingHit[4];

        private static float prevRow;
        private static bool rowInit;

        //本 tick 分级采样缓存（光钩子直接消费，避免每钩重采样）
        private static Color cTileT;
        private static Color cBgT;
        private static float cTileF;
        private static float cBgF;
        private static float cBright = 1f;

        //==================== 对外接口（C 路 Boss 房一行可调）====================

        /// <summary>播一记重音并把声床音量压到 <paramref name="duck"/>（0~1）约 2s，Boss 入场拍用</summary>
        public static void PushStinger(SoundStyle style, float duck) {
            if (Main.dedServ) {
                return;
            }
            SoundEngine.PlaySound(style);
            duckAmount = MathHelper.Clamp(duck, 0f, 1f);
            duckTimer = 120;
        }

        /// <summary>短时全屏色倾（Boss 阶段切换用）：<paramref name="frames"/> 帧内快进慢出</summary>
        public static void PushGradePulse(Color color, float strength, int frames) {
            if (Main.dedServ) {
                return;
            }
            pulseColor = color;
            pulseStrength = MathHelper.Clamp(strength, 0f, 1f);
            pulseFrames = Math.Max(frames, 8);
            pulseLife = pulseFrames;
        }

        //==================== 生命周期 ====================

        public override void OnWorldLoad() => HardReset();
        public override void ClearWorld() => HardReset();

        public override void Unload() {
            HardReset();
            FakeRow = -1f;
            GradeMul = 1f;
            MuteBeds = false;
        }

        private static void HardReset() {
            presence = 0f;
            duckFactor = 1f;
            duckAmount = 1f;
            duckTimer = 0;
            pulseLife = 0;
            pulseFrames = 0;
            ceremonyCooldown = 0;
            boostTimer = 0;
            boostEnv = 0f;
            accentTimers = null;
            accentGlobalCd = 0;
            rowInit = false;
            cBright = 1f;
            cTileF = 0f;
            cBgF = 0f;
            for (int i = 0; i < sepArmed.Length; i++) {
                sepArmed[i] = false;
            }
            for (int i = 0; i < pendingHits.Length; i++) {
                pendingHits[i].Active = false;
            }
        }

        private static float CurrentRow() {
            if (FakeRow >= 0f) {
                return FakeRow;
            }
            Player player = Main.LocalPlayer;
            return player != null && player.active ? player.Center.Y / 16f : 0f;
        }

        //==================== 驱动 ====================

        public override void PostUpdateEverything() {
            if (Main.dedServ) {
                return;
            }

            bool want = Dungeonworld.Active && !Main.gameMenu;
            presence = MathHelper.Lerp(presence, want ? 1f : 0f, 0.04f);
            if (!want && presence < 0.003f) {
                presence = 0f;
                return;
            }

            float row = CurrentRow();
            AmbienceScore.SampleGrade(row, out cTileT, out cTileF, out cBgT, out cBgF, out cBright);
            if (!rowInit) {
                prevRow = row;
                rowInit = true;
            }

            if (want) {
                UpdateCeremony(row);
                UpdateAccents(row);
                UpdateBeds();
            }
            UpdateEnvelopes();
            FlushHits();
            prevRow = row;
        }

        private static void UpdateEnvelopes() {
            //重音闪避：快压慢放
            if (duckTimer > 0) {
                duckTimer--;
                duckFactor = MathHelper.Lerp(duckFactor, duckAmount, 0.25f);
            }
            else {
                duckFactor = MathHelper.Lerp(duckFactor, 1f, 0.03f);
            }
            //仪式色温抖动：30f 上 60f 下
            if (boostTimer > 0) {
                boostTimer--;
                boostEnv = boostTimer > AmbienceScore.CeremonyBoostDown
                    ? 1f - (boostTimer - AmbienceScore.CeremonyBoostDown) / (float)AmbienceScore.CeremonyBoostUp
                    : boostTimer / (float)AmbienceScore.CeremonyBoostDown;
            }
            else {
                boostEnv = 0f;
            }
            if (pulseLife > 0) {
                pulseLife--;
            }
        }

        //==================== 声床 ====================

        private static void UpdateBeds() {
            if (MuteBeds || presence < 0.01f) {
                return;
            }
            //循环丢失（切场景/音量档变化）就补挂；音量在回调里逐帧走
            if (!SoundEngine.TryGetActiveSound(windSlot, out _)) {
                windSlot = SoundEngine.PlaySound(WindStyle, null, UpdateWind);
            }
            if (AmbienceScore.FurnaceVolume(CurrentRow()) > 0.004f
                && !SoundEngine.TryGetActiveSound(furnaceSlot, out _)) {
                furnaceSlot = SoundEngine.PlaySound(FurnaceStyle, null, UpdateFurnace);
            }
        }

        private static bool UpdateWind(ActiveSound sound) {
            if (Main.gameMenu || MuteBeds) {
                return false;
            }
            if (presence < 0.005f && !Dungeonworld.Active) {
                return false;
            }
            float row = CurrentRow();
            sound.Volume = MathF.Min(AmbienceScore.WindVolume(row) * presence * duckFactor,
                AmbienceScore.LoopVolCap);
            sound.Pitch = AmbienceScore.WindPitch(row);
            sound.Position = null;
            return true;
        }

        private static bool UpdateFurnace(ActiveSound sound) {
            if (Main.gameMenu || MuteBeds) {
                return false;
            }
            if (presence < 0.005f && !Dungeonworld.Active) {
                return false;
            }
            float row = CurrentRow();
            float vol = AmbienceScore.FurnaceVolume(row) * presence * duckFactor;
            //升离 L6 后不空转声道：静音即自杀，下潜时补挂制自动拉回
            if (vol < 0.002f) {
                return false;
            }
            sound.Volume = MathF.Min(vol, AmbienceScore.LoopVolCap);
            sound.Pitch = AmbienceScore.FurnacePitch(row);
            sound.Position = null;
            return true;
        }

        //==================== 一次性点缀 ====================

        private static void UpdateAccents(float row) {
            if (accentGlobalCd > 0) {
                accentGlobalCd--;
            }
            var cues = AmbienceScore.Accents;
            if (accentTimers == null || accentTimers.Length != cues.Length) {
                accentTimers = new int[cues.Length];
                for (int i = 0; i < cues.Length; i++) {
                    //初相错开：进塔后各点缀不同时首响
                    accentTimers[i] = (int)(cues[i].Period * Main.rand.NextFloat(0.3f, 0.8f));
                }
            }

            int band = AmbienceScore.BandAt(row);
            for (int i = 0; i < cues.Length; i++) {
                if (cues[i].Band != band) {
                    continue;
                }
                if (--accentTimers[i] > 0) {
                    continue;
                }
                accentTimers[i] = NextPeriod(cues[i]);
                if (accentGlobalCd > 0) {
                    continue;    //全局冷却中：本轮让位，周期已重置不积压
                }
                accentGlobalCd = AmbienceScore.AccentGlobalCooldown;
                FireAccent(cues[i]);
            }
        }

        private static int NextPeriod(in AccentCue cue)
            => Math.Max((int)(cue.Period * (1f + Main.rand.NextFloat(-cue.Jitter, cue.Jitter))), 60);

        private static void FireAccent(in AccentCue cue) {
            Player player = Main.LocalPlayer;
            if (player == null || !player.active) {
                return;
            }
            //音源落在玩家 12~30 tile 外的随机方位（左右声道可辨）
            Vector2 dir = Main.rand.NextFloat(MathHelper.TwoPi).ToRotationVector2();
            Vector2 pos = player.Center + dir * Main.rand.NextFloat(12f, 30f) * 16f;
            SoundStyle style = cue.Style with {
                Volume = MathF.Min(cue.Volume, AmbienceScore.AccentVolCap),
                Pitch = cue.Pitch + Main.rand.NextFloat(-0.04f, 0.04f),
                MaxInstances = 2
            };
            SoundEngine.PlaySound(style, pos);
            for (int h = 1; h < cue.Hits; h++) {
                QueueHit(style, pos + Main.rand.NextVector2Circular(12f, 12f), h * cue.HitGap);
            }
        }

        private static void QueueHit(SoundStyle style, Vector2 pos, int delay) {
            for (int i = 0; i < pendingHits.Length; i++) {
                if (pendingHits[i].Active) {
                    continue;
                }
                pendingHits[i] = new PendingHit { Active = true, Style = style, Pos = pos, Delay = delay };
                return;
            }
        }

        private static void FlushHits() {
            for (int i = 0; i < pendingHits.Length; i++) {
                if (!pendingHits[i].Active) {
                    continue;
                }
                if (--pendingHits[i].Delay > 0) {
                    continue;
                }
                SoundEngine.PlaySound(pendingHits[i].Style, pendingHits[i].Pos);
                pendingHits[i].Active = false;
            }
        }

        //==================== 层界仪式 ====================

        private static void UpdateCeremony(float row) {
            if (ceremonyCooldown > 0) {
                ceremonyCooldown--;
            }
            for (int s = 0; s < sepArmed.Length; s++) {
                float mid = AmbienceScore.SeparatorMid(s);
                if (!sepArmed[s]) {
                    //滞回：离中线足够远才重新武装（楼梯井反复横跳不连刷）
                    if (MathF.Abs(row - mid) > AmbienceScore.CeremonyHysteresis) {
                        sepArmed[s] = true;
                    }
                    continue;
                }
                bool crossed = (prevRow < mid && row >= mid) || (prevRow > mid && row <= mid);
                if (!crossed || ceremonyCooldown > 0) {
                    continue;
                }
                FireCeremony(s);
            }
        }

        private static void FireCeremony(int sep) {
            sepArmed[sep] = false;
            ceremonyCooldown = AmbienceScore.CeremonyCooldown;

            //①闷钟：随深度降调（L5→L6 那记低于 L1→L2）
            float pitch = MathHelper.Clamp(
                AmbienceScore.BellBasePitch - (sep + 1) * AmbienceScore.BellPitchStep, -1f, 1f);
            SoundEngine.PlaySound(SoundID.Item35 with {
                Volume = AmbienceScore.BellVolume, Pitch = pitch, MaxInstances = 2
            });

            //②雾呼吸：推开一圈，按雾系统自己的时间不对称慢慢合拢（L1/L2 无雾自动无感）
            Player player = Main.LocalPlayer;
            if (player != null && player.active) {
                FogSuppression.RequestCircle(player.Center, AmbienceScore.CeremonyFogRadius,
                    AmbienceScore.CeremonyFogTtl, AmbienceScore.CeremonyFogFeather);
            }

            //③色温抖动包络点火
            boostTimer = AmbienceScore.CeremonyBoostUp + AmbienceScore.CeremonyBoostDown;
        }

        //==================== 光色钩子（乘法可组合，与他系统天然共存）====================

        public override void ModifySunLightColor(ref Color tileColor, ref Color backgroundColor) {
            if (Main.dedServ || presence <= 0.001f) {
                return;
            }
            float force = presence * MathHelper.Clamp(GradeMul, 0f, 3f)
                * (1f + AmbienceScore.CeremonyBoost * boostEnv);
            tileColor = Color.Lerp(tileColor, cTileT, MathHelper.Clamp(cTileF * force, 0f, 0.85f));
            backgroundColor = Color.Lerp(backgroundColor, cBgT, MathHelper.Clamp(cBgF * force, 0f, 0.9f));

            //Boss 阶段色倾脉冲：快进慢出
            if (pulseLife > 0 && pulseFrames > 0) {
                float elapsed = pulseFrames - pulseLife;
                float env = elapsed < 6f ? elapsed / 6f : pulseLife / MathF.Max(pulseFrames - 6f, 1f);
                float k = MathHelper.Clamp(pulseStrength * env, 0f, 1f);
                tileColor = Color.Lerp(tileColor, pulseColor, k * 0.5f);
                backgroundColor = Color.Lerp(backgroundColor, pulseColor, k);
            }
        }

        public override void ModifyLightingBrightness(ref float scale) {
            if (Main.dedServ || presence <= 0.001f) {
                return;
            }
            scale *= MathHelper.Lerp(1f, cBright, presence * MathHelper.Clamp(GradeMul, 0f, 1f));
        }

        /// <summary>一行状态摘要（TestItem 验收用）</summary>
        internal static string StatusLine() {
            float row = CurrentRow();
            return $"[音景] presence{presence:F2} 行{row:F0} 带{AmbienceScore.BandAt(row)}"
                + $" duck{duckFactor:F2} boost{boostEnv:F2} 冷却{ceremonyCooldown}"
                + $" 亮度系数{cBright:F2} GradeMul{GradeMul:F2}";
        }
    }
}
