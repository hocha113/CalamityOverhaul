using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDeerclops.Core;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.GameSystem;
using InnoVault.PRT;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDeerclops.States
{
    /// <summary>
    /// 投技·独眼凝视擒抱：影手把玩家拖回→巨爪拎起举到独眼前→风雪骤退静止凝视一拍→
    /// 冰霜吐息点脸→高高扬起砸进雪地→释放恢复。玩家位移由被抓者客户端结算，
    /// 本状态只驱动boss姿态、节拍表现与服务端出口阀
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)DeerclopsStateIndex.EyeGrab, typeof(DeerclopsStateContext))]
    internal class DeerclopsEyeGrabState : DeerclopsStateBase
    {
        public override string StateName => "EyeGrab";
        public override DeerclopsStateIndex StateIndex => DeerclopsStateIndex.EyeGrab;

        #region 节拍常量(运镜/玩家侧/攫取手全部对齐这些值)
        /// <summary>顿帧：抓住一瞬全场定住</summary>
        internal const int CatchFreezeEnd = 8;
        /// <summary>拖拽结束，玩家已到爪中</summary>
        internal const int DragEnd = 42;
        /// <summary>拎起结束，玩家已在独眼前</summary>
        internal const int LiftEnd = 78;
        /// <summary>握爪挤压节拍(伤害1)</summary>
        internal const int GripHit = 60;
        /// <summary>凝视结束(静止一拍的尾端)</summary>
        internal const int GazeEnd = 138;
        /// <summary>吐息命中节拍(伤害2)</summary>
        internal const int BreathHit = 146;
        /// <summary>吐息结束</summary>
        internal const int BreathEnd = 176;
        /// <summary>砸击蓄势结束(爪已举到最高)</summary>
        internal const int SlamWindupEnd = 196;
        /// <summary>砸地命中节拍(伤害3，最重)</summary>
        internal const int SlamHit = 202;
        /// <summary>释放：玩家恢复操控并获得无敌</summary>
        internal const int ReleaseTick = 208;
        /// <summary>状态总长(=运镜时长)</summary>
        internal const int TotalTime = 232;

        /// <summary>三段伤害占最大生命比例(死亡模式×1.25)，逐段留命钳制，满血必不致死</summary>
        internal const float GripDamageFrac = 0.08f;
        internal const float BreathDamageFrac = 0.12f;
        internal const float SlamDamageFrac = 0.16f;
        #endregion

        /// <summary>传送逃逸检测累计(服务端)</summary>
        private int escapeTicks;

        public override void OnEnter(DeerclopsStateContext context) {
            base.OnEnter(context);
            escapeTicks = 0;
            NPC npc = context.Npc;
            npc.velocity.X = 0f;

            //抓住一瞬：闷响+影爆(各端自补)
            if (!Main.dedServ) {
                SoundEngine.PlaySound(SoundID.DeerclopsHit with { Volume = 1.1f, Pitch = -0.4f }, npc.Center);
                SoundEngine.PlaySound(SoundID.NPCDeath6 with { Volume = 0.7f, Pitch = -0.6f }, npc.Center);
            }
            DeerclopsMotion.CameraPunch(npc.Center, 6f, 16, "DeerGrabCatch");
        }

        public override IDeerclopsState OnUpdate(DeerclopsStateContext context) {
            NPC npc = context.Npc;
            Timer++;

            //站定自管物理：只保留垂直贴地，不追不跳
            context.SkipDefaultMovement = true;
            npc.velocity.X *= 0.8f;
            DeerclopsMotion.ApplyVertical(npc, context, allowJump: false);
            npc.damage = 0;

            Player victim = GrabTarget(npc);

            DriveBossPresentation(context, npc, victim);

            //——服务端出口阀——
            if (!VaultUtils.isClient) {
                //目标死亡/离场：立即断投(玩家侧自会释放)
                if (Timer < ReleaseTick && !victim.Alives()) {
                    StampCooldown(context);
                    return new DeerclopsStalkState();
                }
                //传送逃逸(回忆镜等边缘路径)：远离爪锚持续则断投
                if (victim != null && Timer > DragEnd && Timer < ReleaseTick) {
                    escapeTicks = victim.Distance(ClawAnchor(npc, Timer)) > 900f ? escapeTicks + 1 : 0;
                    if (escapeTicks > 12) {
                        StampCooldown(context);
                        return new DeerclopsStalkState();
                    }
                }
                //正常收尾兼保底超时
                if (Timer >= TotalTime) {
                    StampCooldown(context);
                    return new DeerclopsStalkState();
                }
            }
            return null;
        }

        public override void OnExit(DeerclopsStateContext context) {
            base.OnExit(context);
            context.BodyLean = 0f;
            if (!VaultUtils.isClient) {
                context.Npc.ai[1] = 0f;
                context.Npc.netUpdate = true;
            }
        }

        /// <summary>boss姿态/音画节拍，各端按本地Timer推进</summary>
        private void DriveBossPresentation(DeerclopsStateContext context, NPC npc, Player victim) {
            //凝视窗内风雪骤退——"世界安静下来=它在看你"的既有语言
            bool gazeWindow = Timer > LiftEnd && Timer <= BreathEnd;
            context.VeilTarget = gazeWindow ? 0.05f : 0.12f;
            context.EyeHeat = 1f;

            //幕一：顿帧+拖拽，伏低引臂
            if (Timer <= DragEnd) {
                context.AnimMode = DeerAnimMode.Crouch;
                context.EyeGlow = 0.55f;
                if (Timer == CatchFreezeEnd + 2 && !Main.dedServ) {
                    SoundEngine.PlaySound(SoundID.DD2_GhastlyGlaiveImpactGhost with { Volume = 0.8f, Pitch = -0.5f }, npc.Center);
                }
                //拖拽尾流(本端)：玩家身后拉出影雪
                if (!Main.dedServ && Timer > CatchFreezeEnd && victim != null && Timer % 2 == 0) {
                    Dust dust = Dust.NewDustPerfect(victim.Center + Main.rand.NextVector2Circular(16f, 16f),
                        Main.rand.NextBool() ? DustID.Shadowflame : DustID.Snow,
                        -victim.velocity * 0.05f, 130, default, Main.rand.NextFloat(0.9f, 1.5f));
                    dust.noGravity = true;
                }
                return;
            }

            //幕二：拎起(掀地帧序=举爪)
            if (Timer <= LiftEnd) {
                context.AnimMode = DeerAnimMode.Scoop;
                context.AnimTimer = (Timer - DragEnd) * 48 / (LiftEnd - DragEnd);
                context.EyeGlow = MathHelper.Lerp(0.55f, 0.85f, (Timer - DragEnd) / (float)(LiftEnd - DragEnd));

                if (Timer == GripHit && !Main.dedServ) {
                    //握爪挤压
                    SoundEngine.PlaySound(SoundID.NPCHit1 with { Volume = 0.9f, Pitch = -0.7f }, ClawAnchor(npc, Timer));
                    SpawnGripBurst(ClawAnchor(npc, Timer));
                }
                return;
            }

            //幕三：静止凝视一拍——独眼血红，全场只剩心跳
            if (Timer <= GazeEnd) {
                context.AnimMode = DeerAnimMode.Crouch;
                float p = (Timer - LiftEnd) / (float)(GazeEnd - LiftEnd);
                context.EyeGlow = MathHelper.Lerp(0.85f, 1f, p);

                if ((Timer == LiftEnd + 14 || Timer == LiftEnd + 40) && !Main.dedServ) {
                    //心跳般的闷响(死亡演出同语汇)
                    SoundEngine.PlaySound(SoundID.DeerclopsStep with { Volume = 0.85f, Pitch = -0.85f }, npc.Center);
                }
                //凝视尾段：冷芒收束吸入独眼(吸气)，最后8帧骤然安静
                if (!Main.dedServ && Timer > GazeEnd - 20 && Timer <= GazeEnd - 8 && Timer % 2 == 0) {
                    Vector2 eye = EyePos(npc);
                    Vector2 spawn = eye + Main.rand.NextVector2Unit() * Main.rand.NextFloat(70f, 180f);
                    Dust dust = Dust.NewDustPerfect(spawn, DustID.Frost, (eye - spawn) * 0.09f, 120, default, Main.rand.NextFloat(0.8f, 1.3f));
                    dust.noGravity = true;
                }
                if (Timer == GazeEnd - 20 && !Main.dedServ) {
                    SoundEngine.PlaySound(SoundID.Item119 with { Volume = 0.55f, Pitch = -0.5f }, npc.Center);
                }
                return;
            }

            //幕四：冰霜吐息点脸(吼帧=张口)
            if (Timer <= BreathEnd) {
                context.AnimMode = DeerAnimMode.Roar;
                context.AnimTimer = 8 + (Timer - GazeEnd) / 3;
                context.EyeGlow = 1f;

                if (Timer == GazeEnd + 1 && !Main.dedServ) {
                    SoundEngine.PlaySound(SoundID.DeerclopsIceAttack with { Volume = 1.1f, Pitch = -0.15f }, npc.Center);
                }
                if (Timer % 6 == 0) {
                    DeerclopsMotion.CameraPunch(npc.Center, 2.2f, 8, "DeerGrabBreathRumble");
                }
                //吐息锥(本端)：自独眼喷向爪中玩家
                if (!Main.dedServ && victim != null && Timer < BreathEnd - 6) {
                    SpawnBreathCone(npc, victim);
                }
                return;
            }

            //幕五：扬爪蓄势→砸地
            if (Timer <= SlamHit) {
                context.AnimMode = DeerAnimMode.Stomp;
                float windT = MathHelper.Clamp((Timer - BreathEnd) / (float)(SlamWindupEnd - BreathEnd), 0f, 1f);
                context.AnimTimer = Timer <= SlamWindupEnd ? (int)(windT * 30f) : 36;
                //后仰蓄势(反向预备)
                context.BodyLean = Timer <= SlamWindupEnd ? -0.12f * (float)Math.Pow(windT, 3) : 0.28f;
                context.EyeGlow = 0.95f;

                if (Timer == BreathEnd + 4 && !Main.dedServ) {
                    SoundEngine.PlaySound(SoundID.DeerclopsScream with { Volume = 0.9f, Pitch = -0.3f }, npc.Center);
                }
                if (Timer == SlamHit) {
                    DoSlamImpact(npc);
                }
                return;
            }

            //幕六：释放与喘息恢复
            context.AnimMode = DeerAnimMode.Crouch;
            context.BodyLean = MathHelper.Lerp(context.BodyLean, 0f, 0.12f);
            context.EyeGlow = MathHelper.Lerp(1f, 0.3f, (Timer - SlamHit) / (float)(TotalTime - SlamHit));
        }

        /// <summary>砸地：本投技最重的一拍</summary>
        private static void DoSlamImpact(NPC npc) {
            Vector2 ground = SlamGroundPoint(npc);
            DeerclopsMotion.CameraPunch(ground, 12f, 30, "DeerGrabSlam", Vector2.UnitY);
            DeerclopsGrabPlayer.RequestShake(10f, 30);

            if (Main.dedServ) {
                return;
            }
            SoundEngine.PlaySound(SoundID.DeerclopsRubbleAttack with { Volume = 1.2f, Pitch = -0.2f }, ground);
            SoundEngine.PlaySound(SoundID.Item70 with { Volume = 0.8f, Pitch = -0.5f }, ground);

            //雪爆+冰晶迸溅
            for (int i = 0; i < 22; i++) {
                Dust dust = Dust.NewDustPerfect(ground + new Vector2(Main.rand.NextFloat(-70f, 70f), 0f),
                    DustID.Snow, new Vector2(Main.rand.NextFloat(-4f, 4f), -Main.rand.NextFloat(1f, 6f)), 70, default, Main.rand.NextFloat(1.2f, 2.2f));
                dust.noGravity = Main.rand.NextBool(3);
            }
            for (int i = 0; i < 12; i++) {
                PRTLoader.NewParticle<PRT_ATShard>(ground + new Vector2(Main.rand.NextFloat(-60f, 60f), -Main.rand.NextFloat(0f, 20f)),
                    new Vector2(Main.rand.NextFloat(-4f, 4f), -Main.rand.NextFloat(2f, 7f)),
                    DeerclopsMotion.IceBlue * 0.9f, Main.rand.NextFloat(0.28f, 0.5f))
                    .Configure(Main.rand.Next(18, 30), Main.rand.NextFloat(-0.3f, 0.3f));
            }
            for (int i = 0; i < 5; i++) {
                PRTLoader.NewParticle<PRT_Smoke>(ground + new Vector2(Main.rand.NextFloat(-50f, 50f), 0f),
                    new Vector2(Main.rand.NextFloat(-1.5f, 1.5f), -Main.rand.NextFloat(0.5f, 1.2f)),
                    DeerclopsMotion.ColdWhite * 0.5f, Main.rand.NextFloat(0.9f, 1.4f))
                    .Configure(Main.rand.Next(30, 50), 0.6f, Main.rand.NextFloat(-0.05f, 0.05f));
            }
        }

        /// <summary>握爪挤压的碎冰(本端)</summary>
        private static void SpawnGripBurst(Vector2 pos) {
            for (int i = 0; i < 10; i++) {
                Dust dust = Dust.NewDustPerfect(pos + Main.rand.NextVector2Circular(20f, 20f), DustID.Ice,
                    Main.rand.NextVector2Circular(2.5f, 2.5f), 80, default, Main.rand.NextFloat(0.9f, 1.5f));
                dust.noGravity = Main.rand.NextBool();
            }
        }

        /// <summary>吐息锥粒子：独眼→玩家面部，速度沿视线略散</summary>
        private static void SpawnBreathCone(NPC npc, Player victim) {
            Vector2 eye = EyePos(npc);
            Vector2 dir = (victim.Center - eye).SafeNormalize(Vector2.UnitX * npc.spriteDirection);
            for (int i = 0; i < 3; i++) {
                Vector2 vel = dir.RotatedBy(Main.rand.NextFloat(-0.32f, 0.32f)) * Main.rand.NextFloat(4f, 9f);
                Dust dust = Dust.NewDustPerfect(eye + dir * 12f, Main.rand.NextBool(4) ? DustID.Snow : DustID.Frost,
                    vel, 100, default, Main.rand.NextFloat(1f, 1.7f));
                dust.noGravity = true;
            }
            if (Main.rand.NextBool(6)) {
                PRTLoader.NewParticle<PRT_ATShard>(eye + dir * 16f, dir * Main.rand.NextFloat(5f, 8f),
                    DeerclopsMotion.IceBlue * 0.8f, Main.rand.NextFloat(0.22f, 0.36f))
                    .Configure(Main.rand.Next(12, 20), Main.rand.NextFloat(-0.25f, 0.25f));
            }
        }

        #region 锚点与查询(玩家侧/攫取手/运镜共用)

        /// <summary>独眼世界坐标(与主控 EyeWorldPos 同式)</summary>
        internal static Vector2 EyePos(NPC npc) {
            return npc.Bottom + new Vector2(npc.spriteDirection * 26f, -138f) * npc.scale;
        }

        /// <summary>砸击落点：身前地表</summary>
        internal static Vector2 SlamGroundPoint(NPC npc) {
            return DeerclopsMotion.FindGroundBelow(npc.Bottom + new Vector2(npc.spriteDirection * 130f, -24f), 20);
        }

        /// <summary>
        /// 爪锚世界坐标(按节拍推移)。拖拽段这是"拖向的目标点"，
        /// 玩家实际位置由其客户端自起点插值，见 DeerclopsGrabPlayer
        /// </summary>
        internal static Vector2 ClawAnchor(NPC npc, int timer) {
            int dir = npc.spriteDirection != 0 ? npc.spriteDirection : 1;
            Vector2 lowFront = npc.Bottom + new Vector2(dir * 118f, -52f) * npc.scale;
            Vector2 eyeFront = EyePos(npc) + new Vector2(dir * 64f, 6f);

            if (timer <= DragEnd) {
                return lowFront;
            }
            if (timer <= LiftEnd) {
                float t = (timer - DragEnd) / (float)(LiftEnd - DragEnd);
                return Vector2.Lerp(lowFront, eyeFront, MathHelper.SmoothStep(0f, 1f, t));
            }
            if (timer <= BreathEnd) {
                //凝视/吐息：眼前微微起伏，吐息段被气流压出小幅后摆
                float bob = (float)Math.Sin(timer * 0.11f) * 2.4f;
                float recoil = timer > GazeEnd ? MathHelper.Clamp((timer - GazeEnd) / 14f, 0f, 1f) * 7f : 0f;
                return eyeFront + new Vector2(dir * recoil, bob);
            }
            if (timer <= SlamWindupEnd) {
                //扬爪：pow(3)迟涨，最后几帧才到顶
                float t = MathHelper.Clamp((timer - BreathEnd) / (float)(SlamWindupEnd - BreathEnd), 0f, 1f);
                Vector2 apex = eyeFront + new Vector2(dir * 26f, -68f);
                return Vector2.Lerp(eyeFront, apex, t * t * t);
            }
            if (timer <= SlamHit) {
                //砸落：6帧内高次落地
                float t = MathHelper.Clamp((timer - SlamWindupEnd) / (float)(SlamHit - SlamWindupEnd), 0f, 1f);
                Vector2 apex = eyeFront + new Vector2(dir * 26f, -68f);
                float snap = 1f - (float)Math.Pow(1f - t, 4);
                return Vector2.Lerp(apex, SlamGroundPoint(npc) + new Vector2(0f, -12f), snap);
            }
            //钉在雪坑里
            return SlamGroundPoint(npc) + new Vector2(0f, -8f);
        }

        /// <summary>当前被抓的目标玩家，无效返回null</summary>
        internal static Player GrabTarget(NPC npc) {
            int idx = (int)npc.ai[1] - 1;
            if (idx < 0 || idx >= Main.maxPlayers) {
                return null;
            }
            Player player = Main.player[idx];
            return player.Alives() ? player : null;
        }

        /// <summary>该NPC是否正处于本模组接管的 EyeGrab 态，是则给出状态实例(取其本地Timer)</summary>
        internal static bool TryGetEyeGrabState(NPC npc, out DeerclopsEyeGrabState grabState) {
            grabState = null;
            if (npc == null || !npc.active || npc.type != NPCID.Deerclops) {
                return false;
            }
            if ((int)npc.ai[2] != (int)DeerclopsStateIndex.EyeGrab) {
                return false;
            }
            //原版AI的ai槽可能撞值，必须确认接管在场
            if (!npc.TryGetOverride(out Dictionary<Type, NPCOverride> overrides)
                || !overrides.TryGetValue(typeof(DeerclopsAI), out NPCOverride deerOverride)
                || deerOverride is not DeerclopsAI deerAI) {
                return false;
            }
            if (deerAI.CurrentState is not DeerclopsEyeGrabState state) {
                return false;
            }
            grabState = state;
            return true;
        }

        /// <summary>
        /// 全局查询：正处于 EyeGrab 且目标为指定玩家的独眼巨鹿。
        /// 玩家侧与攫取手共用此判据，保证各端观察一致
        /// </summary>
        internal static bool TryFindGrabbingDeer(int playerIndex, out NPC deer, out DeerclopsEyeGrabState grabState) {
            deer = null;
            grabState = null;
            foreach (NPC npc in Main.ActiveNPCs) {
                if ((int)npc.ai[1] - 1 != playerIndex || !TryGetEyeGrabState(npc, out DeerclopsEyeGrabState state)) {
                    continue;
                }
                deer = npc;
                grabState = state;
                return true;
            }
            return false;
        }

        /// <summary>投技冷却盖戳(服务端出口统一调用)</summary>
        internal static void StampCooldown(DeerclopsStateContext context) {
            context.GrabLastEndStamp = (int)Main.GameUpdateCount;
        }

        #endregion
    }
}
