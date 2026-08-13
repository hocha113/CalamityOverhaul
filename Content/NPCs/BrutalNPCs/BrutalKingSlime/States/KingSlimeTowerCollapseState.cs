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
    /// 立塔倾倒(体积形变签名2)：聚缩→拔地成塔→反向蓄倾→轰然倒下化海啸→回抽重组。
    /// P2解锁；海啸伤害由潮波弹幕承载
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)KingSlimeStateIndex.TowerCollapse, typeof(KingSlimeStateContext))]
    internal class KingSlimeTowerCollapseState : KingSlimeStateBase
    {
        public override string StateName => "TowerCollapse";
        public override KingSlimeStateIndex StateIndex => KingSlimeStateIndex.TowerCollapse;

        private const int GatherTime = 18;
        private const int RiseTime = 20;
        private const int LeanTime = 30;
        private const int ToppleTime = 13;
        private const int RecoverTime = 32;

        /// <summary>0聚缩 1拔塔 2蓄倾 3倒下 4重组</summary>
        private int phase;
        private int phaseTimer;
        private int toppleDir;

        public override void OnEnter(KingSlimeStateContext context) {
            base.OnEnter(context);
            phase = 0;
            phaseTimer = 0;
            toppleDir = 0;
        }

        public override IKingSlimeState OnUpdate(KingSlimeStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;
            Timer++;
            phaseTimer++;

            switch (phase) {
                case 0: {
                    //聚缩：压低蓄势
                    npc.velocity.X *= 0.7f;
                    context.ContactDamageScale = 0f;
                    float t = phaseTimer / (float)GatherTime;
                    context.VisualSquash = MathHelper.Lerp(context.VisualSquash, 1f - 0.38f * t, 0.4f);
                    context.AuraMode = 1;
                    context.AuraProgress = t * 0.5f;

                    if (phaseTimer >= GatherTime && Grounded(npc)) {
                        phase = 1;
                        phaseTimer = 0;
                        SoundEngine.PlaySound(SoundID.Splash with { Pitch = -0.35f, Volume = 1f }, npc.Center);
                        KingSlimeGelFX.CameraPunch(npc.Bottom, 3.5f, 10, "BKSTowerRise", -Vector2.UnitY);
                    }
                    break;
                }
                case 1: {
                    //拔塔：弹性拔高，塔身垂流
                    npc.velocity.X = 0f;
                    context.SkipGravity = true;
                    float t = phaseTimer / (float)RiseTime;
                    float overshoot = 1f + 0.16f * MathF.Sin(MathHelper.Clamp(t * 1.3f, 0f, 1f) * MathHelper.Pi);
                    context.VisualSquash = MathHelper.Lerp(context.VisualSquash, 1.85f * overshoot, 0.28f);
                    context.AuraMode = 1;
                    context.AuraProgress = 0.5f + t * 0.5f;

                    if (!VaultUtils.isServer && Main.rand.NextBool(2)) {
                        Vector2 pos = npc.Bottom - new Vector2(Main.rand.NextFloat(-0.45f, 0.45f) * npc.width,
                            Main.rand.NextFloat(0.3f, 2.1f) * npc.height);
                        InnoVault.PRT.PRTLoader.NewParticle<PRT_BKSGelBead>(pos, new Vector2(0f, Main.rand.NextFloat(1f, 2.5f)),
                            KingSlimeGelFX.GelMid * 0.7f, Main.rand.NextFloat(0.5f, 0.9f))?.Configure(18);
                    }

                    if (phaseTimer >= RiseTime) {
                        toppleDir = DirToTarget(context);
                        phase = 2;
                        phaseTimer = 0;
                    }
                    break;
                }
                case 2: {
                    //蓄倾：先向反方向倾斜(counter-motion)，末4帧完全静止
                    npc.velocity.X = 0f;
                    context.SkipGravity = true;
                    context.VisualSquash = MathHelper.Lerp(context.VisualSquash, 1.8f, 0.2f);
                    float t = phaseTimer / (float)LeanTime;
                    //反倾-0.14rad，pow末段回中蓄满
                    float back = -0.14f * MathF.Sin(MathHelper.Clamp(t * 1.15f, 0f, 1f) * MathHelper.Pi);
                    context.BodyLean = back * toppleDir;
                    context.AuraMode = 1;
                    context.AuraProgress = 1f;

                    if (phaseTimer == LeanTime - 8) {
                        SoundEngine.PlaySound(SoundID.Item95 with { Pitch = -0.75f, Volume = 0.85f, MaxInstances = 3 }, npc.Center);
                    }

                    if (phaseTimer >= LeanTime) {
                        phase = 3;
                        phaseTimer = 0;
                    }
                    break;
                }
                case 3: {
                    //倒下：poly(8)极速旋倒
                    npc.velocity.X = 0f;
                    context.SkipGravity = true;
                    context.VisualSquash = MathHelper.Lerp(context.VisualSquash, 1.7f, 0.25f);
                    float t = MathHelper.Clamp(phaseTimer / (float)ToppleTime, 0f, 1f);
                    float ease = 1f - MathF.Pow(1f - t, 8f);
                    context.BodyLean = MathHelper.Lerp(0f, 1.42f, ease) * toppleDir;

                    //砸地帧
                    if (phaseTimer == ToppleTime) {
                        SlamGround(context);
                    }
                    if (phaseTimer >= ToppleTime + 4) {
                        phase = 4;
                        phaseTimer = 0;
                    }
                    break;
                }
                case 4: {
                    //重组：塔倒即扁，弹簧回弹站定
                    context.ContactDamageScale = 0f;
                    context.BodyLean = 0f;
                    float t = phaseTimer / (float)RecoverTime;
                    if (phaseTimer == 1) {
                        context.VisualSquash = 0.38f;
                        context.SquashVelocity = 0.12f;//回弹
                    }
                    context.BodyOpacity = MathHelper.Clamp(0.6f + t, 0.6f, 1f);

                    if (phaseTimer >= RecoverTime && !VaultUtils.isClient) {
                        return BackToHop(context);
                    }
                    break;
                }
            }

            if (Timer > 260 && !VaultUtils.isClient) {
                return BackToHop(context);
            }

            return null;
        }

        /// <summary>塔身砸地：海啸波+大冲击+震屏</summary>
        private void SlamGround(KingSlimeStateContext context) {
            NPC npc = context.Npc;
            Vector2 slamPoint = KingSlimeGelFX.FindGroundBelow(
                npc.Bottom + new Vector2(toppleDir * npc.height * 1.5f, -30f));

            SoundEngine.PlaySound(SoundID.Item167 with { Pitch = -0.7f, Volume = 0.9f, MaxInstances = 2 }, slamPoint);
            KingSlimeGelFX.ThudSound(slamPoint, 22f);
            KingSlimeGelFX.CameraPunch(slamPoint, 9f, 18, "BKSTowerSlam", new Vector2(toppleDir, 0.4f));
            if (!VaultUtils.isServer) {
                KingSlimeGelFX.LandingBurst(slamPoint, 20f, 1.5f);
            }

            if (VaultUtils.isClient) {
                return;
            }
            //海啸波沿倒向推进
            Projectile.NewProjectile(npc.GetSource_FromAI(), slamPoint - new Vector2(0f, 20f),
                new Vector2(toppleDir * (context.IsDeathMode ? 13f : 11f), 0f),
                ModContent.ProjectileType<BKSTideWaveProj>(), (int)(npc.defDamage * 0.55f), 0f, Main.myPlayer,
                -1f, 1f, 74f);
            //大冲击环
            Projectile.NewProjectile(npc.GetSource_FromAI(), slamPoint, Vector2.Zero,
                ModContent.ProjectileType<BKSShockwaveProj>(), 0, 0f, Main.myPlayer, 2f);
        }

        public override void OnExit(KingSlimeStateContext context) {
            base.OnExit(context);
            context.BodyLean = 0f;
        }
    }
}
