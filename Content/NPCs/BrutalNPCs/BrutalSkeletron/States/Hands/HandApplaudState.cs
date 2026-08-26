using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletron.Core;
using System;
using Terraria;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletron.States.Hands
{
    /// <summary>
    /// 嘲讽鼓掌伺服：读头侧子相位编舞三连击掌（张开蓄势→击前悬停→按剩余帧精确会合）<br/>
    /// 全程无接触伤害（呼吸拍/输出窗），撞掌反馈走冲击计数广播，骨屑环由头侧权威发射
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)SkeletronHandStateIndex.Applaud, typeof(SkeletronHandContext))]
    internal class HandApplaudState : SkeletronHandStateBase
    {
        public override string StateName => "HandApplaud";
        public override SkeletronHandStateIndex StateIndex => SkeletronHandStateIndex.Applaud;

        private int subLatch = -1;
        private int subTimer;

        public override void OnEnter(SkeletronHandContext ctx) {
            base.OnEnter(ctx);
            subLatch = -1;
            subTimer = 0;
        }

        public override SkeletronHandStateBase OnUpdate(SkeletronHandContext ctx) {
            NPC npc = ctx.Npc;
            npc.damage = 0;
            npc.dontTakeDamage = false;

            SkeletronStateIndex headState = SkeletronHeadAI.GetStateIndex(ctx.Head);
            int sub = (int)ctx.Head.ai[SkeletronAiSlots.HeadParamB];

            if (sub != subLatch) {
                subLatch = sub;
                subTimer = 0;
            }

            //头已离开鼓掌状态则回护卫位（服务端决策）
            if (!VaultUtils.isClient && (headState != SkeletronStateIndex.Applause || Timer > 460)) {
                return new HandGuardState();
            }

            if (headState == SkeletronStateIndex.Applause) {
                UpdateBySubPhase(ctx, sub);
            }

            subTimer++;
            Timer++;
            return null;
        }

        private void UpdateBySubPhase(SkeletronHandContext ctx, int sub) {
            NPC npc = ctx.Npc;
            Vector2 meet = SkeletronApplauseState.ClapMeetPoint(ctx.Head);

            switch (sub) {
                case SkeletronApplauseState.SubGather: {
                    //甩到颅前两侧就位，掌心相向
                    Vector2 slot = meet + new Vector2(ctx.Side * SkeletronApplauseState.ClapSpread[0], 0f);
                    SpringMove(ctx, slot, 0.24f, 0.8f, 36f);
                    AimPalm(npc, meet, 0.18f);
                    ctx.PalmFlame = Math.Max(ctx.PalmFlame, 0.3f);
                    break;
                }

                case SkeletronApplauseState.SubClap1:
                case SkeletronApplauseState.SubClap2:
                case SkeletronApplauseState.SubClap3: {
                    int clapIndex = sub - SkeletronApplauseState.SubClap1;
                    int len = SkeletronApplauseState.ClapLen[clapIndex];
                    float spread = SkeletronApplauseState.ClapSpread[clapIndex];
                    int spreadEnd = (int)(len * 0.55f);
                    int holdEnd = (int)(len * 0.72f);

                    if (subTimer < spreadEnd) {
                        //张开蓄势（ease-out 外摆，一击比一击张得开）
                        float t = subTimer / (float)spreadEnd;
                        float ease = 1f - (1f - t) * (1f - t);
                        Vector2 slot = meet + new Vector2(ctx.Side * MathHelper.Lerp(40f, spread, ease), 0f);
                        SpringMove(ctx, slot, 0.3f, 0.76f, 38f);
                        AimPalm(npc, meet, 0.25f);
                        ctx.ChainTension = t * 0.6f;
                        ctx.PalmFlame = 0.3f + 0.5f * t;
                    }
                    else if (subTimer < holdEnd) {
                        //击前悬停微颤（打击力来自静止）
                        Vector2 slot = meet + new Vector2(ctx.Side * spread, 0f);
                        SpringMove(ctx, slot, 0.34f, 0.7f, 30f);
                        float wob = (Main.GameUpdateCount + npc.whoAmI * 31) * 1.3f;
                        npc.velocity += new Vector2((float)Math.Sin(wob), (float)Math.Cos(wob * 1.17f)) * 0.5f;
                        AimPalm(npc, meet, 0.3f);
                        ctx.ChainTension = 0.85f;
                        ctx.PalmFlame = 1f;
                    }
                    else {
                        //合拢：按剩余帧数精确会合（拍点=头侧子相位切换帧，撞掌反馈由计数广播补发）
                        Vector2 slot = meet + new Vector2(ctx.Side * 24f, 0f);
                        int framesLeft = Math.Max(len - subTimer, 1);
                        Vector2 snap = (slot - npc.Center) / framesLeft * 1.25f;
                        if (snap.Length() > 46f) {
                            snap = snap.SafeNormalize(Vector2.UnitX * -ctx.Side) * 46f;
                        }
                        npc.velocity = snap;
                        ctx.SpringVelocity = snap;
                        AimPalm(npc, meet, 0.45f);
                        ctx.ChainTension = 1f;
                        ctx.PalmFlame = 1f;
                    }
                    break;
                }

                default: { //SubRecover：脱力下垂缓摆
                    npc.velocity.X *= 0.92f;
                    npc.velocity.Y = Math.Min(npc.velocity.Y + 0.14f, 2.8f);
                    ctx.SpringVelocity = npc.velocity;
                    npc.rotation += (float)Math.Sin((Main.GameUpdateCount + npc.whoAmI * 29) * 0.09f) * 0.01f;
                    ctx.ChainTension = 0f;
                    ctx.PalmFlame = 0f;
                    break;
                }
            }
        }
    }
}
