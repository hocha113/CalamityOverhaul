using CalamityOverhaul.Content.NPCs.BloomsandSerpents.Core;
using CalamityOverhaul.Content.NPCs.BloomsandSerpents.Projectiles;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BloomsandSerpents.States
{
    /// <summary>
    /// 刺球抛掷：立起 → 后仰蓄势（8 次幂迟滞收势）→ 甩头齐抛 2~4 颗刺球。
    /// 落点包夹玩家原位但不追瞄；球有弹跳 + 引信闪烁两重预告，钉刺扇留贴地逃生道。
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)BssStateIndex.CactusBall, typeof(BssStateContext))]
    internal class BssCactusBallState : BssStateBase
    {
        public override string StateName => "CactusBall";
        public override BssStateIndex StateIndex => BssStateIndex.CactusBall;

        private const int WindupFrames = 12;
        private const int RecoverFrames = 14;
        /// <summary>抛射解算的飞行帧数</summary>
        private const float LobFlightTime = 52f;
        /// <summary>落点包夹表（相对玩家原位的横向偏移）</summary>
        private static readonly float[] BracketOffsets = { -150f, 40f, 190f, -260f, 330f };

        private Vector2 anchor;
        private float groundY;
        private float throwDir = 1f;

        public override void OnEnter(BssStateContext ctx) {
            base.OnEnter(ctx);
            anchor = ctx.Npc.Center;
            groundY = BssVfx.FindGroundY(anchor - new Vector2(0f, 60f));
        }

        public override IBssState OnUpdate(BssStateContext ctx) {
            NPC npc = ctx.Npc;
            int t = (int)Timer;

            if (t == 0) {
                throwDir = Math.Sign(ctx.Target.Center.X - npc.Center.X);
                if (throwDir == 0f) {
                    throwDir = 1f;
                }
            }

            ctx.Mode = BssMoveMode.Direct;
            ctx.LegCommand = BssLegCommand.Raise;
            ctx.FrontRaise = MathHelper.Clamp(t / 14f, 0f, 1f);

            if (t < WindupFrames) {
                //后仰蓄势：8 次幂迟滞，最后几帧才猛然吸满（迟滞收势 = 出手的力量）
                float late = MathF.Pow(t / (float)WindupFrames, 8f);
                Vector2 pose = new(anchor.X - throwDir * (14f + 30f * late),
                    groundY - BssDirector.CrawlRideHeight - 110f - 26f * late);
                Vector2 desired = (pose - npc.Center) * 0.1f;
                if (desired.Length() > 8f) {
                    desired = desired.SafeNormalize(Vector2.Zero) * 8f;
                }
                npc.velocity = Vector2.Lerp(npc.velocity, desired, 0.22f);
                npc.rotation = npc.rotation.AngleLerp(
                    new Vector2(throwDir, -0.5f).ToRotation() + BssHead.FacingRot - throwDir * 0.5f * late, 0.18f);
            }
            else if (t == WindupFrames) {
                //甩头释放：一帧向前抽，球齐出手，鞭波顺链而下
                npc.velocity = new Vector2(throwDir * 10f, -3.5f);
                ctx.PulseWhip(9f);
                if (!Main.dedServ) {
                    SoundEngine.PlaySound(SoundID.Item1 with { Volume = 0.9f, Pitch = -0.5f }, npc.Center);
                    BssVfx.Roar(npc.Center, -0.15f, 0.6f);
                }
                if (!VaultUtils.isClient && ctx.Target.Alives()) {
                    int count = BssDirector.BallCount(ctx.Phase);
                    int damage = BssDirector.ScaleProjectileDamage(npc, BssDirector.CactusBallDamage);
                    int type = ModContent.ProjectileType<BssCactusBallProj>();
                    Vector2 mouth = npc.Center + new Vector2(throwDir * 24f, -10f);
                    for (int i = 0; i < count; i++) {
                        //落点包夹玩家原位（不追瞄），抛物初速反解共用弹幕重力常数
                        float targetX = ctx.Target.Center.X + BracketOffsets[i];
                        float landY = BssVfx.FindGroundY(new Vector2(targetX, ctx.Target.Center.Y - 240f));
                        float dx = targetX - mouth.X;
                        float dy = landY - mouth.Y;
                        float vx = dx / LobFlightTime;
                        float vy = dy / LobFlightTime - 0.5f * BssDirector.BallGravity * LobFlightTime;
                        Projectile.NewProjectile(npc.GetSource_FromAI(), mouth,
                            new Vector2(vx, vy), type, damage, 1f, Main.myPlayer);
                    }
                }
            }
            else {
                //收势：不停步，直接压回去
                ctx.Mode = BssMoveMode.Crawl;
                ctx.CrawlSpeed = BssDirector.CrawlCruiseSpeed;
                ctx.CrawlDirX = FacingToTarget(ctx);
                ctx.LegCommand = BssLegCommand.March;
            }

            Timer++;

            if (t > WindupFrames + RecoverFrames || t > 120) {
                return EndAttack(ctx);
            }
            return null;
        }
    }
}
