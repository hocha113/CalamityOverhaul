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
    /// 合相祭仪:黄道环信标连珠,浑天仪三环共面,98 帧蓄力(重创可打断=拆台抉择)后放阶段大祭<br/>
    /// P0 风暴螺旋(走廊缺口旋转) P1 幻星降世(主星裂化真伪三星→聚阵品字→逐星预瞄轮掷,锁定拍实体度识真,唯真身咬人)<br/>
    /// P2 环系崩落(晶流沿环+坠星带预告柱滚落留巷) P3 冕暴轮转(缺口扇区旋转) P4 月瞳双凝视(先后两束对向扫天)<br/>
    /// 公平阀:放招前 12 帧纯静默;各脚本缺口均为具名常量;结束长喘息
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)CultistStateIndex.Conjunction, typeof(CultistStateContext))]
    internal class CultistConjunctionState : CultistStateBase
    {
        public override string StateName => "CultistConjunction";
        public override CultistStateIndex StateIndex => CultistStateIndex.Conjunction;

        private const int ChargeFrames = 98;
        /// <summary>P0 风暴螺旋走廊半角(rad)与转速</summary>
        private const float SpiralCorridorHalf = 0.52f;
        private const float SpiralCorridorDrift = 0.0055f;
        /// <summary>P1 幻星三相:裂化拍/首掷拍/掷波间隔(帧);波与波错开=同刻至多一条走廊压人(公平阀)</summary>
        private const int TrioSplitBeat = 12;
        private const int TrioFirstLaunch = 128;
        private const int TrioWaveGap = 38;
        /// <summary>P2 坠星雨:7 巷连空 2 巷轮转,巷距 px;梳锚玩家出手拍锁定,预告柱由 CultistFallingStar 自带</summary>
        private const int RainLanes = 7;
        private const int RainGapLanes = 2;
        private const float RainLaneSpacing = 250f;
        /// <summary>P2 坠星节拍(帧):滚动落梳,连绵成星暴</summary>
        private const int RainBeat = 32;
        /// <summary>P2 巷内落点抖动上限(px):最窄走廊=巷距-2*抖动-2*坠星判定半宽(20)≈138px 恒可穿行</summary>
        private const float RainJitterX = 36f;
        /// <summary>P2 标高散排(px):纯纵向,不改巷几何</summary>
        private const float RainSkyJitterY = 64f;
        /// <summary>P2 落点高度(玩家上方 px)</summary>
        private const float RainSkyHeight = 760f;
        /// <summary>P3 冕暴:轮转缺口半角(rad)与每轮进角;<br/>
        /// 追缺口所需切速=GapStep×半径/22f,日面贴身半径 460 处≈5px/f 带翅可跟;
        /// 0.30 步进在中距半径要求 7~9px/f 持续圆周飞行,判无解(2026-08-31),勿回调</summary>
        private const float CoronaGapHalf = 0.55f;
        private const float CoronaGapStep = 0.24f;

        private int ReleaseEnd => ChargeFrames + (StatePhase(ctxCache) == 4 ? 396 : 300);
        private CultistStateContext ctxCache;

        /// <summary>真伪三星的出手次序(权威端裂化拍洗出);-1=空位</summary>
        private readonly int[] trioOrder = [-1, -1, -1];
        /// <summary>裂化成功(权威端置位),失手时退回星珠垫压</summary>
        private bool trioActive;
        /// <summary>P3 冕暴缺口基角(权威端首轮锁玩家方位,先给活路;此前是绝对角 0,站对侧必吃保底伤)</summary>
        private float coronaGapBase;

        private static int StatePhase(CultistStateContext context) => context?.Phase ?? 0;

        public override void OnEnter(CultistStateContext context) {
            base.OnEnter(context);
            ctxCache = context;
            trioActive = false;
            trioOrder[0] = trioOrder[1] = trioOrder[2] = -1;
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
                return new CultistCoilState(43);
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
            if ((Timer == 18 || Timer == 52 || Timer == 82) && !VaultUtils.isServer) {
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
                    //幻星降世:主星裂化真伪三星→聚阵品字→逐星预瞄轮掷;唯真身咬人,锁定拍实体度识真
                    int it = (int)t;
                    //裂化令(权威端):真身入阵,两颗幻象自星心分娩,槽位与出手次序各洗一遍
                    if (!VaultUtils.isClient && it == TrioSplitBeat) {
                        Projectile real = FindSplitCandidate(npc.whoAmI);
                        if (real != null) {
                            trioActive = true;
                            float baseAngle = Main.rand.NextFloat(MathHelper.TwoPi);
                            int[] slots = [0, 1, 2];
                            for (int i = slots.Length - 1; i > 0; i--) {
                                int j = Main.rand.Next(i + 1);
                                (slots[i], slots[j]) = (slots[j], slots[i]);
                            }
                            CultistPlanetProj.CommandMuster(real, baseAngle + slots[0] * MathHelper.TwoPi / 3f);
                            trioOrder[0] = real.whoAmI;
                            for (int i = 1; i <= 2; i++) {
                                int who = Projectile.NewProjectile(npc.GetSource_FromAI(),
                                    real.Center + Main.rand.NextVector2Circular(40f, 40f), Vector2.Zero,
                                    ModContent.ProjectileType<CultistPlanetProj>(), 60, 0f, Main.myPlayer,
                                    CultistPlanetProj.KindNebula, npc.whoAmI, i * 10 + 7);
                                if (who >= 0 && who < Main.maxProjectiles) {
                                    Main.projectile[who].localAI[1] = baseAngle + slots[i] * MathHelper.TwoPi / 3f;
                                    trioOrder[i] = who;
                                }
                            }
                            for (int i = trioOrder.Length - 1; i > 0; i--) {
                                int j = Main.rand.Next(i + 1);
                                (trioOrder[i], trioOrder[j]) = (trioOrder[j], trioOrder[i]);
                            }
                        }
                    }
                    //裂化演出拍(各端)
                    if (it == TrioSplitBeat) {
                        CultistMotion.SigilCommitFX(origin, core, 1.6f);
                        CultistMotion.RuneBurst(origin, core, 16, 9f);
                        CultistMotion.Shake(origin, 8f, 14);
                        CultistScreenFX.PushFlash(0.4f);
                        if (!VaultUtils.isServer) {
                            SoundEngine.PlaySound(SoundID.Item103 with { Volume = 1f, Pitch = -0.35f }, origin);
                            SoundEngine.PlaySound(SoundID.Zombie104 with { Volume = 0.9f, Pitch = -0.2f }, origin);
                        }
                    }
                    //三波轮掷:预瞄线追瞄→锁定(识真窗)→线灭即出手,波距错开=同刻至多一条走廊
                    for (int wave = 0; wave < 3; wave++) {
                        int launchBeat = TrioFirstLaunch + wave * TrioWaveGap;
                        if (!VaultUtils.isClient) {
                            Projectile member = TrioMember(wave);
                            //+1:弹寿在弹相递减,晚一帧上桩才保证出手帧主相仍读得到锁定点
                            if (member != null && it == launchBeat - CultistPlanetAimLine.Lifetime + 1) {
                                Vector2 aim = CultistMotion.PredictTarget(player, member.Center, 9f, 0.55f);
                                Projectile.NewProjectile(npc.GetSource_FromAI(), member.Center, Vector2.Zero,
                                    ModContent.ProjectileType<CultistPlanetAimLine>(), 0, 0f, Main.myPlayer,
                                    member.whoAmI, aim.X, aim.Y);
                            }
                            if (member != null && it == launchBeat) {
                                Vector2 aim = CultistPlanetAimLine.GetLockedAimFor(member.whoAmI)
                                    ?? CultistMotion.PredictTarget(player, member.Center, 9f, 0.55f);
                                CultistPlanetProj.CommandLaunchPlanet(member, aim);
                            }
                        }
                        //出手拍演出(各端,与投掷态同语调)
                        if (it == launchBeat) {
                            CultistMotion.Shake(npc.Center, 5f, 10);
                            CultistScreenFX.PushFlash(0.18f);
                            context.ScalePulse = 1.1f;
                            if (!VaultUtils.isServer) {
                                SoundEngine.PlaySound(SoundID.Zombie105 with { Volume = 1f, Pitch = -0.45f }, npc.Center);
                            }
                        }
                    }
                    //裂化失手(主星不在常驻位):退回稀疏星珠垫压,祭仪不空转
                    if (!VaultUtils.isClient && !trioActive && it > TrioSplitBeat && Timer % 46 == 0) {
                        Vector2 dir = (player.Center - origin).SafeNormalize(Vector2.UnitY);
                        Projectile.NewProjectile(npc.GetSource_FromAI(), origin + dir * planetR * 0.9f,
                            dir * 6f, ModContent.ProjectileType<CultistStarBead>(), 40, 0f,
                            Main.myPlayer, context.Phase);
                    }
                    break;
                }
                case 2: {
                    //环系崩落:晶珠沿环面双向流出+坠星带预告柱滚落留巷
                    if (!VaultUtils.isClient && Timer % 5 == 0) {
                        float tilt = -0.35f;   //与 TechStardust uTilt 同源:环面即弹道
                        Vector2 dir = tilt.ToRotationVector2() * ((int)(t / 5f) % 2 == 0 ? 1f : -1f);
                        Projectile.NewProjectile(npc.GetSource_FromAI(), origin + dir * planetR * 1.15f,
                            dir * 6.5f, ModContent.ProjectileType<CultistStarBead>(), 40, 0f,
                            Main.myPlayer, context.Phase);
                    }
                    //滚动坠星:梳锚玩家出手拍锁定不追踪;奇数波错半巷+整梳随机相移+巷内抖动,
                    //每星种子错拍(预告延展/落速/体型),不落成方阵;缺口 2 巷逐波轮转
                    if (Timer % RainBeat == 0) {
                        CultistScreenFX.PushFlash(0.14f);
                        CultistMotion.Shake(player.Center, 2.5f, 6);
                        if (!VaultUtils.isServer) {
                            SoundEngine.PlaySound(SoundID.Item117 with { Volume = 0.7f, Pitch = 0.1f },
                                player.Center - new Vector2(0f, 200f));
                        }
                        if (!VaultUtils.isClient) {
                            int volley = (int)(t / RainBeat);
                            int gapStart = volley % RainLanes;
                            float combShift = (volley % 2 == 1 ? RainLaneSpacing * 0.5f : 0f)
                                + Main.rand.NextFloat(-0.25f, 0.25f) * RainLaneSpacing;
                            for (int lane = 0; lane < RainLanes; lane++) {
                                int d = ((lane - gapStart) % RainLanes + RainLanes) % RainLanes;
                                if (d < RainGapLanes) {
                                    continue;
                                }
                                float offsetX = (lane - RainLanes / 2) * RainLaneSpacing + combShift
                                    + Main.rand.NextFloat(-RainJitterX, RainJitterX);
                                Vector2 pos = new(player.Center.X + offsetX,
                                    player.Center.Y - RainSkyHeight + Main.rand.NextFloat(-RainSkyJitterY, RainSkyJitterY));
                                Projectile.NewProjectile(npc.GetSource_FromAI(), pos, Vector2.Zero,
                                    ModContent.ProjectileType<CultistFallingStar>(), 40, 0f, Main.myPlayer,
                                    npc.whoAmI, context.Phase, Main.rand.NextFloat());
                            }
                            npc.netUpdate = true;
                        }
                    }
                    break;
                }
                case 3: {
                    //冕暴轮转:冕矛全周辐射,缺口扇区逐轮进角(缺口=活口,追着走)
                    if (!VaultUtils.isClient && planet != null && Timer % 22 == 0) {
                        int volley = (int)(t / 22f);
                        //首轮缺口正对玩家方位(先给活路,与奥术新星/蚀祭本影同惯例),后轮按声明步进轮转
                        if (volley == 0) {
                            coronaGapBase = (player.Center - origin).ToRotation();
                        }
                        float gapAngle = coronaGapBase + volley * CoronaGapStep;
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
                    //月瞳双凝视:先后两束对向扫天,睁眼全程;
                    //巡航扫速 0.010=基础凝视(0.006)的合相高压版,仍靠束内 30f 缓起给反应窗
                    context.PupilOpen = 1f;
                    if (!VaultUtils.isClient && planet != null) {
                        if ((int)t == 14) {
                            float playerAngle = (player.Center - origin).ToRotation();
                            Projectile.NewProjectile(npc.GetSource_FromAI(), origin, Vector2.Zero,
                                ModContent.ProjectileType<CultistGazeBeam>(), 52, 0f, Main.myPlayer,
                                playerAngle - 0.5f, 0.010f, planet.whoAmI);
                        }
                        if ((int)t == 206) {
                            float playerAngle = (player.Center - origin).ToRotation();
                            Projectile.NewProjectile(npc.GetSource_FromAI(), origin, Vector2.Zero,
                                ModContent.ProjectileType<CultistGazeBeam>(), 52, 0f, Main.myPlayer,
                                playerAngle + 0.5f, -0.010f, planet.whoAmI);
                        }
                    }
                    break;
                }
            }
        }

        /// <summary>裂化取材(权威端):抓非幻象主星,常驻/掷出/归位段皆可征用(上一记掷星未归也不落空)</summary>
        private static Projectile FindSplitCandidate(int ownerWho) {
            int type = ModContent.ProjectileType<CultistPlanetProj>();
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.type == type && (int)proj.ai[1] == ownerWho
                    && (int)proj.ai[2] / 10 == 0 && (int)proj.ai[2] % 10 is 1 or 4 or 5) {
                    return proj;
                }
            }
            return null;
        }

        /// <summary>取第 wave 位出手星(权威端):散佚或已离聚阵段返回 null,该波跳过</summary>
        private Projectile TrioMember(int wave) {
            int who = trioOrder[wave];
            if (who < 0 || who >= Main.maxProjectiles) {
                return null;
            }
            Projectile proj = Main.projectile[who];
            return proj.active && proj.type == ModContent.ProjectileType<CultistPlanetProj>()
                && (int)proj.ai[2] % 10 == 7 ? proj : null;
        }

        public override void OnExit(CultistStateContext context) {
            //收势清幻象(真身留任):掷完在飞的幻象也在此散场,不留残星
            if (!VaultUtils.isClient) {
                CultistPlanetProj.DismissPhantoms(context.Npc.whoAmI);
            }
        }
    }
}
