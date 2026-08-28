using CalamityOverhaul.Content.GameModes.BrutalMobs.EliteMove.Projectiles;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.EliteMove
{
    /// <summary>
    /// 困难精英「读招与惩罚」行为层：不接管原版 AI，只做叠加式单体战技。
    /// 决策全在服务端/单人侧；客户端可见状态一律来自已同步的预告弹幕实体，
    /// 实体每帧向本类的镜像字段盖戳，命中门与绘制只读镜像（各端自洽）
    /// </summary>
    internal class EliteMoveNPC : GlobalNPC
    {
        public override bool InstancePerEntity => true;

        public override bool AppliesToEntity(NPC entity, bool lateInstantiation)
            => EliteMoveSets.FamilyOf(entity.type) != EliteFamily.None;

        //——公平性契约常量（发射与判定循环直接读取）——
        /// <summary>相位凝形预告帧，期间可见成形且无杀伤（契约 ≥40）</summary>
        internal const int PhaseCondenseFrames = 42;
        /// <summary>相位淡出帧</summary>
        internal const int PhaseFadeFrames = 26;
        /// <summary>凝形完成后的突刺减益窗口帧</summary>
        internal const int PhaseLungeWindow = 18;
        /// <summary>格挡起手帧：武装前被打不触发反击（预告 ≥30）</summary>
        internal const int StanceArmFrames = 30;
        /// <summary>反击出手前的闪光帧（反应余量）</summary>
        internal const int CounterFlashFrames = 8;
        /// <summary>跃击落点跟踪帧（印记随玩家）</summary>
        internal const int LeapTrackFrames = 20;
        /// <summary>跃击锁定帧（印记冻结，位移投入必然有效）</summary>
        internal const int LeapLockFrames = 18;
        /// <summary>散射瞄准跟踪帧</summary>
        internal const int ScatterTrackFrames = 12;
        /// <summary>散射锁定帧（跟踪+锁定=30 帧预告）</summary>
        internal const int ScatterLockFrames = 18;
        /// <summary>散射扇面槽位数</summary>
        internal const int FanSlots = 6;
        /// <summary>恒定缺口槽位：发射循环跳过此槽=可学习的安全巷</summary>
        internal const int GapSlot = 2;
        /// <summary>散射半张角（弧度）</summary>
        internal const float SpreadHalfAngle = 0.52f;
        /// <summary>散射弹伤害相对 npc.damage（已缩放值）的比例</summary>
        internal const float ScatterDamageMult = 0.5f;
        /// <summary>每个家族的特殊攻击全局并发上限</summary>
        internal const int FamilyConcurrentCap = 6;
        /// <summary>格挡姿态期间的承伤保留系数（档位2+更硬）</summary>
        internal const float GuardKeepT1 = 0.5f;
        internal const float GuardKeepT2 = 0.4f;
        /// <summary>跃击弹道解算用重力（原版战士类 AI 的每帧重力）</summary>
        internal const float LeapGravity = 0.3f;

        private int boundTier;
        private EliteFamily family;
        private EliteProfile profile;

        //——服务端决策私产（客户端不读）——
        private bool initialized;
        private byte phase;
        private int timer;
        private int cooldown;
        private int lifeTracker;
        private uint lastHurtTick;
        private uint hookHurtTick;
        private bool counterUsed;
        private Vector2 lockedPoint;
        private float lockedAngle;
        private float dashVX;
        private int flightFrames;
        private int boundProjIndex = -1;

        //——镜像字段：由已同步的预告弹幕每帧盖戳，各端一致——
        internal uint holdStillUntil;
        internal float holdStillFactor;
        internal bool holdStillBothAxes;
        internal uint stanceVisibleUntil;
        internal uint phaseHarmlessUntil;
        internal float phaseAlpha = 1f;
        internal uint phaseAlphaStampTick;
        internal uint leapFlightUntil;
        internal uint lungeWindowUntil;
        internal uint decoyGlowUntil;

        /// <summary>模式提速补偿：GameModeNPC.PostAI 会按档位追加位置推进，
        /// 所有注入速度除以该系数，保证实际轨迹与预告承诺一致</summary>
        private float MoveGain => 1f + GameModeTuning.SpeedBonus(boundTier);

        private int TierCooldown() => (int)(profile.Cooldown * (boundTier >= 3 ? 0.7f : boundTier >= 2 ? 0.85f : 1f));

        public override void SetDefaults(NPC npc) {
            family = EliteFamily.None;
            boundTier = 0;
            int tier = GameModeSystem.EffectiveTier;
            if (tier <= 0) {
                return;
            }
            if (!EliteMoveSets.Profiles.TryGetValue(npc.type, out profile)) {
                return;
            }
            boundTier = tier;
            family = profile.Family;
        }

        /// <summary>机制触发资格：每次入口都过（雕像怪/友方/体节/Boss 不给机制）</summary>
        private static bool MechanicEligible(NPC npc) {
            if (npc.friendly || npc.townNPC || npc.immortal || npc.dontTakeDamage || npc.boss) {
                return false;
            }
            if (npc.lifeMax <= 5 || npc.damage <= 0) {
                return false;
            }
            if (npc.SpawnedFromStatue || npc.realLife >= 0) {
                return false;
            }
            return npc.HasValidTarget;
        }

        private static bool Grounded(NPC npc) => npc.velocity.Y == 0f;

        private bool TargetWithin(NPC npc, float min, float max) {
            Player target = Main.player[npc.target];
            if (!target.Alives()) {
                return false;
            }
            float dist = npc.Distance(target.Center);
            return dist >= min && dist <= max;
        }

        /// <summary>家族并发计数：数活着的预告实体，自愈无漂移（仅触发时调用）</summary>
        private static int CountActive(int projType) {
            int count = 0;
            for (int i = 0; i < Main.maxProjectiles; i++) {
                if (Main.projectile[i].active && Main.projectile[i].type == projType) {
                    count++;
                }
            }
            return count;
        }

        /// <summary>校验自己名下的预告弹幕仍然有效（防槽位复用）</summary>
        private bool TryGetBoundProj(int projType, int npcIndex, out Projectile proj) {
            proj = null;
            if (boundProjIndex < 0 || boundProjIndex >= Main.maxProjectiles) {
                return false;
            }
            Projectile p = Main.projectile[boundProjIndex];
            if (!p.active || p.type != projType || (int)p.ai[0] != npcIndex) {
                return false;
            }
            proj = p;
            return true;
        }

        #region 镜像盖戳（供预告弹幕在所有端调用）
        internal void StampHold(float factor, bool bothAxes) {
            holdStillUntil = Main.GameUpdateCount + 2;
            holdStillFactor = factor;
            holdStillBothAxes = bothAxes;
        }

        internal void StampStance() => stanceVisibleUntil = Main.GameUpdateCount + 2;

        internal void StampPhase(float alpha, bool harmless) {
            phaseAlpha = alpha;
            phaseAlphaStampTick = Main.GameUpdateCount;
            if (harmless) {
                phaseHarmlessUntil = Main.GameUpdateCount + 2;
            }
        }

        internal void StampLeapFlight() => leapFlightUntil = Main.GameUpdateCount + 2;

        internal void StampLungeWindow() => lungeWindowUntil = Main.GameUpdateCount + 2;

        internal void StampDecoyGlow() => decoyGlowUntil = Main.GameUpdateCount + 2;
        #endregion

        private float PhaseAlphaFresh()
            => Main.GameUpdateCount - phaseAlphaStampTick <= 3 ? phaseAlpha : 1f;

        private bool StanceFresh => Main.GameUpdateCount < stanceVisibleUntil;

        public override void PostAI(NPC npc) {
            if (family == EliteFamily.None) {
                return;
            }
            uint now = Main.GameUpdateCount;

            //镜像定身：蹲伏/拉弓/凝形期间所有端同样压速度，模拟一致不橡皮筋
            if (now < holdStillUntil) {
                npc.velocity.X *= holdStillFactor;
                if (holdStillBothAxes) {
                    npc.velocity.Y *= holdStillFactor;
                }
            }

            if (VaultUtils.isClient) {
                return;
            }

            if (!initialized) {
                initialized = true;
                //错拍初始冷却，避免同屏群体同帧触发；首发窗封顶 180 帧（M7 密度预算），
                //长冷却只管后续节奏，不许把首次亮相也拖走
                int firstBase = Math.Min(profile.Cooldown / 2, 120);
                cooldown = firstBase + npc.whoAmI * 37 % 61;
                lifeTracker = npc.life;
            }

            //受击侦测（服务端）：打击包只改生命值，掉血=本帧被打
            bool hurtThisFrame = npc.life < lifeTracker;
            if (hurtThisFrame) {
                lastHurtTick = now;
            }
            lifeTracker = npc.life;

            switch (family) {
                case EliteFamily.Parry:
                    ServerParry(npc, now, hurtThisFrame);
                    break;
                case EliteFamily.Leap:
                    ServerLeap(npc);
                    break;
                case EliteFamily.Phase:
                    ServerPhase(npc);
                    break;
                case EliteFamily.Decoy:
                    ServerDecoy(npc);
                    break;
                case EliteFamily.Scatter:
                    ServerScatter(npc);
                    break;
            }
        }

        #region 家族状态机（服务端/单人）
        /// <summary>格挡反击：0 待机 / 1 亮架势 / 2 反击闪光 / 3 突进</summary>
        private void ServerParry(NPC npc, uint now, bool hurtThisFrame) {
            switch (phase) {
                case 0:
                    if (--cooldown > 0) {
                        return;
                    }
                    cooldown = 0;
                    //反应式触发：近 90 帧内挨过打才亮架势（教学=见光停手）。
                    //只受过伤才可能触发，顺带保证宝箱怪不会在伪装期亮姿态
                    if (!MechanicEligible(npc) || now - lastHurtTick > 90 || !TargetWithin(npc, 0f, profile.Range)) {
                        return;
                    }
                    if (CountActive(ModContent.ProjectileType<EMParryStanceProj>()) >= FamilyConcurrentCap) {
                        cooldown = 45;
                        return;
                    }
                    boundProjIndex = Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, Vector2.Zero,
                        ModContent.ProjectileType<EMParryStanceProj>(), 0, 0f, Main.myPlayer,
                        npc.whoAmI, profile.Aux, npc.type);
                    phase = 1;
                    timer = 0;
                    counterUsed = false;
                    break;
                case 1: {
                    timer++;
                    //姿态实体缺位（弹幕满额生成失败等异常）：无预告不许反击，回正冷却（失败方向=安全方向）
                    if (!TryGetBoundProj(ModContent.ProjectileType<EMParryStanceProj>(), npc.whoAmI, out Projectile stance)) {
                        phase = 0;
                        cooldown = TierCooldown();
                        return;
                    }
                    //单人侧要求受击钩子戳记（排除持续伤害误触发）；联机服务端钩子不到场，以掉血为准
                    bool hookFresh = now - hookHurtTick <= 2;
                    bool hitConfirm = hurtThisFrame && (hookFresh || !VaultUtils.isSinglePlayer);
                    if (!counterUsed && timer >= StanceArmFrames && hitConfirm) {
                        counterUsed = true;
                        //锁定惩罚目标：优先最近交互玩家；此刻定死方向，出手后不再重瞄
                        int idx = npc.lastInteraction >= 0 && npc.lastInteraction < Main.maxPlayers
                            && Main.player[npc.lastInteraction].Alives() ? npc.lastInteraction : npc.target;
                        lockedPoint = Main.player[idx].Center;
                        stance.ai[1] = -1f;    //转入反击闪光段
                        stance.netUpdate = true;
                        phase = 2;
                        timer = 0;
                    }
                    else if (timer >= StanceArmFrames + profile.Aux) {
                        phase = 0;
                        cooldown = TierCooldown();
                    }
                    break;
                }
                case 2:
                    if (++timer >= CounterFlashFrames) {
                        Vector2 dir = (lockedPoint - npc.Center).SafeNormalize(Vector2.UnitX * npc.direction);
                        float gain = MoveGain;
                        npc.velocity = new Vector2(dir.X * profile.Power / gain,
                            dir.Y * profile.Power * 0.45f / gain - 2.2f / gain);
                        dashVX = npc.velocity.X;
                        npc.netUpdate = true;
                        phase = 3;
                        timer = 0;
                    }
                    break;
                case 3:
                    timer++;
                    npc.velocity.X = dashVX;    //抵住原版 AI 的横向衰减，保持突进直线
                    if (timer % 6 == 0) {
                        npc.netUpdate = true;
                    }
                    if (timer >= 20) {
                        phase = 0;
                        cooldown = TierCooldown();
                    }
                    break;
            }
        }

        /// <summary>二段跃击：0 待机 / 1 一段小扑 / 2 印记跟踪 / 3 锁定蹲伏 / 4 大跳飞行</summary>
        private void ServerLeap(NPC npc) {
            switch (phase) {
                case 0:
                    if (--cooldown > 0) {
                        return;
                    }
                    cooldown = 0;
                    if (!MechanicEligible(npc) || !Grounded(npc) || !TargetWithin(npc, 200f, profile.Range)) {
                        return;
                    }
                    if (CountActive(ModContent.ProjectileType<EMLeapMarkerProj>()) >= FamilyConcurrentCap) {
                        cooldown = 45;
                        return;
                    } {
                        //一段跃：朝向目标的小前扑，量级同原版跳跃，属机动不属攻击
                        float dirX = Main.player[npc.target].Center.X > npc.Center.X ? 1f : -1f;
                        float gain = MoveGain;
                        npc.velocity = new Vector2(dirX * profile.Power * 0.75f / gain, -7.2f / gain);
                        npc.netUpdate = true;
                    }
                    phase = 1;
                    timer = 0;
                    break;
                case 1:
                    timer++;
                    if (timer > 8 && Grounded(npc)) {
                        if (!MechanicEligible(npc)) {
                            phase = 0;
                            cooldown = TierCooldown();
                            return;
                        }
                        boundProjIndex = Projectile.NewProjectile(npc.GetSource_FromAI(),
                            Main.player[npc.target].Center, Vector2.Zero,
                            ModContent.ProjectileType<EMLeapMarkerProj>(), 0, 0f, Main.myPlayer,
                            npc.whoAmI, 0f, npc.type);
                        phase = 2;
                        timer = 0;
                    }
                    else if (timer > 100) {
                        phase = 0;
                        cooldown = TierCooldown() / 2;
                    }
                    break;
                case 2:
                    timer++;
                    if (!TryGetBoundProj(ModContent.ProjectileType<EMLeapMarkerProj>(), npc.whoAmI, out Projectile marker)) {
                        phase = 0;
                        cooldown = TierCooldown();
                        return;
                    }
                    if (timer >= LeapTrackFrames) {
                        //锁定：印记冻结在当前位置并同步，此后跃击不再重瞄
                        marker.ai[1] = 1f;
                        marker.netUpdate = true;
                        lockedPoint = marker.Center;
                        phase = 3;
                        timer = 0;
                    }
                    break;
                case 3:
                    if (++timer >= LeapLockFrames) {
                        //弹道解算：位移含模式提速推进，除以增益保证落点兑现承诺
                        Vector2 d = lockedPoint - npc.Bottom;
                        float gain = MoveGain;
                        int t = (int)MathHelper.Clamp(MathF.Abs(d.X) / 9f, 16f, 30f);
                        flightFrames = t;
                        float vx = MathHelper.Clamp(d.X / (gain * t), -13f, 13f);
                        float vy = MathHelper.Clamp(d.Y / (gain * t) - LeapGravity * (t - 1) / 2f, -16f, 4f);
                        npc.velocity = new Vector2(vx, vy);
                        dashVX = vx;
                        npc.netUpdate = true;
                        phase = 4;
                        timer = 0;
                    }
                    break;
                case 4:
                    timer++;
                    npc.velocity.X = dashVX;    //飞行期抵住原版空中转向，保持弹道
                    if (timer % 10 == 0) {
                        npc.netUpdate = true;
                    }
                    if ((timer > 6 && Grounded(npc)) || timer > flightFrames + 26) {
                        if (TryGetBoundProj(ModContent.ProjectileType<EMLeapMarkerProj>(), npc.whoAmI, out Projectile m)) {
                            m.Kill();    //服务端击杀同步，落地尘在各端 OnKill 播放
                        }
                        phase = 0;
                        cooldown = TierCooldown();
                    }
                    break;
            }
        }

        /// <summary>相位隐现：单计时器对齐锚实体的固定时间表（淡出→隐没→凝形→突刺）</summary>
        private void ServerPhase(NPC npc) {
            if (phase == 0) {
                if (--cooldown > 0) {
                    return;
                }
                cooldown = 0;
                if (!MechanicEligible(npc) || !TargetWithin(npc, 260f, profile.Range)
                    || (profile.NeedsWet && !npc.wet)) {
                    return;
                }
                if (CountActive(ModContent.ProjectileType<EMPhaseAnchorProj>()) >= FamilyConcurrentCap) {
                    cooldown = 45;
                    return;
                }
                boundProjIndex = Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, Vector2.Zero,
                    ModContent.ProjectileType<EMPhaseAnchorProj>(), 0, 0f, Main.myPlayer,
                    npc.whoAmI, profile.Aux, npc.type);
                phase = 1;
                timer = 0;
                return;
            }

            timer++;
            if (!TryGetBoundProj(ModContent.ProjectileType<EMPhaseAnchorProj>(), npc.whoAmI, out _)) {
                //锚失效立即回正：镜像过期后自动恢复可见可伤（失败方向=安全方向）
                phase = 0;
                cooldown = TierCooldown();
                return;
            }
            if (timer == PhaseFadeFrames + profile.Aux + PhaseCondenseFrames) {
                //凝形完成：完全可见后才出突刺，属可见敌人的机动爆发
                Player target = Main.player[npc.target];
                if (target.Alives()) {
                    npc.velocity = npc.DirectionTo(target.Center) * (profile.Power / MoveGain);
                    npc.netUpdate = true;
                }
                phase = 0;
                cooldown = TierCooldown();
            }
        }

        /// <summary>残影惑真：一次性放出残影分身，真体由镜像加亮</summary>
        private void ServerDecoy(NPC npc) {
            if (--cooldown > 0) {
                return;
            }
            cooldown = 0;
            if (!MechanicEligible(npc) || !TargetWithin(npc, 240f, profile.Range)) {
                return;
            }
            int count = boundTier >= 3 ? 3 : 2;
            if (CountActive(ModContent.ProjectileType<EMDecoyProj>()) + count > FamilyConcurrentCap) {
                cooldown = 60;
                return;
            }
            Vector2 baseVel = npc.velocity.Length() < 3f
                ? npc.DirectionTo(Main.player[npc.target].Center) * 3f : npc.velocity;
            for (int i = 0; i < count; i++) {
                float side = i % 2 == 0 ? 1f : -1f;
                Vector2 perp = baseVel.SafeNormalize(Vector2.UnitX).RotatedBy(MathHelper.PiOver2) * side;
                Projectile.NewProjectile(npc.GetSource_FromAI(),
                    npc.Center + perp * (38f + 20f * (i / 2)), baseVel.RotatedBy(0.55f * side),
                    ModContent.ProjectileType<EMDecoyProj>(), 0, 0f, Main.myPlayer,
                    npc.whoAmI, npc.type, 0f);
            }
            cooldown = TierCooldown();
        }

        /// <summary>具名缺口散射：0 待机 / 1 预告扇面（跟踪→锁定→齐射）</summary>
        private void ServerScatter(NPC npc) {
            switch (phase) {
                case 0:
                    if (--cooldown > 0) {
                        return;
                    }
                    cooldown = 0;
                    if (!MechanicEligible(npc) || !TargetWithin(npc, 240f, profile.Range)) {
                        return;
                    } {
                        Player target = Main.player[npc.target];
                        if (!Collision.CanHitLine(npc.Center, 1, 1, target.Center, 1, 1)) {
                            cooldown = 30;
                            return;
                        }
                    }
                    if (CountActive(ModContent.ProjectileType<EMScatterTelegraphProj>()) >= FamilyConcurrentCap) {
                        cooldown = 45;
                        return;
                    }
                    //ai1=999 表示未锁定（合法角度绝不会到这个量级）
                    boundProjIndex = Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, Vector2.Zero,
                        ModContent.ProjectileType<EMScatterTelegraphProj>(), 0, 0f, Main.myPlayer,
                        npc.whoAmI, 999f, profile.Style);
                    phase = 1;
                    timer = 0;
                    break;
                case 1: {
                    timer++;
                    bool teleValid = TryGetBoundProj(ModContent.ProjectileType<EMScatterTelegraphProj>(), npc.whoAmI, out Projectile tele);
                    if (!teleValid && timer <= ScatterTrackFrames + ScatterLockFrames) {
                        phase = 0;
                        cooldown = TierCooldown();
                        return;
                    }
                    if (timer == ScatterTrackFrames) {
                        //锁定瞄角写入 ai1 并一次性同步，各端预告从此冻结
                        Player target = Main.player[npc.target];
                        lockedAngle = target.Alives()
                            ? (target.Center - npc.Center).ToRotation() : npc.direction > 0 ? 0f : MathHelper.Pi;
                        tele.ai[1] = lockedAngle;
                        tele.netUpdate = true;
                    }
                    if (timer == ScatterTrackFrames + ScatterLockFrames) {
                        FireVolley(npc, lockedAngle);
                        if (boundTier < 3) {
                            phase = 0;
                            cooldown = TierCooldown();
                        }
                    }
                    else if (boundTier >= 3 && timer == ScatterTrackFrames + ScatterLockFrames + 12) {
                        //毁灭档二连射：同角同缺口，安全巷承诺不变
                        FireVolley(npc, lockedAngle);
                        phase = 0;
                        cooldown = TierCooldown();
                    }
                    break;
                }
            }
        }

        /// <summary>沿锁定角发出扇面，GapSlot 恒定跳过（缺口即安全巷）</summary>
        private void FireVolley(NPC npc, float centerAngle) {
            int damage = (int)(npc.damage * ScatterDamageMult);
            for (int i = 0; i < FanSlots; i++) {
                if (i == GapSlot) {
                    continue;
                }
                float angle = centerAngle + SpreadHalfAngle * (-1f + 2f * i / (FanSlots - 1));
                Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center,
                    angle.ToRotationVector2() * profile.Power,
                    ModContent.ProjectileType<EMScatterBoltProj>(), damage, 1f, Main.myPlayer,
                    profile.Style);
            }
        }
        #endregion

        #region 命中门与减益（读镜像，各端一致）
        public override bool CanHitPlayer(NPC npc, Player target, ref int cooldownSlot) {
            //相位无害窗：隐没与凝形期间接触无伤（伤害窗口=可见窗口）
            if (family == EliteFamily.Phase && Main.GameUpdateCount < phaseHarmlessUntil) {
                return false;
            }
            return true;
        }

        public override bool? CanBeHitByItem(NPC npc, Player player, Item item) {
            if (family == EliteFamily.Phase && PhaseAlphaFresh() < 0.35f) {
                return false;
            }
            return null;
        }

        public override bool? CanBeHitByProjectile(NPC npc, Projectile projectile) {
            if (family == EliteFamily.Phase && PhaseAlphaFresh() < 0.35f) {
                return false;
            }
            return null;
        }

        public override void ModifyHitByItem(NPC npc, Player player, Item item, ref NPC.HitModifiers modifiers) {
            if (family == EliteFamily.Parry && StanceFresh) {
                modifiers.FinalDamage *= boundTier >= 2 ? GuardKeepT2 : GuardKeepT1;
            }
        }

        public override void ModifyHitByProjectile(NPC npc, Projectile projectile, ref NPC.HitModifiers modifiers) {
            if (family == EliteFamily.Parry && StanceFresh) {
                modifiers.FinalDamage *= boundTier >= 2 ? GuardKeepT2 : GuardKeepT1;
            }
        }

        public override void OnHitByItem(NPC npc, Player player, Item item, NPC.HitInfo hit, int damageDone) {
            if (family == EliteFamily.Parry) {
                hookHurtTick = Main.GameUpdateCount;    //单人侧精确受击戳记
            }
        }

        public override void OnHitByProjectile(NPC npc, Projectile projectile, NPC.HitInfo hit, int damageDone) {
            if (family == EliteFamily.Parry) {
                hookHurtTick = Main.GameUpdateCount;
            }
        }

        public override void OnHitPlayer(NPC npc, Player target, Player.HurtInfo hurtInfo) {
            if (profile.HitBuff <= 0) {
                return;
            }
            uint now = Main.GameUpdateCount;
            //减益只在攻击窗口挂：跃击飞行段 / 相位突刺段（受击方本机结算，原生同步）
            bool window = family == EliteFamily.Leap ? now < leapFlightUntil
                : family == EliteFamily.Phase && now < lungeWindowUntil;
            if (window) {
                target.AddBuff(profile.HitBuff, profile.HitBuffTime);
            }
        }
        #endregion

        #region 绘制（读镜像）
        public override bool PreDraw(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            if (family != EliteFamily.Phase) {
                return true;
            }
            float alpha = PhaseAlphaFresh();
            if (alpha >= 0.98f) {
                return true;
            }
            if (alpha <= 0.03f) {
                return false;    //全隐：不画（此刻镜像同样关闭了接触伤害）
            }
            Main.instance.LoadNPC(npc.type);
            Texture2D tex = TextureAssets.Npc[npc.type].Value;
            Vector2 pos = npc.Center - screenPos + new Vector2(0f, npc.gfxOffY);
            SpriteEffects effects = npc.spriteDirection == 1 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
            //渐隐向主题色偏移：读作相位化而非单纯透明
            Color ghost = Color.Lerp(drawColor, profile.Tint, 0.35f * (1f - alpha));
            spriteBatch.Draw(tex, pos, npc.frame, ghost * alpha, npc.rotation,
                npc.frame.Size() / 2f, npc.scale, effects, 0f);
            return false;
        }

        public override void PostDraw(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            //残影在场时真体加亮脉冲：更亮=真身的可读性阀门
            if (family != EliteFamily.Decoy || Main.GameUpdateCount >= decoyGlowUntil) {
                return;
            }
            Texture2D glow = CWRAsset.SoftGlow.Value;
            float pulse = 0.75f + 0.25f * MathF.Sin(Main.GlobalTimeWrappedHourly * 9f + npc.whoAmI);
            Color halo = profile.Tint with { A = 0 } * (0.55f * pulse);
            spriteBatch.Draw(glow, npc.Center - screenPos, null, halo, 0f,
                glow.Size() / 2f, npc.width / 36f + 0.8f, SpriteEffects.None, 0f);
        }
        #endregion
    }
}
