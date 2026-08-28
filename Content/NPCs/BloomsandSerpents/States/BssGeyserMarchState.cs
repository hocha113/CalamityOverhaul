using CalamityOverhaul.Content.NPCs.BloomsandSerpents.Core;
using CalamityOverhaul.Content.NPCs.BloomsandSerpents.Projectiles;
using System;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BloomsandSerpents.States
{
    /// <summary>
    /// 沙泉行军：贴近 → 立起蓄势（前腿举离地面，立起剪影即预告）→ 前身砸地 →
    /// 冲击波从砸点沿地面向玩家行军，接连顶起隆包并喷发沙泉（P3 双向）。
    /// 四足腿架的高光时刻，也是本 boss 唯一的地面区域封锁。
    /// 公平阀声明：每泉先顶 22 帧隆包（脚下鼓包即警报，横移一步就出圈）；
    /// 泉距 120px 且泉口窄（±10px 喷柱）= 泉缝是常驻逃生道；喷发沙球近竖直上抛，
    /// 回落是可预读的第二拍。蛇砸完地即恢复爬行，行军由隆包实体自走（不站桩）。
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)BssStateIndex.GeyserMarch, typeof(BssStateContext))]
    internal class BssGeyserMarchState : BssStateBase
    {
        public override string StateName => "GeyserMarch";
        public override BssStateIndex StateIndex => BssStateIndex.GeyserMarch;

        private enum GeyserPhase
        {
            Approach, //贴近出手距离
            Rise,     //立起蓄势
            Slam,     //砸地
            March,    //沙泉行军（蛇已交还爬行）
        }

        private GeyserPhase phase;
        /// <summary>立起锚（X 定桩，立起不漂移）</summary>
        private Vector2 riseAnchor;
        /// <summary>砸点（行军原点）</summary>
        private Vector2 slamOrigin;
        /// <summary>行军方向（砸地帧锁定）</summary>
        private float marchDir = 1f;
        /// <summary>已布泉数（单向计数）</summary>
        private int spawned;
        private float prevY;

        public override void OnEnter(BssStateContext ctx) {
            base.OnEnter(ctx);
            phase = GeyserPhase.Approach;
            spawned = 0;
            prevY = ctx.Npc.Center.Y;
        }

        public override IBssState OnUpdate(BssStateContext ctx) {
            NPC npc = ctx.Npc;

            switch (phase) {
                case GeyserPhase.Approach:
                    UpdateApproach(ctx, npc);
                    break;
                case GeyserPhase.Rise:
                    UpdateRise(ctx, npc);
                    break;
                case GeyserPhase.Slam:
                    UpdateSlam(ctx, npc);
                    break;
                case GeyserPhase.March: {
                    IBssState next = UpdateMarch(ctx, npc);
                    if (next != null) {
                        return next;
                    }
                    break;
                }
            }

            prevY = npc.Center.Y;

            //超时保险兜底
            if (Counter++ > 60 * 6) {
                return EndAttack(ctx);
            }
            return null;
        }

        private void SwitchPhase(GeyserPhase next) {
            phase = next;
            Timer = 0;
        }

        /// <summary>贴近：爬到出手距离即早退（不磨蹭）</summary>
        private void UpdateApproach(BssStateContext ctx, NPC npc) {
            float dist = Math.Abs(ctx.Target.Center.X - npc.Center.X);
            ctx.Mode = BssMoveMode.Crawl;
            ctx.CrawlDirX = FacingToTarget(ctx);
            ctx.CrawlSpeed = BssDirector.CrawlChaseSpeed;
            ctx.LegCommand = BssLegCommand.March;

            Timer++;
            if (dist < 560f || Timer >= BssDirector.GeyserApproachFrames) {
                riseAnchor = npc.Center;
                if (!Main.dedServ) {
                    BssVfx.Roar(npc.Center, -0.3f, 0.85f);
                }
                SwitchPhase(GeyserPhase.Rise);
            }
        }

        /// <summary>立起蓄势：前腿举离地面、身体前倾昂起，立起剪影本身即预告</summary>
        private void UpdateRise(BssStateContext ctx, NPC npc) {
            float raise = MathHelper.Clamp(Timer / (float)BssDirector.GeyserRaiseFrames, 0f, 1f);
            float groundY = BssVfx.FindGroundY(new Vector2(riseAnchor.X, riseAnchor.Y - 200f));

            ctx.Mode = BssMoveMode.Direct;
            ctx.LegCommand = BssLegCommand.Raise;
            ctx.FrontRaise = raise;
            ctx.Compression = Math.Min(ctx.Compression, 0.94f);

            Vector2 pose = new(riseAnchor.X, groundY - BssDirector.CrawlRideHeight - 170f * raise);
            Vector2 desired = (pose - npc.Center) * 0.1f;
            npc.velocity = Vector2.Lerp(npc.velocity, desired, 0.3f);

            //末段绷紧 + 亮花
            if (raise > 0.7f) {
                ctx.BloomGlow = Math.Max(ctx.BloomGlow, (raise - 0.7f) * 3f);
                if (!Main.dedServ) {
                    npc.position += Main.rand.NextVector2Circular(1.2f, 1.2f);
                }
            }

            Timer++;
            if (Timer >= BssDirector.GeyserRaiseFrames) {
                //砸地：一帧定初速向下（带一点向玩家的横势）
                if (!VaultUtils.isClient) {
                    float dir = FacingToTarget(ctx, 0f);
                    npc.velocity = new Vector2(dir * 3f, BssDirector.GeyserSlamSpeed);
                    npc.netUpdate = true;
                }
                ctx.LegCommand = BssLegCommand.Flail;
                SwitchPhase(GeyserPhase.Slam);
            }
        }

        /// <summary>砸地：贴面即冲击（震屏 + 全髋下沉 + 鞭波），砸点即行军原点</summary>
        private void UpdateSlam(BssStateContext ctx, NPC npc) {
            ctx.Mode = BssMoveMode.Direct;
            ctx.LegCommand = BssLegCommand.Flail;

            float groundY = BssVfx.FindGroundY(npc.Center - new Vector2(0f, 260f), 800f);
            bool hit = prevY < groundY - 20f && npc.Center.Y >= groundY - 24f;

            Timer++;
            //贴面或兜底超时：落点定桩
            if (hit || Timer > 30f) {
                slamOrigin = new Vector2(npc.Center.X, groundY);
                marchDir = Math.Sign(ctx.Target.Center.X - slamOrigin.X);
                if (marchDir == 0f) {
                    marchDir = 1f;
                }
                ctx.PulseWhip(10f);
                for (int k = 0; k < ctx.StationBob.Length; k++) {
                    ctx.StationBob[k] = 1.2f;
                }
                if (!Main.dedServ) {
                    BssVfx.SandBurst(slamOrigin, 1.8f);
                    BssVfx.Roar(npc.Center, -0.5f, 1f);
                    BssVfx.Shake(npc.Center, 8f, 1400f);
                }
                SwitchPhase(GeyserPhase.March);
            }
        }

        /// <summary>
        /// 行军：每 GeyserStepGap 帧向前布一座自喷发隆包（P3 双向对称），
        /// 蛇本体交还爬行压迫，隆包实体自走自爆（招不站桩）。
        /// </summary>
        private IBssState UpdateMarch(BssStateContext ctx, NPC npc) {
            ctx.Mode = BssMoveMode.Crawl;
            ctx.CrawlDirX = FacingToTarget(ctx);
            ctx.CrawlSpeed = 8f;
            ctx.LegCommand = BssLegCommand.March;

            int t = (int)Timer;
            if (t % BssDirector.GeyserStepGap == 0 && spawned < BssDirector.GeyserCount) {
                spawned++;
                if (!VaultUtils.isClient) {
                    SpawnGeyserOmen(ctx, npc, marchDir, spawned);
                    //P3 双向：反向同拍对称布泉
                    if (ctx.Phase >= 3) {
                        SpawnGeyserOmen(ctx, npc, -marchDir, spawned);
                    }
                }
                //行军隆隆（各端本地）
                if (!Main.dedServ) {
                    BssVfx.Shake(slamOrigin, 1.5f, 900f);
                }
            }

            Timer++;
            //布完最后一泉再留一拍呼吸即收招（喷发由隆包自走，蛇不等）
            if (spawned >= BssDirector.GeyserCount && t > spawned * BssDirector.GeyserStepGap + 14) {
                return EndAttack(ctx);
            }
            return null;
        }

        /// <summary>沿地形布泉：步距即站缝声明，隆包 ai[1]=1 到期自喷发</summary>
        private void SpawnGeyserOmen(BssStateContext ctx, NPC npc, float dir, int step) {
            float posX = slamOrigin.X + dir * step * BssDirector.GeyserStepPx;
            float groundY = BssVfx.FindGroundY(new Vector2(posX, slamOrigin.Y - 300f), 900f);
            Projectile.NewProjectile(npc.GetSource_FromAI(),
                new Vector2(posX, groundY - 4f), Vector2.Zero,
                ModContent.ProjectileType<BssBreachOmen>(), 0, 0f, Main.myPlayer,
                BssDirector.GeyserOmenFrames, 1f, npc.whoAmI);
        }
    }
}
