using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Projectiles;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Rendering;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.States
{
    /// <summary>
    /// 低血大招 三相灾变：轮转元素螺旋→法阵囚笼→三球汇聚总爆→力竭硬直；
    /// npc.ai[3]=螺旋基角种子
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)CultistStateIndex.Cataclysm, typeof(CultistStateContext))]
    internal class CultistCataclysmState : CultistStateBase
    {
        public override string StateName => "Cataclysm";
        public override CultistStateIndex StateIndex => CultistStateIndex.Cataclysm;

        private const int BlinkMoment = 8;
        private const int OrbMoment = 20;
        private const int SpiralStart = 46;
        private const int SpiralEnd = 256;
        private const int CageMoment = 268;
        private const int FinaleMoment = 386;
        private const int Duration = 452;

        public override void OnEnter(CultistStateContext context) {
            base.OnEnter(context);
            context.CataclysmUsed = true;
            if (!VaultUtils.isClient) {
                //大招是独角戏，分身退场
                CultistBossAI.DismissClones(context);
                context.Npc.ai[3] = Main.rand.Next(1000);
                context.Npc.netUpdate = true;
            }
        }

        public override ICultistState OnUpdate(CultistStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;
            Timer++;

            context.SkipDefaultHover = true;
            npc.velocity *= 0.92f;
            context.ElementAura = 1f;
            float veil = MathHelper.Clamp(Timer / 60f, 0f, 1f) * 0.7f;
            CultistScreenFX.DeclareVeil(npc.Center, veil, context.Element);

            //大招全程的身下舞台大阵（独角戏的仪式场，力竭期合拢）
            float sigilIn = MathHelper.Clamp((Timer - OrbMoment) / 30f, 0f, 1f);
            float sigilOut = Timer > Duration - 40 ? MathHelper.Clamp((Duration - Timer) / 34f, 0f, 1f) : 1f;
            context.StageSigilPos = npc.Center + new Vector2(0f, 130f);
            context.StageSigilRadius = 250f;
            context.StageSigilProgress = sigilIn * sigilOut;

            //就位
            if ((int)Timer == BlinkMoment && player.Alives()) {
                Vector2 target = player.Center + new Vector2(0f, -340f);
                if (!VaultUtils.isClient) {
                    CultistBossAI.BlinkTo(context, target);
                }
                else {
                    CultistRenderHelper.BlinkOut(npc.Center, context.Element);
                    CultistRenderHelper.BlinkIn(target, context.Element);
                }
            }

            //展开三球
            if ((int)Timer == OrbMoment) {
                if (!VaultUtils.isClient) {
                    for (int e = 0; e < 3; e++) {
                        Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, Vector2.Zero,
                            ModContent.ProjectileType<CultistElementOrb>(), 0, 0f, Main.myPlayer,
                            e, e, npc.whoAmI);
                    }
                }
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Zombie104 with { Volume = 1.1f, Pitch = -0.3f }, npc.Center);
                    SoundEngine.PlaySound(SoundID.Item123 with { Volume = 0.9f }, npc.Center);
                }
            }

            //幕一 轮转元素螺旋（70帧换一相）
            if (Timer >= SpiralStart && Timer < SpiralEnd) {
                context.CastPose = CultistPose.CastUp;
                context.CastGlow = 1f;
                int spiralAge = (int)Timer - SpiralStart;
                var element = (CultistElement)(spiralAge / 70 % 3);

                //相变节拍：换相帧的闪+和声+舞台阵回闪（元素轮转可听可见）
                int phaseAge = spiralAge % 70;
                if (phaseAge < 12) {
                    context.StageSigilFlash = 1f - phaseAge / 12f;
                }
                if (phaseAge == 0 && !VaultUtils.isServer) {
                    CultistScreenFX.PushFlash(0.16f, 9);
                    CultistRenderHelper.ChantVoice(npc.Center, 0.75f, -0.2f + (int)element * 0.25f);
                }

                if (spiralAge % 9 == 0 && !VaultUtils.isClient && player.Alives()) {
                    int damage = ProjDamage(npc, 42f, 29f);
                    float baseAngle = npc.ai[3] * 0.17f + spiralAge * 0.052f;
                    for (int arm = 0; arm < 3; arm++) {
                        Vector2 dir = (baseAngle + arm * MathHelper.TwoPi / 3f).ToRotationVector2();
                        SpawnSpiralBolt(context, npc, dir, element, damage);
                    }
                }
                if (spiralAge % 18 == 0 && !VaultUtils.isServer) {
                    CultistRenderHelper.CastBurst(npc.Center, -Vector2.UnitY, element, 0.7f);
                }
            }

            //幕二 法阵囚笼
            if ((int)Timer == CageMoment && !VaultUtils.isClient && player.Alives()) {
                int damage = ProjDamage(npc, 40f, 28f);
                for (int i = 0; i < 8; i++) {
                    float angle = MathHelper.TwoPi * i / 8f + npc.ai[3] * 0.09f;
                    Vector2 pos = player.Center + angle.ToRotationVector2() * 560f;
                    Projectile.NewProjectile(npc.GetSource_FromAI(), pos, Vector2.Zero,
                        ModContent.ProjectileType<CultistSigilProj>(), damage, 0f, Main.myPlayer,
                        (float)(i % 3), i * 9f, 2f);
                }
                //笼内双柱压顶
                int colDamage = ProjDamage(npc, 46f, 31f);
                for (int s = -1; s <= 1; s += 2) {
                    Vector2 ground = CultistElementBarrageState.FindGround(player.Center + new Vector2(s * 200f, 0f));
                    Projectile.NewProjectile(npc.GetSource_FromAI(), ground, Vector2.Zero,
                        ModContent.ProjectileType<CultistThunderColumn>(), colDamage, 0f, Main.myPlayer, 62f, 1400f);
                }
            }

            if (Timer > CageMoment && Timer < FinaleMoment) {
                context.CastPose = CultistPose.CastForward;
                context.CastGlow = 0.8f;
            }

            //终爆前36帧：三球反复鼓胀+符文重汇聚（"要出事了"的蓄势拍）
            if (Timer > FinaleMoment - 36 && Timer < FinaleMoment && !VaultUtils.isServer) {
                if ((int)Timer % 6 == 0) {
                    for (int e = 0; e < 3; e++) {
                        Projectile orb = CultistElementWheelState.FindOrbProj(npc, e);
                        if (orb != null) {
                            orb.localAI[1] = 10f;
                        }
                    }
                }
                CultistRenderHelper.ConvergeRunes(npc.Center, 460f, context.Element, 1.3f);
                CultistRenderHelper.ConvergeRunes(npc.Center, 460f, (CultistElement)(((int)context.Element + 1) % 3), 1f);
                context.StageSigilFlash = MathHelper.Clamp((Timer - (FinaleMoment - 36)) / 36f, 0f, 1f);
            }

            //幕三 汇聚总爆
            if ((int)Timer == FinaleMoment) {
                context.CastPose = CultistPose.Scream;
                CultistScreenFX.PushFlash(0.95f, 30);
                CultistScreenFX.Punch(npc.Center, 12f, 22, "CultistCataclysm");
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Zombie105 with { Volume = 1.25f, Pitch = -0.5f }, npc.Center);
                    SoundEngine.PlaySound(SoundID.Item122 with { Volume = 1.1f, Pitch = -0.4f }, npc.Center);
                    for (int e = 0; e < 3; e++) {
                        CultistRenderHelper.ElementImpact(npc.Center, (CultistElement)e, 2.2f);
                    }
                }
                if (!VaultUtils.isClient) {
                    //收掉三球（它们的OnKill自带爆点）
                    foreach (var p in Main.ActiveProjectiles) {
                        if (p.type == ModContent.ProjectileType<CultistElementOrb>() && (int)p.ai[2] == npc.whoAmI) {
                            p.Kill();
                        }
                    }
                }
            }

            //三重环形爆发（386/398/410），元素交替
            if (!VaultUtils.isClient && player.Alives()) {
                for (int ring = 0; ring < 3; ring++) {
                    if ((int)Timer == FinaleMoment + ring * 12) {
                        int damage = ProjDamage(npc, 44f, 30f);
                        int count = 18;
                        float speed = 4.6f + ring * 1.3f;
                        float offset = ring * 0.12f + npc.ai[3] * 0.05f;
                        for (int i = 0; i < count; i++) {
                            var element = (CultistElement)((i + ring) % 3);
                            Vector2 dir = (MathHelper.TwoPi * i / count + offset).ToRotationVector2();
                            SpawnFinaleBolt(npc, dir, speed, element, damage);
                        }
                    }
                }
            }

            //力竭
            if ((int)Timer == Duration - 40 && !VaultUtils.isClient) {
                context.StaggerTimer = 120;
                npc.ai[1] = 3f;
                npc.netUpdate = true;
            }
            if (Timer > Duration - 40) {
                context.CastPose = CultistPose.Stand;
                context.CastGlow = 0f;
            }

            if (Timer >= Duration) {
                return new CultistWeaveState();
            }
            return null;
        }

        /// <summary>螺旋臂弹：元素各语言（直线火/短摇冰/电蛇）</summary>
        private static void SpawnSpiralBolt(CultistStateContext context, NPC npc, Vector2 dir, CultistElement element, int damage) {
            var source = npc.GetSource_FromAI();
            Vector2 spawn = npc.Center + dir * 60f;
            switch (element) {
                case CultistElement.Fire:
                    Projectile.NewProjectile(source, spawn, dir * 5.6f,
                        ModContent.ProjectileType<CultistFireBolt>(), damage, 0f, Main.myPlayer, 0f, 0f);
                    break;
                case CultistElement.Ice:
                    Projectile.NewProjectile(source, spawn, dir,
                        ModContent.ProjectileType<CultistIceLance>(), damage, 0f, Main.myPlayer, 12f, 13f);
                    break;
                default:
                    Projectile.NewProjectile(source, spawn, dir * 6.4f,
                        ModContent.ProjectileType<CultistArcSpark>(), damage, 0f, Main.myPlayer,
                        (float)CultistElement.Thunder, 0f);
                    break;
            }
        }

        /// <summary>终爆环弹</summary>
        private static void SpawnFinaleBolt(NPC npc, Vector2 dir, float speed, CultistElement element, int damage) {
            var source = npc.GetSource_FromAI();
            switch (element) {
                case CultistElement.Fire:
                    Projectile.NewProjectile(source, npc.Center, dir * speed,
                        ModContent.ProjectileType<CultistFireBolt>(), damage, 0f, Main.myPlayer, 0f, 0f);
                    break;
                case CultistElement.Ice:
                    Projectile.NewProjectile(source, npc.Center + dir * 40f, dir,
                        ModContent.ProjectileType<CultistIceLance>(), damage, 0f, Main.myPlayer, 10f, speed + 8f);
                    break;
                default:
                    Projectile.NewProjectile(source, npc.Center, dir * (speed + 1.6f),
                        ModContent.ProjectileType<CultistArcSpark>(), damage, 0f, Main.myPlayer,
                        (float)CultistElement.Thunder, 0f);
                    break;
            }
        }
    }
}
