using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalKingSlime.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalKingSlime.Projectiles;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalKingSlime.Rendering;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalKingSlime.States
{
    /// <summary>
    /// 质心抛掷(体积形变签名3)：吸聚全身→把六成质量抛向预测落点→核心裸奔(减防受创窗)→质量回流重组。<br/>
    /// 压迫感来自落点预判：出手即锁定(非追踪承诺)，落点标记全程可见；
    /// 核心期与回流期是明码标价的输出奖励窗。P2解锁；服务端决策
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)KingSlimeStateIndex.MassEject, typeof(KingSlimeStateContext))]
    internal class KingSlimeMassEjectState : KingSlimeStateBase
    {
        public override string StateName => "MassEject";
        public override KingSlimeStateIndex StateIndex => KingSlimeStateIndex.MassEject;

        private const int GatherTime = 30;
        /// <summary>核心期看门狗：质心弹自带寿命，正常远早于此爆开</summary>
        private const int CoreMaxFrames = 130;
        private const int ReflowTime = 66;

        //---- 公平阀(契约3)：落点锁定规则 ----
        /// <summary>落点预判提前帧：出手瞬间按玩家横速外推一次，此后落点不再移动</summary>
        private const float ImpactLeadFrames = 18f;
        /// <summary>抛掷距离下限：不许贴脸砸(玩家有横移逃逸空间)</summary>
        private const float MinThrowDistPx = 150f;
        /// <summary>抛掷距离上限</summary>
        private const float MaxThrowDistPx = 950f;
        /// <summary>核心期减防：明码标价的受创加深奖励窗</summary>
        private const int CoreDefenseCut = 10;
        /// <summary>核心期体积</summary>
        private const float CoreScale = 0.55f;

        /// <summary>0吸聚 1核心期(质心在天上) 2回流重组</summary>
        private int phase;
        private int phaseTimer;

        public override void OnEnter(KingSlimeStateContext context) {
            base.OnEnter(context);
            phase = 0;
            phaseTimer = 0;
        }

        public override IKingSlimeState OnUpdate(KingSlimeStateContext context) {
            NPC npc = context.Npc;
            Timer++;
            phaseTimer++;

            switch (phase) {
                case 0: {
                    //吸聚：周身凝胶向体内收拢，身体向上拔起蓄势
                    npc.velocity.X *= 0.7f;
                    context.ContactDamageScale = 0f;
                    float t = phaseTimer / (float)GatherTime;
                    context.VisualSquash = MathHelper.Lerp(context.VisualSquash, 1f + 0.4f * t, 0.3f);
                    context.AuraMode = 1;
                    context.AuraProgress = t;
                    npc.direction = npc.spriteDirection = DirToTarget(context);

                    if (phaseTimer == 2) {
                        SoundEngine.PlaySound(SoundID.Item95 with { Pitch = -0.55f, Volume = 0.8f, MaxInstances = 3 }, npc.Center);
                    }
                    //吸入流：凝珠从外圈飞向体心
                    if (!VaultUtils.isServer && phaseTimer % 2 == 0) {
                        Vector2 from = npc.Center + Main.rand.NextVector2Unit() * Main.rand.NextFloat(90f, 190f);
                        Vector2 vel = (npc.Center - from) * 0.09f;
                        PRTLoader.NewParticle<PRT_BKSGelBead>(from, vel,
                            KingSlimeGelFX.GelMid * 0.75f, Main.rand.NextFloat(0.6f, 1.1f))?.Configure(16);
                    }

                    if (phaseTimer >= GatherTime && Grounded(npc)) {
                        phase = 1;
                        phaseTimer = 0;
                    }
                    break;
                }
                case 1: {
                    //核心期：出手帧抛质心+反冲，随后缩成裸核，减防奖励窗
                    if (phaseTimer == 1) {
                        LaunchMassGlob(context);
                    }

                    npc.velocity.X *= 0.92f;
                    context.ScaleMul = MathHelper.Lerp(context.ScaleMul, CoreScale, 0.18f);
                    context.ContactDamageScale = 0f;
                    //沸腾金环标记受创窗
                    context.AuraMode = 2;
                    context.AuraProgress = 0.55f;
                    npc.defense = Math.Max(0, npc.defDefense - CoreDefenseCut);

                    if (!VaultUtils.isServer && phaseTimer % 8 == 0) {
                        KingSlimeGelFX.BubbleFizz(npc.Bottom - new Vector2(0f, 10f), npc.width * 0.35f, 1);
                    }

                    //质心爆开(或异常缺失)→回流
                    bool globGone = FindMassGlob(context) == null && phaseTimer > 12;
                    if (globGone || phaseTimer >= CoreMaxFrames) {
                        phase = 2;
                        phaseTimer = 0;
                    }
                    break;
                }
                case 2: {
                    //回流重组：质量顺着回流波爬回来，体积渐涨；受创窗持续到重组完成
                    npc.velocity.X *= 0.9f;
                    context.ContactDamageScale = 0f;
                    float t = phaseTimer / (float)ReflowTime;
                    context.ScaleMul = MathHelper.Lerp(CoreScale, 1f, VaultUtils.EaseOutQuad(t));
                    context.AuraMode = 2;
                    context.AuraProgress = 0.55f * (1f - t);
                    npc.defense = Math.Max(0, npc.defDefense - CoreDefenseCut);

                    //重组收尾：弹性回胀
                    if (phaseTimer == ReflowTime - 8) {
                        context.StretchImpulse(0.3f);
                        KingSlimeGelFX.SquishSound(npc.Bottom, -0.1f, 0.9f);
                        if (!VaultUtils.isServer) {
                            KingSlimeGelFX.GelSplatter(npc.Center, -Vector2.UnitY, 8, 4f, 1f);
                        }
                    }

                    if (phaseTimer >= ReflowTime && !VaultUtils.isClient) {
                        return BackToHop(context);
                    }
                    break;
                }
            }

            //看门狗
            if (Timer > 300 && !VaultUtils.isClient) {
                return BackToHop(context);
            }

            return null;
        }

        /// <summary>
        /// 出手帧：服务端解落点并生成质心弹，各端做反冲表现。<br/>
        /// 落点=玩家横速外推一次后垂扫地表，出手即锁死(公平阀：预告即承诺)
        /// </summary>
        private void LaunchMassGlob(KingSlimeStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;
            int dir = DirToTarget(context);

            //反冲：质量守恒的后坐(服务端定速，netUpdate广播)
            if (!VaultUtils.isClient) {
                LaunchHop(npc, -dir * 6.2f, -5.4f);
            }
            context.SquashVelocity -= 0.22f;
            SoundEngine.PlaySound(SoundID.Splash with { Pitch = -0.5f, Volume = 1.1f, MaxInstances = 2 }, npc.Center);
            SoundEngine.PlaySound(SoundID.Item167 with { Pitch = -0.4f, Volume = 0.55f, MaxInstances = 2 }, npc.Center);
            KingSlimeGelFX.CameraPunch(npc.Center, 5f, 10, "BKSMassEject", new Vector2(-dir, -0.4f));
            if (!VaultUtils.isServer) {
                KingSlimeGelFX.GelSplatter(npc.Top, new Vector2(dir, -1.2f), 10, 7f, 1.2f);
            }

            if (VaultUtils.isClient) {
                return;
            }

            //落点：横速外推+距离夹紧+垂扫地表
            float rawX = player.Center.X + player.velocity.X * ImpactLeadFrames;
            float dx = MathHelper.Clamp(rawX - npc.Center.X, -MaxThrowDistPx, MaxThrowDistPx);
            if (Math.Abs(dx) < MinThrowDistPx) {
                dx = Math.Sign(dx == 0f ? dir : dx) * MinThrowDistPx;
            }
            Vector2 impact = KingSlimeGelFX.FindGroundBelow(new Vector2(npc.Center.X + dx, player.Center.Y - 40f));

            //抛物线一次性解出：飞行帧数与重力为质心弹常量，落点即承诺
            Vector2 spawn = npc.Top - new Vector2(0f, 10f);
            float frames = BKSMassGlobProj.FlightFrames;
            Vector2 vel = new Vector2(
                (impact.X - spawn.X) / frames,
                (impact.Y - spawn.Y) / frames - 0.5f * BKSMassGlobProj.Gravity * frames);

            int dmg = (int)(npc.defDamage * (context.IsAsuraMode ? 0.7f : 0.6f));
            Projectile.NewProjectile(npc.GetSource_FromAI(), spawn, vel,
                ModContent.ProjectileType<BKSMassGlobProj>(), dmg, 0f, Main.myPlayer,
                npc.whoAmI, impact.X, impact.Y);
        }

        /// <summary>各端自寻质心弹(以宿主索引配对)，免额外同步</summary>
        private static Projectile FindMassGlob(KingSlimeStateContext context) {
            int type = ModContent.ProjectileType<BKSMassGlobProj>();
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.type == type && (int)proj.ai[0] == context.Npc.whoAmI) {
                    return proj;
                }
            }
            return null;
        }

        public override void OnExit(KingSlimeStateContext context) {
            base.OnExit(context);
            context.Npc.defense = context.Npc.defDefense;
        }
    }
}
