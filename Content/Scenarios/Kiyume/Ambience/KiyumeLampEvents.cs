using CalamityOverhaul.Content.PRTTypes;
using CalamityOverhaul.Content.Scenarios.Kiyume.Gen;
using InnoVault.PRT;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Kiyume.Ambience
{
    /// <summary>
    /// 鬼梦灯火事件组（KIY-P5-C）：三个独立小状态机共用 <see cref="KiyumeLampField"/> 登记表。
    /// E1 窗火蔓延熄灭（S 级）：远端向玩家逐盏推进，留最近两盏，保持后反向无声回亮；
    /// E2 回头灯灭（A 级）：背向筛灯硬切 0，同帧关门声；
    /// E3 无风铃（A 级）：戏剧风静默窗里侧后孤铃一声，近灯应铃微颤。
    /// 全部经 <see cref="KiyumeDirector.TryClaimScare"/> 申请档期，收尾 ReleaseScare；
    /// 槽独占保证三机同刻至多一台在演。守田人静默区由导演门 10 统一拦（裁决 16 W4 收口，
    /// 本文件不再自查）。<br/>
    /// 权威端+同步字段：无。litFactor 是「你看到的光」，声与烟皆本地演出，
    /// 各人被吓的时机不同正是「独自遇鬼」的叙事（DungeonworldSnuff 同款口径）。
    /// </summary>
    internal class KiyumeLampEvents : ModSystem
    {
        //──E1 窗火蔓延熄灭──
        private enum FallPhase { Idle, Snuff, Hold, Relight }

        private static FallPhase fallPhase;
        private static int fallTimer;
        //熄灭顺序（登记表索引，远→近；建于档期过门后）
        private static readonly List<int> fallOrder = new(KiyumeScore.LampScanCap);
        private static int fallSnuffed;
        private static int villageTicks;

        //──E2 回头灯灭（E1 降格单盏版共用本机，cutOwnerId 记录占的是谁的槽）──
        private enum CutPhase { Idle, Cut, Hold, Recover }

        private static CutPhase cutPhase;
        private static int cutTimer;
        private static int cutLampIdx = -1;
        private static KiyumeScareId cutOwnerId = KiyumeScareId.LampBehind;
        private static int behindCooldown;

        //──E3 无风铃──
        private static int stillTicks;
        private static int bellTimer;
        private static int bellLampIdx = -1;

        //远→近排序（静态委托防逐帧闭包分配）
        private static Vector2 sortOrigin;
        private static readonly Comparison<int> FarToNear = (a, b) => {
            var e = KiyumeLampField.Entries;
            return Vector2.DistanceSquared(e[b].WorldCenter, sortOrigin)
                .CompareTo(Vector2.DistanceSquared(e[a].WorldCenter, sortOrigin));
        };

        //==================== 生命周期 ====================

        public override void OnWorldLoad() => HardReset();
        public override void ClearWorld() => HardReset();
        public override void Unload() => HardReset();

        private static void HardReset() {
            fallPhase = FallPhase.Idle;
            fallTimer = 0;
            fallOrder.Clear();
            fallSnuffed = 0;
            villageTicks = 0;
            cutPhase = CutPhase.Idle;
            cutTimer = 0;
            cutLampIdx = -1;
            cutOwnerId = KiyumeScareId.LampBehind;
            behindCooldown = 0;
            stillTicks = 0;
            bellTimer = 0;
            bellLampIdx = -1;
        }

        //==================== 驱动 ====================

        public override void PostUpdateEverything() {
            if (Main.dedServ) {
                return;
            }
            if (!KiyumeWorld.Active || Main.gameMenu || KiyumeAmbienceSystem.Presence < 0.01f) {
                return;
            }
            Player player = Main.LocalPlayer;
            if (player == null || !player.active) {
                return;
            }

            TrackGates(player);
            UpdateFall(player);
            UpdateCut(player);
            UpdateBell(player);
        }

        private static void TrackGates(Player player) {
            //E1 村带驻留：离带清零
            if (KiyumeMetrics.BandIndexForColumn((int)(player.Center.X / 16f)) == 2) {
                if (villageTicks < int.MaxValue / 2) {
                    villageTicks++;
                }
            }
            else {
                villageTicks = 0;
            }
            //E3 无风窗：玩家刚亲耳确认过「没风」（与床层同源的戏剧风曲线）
            if (KiyumeSoundscape.DramaticWind < KiyumeScore.StillBellWindGate) {
                if (stillTicks < int.MaxValue / 2) {
                    stillTicks++;
                }
            }
            else {
                stillTicks = 0;
            }
            if (behindCooldown > 0) {
                behindCooldown--;
            }
        }

        //==================== E1 窗火蔓延熄灭 ====================

        private static void UpdateFall(Player player) {
            var entries = KiyumeLampField.Entries;
            switch (fallPhase) {
                case FallPhase.Idle: {
                    if (!KiyumeLampField.Scanned || cutPhase != CutPhase.Idle) {
                        return;
                    }
                    if (KiyumeMetrics.BandIndexForColumn((int)(player.Center.X / 16f)) != 2) {
                        return;
                    }
                    //驻留是铺垫门（武装跳过），带位与灯量是物理门（武装也拦）
                    if (villageTicks < KiyumeScore.LampFallVillageStay
                        && !KiyumeDirectorDebug.PeekArm(KiyumeScareId.LampFall)) {
                        return;
                    }
                    //登记 <4 盏：降格 E2 单盏版，预算合并进 LampFall
                    if (entries.Count < KiyumeScore.LampFallMinLamps) {
                        TryStartCut(player, KiyumeScareId.LampFall,
                            KiyumeScore.LampFallWindowLo, KiyumeScore.LampFallWindowHi);
                        return;
                    }
                    //预筛：亮灯较多的一侧去掉保留盏后仍有可熄的才申请档期
                    if (!CountFallSides(player, out bool west)) {
                        return;
                    }
                    if (!KiyumeDirector.TryClaimScare(KiyumeScareId.LampFall,
                        KiyumeScore.LampFallWindowLo, KiyumeScore.LampFallWindowHi)) {
                        return;
                    }
                    if (!BuildFallOrder(player, west)) {
                        //同 tick 预筛已过，理论不可达；防御性放槽
                        KiyumeDirector.ReleaseScare(KiyumeScareId.LampFall);
                        return;
                    }
                    fallPhase = FallPhase.Snuff;
                    fallTimer = 0;
                    fallSnuffed = 0;
                    break;
                }
                case FallPhase.Snuff: {
                    //逐盏推进：第 i 盏在 i*Step 帧起 ease FadePerLamp 帧 1→0
                    for (int i = 0; i < fallOrder.Count; i++) {
                        int local = fallTimer - i * KiyumeScore.LampFallStep;
                        if (local < 0) {
                            break;
                        }
                        var entry = entries[fallOrder[i]];
                        if (local == 0) {
                            //熄灭拍：一口轻声 + 两口烟
                            SoundEngine.PlaySound(SoundID.Drip with {
                                Volume = KiyumeScore.CapAccent(KiyumeScore.LampSnuffVol),
                                Pitch = KiyumeScore.LampSnuffPitch,
                                MaxInstances = 3
                            }, entry.WorldCenter);
                            SnuffPuff(entry.WorldCenter, KiyumeScore.LampSnuffSmoke);
                            fallSnuffed = i + 1;
                        }
                        float f = 1f - MathHelper.Clamp(local / (float)KiyumeScore.LampFallFadePerLamp, 0f, 1f);
                        KiyumeLampField.SetFactor(entry.Key, f);
                    }
                    fallTimer++;
                    int total = (fallOrder.Count - 1) * KiyumeScore.LampFallStep + KiyumeScore.LampFallFadePerLamp;
                    if (fallTimer > total) {
                        fallPhase = FallPhase.Hold;
                        fallTimer = 0;
                    }
                    break;
                }
                case FallPhase.Hold:
                    //全村只剩你面前这点光
                    if (++fallTimer >= KiyumeScore.LampFallHold) {
                        fallPhase = FallPhase.Relight;
                        fallTimer = 0;
                    }
                    break;
                case FallPhase.Relight: {
                    //从近端反向回亮，无声，好像什么都没发生过
                    int n = fallOrder.Count;
                    for (int j = 0; j < n; j++) {
                        int local = fallTimer - j * KiyumeScore.LampRelightStep;
                        if (local < 0) {
                            break;
                        }
                        var entry = entries[fallOrder[n - 1 - j]];
                        float f = MathHelper.Clamp(local / (float)KiyumeScore.LampRelightFade, 0f, 1f);
                        KiyumeLampField.SetFactor(entry.Key, f);
                    }
                    fallTimer++;
                    int totalR = (n - 1) * KiyumeScore.LampRelightStep + KiyumeScore.LampRelightFade;
                    if (fallTimer > totalR) {
                        for (int i = 0; i < n; i++) {
                            KiyumeLampField.SetFactor(entries[fallOrder[i]].Key, 1f);
                        }
                        fallOrder.Clear();
                        fallPhase = FallPhase.Idle;
                        KiyumeDirector.ReleaseScare(KiyumeScareId.LampFall);
                    }
                    break;
                }
            }
        }

        //选侧预筛：窗口内点亮灯按左右计数，多的一侧去掉保留盏后仍有余量才值得开演
        private static bool CountFallSides(Player player, out bool west) {
            var entries = KiyumeLampField.Entries;
            int leftCount = 0;
            int rightCount = 0;
            for (int i = 0; i < entries.Count; i++) {
                if (!FallEligible(entries[i], player, out bool onWest)) {
                    continue;
                }
                if (onWest) {
                    leftCount++;
                }
                else {
                    rightCount++;
                }
            }
            west = leftCount >= rightCount;
            return Math.Max(leftCount, rightCount) > KiyumeScore.LampFallKeepNearest;
        }

        private static bool FallEligible(in KiyumeLampEntry entry, Player player, out bool onWest) {
            onWest = entry.WorldCenter.X < player.Center.X;
            float dist = Vector2.Distance(entry.WorldCenter, player.Center);
            if (dist < KiyumeScore.LampFallPickMinPx || dist > KiyumeScore.LampFallPickMaxPx) {
                return false;
            }
            return KiyumeLampField.GetFactor(entry.Key) >= 0.999f;
        }

        private static bool BuildFallOrder(Player player, bool west) {
            fallOrder.Clear();
            var entries = KiyumeLampField.Entries;
            for (int i = 0; i < entries.Count; i++) {
                if (FallEligible(entries[i], player, out bool onWest) && onWest == west) {
                    fallOrder.Add(i);
                }
            }
            sortOrigin = player.Center;
            fallOrder.Sort(FarToNear);
            //留最近 KeepNearest 盏：队尾（近端）裁掉
            int keep = KiyumeScore.LampFallKeepNearest;
            if (fallOrder.Count <= keep) {
                fallOrder.Clear();
                return false;
            }
            fallOrder.RemoveRange(fallOrder.Count - keep, keep);
            return true;
        }

        //==================== E2 回头灯灭（+ E1 降格单盏版）====================

        private static void UpdateCut(Player player) {
            switch (cutPhase) {
                case CutPhase.Idle: {
                    if (!KiyumeLampField.Scanned || fallPhase != FallPhase.Idle) {
                        return;
                    }
                    //事件自冷却 7200f（叠加共享冷却；武装跳过）
                    if (behindCooldown > 0 && !KiyumeDirectorDebug.PeekArm(KiyumeScareId.LampBehind)) {
                        return;
                    }
                    TryStartCut(player, KiyumeScareId.LampBehind,
                        KiyumeScore.LampBehindWindowLo, KiyumeScore.LampBehindWindowHi);
                    break;
                }
                case CutPhase.Cut: {
                    //4f 硬切 0
                    var entry = KiyumeLampField.Entries[cutLampIdx];
                    cutTimer++;
                    float f = 1f - MathHelper.Clamp(cutTimer / (float)KiyumeScore.LampBehindCut, 0f, 1f);
                    KiyumeLampField.SetFactor(entry.Key, f);
                    if (cutTimer >= KiyumeScore.LampBehindCut) {
                        cutPhase = CutPhase.Hold;
                        cutTimer = 0;
                    }
                    break;
                }
                case CutPhase.Hold:
                    if (++cutTimer >= KiyumeScore.LampBehindHold) {
                        cutPhase = CutPhase.Recover;
                        cutTimer = 0;
                    }
                    break;
                case CutPhase.Recover: {
                    var entry = KiyumeLampField.Entries[cutLampIdx];
                    cutTimer++;
                    float f = MathHelper.Clamp(cutTimer / (float)KiyumeScore.LampBehindRecover, 0f, 1f);
                    KiyumeLampField.SetFactor(entry.Key, f);
                    if (cutTimer >= KiyumeScore.LampBehindRecover) {
                        KiyumeLampField.SetFactor(entry.Key, 1f);
                        cutPhase = CutPhase.Idle;
                        cutLampIdx = -1;
                        KiyumeDirector.ReleaseScare(cutOwnerId);
                    }
                    break;
                }
            }
        }

        //筛灯成功才申请档期（不为无灯 tick 白烧预算/冷却）
        private static void TryStartCut(Player player, KiyumeScareId id, float lo, float hi) {
            int idx = PickBehindLamp(player);
            if (idx < 0) {
                return;
            }
            if (!KiyumeDirector.TryClaimScare(id, lo, hi)) {
                return;
            }
            cutOwnerId = id;
            cutLampIdx = idx;
            cutPhase = CutPhase.Cut;
            cutTimer = 0;
            var entry = KiyumeLampField.Entries[idx];
            //同帧关门声：有人回屋了；烟三口，转身还能看到余烟
            SoundEngine.PlaySound(SoundID.DoorClosed with {
                Volume = KiyumeScore.CapAccent(KiyumeScore.LampDoorVol),
                Pitch = KiyumeScore.LampDoorPitch,
                MaxInstances = 2
            }, entry.WorldCenter);
            SnuffPuff(entry.WorldCenter, KiyumeScore.LampBehindSmoke);
            if (id == KiyumeScareId.LampBehind) {
                behindCooldown = KiyumeScore.LampBehindSelfCooldown;
            }
        }

        //背向筛灯：dot(朝灯方向, 面朝) < -0.4 且 400~900px 最近一盏点亮灯
        private static int PickBehindLamp(Player player) {
            var entries = KiyumeLampField.Entries;
            int best = -1;
            float bestDist = float.MaxValue;
            for (int i = 0; i < entries.Count; i++) {
                Vector2 toLamp = entries[i].WorldCenter - player.Center;
                float dist = toLamp.Length();
                if (dist < KiyumeScore.LampBehindMinPx || dist > KiyumeScore.LampBehindMaxPx) {
                    continue;
                }
                if (toLamp.X * player.direction / dist >= KiyumeScore.LampBehindDot) {
                    continue;
                }
                if (KiyumeLampField.GetFactor(entries[i].Key) < 0.999f) {
                    continue;
                }
                if (dist < bestDist) {
                    bestDist = dist;
                    best = i;
                }
            }
            return best;
        }

        //==================== E3 无风铃 ====================

        private static void UpdateBell(Player player) {
            var entries = KiyumeLampField.Entries;
            if (bellTimer > 0) {
                bellTimer--;
                if (bellLampIdx >= 0) {
                    //灯影应铃一颤：两个快周期 1→0.7→1→0.7→1，收尾精确归 1
                    var entry = entries[bellLampIdx];
                    if (bellTimer == 0) {
                        KiyumeLampField.SetFactor(entry.Key, 1f);
                    }
                    else {
                        float p = 1f - bellTimer / (float)KiyumeScore.StillBellFlicker;
                        float f = 0.85f + 0.15f * MathF.Cos(p * MathHelper.TwoPi * 2f);
                        KiyumeLampField.SetFactor(entry.Key, f);
                    }
                }
                if (bellTimer == 0) {
                    bellLampIdx = -1;
                    KiyumeDirector.ReleaseScare(KiyumeScareId.StillBell);
                }
                return;
            }
            //武装门：无风持续 ≥90f（武装也不跳过——风声明显时拒绝，验收条款 4）
            if (stillTicks < KiyumeScore.StillBellGateHold) {
                return;
            }
            //檐铃是村带的物件（点缀表同带）
            if (KiyumeMetrics.BandIndexForColumn((int)(player.Center.X / 16f)) != 2) {
                return;
            }
            if (!KiyumeDirector.TryClaimScare(KiyumeScareId.StillBell,
                KiyumeScore.StillBellWindowLo, KiyumeScore.StillBellWindowHi)) {
                return;
            }
            //侧后一声孤铃：风停了有一会儿了
            float dist = Main.rand.NextFloat(KiyumeScore.StillBellMinPx, KiyumeScore.StillBellMaxPx);
            Vector2 dir = new Vector2(-player.direction, 0f).RotatedBy(Main.rand.NextFloat(-0.6f, 0.6f));
            SoundEngine.PlaySound(SoundID.Item35 with {
                Volume = KiyumeScore.CapAccent(KiyumeScore.StillBellVol),
                Pitch = KiyumeScore.StillBellPitch,
                MaxInstances = 2
            }, player.Center + dir * dist);
            bellTimer = KiyumeScore.StillBellFlicker;
            bellLampIdx = NearestLitLamp(player.Center, KiyumeScore.StillBellLampRadiusPx);
        }

        private static int NearestLitLamp(Vector2 center, float radiusPx) {
            var entries = KiyumeLampField.Entries;
            int best = -1;
            float bestDist = radiusPx;
            for (int i = 0; i < entries.Count; i++) {
                float dist = Vector2.Distance(entries[i].WorldCenter, center);
                if (dist >= bestDist || KiyumeLampField.GetFactor(entries[i].Key) < 0.999f) {
                    continue;
                }
                bestDist = dist;
                best = i;
            }
            return best;
        }

        //==================== 共用 ====================

        //熄灯烟：从灯口撕下的一小口暗雾（犬烟同款粒子与色系）
        private static void SnuffPuff(Vector2 center, int count) {
            for (int i = 0; i < count; i++) {
                PRTLoader.NewParticle<PRT_KikasaHoundSmoke>(
                    center + Main.rand.NextVector2Circular(6f, 5f),
                    new Vector2(Main.rand.NextFloat(-0.4f, 0.4f), Main.rand.NextFloat(-1.1f, -0.4f)),
                    new Color(46, 16, 20) * 0.8f, Main.rand.NextFloat(0.24f, 0.4f))
                    ?.Configure(Main.rand.Next(24, 41), 0.012f);
            }
        }

        /// <summary>一行状态摘要（TestItem 验收用）</summary>
        internal static string StatusLine()
            => $"[灯火事件] E1相位{fallPhase} t{fallTimer} 波{fallSnuffed}/{fallOrder.Count}"
            + $" 村驻{villageTicks / 60}s E2相位{cutPhase}(槽主{cutOwnerId}) 自冷{behindCooldown}"
            + $" E3无风{stillTicks}f 铃颤{bellTimer}f";
    }
}
