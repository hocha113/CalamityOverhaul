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
    /// 受控分裂-再聚合：两拍鼓缩→爆散五体围跳合围→王冠鸣响回聚→融合脉冲。
    /// 分裂期本体缩为核心球缓浮，可被集火(风险奖励窗口)
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)KingSlimeStateIndex.SplitSwarm, typeof(KingSlimeStateContext))]
    internal class KingSlimeSplitSwarmState : KingSlimeStateBase
    {
        public override string StateName => "SplitSwarm";
        public override KingSlimeStateIndex StateIndex => KingSlimeStateIndex.SplitSwarm;

        private const int PulseTime = 26;
        private const int SwarmTime = 235;
        private const int MergeWindow = 100;
        private const int ReformTime = 18;

        /// <summary>0鼓缩 1爆散围跳 2回聚 3重整</summary>
        private int phase;
        private int phaseTimer;
        private int lastSplitCount;

        public override void OnEnter(KingSlimeStateContext context) {
            base.OnEnter(context);
            phase = 0;
            phaseTimer = 0;
            lastSplitCount = 0;
        }

        private int CountSplits(KingSlimeStateContext context) {
            int type = ModContent.ProjectileType<BKSSplitSlimeProj>();
            int count = 0;
            foreach (var proj in Main.ActiveProjectiles) {
                if (proj.type == type && (int)proj.ai[0] == context.Npc.whoAmI) {
                    count++;
                }
            }
            return count;
        }

        public override IKingSlimeState OnUpdate(KingSlimeStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;
            Timer++;
            phaseTimer++;

            switch (phase) {
                case 0: {
                    //两拍鼓缩：吸气-压缩，第二拍更猛
                    npc.velocity.X *= 0.75f;
                    context.ContactDamageScale = 0f;
                    context.AuraMode = 1;
                    context.AuraProgress = phaseTimer / (float)PulseTime;

                    if (phaseTimer == 4 || phaseTimer == 16) {
                        context.StretchImpulse(phaseTimer == 4 ? 0.22f : 0.3f);
                        KingSlimeGelFX.SquishSound(npc.Center, 0.1f, 0.7f);
                    }
                    if (phaseTimer == 10 || phaseTimer == 22) {
                        context.SquashVelocity -= phaseTimer == 10 ? 0.26f : 0.36f;
                    }
                    if (!VaultUtils.isServer && phaseTimer % 3 == 0) {
                        KingSlimeGelFX.BubbleFizz(npc.Center, npc.width * 0.45f, 2);
                    }

                    if (phaseTimer >= PulseTime) {
                        BurstSplit(context);
                        phase = 1;
                        phaseTimer = 0;
                    }
                    break;
                }
                case 1: {
                    //围跳期：核心球缓浮避让，可被打
                    UpdateCore(context);

                    if (phaseTimer >= SwarmTime) {
                        //王冠(或核心)鸣响，召回分裂体
                        KingSlimeGelFX.CrownChime(npc.Center, 0.5f, 1f);
                        if (!VaultUtils.isClient) {
                            int type = ModContent.ProjectileType<BKSSplitSlimeProj>();
                            foreach (var proj in Main.ActiveProjectiles) {
                                if (proj.type == type && (int)proj.ai[0] == npc.whoAmI) {
                                    proj.ai[2] = 1f;
                                    proj.netUpdate = true;
                                }
                            }
                        }
                        lastSplitCount = CountSplits(context);
                        phase = 2;
                        phaseTimer = 0;
                    }
                    break;
                }
                case 2: {
                    //回聚：每融合一份，体积涨一档+脉冲
                    UpdateCore(context);

                    int now = CountSplits(context);
                    if (now < lastSplitCount) {
                        int merged = lastSplitCount - now;
                        lastSplitCount = now;
                        context.StretchImpulse(0.16f * merged);
                        //份额涨回
                        context.ScaleMul = MathHelper.Clamp(context.ScaleMul + 0.115f * merged, 0.42f, 1f);
                    }

                    if (now <= 0 || phaseTimer >= MergeWindow) {
                        //融合完成脉冲：环状凝胶+小冲击环(惩罚贴身)
                        if (!VaultUtils.isClient) {
                            Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, Vector2.Zero,
                                ModContent.ProjectileType<BKSShockwaveProj>(), 0, 0f, Main.myPlayer, 1f);
                            int dmg = (int)(npc.defDamage * 0.32f);
                            for (int i = 0; i < 10; i++) {
                                Vector2 dir = (MathHelper.TwoPi / 10f * i).ToRotationVector2();
                                Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, dir * 7.5f + new Vector2(0f, -2f),
                                    ModContent.ProjectileType<BKSGelGlobProj>(), dmg, 0f, Main.myPlayer, 0f);
                            }
                        }
                        SoundEngine.PlaySound(SoundID.QueenSlime with { Pitch = -0.3f, Volume = 0.9f, MaxInstances = 2 }, npc.Center);
                        KingSlimeGelFX.CameraPunch(npc.Center, 6f, 14, "BKSMerge");
                        phase = 3;
                        phaseTimer = 0;
                    }
                    break;
                }
                case 3: {
                    //重整：体积弹回
                    context.ContactDamageScale = 0f;
                    context.ScaleMul = MathHelper.Lerp(context.ScaleMul, 1f, 0.3f);
                    if (phaseTimer == 1) {
                        context.SquashVelocity += 0.4f;
                    }
                    if (phaseTimer >= ReformTime && !VaultUtils.isClient) {
                        return BackToHop(context);
                    }
                    break;
                }
            }

            if (Timer > 520 && !VaultUtils.isClient) {
                return BackToHop(context);
            }

            return null;
        }

        /// <summary>爆散：五体扇散，核心球化</summary>
        private void BurstSplit(KingSlimeStateContext context) {
            NPC npc = context.Npc;
            SoundEngine.PlaySound(SoundID.NPCDeath1 with { Pitch = -0.3f, Volume = 1.1f }, npc.Center);
            SoundEngine.PlaySound(SoundID.QueenSlime with { Pitch = 0.2f, Volume = 0.8f, MaxInstances = 2 }, npc.Center);
            KingSlimeGelFX.CameraPunch(npc.Center, 5f, 12, "BKSSplit");
            if (!VaultUtils.isServer) {
                KingSlimeGelFX.GelSplatter(npc.Center, -Vector2.UnitY, 14, 8f, 1.2f);
            }

            if (VaultUtils.isClient) {
                return;
            }
            int dmg = (int)(npc.defDamage * 0.42f);
            for (int i = 0; i < 5; i++) {
                //扇形上抛散开
                float angle = MathHelper.Lerp(-2.45f, -0.7f, i / 4f);
                Vector2 vel = angle.ToRotationVector2() * (8.5f + i % 2 * 2f);
                Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, vel,
                    ModContent.ProjectileType<BKSSplitSlimeProj>(), dmg, 0f, Main.myPlayer,
                    npc.whoAmI, i, 0f);
            }
        }

        /// <summary>核心球：缩体缓浮避让，暴露但机动</summary>
        private void UpdateCore(KingSlimeStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;

            context.ScaleMul = MathHelper.Lerp(context.ScaleMul, 0.42f, 0.2f);
            context.ContactDamageScale = 0f;
            context.SkipGravity = true;
            context.AuraMode = 2;
            context.AuraProgress = 0.7f;
            context.BodyOpacity = 0.92f;

            //缓浮：远离玩家+正弦浮沉
            if (player.Alives()) {
                float away = npc.Center.X < player.Center.X ? -1f : 1f;
                float bob = (float)Math.Sin(Main.GameUpdateCount * 0.05f) * 0.7f;
                Vector2 desired = new Vector2(away * 2.4f, bob - 0.4f);
                npc.velocity = Vector2.Lerp(npc.velocity, desired, 0.06f);
            }

            //核心滴漏
            if (!VaultUtils.isServer && Main.rand.NextBool(5)) {
                InnoVault.PRT.PRTLoader.NewParticle<PRT_BKSGelBead>(
                    npc.Center + Main.rand.NextVector2Circular(npc.width * 0.4f, npc.height * 0.4f),
                    new Vector2(0f, Main.rand.NextFloat(0.5f, 1.5f)),
                    KingSlimeGelFX.GelMid * 0.7f, Main.rand.NextFloat(0.4f, 0.8f))?.Configure(20);
            }
        }

        public override void OnExit(KingSlimeStateContext context) {
            base.OnExit(context);
            //保险：清掉残留分裂体
            if (!VaultUtils.isClient) {
                int type = ModContent.ProjectileType<BKSSplitSlimeProj>();
                foreach (var proj in Main.ActiveProjectiles) {
                    if (proj.type == type && (int)proj.ai[0] == context.Npc.whoAmI) {
                        proj.Kill();
                    }
                }
            }
        }
    }
}
