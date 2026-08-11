using CalamityOverhaul.Content.HackTimes.CircuitNodes;
using CalamityOverhaul.Content.PRTTypes;
using CalamityOverhaul.Content.RAMSystems;
using CalamityOverhaul.Content.TimeFreezes;
using InnoVault.PRT;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.HackTimes.Protocols
{
    /// <summary>
    /// 提权：元协议，改的是骇入系统本身。四十五秒内施法者的协议 RAM 成本 −1（下限 1）、
    /// 上传时间 ×0.6、距离判定放行；代价是期间 RAM 只能花不能回（走 IRamModifierProvider
    /// 把回复率压零），且塔在广播你的位置（等效仇恨 +1500）。
    /// 同一玩家不可叠加，重复施放只刷新时长；InfiniteHack 活跃时拒绝施放。<br/>
    /// 折扣状态放在 <see cref="PrivilegeEscalateState"/>；三处消费点
    /// （HackCostEvaluator 成本 −1 / HackTimeNetSync 上传 ×0.6 / 距离闸放行）
    /// 已随 HACK32 整合批接线，面板读数与徽章同步跟进
    /// </summary>
    internal class PrivilegeEscalate : QuickHackDef
    {
        //持续四十五秒
        private const int DurationFrames = 2700;

        private static readonly Color UplinkColor = new(140, 255, 170);

        public override void SetDefaults() {
            UploadTime = 240;
            RamCost = 8;
            Category = QuickHackCategory.Covert;
            SupportedTargets = HackTargetKind.SignalTower;
            UnlockedByDefault = false;
        }

        public override int GetDuration() => DurationFrames;

        public override void Unload() {
            base.Unload();
            PrivilegeEscalateState.ClearAll();
        }

        public override bool CanApplyTo(IHackTarget target) {
            //已经全权限了，没什么可提的
            return base.CanApplyTo(target)
                && target is IHackableSignalTower
                && !HackTime.InfiniteHack;
        }

        public override bool OnApply(IHackTarget target, Player caster) {
            if (target is not IHackableSignalTower tower) {
                return false;
            }
            if (Main.netMode != NetmodeID.MultiplayerClient) {
                int casterIndex = caster?.whoAmI ?? -1;
                if (casterIndex < 0) {
                    return false;
                }
                //重复施放＝刷新时长，不叠折扣
                PrivilegeEscalateState.Grant(casterIndex, DurationFrames);
                (tower as IPrivilegeUplinkTower)?.BeginPrivilegeUplink(DurationFrames, caster);
            }
            if (Main.netMode != NetmodeID.Server) {
                EmitUplinkBurst(tower.WorldCenter);
            }
            return true;
        }

        public override void OnReplicatedApply(IHackTarget target, int elapsed) {
            EmitUplinkBurst(target.WorldCenter);
        }

        public override bool OnTick(IHackTarget target, int elapsed) {
            //把剩余窗口对齐给状态账：重复施放后旧效果先到期也不会把新窗口拽短
            if (Main.netMode != NetmodeID.MultiplayerClient) {
                Player caster = HackEffectTracker.ResolveEffectCaster(this, target);
                if (caster != null) {
                    PrivilegeEscalateState.EnsureAtLeast(caster.whoAmI, DurationFrames - elapsed);
                }
            }
            return true;
        }

        //OnRemove 刻意留空：窗口由 PrivilegeEscalateState 自己倒计时到期，
        //效果实例提前消亡（塔没了）不吊销已授予的权限

        private static void EmitUplinkBurst(Vector2 center) {
            for (int i = 0; i < 22; i++) {
                Vector2 vel = new(Main.rand.NextFloat(-1.2f, 1.2f), Main.rand.NextFloat(-7f, -3f));
                PRTLoader.NewParticle<PRT_Spark>(center + Main.rand.NextVector2Circular(12f, 30f),
                    vel, UplinkColor, 0.9f)?.Configure(false, 34);
            }
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item4 with { Pitch = 0.5f, Volume = 0.8f }, center);
            }
        }
    }

    /// <summary>
    /// 提权窗口的权威账：玩家索引 → 剩余帧。倒计时与效果追踪器共用
    /// <see cref="TimeGear"/> 节拍，时停期间窗口不流失。<br/>
    /// 折扣的消费点（GetActualCost / 上传时间 / 距离闸 / 面板显示）已接线，
    /// 这里只负责供出干净的查询口
    /// </summary>
    internal static class PrivilegeEscalateState
    {
        private static readonly Dictionary<int, int> remainingFrames = [];
        private static readonly List<int> expiredBuffer = [];
        private static float timeCarry;

        /// <summary>授予或刷新窗口，取剩余与新授的较大值</summary>
        internal static void Grant(int playerIndex, int frames) {
            if (playerIndex < 0 || playerIndex >= Main.maxPlayers || frames <= 0) {
                return;
            }
            remainingFrames[playerIndex] = remainingFrames.TryGetValue(playerIndex, out int left)
                ? Math.Max(left, frames) : frames;
        }

        /// <summary>把窗口至少顶到指定剩余，效果 Tick 逐帧对账用</summary>
        internal static void EnsureAtLeast(int playerIndex, int frames) => Grant(playerIndex, frames);

        internal static bool IsActiveFor(int playerIndex)
            => remainingFrames.TryGetValue(playerIndex, out int left) && left > 0;

        internal static bool IsActiveFor(Player player)
            => player?.active == true && IsActiveFor(player.whoAmI);

        /// <summary>剩余秒数，HUD/面板显示用；未激活给 0</summary>
        internal static int RemainingSeconds(int playerIndex)
            => remainingFrames.TryGetValue(playerIndex, out int left)
                ? (int)MathF.Ceiling(left / 60f) : 0;

        /// <summary>RAM 成本折扣：−1，下限 1。Boss 倍率照常，不叠优惠</summary>
        internal static int ApplyCostDiscount(int cost, Player caster)
            => IsActiveFor(caster) ? Math.Max(1, cost - 1) : cost;

        /// <summary>上传时间 ×0.6，下限 1 帧</summary>
        internal static int ApplyUploadTime(int uploadFrames, Player caster)
            => IsActiveFor(caster) ? Math.Max(1, (int)(uploadFrames * 0.6f)) : uploadFrames;

        /// <summary>距离/领域半径判定放行（屏幕内即可骇）</summary>
        internal static bool BypassRangeGate(Player caster) => IsActiveFor(caster);

        internal static void ClearAll() {
            remainingFrames.Clear();
            timeCarry = 0f;
        }

        /// <summary>每帧倒计时，节拍对齐效果追踪器：时停冻结窗口不冻结代价之外的东西</summary>
        internal static void Tick() {
            if (remainingFrames.Count == 0) {
                return;
            }
            if (TimeGear.PullFrameAdvance(ref timeCarry) <= 0) {
                return;
            }
            expiredBuffer.Clear();
            foreach (int key in remainingFrames.Keys) {
                int left = remainingFrames[key] - 1;
                if (left <= 0) {
                    expiredBuffer.Add(key);
                }
                else {
                    remainingFrames[key] = left;
                }
            }
            for (int i = 0; i < expiredBuffer.Count; i++) {
                remainingFrames.Remove(expiredBuffer[i]);
            }
        }
    }

    /// <summary>提权窗口的倒计时驱动</summary>
    internal class PrivilegeEscalateSystem : ModSystem
    {
        public override void PostUpdateEverything() => PrivilegeEscalateState.Tick();
    }

    /// <summary>
    /// 提权的代价其一：窗口内 RAM 回复率压零。走既有 IRamModifierProvider 通道，
    /// 注册照 SelfHackCrystalRamProvider 的形状自持（ICWRLoader），不动任何现有文件；
    /// 大负数经 RAMPlayer.RecomputeEffectiveCore 的 [0, Max] 夹取落成零回复
    /// </summary>
    internal sealed class PrivilegeRamSuppressor : IRamModifierProvider, ICWRLoader
    {
        public int MaxRamBonus => 0;

        public float RecoveryRateBonus => -10000f;

        public bool IsActive(Player player) => PrivilegeEscalateState.IsActiveFor(player);

        void ICWRLoader.LoadData() => RamSystem.RegisterProvider(this);

        void ICWRLoader.UnLoadData() => RamSystem.UnregisterProvider(this);
    }

    /// <summary>
    /// 提权的代价其二：塔在广播你的位置。等效仇恨 +1500，
    /// 敌怪索敌半径翻倍在设计稿里与它同义（原版 aggro 抬的就是被选中优先级），折进这一笔
    /// </summary>
    internal class PrivilegeEscalatePlayer : ModPlayer
    {
        public override void PostUpdateEquips() {
            if (PrivilegeEscalateState.IsActiveFor(Player)) {
                Player.aggro += 1500;
            }
        }
    }
}
