using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletron.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletron.Projectiles;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletron.States
{
    /// <summary>
    /// 嘲讽鼓掌（头侧主状态，一阶段人格拍）：双掌颅前三连击掌渐快，掌间迸发骨屑环；<br/>
    /// 头无接触伤害、双掌无接触伤害，全程是玩家的输出窗（呼吸拍），威胁只有带声明缺口的骨屑环<br/>
    /// 广播契约：ai[3]=子相位（0聚合 1~3击掌 4收势），手部读取自编舞
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)SkeletronStateIndex.Applause, typeof(SkeletronStateContext))]
    internal class SkeletronApplauseState : SkeletronStateBase
    {
        public override string StateName => "Applause";
        public override SkeletronStateIndex StateIndex => SkeletronStateIndex.Applause;

        #region 子相位契约（写入头 ai[3]，各端读取）
        internal const int SubGather = 0;   //双掌聚合就位
        internal const int SubClap1 = 1;    //第一击
        internal const int SubClap2 = 2;    //第二击
        internal const int SubClap3 = 3;    //第三击（大拍）
        internal const int SubRecover = 4;  //收势
        #endregion

        #region 时间线常量（手部编舞同读）
        /// <summary>每击子时长，渐快（拍点内 55% 张开、72% 悬停、余下合拢）</summary>
        internal static readonly int[] ClapLen = { 52, 44, 34 };
        /// <summary>每击张开半距，一击比一击张得开（力从蓄来）</summary>
        internal static readonly float[] ClapSpread = { 230f, 270f, 330f };
        internal const int GatherMax = 46;
        internal const int RecoverLen = 46;
        #endregion

        private int subTimer;
        private int subLatch = -1;
        /// <summary>拍点点头冲量（动线每帧重算速度，叠加后衰减）</summary>
        private float nodImpulse;

        /// <summary>击掌交汇点：颏下颅前</summary>
        internal static Vector2 ClapMeetPoint(NPC head) => head.Center + new Vector2(0f, 44f);

        public override void OnEnter(SkeletronStateContext context) {
            base.OnEnter(context);
            subTimer = 0;
            subLatch = -1;
            if (!VaultUtils.isClient) {
                context.Npc.ai[SkeletronAiSlots.HeadParamB] = SubGather;
                context.Npc.netUpdate = true;
            }
        }

        public override ISkeletronState OnUpdate(SkeletronStateContext context) {
            NPC npc = context.Npc;
            //观赏拍：头不撞人（威胁只有骨屑环）
            npc.damage = 0;

            int sub = (int)npc.ai[SkeletronAiSlots.HeadParamB];

            //各端演出去重：子相位前进沿一次性声画 + 头部点头顿挫
            if (sub != subLatch) {
                bool forward = sub > subLatch;
                subLatch = sub;
                subTimer = 0;
                if (forward) {
                    PlaySubPhaseCue(context, sub);
                    if (sub >= SubClap2) {
                        //每记击掌头随之一顿（点头=节拍的身体反应）
                        nodImpulse = 3f;
                    }
                }
            }

            UpdateHeadMotion(context, sub);

            //权威推进
            if (!VaultUtils.isClient) {
                ISkeletronState next = AuthorityUpdate(context, sub);
                if (next != null) {
                    return next;
                }
            }

            subTimer++;
            Timer++;
            return null;
        }

        #region 权威逻辑

        private ISkeletronState AuthorityUpdate(SkeletronStateContext context, int sub) {
            NPC npc = context.Npc;

            //保底超时
            if (Timer > 420) {
                return new SkeletronHubState();
            }
            //断手中断：直接收势（转阶段由全局转移接管）
            if (context.HandCount < 2 && sub < SubRecover) {
                SetSub(npc, SubRecover);
                return null;
            }

            switch (sub) {
                case SubGather: {
                    if (HandsGathered(context) || subTimer >= GatherMax) {
                        SetSub(npc, SubClap1);
                    }
                    break;
                }
                case SubClap1:
                case SubClap2:
                case SubClap3: {
                    int clapIndex = sub - SubClap1;
                    if (subTimer >= ClapLen[clapIndex]) {
                        //拍点落定：掌间迸发骨屑环 + 冲击广播（客户端凭计数播撞掌反馈）
                        EmitClapRing(context, clapIndex);
                        if (context.LeftHand != null) {
                            context.LeftHand.ai[SkeletronAiSlots.HandFree] += 1f;
                            context.LeftHand.netUpdate = true;
                        }
                        SetSub(npc, sub + 1);
                    }
                    break;
                }
                default: { //SubRecover
                    if (subTimer >= RecoverLen) {
                        return new SkeletronHubState();
                    }
                    break;
                }
            }
            return null;
        }

        /// <summary>双掌均已到聚合槽位附近</summary>
        private static bool HandsGathered(SkeletronStateContext context) {
            if (context.LeftHand == null || context.RightHand == null) {
                return false;
            }
            Vector2 meet = ClapMeetPoint(context.Npc);
            float spread = ClapSpread[0];
            return context.LeftHand.Center.Distance(meet + new Vector2(-spread, 0f)) < 110f
                && context.RightHand.Center.Distance(meet + new Vector2(spread, 0f)) < 110f;
        }

        /// <summary>击掌骨屑环：径向直线弹（不弧旋，保缺口几何），朝玩家扇区永空</summary>
        private void EmitClapRing(SkeletronStateContext context, int clapIndex) {
            NPC npc = context.Npc;
            Vector2 meet = ClapMeetPoint(npc);
            float playerAng = (context.Target.Center - meet).ToRotation();
            int count = SkeletronDirector.ApplauseRingCount + clapIndex * 3;
            float speed = SkeletronDirector.ApplauseRingSpeed(context.AsuraMode) + clapIndex * 0.5f;
            int damage = SkullDamage(context);

            for (int i = 0; i < count; i++) {
                float ang = MathHelper.TwoPi * i / count + clapIndex * 0.21f;
                //缺口（契约3）：朝玩家 ±ApplauseGapHalfAngle 扇区永不发射（鼓掌不瞄人），发射循环直接读取
                if (Math.Abs(MathHelper.WrapAngle(ang - playerAng)) < SkeletronDirector.ApplauseGapHalfAngle) {
                    continue;
                }
                Projectile.NewProjectile(npc.GetSource_FromAI(), meet, ang.ToRotationVector2() * speed,
                    ModContent.ProjectileType<SkeletronBoneShard>(), damage, 0f, Main.myPlayer, 0f, 0f);
            }
            npc.netUpdate = true;
        }

        private void SetSub(NPC npc, int sub) {
            npc.ai[SkeletronAiSlots.HeadParamB] = sub;
            npc.netUpdate = true;
        }

        #endregion

        #region 运动与演出

        /// <summary>头部动线：退到玩家上方观赏位，随拍点轻晃；收势失力漂浮</summary>
        private void UpdateHeadMotion(SkeletronStateContext context, int sub) {
            NPC npc = context.Npc;
            if (sub == SubRecover) {
                npc.velocity *= 0.93f;
                npc.velocity.Y -= 0.015f;
                SettleRotation(npc, 0.07f);
                context.EyeFlame = MathHelper.Lerp(context.EyeFlame, 0.6f, 0.06f);
                return;
            }

            Vector2 want = context.Target.Center + new Vector2(0f, -430f);
            npc.velocity = (want - npc.Center) * 0.032f;
            //拍点点头顿挫叠加（指数衰减）
            npc.velocity.Y += nodImpulse;
            nodImpulse *= 0.78f;
            //观赏时的玩味歪头（随编队时钟慢摆）
            float tilt = (float)Math.Sin(context.OrbitClock * 0.045f) * 0.09f;
            npc.rotation = npc.rotation.AngleLerp(tilt, 0.1f);
            context.EyeFlame = MathHelper.Lerp(context.EyeFlame, sub >= SubClap1 ? 1.35f : 1.1f, 0.08f);
        }

        /// <summary>子相位前进沿的一次性声画（各端本地，服务端静默）；撞掌钝响由手部冲击计数广播承担</summary>
        private void PlaySubPhaseCue(SkeletronStateContext context, int sub) {
            if (VaultUtils.isServer) {
                return;
            }
            NPC npc = context.Npc;
            Vector2 meet = ClapMeetPoint(npc);

            switch (sub) {
                case SubClap1:
                    //开拍狞笑
                    SoundEngine.PlaySound(SoundID.ForceRoarPitched with { Volume = 0.55f, Pitch = 0.35f }, npc.Center);
                    break;
                case SubClap2:
                    SkeletronScreenEffects.PushShockRing(meet, 0.45f, 300f, 16);
                    SkeletronScreenEffects.PushShake(meet, 3.5f);
                    break;
                case SubClap3:
                    SkeletronScreenEffects.PushShockRing(meet, 0.6f, 380f, 18);
                    SkeletronScreenEffects.PushShake(meet, 4.5f);
                    SoundEngine.PlaySound(SoundID.ForceRoarPitched with { Volume = 0.5f, Pitch = 0.45f }, npc.Center);
                    break;
                case SubRecover:
                    //大拍收官
                    SkeletronScreenEffects.PushShockRing(meet, 0.9f, 560f, 24);
                    SkeletronScreenEffects.PushShake(meet, 7f);
                    SoundEngine.PlaySound(SoundID.ForceRoarPitched with { Volume = 0.6f, Pitch = 0.55f }, npc.Center);
                    break;
            }
        }

        #endregion
    }
}
