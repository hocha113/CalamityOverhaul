using CalamityOverhaul.Content.NPCs.BloomsandSerpents.Core;
using InnoVault.Actors;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BloomsandSerpents.States
{
    /// <summary>
    /// 沙柱腾跃：选最近滞留柱（无柱先召一根）→ 贴近柱脚 → 螺旋盘柱而上（腿抓柱身、
    /// 蓄力聚拢上膛）→ 柱顶盘紧静止一拍（收势即预告）→ 蹬柱上抛跳到空中（柱身同帧
    /// 塌沉 = 演出兑现 + 场地自清；滞空前段重瞄、末段死向 = 承诺）→ 锁向爆冲 → 硬刹收招。
    /// 速度分层：爆冲 48 介于掠冲 46 与漩涡 50 之间；伤害窗 = 速度门槛。
    /// 联机口径：柱选取各端本地同规则解析（SyncVar 几何一致），头位姿靠周期同步纠偏。
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)BssStateIndex.PillarVault, typeof(BssStateContext))]
    internal class BssPillarVaultState : BssStateBase
    {
        public override string StateName => "PillarVault";
        public override BssStateIndex StateIndex => BssStateIndex.PillarVault;

        private enum VaultPhase
        {
            Summon,   //找柱/召柱（等它可盘）
            Approach, //贴近柱脚
            Climb,    //螺旋盘柱而上
            Coil,     //柱顶盘紧收势
            Hop,      //蹬柱上抛滞空（跳到空中再冲）
            Flight,   //锁向爆冲
            Brake,    //硬刹
        }

        private VaultPhase phase;
        /// <summary>盘柱 actor 槽位（各端本地解析）</summary>
        private int pillarWho = -1;
        /// <summary>螺旋相位</summary>
        private float climbAngle;
        /// <summary>盘柱起点 Y</summary>
        private float climbStartY;
        /// <summary>锁定射向（锁向帧后死向 = 预告即承诺）</summary>
        private Vector2 lockedDir = Vector2.UnitX;
        /// <summary>权威端已召应急柱（防重复召）</summary>
        private bool summoned;

        public override void OnEnter(BssStateContext ctx) {
            base.OnEnter(ctx);
            phase = VaultPhase.Summon;
            pillarWho = -1;
            summoned = false;
            climbAngle = 0f;
        }

        /// <summary>解析盘柱引用（槽位失效返回 null）</summary>
        private BssSandPillar ResolvePillar() {
            if (pillarWho < 0 || pillarWho >= ActorLoader.MaxActorCount) {
                return null;
            }
            return ActorLoader.Actors[pillarWho] is BssSandPillar pillar && pillar.Active ? pillar : null;
        }

        public override IBssState OnUpdate(BssStateContext ctx) {
            NPC npc = ctx.Npc;

            switch (phase) {
                case VaultPhase.Summon:
                    UpdateSummon(ctx, npc);
                    break;
                case VaultPhase.Approach:
                    UpdateApproach(ctx, npc);
                    break;
                case VaultPhase.Climb:
                    UpdateClimb(ctx, npc);
                    break;
                case VaultPhase.Coil:
                    UpdateCoil(ctx, npc);
                    break;
                case VaultPhase.Hop:
                    UpdateHop(ctx, npc);
                    break;
                case VaultPhase.Flight:
                    UpdateFlight(ctx, npc);
                    break;
                case VaultPhase.Brake:
                    ctx.Mode = BssMoveMode.Direct;
                    ctx.LegCommand = BssLegCommand.March;
                    npc.velocity *= 0.66f;
                    if (npc.velocity.Length() > BssDirector.VaultContactSpeed) {
                        npc.damage = npc.defDamage;
                    }
                    Timer++;
                    if (Timer >= BssDirector.VaultBrakeFrames) {
                        return EndAttack(ctx);
                    }
                    break;
            }

            //超时保险兜底（含找不到柱的整体撤退）
            if (Counter++ > 60 * 9) {
                npc.velocity *= 0.6f;
                return EndAttack(ctx);
            }
            return null;
        }

        private void SwitchPhase(VaultPhase next) {
            phase = next;
            Timer = 0;
        }

        /// <summary>
        /// 找柱/召柱：优先场上滞留柱；没有就在蛇前方召一根。等柱成形期贴着柱脚
        /// 徘徊（不朝玩家爬走再折返的空转），柱可盘即入场。
        /// </summary>
        private void UpdateSummon(BssStateContext ctx, NPC npc) {
            ctx.Mode = BssMoveMode.Crawl;
            ctx.LegCommand = BssLegCommand.March;

            BssSandPillar climbable = BssSandPillar.FindNearestClimbable(npc.Center);
            if (climbable != null) {
                pillarWho = climbable.WhoAmI;
                SwitchPhase(VaultPhase.Approach);
                return;
            }

            //权威端召一根应急柱：落在蛇与玩家之间靠蛇一侧（无伤害的攀爬道具）
            if (!VaultUtils.isClient && !summoned) {
                summoned = true;
                float toward = FacingToTarget(ctx, 0f);
                Vector2 anchor = new(npc.Center.X + toward * 150f, npc.Center.Y);
                BssSandPillar.Spawn(npc, anchor, BssDirector.PillarHeightMax,
                    BssDirector.PillarWidth, 16, BssDirector.PillarSpikeLinger, armedPillar: false);
            }

            //有成形中的柱就守着它蓄势，没有才朝玩家压迫
            BssSandPillar forming = BssSandPillar.FindNearestForming(npc.Center);
            if (forming != null) {
                float dx = forming.CenterX - npc.Center.X;
                ctx.CrawlDirX = Math.Abs(dx) > 70f ? Math.Sign(dx) : ctx.CrawlDirX;
                ctx.CrawlSpeed = Math.Abs(dx) > 240f ? BssDirector.CrawlCruiseSpeed : 5f;
                ctx.GatherLevel = 0.3f;
            }
            else {
                ctx.CrawlDirX = FacingToTarget(ctx);
                ctx.CrawlSpeed = BssDirector.CrawlCruiseSpeed;
            }

            Timer++;
            //召柱迟迟不可用（探地失败被拒等）：放弃本招
            if (Timer > 80) {
                Counter = 60 * 9 + 1;
            }
        }

        /// <summary>贴近柱脚（早到早退）</summary>
        private void UpdateApproach(BssStateContext ctx, NPC npc) {
            BssSandPillar pillar = ResolvePillar();
            if (pillar == null || !pillar.Climbable) {
                SwitchPhase(VaultPhase.Summon);
                return;
            }

            float dx = pillar.CenterX - npc.Center.X;
            ctx.Mode = BssMoveMode.Crawl;
            ctx.CrawlDirX = Math.Sign(dx) != 0 ? Math.Sign(dx) : 1f;
            ctx.CrawlSpeed = BssDirector.CrawlChaseSpeed;
            ctx.LegCommand = BssLegCommand.March;

            Timer++;
            if (Math.Abs(dx) < 150f || Timer >= BssDirector.VaultApproachFrames) {
                climbStartY = npc.Center.Y;
                //起盘侧取当前所在侧（贴身入螺旋不跳位）
                climbAngle = npc.Center.X >= pillar.CenterX ? 0f : MathHelper.Pi;
                SwitchPhase(VaultPhase.Climb);
            }
        }

        /// <summary>
        /// 螺旋盘柱：X 绕柱轴摆、Y 缓动升顶（位置伺服），腿抓柱身逐步重新抓握，
        /// 蓄力聚拢一路上膛。柱中途失效则就地转收势（不空转）。
        /// </summary>
        private void UpdateClimb(BssStateContext ctx, NPC npc) {
            BssSandPillar pillar = ResolvePillar();
            if (pillar == null) {
                SwitchPhase(VaultPhase.Coil);
                return;
            }

            float p = MathHelper.Clamp(Timer / (float)BssDirector.VaultClimbFrames, 0f, 1f);
            float eased = p * p * (3f - 2f * p);
            //越盘越快（离心蓄力的读数）
            climbAngle += BssDirector.VaultClimbOmega * (0.8f + 0.6f * p);

            float orbit = pillar.PillarHalfWidth * BssDirector.VaultOrbitScale;
            Vector2 desired = new(
                pillar.CenterX + MathF.Cos(climbAngle) * orbit,
                MathHelper.Lerp(climbStartY, pillar.TopY - 46f, eased));

            ctx.Mode = BssMoveMode.Direct;
            npc.velocity = (desired - npc.Center) * 0.3f;

            DeclareGrip(ctx, pillar);
            ctx.GatherLevel = p * 0.7f;
            ctx.Compression = Math.Min(ctx.Compression, 0.92f);

            //盘擦柱身的刮沙（客户端）
            if (!Main.dedServ && Main.rand.NextBool(3)) {
                float side = Math.Sign(npc.Center.X - pillar.CenterX);
                Dust d = Dust.NewDustPerfect(
                    new Vector2(pillar.CenterX + side * pillar.PillarHalfWidth,
                        npc.Center.Y + Main.rand.NextFloat(-20f, 20f)),
                    DustID.Sand,
                    new Vector2(side * Main.rand.NextFloat(0.5f, 1.6f), Main.rand.NextFloat(0.5f, 2f)),
                    110, default, Main.rand.NextFloat(0.8f, 1.2f));
                d.noGravity = false;
            }

            Timer++;
            if (p >= 1f) {
                SwitchPhase(VaultPhase.Coil);
            }
        }

        /// <summary>
        /// 柱顶盘紧：钉在柱冠上收势（静止即预告），头追瞄压向，末段绷紧颤抖 + 亮花。
        /// 收满蹬柱上抛：跳向空中，柱身同帧塌沉。
        /// </summary>
        private void UpdateCoil(BssStateContext ctx, NPC npc) {
            int t = (int)Timer;
            BssSandPillar pillar = ResolvePillar();

            Vector2 hold = pillar != null
                ? new Vector2(pillar.CenterX, pillar.TopY - 52f)
                : npc.Center;
            ctx.Mode = BssMoveMode.Direct;
            npc.velocity = (hold - npc.Center) * 0.25f;

            if (pillar != null) {
                DeclareGrip(ctx, pillar);
            }
            else {
                ctx.LegCommand = BssLegCommand.Brace;
            }

            float progress = MathHelper.Clamp(t / (float)BssDirector.VaultCoilFrames, 0f, 1f);
            ctx.GatherLevel = 0.7f + 0.3f * progress;
            ctx.Compression = MathHelper.Lerp(0.92f, 0.85f, progress);
            ctx.FrontRaise = MathHelper.Clamp(progress * 1.4f, 0f, 0.8f);
            ctx.BloomGlow = Math.Max(ctx.BloomGlow, progress);

            //盘紧期头追瞄压向（正式锁向在滞空段收口）
            if (ctx.Target.Alives()) {
                Vector2 predicted = PredictTarget(ctx, 10f);
                lockedDir = (predicted - npc.Center).SafeNormalize(Vector2.UnitX);
            }
            npc.rotation = npc.rotation.AngleLerp(lockedDir.ToRotation() + BssHead.FacingRot, 0.2f);

            if (t == 1 && !Main.dedServ) {
                SoundEngine.PlaySound(SoundID.Item102 with { Volume = 0.7f, Pitch = -0.45f, MaxInstances = 2 }, npc.Center);
            }
            //末段绷紧颤抖
            if (progress > 0.65f && !Main.dedServ) {
                npc.position += Main.rand.NextVector2Circular(1.4f, 1.4f);
            }

            Timer++;
            if (t >= BssDirector.VaultCoilFrames) {
                Kickoff(ctx, npc, pillar);
                SwitchPhase(VaultPhase.Hop);
            }
        }

        /// <summary>蹬柱上抛：一帧定竖直初速跳向空中，柱身同帧塌沉（演出兑现 + 场地自清）</summary>
        private void Kickoff(BssStateContext ctx, NPC npc, BssSandPillar pillar) {
            if (!VaultUtils.isClient) {
                float toward = ctx.Target.Alives() ? FacingToTarget(ctx, 0f) : 1f;
                npc.velocity = new Vector2(toward * 5f, -BssDirector.VaultHopKick);
                npc.netUpdate = true;
                pillar?.CommandSink();
            }
            ctx.PulseGapWave(SerpentChainMath.WaveRelease, 0.1f);
            if (!Main.dedServ) {
                Vector2 foot = pillar != null ? new Vector2(pillar.CenterX, pillar.TopY) : npc.Center;
                BssVfx.SandBurst(foot, 1.6f);
                BssVfx.Shake(npc.Center, 4f, 1100f);
            }
        }

        /// <summary>
        /// 滞空：轻重力抛物悬一拍（跳到空中的读数），前段重瞄新锁向、
        /// 末 VaultLockLead 帧死向（预告即承诺），到拍锁向爆冲。
        /// </summary>
        private void UpdateHop(BssStateContext ctx, NPC npc) {
            int t = (int)Timer;
            ctx.Mode = BssMoveMode.Direct;
            ctx.LegCommand = BssLegCommand.Flail;
            npc.velocity.Y += 0.62f;
            npc.velocity.X *= 0.99f;

            //滞空前段重瞄（空中调整姿态盯住玩家），末段死向
            if (t <= BssDirector.VaultHopFrames - BssDirector.VaultLockLead && ctx.Target.Alives()) {
                Vector2 predicted = PredictTarget(ctx, 8f);
                lockedDir = (predicted - npc.Center).SafeNormalize(Vector2.UnitX);
            }
            npc.rotation = npc.rotation.AngleLerp(lockedDir.ToRotation() + BssHead.FacingRot, 0.24f);

            if (!Main.dedServ && Main.rand.NextBool(2)) {
                Dust d = Dust.NewDustPerfect(npc.Center + Main.rand.NextVector2Circular(20f, 20f),
                    DustID.Sand, -npc.velocity * 0.05f, 130, default, Main.rand.NextFloat(0.8f, 1.1f));
                d.noGravity = true;
            }

            Timer++;
            if (t >= BssDirector.VaultHopFrames) {
                Launch(ctx, npc);
                SwitchPhase(VaultPhase.Flight);
            }
        }

        /// <summary>锁向爆冲：一帧定速（力量在出手帧）+ 吼声震屏</summary>
        private void Launch(BssStateContext ctx, NPC npc) {
            if (!VaultUtils.isClient) {
                npc.velocity = lockedDir * BssDirector.VaultDashSpeed;
                npc.netUpdate = true;
            }
            ctx.PulseWhip(12f);
            ctx.PulseGapWave(SerpentChainMath.WaveRelease, 0.18f);
            if (!Main.dedServ) {
                BssVfx.Roar(npc.Center, -0.35f, 1f);
                BssVfx.Shake(npc.Center, 6f, 1300f);
            }
        }

        /// <summary>爆冲飞行：直线承诺不转向，速度门槛开伤害窗</summary>
        private void UpdateFlight(BssStateContext ctx, NPC npc) {
            ctx.Mode = BssMoveMode.Direct;
            ctx.LegCommand = BssLegCommand.Tuck;
            npc.velocity *= 1.012f;
            npc.rotation = npc.velocity.ToRotation() + BssHead.FacingRot;

            if (npc.velocity.Length() > BssDirector.VaultContactSpeed) {
                npc.damage = npc.defDamage;
            }

            if (!Main.dedServ && Main.GameUpdateCount % 2 == 0) {
                Dust d = Dust.NewDustPerfect(npc.Center + Main.rand.NextVector2Circular(18f, 18f),
                    DustID.Sand, -npc.velocity * 0.06f, 120, default, Main.rand.NextFloat(0.9f, 1.3f));
                d.noGravity = true;
            }

            Timer++;
            if (Timer >= BssDirector.VaultFlightFrames) {
                SwitchPhase(VaultPhase.Brake);
            }
        }

        /// <summary>盘柱期的抓握声明（每帧重声明）</summary>
        private static void DeclareGrip(BssStateContext ctx, BssSandPillar pillar) {
            ctx.LegCommand = BssLegCommand.Grip;
            ctx.LegGripActive = true;
            ctx.LegGripCenterX = pillar.CenterX;
            ctx.LegGripHalfWidth = pillar.PillarHalfWidth;
            ctx.LegGripTopY = pillar.TopY;
            ctx.LegGripBottomY = pillar.BaseY;
        }
    }
}
