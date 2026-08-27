using CalamityOverhaul.Content.GameModes.BrutalMobs.Common;
using CalamityOverhaul.Content.GameModes.BrutalMobs.DungeonDeep.Projectiles;
using System;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.DungeonDeep
{
    /// <summary>
    /// 近战分册：怒骨近战族军列冲锋（跺骨前摇→包络冲锋→力竭）、
    /// Paladin 锤震地（锥形骨刺 omen）与举盾格挡、BoneLee 压身二连段。
    /// 位移一律 <see cref="MobDash.Envelope"/> 塑形并除回提速补偿；相位沿 netUpdate、长保持段 %6 低频重推
    /// </summary>
    internal partial class DungeonDeepNPC
    {
        //==== 怒骨军列冲锋 ====
        /// <summary>同族同时冲锋硬上限（军列一次最多两人压上）</summary>
        private const int ChargeConcurrentCap = 2;
        /// <summary>静态节拍错拍：两次冲锋起手的最小间隔帧（同屏军列依次压上而非齐动）</summary>
        private const int ChargeBeatFrames = 22;
        private const float ChargeMinRangeX = 90f;
        private const float ChargeMaxRangeX = 460f;
        private const float ChargeMaxRangeY = 110f;
        /// <summary>冲锋冷却（档位 1/2/3），另加公共抖动</summary>
        private static readonly int[] ChargeCooldownByTier = [400, 340, 280];

        //==== Paladin 锤震地 ====
        private const float HammerMinRange = 120f;
        private const float HammerMaxRange = 420f;
        /// <summary>骨刺伤害 = npc.damage（已缩放值）× 此值</summary>
        private const float SpikeDamageFrac = 0.5f;
        /// <summary>锥幕预告全局并发上限</summary>
        private const int HammerCap = 4;
        /// <summary>抡锤后的收势帧</summary>
        private const int HammerRecoverFrames = 20;
        private static readonly int[] HammerCooldownByTier = [560, 500, 440];

        //==== BoneLee 二连段 ====
        /// <summary>BoneLee 并发上限（武僧一次只来一位）</summary>
        private const int BoneLeeCap = 2;
        private const float BoneLeeMinRange = 70f;
        private const float BoneLeeMaxRange = 360f;
        /// <summary>突进拳名义峰速</summary>
        private const float BLDash1Peak = 11.5f;
        /// <summary>回旋踢名义峰速</summary>
        private const float BLDash2Peak = 12.5f;
        private static readonly int[] BoneLeeCooldownByTier = [430, 380, 330];

        private int ChargeCooldown() => ChargeCooldownByTier[boundTier - 1] + Main.rand.Next(CooldownJitter + 1);

        #region 怒骨军列冲锋
        /// <summary>军列冲锋起手：并发闸+静态节拍错拍→锁水平向→预告实体→跺骨压速前摇</summary>
        private void TryStartCharge(NPC npc, Player player) {
            if (npc.velocity.Y != 0f) {
                cooldown = RetryDelay;
                return;
            }
            float dx = Math.Abs(player.Center.X - npc.Center.X);
            float dy = Math.Abs(player.Bottom.Y - npc.Bottom.Y);
            if (dx < ChargeMinRangeX || dx > ChargeMaxRangeX || dy > ChargeMaxRangeY || !CanSee(npc, player)) {
                cooldown = RetryDelay;
                return;
            }
            //军列并发闸：同族同时冲锋 ≤2（数活着的预告实体，自愈无漂移）
            if (DdChargeOmen.CountActiveCharges(boneLee: false) >= ChargeConcurrentCap) {
                cooldown = RetryDelay;
                return;
            }
            //静态节拍错拍：距离上一次冲锋起手不足一拍则退避
            if (Main.GameUpdateCount - lastChargeBeat < ChargeBeatFrames) {
                cooldown = RetryDelay;
                return;
            }

            DdChargeRow row = ChargeRows[npc.type];
            //锁定水平冲向（预告即承诺，锁定随生成包同步，此后不再重瞄）
            lockDir = player.Center.X >= npc.Center.X ? 0f : MathHelper.Pi;
            omenIndex = Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, Vector2.Zero,
                ModContent.ProjectileType<DdChargeOmen>(), 0, 0f, Main.myPlayer,
                PackSource(npc), DdChargeOmen.PackTimeline(row.Flavor, row.Windup, row.StrikeFrames), lockDir + 10f);
            if (omenIndex < 0 || omenIndex >= Main.maxProjectiles) {
                AbortToCooldown(RetryDelay);
                return;
            }
            lastChargeBeat = Main.GameUpdateCount;
            //跺骨蓄势：刹车脉冲即前摇开始（脉冲帧才跟同步）
            npc.velocity.X *= 0.15f;
            npc.netUpdate = true;
            phase = PhaseWindup;
            timer = row.Windup;
        }

        private void TickCharge(NPC npc) {
            DdChargeRow row = ChargeRows[npc.type];
            if (phase == PhaseWindup) {
                timer--;
                //预告实体缺位（弹幕位满等异常）：无预告不冲锋（失败方向=安全方向）
                if (!TryGetBoundOmen(omenIndex, ModContent.ProjectileType<DdChargeOmen>(), npc, 0, out _)) {
                    AbortToCooldown(ChargeCooldown());
                    return;
                }
                if (timer == row.Windup / 2) {
                    //中段再刹一次，压住走位漂移让冲锋起点贴住预告
                    npc.velocity.X *= 0.3f;
                    npc.netUpdate = true;
                }
                if (timer <= 0) {
                    phase = PhaseStrike;
                    timer = 0;
                }
                return;
            }
            if (phase == PhaseStrike) {
                timer++;
                float dirX = lockDir == 0f ? 1f : -1f;
                float env = MobDash.Envelope(timer, row.Rise, row.Hold, row.Decay);
                //包络冲锋：X 轴承诺速度除回提速补偿；Y 轴交还原版重力
                npc.velocity.X = dirX * (row.Peak / MoveGain) * env;
                if (timer == 1 || timer % 6 == 0) {
                    npc.netUpdate = true;
                }
                if (timer >= row.StrikeFrames) {
                    phase = PhaseRecover;
                    timer = row.Recover;
                    npc.netUpdate = true;
                }
                return;
            }
            //力竭：衰减清残速，把控制权干净还给原版 AI（锈系此窗显著更长）
            timer--;
            npc.velocity.X *= 0.82f;
            if (timer % 6 == 0) {
                npc.netUpdate = true;
            }
            if (timer <= 0) {
                omenIndex = -1;
                phase = PhaseIdle;
                cooldown = ChargeCooldown();
            }
        }
        #endregion

        #region Paladin 锤震地 + 举盾格挡
        /// <summary>抡锤起手：锁定近水平锥向→地面锥形 omen（骨刺与虚影同判缺口）→压速前摇</summary>
        private void TryStartHammer(NPC npc, Player player) {
            if (npc.velocity.Y != 0f) {
                cooldown = RetryDelay;
                return;
            }
            float dist = npc.Distance(player.Center);
            if (dist < HammerMinRange || dist > HammerMaxRange || !CanSee(npc, player)) {
                cooldown = RetryDelay;
                return;
            }
            if (CountActive(ModContent.ProjectileType<DdHammerConeOmen>()) >= HammerCap) {
                cooldown = RetryDelay;
                return;
            }

            //锥轴俯仰钳制在近水平带（骨刺带重力，过陡会喂给地板）
            float dirSign = player.Center.X >= npc.Center.X ? 1f : -1f;
            Vector2 to = player.Center - npc.Center;
            float pitch = MathHelper.Clamp(MathF.Atan2(to.Y, Math.Abs(to.X)), -0.55f, 0.15f);
            lockDir = dirSign > 0f ? pitch : MathHelper.Pi - pitch;

            int damage = Math.Max(1, (int)(npc.damage * SpikeDamageFrac));
            omenIndex = Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, Vector2.Zero,
                ModContent.ProjectileType<DdHammerConeOmen>(), damage, 1f, Main.myPlayer,
                lockDir, DdHammerConeOmen.Pack(boundTier - 1, Main.rand.NextBool()), PackSource(npc));
            if (omenIndex < 0 || omenIndex >= Main.maxProjectiles) {
                AbortToCooldown(RetryDelay);
                return;
            }
            npc.velocity.X *= 0.1f;
            npc.netUpdate = true;
            phase = PhaseWindup;
            timer = DdHammerConeOmen.TelegraphFrames;
        }

        private void TickHammer(NPC npc) {
            if (phase == PhaseWindup) {
                timer--;
                if (!TryGetBoundOmen(omenIndex, ModContent.ProjectileType<DdHammerConeOmen>(), npc, 2, out _)) {
                    AbortToCooldown(HammerCooldownByTier[boundTier - 1]);
                    return;
                }
                if (timer % 10 == 0) {
                    //抡锤立定：离散刹车脉冲压住走位（脉冲帧才跟同步）
                    npc.velocity.X *= 0.3f;
                    npc.netUpdate = true;
                }
                if (timer <= 0) {
                    //骨刺由 omen 在提交帧自行发射，本体只收势
                    phase = PhaseRecover;
                    timer = HammerRecoverFrames;
                }
                return;
            }
            timer--;
            if (timer <= 0) {
                omenIndex = -1;
                phase = PhaseIdle;
                cooldown = HammerCooldownByTier[boundTier - 1] + Main.rand.Next(CooldownJitter + 1);
            }
        }

        /// <summary>
        /// 举盾侦测（权威端每帧）：掉血=本帧被打，30% 概率亮盾 90 帧。
        /// 可见盾光与承伤门都读同步的姿态实体（镜像 EliteMove 格挡的可读性，独立实现）
        /// </summary>
        private void TickGuardWatch(NPC npc) {
            bool hurt = npc.life < lifeTracker;
            lifeTracker = npc.life;
            if (blockCooldown > 0) {
                blockCooldown--;
                return;
            }
            if (!hurt || !Eligible(npc)) {
                return;
            }
            if (Main.rand.NextFloat() >= BlockChance) {
                //本次不举盾也吃一小段判定冷却，避免连续掉血逐帧掷骰
                blockCooldown = 20;
                return;
            }
            if (CountActive(ModContent.ProjectileType<DdGuardStanceProj>()) >= GuardCap) {
                blockCooldown = 20;
                return;
            }
            Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, Vector2.Zero,
                ModContent.ProjectileType<DdGuardStanceProj>(), 0, 0f, Main.myPlayer,
                PackSource(npc), GuardStanceFrames);
            blockCooldown = BlockCooldownFrames;
        }
        #endregion

        #region BoneLee 二连段
        /// <summary>压身起手：锁水平向→预告实体（二连段共用，回旋踢重锁一次）→压速前摇</summary>
        private void TryStartBoneLee(NPC npc, Player player) {
            if (npc.velocity.Y != 0f) {
                cooldown = RetryDelay;
                return;
            }
            float dist = npc.Distance(player.Center);
            if (dist < BoneLeeMinRange || dist > BoneLeeMaxRange || !CanSee(npc, player)) {
                cooldown = RetryDelay;
                return;
            }
            if (DdChargeOmen.CountActiveCharges(boneLee: true) >= BoneLeeCap) {
                cooldown = RetryDelay;
                return;
            }

            lockDir = player.Center.X >= npc.Center.X ? 0f : MathHelper.Pi;
            omenIndex = Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, Vector2.Zero,
                ModContent.ProjectileType<DdChargeOmen>(), 0, 0f, Main.myPlayer,
                PackSource(npc), DdChargeOmen.PackTimeline(DdChargeOmen.FlavorBoneLee, 0, 0), lockDir + 10f);
            if (omenIndex < 0 || omenIndex >= Main.maxProjectiles) {
                AbortToCooldown(RetryDelay);
                return;
            }
            npc.velocity.X *= 0.1f;
            npc.netUpdate = true;
            phase = PhaseWindup;
            timer = DdChargeOmen.BLWindupFrames;
        }

        private void TickBoneLee(NPC npc) {
            float dirX = lockDir == 0f ? 1f : -1f;
            switch (phase) {
                case PhaseWindup: {
                    timer--;
                    if (!TryGetBoundOmen(omenIndex, ModContent.ProjectileType<DdChargeOmen>(), npc, 0, out _)) {
                        AbortToCooldown(BoneLeeCooldownByTier[boundTier - 1]);
                        return;
                    }
                    if (timer == DdChargeOmen.BLWindupFrames / 2) {
                        npc.velocity.X *= 0.25f;
                        npc.netUpdate = true;
                    }
                    if (timer <= 0) {
                        phase = PhaseStrike;
                        timer = 0;
                    }
                    return;
                }
                case PhaseStrike: {
                    //突进拳：包络塑形
                    timer++;
                    float env = MobDash.Envelope(timer, 5, 8, 6);
                    npc.velocity.X = dirX * (BLDash1Peak / MoveGain) * env;
                    if (timer == 1 || timer % 6 == 0) {
                        npc.netUpdate = true;
                    }
                    if (timer >= DdChargeOmen.BLDash1Frames) {
                        phase = PhasePause;
                        timer = DdChargeOmen.BLPauseFrames;
                        npc.velocity.X *= 0.3f;
                        npc.netUpdate = true;
                    }
                    return;
                }
                case PhasePause: {
                    //顿帧：拳后停桩
                    timer--;
                    npc.velocity.X *= 0.6f;
                    if (timer > 0) {
                        return;
                    }
                    //回旋踢重锁一次（此后冻结）：新方向写回预告实体一次性同步
                    if (npc.target >= 0 && npc.target < Main.maxPlayers && Main.player[npc.target].Alives()) {
                        lockDir = Main.player[npc.target].Center.X >= npc.Center.X ? 0f : MathHelper.Pi;
                    }
                    if (TryGetBoundOmen(omenIndex, ModContent.ProjectileType<DdChargeOmen>(), npc, 0, out Projectile omen)) {
                        omen.ai[2] = lockDir + 10f;
                        omen.netUpdate = true;
                    }
                    else {
                        //二段前预告缺位：不出第二段（失败方向=安全方向）
                        AbortToCooldown(BoneLeeCooldownByTier[boundTier - 1]);
                        return;
                    }
                    phase = PhaseStrike2;
                    timer = 0;
                    npc.netUpdate = true;
                    return;
                }
                case PhaseStrike2: {
                    //回旋踢：更快更短的第二段
                    timer++;
                    float dir2 = lockDir == 0f ? 1f : -1f;
                    float env = MobDash.Envelope(timer, 4, 8, 6);
                    npc.velocity.X = dir2 * (BLDash2Peak / MoveGain) * env;
                    if (timer == 1 || timer % 6 == 0) {
                        npc.netUpdate = true;
                    }
                    if (timer >= DdChargeOmen.BLDash2Frames) {
                        phase = PhaseRecover;
                        timer = DdChargeOmen.BLRecoverFrames;
                        npc.netUpdate = true;
                    }
                    return;
                }
                default: {
                    //长力竭（惩罚窗）：二连段打空的代价
                    timer--;
                    npc.velocity.X *= 0.85f;
                    if (timer % 6 == 0) {
                        npc.netUpdate = true;
                    }
                    if (timer <= 0) {
                        omenIndex = -1;
                        phase = PhaseIdle;
                        cooldown = BoneLeeCooldownByTier[boundTier - 1] + Main.rand.Next(CooldownJitter + 1);
                    }
                    return;
                }
            }
        }
        #endregion
    }
}
