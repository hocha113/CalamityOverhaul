using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletron.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletron.States;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletron.States.Hands
{
    /// <summary>
    /// 合掌拍捉伺服：读头侧子相位行动<br/>
    /// 全程零接触伤害（与普通合拍钳杀的机制区别）；夹持期免伤，拍空期挨打（惩罚窗）
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)SkeletronHandStateIndex.Snatch, typeof(SkeletronHandContext))]
    internal class HandSnatchState : SkeletronHandStateBase
    {
        public override string StateName => "HandSnatch";
        public override SkeletronHandStateIndex StateIndex => SkeletronHandStateIndex.Snatch;

        private Vector2 clapAnchor;
        private bool snapLaunched;
        private int subLatch = -1;
        private int subTimer;

        public override void OnEnter(SkeletronHandContext ctx) {
            base.OnEnter(ctx);
            snapLaunched = false;
            subLatch = -1;
            subTimer = 0;
        }

        public override SkeletronHandStateBase OnUpdate(SkeletronHandContext ctx) {
            NPC npc = ctx.Npc;
            //投技全程无接触伤害：威胁只来自被明确 telegraph 的抓取判定
            npc.damage = 0;

            SkeletronStateIndex headState = SkeletronHeadAI.GetStateIndex(ctx.Head);
            int sub = (int)ctx.Head.ai[SkeletronAiSlots.HeadParamB];

            if (sub != subLatch) {
                subLatch = sub;
                subTimer = 0;
            }

            //夹持期免伤（断手转阶段不该在举人时被打出来），其余相位照常挨打
            npc.dontTakeDamage = headState == SkeletronStateIndex.PalmSnatch
                && sub >= SkeletronPalmSnatchState.SubClamp && sub <= SkeletronPalmSnatchState.SubSlam;

            //头已离开投技状态则回护卫位（服务端决策）
            if (!VaultUtils.isClient && (headState != SkeletronStateIndex.PalmSnatch || Timer > 760)) {
                return new HandGuardState();
            }

            if (headState == SkeletronStateIndex.PalmSnatch) {
                UpdateBySubPhase(ctx, sub);
            }

            subTimer++;
            Timer++;
            return null;
        }

        public override void OnExit(SkeletronHandContext ctx) {
            base.OnExit(ctx);
            ctx.Npc.dontTakeDamage = false;
        }

        private void UpdateBySubPhase(SkeletronHandContext ctx, int sub) {
            NPC npc = ctx.Npc;
            float halfGap = SkeletronDirector.SnatchHalfGap;

            switch (sub) {
                case SkeletronPalmSnatchState.SubFlank: {
                    //甩到玩家两侧远位，掌心相向
                    Vector2 slot = ctx.Target.Center + new Vector2(ctx.Side * SkeletronDirector.SnatchFlankDistance, 0f);
                    SpringMove(ctx, slot, 0.22f, 0.8f, 38f);
                    AimPalm(npc, ctx.Target.Center, 0.16f);
                    ctx.PalmFlame = Math.Max(ctx.PalmFlame, 0.3f);
                    break;
                }

                case SkeletronPalmSnatchState.SubTelegraph: {
                    //对峙：锚点锁定前随人平移，锁定后钉死走廊两端（读秒窗）
                    Vector2 focus = ReadAnchor(ctx, out bool lockedAnchor);
                    Vector2 hold = focus + new Vector2(ctx.Side * SkeletronDirector.SnatchFlankDistance, 0f);
                    SpringMove(ctx, hold, lockedAnchor ? 0.3f : 0.2f, 0.78f, 36f);
                    AimPalm(npc, focus, 0.25f);

                    float t = MathHelper.Clamp(subTimer / (float)SkeletronDirector.SnatchTelegraphFrames, 0f, 1f);
                    ctx.ChainTension = t;
                    ctx.PalmFlame = 0.3f + 0.7f * t;

                    //末拍颤抖渐强（确定性抖动，服务端权威位置也一致）
                    if (t > 0.6f) {
                        float amp = (t - 0.6f) / 0.4f * 2.2f;
                        float wob = (Main.GameUpdateCount + npc.whoAmI * 37) * 0.9f;
                        npc.velocity += new Vector2((float)Math.Sin(wob), (float)Math.Cos(wob * 1.31f)) * amp * 0.4f;
                    }
                    break;
                }

                case SkeletronPalmSnatchState.SubSnap: {
                    //一帧内定速直线闭合：直线读得快
                    if (!snapLaunched) {
                        snapLaunched = true;
                        clapAnchor = ReadAnchor(ctx, out _);
                        Vector2 slot = clapAnchor + new Vector2(ctx.Side * SkeletronDirector.SnatchHalfGap, 0f);
                        npc.velocity = (slot - npc.Center).SafeNormalize(Vector2.UnitX * -ctx.Side)
                            * SkeletronDirector.SnatchSnapSpeed(ctx.Death);
                        ctx.SpringVelocity = npc.velocity;
                        if (!VaultUtils.isServer) {
                            SoundEngine.PlaySound(SoundID.Item71 with { Volume = 1f, Pitch = -0.45f }, npc.Center);
                        }
                    }
                    //闭合到槽位即急刹（等待头侧捕获/拍空裁决）
                    Vector2 mySlot = clapAnchor + new Vector2(ctx.Side * halfGap, 0f);
                    if ((mySlot - npc.Center).Length() < 30f) {
                        npc.velocity *= 0.25f;
                        ctx.SpringVelocity = npc.velocity;
                    }
                    AimPalm(npc, clapAnchor, 0.35f);
                    ctx.ChainTension = 1f;
                    break;
                }

                case SkeletronPalmSnatchState.SubClamp: {
                    //夹持顿帧：钉死囚笼槽位
                    Vector2 cage = SkeletronPalmSnatchState.GetCageCenter(ctx.Head);
                    SpringMove(ctx, ClampSlot(ctx, cage, halfGap), 0.42f, 0.62f, 30f);
                    AimPalm(npc, cage, 0.4f);
                    ctx.ChainTension = 1f;
                    ctx.PalmFlame = 1f;
                    break;
                }

                case SkeletronPalmSnatchState.SubLift:
                case SkeletronPalmSnatchState.SubBarrage: {
                    //举至颅前：以头为锚伺服，环轰期轻微呼吸浮沉
                    Vector2 liftPoint = ctx.Head.Center + new Vector2(0f, 205f);
                    if (sub == SkeletronPalmSnatchState.SubBarrage) {
                        //各端确定性时钟驱动浮沉
                        ctx.Head.TryGetOverride(out SkeletronHeadAI headOverride);
                        float clock = headOverride?.ai[SkeletronAiSlots.OverrideOrbitClock] ?? Main.GameUpdateCount;
                        liftPoint.Y += (float)Math.Sin(clock * 0.045f) * 10f;
                    }
                    SpringMove(ctx, ClampSlot(ctx, liftPoint, halfGap), 0.24f, 0.8f, 24f);
                    AimPalm(npc, liftPoint, 0.3f);
                    ctx.ChainTension = 0.75f;
                    ctx.PalmFlame = 0.85f;
                    break;
                }

                case SkeletronPalmSnatchState.SubWindup: {
                    //收尾蓄势：携人急提 + 颤抖（力从蓄来）
                    Vector2 riseTo = ctx.Head.Center + new Vector2(0f, 60f);
                    SpringMove(ctx, ClampSlot(ctx, riseTo, halfGap), 0.3f, 0.76f, 30f);
                    float wob = (Main.GameUpdateCount + npc.whoAmI * 53) * 1.1f;
                    npc.velocity += new Vector2((float)Math.Sin(wob), 0f) * 0.7f;
                    AimPalm(npc, npc.Center + Vector2.UnitY * 120f, 0.2f);
                    ctx.ChainTension = 1f;
                    ctx.PalmFlame = 1f;
                    break;
                }

                case SkeletronPalmSnatchState.SubSlam: {
                    //携人直线下砸
                    if (subTimer == 0) {
                        npc.velocity = new Vector2(0f, SkeletronDirector.SlamSpeed(ctx.Death) + 6f);
                        ctx.SpringVelocity = npc.velocity;
                    }
                    AimPalm(npc, npc.Center + Vector2.UnitY * 200f, 0.4f);
                    ctx.ChainTension = 1f;
                    break;
                }

                case SkeletronPalmSnatchState.SubRecover: {
                    //嵌地失力
                    npc.velocity *= 0.6f;
                    ctx.SpringVelocity = npc.velocity;
                    ctx.ChainTension = 0f;
                    ctx.PalmFlame = 0f;
                    break;
                }

                default: { //SubWhiff：拍空失衡下垂（惩罚窗，挨打）
                    npc.velocity.X *= 0.9f;
                    npc.velocity.Y = Math.Min(npc.velocity.Y + 0.22f, 3.5f);
                    ctx.SpringVelocity = npc.velocity;
                    npc.rotation += (float)Math.Sin((Main.GameUpdateCount + npc.whoAmI * 29) * 0.11f) * 0.012f;
                    ctx.ChainTension = 0f;
                    ctx.PalmFlame = 0f;
                    break;
                }
            }
        }

        /// <summary>囚笼槽位：中心 ± 半间距</summary>
        private static Vector2 ClampSlot(SkeletronHandContext ctx, Vector2 center, float halfGap) {
            return center + new Vector2(ctx.Side * halfGap, 0f);
        }

        /// <summary>读头侧锁定锚点；未锁定/取不到时回退玩家当前位</summary>
        private static Vector2 ReadAnchor(SkeletronHandContext ctx, out bool lockedAnchor) {
            //服务端读头部上下文的权威锚点；客户端是位置傀儡，回退值只影响被丢弃的本地模拟
            if (ctx.Head.TryGetOverride(out SkeletronHeadAI headOverride)
                && headOverride?.Context != null && headOverride.Context.SnatchAnchorLocked) {
                lockedAnchor = true;
                return headOverride.Context.SnatchAnchor;
            }
            lockedAnchor = false;
            return ctx.Target.Center;
        }
    }
}
