using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Projectiles;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.States
{
    /// <summary>
    /// 合相祭仪:黄道环信标连珠,浑天仪三环共面,140 帧蓄力(重创可打断=拆台抉择)后放阶段大祭<br/>
    /// P0 风暴螺旋(走廊缺口旋转) P1 幻星降世(真伪三星) P2 环系崩落(天降星雨留巷)<br/>
    /// P3 冕暴轮转(缺口扇区旋转) P4 月瞳双凝视(先后两束对向扫天)<br/>
    /// 公平阀:放招前 12 帧纯静默;各脚本缺口均为具名常量;结束长喘息
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)CultistStateIndex.Conjunction, typeof(CultistStateContext))]
    internal class CultistConjunctionState : CultistStateBase
    {
        public override string StateName => "CultistConjunction";
        public override CultistStateIndex StateIndex => CultistStateIndex.Conjunction;

        private const int ChargeFrames = 140;
        /// <summary>P0 风暴螺旋走廊半角(rad)与转速</summary>
        private const float SpiralCorridorHalf = 0.52f;
        private const float SpiralCorridorDrift = 0.0055f;
        /// <summary>P2 天降星雨:7 巷,连空 2 巷,巷距 px</summary>
        private const int RainLanes = 7;
        private const int RainGapLanes = 2;
        private const float RainLaneSpacing = 250f;
        /// <summary>P3 冕暴:轮转缺口半角(rad)与每轮进角</summary>
        private const float CoronaGapHalf = 0.55f;
        private const float CoronaGapStep = 0.30f;

        private int ReleaseEnd => 140 + (StatePhase(ctxCache) == 4 ? 396 : 300);
        private CultistStateContext ctxCache;

        private static int StatePhase(CultistStateContext context) => context?.Phase ?? 0;

        public override void OnEnter(CultistStateContext context) {
            base.OnEnter(context);
            ctxCache = context;
            NPC npc = context.Npc;
            context.ConjunctionLifeStart = npc.life;
            npc.velocity = Vector2.Zero;
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Zombie105 with { Volume = 1f, Pitch = -0.5f }, npc.Center);
            }
        }

        public override ICultistState OnUpdate(CultistStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;
            ctxCache = context;
            Timer++;

            SetPose(npc, 13);
            FaceTarget(npc, player.Center);
            context.OrreryGlow = 1f;

            Vector2 hover = context.ArenaCenter + new Vector2(0f, -400f);
            CultistMotion.SpringHover(npc, hover, 0.014f, 0.10f, 15f);

            Color core = CultistMotion.PhaseCore(context.Phase);

            if (Timer <= ChargeFrames) {
                UpdateCharge(context, npc, core);
                //拆台阀(权威端):蓄力窗内重创即失衡
                if (!VaultUtils.isClient
                    && context.ConjunctionLifeStart - npc.life >= npc.lifeMax * CultistStateContext.ConjunctionBreakRatio) {
                    context.AddAlign(-130f);
                    return new CultistStaggerState();
                }
            }
            else {
                UpdateRelease(context, npc, player, core);
                //释放期充能缓排空(权威端)
                if (!VaultUtils.isClient) {
                    context.AlignCharge = MathHelper.Max(0f,
                        CultistStateContext.AlignMax * (1f - (Timer - ChargeFrames) / 300f));
                }
            }

            if (VaultUtils.isClient) {
                return null;
            }
            if (Timer >= ReleaseEnd) {
                context.AlignCharge = 0f;
                return new CultistCoilState(90);
            }
            return null;
        }

        /// <summary>蓄力窗:符文向心汇聚,末 12 帧纯静默(爆发前的吸气)</summary>
        private void UpdateCharge(CultistStateContext context, NPC npc, Color core) {
            context.PushAura(Timer / (float)ChargeFrames, core);
            context.BodyHot = MathHelper.Max(context.BodyHot, Timer / (float)ChargeFrames * 0.8f);
            CultistScreenFX.SetVeil(0.35f * Timer / ChargeFrames, npc.Center, core, 760f);

            bool silence = Timer > ChargeFrames - 12;
            if (!silence && Timer % 6 == 0) {
                //向心符文:从外围被拽进身体
                Vector2 pos = npc.Center + Main.rand.NextVector2Unit() * Main.rand.NextFloat(160f, 300f);
                CultistMotion.RuneBurst(pos, core, 1, -6f);
            }
            if ((Timer == 24 || Timer == 74 || Timer == 116) && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item117 with {
                    Volume = 0.6f,
                    Pitch = -0.4f + Timer / (float)ChargeFrames * 0.9f
                }, npc.Center);
            }
            //释放拍
            if ((int)Timer == ChargeFrames) {
                CultistScreenFX.PushFlash(0.6f);
                CultistMotion.Shake(npc.Center, 10f, 18);
                CultistMotion.SigilCommitFX(npc.Center, core, 2f);
                context.ScalePulse = 1.16f;
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Zombie105 with { Volume = 1.3f, Pitch = -0.2f }, npc.Center);
                    SoundEngine.PlaySound(SoundID.Item122 with { Volume = 1f, Pitch = -0.4f }, npc.Center);
                }
            }
        }

        /// <summary>释放脚本:12 帧静默缓冲后按阶段放祭(权威端发射)</summary>
        private void UpdateRelease(CultistStateContext context, NPC npc, Player player, Color core) {
            context.PushAura(1f, core);
            float t = Timer - ChargeFrames;
            if (t < 12f) {
                return;   //转拍缓冲:释放闪后先静一拍再落雨
            }
            Projectile planet = CultistEclipseState.FindPlanet(npc.whoAmI);
            Vector2 origin = planet?.Center ?? context.ArenaCenter;
            float planetR = planet?.ModProjectile is CultistPlanetProj pp ? pp.VisRadius * planet.scale : 220f;

            switch (context.Phase) {
                case 0: {
                    //风暴螺旋:三臂星珠自星面旋出,走廊缺口旋转(具名常量,发射循环直读)
                    if (!VaultUtils.isClient && Timer % 4 == 0) {
                        float corridor = SpiralCorridorDrift * Timer;
                        float baseAngle = t * 0.09f;
                        for (int arm = 0; arm < 3; arm++) {
                            float angle = baseAngle + arm * MathHelper.TwoPi / 3f;
                            if (Math.Abs(MathHelper.WrapAngle(angle - corridor)) < SpiralCorridorHalf) {
                                continue;
                            }
                            Vector2 dir = angle.ToRotationVector2();
                            Projectile.NewProjectile(npc.GetSource_FromAI(), origin + dir * planetR * 0.9f,
                                dir * 5.2f, ModContent.ProjectileType<CultistStarBead>(), 40, 0f,
                                Main.myPlayer, context.Phase);
                        }
                    }
                    break;
                }
                case 1: {
                    //幻星降世:两颗幻象星与真身同台漂移,唯真身咬人(实体度=识真线索)
                    if (!VaultUtils.isClient && (int)t == 14) {
                        for (int i = 1; i <= 2; i++) {
                            Projectile.NewProjectile(npc.GetSource_FromAI(),
                                context.ArenaCenter + new Vector2(i == 1 ? -560f : 560f, -240f), Vector2.Zero,
                                ModContent.ProjectileType<CultistPlanetProj>(), 60, 0f, Main.myPlayer,
                                CultistPlanetProj.KindNebula, npc.whoAmI, i * 10f);
                        }
                    }
                    //稀疏星珠垫压
                    if (!VaultUtils.isClient && Timer % 46 == 0) {
                        Vector2 dir = (player.Center - origin).SafeNormalize(Vector2.UnitY);
                        Projectile.NewProjectile(npc.GetSource_FromAI(), origin + dir * planetR * 0.9f,
                            dir * 6f, ModContent.ProjectileType<CultistStarBead>(), 40, 0f,
                            Main.myPlayer, context.Phase);
                    }
                    //收场:幻象散场(真身留任)
                    if (!VaultUtils.isClient && Timer == ReleaseEnd - 24) {
                        CultistPlanetProj.DismissPhantoms(npc.whoAmI);
                    }
                    break;
                }
                case 2: {
                    //环系崩落:晶珠沿环面双向流出+天降星雨留巷(巷缺口具名声明)
                    if (!VaultUtils.isClient && Timer % 5 == 0) {
                        float tilt = -0.35f;   //与 TechStardust uTilt 同源:环面即弹道
                        Vector2 dir = tilt.ToRotationVector2() * ((int)(t / 5f) % 2 == 0 ? 1f : -1f);
                        Projectile.NewProjectile(npc.GetSource_FromAI(), origin + dir * planetR * 1.15f,
                            dir * 6.5f, ModContent.ProjectileType<CultistStarBead>(), 40, 0f,
                            Main.myPlayer, context.Phase);
                    }
                    if (!VaultUtils.isClient && Timer % 32 == 0) {
                        int volley = (int)(t / 32f);
                        int gapStart = volley % RainLanes;
                        for (int lane = 0; lane < RainLanes; lane++) {
                            int d = ((lane - gapStart) % RainLanes + RainLanes) % RainLanes;
                            if (d < RainGapLanes) {
                                continue;
                            }
                            Vector2 pos = new(context.ArenaCenter.X + (lane - RainLanes / 2) * RainLaneSpacing,
                                context.ArenaCenter.Y - 940f);
                            Projectile.NewProjectile(npc.GetSource_FromAI(), pos, new Vector2(0f, 5f),
                                ModContent.ProjectileType<CultistStarBead>(), 40, 0f,
                                Main.myPlayer, context.Phase);
                        }
                    }
                    break;
                }
                case 3: {
                    //冕暴轮转:冕矛全周辐射,缺口扇区逐轮进角(缺口=活口,追着走)
                    if (!VaultUtils.isClient && planet != null && Timer % 22 == 0) {
                        int volley = (int)(t / 22f);
                        float gapAngle = volley * CoronaGapStep;
                        for (int i = 0; i < 12; i++) {
                            float angle = i * MathHelper.TwoPi / 12f + volley * 0.13f;
                            if (Math.Abs(MathHelper.WrapAngle(angle - gapAngle)) < CoronaGapHalf) {
                                continue;
                            }
                            Projectile.NewProjectile(npc.GetSource_FromAI(), origin,
                                angle.ToRotationVector2() * 0.01f,
                                ModContent.ProjectileType<CultistCoronaLance>(), 44, 0f,
                                Main.myPlayer, angle, planet.whoAmI, context.Phase);
                        }
                    }
                    break;
                }
                default: {
                    //月瞳双凝视:先后两束对向扫天,睁眼全程
                    context.PupilOpen = 1f;
                    if (!VaultUtils.isClient && planet != null) {
                        if ((int)t == 14) {
                            float playerAngle = (player.Center - origin).ToRotation();
                            Projectile.NewProjectile(npc.GetSource_FromAI(), origin, Vector2.Zero,
                                ModContent.ProjectileType<CultistGazeBeam>(), 52, 0f, Main.myPlayer,
                                playerAngle - 0.7f, 0.021f, planet.whoAmI);
                        }
                        if ((int)t == 206) {
                            float playerAngle = (player.Center - origin).ToRotation();
                            Projectile.NewProjectile(npc.GetSource_FromAI(), origin, Vector2.Zero,
                                ModContent.ProjectileType<CultistGazeBeam>(), 52, 0f, Main.myPlayer,
                                playerAngle + 0.7f, -0.021f, planet.whoAmI);
                        }
                    }
                    break;
                }
            }
        }

        public override void OnExit(CultistStateContext context) {
            //收势清幻象兜底(P1 提前散场已做,这里防打断残留)
            if (!VaultUtils.isClient) {
                CultistPlanetProj.DismissPhantoms(context.Npc.whoAmI);
            }
        }
    }
}
