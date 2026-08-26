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
    /// 黑闪（终局大招，一场一次，比虚空撕裂更迟解锁）：
    /// 四条幻影臂物化→合拢环抱→揉搓压缩黑球（打断窗：集火核心可令其失手）→
    /// 一拍寂静锁定掷向→掷出黑洞→长硬直余波。
    /// 全程清场先行、预告超长、掷向锁定即承诺。
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
        /// <summary>寂静一拍：粒子/运动/声全部收干，掷向已锁</summary>
        internal const int SilenceEnd = KneadEnd + 18;
        internal const int ThrowEnd = SilenceEnd + 12;
        internal const int AftermathEnd = ThrowEnd + 96;
        //―――― 失手段（打断分支）：远离主时间轴的独立区段，各端经 beat 槽跳入 ――――
        internal const int FumbleStart = 10000;
        internal const int FumbleEnd = FumbleStart + 128;

        /// <summary>揉搓打断窗起点的核心生命（服务端失血审计基线）</summary>
        private int kneadStartLife;

        public override void OnEnter(MLordContext context) {
            base.OnEnter(context);
            kneadStartLife = 0;
            if (!VaultUtils.isClient) {
                context.Owner.ai[MLordAiSlots.OvBlackFlashUsed] = 1f;
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
                //失手不消耗底牌：清回未用标记，重试门线（OvBlackFlashRearm）已在打断帧写入
                if (Timer >= FumbleStart) {
                    context.Owner.ai[MLordAiSlots.OvBlackFlashUsed] = 0f;
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
            //同时写重试门线=当前血线再降一档（底牌被打断→更低血量孤注一掷）
            if (!VaultUtils.isClient && Timer > EmbraceEnd && kneadStartLife > 0
                && kneadStartLife - npc.life >= npc.lifeMax * MLordDirector.BlackFlashBreakRatio) {
                context.Owner.ai[MLordAiSlots.OvBlackFlashBeat] = 1f;
                context.Owner.ai[MLordAiSlots.OvBlackFlashRearm] = MathHelper.Clamp(
                    npc.life / (float)npc.lifeMax - MLordDirector.BlackFlashRearmStep, 0.02f, 1f);
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

        /// <summary>掷出：服务端生成黑洞弹体，客户端冲击帧+反冲</summary>
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
                    MLordScreenFX.Punch(ballPos, 11f, 16, dir);
                    //掷出反冲的红黑星尘
                    MLordScreenFX.StarBurst(ballPos, 1.2f, 10);
                }
                if (!VaultUtils.isClient) {
                    int damage = ScaleDamage(context, MLordDirector.BlackHoleContactDamage);
                    Projectile.NewProjectile(npc.GetSource_FromAI(), ballPos,
                        dir * MLordBlackHoleProj.LaunchSpeed,
                        ModContent.ProjectileType<MLordBlackHoleProj>(), damage, 0f, Main.myPlayer,
                        anchor.X, anchor.Y);
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
                //压缩中带阻抗脉动：球在抵抗，越压越小、脉动越弱
                float pulse = 7f * (1f - t) * (float)Math.Sin(Timer * 0.37f);
                d.BallRadius = MathHelper.Lerp(124f, 64f, t) + pulse;
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
                d.BallRadius = MathHelper.Lerp(64f, 56f, t);
                d.BallVisible = 1f;
                d.Collapse = 0.85f + 0.15f * t;
                return d;
            }
            if (Timer < ThrowEnd) {
                float t = (Timer - SilenceEnd) / (float)(ThrowEnd - SilenceEnd);
                d.Phase = MLordUltArmPhase.Throw;
                d.PhaseT = t;
                d.BallRadius = 56f;
                //交棒：弹体已生成，手中球沿掷向外推并快速隐去（覆盖生成包延迟的几帧）
                d.BallCenter += d.ThrowDir * (MLordBlackHoleProj.LaunchSpeed * (Timer - SilenceEnd));
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
            NPC npc = context.Npc;
            if (Timer >= KneadEnd) {
                Vector2 anchor = new(context.Owner.ai[MLordAiSlots.OvAnchorX],
                    context.Owner.ai[MLordAiSlots.OvAnchorY]);
                return (anchor - BallCenter(npc)).SafeNormalize(Vector2.UnitY);
            }
            return DirectionToTarget(context);
        }

        #endregion
    }
}
