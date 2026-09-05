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
    /// 旋沙龙卷（P2 起）：前身立起吸气 → 怒吼，沙暴在玩家上风侧的地面拧出 DevilCount 处旋沙 →
    /// 旋沙成形拔成高柱，顺风漂过玩家所在地带 → 蛇不等，收势即回巡曳压迫。
    /// 沙暴是这条虫的一部分：风向恒定（WindSign）、柱子顺风走，玩家从沙暴第一秒起就知道风往哪吹。
    /// 公平阀声明：成形期 DevilFormFrames 地面旋沙无伤（预告）；柱间距 DevilSpacing 即穿行道；
    /// 漂速慢于步行可绕；命中掀上天不硬控。P3 柱身甩瓣（由柱实体自管）。
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)BssStateIndex.DustDevil, typeof(BssStateContext))]
    internal class BssDustDevilState : BssStateBase
    {
        public override string StateName => "DustDevil";
        public override BssStateIndex StateIndex => BssStateIndex.DustDevil;

        private const int RoarFrame = 16;
        private const int RecoverFrames = 26;

        private Vector2 anchor;
        private float groundY;
        private bool summoned;

        public override void OnEnter(BssStateContext ctx) {
            base.OnEnter(ctx);
            anchor = ctx.Npc.Center;
            groundY = BssVfx.FindGroundY(anchor - new Vector2(0f, 60f));
            summoned = false;
        }

        public override IBssState OnUpdate(BssStateContext ctx) {
            NPC npc = ctx.Npc;
            int t = (int)Timer;
            int rearEnd = BssDirector.DevilRoarFrames;

            if (t < rearEnd) {
                //立起吸气 → 怒吼：立起剪影 + 吸气收沙是预告，怒吼帧点火
                float raise = MathHelper.Clamp(t / (float)RoarFrame, 0f, 1f);
                raise = raise * raise * (3f - 2f * raise);
                ctx.Mode = BssMoveMode.Direct;
                ctx.LegCommand = BssLegCommand.Raise;
                ctx.FrontRaise = raise * 0.85f;
                ctx.Compression = MathHelper.Lerp(1f, 0.9f, raise);
                Vector2 pose = new(anchor.X, groundY - BssDirector.CrawlRideHeight - 150f * raise);
                Vector2 desired = (pose - npc.Center) * 0.1f;
                if (desired.Length() > 8f) {
                    desired = desired.SafeNormalize(Vector2.Zero) * 8f;
                }
                npc.velocity = Vector2.Lerp(npc.velocity, desired, 0.25f);
                npc.rotation = npc.rotation.AngleLerp(new Vector2(0.35f * ctx.WindSign, -1f).ToRotation() + BssHead.FacingRot, 0.14f);

                if (t < RoarFrame) {
                    DeclareJaw(ctx, BssJawCommand.Inhale, raise);
                    //吸气：风沙向嘴收束（各端本地）
                    if (!Main.dedServ && t > 4 && Main.GameUpdateCount % 2 == 0) {
                        Vector2 mouth = BssClawScript.MouthPos(npc.Center, npc.rotation, npc.scale);
                        Vector2 from = mouth + Main.rand.NextVector2CircularEdge(1f, 1f) * Main.rand.NextFloat(40f, 90f);
                        Dust d = Dust.NewDustPerfect(from, DustID.Sand, (mouth - from) * 0.12f, 120, default, 1f);
                        d.noGravity = true;
                    }
                }
                else {
                    DeclareRoarHold(ctx, t - RoarFrame, 24);
                    ctx.StormLevel = Math.Max(ctx.StormLevel, 1f);
                }

                if (t == RoarFrame && !summoned) {
                    summoned = true;
                    Summon(ctx, npc);
                }
            }
            else {
                //收势：吼声余韵里压回去，柱子自己漂
                ctx.Mode = BssMoveMode.Crawl;
                ctx.CrawlDirX = FacingToTarget(ctx);
                ctx.CrawlSpeed = MathHelper.Lerp(0f, BssDirector.CrawlCruiseSpeed,
                    MathHelper.Clamp((t - rearEnd) / 14f, 0f, 1f));
                ctx.LegCommand = BssLegCommand.March;
                DeclareRoarHold(ctx, t - RoarFrame, 30);
            }

            Timer++;
            if (t > rearEnd + RecoverFrames || t > 60 * 3) {
                return EndAttack(ctx);
            }
            return null;
        }

        /// <summary>
        /// 怒吼点火：玩家上风侧沿风向按 DevilSpacing 布 DevilCount 处旋沙起点（贴地形），
        /// 全部顺风漂向玩家地带。表现各端本地，实体权威端。
        /// </summary>
        private void Summon(BssStateContext ctx, NPC npc) {
            int wind = ctx.WindSign;
            ctx.PulseWhip(9f);
            if (!Main.dedServ) {
                BssVfx.Roar(npc.Center, -0.5f, 1.1f);
                BssVfx.Shake(npc.Center, 7f, 1500f);
                SoundEngine.PlaySound(SoundID.Item102 with { Volume = 0.6f, Pitch = -0.7f, MaxInstances = 2 }, npc.Center);
                //吼出的风：嘴前一口沙顺风甩出去
                Vector2 mouth = BssClawScript.MouthPos(npc.Center, npc.rotation, npc.scale);
                for (int i = 0; i < 20; i++) {
                    Dust d = Dust.NewDustPerfect(mouth + Main.rand.NextVector2Circular(16f, 16f), DustID.Sand,
                        new Vector2(wind * Main.rand.NextFloat(6f, 14f), Main.rand.NextFloat(-3f, 1f)),
                        110, default, Main.rand.NextFloat(1f, 1.6f));
                    d.noGravity = true;
                }
            }
            if (VaultUtils.isClient || !ctx.Target.Alives()) {
                return;
            }

            int count = BssDirector.DevilCount(ctx.Phase);
            int damage = BssDirector.ScaleProjectileDamage(npc, BssDirector.DustDevilDamage);
            int type = ModContent.ProjectileType<BssDustDevilProj>();
            for (int i = 0; i < count; i++) {
                float x = ctx.Target.Center.X - wind * (BssDirector.DevilLeadOffset + i * BssDirector.DevilSpacing);
                float g = BssVfx.FindGroundY(new Vector2(x, ctx.Target.Center.Y - 300f), 1200f);
                Projectile.NewProjectile(npc.GetSource_FromAI(), new Vector2(x, g - 8f), Vector2.Zero,
                    type, damage, 0.8f, Main.myPlayer, wind, npc.whoAmI, BssDirector.DevilFormFrames + i * 6);
            }
            npc.netUpdate = true;
        }
    }
}
