using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalGolem.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalGolem.Projectiles;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalGolem.States.Fists
{
    /// <summary>锚点跟随：弹簧滞后，收新指令分发</summary>
    [InnoVault.StateMachines.VaultState((int)GolemFistStateIndex.Anchor, typeof(GolemFistStateContext))]
    internal class GolemFistAnchorState : GolemFistStateBase
    {
        public override string StateName => "FistAnchor";
        public override GolemFistStateIndex StateIndex => GolemFistStateIndex.Anchor;

        public override IGolemFistState OnUpdate(GolemFistStateContext ctx) {
            SpringToAnchor(ctx);

            //指令分发（服务端裁决）
            if (!VaultUtils.isClient) {
                int seq = (int)ctx.Owner.ai[GolemAiSlots.FistCmdSeq];
                if (seq != ctx.LastCmdSeq) {
                    ctx.LastCmdSeq = seq;
                    switch (ctx.CmdKind) {
                        case GolemFistCommand.StraightPunch:
                        case GolemFistCommand.HookSwing:
                        case GolemFistCommand.LowSweep:
                            return new GolemFistWindupState();
                        case GolemFistCommand.GuardOrbit: {
                            //过期护卫令丢弃：仅躯干仍处仪式态时生效
                            GolemStateIndex bodyState = GolemFacts.GetStateIndex(ctx.Body);
                            if (bodyState is GolemStateIndex.SolarOverdrive or GolemStateIndex.HeadDetach
                                or GolemStateIndex.MeteorLeap or GolemStateIndex.Intro) {
                                return new GolemFistGuardState();
                            }
                            break;
                        }
                        case GolemFistCommand.DeathFall:
                            return new GolemFistDeathFallState();
                    }
                }
            }
            return null;
        }
    }

    /// <summary>出拳蓄力：反向后拉的吸气 + 汇聚尘</summary>
    [InnoVault.StateMachines.VaultState((int)GolemFistStateIndex.Windup, typeof(GolemFistStateContext))]
    internal class GolemFistWindupState : GolemFistStateBase
    {
        public override string StateName => "FistWindup";
        public override GolemFistStateIndex StateIndex => GolemFistStateIndex.Windup;

        public override IGolemFistState OnUpdate(GolemFistStateContext ctx) {
            NPC npc = ctx.Npc;
            int windup = ctx.CmdWindup;
            float t = MathHelper.Clamp(Timer / (float)windup, 0f, 1f);
            ctx.WindupGlow = t;

            Vector2 anchor = GolemFacts.FistAnchor(ctx.Body, ctx.Side);
            //低位横扫：蓄力期先滑到对侧起扫位（贴地高度），预告扫掠线
            if (ctx.CmdKind == GolemFistCommand.LowSweep) {
                float startX = ctx.Owner.ai[GolemAiSlots.FistSweepStartX];
                if (startX == 0f && ctx.Target.Alives()) {
                    startX = 2f * ctx.Target.Center.X - ctx.CmdPoint.X;
                }
                Vector2 sweepStart = new(startX, ctx.CmdPoint.Y);
                anchor = Vector2.Lerp(GolemFacts.FistAnchor(ctx.Body, ctx.Side), sweepStart,
                    MathHelper.SmoothStep(0f, 1f, MathHelper.Clamp(t * 1.35f, 0f, 1f)));
            }

            Vector2 aimDir = (ctx.CmdPoint - anchor).SafeNormalize(Vector2.UnitX * ctx.Side);
            //8次幂后拉：几乎不动——最后几帧猛然吸回
            float pull = MathF.Pow(t, 8f) * 64f;
            npc.Center = anchor - aimDir * pull;
            npc.velocity = Vector2.Zero;
            npc.rotation = ctx.Side < 0 ? (-aimDir).ToRotation() : aimDir.ToRotation();

            //汇聚尘（前2/3充能，末段静默——爆发前的吸气）
            if (!VaultUtils.isServer && t < 0.7f && Timer % 3 == 0) {
                Vector2 from = npc.Center + Main.rand.NextVector2CircularEdge(70f, 70f);
                Vector2 vel = (npc.Center - from) * 0.09f;
                Dust dust = Dust.NewDustPerfect(from, DustID.SolarFlare, vel, 0, default, 1.1f);
                dust.noGravity = true;
            }
            if (!VaultUtils.isServer && Timer == (int)(windup * 0.7f)) {
                SoundEngine.PlaySound(SoundID.Item74 with { Pitch = -0.6f, Volume = 0.8f }, npc.Center);
            }

            Timer++;
            if (Timer >= windup && !VaultUtils.isClient) {
                return new GolemFistPunchState();
            }
            return null;
        }
    }

    /// <summary>出拳飞行：直拳/勾拳/横扫共用，撞墙反弹为二次弹道</summary>
    [InnoVault.StateMachines.VaultState((int)GolemFistStateIndex.Punch, typeof(GolemFistStateContext))]
    internal class GolemFistPunchState : GolemFistStateBase
    {
        public override string StateName => "FistPunch";
        public override GolemFistStateIndex StateIndex => GolemFistStateIndex.Punch;

        /// <summary>飞行兜底帧</summary>
        private const int MaxFlight = 96;
        //勾拳弧线角速度（符号 = 弯向）
        private float hookTurnRate;
        private bool launched;

        public override void OnEnter(GolemFistStateContext ctx) {
            base.OnEnter(ctx);
            launched = false;
            ctx.BounceBudget = ctx.CmdBounce;
        }

        public override IGolemFistState OnUpdate(GolemFistStateContext ctx) {
            NPC npc = ctx.Npc;

            if (!launched) {
                launched = true;
                Launch(ctx);
            }

            //接触伤害只在高速时生效
            npc.damage = npc.velocity.Length() > GolemDirector.FistContactSpeed ? npc.defDamage : 0;

            //勾拳持续偏转
            if (ctx.CmdKind == GolemFistCommand.HookSwing && hookTurnRate != 0f) {
                npc.velocity = npc.velocity.RotatedBy(hookTurnRate);
            }

            npc.rotation = ctx.Side < 0
                ? (-npc.velocity).ToRotation()
                : npc.velocity.ToRotation();

            //拖尾火星
            if (!VaultUtils.isServer && Main.rand.NextBool(2)) {
                Dust dust = Dust.NewDustPerfect(npc.Center + Main.rand.NextVector2Circular(16f, 16f),
                    DustID.Torch, -npc.velocity * 0.06f, 0, default, 1.4f);
                dust.noGravity = true;
            }

            //撞墙反弹（服务端裁决弹道，碎石弹幕承载各端表现）
            if (!VaultUtils.isClient) {
                TryBounce(ctx);

                Vector2 anchor = GolemFacts.FistAnchor(ctx.Body, ctx.Side);
                bool tooFar = npc.Distance(anchor) > GolemDirector.PunchLeash;
                bool timeout = Timer >= MaxFlight;
                bool spent = ctx.BounceBudget < 0;
                //低位横扫：行程足够即返
                bool sweepDone = ctx.CmdKind == GolemFistCommand.LowSweep && Timer > 46;
                if (tooFar || timeout || spent || sweepDone) {
                    return new GolemFistReturnState();
                }
            }

            Timer++;
            return null;
        }

        private void Launch(GolemFistStateContext ctx) {
            NPC npc = ctx.Npc;
            Vector2 aim = (ctx.CmdPoint - npc.Center).SafeNormalize(Vector2.UnitX * ctx.Side);
            float speed = ctx.CmdSpeed;

            switch (ctx.CmdKind) {
                case GolemFistCommand.HookSwing: {
                    //起手偏离目标线 55 度，飞行中弧线弯回——回旋勾拳
                    float side = ctx.Side;
                    Vector2 dir = aim.RotatedBy(-0.96f * side);
                    npc.velocity = dir * speed;
                    hookTurnRate = 0.041f * side;
                    break;
                }
                case GolemFistCommand.LowSweep: {
                    //贴地横扫：只保留水平分量
                    npc.velocity = new Vector2(Math.Sign(aim.X) * speed, 0f);
                    hookTurnRate = 0f;
                    break;
                }
                default: {
                    npc.velocity = aim * speed;
                    hookTurnRate = 0f;
                    break;
                }
            }

            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item14 with { Pitch = 0.2f, Volume = 0.75f }, npc.Center);
                GolemScreenEffects.Shake(3f);
            }
            npc.netUpdate = true;
        }

        /// <summary>逐轴测试下一步碰撞，反弹并向目标折射</summary>
        private void TryBounce(GolemFistStateContext ctx) {
            NPC npc = ctx.Npc;
            //出手10帧内不判反弹，防止贴着发射者地形卡死
            if (Timer < 10) {
                return;
            }

            bool hitX = npc.velocity.X != 0f
                && Collision.SolidCollision(npc.position + new Vector2(npc.velocity.X, 0f), npc.width, npc.height);
            bool hitY = npc.velocity.Y != 0f
                && Collision.SolidCollision(npc.position + new Vector2(0f, npc.velocity.Y), npc.width, npc.height);

            if (!hitX && !hitY) {
                return;
            }

            ctx.BounceBudget--;
            if (ctx.BounceBudget < 0) {
                return;
            }

            if (hitX) {
                npc.velocity.X = -npc.velocity.X * GolemDirector.BounceKeep;
            }
            if (hitY) {
                npc.velocity.Y = -npc.velocity.Y * GolemDirector.BounceKeep;
            }

            //向目标折射（限角），反弹是二次弹道不是无害回弹
            if (ctx.Target.Alives()) {
                Vector2 toTarget = (ctx.Target.Center - npc.Center).SafeNormalize(Vector2.UnitY);
                float current = npc.velocity.ToRotation();
                float wanted = toTarget.ToRotation();
                float steered = current.AngleTowards(wanted, GolemDirector.BounceSteer);
                npc.velocity = steered.ToRotationVector2() * npc.velocity.Length();
            }

            //碎石扇（弹幕承载跨端表现与音效）
            Vector2 normal = hitX ? new Vector2(-Math.Sign(npc.velocity.X), 0f) : new Vector2(0f, -Math.Sign(npc.velocity.Y));
            int damage = GolemDirector.ScaleDamage(GolemDirector.ShrapnelDamage, ctx.DeathMode);
            int count = ctx.DeathMode ? 5 : 4;
            for (int i = 0; i < count; i++) {
                Vector2 vel = (-normal).RotatedBy(MathHelper.Lerp(-0.85f, 0.85f, i / (count - 1f)))
                    * Main.rand.NextFloat(6f, 9f) * -1f;
                Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, vel,
                    ModContent.ProjectileType<GolemStoneShrapnel>(), damage, 0f, Main.myPlayer);
            }
            npc.netUpdate = true;
        }
    }

    /// <summary>回收归位：加速直返，入位铿锵</summary>
    [InnoVault.StateMachines.VaultState((int)GolemFistStateIndex.Return, typeof(GolemFistStateContext))]
    internal class GolemFistReturnState : GolemFistStateBase
    {
        public override string StateName => "FistReturn";
        public override GolemFistStateIndex StateIndex => GolemFistStateIndex.Return;

        public override IGolemFistState OnUpdate(GolemFistStateContext ctx) {
            NPC npc = ctx.Npc;
            Vector2 anchor = GolemFacts.FistAnchor(ctx.Body, ctx.Side);
            Vector2 to = anchor - npc.Center;
            float dist = to.Length();

            float speed = MathHelper.Clamp(dist / 14f, 10f, GolemDirector.FistReturnSpeed);
            npc.velocity = Vector2.Lerp(npc.velocity, to.SafeNormalize(Vector2.Zero) * speed, 0.2f);
            npc.rotation = ctx.Side < 0 ? npc.velocity.ToRotation() : (-npc.velocity).ToRotation();

            Timer++;
            if (!VaultUtils.isClient && (dist < 26f || Timer > 80)) {
                if (!VaultUtils.isServer) {
                    //单机端入位铿锵
                    SoundEngine.PlaySound(SoundID.NPCHit4 with { Pitch = -0.5f, Volume = 0.7f }, npc.Center);
                }
                return new GolemFistAnchorState();
            }
            return null;
        }
    }

    /// <summary>护卫环绕：大招/仪式期收拢为卫星</summary>
    [InnoVault.StateMachines.VaultState((int)GolemFistStateIndex.Guard, typeof(GolemFistStateContext))]
    internal class GolemFistGuardState : GolemFistStateBase
    {
        public override string StateName => "FistGuard";
        public override GolemFistStateIndex StateIndex => GolemFistStateIndex.Guard;

        public override IGolemFistState OnUpdate(GolemFistStateContext ctx) {
            NPC npc = ctx.Npc;
            npc.dontTakeDamage = true;
            npc.damage = 0;
            npc.velocity = Vector2.Zero;

            //绕躯干旋转编队（相位差半圈）
            GolemBodyAI bodyOverride = ctx.Body.GetOverride<GolemBodyAI>();
            float clock = bodyOverride != null ? bodyOverride.ai[GolemAiSlots.OverrideShowClock] : Timer;
            float rot = clock * 0.055f + (ctx.Side < 0 ? 0f : MathHelper.Pi);
            Vector2 slot = ctx.Body.Center + rot.ToRotationVector2() * 190f;
            npc.Center = Vector2.Lerp(npc.Center, slot, 0.2f);
            npc.rotation = rot + MathHelper.PiOver2;

            Timer++;

            //躯干离开仪式态自动散队
            GolemStateIndex bodyState = GolemFacts.GetStateIndex(ctx.Body);
            bool stillGuard = bodyState is GolemStateIndex.SolarOverdrive or GolemStateIndex.HeadDetach
                or GolemStateIndex.MeteorLeap or GolemStateIndex.Intro;
            if (!stillGuard && !VaultUtils.isClient) {
                return new GolemFistReturnState();
            }
            return null;
        }
    }

    /// <summary>坠地崩解：死亡演出专用</summary>
    [InnoVault.StateMachines.VaultState((int)GolemFistStateIndex.DeathFall, typeof(GolemFistStateContext))]
    internal class GolemFistDeathFallState : GolemFistStateBase
    {
        public override string StateName => "FistDeathFall";
        public override GolemFistStateIndex StateIndex => GolemFistStateIndex.DeathFall;

        private bool landed;

        public override void OnEnter(GolemFistStateContext ctx) {
            base.OnEnter(ctx);
            landed = false;
        }

        public override IGolemFistState OnUpdate(GolemFistStateContext ctx) {
            NPC npc = ctx.Npc;
            npc.dontTakeDamage = true;
            npc.damage = 0;

            if (landed) {
                npc.velocity = Vector2.Zero;
                Timer++;
                return null;
            }

            //松脱坠落：引擎重力 + 地形碰撞
            npc.noGravity = false;
            npc.noTileCollide = false;
            npc.rotation += npc.velocity.Y * 0.01f * ctx.Side;

            if (npc.velocity.Y == 0f && Timer > 4) {
                landed = true;
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.NPCHit4 with { Pitch = -0.7f }, npc.Center);
                    GolemScreenEffects.Shake(4f);
                    for (int i = 0; i < 12; i++) {
                        PRTLoader.NewParticle<PRT_MarbleChip>(npc.Bottom + Main.rand.NextVector2Circular(18f, 6f),
                            new Vector2(Main.rand.NextFloat(-3f, 3f), Main.rand.NextFloat(-5f, -1f)),
                            new Color(122, 104, 78), Main.rand.NextFloat(0.7f, 1.2f)).Configure(46);
                    }
                }
            }

            Timer++;
            return null;
        }
    }
}
