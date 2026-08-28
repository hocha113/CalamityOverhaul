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
    /// 盘天环猎：腾空绕玩家成环，环径由 450 收紧到 310，P2 起红花节按拍向环心射刺，
    /// 时限一到亮花怒吼 + 穿心突刺收束，落地转爬行。
    /// 公平阀声明：体长（约 21 节 × 40px ≈ 840px）远小于环周长，链条覆盖不满一圈，
    /// 头尾之间的开口弧就是持续存在且随环转动的逃生门；向心刺射向环心（非追踪玩家）、
    /// 每拍每花 1 枚且预亮 10 帧；穿心突刺锁定预告起始帧的环心，不追瞄。
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)BssStateIndex.CoilOrbit, typeof(BssStateContext))]
    internal class BssCoilOrbitState : BssStateBase
    {
        public override string StateName => "CoilOrbit";
        public override BssStateIndex StateIndex => BssStateIndex.CoilOrbit;

        private const int EntryFrames = 22;
        private int OrbitEnd => EntryFrames + BssDirector.OrbitDuration(phaseAtEnter);
        private int ExitFrame => OrbitEnd + BssDirector.OrbitExitTelegraph;
        private const int ExitFlightFrames = 26;

        private int phaseAtEnter = 1;
        /// <summary>环角（各端同速推进，实际运动以权威端同步为准）</summary>
        private float orbitAngle;
        /// <summary>环转方向（进入时按相对位置定死，各端一致）</summary>
        private float orbitSign = 1f;
        /// <summary>环心（缓跟玩家，甩不掉但可从开口弧走脱）</summary>
        private Vector2 anchor;
        /// <summary>穿心突刺锁点</summary>
        private Vector2 exitLock;
        private bool exitDashed;
        private float prevY;

        public override void OnEnter(BssStateContext ctx) {
            base.OnEnter(ctx);
            phaseAtEnter = Math.Max(ctx.Phase, 1);
            anchor = ctx.Target.Alives() ? ctx.Target.Center : ctx.Npc.Center;
            orbitAngle = (ctx.Npc.Center - anchor).ToRotation();
            orbitSign = ctx.Npc.Center.X < anchor.X ? 1f : -1f;
            exitDashed = false;
            prevY = ctx.Npc.Center.Y;
            ctx.RefreshSegments();
            ctx.PulseWhip(8f);
            if (!Main.dedServ) {
                BssVfx.Roar(ctx.Npc.Center, -0.1f, 0.7f);
            }
        }

        public override IBssState OnUpdate(BssStateContext ctx) {
            NPC npc = ctx.Npc;
            int t = (int)Timer;

            //环心缓跟玩家
            if (ctx.Target.Alives()) {
                anchor = Vector2.Lerp(anchor, ctx.Target.Center, 0.03f);
            }

            if (t < ExitFrame) {
                //绕环：追一个沿圆周行进的点，链条自然成环
                float progress = MathHelper.Clamp((t - EntryFrames) / (float)BssDirector.OrbitDuration(phaseAtEnter), 0f, 1f);
                float radius = MathHelper.Lerp(BssDirector.OrbitRadiusStart, BssDirector.OrbitRadiusEnd, progress);
                float omega = BssDirector.OrbitAngularSpeed(phaseAtEnter);
                //收束预告期转速放缓（吸气拍）
                if (t >= OrbitEnd) {
                    omega *= 0.5f;
                }
                orbitAngle += omega * orbitSign;
                Vector2 orbitPoint = anchor + orbitAngle.ToRotationVector2() * radius;

                ctx.Mode = BssMoveMode.Steer;
                ctx.MoveTarget = orbitPoint;
                ctx.MoveSpeed = omega * radius * 1.3f + 6f;
                ctx.TurnSpeed = 3.4f;
                ctx.AccelRate = 0.16f;
                ctx.Slither = 0.2f;
                ctx.LegCommand = BssLegCommand.Flail;
                ctx.LegAlpha = 0.8f;

                //环身即墙：高速段开伤害窗
                if (npc.velocity.Length() > 12f) {
                    npc.damage = npc.defDamage;
                }

                //向心钉刺（P2 起）
                if (ctx.Phase >= 2 && t >= EntryFrames && t < OrbitEnd) {
                    UpdateInwardNeedles(ctx, npc, t);
                }

                //收束预告：亮花 + 吼（锁点在预告起始帧）
                if (t >= OrbitEnd) {
                    if (t == OrbitEnd) {
                        exitLock = anchor;
                        if (!Main.dedServ) {
                            BssVfx.Roar(npc.Center, -0.5f, 1f);
                        }
                    }
                    ctx.BloomGlow = Math.Max(ctx.BloomGlow, 1f);
                }
            }
            else {
                //穿心突刺：一帧定速，直线承诺
                if (!exitDashed) {
                    exitDashed = true;
                    if (!VaultUtils.isClient) {
                        npc.velocity = (exitLock - npc.Center).SafeNormalize(Vector2.UnitY)
                            * BssDirector.OrbitExitSpeed;
                        npc.netUpdate = true;
                    }
                    ctx.PulseWhip(12f);
                    if (!Main.dedServ) {
                        BssVfx.Shake(npc.Center, 5f, 1200f);
                    }
                }
                ctx.Mode = BssMoveMode.Direct;
                ctx.LegCommand = BssLegCommand.Tuck;
                ctx.LegAlpha = 0.6f;
                float speed = npc.velocity.Length();
                if (speed > BssDirector.LungeContactSpeed) {
                    npc.damage = npc.defDamage;
                }

                //入地表现
                float groundY = BssVfx.FindGroundY(npc.Center - new Vector2(0f, 300f), 900f);
                if (prevY < groundY && npc.Center.Y >= groundY - 10f && !Main.dedServ) {
                    BssVfx.SandBurst(new Vector2(npc.Center.X, groundY), 1.2f);
                }

                if (t >= ExitFrame + ExitFlightFrames) {
                    npc.velocity *= 0.6f;
                    return EndAttack(ctx);
                }
            }

            prevY = npc.Center.Y;
            Timer++;

            if (t > 60 * 9) {
                return EndAttack(ctx);
            }
            return null;
        }

        /// <summary>向心钉刺：每拍预亮 10 帧，红花节朝环心各射 1 枚（非追踪，环心汇聚外圈稀疏）</summary>
        private void UpdateInwardNeedles(BssStateContext ctx, NPC npc, int t) {
            int beat = (t - EntryFrames) % BssDirector.OrbitNeedleGap;
            if (beat >= BssDirector.OrbitNeedleGap - 10) {
                ctx.BloomGlow = Math.Max(ctx.BloomGlow, 0.7f);
            }
            if (beat != BssDirector.OrbitNeedleGap - 1) {
                return;
            }

            if (!Main.dedServ) {
                SoundEngine.PlaySound(SoundID.Item17 with { Volume = 0.55f, Pitch = 0.4f, MaxInstances = 3 }, npc.Center);
            }
            if (VaultUtils.isClient) {
                return;
            }
            int bodyType = ModContent.NPCType<BssBody>();
            int damage = BssDirector.ScaleProjectileDamage(npc, BssDirector.NeedleDamage);
            int type = ModContent.ProjectileType<BssNeedleProj>();
            foreach (var seg in ctx.Segments) {
                if (!seg.Alives() || seg.type != bodyType
                    || !BssStateContext.IsFlowerOrdinal((int)seg.ai[0])) {
                    continue;
                }
                Vector2 dir = (anchor - seg.Center).SafeNormalize(Vector2.UnitY);
                Projectile.NewProjectile(npc.GetSource_FromAI(), seg.Center + dir * 12f,
                    dir * (BssDirector.NeedleSpeed * 0.85f), type, damage, 0.4f, Main.myPlayer);
            }
        }
    }
}
