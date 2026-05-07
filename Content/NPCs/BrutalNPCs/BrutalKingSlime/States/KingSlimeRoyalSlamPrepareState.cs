using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalKingSlime.Core;
using Terraria;
using Terraria.Audio;
using Terraria.Graphics.CameraModifiers;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalKingSlime.States
{
    /// <summary>
    /// 皇家砸地——蓄力阶段：蹲伏蓄力 → 爆发跃起 → 弧线飞行至玩家头顶 → 悬停蓄力，蓄满后切入 Falling。
    /// </summary>
    internal class KingSlimeRoyalSlamPrepareState : KingSlimeStateBase
    {
        public override string StateName => "RoyalSlamPrepare";
        public override KingSlimeStateIndex StateIndex => KingSlimeStateIndex.RoyalSlamPrepare;

        //子阶段：0 蹲伏蓄力 / 1 爆发跃起 / 2 弧线飞行 / 3 悬停蓄力
        private const int AnticipationTime = 16;
        private const int LeapTime = 22;
        private const int FlyArcMaxTime = 110;
        private const int HoverChargeTime = 70;

        private const float HoverHeight = 380f;
        private const float FlyArcSpeed = 22f;

        private int subPhase;
        private int phaseTimer;

        public override void OnEnter(KingSlimeStateContext context) {
            base.OnEnter(context);
            context.LastAttackKind = KingSlimeStateIndex.RoyalSlamPrepare;
            subPhase = 0;
            phaseTimer = 0;

            //跃起期间需自定义重力，避免地形/重力把演出节奏带乱
            context.Npc.noGravity = true;
            //蹲伏阶段保留地形碰撞，避免空中蹲伏的诡异画面
            context.Npc.noTileCollide = false;
        }

        public override IKingSlimeState OnUpdate(KingSlimeStateContext context) {
            phaseTimer++;
            Timer++;

            switch (subPhase) {
                case 0: HandleAnticipate(context); break;
                case 1: HandleLeap(context); break;
                case 2: HandleArcFly(context); break;
                case 3: {
                    var next = HandleHoverCharge(context);
                    if (next != null) return next;
                    break;
                }
            }

            return null;
        }

        #region 阶段0：蹲伏蓄力

        //深蹲——皇室凝胶被向下压实，外圈光屑向身体汇聚，提示玩家"大招要来了"
        private void HandleAnticipate(KingSlimeStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;
            FaceTargetX(npc, player);

            //摩擦减速，让史莱姆王立刻"扎根"在地面
            npc.velocity.X *= 0.78f;
            //贴地：在 noGravity 下手动模拟轻微重力，遇地面立刻归零
            if (!npc.collideY) {
                npc.velocity.Y = MathHelper.Min(npc.velocity.Y + 0.6f, 16f);
            }
            else {
                npc.velocity.Y = 0f;
            }

            float t = MathHelper.Clamp(phaseTimer / (float)AnticipationTime, 0f, 1f);
            //深蹲压扁
            context.SquishY = MathHelper.SmoothStep(0f, 0.55f, t);
            //逐步亮起皇室描边，做出"力量积聚"的视觉前置
            context.SetChargeState(1, t * 0.30f);

            //外圈粒子向身体汇聚——皇室凝胶被吸引
            if (!VaultUtils.isServer && phaseTimer % 2 == 0) {
                Vector2 dir = Main.rand.NextVector2CircularEdge(1f, 0.7f);
                Vector2 spawn = npc.Center + dir * Main.rand.NextFloat(110f, 160f);
                Dust dust = Dust.NewDustDirect(spawn - new Vector2(8, 8), 16, 16,
                    DustID.RedTorch, 0, 0, 100, default, 1.4f);
                dust.noGravity = true;
                dust.velocity = (npc.Center - spawn).SafeNormalize(Vector2.Zero) * 6f;
            }

            //蓄力中段轻微低频低音，营造蓄势感
            if (!VaultUtils.isServer && phaseTimer == AnticipationTime / 2) {
                SoundEngine.PlaySound(SoundID.NPCHit1 with { Pitch = -0.6f, Volume = 0.6f }, npc.Center);
            }

            if (phaseTimer >= AnticipationTime) {
                LaunchPowerLeap(context);
                subPhase = 1;
                phaseTimer = 0;
            }
        }

        private static void LaunchPowerLeap(KingSlimeStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;

            //横向给一点偏移让弧线更生动，但主体是大幅垂直起跳
            float dx = player.Center.X - npc.Center.X;
            float vx = MathHelper.Clamp(dx * 0.020f, -7f, 7f);
            float vy = -19f;
            npc.velocity = new Vector2(vx, vy);

            //跃起期间忽略地形——避免被天花板卡住打断演出
            npc.noTileCollide = true;

            if (!VaultUtils.isServer) {
                //更厚重的起跳声（跳跃声 + 低频砸地音）
                SoundEngine.PlaySound(SoundID.Item154 with { Pitch = -0.4f, Volume = 1.1f }, npc.Center);
                SoundEngine.PlaySound(SoundID.NPCDeath6 with { Pitch = 0.3f, Volume = 0.8f }, npc.Center);

                //起跳瞬间：脚下溅射一圈尘土与凝胶光屑
                for (int i = 0; i < 18; i++) {
                    float ang = -MathHelper.PiOver2 + Main.rand.NextFloat(-1.05f, 1.05f);
                    Vector2 dir = ang.ToRotationVector2();
                    Dust dust = Dust.NewDustDirect(npc.Bottom - new Vector2(8, 12), 16, 12,
                        DustID.RedTorch, dir.X * Main.rand.NextFloat(3f, 6.5f),
                        dir.Y * Main.rand.NextFloat(2.5f, 5.5f), 100, default, 1.6f);
                    dust.noGravity = true;
                }

                //轻微震屏，让起跳更有重量感
                Main.instance.CameraModifiers.Add(new PunchCameraModifier(
                    npc.Bottom, -Vector2.UnitY, 4f, 6f, 12, 1200f, "KingSlimeRoyalLeap"));
            }
        }

        #endregion

        #region 阶段1：爆发跃起

        private void HandleLeap(KingSlimeStateContext context) {
            NPC npc = context.Npc;

            //初期弱重力保留腾空感，峰值附近衰减到近零以便衔接弧线飞行
            float t = MathHelper.Clamp(phaseTimer / (float)LeapTime, 0f, 1f);
            float gravity = MathHelper.Lerp(0.45f, 0.05f, t);
            npc.velocity.Y += gravity;
            //空气阻力
            npc.velocity.X *= 0.985f;

            //上升过程被纵向拉伸成水滴
            context.SquishY = MathHelper.SmoothStep(0.55f, -0.42f, t);
            //蓄力描边强度继续上升，让玩家追着主体往上看
            context.SetChargeState(1, MathHelper.Lerp(0.30f, 0.55f, t));

            //拖尾粒子——皇室凝胶残光
            if (!VaultUtils.isServer && phaseTimer % 2 == 0) {
                Vector2 spawn = npc.Center + Main.rand.NextVector2Circular(20f, 24f);
                Dust trail = Dust.NewDustDirect(spawn - new Vector2(8, 8), 16, 16,
                    DustID.RedTorch, 0, 0, 100, default, 1.5f);
                trail.noGravity = true;
                trail.velocity = -npc.velocity * 0.30f;
            }

            if (phaseTimer >= LeapTime) {
                subPhase = 2;
                phaseTimer = 0;
            }
        }

        #endregion

        #region 阶段2：弧线飞行至玩家头顶

        private void HandleArcFly(KingSlimeStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;
            Vector2 hoverPos = player.Center + new Vector2(0, -HoverHeight);
            Vector2 toTarget = hoverPos - npc.Center;
            float dist = toTarget.Length();

            float t = MathHelper.Clamp(phaseTimer / (float)FlyArcMaxTime, 0f, 1f);

            if (!VaultUtils.isClient) {
                float speedScale = MathHelper.Clamp(dist / 300f, 0.25f, 1f);
                Vector2 desired = toTarget.SafeNormalize(Vector2.Zero) * FlyArcSpeed * speedScale;
                npc.velocity = Vector2.Lerp(npc.velocity, desired, 0.14f);
                npc.netUpdate = true;
            }

            context.SquishY = MathHelper.SmoothStep(-0.42f, 0.15f, t);
            context.SetChargeState(1, MathHelper.Lerp(0.55f, 0.85f, t));

            if (!VaultUtils.isServer && phaseTimer % 2 == 0) {
                Dust trail = Dust.NewDustDirect(
                    npc.Center + Main.rand.NextVector2Circular(18f, 18f) - new Vector2(8, 8),
                    16, 16, DustID.RedTorch, 0, 0, 120, default, 1.4f);
                trail.noGravity = true;
                trail.velocity = -npc.velocity * 0.25f;
            }

            if (dist < 40f || phaseTimer >= FlyArcMaxTime) {
                if (!VaultUtils.isClient) {
                    npc.Center = hoverPos;
                    npc.velocity = Vector2.Zero;
                    npc.netUpdate = true;
                }
                OnArriveAtHover(npc, hoverPos);
                subPhase = 3;
                phaseTimer = 0;
            }
        }

        private static void OnArriveAtHover(NPC npc, Vector2 pos) {
            if (VaultUtils.isServer) return;
            SoundEngine.PlaySound(SoundID.Item67 with { Pitch = 0.2f, Volume = 0.9f }, pos);
            int rays = 20;
            for (int i = 0; i < rays; i++) {
                float ang = MathHelper.TwoPi / rays * i;
                Vector2 dir = ang.ToRotationVector2();
                Dust dust = Dust.NewDustDirect(pos - new Vector2(8, 8), 16, 16,
                    DustID.RedTorch,
                    dir.X * Main.rand.NextFloat(5f, 9f),
                    dir.Y * Main.rand.NextFloat(5f, 9f),
                    100, default, 1.7f);
                dust.noGravity = true;
            }
            for (int i = 0; i < 8; i++) {
                float ang = Main.rand.NextFloat(MathHelper.TwoPi);
                Vector2 dir = ang.ToRotationVector2();
                Dust spark = Dust.NewDustDirect(pos - new Vector2(8, 8), 16, 16,
                    DustID.GoldFlame, dir.X * 5f, dir.Y * 5f, 80, default, 1.5f);
                spark.noGravity = true;
            }
            Main.instance.CameraModifiers.Add(new PunchCameraModifier(
                pos, Vector2.UnitX, 4f, 6f, 12, 1200f, "KingSlimeArcArrive"));
        }

        #endregion

        #region 阶段3：悬停蓄力

        private IKingSlimeState HandleHoverCharge(KingSlimeStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;

            float prog = MathHelper.Clamp(phaseTimer / (float)HoverChargeTime, 0f, 1f);

            npc.alpha = 0;

            if (!VaultUtils.isClient) {
                Vector2 desired = player.Center + new Vector2(0, -HoverHeight);
                Vector2 toDesired = desired - npc.Center;
                if (toDesired.LengthSquared() > 36f) {
                    npc.velocity = toDesired * 0.08f;
                }
                else {
                    npc.velocity = Vector2.Zero;
                    npc.Center = desired;
                }
            }
            else {
                npc.velocity = Vector2.Zero;
            }

            context.SquishY = MathHelper.SmoothStep(0.15f, 0.45f, prog);
            context.SetChargeState(1, MathHelper.Lerp(0.85f, 1f, prog));

            if (!VaultUtils.isServer && phaseTimer % 4 == 0) {
                Vector2 dustOffset = Main.rand.NextVector2Circular(60, 60);
                Dust dust = Dust.NewDustDirect(npc.Center + dustOffset - new Vector2(8, 8),
                    16, 16, DustID.RedTorch, 0, 0, 100, default, 1.4f);
                dust.noGravity = true;
                dust.velocity = (npc.Center - dust.position).SafeNormalize(Vector2.Zero) * 4f;
            }

            if (phaseTimer >= HoverChargeTime) {
                return new KingSlimeRoyalSlamFallingState();
            }
            return null;
        }

        #endregion

        public override void OnExit(KingSlimeStateContext context) {
            base.OnExit(context);
            context.Npc.noTileCollide = false;
            context.Npc.noGravity = false;
            context.SquishY = 0f;
            context.Npc.alpha = 0;
        }
    }
}
