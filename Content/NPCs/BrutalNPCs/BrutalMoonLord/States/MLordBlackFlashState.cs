using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMoonLord.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMoonLord.Projectiles;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMoonLord.Rendering;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMoonLord.States
{
    /// <summary>
    /// 黑闪（终局大招）：出场三路，开幕拍（核心裸露仪式直接接棒）+ 裸露出招表每轮压轴席 + 残血底牌保底。
    /// 四条幻影臂物化→合拢环抱→揉搓压缩黑球（打断窗：集火核心可令其失手）→
    /// 一拍寂静锁定掷向→撕空掷出→黑洞钉在锚点坍缩（预告环即爆点判定）→黑闪爆点→虚空创口余波，
    /// 本体全程长硬直。全程清场先行、预告超长、掷向锁定即承诺。
    /// 半血以下为残血变体：重震屏 + 失控膨胀巨球 + 更快掷速 + 更大爆点。
    /// Timer 各端本地推进；打断分支经 OvBlackFlashBeat 槽广播，各端跳至失手段
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)MLordStateIndex.BlackFlash, typeof(MLordContext))]
    internal class MLordBlackFlashState : MLordStateBase
    {
        public override string StateName => "BlackFlash";
        public override MLordStateIndex StateIndex => MLordStateIndex.BlackFlash;

        //―――― 时间轴（Timer 帧）――――
        internal const int ManifestEnd = 66;
        internal const int EmbraceEnd = ManifestEnd + 40;
        internal const int KneadEnd = EmbraceEnd + 150;
        /// <summary>掷向预读线提前量：揉搓末段起亮出追踪细线（读向），寂静拍锁定后转为承诺线</summary>
        internal const int AimLeadFrames = 44;
        /// <summary>寂静一拍：粒子/运动/声全部收干，掷向已锁</summary>
        internal const int SilenceEnd = KneadEnd + 18;
        internal const int ThrowEnd = SilenceEnd + 12;
        /// <summary>余波硬直：覆盖弹体撕空飞行+坍缩+爆点（约 100 帧）之后再留 ~50 帧惩罚窗</summary>
        internal const int AftermathEnd = ThrowEnd + 150;
        //―――― 失手段（打断分支）：远离主时间轴的独立区段，各端经 beat 槽跳入 ――――
        internal const int FumbleStart = 10000;
        internal const int FumbleEnd = FumbleStart + 128;

        /// <summary>揉搓打断窗起点的核心生命（服务端失血审计基线）</summary>
        private int kneadStartLife;

        /// <summary>残血变体（半血分界，各端可确定性复判：状态槽与生命值同包同步）。
        /// 重震屏 + 失控膨胀巨球（2 倍体量）+ 更快掷速 + 更大爆点</summary>
        private bool desperate;

        /// <summary>本拍是否底牌发（解锁线下出手）：只有它记账/失手重试，压轴席常规发不记</summary>
        private bool trumpCard;

        /// <summary>本拍出手初速（残血变体更快，预告即承诺的锁定语法不变）</summary>
        private float LaunchSpeedNow => desperate
            ? MLordBlackHoleProj.DesperateLaunchSpeed : MLordBlackHoleProj.LaunchSpeed;

        public override void OnEnter(MLordContext context) {
            base.OnEnter(context);
            kneadStartLife = 0;
            desperate = context.Npc.life < context.Npc.lifeMax * 0.5f;
            trumpCard = context.Npc.life < context.Npc.lifeMax * MLordDirector.BlackFlashLifeRatio;
            if (!VaultUtils.isClient) {
                if (trumpCard) {
                    context.Owner.ai[MLordAiSlots.OvBlackFlashUsed] = MLordBlackFlashFlags.With(
                        context.Owner.ai[MLordAiSlots.OvBlackFlashUsed], MLordBlackFlashFlags.Desperate);
                }
                context.Owner.ai[MLordAiSlots.OvBlackFlashBeat] = 0f;
                context.Owner.ai[MLordAiSlots.OvEyeCommand] = MLordEyeCommand.Retreat;
                context.Owner.ai[MLordAiSlots.OvAttackSeed] = Main.rand.Next(1, 100000);
                ClearHostileStage();
                context.Npc.netUpdate = true;
            }
            if (!VaultUtils.isServer) {
                //序拍：披风后传出的扭曲低吼，宣告演出开场
                SoundEngine.PlaySound(SoundID.Zombie96 with { Volume = 1.2f, Pitch = -0.9f }, context.Npc.Center);
            }
        }

        /// <summary>演出级大招开场清场：撤掉全部己方敌对弹幕，独占舞台（契约3.5，月明湮灭复用）</summary>
        internal static void ClearHostileStage() {
            foreach (Projectile p in Main.ActiveProjectiles) {
                bool mine = p.type == ModContent.ProjectileType<MLordScanRayProj>()
                    || p.type == ModContent.ProjectileType<MLordArcRayProj>()
                    || p.type == ModContent.ProjectileType<MLordEyeLinkProj>()
                    || p.type == ModContent.ProjectileType<MLordCometProj>()
                    || p.type == ModContent.ProjectileType<MLordOrbProj>()
                    || p.type == ModContent.ProjectileType<MLordStarfireProj>()
                    || p.type == ModContent.ProjectileType<MLordGravityWellProj>()
                    || p.type == ModContent.ProjectileType<MLordBoltProj>()
                    || p.type == ModContent.ProjectileType<MLordAnnihilationRayProj>()
                    || p.type == ProjectileID.PhantasmalBolt
                    || p.type == ProjectileID.PhantasmalEye
                    || p.type == ProjectileID.PhantasmalSphere;
                if (mine) {
                    p.Kill();
                }
            }
        }

        public override void OnExit(MLordContext context) {
            base.OnExit(context);
            if (!VaultUtils.isClient) {
                //底牌发失手不消耗：清回未用位，重试门线（OvBlackFlashRearm）已在打断帧写入。
                //常规压轴席失手不做任何补偿，下一轮循环照旧会来，不会滚成即时重试的打断刷子
                if (Timer >= FumbleStart && trumpCard) {
                    context.Owner.ai[MLordAiSlots.OvBlackFlashUsed] = MLordBlackFlashFlags.Without(
                        context.Owner.ai[MLordAiSlots.OvBlackFlashUsed], MLordBlackFlashFlags.Desperate);
                }
                context.Owner.ai[MLordAiSlots.OvEyeCommand] = MLordEyeCommand.Solo;
                context.Owner.ai[MLordAiSlots.OvBlackFlashBeat] = 0f;
                context.Npc.netUpdate = true;
            }
        }

        public override IMLordState OnUpdate(MLordContext context) {
            NPC npc = context.Npc;
            Player target = context.Target;

            context.EclipseDrive = 1f;
            context.HoldAllParts = Timer <= ThrowEnd || Timer >= FumbleStart;

            //打断分支广播：各端一旦读到 beat=1 就跳入失手段（服务端写槽时已同步）
            if (context.Owner.ai[MLordAiSlots.OvBlackFlashBeat] == 1f && Timer < FumbleStart) {
                EnterFumble(context);
            }

            if (Timer >= FumbleStart) {
                UpdateFumble(context);
            }
            else if (Timer < KneadEnd) {
                UpdateWindup(context, target);
            }
            else if (Timer < SilenceEnd) {
                UpdateSilence(context);
            }
            else if (Timer < ThrowEnd) {
                UpdateThrow(context);
            }
            else {
                //余波：长硬直大惩罚窗
                context.StaggerVulnerable = true;
                context.HeartExposure = 1f;
                npc.velocity *= 0.9f;
            }

            //黑球世界坐标与幻影臂驱动（客户端表现，全量由 Timer+同步槽确定性推导）
            if (!VaultUtils.isServer) {
                MLordUltArms.Drive(npc, BuildArmDrive(context));
            }

            Timer++;
            if (Timer >= AftermathEnd && Timer < FumbleStart) {
                return NextAttack(context);
            }
            if (Timer >= FumbleEnd) {
                return NextAttack(context);
            }
            return null;
        }

        #region 阶段推进

        /// <summary>物化+环抱+揉搓：定桩蓄势，打断窗审计，蓄力语法全套</summary>
        private void UpdateWindup(MLordContext context, Player target) {
            NPC npc = context.Npc;
            //仪式悬滞：四条黑臂全数征去搓球，本体没有肢体可移动，只是渐渐停死
            npc.velocity *= 0.93f;
            UpdateLean(context);
            context.SetChargeState(Timer / (float)KneadEnd);

            //失血基线各端镜像（客户端供打断进度红纹演出，服务端供裁定）
            if (Timer == EmbraceEnd) {
                kneadStartLife = npc.life;
            }
            //揉搓打断窗（服务端裁定）：窗内核心失血超阈值→失手；
            //底牌发同时写重试门线=当前血线再降一档（底牌被打断→更低血量孤注一掷）
            if (!VaultUtils.isClient && Timer > EmbraceEnd && kneadStartLife > 0
                && kneadStartLife - npc.life >= npc.lifeMax * MLordDirector.BlackFlashBreakRatio) {
                context.Owner.ai[MLordAiSlots.OvBlackFlashBeat] = 1f;
                if (trumpCard) {
                    context.Owner.ai[MLordAiSlots.OvBlackFlashRearm] = MathHelper.Clamp(
                        npc.life / (float)npc.lifeMax - MLordDirector.BlackFlashRearmStep, 0.02f, 1f);
                }
                npc.netUpdate = true;
            }

            //掷向锁定：寂静拍前最后一刻取玩家位（预告即承诺，此后不再追踪）
            if (!VaultUtils.isClient && Timer == KneadEnd - 1) {
                context.Owner.ai[MLordAiSlots.OvAnchorX] = target.Center.X;
                context.Owner.ai[MLordAiSlots.OvAnchorY] = target.Center.Y;
                npc.netUpdate = true;
            }
            if (VaultUtils.isServer) {
                return;
            }

            //―――― 以下客户端表现 ――――
            float charge = Timer / (float)KneadEnd;
            MLordScreenEffects.PushGravityDim(BallCenter(npc), charge * 0.9f);
            //残血底牌拍：蓄力全程中幅持续震屏随蓄力渐强（寂静拍骤停，反差压出爆发）
            if (desperate) {
                Main.LocalPlayer.CWR()?.GetScreenShake(2.5f + charge * 3.5f);
            }

            if (Timer == ManifestEnd) {
                //合拢启动的低吼
                SoundEngine.PlaySound(SoundID.Zombie104 with { Volume = 0.8f, Pitch = -0.85f }, npc.Center);
            }
            if (Timer == EmbraceEnd) {
                //黑球诞生
                SoundEngine.PlaySound(CWRSound.BlackHole with { Volume = 0.95f, Pitch = -0.35f }, npc.Center);
                MLordScreenFX.Punch(npc.Center, 4f, 10);
            }
            //揉搓升调节拍：30f 固定周期，音调爬升+震屏渐强（玩家可内化的倒计时）
            if (Timer > EmbraceEnd && (Timer - EmbraceEnd) % 30 == 0) {
                int beat = (Timer - EmbraceEnd) / 30;
                SoundEngine.PlaySound(SoundID.Item15 with { Volume = 0.85f, Pitch = -0.5f + beat * 0.18f }, npc.Center);
                MLordScreenFX.Punch(npc.Center, 1.5f + beat * 0.8f, 8);
            }
        }

        /// <summary>寂静一拍：一切收干。无声、无粒子、无移动，爆发前的黑</summary>
        private void UpdateSilence(MLordContext context) {
            NPC npc = context.Npc;
            npc.velocity *= 0.78f;
            context.SetChargeState(1f);

            if (VaultUtils.isServer) {
                return;
            }
            if (Timer == KneadEnd) {
                //吸气：所有声音的截断由这一声短促的收干标出
                SoundEngine.PlaySound(SoundID.MaxMana with { Volume = 0.7f, Pitch = -0.8f }, npc.Center);
            }
            //寂静期不推 GravityDim：上一帧的余晖自然衰减，画面亮度回抬一拍再暗
        }

        /// <summary>掷出：服务端生成黑洞弹体，客户端冲击帧+反冲。
        /// 弹体自此撕空直奔锚点钉住坍缩，后续演出全在弹体侧（<see cref="MLordBlackHoleProj"/>）</summary>
        private void UpdateThrow(MLordContext context) {
            NPC npc = context.Npc;
            npc.velocity *= 0.86f;

            if (Timer == SilenceEnd) {
                Vector2 anchor = new(context.Owner.ai[MLordAiSlots.OvAnchorX],
                    context.Owner.ai[MLordAiSlots.OvAnchorY]);
                Vector2 ballPos = BallCenter(npc);
                Vector2 dir = (anchor - ballPos).SafeNormalize(Vector2.UnitY);

                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(CWRSound.BlackHole with { Volume = 1.1f, Pitch = 0.25f }, ballPos);
                    SoundEngine.PlaySound(SoundID.Zombie104 with { Volume = 1f, Pitch = -0.3f }, ballPos);
                    //撕空的鞭响：出手瞬间的高频刮擦，和后面爆点的低频轰鸣拉开频段
                    SoundEngine.PlaySound(SoundID.Item71 with { Volume = 0.9f, Pitch = -0.4f }, ballPos);
                    //残血变体掷出更重：方向冲击加深并叠一记余震
                    MLordScreenFX.Punch(ballPos, desperate ? 14f : 11f, 16, dir);
                    if (desperate) {
                        Main.LocalPlayer.CWR()?.GetScreenShake(8f);
                    }
                    //掷出反冲的红黑星尘
                    MLordScreenFX.StarBurst(ballPos, 1.2f, 10);
                }
                if (!VaultUtils.isClient) {
                    int damage = ScaleDamage(context, MLordDirector.BlackHoleContactDamage);
                    Projectile.NewProjectile(npc.GetSource_FromAI(), ballPos,
                        dir * LaunchSpeedNow,
                        ModContent.ProjectileType<MLordBlackHoleProj>(), damage, 0f, Main.myPlayer,
                        anchor.X, anchor.Y, desperate ? 1f : 0f);
                }
            }
        }

        /// <summary>
        /// 各端跳入失手段：黑球在掌中提前引爆——失手也是一场炸点演出
        /// （全屏冲击帧+冲击环+碎星，无伤害判定），玩家清楚知道自己打断了它
        /// </summary>
        private void EnterFumble(MLordContext context) {
            Timer = FumbleStart;
            if (VaultUtils.isServer) {
                return;
            }
            NPC npc = context.Npc;
            Vector2 ballPos = BallCenter(npc);
            SoundEngine.PlaySound(SoundID.Item62 with { Volume = 1f, Pitch = -0.55f }, ballPos);
            SoundEngine.PlaySound(SoundID.NPCDeath59 with { Volume = 0.7f, Pitch = -0.3f }, ballPos);
            SoundEngine.PlaySound(SoundID.Item122 with { Volume = 1f, Pitch = -0.6f }, ballPos);
            MLordScreenFX.Punch(ballPos, 11f, 16);
            //掌中提前引爆：黑闪冲击帧 + 无伤冲击环 + 红黑碎星四溅
            MLordBlackFlashFX.PushFlash(ballPos);
            MLordScreenEffects.PushStarRing(ballPos, 1f, 780f, 32);
            MLordScreenFX.StarBurst(ballPos, 1.9f, 26);
        }

        /// <summary>失手段：主动打断的奖励，超长硬直+受击加伤，比正常余波更痛</summary>
        private void UpdateFumble(MLordContext context) {
            NPC npc = context.Npc;
            npc.velocity *= 0.88f;
            context.StaggerVulnerable = true;
            context.HeartExposure = 1f;
            context.ResetChargeState();
        }

        #endregion

        #region 幻影臂驱动构建

        /// <summary>黑球锚位：核心胸前下方，四掌合抱的几何中心</summary>
        internal static Vector2 BallCenter(NPC npc) {
            return npc.Center + new Vector2(0f, 150f).RotatedBy(npc.rotation);
        }

        /// <summary>把 Timer 翻译成幻影臂驱动包（确定性，各端一致）</summary>
        private MLordUltArmDrive BuildArmDrive(MLordContext context) {
            NPC npc = context.Npc;
            MLordUltArmDrive d = new() {
                Seed = (int)context.Owner.ai[MLordAiSlots.OvAttackSeed],
                BallCenter = BallCenter(npc),
                ThrowDir = ThrowDirNow(context),
                Anchor = AnchorNow(context),
                AimLine = AimLineNow(),
                AimLocked = Timer >= KneadEnd,
                RingRadius = desperate
                    ? MLordBlackHoleProj.DesperateDetonationRadius : MLordBlackHoleProj.DetonationRadius,
            };

            if (Timer >= FumbleStart) {
                float t = (Timer - FumbleStart) / (float)(FumbleEnd - FumbleStart);
                d.Phase = MLordUltArmPhase.Fumble;
                d.PhaseT = MathHelper.Clamp(t, 0f, 1f);
                d.BallVisible = 0f;
                return d;
            }
            if (Timer < ManifestEnd) {
                d.Phase = MLordUltArmPhase.Manifest;
                d.PhaseT = Timer / (float)ManifestEnd;
                d.BallVisible = 0f;
                return d;
            }
            if (Timer < EmbraceEnd) {
                float t = (Timer - ManifestEnd) / (float)(EmbraceEnd - ManifestEnd);
                d.Phase = MLordUltArmPhase.Embrace;
                d.PhaseT = t;
                d.BallRadius = MathHelper.Lerp(8f, 124f, VaultUtils.EaseOutCubic(t));
                d.BallVisible = MathHelper.Clamp(t * 3f, 0f, 1f);
                return d;
            }
            if (Timer < KneadEnd) {
                float t = (Timer - EmbraceEnd) / (float)(KneadEnd - EmbraceEnd);
                d.Phase = MLordUltArmPhase.Knead;
                d.PhaseT = t;
                if (desperate) {
                    //残血底牌拍：失控膨胀，越搓越大、脉动越强（孤注一掷把一切灌进球里）
                    float swell = 9f * t * (float)Math.Sin(Timer * 0.37f);
                    d.BallRadius = MathHelper.Lerp(124f, 168f, t) + swell;
                }
                else {
                    //开幕拍：压缩中带阻抗脉动，球在抵抗，越压越小、脉动越弱
                    float pulse = 7f * (1f - t) * (float)Math.Sin(Timer * 0.37f);
                    d.BallRadius = MathHelper.Lerp(124f, 64f, t) + pulse;
                }
                d.BallVisible = 1f;
                d.Collapse = t * 0.85f;
                //打断进度：失血占阈值比例，红纹随之加剧（把打断博弈做成可见的语言）
                d.BreakCharge = kneadStartLife > 0
                    ? MathHelper.Clamp((kneadStartLife - npc.life)
                        / (npc.lifeMax * MLordDirector.BlackFlashBreakRatio), 0f, 1f)
                    : 0f;
                return d;
            }
            if (Timer < SilenceEnd) {
                float t = (Timer - KneadEnd) / (float)(SilenceEnd - KneadEnd);
                d.Phase = MLordUltArmPhase.Silence;
                d.PhaseT = t;
                //残血巨球寂静拍轻收一档锁定，开幕拍延续压缩收束
                d.BallRadius = desperate ? MathHelper.Lerp(168f, 160f, t) : MathHelper.Lerp(64f, 56f, t);
                d.BallVisible = 1f;
                d.Collapse = 0.85f + 0.15f * t;
                return d;
            }
            if (Timer < ThrowEnd) {
                float t = (Timer - SilenceEnd) / (float)(ThrowEnd - SilenceEnd);
                d.Phase = MLordUltArmPhase.Throw;
                d.PhaseT = t;
                d.BallRadius = desperate ? 160f : 56f;
                //交棒：弹体已生成，手中球沿掷向外推并快速隐去（覆盖生成包延迟的几帧）
                d.BallCenter += d.ThrowDir * (LaunchSpeedNow * (Timer - SilenceEnd));
                d.BallVisible = MathHelper.Clamp(1f - t * 2.4f, 0f, 1f);
                d.Collapse = 1f;
                return d;
            }
            float at = (Timer - ThrowEnd) / (float)(AftermathEnd - ThrowEnd);
            d.Phase = MLordUltArmPhase.Aftermath;
            d.PhaseT = MathHelper.Clamp(at, 0f, 1f);
            d.BallVisible = 0f;
            return d;
        }

        /// <summary>当前掷向：锁定前指向玩家（预备读向），锁定后指向锚点（承诺）</summary>
        private Vector2 ThrowDirNow(MLordContext context) {
            return (AnchorNow(context) - BallCenter(context.Npc)).SafeNormalize(Vector2.UnitY);
        }

        /// <summary>当前锚点：锁定前跟玩家（预读），锁定后读同步槽（承诺，弹体钉住坍缩的爆心）</summary>
        private Vector2 AnchorNow(MLordContext context) {
            if (Timer >= KneadEnd) {
                return new Vector2(context.Owner.ai[MLordAiSlots.OvAnchorX],
                    context.Owner.ai[MLordAiSlots.OvAnchorY]);
            }
            return context.Target.Center;
        }

        /// <summary>掷向线强度：揉搓末段 <see cref="AimLeadFrames"/> 帧内渐亮到 0.5（追踪预读），
        /// 寂静拍满亮（锁定承诺），掷出拍随球出手迅速收线</summary>
        private float AimLineNow() {
            if (Timer >= FumbleStart) {
                return 0f;
            }
            if (Timer < KneadEnd) {
                int lead = Timer - (KneadEnd - AimLeadFrames);
                return lead <= 0 ? 0f : 0.5f * MathHelper.Clamp(lead / (float)AimLeadFrames, 0f, 1f);
            }
            if (Timer < SilenceEnd) {
                return 1f;
            }
            if (Timer < ThrowEnd) {
                return 1f - (Timer - SilenceEnd) / (float)(ThrowEnd - SilenceEnd);
            }
            return 0f;
        }

        #endregion
    }
}
