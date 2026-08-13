using CalamityOverhaul.Content.NPCs.ScrapCommanders.Core;
using CalamityOverhaul.Content.NPCs.ScrapCommanders.Projectiles;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.ScrapCommanders.States
{
    /// <summary>
    /// 甩壳重构（55% 转阶段，可玩的演出而非无敌暂停）：
    /// 怒吼蓄力 → 四工具炸开甩飞成四发一次性弹幕 → 头独自裸奔 3 秒（受击 ×1.25 奖励压血）
    /// 同时磁力吸收场上废钢堆 → 工具逐件磁装回位，进入统帅模式
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)ScrapStateIndex.PhaseTransition, typeof(ScrapStateContext))]
    internal class ScrapPhaseTransitionState : ScrapStateBase
    {
        public override string StateName => "PhaseTransition";
        public override ScrapStateIndex StateIndex => ScrapStateIndex.PhaseTransition;

        //==================== 时序 ====================

        private const int BlastBeat = 30;
        private const int ReassembleBeat = 120;
        private const int StateEnd = 152;

        private bool roared;
        private bool blasted;
        private bool reassembled;

        public override IScrapState OnUpdate(ScrapStateContext ctx) {
            NPC npc = ctx.Npc;
            ScrapCommander owner = ctx.Owner;
            int t = (int)Timer;

            npc.velocity *= 0.92f;

            if (t < BlastBeat) {
                //==================== 怒吼蓄力 ====================
                if (!roared) {
                    roared = true;
                    SoundEngine.PlaySound(SoundID.Roar with { Volume = 0.9f, Pitch = -0.35f, MaxInstances = 1 }, npc.Center);
                }
                //链条挣动：四臂高频小冲量
                if (t % 5 == 0) {
                    for (int i = 0; i < ScrapCommander.ArmCount; i++) {
                        owner.ImpulseArm(i, new Vector2(
                            MathF.Sin(t * 0.9f + i * 1.7f) * 2.2f, -MathF.Abs(MathF.Cos(t * 0.7f + i)) * 1.4f));
                    }
                }
                ctx.EyeScan = (t % 10) / 10f;
                if (t % 6 == 0) {
                    ShakeNearby(npc.Center, 0.8f);
                }
                Timer++;
                return null;
            }

            if (t < ReassembleBeat) {
                //==================== 甩壳裸奔：高速游走 + 来袭废料压力 ====================
                if (!blasted) {
                    blasted = true;
                    BlastBeatBurst(ctx, owner, npc);
                    owner.EnsureMagnetFieldProj();
                }
                //裸奔窗：工具没了，受击加深
                for (int i = 0; i < ScrapCommander.ArmCount; i++) {
                    ctx.ToolAlpha[i] = 0f;
                }
                ctx.BareWindow = true;
                ctx.AfterimageStrength = MathF.Max(ctx.AfterimageStrength, 0.6f);

                //裸头绕玩家高速游走：压血窗不是站桩靶
                float orbit = t * 0.05f + owner.Seed;
                Vector2 anchor = ctx.Target.Center + orbit.ToRotationVector2() * 360f;
                GlideToward(ctx, anchor, 0.1f, 19f, 0.16f);
                LeanByVelocity(npc, 0.14f);

                //磁力回收：60 帧起整座拽飞废钢堆 + 场边来袭件逼走位
                if (t >= 60) {
                    ctx.MagnetGlow = MathHelper.Clamp((t - 60) / 20f, 0f, 1f);
                    ctx.MagnetPull = 1f;
                    if (!VaultUtils.isClient) {
                        if (t == 60) {
                            ScrapJunkPile.SuckAll();
                        }
                        //来袭废料流：从场边拽进统帅，穿场即压力
                        if (t % 18 == 0) {
                            float ang = Main.rand.NextFloat(MathHelper.TwoPi);
                            Vector2 from = npc.Center + ang.ToRotationVector2() * 860f;
                            int damage = ScrapDirector.ScaleProjectileDamage(npc, (22f, 18f));
                            Projectile.NewProjectile(npc.GetSource_FromAI(), from,
                                (npc.Center - from).SafeNormalize(Vector2.UnitX) * 8f,
                                ModContent.ProjectileType<ScrapDebris>(), damage, 2f,
                                Main.myPlayer, npc.whoAmI, -2f);
                        }
                    }
                    //吸入的废料火星流
                    if (!Main.dedServ && t % 3 == 0) {
                        Vector2 from = npc.Center + Main.rand.NextVector2Unit() * Main.rand.NextFloat(140f, 320f);
                        PRTLoader.NewParticle<PRT_Spark>(from, (npc.Center - from) * 0.06f,
                            ScrapCommander.WeldOrange * 0.6f, Main.rand.NextFloat(0.4f, 0.7f))
                            ?.Configure(false, 16);
                    }
                }
                Timer++;
                return null;
            }

            //==================== 磁装回位 ====================
            if (!reassembled) {
                reassembled = true;
                ctx.Phase = 2;
                SoundEngine.PlaySound(SoundID.DD2_EtherianPortalOpen with { Volume = 0.55f, Pitch = 0.1f, MaxInstances = 1 }, npc.Center);
                //工具从四周环位飞回：先散点再强弹簧收拢
                for (int i = 0; i < ScrapCommander.ArmCount; i++) {
                    float ang = owner.Seed + i * MathHelper.PiOver2 + 0.7f;
                    owner.PlaceArm(i, npc.Center + ang.ToRotationVector2() * 330f);
                }
                ShakeNearby(npc.Center, 2.5f);
            }
            ctx.MagnetGlow = 1f;
            //逐件到位的咔哒
            int slot = (t - ReassembleBeat) / 7;
            if ((t - ReassembleBeat) % 7 == 0 && slot < ScrapCommander.ArmCount) {
                SoundEngine.PlaySound(SoundID.Item37 with {
                    Volume = 0.55f,
                    Pitch = -0.35f + slot * 0.15f,
                    MaxInstances = 3
                }, npc.Center);
            }

            Timer++;
            if (t >= StateEnd) {
                ctx.AttackCooldown = 55;
                if (!VaultUtils.isClient) {
                    return new ScrapHubState();
                }
            }
            return null;
        }

        /// <summary>甩壳拍：四工具化作带预警的一次性弹幕炸飞出去</summary>
        private static void BlastBeatBurst(ScrapStateContext ctx, ScrapCommander owner, NPC npc) {
            SoundEngine.PlaySound(SoundID.Item37 with { Volume = 0.9f, Pitch = -0.6f, MaxInstances = 1 }, npc.Center);
            SoundEngine.PlaySound(SoundID.NPCHit4 with { Volume = 0.8f, Pitch = -0.5f, MaxInstances = 2 }, npc.Center);
            ShakeNearby(npc.Center, 4.5f);
            ScrapSiegeScreen.TriggerImpactFrame(0.35f);
            ScrapVfx.MetalExplosion(npc.Center, 1.2f);
            if (!Main.dedServ) {
                for (int k = 0; k < 14; k++) {
                    PRTLoader.NewParticle<PRT_Spark>(npc.Center + Main.rand.NextVector2Circular(40f, 40f),
                        Main.rand.NextVector2Circular(6f, 6f),
                        Color.Lerp(ScrapCommander.WeldOrange, Color.White, Main.rand.NextFloat(0.5f)),
                        Main.rand.NextFloat(0.6f, 1.1f))?.Configure(true, Main.rand.Next(12, 20));
                }
                PRTLoader.NewParticle<PRT_GhostRainMist>(npc.Center, new Vector2(0f, -0.4f),
                    ScrapCommander.SmokeGray, 1f)?.Configure(50);
            }

            if (VaultUtils.isClient) {
                return;
            }
            int damage = ScrapDirector.ScaleProjectileDamage(npc, ScrapDirector.ArmStrikeDamage);
            for (int i = 0; i < ScrapCommander.ArmCount; i++) {
                Vector2 outward = (owner.GetArmPos(i) - npc.Center).SafeNormalize(
                    (i % 2 == 0 ? -Vector2.UnitX : Vector2.UnitX));
                Projectile.NewProjectile(npc.GetSource_FromAI(), owner.GetArmPos(i),
                    outward * 9f, ModContent.ProjectileType<ScrapFlungTool>(), damage, 4f,
                    Main.myPlayer, i);
            }
        }

    }
}
