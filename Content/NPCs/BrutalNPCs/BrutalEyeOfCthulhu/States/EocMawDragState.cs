using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEyeOfCthulhu.Core;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEyeOfCthulhu.States
{
    /// <summary>
    /// 撕咬拖曳投技（二阶段）：佯攻假冲刺故意擦身而过→急停回首裂口大张（专属前摇+咬程车道）→<br/>
    /// 第二段真冲刺以口器判定咬人（无接触伤，咬中即投技）；咬中后贴地高速拖行横穿战场，<br/>
    /// 沿途血雾迸溅，终以甩头把玩家砸进地面，力竭长喘收场。落空则长收招惩罚窗<br/>
    /// 网络：抓取目标经 ai[3]=±(whoAmI+1) 同步，符号即拖行方向；被抓者位移/锁控/运镜/分段结算
    /// 全部由 <see cref="EocGrabPerformancePlayer"/> 在其本机由同步态推导，本状态只管 NPC 权威与全端演出
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)EocStateIndex.MawDrag, typeof(EocStateContext))]
    internal class EocMawDragState : EocStateBase
    {
        public override string StateName => "EocMawDrag";
        public override EocStateIndex StateIndex => EocStateIndex.MawDrag;
        public override bool AllowFogStep => false;

        private enum DragPhase
        {
            Track,      //绕侧接近
            Reel,       //后撤蓄力（伪装成普通冲刺）
            FeintPass,  //佯攻冲刺，故意擦身而过
            Pivot,      //急停回首裂口大张，投技专属前摇
            RealDash,   //真冲刺，口器抓取判定
            Whiff,      //落空长收招
            Seize,      //咬合顿帧
            Drag,       //贴地拖行
            WhipUp,     //上扬蓄势
            Slam,       //甩头砸地
            Recover,    //力竭喘息
        }

        private const int TrackTime = 24;
        private const int ReelTime = 26;
        private const int FeintPassTime = 18;
        private const int PivotTime = 40;
        private const int RealDashTime = 16;
        private const int WhiffTime = 30;
        private const int SeizeTime = 22;
        private const int DragTime = 84;
        private const int WhipUpTime = 26;
        private const int SlamTime = 14;
        private const int RecoverTime = 46;
        /// <summary>真冲刺最大咬程，前摇车道如实展示</summary>
        private const float GrabReach = 940f;
        /// <summary>口器判定：中心前伸距离与判定半径</summary>
        private const float MawOffset = 52f;
        private const float MawRadius = 62f;
        /// <summary>眼体中心贴地悬高</summary>
        private const float DragHoverHeight = 66f;

        private float FeintSpeed => Context.IsDeathMode ? 46f : 42f;
        private float RealDashSpeed => Context.IsDeathMode ? 62f : 58f;
        private float DragSpeed => Context.IsDeathMode ? 40f : 36f;

        private EocStateContext Context;
        private DragPhase phase;
        /// <summary>佯攻擦身的侧偏符号，权威端掷骰，客户端本地演出自定</summary>
        private float passSide;
        /// <summary>真冲刺累计行程，超咬程即落空</summary>
        private float dashTraveled;
        /// <summary>被抓玩家下标，各端由 ai[3] 推导</summary>
        private int grabbedIndex = -1;
        /// <summary>拖行方向符号，随 ai[3] 符号同步</summary>
        private float dragSign = 1f;
        /// <summary>全状态总帧数，保底超时用</summary>
        private int totalTicks;
        /// <summary>砸地终结已演出（防重复爆点）</summary>
        private bool impactPlayed;

        public override void OnEnter(EocStateContext context) {
            base.OnEnter(context);
            Context = context;
            phase = DragPhase.Track;
            passSide = 1f;
            dashTraveled = 0f;
            grabbedIndex = -1;
            dragSign = 1f;
            totalTicks = 0;
            impactPlayed = false;
            context.FrameRate = 3;
            //清上一状态残留的 ai[3]（变轨帧/环半径等），防被误读成抓取目标
            if (!VaultUtils.isClient) {
                context.Npc.ai[3] = 0f;
                context.Npc.netUpdate = true;
            }
        }

        public override IEocState OnUpdate(EocStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;
            DisableContactDamage(npc);
            totalTicks++;

            //客户端镜像：ai[3] 非零即被咬合（含晚入场），归零即砸地释放
            if (VaultUtils.isClient) {
                int packed = (int)npc.ai[3];
                if (packed != 0 && phase < DragPhase.Seize) {
                    ApplyPacked(packed);
                    EnterSeize(npc, context);
                }
                else if (packed == 0 && phase >= DragPhase.Seize && phase < DragPhase.Recover) {
                    PlayImpact(npc, context);
                    SwitchPhase(DragPhase.Recover);
                }
                else if (packed != 0) {
                    ApplyPacked(packed);
                }
            }

            //保底超时：任何相位卡死都强制收场
            if (totalTicks > 640 && !VaultUtils.isClient) {
                if (grabbedIndex >= 0) {
                    ReleaseGrab(npc, playImpact: false);
                }
                return new EocVeilHoverState(60);
            }

            switch (phase) {
                case DragPhase.Track:
                    UpdateTrack(npc, player);
                    break;
                case DragPhase.Reel:
                    UpdateReel(npc, player, context);
                    break;
                case DragPhase.FeintPass:
                    UpdateFeintPass(npc, context);
                    break;
                case DragPhase.Pivot:
                    UpdatePivot(npc, player, context);
                    break;
                case DragPhase.RealDash:
                    UpdateRealDash(npc, player, context);
                    break;
                case DragPhase.Whiff:
                    return UpdateWhiff(npc, player, context);
                case DragPhase.Seize:
                    UpdateSeize(npc, context);
                    break;
                case DragPhase.Drag:
                    UpdateDrag(npc, context);
                    break;
                case DragPhase.WhipUp:
                    UpdateWhipUp(npc, context);
                    break;
                case DragPhase.Slam:
                    UpdateSlam(npc, context);
                    break;
                case DragPhase.Recover:
                    return UpdateRecover(npc, player, context);
            }

            return null;
        }

        private void SwitchPhase(DragPhase next) {
            phase = next;
            Timer = 0;
        }

        private void ApplyPacked(int packed) {
            dragSign = Math.Sign(packed);
            grabbedIndex = Math.Abs(packed) - 1;
        }

        /// <summary>被抓玩家仍可抓（在场、活着、没被外力拽远）</summary>
        private bool HeldPlayerValid(NPC npc) {
            if (grabbedIndex < 0 || grabbedIndex >= Main.maxPlayers) {
                return false;
            }
            Player held = Main.player[grabbedIndex];
            return held.active && !held.dead && !held.shimmering && held.Distance(npc.Center) < 1300f;
        }

        #region 佯攻段
        private void UpdateTrack(NPC npc, Player player) {
            float side = npc.Center.X < player.Center.X ? -1f : 1f;
            EocMotion.CurveChase(npc, player.Center + new Vector2(side * 460f, -90f), 22f, 0.12f);
            FaceTarget(npc, player.Center, 0.3f);

            Timer++;
            if (Timer >= TrackTime) {
                SwitchPhase(DragPhase.Reel);
            }
        }

        private void UpdateReel(NPC npc, Player player, EocStateContext context) {
            float progress = Timer / (float)ReelTime;
            Vector2 awayDir = (npc.Center - player.Center).SafeNormalize(Vector2.UnitY);
            EocMotion.ReelBack(npc, awayDir, progress, 5f);
            FaceTarget(npc, player.Center, 0.5f);
            context.SetChargeState(1, progress);
            context.PushIris(progress, EocMotion.IrisRed);

            //完全复刻普通冲刺的蓄力语言，这是骗局的上半场
            if (progress > 0.72f && !VaultUtils.isServer) {
                npc.position += Main.rand.NextVector2Circular(1.7f, 1.7f);
            }
            Vector2 aimDir = (EocMotion.PredictTarget(player, npc.Center, FeintSpeed, 0.55f) - npc.Center)
                .SafeNormalize(Vector2.UnitY);
            context.LaneIntensity = 0.4f + progress * 0.6f;
            context.LaneStart = npc.Center;
            context.LaneDir = aimDir;
            context.LaneLength = 1350f;
            context.LaneProgress = progress;
            if (Timer % 2 == 0) {
                EocMotion.ConvergeStreaks(npc.Center, progress, 130f);
            }
            if (Timer == 1 && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item103 with { Volume = 0.8f, Pitch = -0.45f }, npc.Center);
            }

            Timer++;
            if (Timer >= ReelTime) {
                //佯攻起跑：权威端掷侧偏，瞄向擦身线而非玩家
                if (!VaultUtils.isClient) {
                    passSide = Main.rand.NextBool() ? 1f : -1f;
                    Vector2 predicted = EocMotion.PredictTarget(player, npc.Center, FeintSpeed, 0.5f);
                    Vector2 toPlayer = (predicted - npc.Center).SafeNormalize(Vector2.UnitY);
                    Vector2 perp = toPlayer.RotatedBy(MathHelper.PiOver2) * passSide;
                    Vector2 dir = (predicted + perp * 180f - npc.Center).SafeNormalize(Vector2.UnitY);
                    EocMotion.DashLaunch(npc, context, dir, FeintSpeed);
                    npc.netUpdate = true;
                }
                else {
                    EocMotion.DashLaunch(npc, context,
                        (player.Center - npc.Center).SafeNormalize(Vector2.UnitY), FeintSpeed);
                }
                context.ResetChargeState();
                FaceVelocity(npc);
                SwitchPhase(DragPhase.FeintPass);
            }
        }

        private void UpdateFeintPass(NPC npc, EocStateContext context) {
            context.PushDashVisuals(1f, 1f);
            FaceVelocity(npc);
            //佯攻仍是实体冲撞，撞上算普通擦伤
            EnableContactDamageIfFast(npc, 26f, 0.85f);

            Timer++;
            if (Timer >= FeintPassTime) {
                SwitchPhase(DragPhase.Pivot);
            }
        }

        private void UpdatePivot(NPC npc, Player player, EocStateContext context) {
            float progress = Timer / (float)PivotTime;
            //急刹+回首死盯，口器越张越大，这是投技的专属语言
            npc.velocity *= 0.8f;
            FaceTarget(npc, player.Center, 0.35f);
            context.FrameRate = 2;
            context.ScalePulse = 1f + 0.12f * progress;
            context.SetChargeState(3, progress);
            context.PushIris(0.5f + progress * 0.5f, EocMotion.BrightBlood);

            //咬程车道：真冲刺打多远，车道就画多远
            Vector2 aimDir = (player.Center - npc.Center).SafeNormalize(Vector2.UnitY);
            context.LaneIntensity = 0.5f + progress * 0.5f;
            context.LaneStart = npc.Center;
            context.LaneDir = aimDir;
            context.LaneLength = GrabReach;
            context.LaneProgress = progress;

            //口器内聚血丝，末 1/4 自动静默（尖叫前的吸气）
            if (Timer % 2 == 0) {
                EocMotion.ConvergeStreaks(npc.Center + aimDir * 40f, progress, 110f);
            }
            if (!VaultUtils.isServer) {
                if (Timer == 2) {
                    SoundEngine.PlaySound(SoundID.NPCHit13 with { Volume = 1.1f, Pitch = -0.7f }, npc.Center);
                }
                if (Timer == 8) {
                    SoundEngine.PlaySound(SoundID.ForceRoarPitched with { Volume = 0.65f, Pitch = -0.6f }, npc.Center);
                }
                //升调嘶声，饥饿感逐帧拉满
                if (Timer % 8 == 0) {
                    SoundEngine.PlaySound(SoundID.Zombie2 with { Volume = 0.4f, Pitch = -0.5f + progress * 0.9f }, npc.Center);
                }
                if (progress > 0.7f) {
                    npc.position += Main.rand.NextVector2Circular(1.9f, 1.9f);
                }
            }

            Timer++;
            if (Timer >= PivotTime) {
                //真冲刺起跑
                dashTraveled = 0f;
                if (!VaultUtils.isClient) {
                    Vector2 predicted = EocMotion.PredictTarget(player, npc.Center, RealDashSpeed, 0.6f);
                    Vector2 dir = (predicted - npc.Center).SafeNormalize(Vector2.UnitY);
                    EocMotion.DashLaunch(npc, context, dir, RealDashSpeed, 1.25f);
                    npc.netUpdate = true;
                }
                else {
                    EocMotion.DashLaunch(npc, context,
                        (player.Center - npc.Center).SafeNormalize(Vector2.UnitY), RealDashSpeed, 1.25f);
                }
                context.ResetChargeState();
                FaceVelocity(npc);
                SwitchPhase(DragPhase.RealDash);
            }
        }

        private void UpdateRealDash(NPC npc, Player player, EocStateContext context) {
            context.PushDashVisuals(1f, 1f);
            context.FrameRate = 2;
            FaceVelocity(npc);
            //真冲刺无接触伤：咬中即投技，擦过不算，与普通冲撞的本质区别
            DisableContactDamage(npc);
            dashTraveled += npc.velocity.Length();

            //口器判定与抓取，仅权威端
            if (!VaultUtils.isClient) {
                Vector2 mawPos = npc.Center + (npc.rotation + MathHelper.PiOver2).ToRotationVector2() * MawOffset;
                foreach (Player p in Main.ActivePlayers) {
                    if (p.dead || p.ghost || p.shimmering) {
                        continue;
                    }
                    if (Vector2.Distance(p.Center, mawPos) > MawRadius + p.width * 0.5f) {
                        continue;
                    }
                    //咬中：目标与拖行方向打包写 ai[3]，立刻下发
                    grabbedIndex = p.whoAmI;
                    dragSign = npc.velocity.X >= 0f ? 1f : -1f;
                    npc.ai[3] = (grabbedIndex + 1) * dragSign;
                    npc.netUpdate = true;
                    EnterSeize(npc, context);
                    return;
                }

                //超咬程或超时即落空
                if (dashTraveled > GrabReach || Timer >= RealDashTime) {
                    SwitchPhase(DragPhase.Whiff);
                    return;
                }
            }
            else if (Timer >= RealDashTime + 6) {
                //客户端本地演出兜底：迟迟没等到抓取包就当落空
                SwitchPhase(DragPhase.Whiff);
                return;
            }

            Timer++;
        }

        private IEocState UpdateWhiff(NPC npc, Player player, EocStateContext context) {
            //咬空长收招：明确的惩罚窗
            npc.velocity *= 0.82f;
            EocMotion.BrakeDroplets(npc);
            FaceTarget(npc, player.Center, 0.1f);
            context.FrameRate = 4;
            context.ScalePulse = 0.96f;

            if (Timer == 1 && !VaultUtils.isServer) {
                //空咬合齿声+懊恼嘶声
                SoundEngine.PlaySound(SoundID.NPCHit13 with { Volume = 0.9f, Pitch = 0.25f }, npc.Center);
                SoundEngine.PlaySound(SoundID.Zombie3 with { Volume = 0.7f, Pitch = -0.3f }, npc.Center);
            }
            //权威端登记冷却：落空冷却减半，仍算亮过相
            if (Timer == 2 && !VaultUtils.isClient) {
                context.MawDragPlayed = true;
                context.MawDragCooldown = 780;
            }

            Timer++;
            if (Timer >= WhiffTime) {
                if (VaultUtils.isClient) {
                    return null;
                }
                return new EocVeilHoverState(52);
            }
            return null;
        }
        #endregion

        #region 拖曳段
        /// <summary>咬合入场：顿帧+爆点，全端各自演出</summary>
        private void EnterSeize(NPC npc, EocStateContext context) {
            SwitchPhase(DragPhase.Seize);
            context.FrameRate = 2;
            context.ScalePulse = 1.14f;
            Vector2 mawPos = MawWorldPos(npc);
            if (!VaultUtils.isServer) {
                EocMotion.BloodBurst(mawPos, 1.25f);
                SoundEngine.PlaySound(SoundID.NPCHit13 with { Volume = 1.2f, Pitch = -0.25f }, mawPos);
                SoundEngine.PlaySound(SoundID.Item74 with { Volume = 0.7f, Pitch = -0.3f }, mawPos);
            }
            EocMotion.Shake(mawPos, 7f, 12);
        }

        private void UpdateSeize(NPC npc, EocStateContext context) {
            //咬合顿帧：一帧锁死，随后近乎悬停的僵持
            if (Timer == 0) {
                npc.velocity *= 0.12f;
            }
            npc.velocity *= 0.82f;
            context.PushIris(1f, EocMotion.BrightBlood);
            context.FrameRate = 2;
            //僵持震颤，咬紧的张力
            if (!VaultUtils.isServer) {
                npc.position += Main.rand.NextVector2Circular(1.3f, 1.3f);
                if (Timer % 5 == 0) {
                    EocMotion.BloodSpray(MawWorldPos(npc), Main.rand.NextVector2Unit(), 2, 5f, 0.9f);
                }
            }

            //权威端持抓校验
            if (!VaultUtils.isClient && !HeldPlayerValid(npc)) {
                ReleaseGrab(npc, playImpact: false);
                SwitchPhase(DragPhase.Recover);
                return;
            }

            Timer++;
            if (Timer >= SeizeTime) {
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Roar with { Volume = 0.9f, Pitch = 0.3f }, npc.Center);
                }
                SwitchPhase(DragPhase.Drag);
            }
        }

        private void UpdateDrag(NPC npc, EocStateContext context) {
            //贴地拖行：横向提速，纵向咬住地表悬高
            float speedT = MathF.Pow(Math.Min(Timer / 30f, 1f), 2f);
            float targetSpeed = MathHelper.Lerp(14f, DragSpeed, speedT);
            float lookX = npc.Center.X + dragSign * 190f;
            float groundY = FindGroundY(new Vector2(lookX, npc.Center.Y));
            float targetY = groundY - DragHoverHeight;
            float vy = MathHelper.Clamp((targetY - npc.Center.Y) * 0.12f, -13f, 15f);
            npc.velocity = new Vector2(dragSign * targetSpeed, vy);
            FaceVelocity(npc);
            context.PushDashVisuals(1f, 1f);
            context.FrameRate = 2;

            //权威端：持抓校验+高频同步，拖行轨迹漂移自愈
            if (!VaultUtils.isClient) {
                if (!HeldPlayerValid(npc)) {
                    ReleaseGrab(npc, playImpact: false);
                    SwitchPhase(DragPhase.Recover);
                    return;
                }
                if (Timer % 6 == 0) {
                    npc.netUpdate = true;
                }
                //逼近世界边缘提前抬升
                float edge = 60f * 16f;
                if (npc.Center.X < edge || npc.Center.X > Main.maxTilesX * 16f - edge) {
                    SwitchPhase(DragPhase.WhipUp);
                    return;
                }
            }

            //沿途血雾迸溅+地表刮削
            if (!VaultUtils.isServer) {
                Vector2 scrapePos = MawWorldPos(npc) + new Vector2(0f, 26f);
                EocMotion.BloodSpray(scrapePos, new Vector2(-dragSign, -0.55f), 2, 8f, 0.5f);
                if (Timer % 3 == 0) {
                    PRTLoader.NewParticle<PRT_EocBloodMist>(scrapePos - new Vector2(dragSign * 30f, 0f),
                        new Vector2(-dragSign * Main.rand.NextFloat(1f, 3f), -Main.rand.NextFloat(0.5f, 1.5f)),
                        EocMotion.MistWine, Main.rand.NextFloat(0.7f, 1.2f))?.Configure(Main.rand.Next(24, 40), 0.5f);
                }
                //研磨火花：擦地的白热碎屑
                if (Timer % 4 == 0) {
                    PRTLoader.NewParticle<PRT_Spark>(scrapePos, new Vector2(-dragSign * Main.rand.NextFloat(2f, 6f), -Main.rand.NextFloat(1f, 4f)),
                        EocMotion.BrightBlood, Main.rand.NextFloat(0.6f, 1f))?.Configure(true, Main.rand.Next(8, 14));
                }
                if (Timer % 8 == 0) {
                    SoundEngine.PlaySound(SoundID.WormDigQuiet with { Volume = 0.55f, Pitch = 0.3f, MaxInstances = 3 }, scrapePos);
                }
            }

            //研磨颠簸拍：把人往地里再按一下
            if (Timer % 26 == 25) {
                EocMotion.Shake(npc.Center, 3.4f, 7, new Vector2(0f, 1f));
                EocGrabPerformancePlayer.RequestShake(4f, 7);
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.NPCHit1 with { Volume = 0.85f, Pitch = -0.5f }, npc.Center);
                    EocMotion.BloodSpray(MawWorldPos(npc) + new Vector2(0f, 20f), -Vector2.UnitY, 5, 7f, 0.8f);
                }
            }

            Timer++;
            if (Timer >= DragTime) {
                SwitchPhase(DragPhase.WhipUp);
            }
        }

        private void UpdateWhipUp(NPC npc, EocStateContext context) {
            //上扬蓄势：横速衰减，先猛后缓地拉升，为甩头蓄满反差
            float t = Timer / (float)WhipUpTime;
            npc.velocity.X *= 0.86f;
            npc.velocity.Y = -18f * (1f - t * t);
            FaceVelocity(npc);
            context.FrameRate = 3;
            context.SetChargeState(3, t);
            context.PushIris(t, EocMotion.BrightBlood);

            //权威端持抓校验
            if (!VaultUtils.isClient && !HeldPlayerValid(npc)) {
                ReleaseGrab(npc, playImpact: false);
                SwitchPhase(DragPhase.Recover);
                return;
            }

            if (Timer == 2 && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item103 with { Volume = 0.9f, Pitch = -0.2f }, npc.Center);
            }
            //末 6 帧全静默：爆发前的收气
            if (t < 0.75f && !VaultUtils.isServer && Timer % 3 == 0) {
                EocMotion.BloodSpray(MawWorldPos(npc), -Vector2.UnitY, 1, 4f, 0.7f);
            }

            Timer++;
            if (Timer >= WhipUpTime) {
                SwitchPhase(DragPhase.Slam);
            }
        }

        private void UpdateSlam(NPC npc, EocStateContext context) {
            //甩头砸地：3 帧顶点滞空，随后一帧灌满下坠速度
            if (Timer < 3) {
                npc.velocity *= 0.5f;
            }
            else {
                if (Timer == 3) {
                    npc.velocity = new Vector2(dragSign * 7f, 40f);
                    if (!VaultUtils.isServer) {
                        SoundEngine.PlaySound(SoundID.ForceRoarPitched with { Volume = 1f, Pitch = -0.15f }, npc.Center);
                    }
                }
                FaceVelocity(npc);
                context.PushDashVisuals(1f, 1f);
            }

            //权威端：砸到地表或超时即释放
            if (!VaultUtils.isClient) {
                float groundY = FindGroundY(npc.Center);
                bool hitGround = Timer >= 3 && npc.Center.Y >= groundY - 50f;
                if (hitGround || Timer >= SlamTime || !HeldPlayerValid(npc)) {
                    ReleaseGrab(npc, playImpact: hitGround || Timer >= SlamTime);
                    //反冲上抬：把人砸进地里，自己被反作用力顶起
                    npc.velocity = new Vector2(-dragSign * 7f, -13f);
                    SwitchPhase(DragPhase.Recover);
                    return;
                }
            }

            Timer++;
        }

        private IEocState UpdateRecover(NPC npc, Player player, EocStateContext context) {
            //力竭喘息：投技后的明确输出窗
            npc.velocity *= 0.9f;
            context.FrameRate = 5;
            context.ScalePulse = 1f;
            FaceTarget(npc, player.Center, 0.12f);

            if (Timer == 1 && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Zombie3 with { Volume = 0.85f, Pitch = -0.55f }, npc.Center);
            }
            //权威端登记冷却
            if (Timer == 2 && !VaultUtils.isClient) {
                context.MawDragPlayed = true;
                context.MawDragCooldown = 1500;
            }
            if (!VaultUtils.isServer && Timer % 7 == 0) {
                Vector2 mawDir = (npc.rotation + MathHelper.PiOver2).ToRotationVector2();
                EocMotion.BloodSpray(npc.Center + mawDir * 40f, mawDir, 1, 3f, 0.5f);
            }

            Timer++;
            if (Timer >= RecoverTime) {
                if (VaultUtils.isClient) {
                    return null;
                }
                return new EocVeilHoverState(66);
            }
            return null;
        }

        /// <summary>权威端释放：清 ai[3] 下发，可选砸地爆点</summary>
        private void ReleaseGrab(NPC npc, bool playImpact) {
            npc.ai[3] = 0f;
            npc.netUpdate = true;
            grabbedIndex = -1;
            if (playImpact) {
                PlayImpact(npc, Context);
            }
        }

        /// <summary>砸地终结的世界侧爆点，全端各自演出一次</summary>
        private void PlayImpact(NPC npc, EocStateContext context) {
            if (impactPlayed) {
                return;
            }
            impactPlayed = true;
            Vector2 mawPos = MawWorldPos(npc);
            EocMotion.Shake(mawPos, 11f, 16, new Vector2(0f, 1f));
            EocGrabPerformancePlayer.RequestShake(11f, 16);
            context.ScalePulse = 0.9f;
            if (VaultUtils.isServer) {
                return;
            }
            EocMotion.BloodBurst(mawPos, 1.7f);
            EocScreenFX.PushFlash(0.5f, 10);
            //横向压扁的冲击环贴地铺开
            PRTLoader.NewParticle<PRT_DWave>(mawPos, Vector2.Zero, EocMotion.Arterial, 0.3f)?
                .Configure(new Vector2(1.9f, 0.55f), 0f, 1.5f, 18);
            //向上喷的血泉与组织块
            for (int i = 0; i < 10; i++) {
                Vector2 vel = new Vector2(Main.rand.NextFloat(-6f, 6f), -Main.rand.NextFloat(4f, 12f));
                PRTLoader.NewParticle<PRT_HeartcarverDroplet>(mawPos, vel,
                    Color.Lerp(EocMotion.Arterial, EocMotion.BrightBlood, Main.rand.NextFloat()),
                    Main.rand.NextFloat(1.2f, 2.1f))?.Configure(Main.rand.Next(26, 44), 0.36f, 0.985f);
            }
            for (int i = 0; i < 4; i++) {
                PRTLoader.NewParticle<PRT_EocSkinShred>(mawPos, new Vector2(Main.rand.NextFloat(-4f, 4f), -Main.rand.NextFloat(3f, 8f)),
                    EocMotion.VenousDark, Main.rand.NextFloat(0.5f, 0.9f))?.Configure(Main.rand.Next(30, 50));
            }
            EocMotion.MistPuff(mawPos, 5, 1.4f, 0.6f);
            SoundEngine.PlaySound(SoundID.NPCDeath12 with { Volume = 1.1f, Pitch = -0.4f }, mawPos);
            SoundEngine.PlaySound(SoundID.WormDig with { Volume = 1f, Pitch = -0.6f }, mawPos);
            SoundEngine.PlaySound(SoundID.Item74 with { Volume = 0.9f, Pitch = -0.5f }, mawPos);
        }

        /// <summary>口器世界坐标（瞳孔朝向前端）</summary>
        internal static Vector2 MawWorldPos(NPC npc) {
            return npc.Center + (npc.rotation + MathHelper.PiOver2).ToRotationVector2() * MawOffset;
        }

        /// <summary>
        /// 找拖行地表（含平台）：起点取眼体略上方，起点在空中就向下找地板（平地/洞穴都不误认洞顶），
        /// 起点已在地里说明前方地形抬升，向上找表面攀越；都找不到视作悬崖缓降
        /// </summary>
        internal static float FindGroundY(Vector2 from) {
            int tx = (int)(from.X / 16f);
            if (tx < 10 || tx > Main.maxTilesX - 10) {
                return from.Y + 600f;
            }
            int startY = Math.Clamp((int)(from.Y / 16f) - 6, 10, Main.maxTilesY - 12);

            if (SolidGround(tx, startY)) {
                for (int ty = startY - 1; ty > startY - 44 && ty > 10; ty--) {
                    if (!SolidGround(tx, ty)) {
                        return (ty + 1) * 16f;
                    }
                }
                return startY * 16f;
            }

            int maxY = Math.Min(startY + 64, Main.maxTilesY - 10);
            for (int ty = startY; ty < maxY; ty++) {
                if (SolidGround(tx, ty)) {
                    return ty * 16f;
                }
            }
            return from.Y + 600f;
        }

        private static bool SolidGround(int tx, int ty) {
            Tile tile = Framing.GetTileSafely(tx, ty);
            return tile.HasTile && (Main.tileSolid[tile.TileType] || Main.tileSolidTop[tile.TileType]);
        }
        #endregion

        public override void OnExit(EocStateContext context) {
            base.OnExit(context);
            context.FrameRate = context.IsSecondPhase ? 4 : 6;
            context.ScalePulse = 1f;
            //异常出口（死亡演出/撤离强切）兜底：抓取旗必须清干净
            if (!VaultUtils.isClient && (int)context.Npc.ai[3] != 0) {
                context.Npc.ai[3] = 0f;
                context.Npc.netUpdate = true;
            }
        }
    }
}
