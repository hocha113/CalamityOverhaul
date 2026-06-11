using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMechanicalEye.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMechanicalEye.States.Retinazer;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMechanicalEye.States.Spazmatism;
using CalamityOverhaul.Content.PRTTypes;
using CalamityOverhaul.Content.Projectiles.Boss.MechanicalEye;
using InnoVault.PRT;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMechanicalEye.States.Common
{
    /// <summary>
    /// 双子大招·剪刀死光：
    /// 任一眼血量低于阈值后解锁。双眼飞至高空对角远端，以电弧对接成"铰链"蓄力，
    /// 随后各自释放持续死亡射线(魔焰眼烈焰/激光眼死光)，由外向内夹剪扫过战场，
    /// 玩家须跟随收缩的安全缝走位。全程预警充分、收招硬直明显
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)TwinsStateIndex.TwinsScissorRay, typeof(TwinsStateContext))]
    internal class TwinsScissorRayState : TwinsStateBase
    {
        public override string StateName => "TwinsScissorRay";
        public override TwinsStateIndex StateIndex => TwinsStateIndex.TwinsScissorRay;

        private int PositionPhase => Context.IsDeathMode ? 56 : 66;
        private int LockPhase => Context.IsDeathMode ? 60 : 70;
        private int SweepPhase => Context.IsDeathMode ? 135 : 155;
        private const int RecoveryPhase = 42;
        private const int MaxPartnerWait = 120;

        private int TotalDuration => PositionPhase + LockPhase + SweepPhase + RecoveryPhase;

        /// <summary>
        /// 射线起始角(相对正下方向外偏)与结束角(向内收剪越过中线)
        /// </summary>
        private const float StartSpread = 0.92f;
        private const float EndSpread = -0.85f;

        private TwinsStateContext Context;
        private int comboStep;
        private int partnerWait;
        private Vector2 anchorPos;
        private bool anchorLocked;
        private bool rayFired;
        private bool hingeSpawned;

        public TwinsScissorRayState() : this(0) {
        }

        public TwinsScissorRayState(int currentComboStep) {
            comboStep = currentComboStep;
        }

        public override void OnEnter(TwinsStateContext context) {
            base.OnEnter(context);
            Context = context;
            partnerWait = 0;
            anchorLocked = false;
            rayFired = false;
            hingeSpawned = false;
        }

        public override ITwinsState OnUpdate(TwinsStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;

            //搭档失效→退出(射线与电弧会自行收束)
            NPC partner = TwinsStateContext.GetPartnerNpc(npc.type);
            if (!partner.Alives()) {
                TwinsStateContext.ClearComboSignal();
                return GetExitState();
            }

            Timer++;

            if (Timer <= PositionPhase) {
                ExecutePositionPhase(npc, player);

                //就位末尾标记就绪，等待双方都就位再同拍开剪
                if (Timer == PositionPhase) {
                    TwinsStateContext.MarkComboReady(context.IsSpazmatism);
                    if (!TwinsStateContext.BothComboReady && partnerWait < MaxPartnerWait) {
                        Timer--;
                        partnerWait++;
                    }
                }
            }
            else if (Timer <= PositionPhase + LockPhase) {
                ExecuteLockPhase(npc, player, partner);
            }
            else if (Timer <= PositionPhase + LockPhase + SweepPhase) {
                ExecuteSweepPhase(npc, player);
            }
            else {
                ExecuteRecoveryPhase(npc, player);
            }

            if (Timer >= TotalDuration) {
                return GetExitState();
            }

            return null;
        }

        /// <summary>
        /// 就位阶段：双眼弹簧飞往玩家上方两翼远端
        /// </summary>
        private void ExecutePositionPhase(NPC npc, Player player) {
            float progress = Timer / (float)PositionPhase;

            float side = Context.IsSpazmatism ? -1f : 1f;
            Vector2 targetPos = player.Center + new Vector2(side * 680f, -300f);
            TwinsMotion.SpringHover(npc, targetPos, 0.024f, 0.105f, 38f);
            FaceTarget(npc, player.Center);
            Context.PushDashVisuals(0.4f * progress, 0.5f * progress);

            Context.SetChargeState(13, progress * 0.3f);

            if (Timer == 1 && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.45f, Volume = 1.3f }, npc.Center);
            }
        }

        /// <summary>
        /// 锁定蓄力阶段：电弧铰链对接，眼体颤抖蓄能，缓慢转向起始射角
        /// </summary>
        private void ExecuteLockPhase(NPC npc, Player player, NPC partner) {
            int phaseTimer = Timer - PositionPhase;
            float progress = phaseTimer / (float)LockPhase;

            //锁定世界坐标锚点(不再追踪玩家——给走位留出决策空间)
            if (!anchorLocked) {
                anchorLocked = true;
                float side = Context.IsSpazmatism ? -1f : 1f;
                anchorPos = player.Center + new Vector2(side * 680f, -300f);
            }

            TwinsMotion.SpringHover(npc, anchorPos, 0.05f, 0.2f);
            if (progress > 0.5f && !VaultUtils.isServer) {
                npc.position += Main.rand.NextVector2Circular(2.2f, 2.2f) * progress;
            }

            //电弧铰链(由魔焰眼生成，纯演出张力，伤害线远在玩家头顶上方)
            if (!hingeSpawned) {
                hingeSpawned = true;
                if (Context.IsSpazmatism && !VaultUtils.isClient) {
                    Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, Vector2.Zero,
                        ModContent.ProjectileType<TwinsTetherArc>(), 40, 0f, Main.myPlayer,
                        npc.whoAmI, partner.whoAmI, LockPhase + SweepPhase / 2);
                }
            }

            //缓慢压向起始射角(向外下方)
            float side2 = Context.IsSpazmatism ? 1f : -1f;
            float startAngle = MathHelper.PiOver2 + side2 * StartSpread;
            TwinsMotion.RotateToward(npc, startAngle, 0.045f);

            Context.SetChargeState(13, 0.3f + progress * 0.7f);

            //蓄能内聚与节拍震动
            if (phaseTimer % 2 == 0) {
                TwinsMotion.ChargeGatherFX(npc.Center, Context.IsSpazmatism, progress, 120f);
            }
            if (phaseTimer % 18 == 0 && !VaultUtils.isServer) {
                TwinsMotion.Shake(npc.Center, 2.5f + progress * 2f, 8);
            }

            //蓄力完成预告
            if (phaseTimer == LockPhase - 4 && !VaultUtils.isServer) {
                Color themeColor = Context.IsSpazmatism ? TwinsMotion.SpazColor : TwinsMotion.RetinColor;
                PRTLoader.NewParticle<PRT_DWave>(npc.Center, Vector2.Zero, themeColor, 0.2f)?
                    .Configure(Vector2.One, 0f, 1.1f, 14);
                SoundEngine.PlaySound(SoundID.Item15 with { Pitch = 0.6f, Volume = 1f }, npc.Center);
            }
        }

        /// <summary>
        /// 夹剪扫射阶段：双射线由外向内缓动闭合，越过中线完成"剪切"
        /// </summary>
        private void ExecuteSweepPhase(NPC npc, Player player) {
            int phaseTimer = Timer - PositionPhase - LockPhase;
            float progress = phaseTimer / (float)SweepPhase;

            Context.ResetChargeState();

            //锁定悬停在锚点
            TwinsMotion.SpringHover(npc, anchorPos, 0.05f, 0.22f);

            //发射死亡射线(各自一道，主题色区分)
            if (!rayFired) {
                rayFired = true;
                if (!VaultUtils.isClient) {
                    int damage = Context.IsDeathMode ? 56 : 50;
                    Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, Vector2.Zero,
                        ModContent.ProjectileType<RetinazerDeathRay>(), damage, 0f, Main.myPlayer,
                        npc.whoAmI, SweepPhase, Context.IsSpazmatism ? 1f : 0f);
                    //开火瞬间的屏幕扭曲冲击波
                    Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, Vector2.Zero,
                        ModContent.ProjectileType<TwinsSupernovaBlast>(), 0, 0f, Main.myPlayer,
                        1f, Context.IsSpazmatism ? 1f : 0f);
                }
                if (!VaultUtils.isServer) {
                    TwinsMotion.Shake(npc.Center, 7f, 16);
                }
            }

            //缓动扫射:由外向内夹剪，起末速度低、中段加速
            float side = Context.IsSpazmatism ? 1f : -1f;
            float eased = CWRUtils.EaseInOutQuad(progress);
            float currentSpread = MathHelper.Lerp(StartSpread, EndSpread, eased);
            float targetAngle = MathHelper.PiOver2 + side * currentSpread;
            //rotation直接驱动(射线锚定读取npc.rotation)
            npc.rotation = targetAngle - MathHelper.PiOver2;

            //扫射期间的持续震感与排气火花
            if (phaseTimer % 12 == 0 && !VaultUtils.isServer) {
                TwinsMotion.Shake(npc.Center, 2f, 6);
            }
            if (!VaultUtils.isServer && phaseTimer % 3 == 0) {
                Vector2 rayDir = (npc.rotation + MathHelper.PiOver2).ToRotationVector2();
                PRTLoader.NewParticle<PRT_TwinsSpark>(npc.Center - rayDir * 30f + Main.rand.NextVector2Circular(14, 14),
                    -rayDir * Main.rand.NextFloat(2f, 5f), Color.White, Main.rand.NextFloat(1f, 1.6f))?
                    .Configure(15, Context.IsSpazmatism ? 1 : 0);
            }
        }

        /// <summary>
        /// 收招阶段：射线收束后的明显硬直——眼体下沉排气，给予输出窗口
        /// </summary>
        private void ExecuteRecoveryPhase(NPC npc, Player player) {
            int phaseTimer = Timer - PositionPhase - LockPhase - SweepPhase;

            //疲惫下沉
            npc.velocity *= 0.9f;
            npc.velocity.Y += 0.12f;
            FaceTarget(npc, player.Center);

            //过热排气
            if (!VaultUtils.isServer && phaseTimer % 5 == 0) {
                Color themeColor = Context.IsSpazmatism ? TwinsMotion.SpazColor : TwinsMotion.RetinColor;
                PRTLoader.NewParticle<PRT_Smoke>(npc.Center + Main.rand.NextVector2Circular(24, 24),
                    new Vector2(0, -1.5f) + Main.rand.NextVector2Circular(0.8f, 0.8f),
                    themeColor * 0.5f, Main.rand.NextFloat(0.7f, 1.1f))?.Configure(36, 0.5f, 0.02f, false, 0f);
            }
        }

        /// <summary>
        /// 退出状态：返回各自二阶段锚点
        /// </summary>
        private ITwinsState GetExitState() {
            if (Context.IsSpazmatism) {
                return new SpazmatismFlameChaseState(comboStep);
            }
            return new RetinazerVerticalBarrageState(comboStep);
        }

        public override void OnExit(TwinsStateContext context) {
            base.OnExit(context);
            TwinsStateContext.ClearComboSignal();
        }
    }
}
