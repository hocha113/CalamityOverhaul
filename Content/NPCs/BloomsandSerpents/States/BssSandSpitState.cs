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
    /// 喷沙（行进间齐射）：身体不停、继续压向玩家，头短暂跟踪 → 锁向吸气 →
    /// 沿扇面车道轮转齐射（外→中→内收拢的扫射读法）。
    /// 公平阀：中心 0 位恒不发射（声明的中央走廊）；锁向后不追瞄；
    /// 最小射距 230（贴脸直接收招 = 邀请骑脸）；重力弧线慢弹本身可读。
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)BssStateIndex.SandSpit, typeof(BssStateContext))]
    internal class BssSandSpitState : BssStateBase
    {
        public override string StateName => "SandSpit";
        public override BssStateIndex StateIndex => BssStateIndex.SandSpit;

        private const int LockFrame = BssDirector.SpitTrackFrames;
        private const int FireFrom = LockFrame + BssDirector.SpitInhaleFrames;
        private int FireEnd => FireFrom + BssDirector.SpitVolleys * BssDirector.SpitVolleyGap;

        /// <summary>车道对轮转表（弧度）：外→中→内收拢扫射，中心 0 位恒空 = 声明的中央走廊</summary>
        private static readonly float[] LanePairs = { 0.63f, 0.42f, 0.21f };

        /// <summary>锁定射向（权威端裁决弹幕；各端本地跟出姿态）</summary>
        private Vector2 lockedAim = Vector2.UnitX;
        private bool aimLocked;

        public override void OnEnter(BssStateContext ctx) {
            base.OnEnter(ctx);
            aimLocked = false;
        }

        public override IBssState OnUpdate(BssStateContext ctx) {
            NPC npc = ctx.Npc;
            int t = (int)Timer;

            //全程行进：身体压向玩家，头管瞄准
            ctx.Mode = BssMoveMode.Crawl;
            ctx.CrawlDirX = FacingToTarget(ctx);
            ctx.CrawlSpeed = t < LockFrame ? BssDirector.CrawlCruiseSpeed
                : t < FireEnd ? 5f : BssDirector.CrawlCruiseSpeed;
            ctx.LegCommand = BssLegCommand.March;
            ctx.FrontRaise = MathHelper.Clamp(t / 10f, 0f, 0.5f);

            //跟踪与锁向
            if (!aimLocked) {
                Vector2 aim = (ctx.Target.Center - npc.Center).SafeNormalize(Vector2.UnitX);
                lockedAim = t == 0 ? aim : Vector2.Lerp(lockedAim, aim, 0.3f).SafeNormalize(Vector2.UnitX);
                if (t >= LockFrame) {
                    aimLocked = true;
                    //最小射距阀：贴脸不吐沙，直接收招（邀请骑脸）
                    if (Vector2.Distance(npc.Center, ctx.Target.Center) < BssDirector.SpitMinDistance) {
                        Timer = FireEnd + 1;
                        if (!Main.dedServ) {
                            BssVfx.Roar(npc.Center, 0.1f, 0.5f);
                        }
                    }
                }
            }
            ctx.AimAngle = lockedAim.ToRotation();

            //吸气表现：沙尘向嘴收束
            if (!Main.dedServ && t >= LockFrame && t < FireFrom && Main.GameUpdateCount % 2 == 0) {
                Vector2 mouth = npc.Center + lockedAim * 28f;
                Vector2 from = mouth + Main.rand.NextVector2CircularEdge(1f, 1f) * Main.rand.NextFloat(26f, 54f);
                Dust d = Dust.NewDustPerfect(from, DustID.Sand, (mouth - from) * 0.13f, 120, default, 0.9f);
                d.noGravity = true;
            }

            //轮转齐射：每轮 2 发对称车道，外→中→内收拢
            if (t >= FireFrom && t < FireEnd && (t - FireFrom) % BssDirector.SpitVolleyGap == 0) {
                int volley = (t - FireFrom) / BssDirector.SpitVolleyGap;
                float lane = LanePairs[volley % LanePairs.Length];
                Vector2 mouth = npc.Center + lockedAim * 28f;
                //后坐：每口喷沙头向后一顿
                npc.velocity -= lockedAim * 2.6f;
                if (!Main.dedServ) {
                    SoundEngine.PlaySound(SoundID.Item17 with { Volume = 0.65f, Pitch = -0.2f, MaxInstances = 3 }, mouth);
                    for (int i = 0; i < 4; i++) {
                        Dust d = Dust.NewDustPerfect(mouth, DustID.Sand,
                            lockedAim.RotatedByRandom(0.5f) * Main.rand.NextFloat(2f, 5f), 100, default, 1.1f);
                        d.noGravity = false;
                    }
                }
                if (!VaultUtils.isClient) {
                    int damage = BssDirector.ScaleProjectileDamage(npc, BssDirector.SandGlobDamage);
                    int type = ModContent.ProjectileType<BssSandGlob>();
                    for (int s = -1; s <= 1; s += 2) {
                        Vector2 vel = lockedAim.RotatedBy(lane * s) * BssDirector.SandGlobSpeed
                            + new Vector2(0f, -1.4f);
                        Projectile.NewProjectile(npc.GetSource_FromAI(), mouth, vel, type, damage, 0.6f, Main.myPlayer);
                    }
                }
            }

            Timer++;

            if (t > FireEnd + 8 || t > 130) {
                return EndAttack(ctx);
            }
            return null;
        }
    }
}
