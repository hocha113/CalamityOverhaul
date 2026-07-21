using CalamityOverhaul.Common;
using CalamityOverhaul.Content.HackTimes;
using CalamityOverhaul.Content.RAMSystems;
using CalamityOverhaul.Content.TimeFreezes;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces.Restart
{
    /// <summary>领域重启，四阶段，奇点帧恢复；层≥1</summary>
    internal class CyberRestart : ICWRLoader
    {
        void ICWRLoader.UnLoadData() => Reset();

        /// <summary>最低层数</summary>
        public const int RequiredLayer = 1;

        /// <summary>单次 RAM（<see cref="HackTime.InfiniteHack"/> 免耗）</summary>
        public const float RamCostPerCast = 6f;

        /// <summary>重启后 RAM 锁定帧数（约 22s）</summary>
        public const int RamLockFrames = 60 * 22;

        /// <summary>撕裂终点帧</summary>
        public const int PhaseTearEnd = 22;
        /// <summary>收缩终点帧</summary>
        public const int PhaseCollapseEnd = 50;
        /// <summary>奇点终点帧，恢复落点</summary>
        public const int PhaseSingularityEnd = 64;
        /// <summary>炸裂终点帧</summary>
        public const int PhaseBurstEnd = 92;
        /// <summary>整段演出长度</summary>
        public const int TotalFrames = PhaseBurstEnd;

        //本地计时
        private static int progressTimer;
        private static float progressTimerCarry;
        //锚定层，炸裂恢复用
        private static int anchorLayer;
        //奇点恢复已触发
        private static bool restoreFired;

        /// <summary>演出中</summary>
        public static bool IsActive => progressTimer > 0;

        /// <summary>进度 0..1</summary>
        public static float Progress => progressTimer <= 0 ? 0f
            : MathHelper.Clamp((float)progressTimer / TotalFrames, 0f, 1f);

        /// <summary>剩余冷却=RAM 锁定帧</summary>
        public static int CooldownRemain => RamSystem.LockRemain;

        /// <summary>冷却中=RAM 锁定</summary>
        public static bool OnCooldown => RamSystem.IsLocked;

        /// <summary>演出阶段</summary>
        public enum Phase
        {
            None,
            Tear,
            Collapse,
            Singularity,
            Burst,
        }

        /// <summary>当前阶段</summary>
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

        /// <summary>收缩末+奇点隐藏本地玩家，见 <see cref="CyberRestartHideOverride"/></summary>
        public static bool IsLocalPlayerHidden {
            get {
                int t = progressTimer;
                //收缩末隐藏，炸裂前复显
                return t > PhaseCollapseEnd - 8 && t <= PhaseSingularityEnd + 2;
            }
        }

        /// <summary>校验后触发重启</summary>
        public static void TryRestart(Player owner) {
            if (owner == null || !owner.Alives()) return;

            //域激活、强度、层
            if (!Cyberspace.Active) return;
            if (Cyberspace.Intensity < 0.5f) return;
            if (Cyberspace.CurrentLayer < RequiredLayer) return;

            //演出中/锁定拒
            if (progressTimer > 0 || RamSystem.IsLocked) {
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(CWRSound.FailureCurrent with {
                        Volume = 0.35f,
                        Pitch = -0.4f,
                    }, owner.Center);
                    //锁定中额外闪 HUD
                    if (RamSystem.IsLocked) {
                        RamSystem.NotifyInsufficient();
                    }
                }
                return;
            }

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

        /// <summary>耗 RAM、计时、演出弹幕、起手音</summary>
        private static void Activate(Player owner) {
            if (!HackTime.InfiniteHack) {
                RamSystem.TryConsume((int)Math.Ceiling(RamCostPerCast));
            }

            progressTimer = 1;
            progressTimerCarry = 0f;
            anchorLayer = Math.Clamp(Cyberspace.CurrentLayer, 1, Cyberspace.MaxLayerCount);
            restoreFired = false;

            //仅 myPlayer 生成演出弹幕
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

        /// <summary>每帧推进演出</summary>
        public static void Update() {
            if (progressTimer <= 0) {
                //非演出清 RestartCollapse
                if (Cyberspace.RestartCollapse > 0f) {
                    Cyberspace.RestartCollapse = MathHelper.Lerp(Cyberspace.RestartCollapse, 0f, 0.35f);
                    if (Cyberspace.RestartCollapse < 0.005f) {
                        Cyberspace.RestartCollapse = 0f;
                    }
                }
                return;
            }

            //驱动 RestartCollapse
            float collapse = ComputeCollapse(progressTimer);
            Cyberspace.RestartCollapse = collapse;

            //奇点中点恢复 HP/魔/异常
            int restoreFrame = (PhaseCollapseEnd + PhaseSingularityEnd) / 2;
            if (!restoreFired && progressTimer >= restoreFrame) {
                restoreFired = true;
                ApplyRestoreEffects();
            }

            //炸裂起点冲击波+故障雷
            if (progressTimer == PhaseSingularityEnd + 1) {
                SpawnBurstVFX();
            }

            progressTimer += TimeGear.PullFrameAdvance(ref progressTimerCarry);
            if (progressTimer > TotalFrames) {
                FinishRoutine();
            }
        }

        /// <summary>视觉收缩系数 0..1</summary>
        private static float ComputeCollapse(int t) {
            if (t <= PhaseTearEnd) {
                //撕裂，微抖
                float k = (float)t / PhaseTearEnd;
                return MathHelper.Clamp(k * 0.05f, 0f, 0.05f);
            }
            if (t <= PhaseCollapseEnd) {
                //收缩 0.05→1
                float k = (float)(t - PhaseTearEnd) / (PhaseCollapseEnd - PhaseTearEnd);
                float ease = MathF.Pow(k, 2.2f);
                return MathHelper.Lerp(0.05f, 1f, ease);
            }
            if (t <= PhaseSingularityEnd) {
                //奇点全缩+心跳
                float k = (float)(t - PhaseCollapseEnd) / (PhaseSingularityEnd - PhaseCollapseEnd);
                float pulse = MathF.Sin(k * MathF.PI * 2.5f) * 0.04f;
                return MathHelper.Clamp(0.96f + pulse, 0.92f, 1f);
            }
            //炸裂 1→0
            float kb = (float)(t - PhaseSingularityEnd) / (PhaseBurstEnd - PhaseSingularityEnd);
            float easeOut = 1f - MathF.Pow(1f - kb, 3.0f);
            return MathHelper.Clamp(1f - easeOut, 0f, 1f);
        }

        /// <summary>奇点恢复 HP/魔/异常+无敌</summary>
        private static void ApplyRestoreEffects() {
            Player owner = Main.player[Main.myPlayer];
            if (owner == null || !owner.active) return;

            //仅 myPlayer
            if (Main.myPlayer != owner.whoAmI) return;

            //满血满魔
            owner.statLife = owner.statLifeMax2;
            owner.statMana = owner.statManaMax2;

            //仅清 debuff
            for (int i = 0; i < Player.MaxBuffs; i++) {
                int buffType = owner.buffType[i];
                if (buffType <= 0) continue;
                if (Main.debuff[buffType]) {
                    owner.DelBuff(i);
                    i--;
                }
            }

            //榨干并锁定 RAM，取代冷却
            RamSystem.SystemLock(RamLockFrames);

            //短暂无敌
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

        /// <summary>炸裂开端冲击 VFX</summary>
        private static void SpawnBurstVFX() {
            Player owner = Main.player[Main.myPlayer];
            if (owner == null || !owner.active) return;
            if (Main.myPlayer != owner.whoAmI) return;

            IEntitySource source = owner.GetSource_FromThis();
            Vector2 center = owner.Center;

            //冲击波，同激活款
            Projectile.NewProjectile(source, center, Vector2.Zero,
                ModContent.ProjectileType<CyberShockwaveProj>(), 0, 0, owner.whoAmI);

            //故障雷，随锚定层
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

        /// <summary>收尾清进度，冷却归 RamSystem</summary>
        private static void FinishRoutine() {
            progressTimer = 0;
            progressTimerCarry = 0f;
            restoreFired = false;
        }

        /// <summary>清计时</summary>
        public static void Reset() {
            progressTimer = 0;
            progressTimerCarry = 0f;
            anchorLayer = 0;
            restoreFired = false;
            Cyberspace.RestartCollapse = 0f;
        }
    }
}
