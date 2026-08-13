using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalKingSlime.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalKingSlime.Projectiles;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalKingSlime.Rendering;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalKingSlime.States
{
    /// <summary>
    /// 凝胶迫击：深蹲蓄势→垂直冲天→顶点悬滞环状泼洒(P2追加瞄准扇)→重锤落地
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)KingSlimeStateIndex.GelMortar, typeof(KingSlimeStateContext))]
    internal class KingSlimeGelMortarState : KingSlimeStateBase
    {
        public override string StateName => "GelMortar";
        public override KingSlimeStateIndex StateIndex => KingSlimeStateIndex.GelMortar;

        private const int ChargeTime = 26;
        private const int ApexHold = 9;

        /// <summary>0蹲蓄 1升空 2顶点泼洒 3重落</summary>
        private int phase;
        private int phaseTimer;
        private bool volley2Fired;

        public override void OnEnter(KingSlimeStateContext context) {
            base.OnEnter(context);
            phase = 0;
            phaseTimer = 0;
            volley2Fired = false;
        }

        public override IKingSlimeState OnUpdate(KingSlimeStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;
            Timer++;
            phaseTimer++;

            switch (phase) {
                case 0: {
                    //深蹲蓄势：比普通跳更深，体表冒泡，伤害关闭
                    npc.velocity.X *= 0.7f;
                    context.ContactDamageScale = 0f;
                    float t = phaseTimer / (float)ChargeTime;
                    context.VisualSquash = MathHelper.Lerp(context.VisualSquash, 1f - 0.52f * MathF.Pow(t, 2.4f), 0.4f);
                    context.AuraMode = 1;
                    context.AuraProgress = t;

                    if (!VaultUtils.isServer && phaseTimer % 4 == 0) {
                        KingSlimeGelFX.BubbleFizz(npc.Center, npc.width * 0.4f, 2);
                    }
                    if (phaseTimer == ChargeTime - 6) {
                        SoundEngine.PlaySound(SoundID.Item95 with { Pitch = -0.6f, Volume = 0.7f, MaxInstances = 3 }, npc.Center);
                    }

                    if (phaseTimer >= ChargeTime && Grounded(npc)) {
                        //垂直冲天
                        float vy = -19.5f;
                        float dy = player.Center.Y - npc.Center.Y;
                        if (dy < -180f) {
                            vy -= MathHelper.Clamp(-dy * 0.01f, 0f, 4f);
                        }
                        LaunchHop(npc, MathHelper.Clamp((player.Center.X - npc.Center.X) / 90f, -3.5f, 3.5f), vy);
                        context.StretchImpulse(0.5f);
                        KingSlimeGelFX.SquishSound(npc.Bottom, -0.35f, 1f);
                        KingSlimeGelFX.CameraPunch(npc.Bottom, 3.5f, 10, "BKSMortarJump", -Vector2.UnitY);
                        phase = 1;
                        phaseTimer = 0;
                    }
                    break;
                }
                case 1: {
                    //升空：接近顶点(纵速衰减)转入悬滞
                    if (npc.velocity.Y > -1.5f) {
                        phase = 2;
                        phaseTimer = 0;
                    }
                    break;
                }
                case 2: {
                    //顶点悬滞：微膨胀+一轮环状泼洒
                    context.SkipGravity = true;
                    npc.velocity *= 0.72f;

                    if (phaseTimer == 2) {
                        context.StretchImpulse(0.22f);
                        FireRingVolley(context);
                    }
                    //P2 second：瞄准扇
                    if (context.IsPhase2 && !volley2Fired && phaseTimer == ApexHold - 2) {
                        volley2Fired = true;
                        FireAimedFan(context);
                    }

                    if (phaseTimer >= ApexHold) {
                        //重落：预定中级冲击波
                        context.PendingLandingShockwave = 1;
                        context.LandingSplashMul = 1.4f;
                        npc.velocity = new Vector2(MathHelper.Clamp((player.Center.X - npc.Center.X) / 80f, -4f, 4f), 6f);
                        phase = 3;
                        phaseTimer = 0;
                    }
                    break;
                }
                case 3: {
                    //重落：追加下坠力，快而狠
                    npc.velocity.Y += 0.28f;
                    if (context.JustLanded || (phaseTimer > 8 && Grounded(npc))) {
                        if (!VaultUtils.isClient) {
                            return BackToHop(context);
                        }
                    }
                    break;
                }
            }

            if (Timer > 300 && !VaultUtils.isClient) {
                return BackToHop(context);
            }

            return null;
        }

        /// <summary>顶点环状泼洒：重力弧线珠幕，两发带滞留池</summary>
        private void FireRingVolley(KingSlimeStateContext context) {
            NPC npc = context.Npc;
            SoundEngine.PlaySound(SoundID.Splash with { Pitch = -0.15f, Volume = 0.95f, MaxInstances = 3 }, npc.Center);
            if (!VaultUtils.isServer) {
                KingSlimeGelFX.GelSplatter(npc.Center, -Vector2.UnitY, 10, 6f, 1.1f);
            }
            if (VaultUtils.isClient) {
                return;
            }

            int count = context.IsDeathMode ? 15 : 12;
            int dmg = (int)(npc.defDamage * 0.38f);
            for (int i = 0; i < count; i++) {
                float t = i / (float)(count - 1);
                float vx = MathHelper.Lerp(-10f, 10f, t) + Main.rand.NextFloat(-0.7f, 0.7f);
                float vy = -Main.rand.NextFloat(4.5f, 8.5f);
                //两发标记生成滞留池
                float poolFlag = i == count / 3 || i == count * 2 / 3 ? 1f : 0f;
                Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, new Vector2(vx, vy),
                    ModContent.ProjectileType<BKSGelGlobProj>(), dmg, 0f, Main.myPlayer, poolFlag);
            }
        }

        /// <summary>P2追加：朝目标预测位的密集扇</summary>
        private void FireAimedFan(KingSlimeStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;
            SoundEngine.PlaySound(SoundID.Splash with { Pitch = 0.2f, Volume = 0.85f, MaxInstances = 3 }, npc.Center);
            if (VaultUtils.isClient || !player.Alives()) {
                return;
            }

            Vector2 predicted = player.Center + player.velocity * 22f;
            Vector2 baseDir = (predicted - npc.Center).SafeNormalize(Vector2.UnitY);
            int dmg = (int)(npc.defDamage * 0.38f);
            for (int i = -2; i <= 2; i++) {
                Vector2 dir = baseDir.RotatedBy(i * 0.14f);
                Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, dir * 11.5f,
                    ModContent.ProjectileType<BKSGelGlobProj>(), dmg, 0f, Main.myPlayer, 0f);
            }
        }
    }
}
