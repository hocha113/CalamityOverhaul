using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEaterOfWorlds.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEaterOfWorlds.Projectiles;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEaterOfWorlds.Rendering;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEaterOfWorlds.States
{
    /// <summary>
    /// 分裂钳猎(招牌)：受控三分裂→两翼高位钳形对冲+地底第三席轮番喷发→
    /// 地底汇拢再合体→合体巨喷收官。分裂是主动招式而非死亡惩罚
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)EowStateIndex.SplitPincer, typeof(EowStateContext))]
    internal class EowSplitPincerState : EowStateBase
    {
        public override string StateName => "SplitPincer";
        public override EowStateIndex StateIndex => EowStateIndex.SplitPincer;
        public override bool AllowFarSnap => false;

        #region 节奏常量
        private const int SqueezeTime = 34;
        private const int RipFrame = 44;
        private const int RipEnd = 54;
        private const int FlankTime = 64;
        private const int CycleLength = 78;
        private const int MergeMaxTime = 96;
        private const int FinaleOmenTime = 30;
        private const int FinaleTime = 96;
        #endregion

        private int PincerCycles(EowStateContext ctx) => ctx.IsPhase2 ? 3 : 2;
        private float DashSpeed(EowStateContext ctx) => (ctx.IsPhase2 ? 46f : 41f) + (ctx.IsDeathMode ? 4f : 0f);

        private enum Phase
        {
            Rip = 0,
            Flank = 1,
            Pincer = 2,
            Merge = 3,
            Finale = 4,
        }

        private Phase phase;
        private int cyclesDone;
        /// <summary>钳形侧位翻转(每周期交换)</summary>
        private int sideFlip = 1;
        private float groundY;
        private bool ripFxFired;
        private bool finaleBreachFired;
        private Vector2 headDashDir;
        private bool omenPlaced;

        public EowSplitPincerState() {
        }

        public override void OnEnter(EowStateContext context) {
            base.OnEnter(context);
            context.SkipDefaultMovement = false;
            phase = Phase.Rip;
            cyclesDone = 0;
            sideFlip = 1;
            ripFxFired = false;
            finaleBreachFired = false;
            omenPlaced = false;
        }

        public override IEowState OnUpdate(EowStateContext context) {
            Tick();
            switch (phase) {
                case Phase.Rip:
                    UpdateRip(context);
                    break;
                case Phase.Flank:
                    UpdateFlank(context);
                    break;
                case Phase.Pincer:
                    UpdatePincer(context);
                    break;
                case Phase.Merge:
                    UpdateMerge(context);
                    break;
                case Phase.Finale:
                    if (UpdateFinale(context)) {
                        return new EowWeaveState();
                    }
                    break;
            }
            return null;
        }

        private void SwitchPhase(Phase next) {
            phase = next;
            Timer = 0;
        }

        #region 撕裂分身
        private void UpdateRip(EowStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;

            //盘紧减速+分裂点预闪(PulsePhase 携带目标组数供边界预闪定位)
            float squeezeT = MathHelper.Clamp(Timer / (float)SqueezeTime, 0f, 1f);
            context.Compression = MathHelper.Lerp(1f, 0.62f, squeezeT * squeezeT);
            context.PulseKind = 4;
            context.PulsePhase = 3f;
            SetMovement(context, player.Center + new Vector2(0f, -430f), MathHelper.Lerp(15f, 6f, squeezeT), 1.2f);
            context.SlitherStrength = 0.3f * (1f - squeezeT);
            npc.damage = 0;

            if (Timer == 6) {
                SoundEngine.PlaySound(SoundID.Zombie13 with { Pitch = -0.6f, Volume = 1f }, npc.Center);
            }

            //分裂帧(服务端裁定，组数经同步槽下发)
            if (Timer == RipFrame && !VaultUtils.isClient) {
                context.SplitGroups = 3;
            }

            //撕裂表现帧：酸爆+吼声(各端本地，凭本地Timer)
            if (Timer >= RipFrame && !ripFxFired) {
                ripFxFired = true;
                EowMotionFX.PlayRoar(npc.Center, -0.1f, 1.15f);
                EowMotionFX.CameraPunch(npc.Center, 6f, 14, "EowRip");
                FireBoundaryRipFX(context, 3);
            }

            //撕开进度
            if (Timer > RipFrame) {
                context.SplitProgress = MathHelper.Clamp((Timer - RipFrame) / (float)(RipEnd - RipFrame), 0f, 1f);
                //三条分身向外弹开
                DeclareStations(context, player, 1.35f);
            }

            if (Timer >= RipEnd) {
                context.Compression = 1f;
                groundY = EowMotionFX.FindGroundBelow(player.Center).Y;
                SwitchPhase(Phase.Flank);
            }
        }

        /// <summary>组边界撕裂酸爆(客户端表现)</summary>
        private void FireBoundaryRipFX(EowStateContext context, int groups) {
            if (VaultUtils.isServer || context.Segments.Count == 0) {
                return;
            }
            int totalSegs = context.Segments.Count;
            for (int g = 1; g < groups; g++) {
                int b = EowSplitLayout.LeaderOrdinal(totalSegs, groups, g);
                if (b <= 0 || b >= totalSegs) {
                    continue;
                }
                NPC leader = context.Segments[b];
                NPC stump = context.Segments[b - 1];
                if (leader.Alives()) {
                    EowMotionFX.SpawnRipBurst(leader.Center, leader.rotation.ToRotationVector2(), 1.4f);
                }
                if (stump.Alives()) {
                    EowMotionFX.SpawnRipBurst(stump.Center, stump.rotation.ToRotationVector2(), 1.1f);
                }
            }
        }
        #endregion

        #region 占位
        /// <summary>三席站位：头=左高翼 组1=右高翼 组2=地底潜伏(随周期左右翻转)</summary>
        private void DeclareStations(EowStateContext context, Player player, float speedMul = 1f) {
            Vector2 leftWing = player.Center + new Vector2(-620f * sideFlip, -360f);
            Vector2 rightWing = player.Center + new Vector2(620f * sideFlip, -360f);
            Vector2 under = new Vector2(player.Center.X, groundY + 320f);

            SetMovement(context, leftWing, 30f * speedMul, 1.7f);
            context.GroupTargets[1] = rightWing;
            context.GroupSpeeds[1] = 30f * speedMul;
            context.GroupTurns[1] = 1.7f;
            context.GroupTargets[2] = under;
            context.GroupSpeeds[2] = 34f * speedMul;
            context.GroupTurns[2] = 1.6f;
        }

        private void UpdateFlank(EowStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;

            npc.damage = 0;
            groundY = EowMotionFX.FindGroundBelow(player.Center).Y;
            DeclareStations(context, player);
            context.SlitherStrength = 0.5f;

            if (Timer >= FlankTime) {
                omenPlaced = false;
                SwitchPhase(Phase.Pincer);
            }
        }
        #endregion

        #region 钳形对冲
        private void UpdatePincer(EowStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;
            groundY = EowMotionFX.FindGroundBelow(player.Center).Y;

            int t = Timer % CycleLength;
            int stagger = context.IsPhase2 ? 9 : 13;

            //默认保持站位(未被覆盖的组按站位走)
            DeclareStations(context, player);

            //预警段：两翼后拉蓄势
            if (t < 18) {
                Vector2 pull = player.Center + new Vector2(-(620f + 110f) * sideFlip, -380f);
                SetMovement(context, pull, 24f, 1.8f);
                context.GroupTargets[1] = player.Center + new Vector2((620f + 110f) * sideFlip, -380f);
                context.MawGlow = t / 18f;
                context.PulseKind = 4;
                context.PulsePhase = 3f;
                npc.damage = 0;

                if (t == 4) {
                    SoundEngine.PlaySound(SoundID.Zombie13 with { Pitch = -0.2f, Volume = 0.9f, MaxInstances = 3 }, npc.Center);
                }
                //地底席预兆(服务端每周期一次)
                if (!VaultUtils.isClient && t == 16 && !omenPlaced) {
                    omenPlaced = true;
                    Projectile.NewProjectile(npc.GetSource_FromAI(),
                        new Vector2(player.Center.X + player.velocity.X * 12f, groundY), Vector2.Zero,
                        ModContent.ProjectileType<EowBreachOmen>(), 0, 0f, Main.myPlayer, 22f, 0f);
                }
                return;
            }

            //头席冲刺帧
            if (t == 18) {
                Vector2 aim = player.Center + player.velocity * 10f;
                headDashDir = (aim - npc.Center).SafeNormalize(Vector2.UnitX);
                EowMotionFX.PlayRoar(npc.Center, 0.3f, 0.85f);
            }

            //头席冲刺行进(直线穿过)
            if (t >= 18 && t < 46) {
                context.SkipDefaultMovement = true;
                if (t == 18) {
                    npc.velocity = headDashDir * DashSpeed(context);
                    npc.netUpdate = true;
                }
                npc.velocity *= 1.008f;
                npc.rotation = npc.velocity.ToRotation() + MathHelper.PiOver2;
                npc.damage = npc.defDamage;
            }
            else if (t >= 46) {
                context.SkipDefaultMovement = false;
                npc.damage = 0;
            }

            //组1错拍反向冲刺
            if (t >= 18 + stagger && t < 46 + stagger) {
                if (t == 18 + stagger) {
                    Vector2 aim = player.Center + player.velocity * 10f;
                    //由右翼扑向玩家(与头对向交叉)
                    int lead1 = LeaderOrdinalOf(context, 1);
                    if (lead1 >= 0) {
                        NPC leader = context.Segments[lead1];
                        Vector2 dir = (aim - leader.Center).SafeNormalize(-Vector2.UnitX);
                        context.GroupDirectVelocity[1] = dir * DashSpeed(context);
                        if (!VaultUtils.isServer) {
                            SoundEngine.PlaySound(SoundID.ForceRoar with { Pitch = 0.5f, Volume = 0.7f, MaxInstances = 3 }, leader.Center);
                        }
                    }
                }
                else {
                    //保持直线(直控速度不变即维持惯性方向)
                    int lead1 = LeaderOrdinalOf(context, 1);
                    if (lead1 >= 0) {
                        NPC leader = context.Segments[lead1];
                        context.GroupDirectVelocity[1] = leader.velocity.SafeNormalize(Vector2.UnitX)
                            * Math.Min(leader.velocity.Length() * 1.008f, DashSpeed(context) * 1.2f);
                    }
                }
            }

            //地底席喷发(t=38起竖直破土，弧线回落)
            if (t >= 38 && t < CycleLength - 4) {
                int lead2 = LeaderOrdinalOf(context, 2);
                if (lead2 >= 0) {
                    NPC leader = context.Segments[lead2];
                    if (t == 38) {
                        //瞬移到预兆点正下方蓄势射出
                        if (!VaultUtils.isClient) {
                            leader.Center = new Vector2(player.Center.X, groundY + 620f);
                            leader.netUpdate = true;
                        }
                        context.GroupDirectVelocity[2] = -Vector2.UnitY * (DashSpeed(context) + 6f);
                        EowMotionFX.SpawnBreachBlast(new Vector2(leader.Center.X, groundY), 1.2f, -Vector2.UnitY);
                        EowMotionFX.CameraPunch(new Vector2(leader.Center.X, groundY), 5.5f, 12, "EowPincerErupt", -Vector2.UnitY);
                    }
                    else {
                        //重力弧线回落
                        Vector2 vel = leader.velocity + new Vector2(0f, 1.6f);
                        if (vel.Length() > DashSpeed(context) + 6f) {
                            vel = vel.SafeNormalize(Vector2.UnitY) * (DashSpeed(context) + 6f);
                        }
                        context.GroupDirectVelocity[2] = vel;
                    }
                }
            }

            //周期收束
            if (t == CycleLength - 1) {
                cyclesDone++;
                sideFlip = -sideFlip;
                omenPlaced = false;
                if (cyclesDone >= PincerCycles(context)) {
                    SwitchPhase(Phase.Merge);
                }
            }
        }

        /// <summary>组首节在 Segments 中的下标，未就绪返回-1(与主控驾驶同口径)</summary>
        private int LeaderOrdinalOf(EowStateContext context, int group) {
            int totalSegs = context.TotalSegments;
            if (totalSegs <= 0 || context.SplitGroups <= 1) {
                return -1;
            }
            int lead = EowSplitLayout.LeaderOrdinal(totalSegs, context.SplitGroups, group);
            if (lead < 0 || lead >= context.Segments.Count || !context.Segments[lead].Alives()) {
                return -1;
            }
            return lead;
        }
        #endregion

        #region 地底合体
        private void UpdateMerge(EowStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;
            groundY = EowMotionFX.FindGroundBelow(player.Center).Y;

            npc.damage = 0;
            //头潜回玩家下方深处，分身各自回链(主控 MergeHoming 驱动)
            context.MergeHoming = true;
            SetMovement(context, new Vector2(player.Center.X, groundY + 560f), 30f, 1.6f);
            context.SlitherStrength = 0.4f;
            //合拢进度回落(缝合表现)
            context.SplitProgress = MathHelper.Clamp(1f - Timer / 40f, 0f, 1f);

            //入土尘爆(头穿地表时一次)
            if (Timer == 1) {
                EowMotionFX.SpawnDirtBurst(new Vector2(npc.Center.X, groundY), 1.1f);
            }

            //全部回链或超时→合体收针
            bool docked = npc.TryGetOverride<EowHeadAI>(out var headOverride) && headOverride.AllLeadersDocked();
            if ((Timer > 24 && docked) || Timer > MergeMaxTime) {
                if (!VaultUtils.isClient) {
                    context.SplitGroups = 0;
                }
                //合体缝合酸光(地下不可见也无妨，掩护段序落位)
                FireBoundaryRipFX(context, 3);
                finaleBreachFired = false;
                omenPlaced = false;
                SwitchPhase(Phase.Finale);
            }
        }
        #endregion

        #region 合体巨喷收官
        private bool UpdateFinale(EowStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;

            //预兆蓄势
            if (Timer <= FinaleOmenTime) {
                context.SkipDefaultMovement = false;
                SetMovement(context, new Vector2(player.Center.X, groundY + 540f), 24f, 1.7f);
                npc.damage = 0;
                if (!VaultUtils.isClient && !omenPlaced) {
                    omenPlaced = true;
                    Projectile.NewProjectile(npc.GetSource_FromAI(),
                        new Vector2(player.Center.X, groundY), Vector2.Zero,
                        ModContent.ProjectileType<EowBreachOmen>(), 0, 0f, Main.myPlayer, FinaleOmenTime - 2, 0f);
                }
                return false;
            }

            //合体巨喷帧
            if (Timer == FinaleOmenTime + 1) {
                context.SkipDefaultMovement = true;
                npc.Center = new Vector2(player.Center.X, groundY + 700f);
                npc.velocity = -Vector2.UnitY * (DashSpeed(context) + 14f);
                npc.rotation = npc.velocity.ToRotation() + MathHelper.PiOver2;
                npc.netUpdate = true;
                SoundEngine.PlaySound(SoundID.ForceRoar with { Pitch = -0.1f, Volume = 1.2f }, player.Center);
            }

            context.SkipDefaultMovement = true;
            npc.damage = npc.velocity.Length() > 18f ? npc.defDamage : 0;

            //破土瞬间：巨尘爆+酸液环
            if (!finaleBreachFired && npc.Center.Y < groundY) {
                finaleBreachFired = true;
                Vector2 breachPoint = new Vector2(npc.Center.X, groundY);
                EowMotionFX.SpawnBreachBlast(breachPoint, 2.1f, -Vector2.UnitY);
                EowMotionFX.CameraPunch(breachPoint, 9f, 18, "EowMergeBreach", -Vector2.UnitY);
                if (!VaultUtils.isClient) {
                    int nova = context.IsPhase2 ? 8 : 6;
                    for (int i = 0; i < nova; i++) {
                        float spread = MathHelper.Lerp(-1.05f, 1.05f, nova <= 1 ? 0.5f : i / (float)(nova - 1));
                        Vector2 vel = (-Vector2.UnitY).RotatedBy(spread) * Main.rand.NextFloat(9f, 12.5f);
                        Projectile.NewProjectile(npc.GetSource_FromAI(), breachPoint - new Vector2(0, 10f), vel,
                            ModContent.ProjectileType<EowAcidGlob>(),
                            EowSpitBarrageState.SpitDamage(npc), 0f, Main.myPlayer, 2f);
                    }
                }
            }

            //弧线回落
            if (finaleBreachFired && Timer > FinaleOmenTime + 20) {
                npc.velocity.Y += 1.5f;
                npc.velocity.X += Math.Sign(npc.velocity.X == 0 ? 1f : npc.velocity.X) * 0.2f;
                float cap = DashSpeed(context) + 14f;
                if (npc.velocity.Length() > cap) {
                    npc.velocity = npc.velocity.SafeNormalize(Vector2.UnitY) * cap;
                }
                npc.rotation = npc.velocity.ToRotation() + MathHelper.PiOver2;
            }

            return Timer > FinaleOmenTime + FinaleTime;
        }
        #endregion

        public override void OnExit(EowStateContext context) {
            base.OnExit(context);
            context.SkipDefaultMovement = false;
            context.AccelRate = 0.07f;
            context.Npc.damage = context.Npc.defDamage;
            if (!VaultUtils.isClient) {
                context.SplitGroups = 0;
            }
            context.SplitProgress = 0f;
            context.MergeHoming = false;
        }
    }
}
