using CalamityOverhaul.Content.NPCs.BloomsandSerpents.Core;
using CalamityOverhaul.Content.NPCs.BloomsandSerpents.Projectiles;
using System;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BloomsandSerpents.States
{
    /// <summary>
    /// 升空祭舞沙尘爆：冲向天空锚点 → 悬停三拍祭祀编舞（鳌足展开大张 → 环绕划弧 →
    /// 过顶合掌；沙暴与亮花随舞拉满，风沙向合掌点收束）→ 合掌拍怒吼，权威端环玩家
    /// 布下风暴标记（旋沙预告 66 帧）→ 标记到期各自立起沙尘暴（复用原版 657 本体，
    /// 自控伤害 + 寿命钳短）→ 俯冲回地收招。
    /// 公平口径：悬停编舞全程 = 白给输出窗；落点相互 ≥250px = 走廊声明；
    /// 标记预告 66 帧且不追人（放下即承诺）。
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)BssStateIndex.StormRite, typeof(BssStateContext))]
    internal class BssStormRiteState : BssStateBase
    {
        public override string StateName => "StormRite";
        public override BssStateIndex StateIndex => BssStateIndex.StormRite;

        private enum RitePhase
        {
            Ascend, //冲天就位
            Dance,  //悬停祭舞
            Exit,   //俯冲回地
        }

        private RitePhase phase;
        /// <summary>天空锚（入场帧锁定）</summary>
        private Vector2 skyAnchor;
        /// <summary>已放召唤（权威端防重复）</summary>
        private bool summoned;

        public override void OnEnter(BssStateContext ctx) {
            base.OnEnter(ctx);
            phase = RitePhase.Ascend;
            summoned = false;
            float toward = ctx.Target.Alives()
                ? Math.Sign(ctx.Target.Center.X - ctx.Npc.Center.X) : 1f;
            skyAnchor = ctx.Npc.Center + new Vector2(toward * 130f, -BssDirector.RiteAscendHeight);
        }

        public override IBssState OnUpdate(BssStateContext ctx) {
            NPC npc = ctx.Npc;

            switch (phase) {
                case RitePhase.Ascend:
                    UpdateAscend(ctx, npc);
                    break;
                case RitePhase.Dance:
                    UpdateDance(ctx, npc);
                    break;
                case RitePhase.Exit: {
                    IBssState next = UpdateExit(ctx, npc);
                    if (next != null) {
                        return next;
                    }
                    break;
                }
            }

            //超时保险兜底
            if (Counter++ > 60 * 9) {
                npc.velocity *= 0.6f;
                return EndAttack(ctx);
            }
            return null;
        }

        private void SwitchPhase(RitePhase next) {
            phase = next;
            Timer = 0;
        }

        /// <summary>冲天就位（到锚即早退）</summary>
        private void UpdateAscend(BssStateContext ctx, NPC npc) {
            ctx.Mode = BssMoveMode.Steer;
            ctx.MoveTarget = skyAnchor;
            ctx.MoveSpeed = 26f;
            ctx.TurnSpeed = 3f;
            ctx.AccelRate = 0.12f;
            ctx.Slither = 0.35f;
            ctx.ClawCommand = BssClawCommand.Tuck;

            Timer++;
            if (Vector2.Distance(npc.Center, skyAnchor) < 80f || Timer >= BssDirector.RiteAscendFrames) {
                SwitchPhase(RitePhase.Dance);
            }
        }

        /// <summary>
        /// 悬停祭舞：位置伺服钉在天空锚（微沉浮），头压向玩家侧，鳌足走三拍编舞；
        /// 合掌拍怒吼 + 布风暴标记。
        /// </summary>
        private void UpdateDance(BssStateContext ctx, NPC npc) {
            int t = (int)Timer;
            float rite01 = MathHelper.Clamp(t / (float)BssDirector.RiteFrames, 0f, 1f);

            //悬停伺服 + 呼吸沉浮
            Vector2 hold = skyAnchor + new Vector2(0f, MathF.Sin(t * 0.06f) * 16f);
            ctx.Mode = BssMoveMode.Direct;
            npc.velocity = Vector2.Lerp(npc.velocity, (hold - npc.Center) * 0.09f, 0.3f);

            //头压向玩家侧微俯（仪式主持的姿态）
            if (ctx.Target.Alives()) {
                float toward = Math.Sign(ctx.Target.Center.X - npc.Center.X);
                if (toward == 0f) {
                    toward = 1f;
                }
                float poseAng = new Vector2(toward * 0.85f, 0.5f).ToRotation();
                npc.rotation = npc.rotation.AngleLerp(poseAng + BssHead.FacingRot, 0.1f);
            }

            //鳌足三拍编舞
            ctx.ClawCommand = BssClawCommand.Rite;
            ctx.ClawPhase = rite01;
            ctx.BloomGlow = Math.Max(ctx.BloomGlow, rite01 * 0.9f);
            ctx.StormLevel = Math.Min(ctx.StormLevel + 0.015f, 1f);
            ctx.Compression = Math.Min(ctx.Compression, MathHelper.Lerp(1f, 0.9f, rite01));
            ctx.GatherLevel = rite01 > 0.72f ? (rite01 - 0.72f) / 0.28f : 0f;

            //风沙向合掌点收束（第三拍的因果读数：沙暴是被"聚"出来的）
            if (!Main.dedServ && rite01 > 0.4f && Main.GameUpdateCount % 2 == 0) {
                Vector2 clasp = BssClawScript.ClaspPoint(npc.Center);
                Vector2 from = clasp + Main.rand.NextVector2CircularEdge(1f, 1f) * Main.rand.NextFloat(90f, 220f);
                Dust d = Dust.NewDustPerfect(from, Terraria.ID.DustID.Sand,
                    (clasp - from) * 0.06f, 130, default, Main.rand.NextFloat(0.8f, 1.2f));
                d.noGravity = true;
            }

            //合掌召唤拍：怒吼 + 鞭波 + 权威端布标记
            if (t == BssDirector.RiteClaspBeat) {
                ctx.PulseWhip(10f);
                ctx.PulseGapWave(SerpentChainMath.WavePress, 0.12f);
                if (!Main.dedServ) {
                    BssVfx.Roar(npc.Center, -0.55f, 1.15f);
                    BssVfx.Shake(npc.Center, 7f, 1600f);
                }
                if (!VaultUtils.isClient && !summoned && ctx.Target.Alives()) {
                    summoned = true;
                    PlaceStorms(ctx, npc);
                }
            }

            Timer++;
            if (t >= BssDirector.RiteFrames + 10) {
                SwitchPhase(RitePhase.Exit);
            }
        }

        /// <summary>
        /// 权威端布风暴标记：玩家两翼交替、车道间距散开（走廊声明），贴地面高度
        /// （沙尘暴身形高 ~240px，标记放在离地 240px 的柱心）；探不到地空中直放。
        /// </summary>
        private static void PlaceStorms(BssStateContext ctx, NPC npc) {
            int count = BssDirector.RiteStormCount(ctx.Phase);
            int damage = BssDirector.ScaleProjectileDamage(npc, BssDirector.StormDamage);
            int type = ModContent.ProjectileType<BssStormMark>();

            for (int k = 0; k < count; k++) {
                //两翼交替车道：+1, -1, +2, -2, +3, -3, +4
                int lane = (k / 2 + 1) * ((k & 1) == 0 ? 1 : -1);
                float x = ctx.Target.Center.X + lane * BssDirector.RiteStormSpacing
                    + Main.rand.NextFloat(-40f, 40f);
                float groundY = BssVfx.FindGroundY(new Vector2(x, ctx.Target.Center.Y - 60f), 1000f);
                float y = groundY >= ctx.Target.Center.Y + 760f
                    ? ctx.Target.Center.Y - 40f   //探不到地：空中直放（657 无碰撞需求）
                    : groundY - 240f;             //贴地：柱心抬到沙尘暴身形高度
                Projectile.NewProjectile(npc.GetSource_FromAI(), new Vector2(x, y), Vector2.Zero,
                    type, 0, 0f, Main.myPlayer,
                    BssDirector.RiteMarkFrames, damage, BssDirector.RiteNadoLife);
            }
        }

        /// <summary>俯冲回地：斜插向玩家侧地面，贴地即交还压迫</summary>
        private IBssState UpdateExit(BssStateContext ctx, NPC npc) {
            float toward = ctx.Target.Alives()
                ? Math.Sign(ctx.Target.Center.X - npc.Center.X) : 1f;
            if (toward == 0f) {
                toward = 1f;
            }
            float exitX = npc.Center.X + toward * 220f;
            float groundY = BssVfx.FindGroundY(new Vector2(exitX, npc.Center.Y), 1400f);

            ctx.Mode = BssMoveMode.Steer;
            ctx.MoveTarget = new Vector2(exitX, groundY - BssDirector.CrawlRideHeight);
            ctx.MoveSpeed = 28f;
            ctx.TurnSpeed = 3.2f;
            ctx.AccelRate = 0.14f;
            ctx.ClawCommand = BssClawCommand.Tuck;

            Timer++;
            bool arrived = npc.Center.Y > groundY - BssDirector.CrawlRideHeight - 60f;
            if (arrived || Timer >= BssDirector.RiteExitFrames) {
                npc.velocity *= 0.6f;
                return EndAttack(ctx);
            }
            return null;
        }
    }
}
