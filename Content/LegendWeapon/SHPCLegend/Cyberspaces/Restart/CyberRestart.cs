using CalamityOverhaul.Common;
using CalamityOverhaul.Content.HackTimes;
using CalamityOverhaul.Content.RAMSystems;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces.Restart
{
    /// <summary>
    /// 赛博空间重启技能 —— 状态管理器
    /// <br/>领域内重启：领域被撕开数道黑墙裂缝→迅速向心收缩→玩家压缩为红黑维度核心裂缝→猛然炸裂复原
    /// <br/>整个过程对外播一段固定的演出，演出关键帧瞬间恢复 HP/魔力/RAM/异常状态
    /// <br/>独立于 HackTime，仅在赛博空间激活且当前层 ≥ <see cref="RequiredLayer"/> 时可用
    /// </summary>
    internal class CyberRestart : ICWRLoader
    {
        void ICWRLoader.UnLoadData() => Reset();

        /// <summary>
        /// 启用重启所需的最低赛博空间层数
        /// </summary>
        public const int RequiredLayer = 1;

        /// <summary>
        /// 单次重启消耗的 RAM 量（<see cref="HackTime.InfiniteHack"/> 模式下不消耗）
        /// </summary>
        public const float RamCostPerCast = 6f;

        /// <summary>
        /// 重启生效后，RAM 锁定为 0 的帧数（约 22 秒）。锁定期间 RAM 不恢复，也不能使用任何消耗 RAM 的技能，以此代替传统冷却
        /// </summary>
        public const int RamLockFrames = 60 * 22;

        //——四阶段帧时点，TotalFrames 为整体演出长度——
        /// <summary>撕裂阶段终点：黑墙裂缝在领域中蔓延</summary>
        public const int PhaseTearEnd = 22;
        /// <summary>收缩阶段终点：领域整体压缩到核心</summary>
        public const int PhaseCollapseEnd = 50;
        /// <summary>奇点阶段终点：玩家化作维度核心裂缝，于此帧恢复全状态</summary>
        public const int PhaseSingularityEnd = 64;
        /// <summary>炸裂阶段终点：领域猛然炸裂复原</summary>
        public const int PhaseBurstEnd = 92;
        /// <summary>整段演出长度</summary>
        public const int TotalFrames = PhaseBurstEnd;

        //本地玩家专用计时
        private static int progressTimer;
        //演出锚定层数：触发时领域所处层数，炸裂阶段恢复时使用
        private static int anchorLayer;
        //奇点关键帧是否已经触发恢复，避免帧重入
        private static bool restoreFired;

        /// <summary>
        /// 当前是否处于重启演出中
        /// </summary>
        public static bool IsActive => progressTimer > 0;

        /// <summary>
        /// 当前演出进度 (0..1)
        /// </summary>
        public static float Progress => progressTimer <= 0 ? 0f
            : MathHelper.Clamp((float)progressTimer / TotalFrames, 0f, 1f);

        /// <summary>
        /// 当前剩余冷却帧（HUD/UI 可读），现代表 RAM 锁定剩余帧数
        /// </summary>
        public static int CooldownRemain => RamSystem.LockRemain;

        /// <summary>
        /// 是否处于冷却中（RAM 锁定期即冷却期）
        /// </summary>
        public static bool OnCooldown => RamSystem.IsLocked;

        /// <summary>
        /// 演出阶段枚举
        /// </summary>
        public enum Phase
        {
            None,
            Tear,
            Collapse,
            Singularity,
            Burst,
        }

        /// <summary>
        /// 当前所处演出阶段
        /// </summary>
        public static Phase CurrentPhase {
            get {
                int t = progressTimer;
                if (t <= 0) return Phase.None;
                if (t <= PhaseTearEnd) return Phase.Tear;
                if (t <= PhaseCollapseEnd) return Phase.Collapse;
                if (t <= PhaseSingularityEnd) return Phase.Singularity;
                return Phase.Burst;
            }
        }

        /// <summary>
        /// 演出期间是否需要隐藏本地玩家（收缩末段+奇点段）
        /// <br/><see cref="CyberRestartHideOverride"/> 据此移除本玩家绘制
        /// </summary>
        public static bool IsLocalPlayerHidden {
            get {
                int t = progressTimer;
                //从收缩末期开始隐藏，给玩家"被吸进核心"的尾韵；炸裂前一帧恢复显示
                return t > PhaseCollapseEnd - 8 && t <= PhaseSingularityEnd + 2;
            }
        }

        /// <summary>
        /// 触发重启：经过领域/层级/RAM/冷却校验后立即开始演出
        /// </summary>
        public static void TryRestart(Player owner) {
            if (owner == null || !owner.Alives()) return;

            //领域必须激活、当前帧视觉强度足够、当前层达标
            if (!Cyberspace.Active) return;
            if (Cyberspace.Intensity < 0.5f) return;
            if (Cyberspace.CurrentLayer < RequiredLayer) return;

            //演出进行中或 RAM 锁定中——拒绝并播放失败反馈
            if (progressTimer > 0 || RamSystem.IsLocked) {
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(CWRSound.FailureCurrent with {
                        Volume = 0.35f,
                        Pitch = -0.4f,
                    }, owner.Center);
                    //锁定中仍尝试重启，额外推一个闪烁提醒
                    if (RamSystem.IsLocked) {
                        RamSystem.NotifyInsufficient();
                    }
                }
                return;
            }

            //RAM 检查
            if (!HackTime.InfiniteHack && RamSystem.CurrentRam < RamCostPerCast) {
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(CWRSound.FailureCurrent with {
                        Volume = 0.4f,
                        Pitch = -0.3f,
                    }, owner.Center);
                    RamSystem.NotifyInsufficient();
                    Color denyColor = new(255, 90, 80);
                    CombatText.NewText(owner.Hitbox, denyColor, "// LOW RAM", true);
                }
                return;
            }

            Activate(owner);
        }

        /// <summary>
        /// 实际触发：消耗 RAM、设定计时、生成演出弹幕、播放起手音效
        /// </summary>
        private static void Activate(Player owner) {
            if (!HackTime.InfiniteHack) {
                RamSystem.TryConsume((int)Math.Ceiling(RamCostPerCast));
            }

            progressTimer = 1;
            anchorLayer = Math.Clamp(Cyberspace.CurrentLayer, 1, Cyberspace.MaxLayerCount);
            restoreFired = false;

            //仅由本地玩家生成演出弹幕，远端通过弹幕同步看到效果
            if (Main.myPlayer == owner.whoAmI) {
                IEntitySource source = owner.GetSource_FromThis();
                Projectile.NewProjectile(source, owner.Center, Vector2.Zero,
                    ModContent.ProjectileType<CyberRestartProj>(), 0, 0, owner.whoAmI);
            }

            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(CWRSound.FaultOccurred with {
                    Volume = 0.7f,
                    Pitch = -0.15f,
                }, owner.Center);
                SoundEngine.PlaySound(CWRSound.Fault with {
                    Volume = 0.6f,
                    Pitch = -0.35f,
                }, owner.Center);
            }
        }

        /// <summary>
        /// 每帧推进：演出进度推进、各阶段视觉/逻辑钩子触发
        /// </summary>
        public static void Update() {
            if (progressTimer <= 0) {
                //非演出态保证 RestartCollapse 不残留
                if (Cyberspace.RestartCollapse > 0f) {
                    Cyberspace.RestartCollapse = MathHelper.Lerp(Cyberspace.RestartCollapse, 0f, 0.35f);
                    if (Cyberspace.RestartCollapse < 0.005f) {
                        Cyberspace.RestartCollapse = 0f;
                    }
                }
                return;
            }

            //驱动 RestartCollapse 让领域整体可视范围跟随阶段缩放
            float collapse = ComputeCollapse(progressTimer);
            Cyberspace.RestartCollapse = collapse;

            //奇点关键帧（PhaseSingularityEnd 帧的中点）：恢复 HP / 魔力 / RAM / 清异常
            int restoreFrame = (PhaseCollapseEnd + PhaseSingularityEnd) / 2;
            if (!restoreFired && progressTimer >= restoreFrame) {
                restoreFired = true;
                ApplyRestoreEffects();
            }

            //炸裂关键帧（炸裂段开始）：弹出冲击波 + 多发故障闪电，给观感"猛然炸开"
            if (progressTimer == PhaseSingularityEnd + 1) {
                SpawnBurstVFX();
            }

            progressTimer++;
            if (progressTimer > TotalFrames) {
                FinishRoutine();
            }
        }

        /// <summary>
        /// 根据演出进度推算当前帧的视觉收缩系数 (0=正常 1=完全压缩)
        /// </summary>
        private static float ComputeCollapse(int t) {
            if (t <= PhaseTearEnd) {
                //撕裂段：仅边缘抖动，不收缩
                float k = (float)t / PhaseTearEnd;
                return MathHelper.Clamp(k * 0.05f, 0f, 0.05f);
            }
            if (t <= PhaseCollapseEnd) {
                //收缩段：0.05 → 1，临近终点加速收紧
                float k = (float)(t - PhaseTearEnd) / (PhaseCollapseEnd - PhaseTearEnd);
                float ease = MathF.Pow(k, 2.2f);
                return MathHelper.Lerp(0.05f, 1f, ease);
            }
            if (t <= PhaseSingularityEnd) {
                //奇点段：完全收缩，伴随轻微"心跳"波动
                float k = (float)(t - PhaseCollapseEnd) / (PhaseSingularityEnd - PhaseCollapseEnd);
                float pulse = MathF.Sin(k * MathF.PI * 2.5f) * 0.04f;
                return MathHelper.Clamp(0.96f + pulse, 0.92f, 1f);
            }
            //炸裂段：1 → 0，前段极速回扩
            float kb = (float)(t - PhaseSingularityEnd) / (PhaseBurstEnd - PhaseSingularityEnd);
            float easeOut = 1f - MathF.Pow(1f - kb, 3.0f);
            return MathHelper.Clamp(1f - easeOut, 0f, 1f);
        }

        /// <summary>
        /// 奇点关键帧：恢复 HP/魔力/RAM/异常状态、给一段无敌帧、播放恢复音
        /// </summary>
        private static void ApplyRestoreEffects() {
            Player owner = Main.player[Main.myPlayer];
            if (owner == null || !owner.active) return;

            //仅本地客户端处理自身恢复，避免重复广播
            if (Main.myPlayer != owner.whoAmI) return;

            //满血、满魔——重启即"刚刚启动的状态"
            owner.statLife = owner.statLifeMax2;
            owner.statMana = owner.statManaMax2;

            //清除负面状态：仅清玩家可承受的debuff，避免误清自身buff
            for (int i = 0; i < Player.MaxBuffs; i++) {
                int buffType = owner.buffType[i];
                if (buffType <= 0) continue;
                if (Main.debuff[buffType]) {
                    owner.DelBuff(i);
                    i--;
                }
            }

            //RAM 不再刷满：重启代价为榨干并锁定 RAM，HUD 进入红色故障状态，以此取代冷却机制
            RamSystem.SystemLock(RamLockFrames);

            //短暂无敌防止刚恢复就被秒
            owner.immune = true;
            owner.immuneTime = Math.Max(owner.immuneTime, 40);

            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(CWRSound.Faultrelease with {
                    Volume = 0.85f,
                    Pitch = 0.25f,
                }, owner.Center);
                SoundEngine.PlaySound(CWRSound.FaultTransition with {
                    Volume = 0.55f,
                    Pitch = 0.4f,
                }, owner.Center);
                Color reviveColor = new(255, 220, 200);
                CombatText.NewText(owner.Hitbox, reviveColor, "// REBOOT", true);
            }
        }

        /// <summary>
        /// 炸裂阶段开端：领域猛然炸裂复原时的额外冲击演出
        /// </summary>
        private static void SpawnBurstVFX() {
            Player owner = Main.player[Main.myPlayer];
            if (owner == null || !owner.active) return;
            if (Main.myPlayer != owner.whoAmI) return;

            IEntitySource source = owner.GetSource_FromThis();
            Vector2 center = owner.Center;

            //冲击波：复用领域激活同款，强化"重启完成回归"的视觉对照
            Projectile.NewProjectile(source, center, Vector2.Zero,
                ModContent.ProjectileType<CyberShockwaveProj>(), 0, 0, owner.whoAmI);

            //故障闪电：数量随锚定层数递增
            int boltCount = 6 + anchorLayer * 2;
            float baseAngle = Main.rand.NextFloat() * MathHelper.TwoPi;
            for (int i = 0; i < boltCount; i++) {
                float angle = baseAngle + MathHelper.TwoPi * i / boltCount
                    + Main.rand.NextFloat(-0.3f, 0.3f);
                int delay = Main.rand.Next(0, 5);
                Projectile.NewProjectile(source, center, Vector2.Zero,
                    ModContent.ProjectileType<CyberGlitchBoltProj>(), 0, 0, owner.whoAmI,
                    ai0: angle, ai1: delay);
            }

            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(CWRSound.FaultTransition with {
                    Volume = 0.85f,
                    Pitch = 0.1f,
                }, center);
                SoundEngine.PlaySound(CWRSound.Faultrelease with {
                    Volume = 0.7f,
                    Pitch = -0.05f,
                }, center);
            }
        }

        /// <summary>
        /// 演出收尾：清空进度（冷却现由 RamSystem 锁定接管）
        /// </summary>
        private static void FinishRoutine() {
            progressTimer = 0;
            restoreFired = false;
        }

        /// <summary>
        /// 立即清空所有计时器（如玩家退出/读档）
        /// </summary>
        public static void Reset() {
            progressTimer = 0;
            anchorLayer = 0;
            restoreFired = false;
            Cyberspace.RestartCollapse = 0f;
        }
    }
}
