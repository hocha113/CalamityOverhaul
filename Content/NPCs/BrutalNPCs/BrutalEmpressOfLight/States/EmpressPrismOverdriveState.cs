using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEmpressOfLight.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEmpressOfLight.Rendering;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEmpressOfLight.States
{
    /// <summary>
    /// 低血大招·棱彩过驱：三个乐章——
    /// I 极光帘幕垂落围出剧场；II 双扇日舞+收缩笼+心跳弹环的三重奏；
    /// III 万光归一的屏息与终唱绽放。一场只此一次
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)EmpressStateIndex.PrismOverdrive, typeof(EmpressStateContext))]
    internal class EmpressPrismOverdriveState : EmpressStateBase
    {
        public override string StateName => "EmpressPrismOverdrive";
        public override EmpressStateIndex StateIndex => EmpressStateIndex.PrismOverdrive;

        //乐章节点（不吃TempoScale：大招的呼吸自己定）
        private const int MovementOne = 120;
        private const int MovementTwo = 420;
        private const int GatherStart = 430;
        private const int FinaleFrame = 500;
        private const int TotalTime = 640;

        public override void OnEnter(EmpressStateContext context) {
            base.OnEnter(context);
            context.OverdriveUsed = true;
            //清场开幕
            EmpressCast.ClearHostileProjectiles(context.Npc);
            context.Npc.damage = 0;
            PlayLocal(SoundID.Item161 with { Volume = 1f, Pitch = -0.3f }, context.Npc.Center);
        }

        public override IEmpressState OnUpdate(EmpressStateContext context) {
            NPC npc = context.Npc;
            Player target = context.Target;
            Timer++;

            npc.damage = 0;

            //她升上剧场中心高位
            if (target.Alives()) {
                float centerPull = Timer < MovementOne ? 0.02f : 0.008f;
                GlideTo(npc, target.Center + new Vector2(0f, -440f) + EmpressMotion.Breathing(3.3f, 14f), centerPull, 0.1f, 16f);
            }
            else {
                npc.velocity *= 0.95f;
            }

            if (Timer < MovementOne) {
                MovementOneUpdate(context, npc, target);
            }
            else if (Timer < MovementTwo) {
                MovementTwoUpdate(context, npc, target);
            }
            else {
                MovementThreeUpdate(context, npc, target);
            }

            if (Timer >= TotalTime) {
                return new EmpressConnectorState();
            }
            return null;
        }

        /// <summary>乐章I：极光帘幕自两翼垂落，向内缓拢围出剧场</summary>
        private void MovementOneUpdate(EmpressStateContext context, NPC npc, Player target) {
            context.Pose = EmpressPose.CastBoth;
            context.PoseTimer = 20f;
            context.SetChargeState(3, Timer / (float)MovementOne);
            EmpressMotion.HandChargeDust(context.LeftHand, Timer / (float)MovementOne, context.DayFormBlend);
            EmpressMotion.HandChargeDust(context.RightHand, Timer / (float)MovementOne, context.DayFormBlend);

            if (Timer == 30 && target.Alives() && !VaultUtils.isClient) {
                //四道帘幕：外侧两道内拢，内侧两道外推——空间自己在呼吸
                float cx = target.Center.X;
                float cy = target.Center.Y - 300f;
                EmpressCast.Aurora(npc, new Vector2(cx - 950f, cy), 0.0f, 0.55f, 560, context.AuroraDamage);
                EmpressCast.Aurora(npc, new Vector2(cx + 950f, cy), 1.6f, -0.55f, 560, context.AuroraDamage);
                EmpressCast.Aurora(npc, new Vector2(cx - 330f, cy), 3.1f, -0.34f, 560, context.AuroraDamage);
                EmpressCast.Aurora(npc, new Vector2(cx + 330f, cy), 4.7f, 0.34f, 560, context.AuroraDamage);
            }
            if (Timer == 34) {
                PlayLocal(SoundID.Item165 with { Volume = 0.9f, Pitch = -0.35f }, npc.Center);
            }
            if (!VaultUtils.isServer) {
                EmpressScreenFX.DeclareAmbient(0.35f + Timer / (float)MovementOne * 0.3f);
            }
        }

        /// <summary>乐章II：双扇日舞旋切+两座收缩笼+心跳弹环</summary>
        private void MovementTwoUpdate(EmpressStateContext context, NPC npc, Player target) {
            context.Pose = EmpressPose.Dance;
            context.PoseTimer = MathHelper.Clamp(Timer - MovementOne, 10f, 170f);
            context.ResetChargeState();

            if (!VaultUtils.isServer) {
                EmpressScreenFX.DeclareAmbient(0.6f);
            }

            if (Timer == 130 && !VaultUtils.isClient) {
                //双扇反向日舞
                float baseAngle = target.Alives() ? (target.Center - npc.Center).ToRotation() + 0.5f : 0f;
                for (int i = 0; i < 6; i++) {
                    float angle = baseAngle + MathHelper.TwoPi / 6f * i;
                    EmpressCast.Sunray(npc, angle, 0.0062f, context.SunrayDamage);
                    EmpressCast.Sunray(npc, angle + MathHelper.TwoPi / 12f, -0.0062f, context.SunrayDamage);
                }
            }
            if (Timer == 130) {
                PlayLocal(SoundID.Item159 with { Volume = 1f }, npc.Center);
                EmpressMotion.Shake(npc.Center, 4f, 14);
            }

            //两座收缩笼
            if ((Timer == 180 || Timer == 308) && target.Alives() && !VaultUtils.isClient) {
                int cageIdx = Timer == 180 ? 0 : 1;
                CastOverdriveCage(context, npc, target.Center, cageIdx);
            }
            if (Timer == 180 || Timer == 308) {
                PlayLocal(SoundID.Item163 with { Volume = 0.95f, Pitch = -0.1f }, npc.Center);
            }

            //心跳弹环：每44帧一记16弹小环，两缺口
            int heartbeat = (Timer - MovementOne) % 44;
            if (heartbeat == 43 && !VaultUtils.isClient) {
                int pulse = (Timer - MovementOne) / 44;
                float gapAngle = pulse * 1.1f;
                for (int i = 0; i < 16; i++) {
                    float angle = MathHelper.TwoPi / 16f * i + pulse * 0.26f;
                    if (Math.Abs(MathHelper.WrapAngle(angle - gapAngle)) < 0.5f
                        || Math.Abs(MathHelper.WrapAngle(angle - gapAngle - MathHelper.Pi)) < 0.5f) {
                        continue;
                    }
                    Vector2 dir = angle.ToRotationVector2();
                    EmpressCast.Bolt(npc, npc.Center + dir * 70f, dir * 4.5f, context.BoltDamage, 0,
                        angle / MathHelper.TwoPi + pulse * 0.13f);
                }
            }
            if (heartbeat == 43) {
                PlayLocal(SoundID.Item164 with { Volume = 0.55f, Pitch = 0.3f }, npc.Center);
            }
        }

        /// <summary>乐章III：万光归一（收束屏息）→终唱三环绽放→退潮呼吸拍</summary>
        private void MovementThreeUpdate(EmpressStateContext context, NPC npc, Player target) {
            if (Timer < FinaleFrame) {
                //收束：全场光尘向她坍缩，末12帧完全静默
                context.Pose = EmpressPose.Transform;
                //上限58：避开原版变身绘制的本体隐没窗
                context.PoseTimer = MathHelper.Clamp((Timer - GatherStart) / (float)(FinaleFrame - GatherStart) * 58f, 0f, 58f);
                float gatherT = (Timer - GatherStart) / (float)(FinaleFrame - GatherStart);
                context.SetChargeState(3, gatherT);

                if (!VaultUtils.isServer) {
                    EmpressScreenFX.DeclareAmbient(0.6f + gatherT * 0.3f);
                    if (Timer < FinaleFrame - 12 && Main.rand.NextFloat() < 0.5f + gatherT * 0.4f) {
                        float hue = Main.rand.NextFloat();
                        Vector2 spawn = npc.Center + Main.rand.NextVector2CircularEdge(500f, 520f) * (1f - gatherT * 0.5f);
                        PRTLoader.NewParticle<PRT_EmpressSpark>(spawn, (npc.Center - spawn) * (0.05f + gatherT * 0.06f),
                            EmpressMotion.Prism(hue, 0.72f), Main.rand.NextFloat(0.8f, 1.3f))?.Configure(16, hue);
                    }
                }
                if (Timer == GatherStart + 6) {
                    PlayLocal(SoundID.Item161 with { Volume = 0.85f, Pitch = 0.2f }, npc.Center);
                }
            }
            else if (Timer == FinaleFrame) {
                //终唱：三层交错弹环+全屏棱彩脉冲+光蝶十六方
                EmpressCast.Radiance(npc, npc.Center, 680f, 40, 0.55f);
                EmpressCast.Radiance(npc, npc.Center, 320f, 28, 0.05f);
                EmpressMotion.Shake(npc.Center, 9.5f, 30);
                if (!VaultUtils.isServer) {
                    EmpressScreenFX.PushPrismPulse(npc.Center, 1f, 42);
                    for (int i = 0; i < 16; i++) {
                        float bh = i / 16f;
                        PRTLoader.NewParticle<PRT_EmpressButterfly>(npc.Center,
                            (MathHelper.TwoPi / 16f * i).ToRotationVector2() * Main.rand.NextFloat(3f, 7f),
                            EmpressMotion.Prism(bh, 0.7f), Main.rand.NextFloat(0.7f, 1.15f))?.Configure(90, bh);
                    }
                }
                PlayLocal(SoundID.Item162 with { Volume = 1f, Pitch = -0.2f }, npc.Center);
                PlayLocal(SoundID.Item164 with { Volume = 1f }, npc.Center);

                if (!VaultUtils.isClient) {
                    //三环速度分层，缺口逐环旋进40°
                    float[] speeds = [4.2f, 5.5f, 6.8f];
                    for (int ring = 0; ring < 3; ring++) {
                        float gapAngle = 0.8f + ring * 0.7f;
                        for (int i = 0; i < 26; i++) {
                            float angle = MathHelper.TwoPi / 26f * i + ring * 0.1f;
                            if (Math.Abs(MathHelper.WrapAngle(angle - gapAngle)) < 0.36f
                                || Math.Abs(MathHelper.WrapAngle(angle - gapAngle - MathHelper.Pi)) < 0.36f) {
                                continue;
                            }
                            Vector2 dir = angle.ToRotationVector2();
                            EmpressCast.Bolt(npc, npc.Center + dir * 50f, dir * speeds[ring], context.BoltDamage, 0,
                                angle / MathHelper.TwoPi + ring * 0.33f);
                        }
                    }
                }
            }
            else {
                //退潮：呼吸拍，只剩她的喘息与光雨
                context.Pose = EmpressPose.Idle;
                context.PoseTimer = 0f;
                context.ResetChargeState();
                if (!VaultUtils.isServer) {
                    float decay = 1f - (Timer - FinaleFrame) / (float)(TotalTime - FinaleFrame);
                    EmpressScreenFX.DeclareAmbient(0.5f * decay);
                }
                EmpressMotion.AmbientGlow(npc, context.DayFormBlend);
            }
        }

        /// <summary>大招版收缩笼：40弹三缺口</summary>
        private void CastOverdriveCage(EmpressStateContext context, NPC npc, Vector2 center, int cageIdx) {
            float gapSeed = Main.rand.NextFloat(MathHelper.TwoPi);
            float chirality = cageIdx % 2 == 0 ? 1f : -1f;
            EmpressCast.Radiance(npc, center, 180f, 20, 0.4f + cageIdx * 0.3f);
            for (int i = 0; i < 40; i++) {
                float angle = MathHelper.TwoPi / 40f * i;
                //三个缺口
                bool inGap = false;
                for (int g = 0; g < 3; g++) {
                    if (Math.Abs(MathHelper.WrapAngle(angle - gapSeed - MathHelper.TwoPi / 3f * g)) < 0.3f) {
                        inGap = true;
                        break;
                    }
                }
                if (inGap) {
                    continue;
                }
                Vector2 pos = center + angle.ToRotationVector2() * 640f;
                Vector2 inward = (center - pos).SafeNormalize(Vector2.UnitY);
                Vector2 vel = inward * 4.9f + inward.RotatedBy(MathHelper.PiOver2) * 1.2f * chirality;
                EmpressCast.Bolt(npc, pos, vel, context.BoltDamage, 2, angle / MathHelper.TwoPi, 46f);
            }
        }
    }
}
