using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEyeOfCthulhu.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEyeOfCthulhu.Projectiles;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEyeOfCthulhu.States
{
    /// <summary>
    /// 猩红血漩涡（低血大招）：吞尽战场血雾→自旋喷吐旋臂血棘弹幕+仆从环卫，<br/>
    /// 中场喘息换旋向，终幕长吸气三连变轨暴冲，力竭长喘收场<br/>
    /// 三连冲每段都有自己的瞄准拍：车道在拍首冻结并整条画出折线，起跑与变轨都沿它飞，不再中途追人
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)EocStateIndex.Maelstrom, typeof(EocStateContext))]
    internal class EocMaelstromState : EocStateBase
    {
        public override string StateName => "EocMaelstrom";
        public override EocStateIndex StateIndex => EocStateIndex.Maelstrom;
        public override bool AllowFogStep => false;

        private const int GatherTime = 70;
        private const int SpiralATime = 150;
        private const int LullTime = 40;
        private const int SpiralBTime = 150;
        private const int FinaleReelTime = 20;
        /// <summary>每段冲刺前的瞄准拍：刹车余速里回首锁线、车道定格</summary>
        private const int DashAimBeat = 12;
        /// <summary>起跑到刹车前的满速帧数</summary>
        private const int DashFlight = 20;
        private const int DashBrake = 8;
        private const int DashCycle = DashAimBeat + DashFlight + DashBrake;
        private const int DashCount = 3;
        private const int BlinkLead = 5;
        private const float KinkLateral = 300f;
        private const float KinkSpeedMul = 1.1f;
        private const int ExhaustTime = 62;

        private int SpiralAEnd => GatherTime + SpiralATime;
        private int LullEnd => SpiralAEnd + LullTime;
        private int SpiralBEnd => LullEnd + SpiralBTime;
        private int ReelEnd => SpiralBEnd + FinaleReelTime;
        private int DashEnd => ReelEnd + DashCycle * DashCount;
        private int TotalTime => DashEnd + ExhaustTime;

        private int SpikeInterval => Context.IsAsuraMode ? 7 : 9;
        private float SpikeCurl => 0.0069f;
        private float FinaleDashSpeed => Context.IsAsuraMode ? 62f : 58f;

        private EocStateContext Context;
        private float volleyAngle;
        private int dashIndex;
        private bool dashBlinked;
        private bool dashKinked;
        private EocDashPlan dashPlan;

        public override void OnEnter(EocStateContext context) {
            base.OnEnter(context);
            Context = context;
            volleyAngle = 0f;
            dashIndex = -1;
            dashBlinked = false;
            dashKinked = false;
            dashPlan = default;
        }

        /// <summary>三连冲拐点侧固定左右交替，各端无需同步即可同构建路</summary>
        private static float DashSide(int index) => index % 2 == 0 ? 1f : -1f;

        public override IEocState OnUpdate(EocStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;
            DisableContactDamage(npc);

            EocScreenFX.PushVignette(0.45f);

            if (Timer <= GatherTime) {
                UpdateGather(npc, player, context);
            }
            else if (Timer <= SpiralAEnd) {
                UpdateSpiral(npc, player, context, mirror: false,
                    (Timer - GatherTime) / (float)SpiralATime);
            }
            else if (Timer <= LullEnd) {
                UpdateLull(npc, player, context);
            }
            else if (Timer <= SpiralBEnd) {
                UpdateSpiral(npc, player, context, mirror: true,
                    (Timer - LullEnd) / (float)SpiralBTime);
            }
            else if (Timer <= ReelEnd) {
                UpdateFinaleReel(npc, player, context);
            }
            else if (Timer < DashEnd) {
                //严格小于：Timer==DashEnd 落入力竭，防第四段幽灵起跑
                UpdateTripleDash(npc, player, context);
            }
            else {
                UpdateExhaust(npc, player, context);
            }

            Timer++;

            if (Timer >= TotalTime) {
                if (VaultUtils.isClient) {
                    return null;
                }
                return new EocVeilHoverState(70);
            }

            return null;
        }

        #region 吞雾聚势
        private void UpdateGather(NPC npc, Player player, EocStateContext context) {
            Vector2 anchor = player.Center + new Vector2(0f, -280f);
            EocMotion.SpringHover(npc, anchor, 0.016f, 0.1f, 22f);
            FaceTarget(npc, player.Center, 0.3f);
            float progress = Timer / (float)GatherTime;
            context.SetChargeState(4, progress);
            context.PushIris(progress, EocMotion.IrisRed);

            if (Timer == 2 && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Roar with { Volume = 1.2f, Pitch = -0.35f }, npc.Center);
            }

            //吞尽在场血雾：雾团被拽向眼球并加速枯竭
            if (!VaultUtils.isClient && Timer % 6 == 0) {
                int fogType = ModContent.ProjectileType<EocFogCloud>();
                foreach (Projectile proj in Main.ActiveProjectiles) {
                    if (proj.type != fogType) {
                        continue;
                    }
                    proj.velocity = (npc.Center - proj.Center).SafeNormalize(Vector2.Zero) * 7f;
                    proj.timeLeft = Math.Min(proj.timeLeft, 130);
                    proj.netUpdate = true;
                }
            }

            //环卫仆从
            if (Timer == 30 && !VaultUtils.isClient) {
                for (int i = 0; i < 3; i++) {
                    ServantOfCthulhuAI.SpawnFormationServant(npc,
                        npc.Center + Main.rand.NextVector2CircularEdge(205f, 205f),
                        ServantOfCthulhuAI.ModeOrbit, 0, i, Main.rand.NextFloat(MathHelper.TwoPi));
                }
            }

            if (Timer % 2 == 0) {
                EocMotion.ConvergeStreaks(npc.Center, progress, 260f);
            }
            EocScreenFX.PushPulse(progress * 0.6f);
        }
        #endregion

        #region 旋臂弹幕
        private void UpdateSpiral(NPC npc, Player player, EocStateContext context, bool mirror, float progress) {
            //锚定慢跟玩家，自旋加速
            Vector2 anchor = player.Center + new Vector2(0f, -260f);
            EocMotion.SpringHover(npc, anchor, 0.008f, 0.1f, 10f);
            float spinDir = mirror ? -1f : 1f;
            npc.rotation += spinDir * MathHelper.Lerp(0.12f, 0.34f, MathHelper.Clamp(progress * 2f, 0f, 1f));
            context.PushIris(0.75f, EocMotion.Arterial);

            int interval = mirror ? Math.Max(SpikeInterval - 1, 5) : SpikeInterval;
            if (Timer % interval == 0) {
                volleyAngle += MathHelper.ToRadians(13f) * spinDir;
                if (!VaultUtils.isClient) {
                    //三臂旋喷
                    for (int arm = 0; arm < 3; arm++) {
                        float angle = volleyAngle + MathHelper.TwoPi * arm / 3f;
                        Vector2 dir = angle.ToRotationVector2();
                        Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center + dir * 40f, dir * 8.5f,
                            ModContent.ProjectileType<EocBloodSpike>(), 11, 0f, Main.myPlayer,
                            spinDir, SpikeCurl);
                    }
                }
                //喷吐飞沫
                EocMotion.BloodSpray(npc.Center, volleyAngle.ToRotationVector2(), 2, 7f, 0.5f);
                if (!VaultUtils.isServer && Timer % (interval * 3) == 0) {
                    SoundEngine.PlaySound(SoundID.NPCDeath13 with { Volume = 0.45f, Pitch = 0.35f }, npc.Center);
                }
            }

            EocScreenFX.PushPulse(0.35f);
            Lighting.AddLight(npc.Center, EocMotion.Arterial.ToVector3() * 1.1f);
        }
        #endregion

        #region 中场喘息
        private void UpdateLull(NPC npc, Player player, EocStateContext context) {
            //旋速骤减，明确的输出窗+换向铺垫
            npc.rotation += 0.12f * (1f - (Timer - SpiralAEnd) / (float)LullTime);
            npc.velocity *= 0.93f;
            if (Timer == SpiralAEnd + 2 && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Zombie3 with { Volume = 0.85f, Pitch = -0.5f }, npc.Center);
            }
            if (!VaultUtils.isServer && Timer % 8 == 0) {
                Vector2 mawDir = (npc.rotation + MathHelper.PiOver2).ToRotationVector2();
                EocMotion.BloodSpray(npc.Center + mawDir * 36f, mawDir, 1, 3f, 0.5f);
            }
        }
        #endregion

        #region 终幕三连冲
        private void UpdateFinaleReel(NPC npc, Player player, EocStateContext context) {
            float progress = (Timer - SpiralBEnd) / (float)FinaleReelTime;
            Vector2 awayDir = (npc.Center - player.Center).SafeNormalize(Vector2.UnitY);
            EocMotion.ReelBack(npc, awayDir, progress, 7f);
            FaceTarget(npc, player.Center, 0.5f);
            context.SetChargeState(1, progress);
            context.PushIris(progress, EocMotion.IrisRed);
            if (Timer == SpiralBEnd + 1 && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item103 with { Volume = 1f, Pitch = -0.55f }, npc.Center);
            }
            if (Timer % 2 == 0) {
                EocMotion.ConvergeStreaks(npc.Center, progress, 200f);
            }
            //长吸气期间第一段折线随人游走显形，进瞄准拍才冻结
            EocDashPlan preview = EocDashPlan.Build(npc.Center, player.Center, DashSide(0), FinaleDashSpeed,
                KinkSpeedMul, KinkLateral, DashFlight, false, 0f);
            preview.WriteLane(context, npc.Center, progress * 0.5f, false);
        }

        private void UpdateTripleDash(NPC npc, Player player, EocStateContext context) {
            int dashTimer = (Timer - ReelEnd) % DashCycle;
            int currentDash = Math.Min((Timer - ReelEnd) / DashCycle, DashCount - 1);

            //瞄准拍首帧：锁线建路，变轨帧写 ai[3] 同步
            if (currentDash != dashIndex) {
                dashIndex = currentDash;
                dashBlinked = false;
                dashKinked = false;
                dashPlan = EocDashPlan.Build(npc.Center, player.Center, DashSide(currentDash), FinaleDashSpeed,
                    KinkSpeedMul, KinkLateral, DashFlight, false, 0f);
                if (!VaultUtils.isClient) {
                    npc.ai[3] = dashPlan.Pack();
                    npc.netUpdate = true;
                }
                EocMotion.AimLockCue(npc, context, dashPlan.Dir1);
            }

            //瞄准拍：刹车余速里回首盯线，车道定格
            if (dashTimer < DashAimBeat) {
                npc.velocity *= 0.8f;
                FaceTarget(npc, npc.Center + dashPlan.Dir1, 0.45f);
                dashPlan.WriteLane(context, npc.Center, dashTimer / (float)DashAimBeat, true);
                context.SetChargeState(1, dashTimer / (float)DashAimBeat);
                context.PushIris(1f, EocMotion.IrisRed);
                if (dashTimer == 2 && !VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Zombie2 with { Volume = 0.5f, Pitch = 0.1f }, npc.Center);
                }
                return;
            }

            //起跑：沿锁定的起跑段
            if (dashTimer == DashAimBeat) {
                context.ResetChargeState();
                EocMotion.DashLaunch(npc, context, dashPlan.Dir1, FinaleDashSpeed, 1.3f);
                if (!VaultUtils.isClient) {
                    npc.netUpdate = true;
                }
                EocMotion.Shake(npc.Center, 7f, 12);
            }

            FaceVelocity(npc);
            EnableContactDamageIfFast(npc, 28f, 1.35f);
            context.PushDashVisuals(1f, 1f);

            int flightTimer = dashTimer - DashAimBeat;
            int kinkFrame = dashPlan.ResolveKinkFrame(npc.ai[3], DashFlight);
            if (!dashBlinked && flightTimer >= kinkFrame - BlinkLead) {
                dashBlinked = true;
                EocMotion.FeintBlink(npc, context);
            }
            //变轨：沿锁定的贯穿段，不看玩家现在在哪
            if (!dashKinked && flightTimer >= kinkFrame) {
                dashKinked = true;
                Vector2 oldVel = npc.velocity;
                npc.velocity = dashPlan.Dir2 * npc.velocity.Length() * KinkSpeedMul;
                if (!VaultUtils.isClient) {
                    npc.netUpdate = true;
                }
                EocMotion.KinkBurst(npc, context, oldVel, context.IsSecondPhase);
            }
            //冲末减速
            if (flightTimer >= DashFlight) {
                npc.velocity *= 0.8f;
                EocMotion.BrakeDroplets(npc);
            }
        }
        #endregion

        #region 力竭
        private void UpdateExhaust(NPC npc, Player player, EocStateContext context) {
            npc.velocity *= 0.92f;
            context.FrameRate = 5;
            FaceTarget(npc, player.Center, 0.1f);
            if (Timer == DashEnd + 2) {
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Zombie2 with { Volume = 0.9f, Pitch = -0.7f }, npc.Center);
                }
                //权威端登记冷却，环卫者解编
                if (!VaultUtils.isClient) {
                    context.MaelstromPlayed = true;
                    context.MaelstromCooldown = 1800;
                    foreach (NPC n in Main.ActiveNPCs) {
                        if (n.type == NPCID.ServantofCthulhu
                            && (int)n.ai[0] == ServantOfCthulhuAI.ModeOrbit
                            && (int)n.ai[2] == npc.whoAmI) {
                            n.ai[0] = ServantOfCthulhuAI.ModeVanilla;
                            n.ai[1] = n.ai[2] = n.ai[3] = 0f;
                            n.noTileCollide = false;
                            n.netUpdate = true;
                        }
                    }
                }
            }
            if (!VaultUtils.isServer && Timer % 7 == 0) {
                Vector2 mawDir = (npc.rotation + MathHelper.PiOver2).ToRotationVector2();
                EocMotion.BloodSpray(npc.Center + mawDir * 38f, mawDir, 1, 3f, 0.6f);
            }
        }
        #endregion

        public override void OnExit(EocStateContext context) {
            base.OnExit(context);
            context.FrameRate = context.IsSecondPhase ? 4 : 6;
        }
    }
}
