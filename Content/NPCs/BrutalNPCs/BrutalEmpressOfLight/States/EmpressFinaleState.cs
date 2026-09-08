using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEmpressOfLight.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEmpressOfLight.Projectiles;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEmpressOfLight.Rendering;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEmpressOfLight.States
{
    /// <summary>
    /// 终幕圆舞：序曲两小节 → 光痕圆舞八小节（每拍留痕 + 每两小节一束）→ 蝶群十小节（合掌 + 每小节一道光痕）
    /// → 月影七小节（昼；夜换成日舞表）→ 终末：竞技场 3000→700 缩二十秒才可击杀，之后每小节轮换光束/光痕/鞭击直到死亡。
    /// 三根柱子在缩圈里层层叠上去；步进按小节，服务端权威，客户端只跟姿态与表现
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)EmpressStateIndex.Finale, typeof(EmpressStateContext))]
    internal class EmpressFinaleState : EmpressStateBase
    {
        public override string StateName => "EmpressFinale";
        public override EmpressStateIndex StateIndex => EmpressStateIndex.Finale;

        private enum Step { Overture, EchoWaltz, Swarm, Whiteout, Final }

        /// <summary>缩圈：起止半径与用时（20 秒）</summary>
        internal const float ArenaStart = 3000f;
        internal const float ArenaEnd = 700f;
        internal const int ShrinkFrames = 1200;

        private const int OvertureBars = 2;
        private const int EchoBars = 8;
        private const int SwarmBars = 10;
        private const int WhiteoutBars = 7;

        private Step step;
        private int stepStartBar = -1;
        private int shrinkTimer;
        private float orbitAngle;
        private int lacewingsReleased;
        private int shardsThrown;
        private EmpressStateContext Context;

        public override void OnEnter(EmpressStateContext context) {
            base.OnEnter(context);
            Context = context;
            step = Step.Overture;
            stepStartBar = -1;
            shrinkTimer = 0;
            lacewingsReleased = 0;
            shardsThrown = 0;
            context.FinaleKillable = false;
            orbitAngle = context.Target.Alives() ? context.Npc.AngleFrom(context.Target.Center) : 0f;
        }

        private int StepBar => stepStartBar < 0 ? -1 : Context.BarIndex - stepStartBar;

        private void Advance(Step next) {
            step = next;
            stepStartBar = Context.BarIndex;
            Context.Npc.netUpdate = true;
            if (!VaultUtils.isServer) {
                EmpressScreenFX.PushPhaseFlash(0.35f);
            }
        }

        public override IEmpressState OnUpdate(EmpressStateContext context) {
            Context = context;
            NPC npc = context.Npc;
            Player target = context.Target;
            Timer++;

            if (!target.Alives()) {
                npc.velocity *= 0.95f;
                return null;
            }
            //第一步等第一拍对表
            if (stepStartBar < 0) {
                npc.velocity *= 0.9f;
                if (context.Downbeat && Timer > 2) {
                    stepStartBar = context.BarIndex;
                    if (!VaultUtils.isServer) {
                        EmpressScreenFX.PushPhaseGlow();
                    }
                }
                return null;
            }

            switch (step) {
                case Step.Overture:
                    Overture(context, npc, target);
                    break;
                case Step.EchoWaltz:
                    EchoWaltz(context, npc, target);
                    break;
                case Step.Swarm:
                    Swarm(context, npc, target);
                    break;
                case Step.Whiteout:
                    Whiteout(context, npc, target);
                    break;
                case Step.Final:
                    Final(context, npc, target);
                    break;
            }

            EmpressMotion.AmbientGlow(npc, context.DayFormBlend);
            //终章不自然结束：死亡/离场由主控层面强制切换
            return null;
        }

        #region 序曲
        /// <summary>两小节：她在你头顶 340 悬着，每一拍翅膀闪一次，光谷里只有拍子</summary>
        private void Overture(EmpressStateContext context, NPC npc, Player target) {
            context.Pose = EmpressPose.Idle;
            context.ArenaRadiusRequest = 2600f;
            Vector2 dest = target.Center + new Vector2(0f, -340f);
            npc.velocity = npc.velocity * 0.8f + (dest - npc.Center) * 0.03f;
            if (context.OnBeat && !VaultUtils.isServer) {
                PRTLoader.NewParticle<PRT_EmpressRipple>(npc.Center, Vector2.Zero, Color.White, context.Downbeat ? 0.8f : 0.45f)?.Configure(14, 0.12f, context.DayFormBlend);
                EmpressScreenFX.DeclareAmbient(0.5f);
            }
            if (StepBar >= OvertureBars && context.Downbeat) {
                HopOut(npc);
                Advance(Step.EchoWaltz);
            }
        }
        #endregion

        #region 光痕圆舞
        /// <summary>八小节：每拍留痕，她绕你走圆，每两小节第一拍一束追踪光束，奇数小节一圈慢球</summary>
        private void EchoWaltz(EmpressStateContext context, NPC npc, Player target) {
            context.Pose = EmpressPose.CastBoth;
            context.PoseTimer = 20f + context.BarFrame * 0.5f;
            context.ArenaRadiusRequest = 2600f;
            context.SetChargeState(3, 0.3f + 0.4f * (context.BarFrame / (float)EmpressTempo.BarFrames));

            orbitAngle += MathHelper.TwoPi / (6f * EmpressTempo.BarFrames);
            Vector2 dest = target.Center + orbitAngle.ToRotationVector2() * 400f + new Vector2(0f, -140f);
            npc.velocity = npc.velocity * 0.8f + (dest - npc.Center) * 0.03f;

            int bar = StepBar;
            if (bar < EchoBars) {
                if (context.OnBeat) {
                    EmpressEchoState.LeaveEchoes(context, npc);
                }
                if (context.Downbeat && bar % 2 == 0 && !VaultUtils.isClient) {
                    float angle = npc.AngleTo(target.Center);
                    EmpressCast.Beam(npc, npc.Center + angle.ToRotationVector2() * 100f, angle, context.BeamDamage, EmpressBeamMode.Normal);
                }
                if (context.Downbeat && bar % 2 == 1) {
                    PlayLocal(SoundID.Item164 with { Volume = 0.6f, Pitch = 0.2f }, npc.Center);
                    if (!VaultUtils.isClient) {
                        for (int i = 0; i < 12; i += 2) {
                            Vector2 dir = (MathHelper.TwoPi / 12f * i + orbitAngle).ToRotationVector2();
                            EmpressCast.Bolt(npc, npc.Center + dir * 40f, dir * 3.2f, context.BoltDamage, EmpressBoltMode.Straight);
                        }
                    }
                }
            }
            //留痕停下后再等三小节让最后一批琉璃剑碎完
            if (bar >= EchoBars + 3 && context.Downbeat) {
                HopOut(npc);
                Advance(Step.Swarm);
            }
        }
        #endregion

        #region 蝶群
        /// <summary>十小节：第一小节放蝶，奇数小节合掌冻蝶，每小节第一拍再留一道光痕</summary>
        private void Swarm(EmpressStateContext context, NPC npc, Player target) {
            context.ArenaRadiusRequest = 2600f;
            int bar = StepBar;
            bool clapBar = bar >= 1 && bar % 2 == 1;
            bool preClap = bar % 2 == 0 && context.BarFrame >= EmpressTempo.BeatFrames * 2;
            context.Pose = clapBar || preClap ? EmpressPose.CastBoth : EmpressPose.Idle;
            context.PoseTimer = clapBar ? 60f : (preClap ? (context.BarFrame - 40f) * 3f : 0f);
            if (preClap) {
                context.SetChargeState(3, (context.BarFrame - 40f) / 20f);
            }

            int side = bar % 4 < 2 ? 1 : -1;
            Vector2 dest = target.Center + new Vector2(side * 320f, -280f);
            npc.velocity = npc.velocity * 0.85f + (dest - npc.Center) * 0.02f;

            if (bar == 0 && context.OnBeat && lacewingsReleased < 24) {
                PlayLocal(SoundID.Item164 with { Volume = 0.45f, Pitch = 0.5f }, npc.Center);
                if (!VaultUtils.isClient) {
                    for (int i = 0; i < 8; i++) {
                        Vector2 hand = i % 2 == 0 ? context.LeftHand : context.RightHand;
                        float angle = -MathHelper.PiOver2 + (i - 3.5f) * 0.35f;
                        EmpressCast.Lacewing(npc, hand, angle.ToRotationVector2() * 4f, context.LacewingDamage, target.whoAmI, context.BarIndex);
                    }
                }
                lacewingsReleased += 8;
            }
            if (clapBar && context.Downbeat) {
                PlayLocal(SoundID.Item29 with { Volume = 0.9f, Pitch = 0.1f }, npc.Center);
                PlayLocal(SoundID.Item35 with { Volume = 0.5f, Pitch = 0.6f }, npc.Center);
                EmpressMotion.Shake(npc.Center, 3f, 10);
                if (!VaultUtils.isServer) {
                    EmpressScreenFX.PushPrismPulse(npc.Center, 0.3f, 18);
                }
            }
            if (bar >= 1 && bar < SwarmBars && context.Downbeat) {
                EmpressEchoState.LeaveEchoes(context, npc);
            }
            if (bar >= SwarmBars && context.Downbeat) {
                KillLacewings(npc);
                HopOut(npc);
                Advance(context.DayEmpowered ? Step.Whiteout : Step.Final);
            }
        }

        private static void KillLacewings(NPC npc) {
            if (VaultUtils.isClient) {
                return;
            }
            int type = ModContent.ProjectileType<EmpressLacewing>();
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile p = Main.projectile[i];
                if (p.active && p.type == type && (int)p.ai[1] == npc.whoAmI) {
                    p.Kill();
                }
            }
        }
        #endregion

        #region 月影
        /// <summary>七小节：抛屑 → 聚光 → 四小节全白每拍蒸发 → 释放。与 MoonShadow 态同一几何</summary>
        private void Whiteout(EmpressStateContext context, NPC npc, Player target) {
            context.ArenaRadiusRequest = 2600f;
            int bar = StepBar;
            int bf = context.BarFrame;

            if (bar == 0) {
                context.Pose = EmpressPose.CastLeft;
                context.PoseTimer = bf;
                if (context.OnBeat && shardsThrown < 3) {
                    ThrowShard(context, npc, target, shardsThrown);
                    shardsThrown++;
                }
                Vector2 dest = target.Center + new Vector2(0f, -420f);
                npc.velocity = npc.velocity * 0.85f + (dest - npc.Center) * 0.02f;
            }
            else if (bar == 1) {
                context.Pose = EmpressPose.Transform;
                context.PoseTimer = Math.Min(bf, 58);
                context.SetChargeState(3, bf / 60f);
                Vector2 dest = target.Center + new Vector2(0f, -420f);
                npc.velocity = npc.velocity * 0.85f + (dest - npc.Center) * 0.03f;
                if (!VaultUtils.isServer) {
                    EmpressScreenFX.DeclareWhiteout(0.5f * bf / 60f, npc.Center);
                    if (bf == 58) {
                        EmpressScreenFX.PushPhaseFlash(0.5f);
                    }
                }
            }
            else if (bar < 6) {
                context.Pose = EmpressPose.Transform;
                context.PoseTimer = 40f + 18f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 2f);
                context.SetChargeState(3, 1f);
                npc.velocity *= 0.9f;
                npc.velocity.Y += (target.Center.Y - 420f - npc.Center.Y) * 0.0006f;
                if (!VaultUtils.isServer) {
                    EmpressScreenFX.DeclareWhiteout(0.9f, npc.Center);
                    EmpressScreenFX.DeclareAmbient(0.6f);
                    if (context.OnBeat) {
                        WhiteoutTick(context, npc);
                    }
                }
            }
            else if (bar == 6) {
                context.Pose = EmpressPose.Idle;
                npc.velocity *= 0.9f;
                if (bf == 0) {
                    ShatterShards(npc);
                    EmpressMotion.Shake(npc.Center, 6f, 16);
                    PlayLocal(SoundID.Item122 with { Volume = 0.9f, Pitch = -0.4f }, npc.Center);
                }
                if (!VaultUtils.isServer) {
                    EmpressScreenFX.DeclareWhiteout(0.9f * (1f - bf / 60f), npc.Center);
                }
            }
            if (bar >= WhiteoutBars && context.Downbeat) {
                HopOut(npc);
                Advance(Step.Final);
            }
        }

        private void ThrowShard(EmpressStateContext context, NPC npc, Player target, int idx) {
            PlayLocal(SoundID.Item8 with { Volume = 0.7f, Pitch = -0.6f }, npc.Center);
            if (VaultUtils.isClient) {
                return;
            }
            Vector2 pos;
            if (idx == 0) {
                pos = target.Center + npc.DirectionTo(target.Center) * 260f;
            }
            else {
                float sideSign = idx == 1 ? 1f : -1f;
                float ang = npc.AngleTo(target.Center) + sideSign * MathHelper.ToRadians(110f);
                pos = npc.Center + ang.ToRotationVector2() * 700f;
            }
            int life = WhiteoutBars * EmpressTempo.BarFrames - context.BarFrame + 10;
            EmpressCast.MoonShard(npc, pos, npc.DirectionTo(pos) * 0.5f, life);
        }

        private static void WhiteoutTick(EmpressStateContext context, NPC npc) {
            Player me = Main.LocalPlayer;
            if (!me.Alives() || me.Distance(npc.Center) > 3200f) {
                return;
            }
            if (EmpressMoonShadow.InShadow(me.Center, npc.Center, EmpressMoonShard.Active)) {
                return;
            }
            int dmg = Math.Max((int)(me.statLifeMax2 * context.WhiteoutTickFraction), 1);
            PlayerDeathReason reason = PlayerDeathReason.ByCustomReason(EmpressOfLightAI.WhiteoutDeathText(me.name));
            me.Hurt(reason, dmg, 0, false, false, -1, false, 0f, 1f, 0f);
            EmpressHitFeedback.Play(me.Center, Vector2.UnitY, 0.6f, true);
            EmpressScreenFX.PushImpactDim(0.15f);
        }

        private static void ShatterShards(NPC npc) {
            if (VaultUtils.isClient) {
                return;
            }
            int type = ModContent.ProjectileType<EmpressMoonShard>();
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile p = Main.projectile[i];
                if (p.active && p.type == type && (int)p.ai[0] == npc.whoAmI) {
                    p.timeLeft = Math.Min(p.timeLeft, 20);
                    p.netUpdate = true;
                }
            }
        }
        #endregion

        #region 终末
        /// <summary>竞技场 3000→700 收缩 20 秒，收满后血量地板解除；之后每小节轮换：光束 / 光痕 / 鞭击（第四小节强击），第三小节一圈慢球</summary>
        private void Final(EmpressStateContext context, NPC npc, Player target) {
            context.Pose = EmpressPose.Dance;
            context.PoseTimer = 170f;
            shrinkTimer++;
            float k = MathHelper.Clamp(shrinkTimer / (float)ShrinkFrames, 0f, 1f);
            context.ArenaRadiusRequest = MathHelper.Lerp(ArenaStart, ArenaEnd, k);
            context.ArenaFollowSpeed = 6f;
            if (k >= 1f && !context.FinaleKillable) {
                context.FinaleKillable = true;
                EmpressOfLightAI.SayAscension(6);
                if (!VaultUtils.isServer) {
                    EmpressScreenFX.PushPhaseFlash(0.5f);
                }
            }

            //缩圈期间她几乎不动，圆心跟她，所以她也就是圈心
            Vector2 dest = target.Center + new Vector2(0f, -300f);
            npc.velocity = Vector2.Lerp(npc.velocity, (dest - npc.Center) * 0.012f, 0.1f);
            if (!VaultUtils.isServer) {
                EmpressScreenFX.DeclareAmbient(0.7f + 0.3f * k);
            }

            int bar = StepBar;
            if (!context.Downbeat || VaultUtils.isClient) {
                return;
            }
            switch (bar % 4) {
                case 0: {
                    float angle = npc.AngleTo(target.Center);
                    EmpressCast.Beam(npc, npc.Center + angle.ToRotationVector2() * 100f, angle, context.BeamDamage, EmpressBeamMode.Persistent, 40);
                    break;
                }
                case 1:
                    EmpressEchoState.LeaveEchoes(context, npc);
                    break;
                case 2: {
                    int count = k >= 1f ? 10 : 14;
                    for (int j = 0; j < count; j++) {
                        if (j % 3 == 2) {
                            continue;
                        }
                        Vector2 dir = (j * MathHelper.TwoPi / count + bar * 0.4f).ToRotationVector2();
                        EmpressCast.Bolt(npc, npc.Center, dir * 5f, context.BoltDamage, EmpressBoltMode.Straight);
                    }
                    break;
                }
                default: {
                    Vector2 lead = target.velocity * EmpressTempo.BarFrames;
                    if (lead.Length() > 500f) {
                        lead = lead.SafeNormalize(Vector2.Zero) * 500f;
                    }
                    EmpressCast.Whip(npc, target.Center + lead, context.WhipDamage + 8, EmpressTempo.BarFrames, true);
                    break;
                }
            }
        }
        #endregion
    }
}
