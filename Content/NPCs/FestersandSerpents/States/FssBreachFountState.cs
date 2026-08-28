using CalamityOverhaul.Content.NPCs.FestersandSerpents.Core;
using CalamityOverhaul.Content.NPCs.FestersandSerpents.Projectiles;
using System;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.FestersandSerpents.States
{
    /// <summary>
    /// A4 破土脓泉：钻沙下潜 → 地底鱼雷贴近 → 腐沙隆包预告（生成即锁点，预告即承诺）→
    /// 直线破土 + 200° 灵液扇（两侧留逃生道）+ 引燃破口近旁脓池成泉柱（池经济首次兑现）。
    /// P2 起三循环。接触伤害只在冲势可见时开（速度门）。
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)FssStateIndex.BreachFount, typeof(FssStateContext))]
    internal class FssBreachFountState : FssStateBase
    {
        public override string StateName => "BreachFount";
        public override FssStateIndex StateIndex => FssStateIndex.BreachFount;

        private enum Phase { Dive, DigApproach, OmenWait, Airborne }

        private Phase phase;
        private int phaseTimer;
        /// <summary>锁定的破土点 X（隆包生成帧钉死）</summary>
        private float lockedX;
        private float breachGroundY;
        private bool breachFxDone;
        private float prevY;

        public override void OnEnter(FssStateContext ctx) {
            base.OnEnter(ctx);
            phase = Phase.Dive;
            phaseTimer = 0;
            breachFxDone = false;
            prevY = ctx.Npc.Center.Y;
        }

        public override IFssState OnUpdate(FssStateContext ctx) {
            NPC npc = ctx.Npc;

            switch (phase) {
                case Phase.Dive:
                    UpdateDive(ctx, npc);
                    break;
                case Phase.DigApproach:
                    UpdateDigApproach(ctx, npc);
                    break;
                case Phase.OmenWait:
                    UpdateOmenWait(ctx, npc);
                    break;
                case Phase.Airborne: {
                    IFssState next = UpdateAirborne(ctx, npc);
                    if (next != null) {
                        return next;
                    }
                    break;
                }
            }

            prevY = npc.Center.Y;
            phaseTimer++;
            Timer++;

            //超时保险：整招十秒封顶
            if (Timer > 600) {
                return EndAttack(ctx);
            }
            return null;
        }

        /// <summary>下潜入沙</summary>
        private void UpdateDive(FssStateContext ctx, NPC npc) {
            ctx.Mode = FssMoveMode.Direct;
            ctx.LegCommand = FssLegCommand.Tuck;
            ctx.LegAlpha = MathHelper.Clamp(1f - phaseTimer / 12f, 0f, 1f);
            npc.velocity.X *= 0.97f;
            npc.velocity.Y = MathHelper.Clamp(npc.velocity.Y + 0.9f, -10f, 26f);

            //入土穿面表现
            if (!breachFxDone && !Main.dedServ) {
                float groundY = FssVfx.FindGroundY(npc.Center - new Vector2(0f, 300f), 900f);
                if (prevY < groundY && npc.Center.Y >= groundY - 10f) {
                    breachFxDone = true;
                    FssVfx.CorruptSandBurst(new Vector2(npc.Center.X, groundY), 1.2f);
                }
            }

            float surface = FssVfx.FindGroundY(ctx.Target.Center - new Vector2(0f, 200f));
            if (npc.Center.Y > surface + 320f || phaseTimer > 50) {
                phase = Phase.DigApproach;
                phaseTimer = 0;
                breachFxDone = false;
            }
        }

        /// <summary>地底鱼雷贴近：钻到玩家脚下（沿途地表渗沙提示大致方位，锁点在隆包帧）</summary>
        private void UpdateDigApproach(FssStateContext ctx, NPC npc) {
            ctx.LegCommand = FssLegCommand.Tuck;
            ctx.LegAlpha = 0f;
            float surface = FssVfx.FindGroundY(ctx.Target.Center - new Vector2(0f, 200f));
            ctx.Mode = FssMoveMode.Steer;
            ctx.MoveTarget = new Vector2(ctx.Target.Center.X, surface + 380f);
            ctx.MoveSpeed = FssDirector.LungeDigSpeed;
            ctx.TurnSpeed = 2.4f;
            ctx.AccelRate = 0.1f;
            ctx.Slither = 0.5f;

            //沿途地表渗沙（大致方位提示，非锁点承诺）
            if (!Main.dedServ && Main.rand.NextBool(3)) {
                FssVfx.FesterTrickle(new Vector2(npc.Center.X + Main.rand.NextFloat(-30f, 30f), surface - 4f), 1.2f);
            }

            bool underTarget = Math.Abs(npc.Center.X - ctx.Target.Center.X) < 70f;
            if (underTarget || phaseTimer > 100) {
                //隆包生成帧即锁点（预告即承诺，破土不再改向）；
                //各端本地同判推进相位，预告实体只在权威端生成
                lockedX = ctx.Target.Center.X;
                breachGroundY = FssVfx.FindGroundY(new Vector2(lockedX, ctx.Target.Center.Y - 200f));
                if (!VaultUtils.isClient) {
                    Projectile.NewProjectile(npc.GetSource_FromAI(),
                        new Vector2(lockedX, breachGroundY - 4f), Vector2.Zero,
                        ModContent.ProjectileType<FssBreachOmen>(), 0, 0f, Main.myPlayer,
                        FssDirector.BreachTelegraphFrames);
                    npc.netUpdate = true;
                }
                phase = Phase.OmenWait;
                phaseTimer = 0;
            }
        }

        /// <summary>隆包等待：钉在锁点正下方蓄势</summary>
        private void UpdateOmenWait(FssStateContext ctx, NPC npc) {
            ctx.LegCommand = FssLegCommand.Tuck;
            ctx.LegAlpha = 0f;
            ctx.Mode = FssMoveMode.Steer;
            ctx.MoveTarget = new Vector2(lockedX, breachGroundY + 380f);
            ctx.MoveSpeed = 14f;
            ctx.TurnSpeed = 2.6f;
            ctx.Compression = Math.Min(ctx.Compression, 0.9f);

            if (phaseTimer >= FssDirector.BreachTelegraphFrames) {
                //破土一帧定初速：直上微偏（锁点承诺内的自然散布）
                if (!VaultUtils.isClient) {
                    npc.Center = new Vector2(lockedX, npc.Center.Y);
                    float tilt = Main.rand.NextFloat(-0.12f, 0.12f);
                    npc.velocity = (-MathHelper.PiOver2 + tilt).ToRotationVector2()
                        * FssDirector.BreachLaunchSpeed * ctx.RampSpeedScale;
                    npc.netUpdate = true;
                }
                phase = Phase.Airborne;
                phaseTimer = 0;
                breachFxDone = false;
            }
        }

        /// <summary>破土腾空：穿面帧放扇+引燃，冲势内开伤害窗，回落再潜</summary>
        private IFssState UpdateAirborne(FssStateContext ctx, NPC npc) {
            ctx.Mode = FssMoveMode.Direct;
            ctx.LegCommand = FssLegCommand.Flail;
            ctx.LegAlpha = 0.85f;
            npc.velocity.Y = MathHelper.Clamp(npc.velocity.Y + FssDirector.LungeGravity, -40f, 24f);
            npc.velocity.X *= 0.998f;

            //穿面帧：沙爆 + 灵液扇 + 引燃近旁脓池（各端本地检测表现，弹幕只在权威端）
            if (!breachFxDone) {
                float groundY = FssVfx.FindGroundY(npc.Center - new Vector2(0f, 300f), 900f);
                if (prevY > groundY && npc.Center.Y <= groundY + 20f) {
                    breachFxDone = true;
                    Vector2 breachPoint = new(npc.Center.X, groundY);
                    ctx.PulseWhip(12f);
                    if (!Main.dedServ) {
                        FssVfx.CorruptSandBurst(breachPoint, 1.8f);
                        FssVfx.IchorBurst(breachPoint, 1.5f, -Vector2.UnitY);
                        FssVfx.Roar(npc.Center, -0.55f, 1f);
                        FssVfx.Shake(npc.Center, 7f, 1500f);
                    }
                    FssVfx.IchorBreachFan(npc, breachPoint, FssDirector.BreachEruptGlobs, ctx.RampSpeedScale);
                    //池经济兑现：破口近旁的池由近及远起泉
                    FssIchorPool.IgniteAround(breachPoint, FssDirector.BreachIgniteRadius,
                        FssDirector.BreachIgniteFuseBase, 1f / 8f);
                }
            }

            //伤害窗=可见冲势
            if (npc.velocity.Length() > FssDirector.LungeContactSpeed) {
                npc.damage = npc.defDamage;
            }

            //回落入地：循环裁决
            if (phaseTimer > 20 && npc.velocity.Y > 0f) {
                float surface = FssVfx.FindGroundY(npc.Center - new Vector2(0f, 300f), 900f);
                if (npc.Center.Y >= surface + 60f) {
                    if (!Main.dedServ) {
                        FssVfx.CorruptSandBurst(new Vector2(npc.Center.X, surface), 1.2f);
                    }
                    Counter++;
                    if (Counter >= FssDirector.BreachCycles(ctx.Phase) || ctx.Owner.TargetInvalid()) {
                        return EndAttack(ctx);
                    }
                    phase = Phase.DigApproach;
                    phaseTimer = 0;
                }
            }
            return null;
        }
    }
}
