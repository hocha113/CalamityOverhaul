using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDreams;
using CalamityOverhaul.Content.Scenarios.Kiyume.Fog;
using CalamityOverhaul.Content.Scenarios.Kiyume.Gen;
using ReLogic.Utilities;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Kiyume.Ambience
{
    /// <summary>
    /// 鬼梦声景底床（KIY-P5-A）：风床/湖床双循环（补挂制 + 回调逐帧调参 + gameMenu 杀链）、
    /// 带归属点缀调度、犬吠方位提示（裁决 21）、duck 包络与戏剧风静态口。
    /// 架构逐行镜像 DungeonworldAmbience；presence 复用 <see cref="KiyumeAmbienceSystem.Presence"/>；
    /// 调音全住 <see cref="KiyumeScore"/>。<br/>
    /// 权威端+同步字段：无。LocalPlayer 本地采样零网络包；真吠读的是已同步到本端的
    /// NPC 位置，各端听见的方位天然一致。本类 static 状态只是本地演出进度，
    /// 非 per-player 游戏状态，netcode 静态禁令不适用（DungeonworldSnuff 同款口径）。
    /// </summary>
    internal class KiyumeSoundscape : ModSystem
    {
        //==== Debug 静态口 ====
        /// <summary>只留点缀、静掉声床（循环声事故降级口）</summary>
        internal static bool MuteBeds;
        /// <summary>伪带序（0..4=深湖..远山）：≥0 时替代玩家真实带位，轮带验收用；-1=关闭</summary>
        internal static int FakeBand = -1;

        /// <summary>戏剧风 0..1：风床音量、檐铃风门、未来灯火/晾衣演出共用此口（声画自洽）</summary>
        internal static float DramaticWind => dramaticWind;

        //犬位扫描缓存：本层唯一实扫点（每 4f 一次），导演 HoundNearby 从这里转读。
        //类型名单 P2-C 落地前恒空 → 恒无犬，犬吠走保底假吠
        internal static bool HoundFound => houndFound;
        internal static Vector2 HoundPos => houndPos;
        internal static float HoundDist => houndDist;

        //声床循环槽（SlotId + TryGetActiveSound 丢失补挂 + 回调逐帧调参）
        private static SlotId windSlot;
        private static SlotId lakeSlot;
        private static readonly SoundStyle WindStyle =
            SoundID.BlizzardInsideBuildingLoop with { IsLooped = true, MaxInstances = 1 };
        private static readonly SoundStyle LakeStyle =
            SoundID.Waterfall with { IsLooped = true, MaxInstances = 1 };

        //duck 包络（床层专属；导演与大事件经 PushDuck 调用）
        private static float duckFactor = 1f;
        private static float duckAmount = 1f;
        private static int duckTimer;

        //戏剧风时钟与雾下没入包络
        private static long windClock;
        private static float dramaticWind = 0.5f;
        private static float submergeK;

        //点缀调度
        private static int[] accentTimers;
        private static int accentGlobalCd;

        //犬吠
        private static int barkTimer;

        //犬位扫描
        private static int[] houndTypes;
        private static int houndScanTimer;
        private static bool houndFound;
        private static Vector2 houndPos;
        private static float houndDist = float.MaxValue;

        //连响延迟队列（滴水/枯枝/梆子双连）
        private struct PendingHit
        {
            internal bool Active;
            internal SoundStyle Style;
            internal Vector2 Pos;
            internal int Delay;
        }

        private static readonly PendingHit[] pendingHits = new PendingHit[4];

        //==================== 对外接口 ====================

        /// <summary>
        /// 把床层音量压到 amount（0~1）约 frames 帧：快压慢放（月轮"世界屏息"等大事件用）。
        /// 多来源语义=后写覆盖；现唯一调用方是月轮，且 A/S 槽独占天然防同期叠加，
        /// 若日后加第二个常态调用方应改取 min 合并（W4 二审留档）
        /// </summary>
        internal static void PushDuck(float amount, int frames) {
            if (Main.dedServ) {
                return;
            }
            duckAmount = MathHelper.Clamp(amount, 0f, 1f);
            duckTimer = Math.Max(frames, 1);
        }

        //──犬类型名单换源点（裁决 21）──
        //P2-C（W2）在 KiyumeHound 侧暴露 KiyumeHoundTypes 注册表后，本方法改为返回该注册表（一行事）。
        //在此之前恒空名单：真吠不播、导演 houndFactor 恒 0、让位不触发，犬吠走保底假吠，其余功能不受阻。
        private static int[] ResolveHoundTypes() => NPCs.KiyumeHound.KiyumeHoundTypes;

        //==================== 生命周期 ====================

        public override void OnWorldLoad() => HardReset();
        public override void ClearWorld() => HardReset();

        public override void Unload() {
            HardReset();
            MuteBeds = false;
            FakeBand = -1;
        }

        private static void HardReset() {
            duckFactor = 1f;
            duckAmount = 1f;
            duckTimer = 0;
            windClock = 0;
            dramaticWind = 0.5f;
            submergeK = 0f;
            accentTimers = null;
            accentGlobalCd = 0;
            barkTimer = KiyumeScore.FakeBarkPeriodMin;
            houndTypes = null;
            houndScanTimer = 0;
            houndFound = false;
            houndPos = Vector2.Zero;
            houndDist = float.MaxValue;
            for (int i = 0; i < pendingHits.Length; i++) {
                pendingHits[i].Active = false;
            }
        }

        //==================== 驱动 ====================

        public override void PostUpdateEverything() {
            if (Main.dedServ) {
                return;
            }
            //presence 复用场景共享条；离场/切菜单后循环残链由回调的 gameMenu/Active 检查自杀
            if (!KiyumeWorld.Active || Main.gameMenu || KiyumeAmbienceSystem.Presence < 0.01f) {
                return;
            }
            Player player = Main.LocalPlayer;
            if (player == null || !player.active) {
                return;
            }

            windClock++;
            dramaticWind = KiyumeScore.DramaticWindAt(windClock);
            //雾下没入度：雾线下越深风声越闷（空间平滑，出入雾面无爆点）
            float depth = player.Center.Y - KiyumeFogTide.SurfaceAt(player.Center.X);
            submergeK = Smooth01(depth / KiyumeScore.SubmergeSmoothPx);

            ScanHounds(player);
            UpdateDuck();
            UpdateBeds();
            UpdateAccents(player);
            UpdateBark(player);
            FlushHits();
        }

        private static void UpdateDuck() {
            //快压慢放（镜像深牢重音闪避包络）
            if (duckTimer > 0) {
                duckTimer--;
                duckFactor = MathHelper.Lerp(duckFactor, duckAmount, 0.25f);
            }
            else {
                duckFactor = MathHelper.Lerp(duckFactor, 1f, 0.03f);
            }
        }

        private static float Smooth01(float x) {
            x = MathHelper.Clamp(x, 0f, 1f);
            return x * x * (3f - 2f * x);
        }

        /// <summary>当前采样列（tile）：FakeBand 时取该带带心</summary>
        private static float CurrentColumn() {
            if (FakeBand >= 0 && FakeBand < KiyumeMetrics.Bands.Length) {
                var band = KiyumeMetrics.Bands[FakeBand];
                return (band.Left + band.Right) * 0.5f;
            }
            Player player = Main.LocalPlayer;
            return player != null && player.active ? player.Center.X / 16f : 0f;
        }

        private static int CurrentBand() {
            if (FakeBand >= 0 && FakeBand < KiyumeMetrics.Bands.Length) {
                return FakeBand;
            }
            return KiyumeMetrics.BandIndexForColumn((int)CurrentColumn());
        }

        //==================== 声床（≤2 条循环同时）====================

        private static void UpdateBeds() {
            if (MuteBeds) {
                return;
            }
            //循环丢失（切场景/音量档变化）就补挂；音量在回调里逐帧走
            if (!SoundEngine.TryGetActiveSound(windSlot, out _)) {
                windSlot = SoundEngine.PlaySound(WindStyle, null, UpdateWind);
            }
            if (KiyumeScore.LakeVolume(CurrentColumn() * 16f) > 0.004f
                && !SoundEngine.TryGetActiveSound(lakeSlot, out _)) {
                lakeSlot = SoundEngine.PlaySound(LakeStyle, null, UpdateLake);
            }
        }

        private static bool UpdateWind(ActiveSound sound) {
            if (Main.gameMenu || MuteBeds) {
                return false;
            }
            float presence = KiyumeAmbienceSystem.Presence;
            if (presence < 0.005f && !KiyumeWorld.Active) {
                return false;
            }
            //带心插值 × 戏剧风摆幅 × 雾下闷压 × presence × duck
            float vol = KiyumeScore.WindVolume(CurrentColumn())
                * (1f + (dramaticWind * 2f - 1f) * KiyumeScore.WindDramaticSwing)
                * MathHelper.Lerp(1f, KiyumeScore.WindSubmergedVolMul, submergeK)
                * presence * duckFactor;
            sound.Volume = MathF.Min(vol, KiyumeScore.LoopVolCap);
            sound.Pitch = MathHelper.Lerp(KiyumeScore.WindPitchBase, KiyumeScore.WindPitchSubmerged, submergeK);
            sound.Position = null;
            return true;
        }

        private static bool UpdateLake(ActiveSound sound) {
            if (Main.gameMenu || MuteBeds) {
                return false;
            }
            float presence = KiyumeAmbienceSystem.Presence;
            if (presence < 0.005f && !KiyumeWorld.Active) {
                return false;
            }
            float vol = KiyumeScore.LakeVolume(CurrentColumn() * 16f) * presence * duckFactor;
            //远离湖岸不空转声道：静音即自杀，走近由补挂制拉回（镜像深牢炉鸣）
            if (vol < 0.002f) {
                return false;
            }
            sound.Volume = MathF.Min(vol, KiyumeScore.LoopVolCap);
            sound.Pitch = KiyumeScore.LakePitch;
            sound.Position = null;
            return true;
        }

        //==================== 点缀 ====================

        private static void UpdateAccents(Player player) {
            if (accentGlobalCd > 0) {
                accentGlobalCd--;
            }
            var cues = KiyumeScore.Accents;
            if (accentTimers == null || accentTimers.Length != cues.Length) {
                accentTimers = new int[cues.Length];
                for (int i = 0; i < cues.Length; i++) {
                    //初相错开：进梦后各点缀不同时首响
                    accentTimers[i] = (int)(cues[i].Period * Main.rand.NextFloat(0.3f, 0.8f));
                }
            }

            int band = CurrentBand();
            if (band < 0) {
                return;
            }
            for (int i = 0; i < cues.Length; i++) {
                if (cues[i].Band != band) {
                    continue;
                }
                if (--accentTimers[i] > 0) {
                    continue;
                }
                //风门未开（檐铃）：短重试，风起后尽快应景
                if (cues[i].WindGate > 0f && dramaticWind < cues[i].WindGate) {
                    accentTimers[i] = Main.rand.Next(90, 180);
                    continue;
                }
                accentTimers[i] = NextPeriod(cues[i]);
                if (accentGlobalCd > 0) {
                    continue;    //全局冷却中：本轮让位，周期已重置不积压
                }
                accentGlobalCd = KiyumeScore.AccentGlobalCooldown;
                FireAccent(cues[i], player);
            }
        }

        private static int NextPeriod(in KiyumeAccentCue cue) {
            int period = Math.Max((int)(cue.Period * (1f + Main.rand.NextFloat(-cue.Jitter, cue.Jitter))), 60);
            //犬让位期（<900px）B 级密度减半：真威胁在场，点缀退后（导演门 7 的 B 级条款）
            if (houndFound && houndDist < KiyumeScore.HoundYieldDistPx) {
                period *= 2;
            }
            return period;
        }

        private static void FireAccent(in KiyumeAccentCue cue, Player player) {
            //音源落在玩家 12~30 tile 外的随机方位（左右声道可辨）
            Vector2 dir = Main.rand.NextFloat(MathHelper.TwoPi).ToRotationVector2();
            Vector2 pos = player.Center + dir * Main.rand.NextFloat(12f, 30f) * 16f;
            SoundStyle style = cue.Style with {
                Volume = KiyumeScore.CapAccent(cue.Volume),
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

        //==================== 犬吠方位提示（裁决 21）====================

        private static void UpdateBark(Player player) {
            if (--barkTimer > 0) {
                return;
            }
            if (houndFound) {
                barkTimer = Main.rand.Next(KiyumeScore.BarkPeriodMin, KiyumeScore.BarkPeriodMax + 1);
                //≤600px 静默让位：近距声带（低吼/长嚎/哀鸣）归 P2 犬实体自己
                if (houndDist <= KiyumeScore.BarkYieldDistPx) {
                    return;
                }
                if (accentGlobalCd > 0) {
                    return;
                }
                accentGlobalCd = KiyumeScore.AccentGlobalCooldown;
                //定位播放自带声像：方位真、距离糊，正是"模糊提示"
                float vol = MathHelper.Lerp(KiyumeScore.BarkVolNear, KiyumeScore.BarkVolFar,
                    MathHelper.Clamp((houndDist - KiyumeScore.BarkYieldDistPx) / KiyumeScore.BarkVolFalloffPx, 0f, 1f));
                KikasaHoundVoice.Wuff(houndPos, KiyumeScore.CapAccent(vol), KiyumeScore.BarkPitch);
                return;
            }
            //无犬保底假吠：方位随机、音量低于真吠；偶带撕咬声（雾里有狗在咬别的东西）
            barkTimer = Main.rand.Next(KiyumeScore.FakeBarkPeriodMin, KiyumeScore.FakeBarkPeriodMax + 1);
            if (accentGlobalCd > 0) {
                return;
            }
            accentGlobalCd = KiyumeScore.AccentGlobalCooldown;
            Vector2 pos = player.Center + Main.rand.NextFloat(MathHelper.TwoPi).ToRotationVector2()
                * Main.rand.NextFloat(KiyumeScore.FakeBarkDistMin, KiyumeScore.FakeBarkDistMax);
            if (Main.rand.Next(KiyumeScore.FakeBarkWorryOneIn) == 0) {
                KikasaHoundVoice.Worry(pos, KiyumeScore.CapAccent(KiyumeScore.FakeBarkVol), KiyumeScore.BarkPitch);
            }
            else {
                KikasaHoundVoice.Wuff(pos, KiyumeScore.CapAccent(KiyumeScore.FakeBarkVol), KiyumeScore.BarkPitch);
            }
        }

        //==================== 犬位扫描（每 4f 一次实扫，预算红线）====================

        private static void ScanHounds(Player player) {
            houndTypes ??= ResolveHoundTypes();
            if (houndTypes.Length == 0) {
                houndFound = false;
                return;    //P2-C 未落地：零成本跳过
            }
            if (++houndScanTimer < 4) {
                return;
            }
            houndScanTimer = 0;
            houndFound = false;
            houndDist = float.MaxValue;
            foreach (NPC npc in Main.ActiveNPCs) {
                bool match = false;
                for (int i = 0; i < houndTypes.Length; i++) {
                    if (npc.type == houndTypes[i]) {
                        match = true;
                        break;
                    }
                }
                if (!match) {
                    continue;
                }
                float dist = Vector2.Distance(npc.Center, player.Center);
                if (dist < houndDist) {
                    houndFound = true;
                    houndDist = dist;
                    houndPos = npc.Center;
                }
            }
        }
    }
}
