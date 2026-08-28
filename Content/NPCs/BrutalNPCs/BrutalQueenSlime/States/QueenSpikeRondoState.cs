using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalQueenSlime.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalQueenSlime.Projectiles;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalQueenSlime.States
{
    /// <summary>
    /// 尖刺回旋曲(一阶段主攻)：蹲身→矮弧飞扑→跳顶悬花(尖刺环成形+缺口预告)→齐放外射→急坠落地。
    /// 缺口每次绽放按黄金角轮转，预告环与发射循环读同一常量。
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)QueenSlimeStateIndex.SpikeRondo, typeof(QueenSlimeStateContext))]
    internal class QueenSpikeRondoState : QueenSlimeStateBase
    {
        public override string StateName => "SpikeRondo";
        public override QueenSlimeStateIndex StateIndex => QueenSlimeStateIndex.SpikeRondo;

        #region 节奏与公平常量
        private const int BreathTime = 6;
        private const int CrouchTime = 8;
        /// <summary>绽放悬帧(材质化之外的额外悬停)，预告环寿命与之同源</summary>
        private const int BurstHangExtra = 7;
        /// <summary>缺口轮转步长(黄金角，可预学)</summary>
        private const float GapGoldenStep = 2.399f;
        private const int HardTimeout = 900;
        #endregion

        private int BurstsPlanned(QueenSlimeStateContext ctx) => ctx.IsAsuraMode ? 4 : 3;
        private int SpikesPerBurst(QueenSlimeStateContext ctx) => ctx.IsAsuraMode ? 20 : 16;

        /// <summary>0落地呼吸 1蹲身预备 2腾空(顶点绽放) 3收势</summary>
        private int stage;
        private int stageTimer;
        private int burstsDone;
        private bool apexFired;

        public QueenSpikeRondoState() {
        }

        public override void OnEnter(QueenSlimeStateContext context) {
            base.OnEnter(context);
            NPC npc = context.Npc;
            npc.noGravity = false;
            npc.noTileCollide = false;
            stage = 0;
            stageTimer = 0;
            burstsDone = 0;
            apexFired = false;
        }

        public override IQueenSlimeState OnUpdate(QueenSlimeStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;

            Timer++;
            stageTimer++;

            //超时保险
            if (Timer > HardTimeout && !VaultUtils.isClient) {
                npc.velocity.X = 0f;
                return new QueenBallroomStepState(1);
            }

            switch (stage) {
                case 0://落地呼吸拍
                    DisableContactDamage(npc);
                    if (npc.velocity.Y == 0f) {
                        npc.velocity.X *= 0.75f;
                        FaceTarget(npc, player.Center);
                        if (stageTimer >= BreathTime) {
                            if (burstsDone >= BurstsPlanned(context)) {
                                if (!VaultUtils.isClient) {
                                    return new QueenBallroomStepState(1);
                                }
                                return null;
                            }
                            stage = 1;
                            stageTimer = 0;
                        }
                    }
                    break;

                case 1://蹲身预备(可读前摇)
                    DisableContactDamage(npc);
                    npc.velocity.X *= 0.7f;
                    context.PoseCommand = 3;
                    context.PushSquash(-0.45f * QueenMotion.LateSnap(stageTimer / (float)CrouchTime, 3));
                    if (!VaultUtils.isServer && stageTimer % 3 == 0) {
                        QueenMotion.GelSplashBurst(npc.Bottom, 0.45f, 2);
                    }
                    if (stageTimer >= CrouchTime) {
                        DoLaunch(context);
                        stage = 2;
                        stageTimer = 0;
                        apexFired = false;
                    }
                    break;

                case 2://腾空：顶点绽放尖刺环，随后急坠
                    EnableContactDamageIfFast(npc, 9f);
                    context.PushSquash(0.4f * MathHelper.Clamp(Math.Abs(npc.velocity.Y) / 12f, 0f, 1f));
                    context.AfterimageBoost = Math.Max(context.AfterimageBoost, 0.55f);
                    context.PrismShimmer = Math.Max(context.PrismShimmer, 0.55f);

                    //顶点帧：绽放(缺口预告+悬花成形)
                    if (!apexFired && npc.velocity.Y > -1.2f) {
                        apexFired = true;
                        DoApexBloom(context);
                    }

                    //过顶急坠(灵动的"收爪"落势)
                    if (apexFired && npc.velocity.Y > 0f) {
                        npc.velocity.Y += 0.34f;
                        if (npc.velocity.Y > 15f) {
                            npc.velocity.Y = 15f;
                        }
                        context.PoseCommand = 2;
                    }

                    //足尖闪星
                    if (!VaultUtils.isServer && stageTimer % 3 == 0) {
                        PRTLoader.NewParticle<PRT_Sparkle>(npc.Bottom + Main.rand.NextVector2Circular(14f, 6f),
                            -npc.velocity * 0.06f, Color.White, Main.rand.NextFloat(0.5f, 0.8f))?
                            .Configure(QueenMotion.PrismHue(burstsDone * 0.31f), 14, 0.06f, 1.2f);
                    }

                    //落地
                    if (stageTimer > 10 && (npc.velocity.Y == 0f || npc.collideY)) {
                        DoLanding(context);
                        stage = 0;
                        stageTimer = 0;
                    }
                    //卡地形保险
                    if (stageTimer > 140) {
                        burstsDone++;
                        stage = 0;
                        stageTimer = 0;
                    }
                    break;
            }

            return null;
        }

        /// <summary>矮弧飞扑：横向压低竖向，落点略过玩家身侧</summary>
        private void DoLaunch(QueenSlimeStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;
            float dx = player.Center.X - npc.Center.X;
            int dir = dx >= 0f ? 1 : -1;
            npc.direction = npc.spriteDirection = dir;

            float vx = MathHelper.Clamp(dx * 0.024f, -10.5f, 10.5f) + dir * 3.4f;
            float vy = context.IsAsuraMode ? -12.4f : -11.6f;
            QueenMotion.LaunchHop(npc, vx, vy);
            context.PushSquash(0.55f);
            context.PoseCommand = 1;

            SoundEngine.PlaySound(SoundID.Item154 with { Volume = 0.6f, Pitch = 0.35f + burstsDone * 0.14f, MaxInstances = 3 }, npc.Center);
            QueenMotion.GelSplashBurst(npc.Bottom, 0.85f, 5);
        }

        /// <summary>跳顶绽放：预告环+悬花尖刺(缺口黄金角轮转，与预告同源)</summary>
        private void DoApexBloom(QueenSlimeStateContext context) {
            NPC npc = context.Npc;
            //顶点滞空一瞬(轻微上托，衬托绽放)
            npc.velocity.Y = -0.6f;
            npc.velocity.X *= 0.55f;
            context.PushSquash(0.35f);

            float gapCenter = 1.1f + burstsDone * GapGoldenStep;
            int hangTotal = QueenCrystalSpikeProj.BurstHangTotal(BurstHangExtra);

            if (!VaultUtils.isClient) {
                QueenMotion.SpawnBurstRingOmen(npc, npc.Center, 60f, gapCenter, hangTotal + 4);
                QueenMotion.SpawnSpikeBurst(npc, npc.Center, SpikesPerBurst(context), gapCenter,
                    QueenSpikeOmenProj.BurstGapHalfAngle, BurstHangExtra,
                    QueenCrystalSpikeProj.SpikeDamage, burstsDone * 0.23f);
                npc.netUpdate = true;
            }

            SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.8f, Pitch = 0.4f, MaxInstances = 3 }, npc.Center);
            if (!VaultUtils.isServer) {
                QueenMotion.CrystalShatterBurst(npc.Center, 0.8f, burstsDone * 0.23f, playSound: false);
            }
        }

        /// <summary>急坠落地：重踏+环纹</summary>
        private void DoLanding(QueenSlimeStateContext context) {
            NPC npc = context.Npc;
            burstsDone++;
            context.PushSquash(-0.58f);
            QueenMotion.LandingRingFX(npc.Bottom, 1.15f, burstsDone * 0.3f);
            QueenMotion.Shake(npc.Center, 3.6f, 10, "QueenRondoLand");
            SoundEngine.PlaySound(SoundID.Item167 with { Volume = 0.6f, Pitch = 0.45f, MaxInstances = 3 }, npc.Center);
        }

        public override void OnExit(QueenSlimeStateContext context) {
            base.OnExit(context);
            DisableContactDamage(context.Npc);
        }
    }
}
