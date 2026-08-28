using CalamityOverhaul.Content.NPCs.FestersandSerpents.Core;
using CalamityOverhaul.Content.NPCs.FestersandSerpents.Projectiles;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.FestersandSerpents.States
{
    /// <summary>
    /// A7 疮爆掠航（P2 起）：拉开跑道 → 短蓄力锁向 → 高速掠过玩家，航过途中
    /// 囊肿沿链序头→尾链式爆裂，向体侧两翼洒短弧灵液滴——扫射航过的走廊威胁。
    /// 爆过的囊肿瘪缩 8 秒充能（可读资源：瘪着的疮不亮不爆，骚扰也哑）。
    /// 公平口径：锁向即承诺、接触伤速度门、滴速慢 + 重力 = 走廊成形在身后可绕。
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)FssStateIndex.FesterRipple, typeof(FssStateContext))]
    internal class FssFesterRippleState : FssStateBase
    {
        public override string StateName => "FesterRipple";
        public override FssStateIndex StateIndex => FssStateIndex.FesterRipple;

        private enum Phase { Stalk, Windup, Pass, Brake }

        private Phase phase;
        private int phaseTimer;
        private Vector2 lockDir;
        private Vector2 passStart;
        private bool locked;
        private int burstFrontier;

        public override void OnEnter(FssStateContext ctx) {
            base.OnEnter(ctx);
            phase = Phase.Stalk;
            phaseTimer = 0;
            locked = false;
            burstFrontier = 0;
            ctx.RefreshSegments();
        }

        public override IFssState OnUpdate(FssStateContext ctx) {
            NPC npc = ctx.Npc;

            switch (phase) {
                case Phase.Stalk:
                    UpdateStalk(ctx, npc);
                    break;
                case Phase.Windup:
                    UpdateWindup(ctx, npc);
                    break;
                case Phase.Pass:
                    UpdatePass(ctx, npc);
                    break;
                case Phase.Brake: {
                    ctx.Mode = FssMoveMode.Direct;
                    ctx.LegCommand = FssLegCommand.March;
                    npc.velocity *= 0.7f;
                    if (phaseTimer >= FssDirector.RippleBrakeFrames) {
                        return EndAttack(ctx);
                    }
                    break;
                }
            }

            phaseTimer++;
            Timer++;

            //超时保险
            if (Timer > 160) {
                npc.velocity *= 0.8f;
                return EndAttack(ctx);
            }
            return null;
        }

        /// <summary>就位：跑道不足先退开</summary>
        private void UpdateStalk(FssStateContext ctx, NPC npc) {
            float dist = Vector2.Distance(npc.Center, ctx.Target.Center);
            ctx.Mode = FssMoveMode.Crawl;
            ctx.LegCommand = FssLegCommand.March;
            if (dist < FssDirector.RippleRunwayMin) {
                ctx.CrawlDirX = -FacingToTarget(ctx);
                ctx.CrawlSpeed = FssDirector.CrawlChaseSpeed;
            }
            else {
                ctx.CrawlDirX = FacingToTarget(ctx);
                ctx.CrawlSpeed = 8f;
            }
            if (phaseTimer >= FssDirector.RippleStalkFrames
                && (dist >= FssDirector.RippleRunwayMin || phaseTimer > 46)) {
                phase = Phase.Windup;
                phaseTimer = 0;
                locked = false;
            }
        }

        /// <summary>短蓄力：满身囊肿预亮 + 末段锁向</summary>
        private void UpdateWindup(FssStateContext ctx, NPC npc) {
            ctx.Mode = FssMoveMode.Direct;
            ctx.LegCommand = FssLegCommand.Tuck;
            ctx.Compression = Math.Min(ctx.Compression, 0.92f);
            ctx.CystGlow = Math.Max(ctx.CystGlow, phaseTimer / (float)FssDirector.RippleWindupFrames);

            if (!locked) {
                lockDir = (PredictTarget(ctx, 8f) - npc.Center).SafeNormalize(Vector2.UnitX);
                if (phaseTimer >= FssDirector.RippleWindupFrames - 4) {
                    locked = true;
                    if (!Main.dedServ) {
                        SoundEngine.PlaySound(SoundID.Item17 with { Volume = 0.6f, Pitch = 0.55f, MaxInstances = 3 }, npc.Center);
                    }
                }
            }
            float w = phaseTimer / (float)FssDirector.RippleWindupFrames;
            npc.velocity = -lockDir * (w * w * 8f);
            npc.rotation = npc.rotation.AngleLerp(lockDir.ToRotation() + FssHead.FacingRot, 0.35f);

            if (phaseTimer >= FssDirector.RippleWindupFrames) {
                npc.velocity = lockDir * FssDirector.RipplePassSpeed * ctx.RampSpeedScale;
                if (!VaultUtils.isClient) {
                    npc.netUpdate = true;
                }
                passStart = npc.Center;
                ctx.PulseWhip(9f);
                if (!Main.dedServ) {
                    FssVfx.Roar(npc.Center, -0.65f, 0.8f);
                    FssVfx.Shake(npc.Center, 4f, 1200f);
                }
                phase = Phase.Pass;
                phaseTimer = 0;
                burstFrontier = 0;
            }
        }

        /// <summary>掠航：伤害窗速度门 + 囊肿链式爆裂（头→尾行波，两翼洒滴）</summary>
        private void UpdatePass(FssStateContext ctx, NPC npc) {
            ctx.Mode = FssMoveMode.Direct;
            ctx.LegCommand = FssLegCommand.Tuck;

            if (npc.velocity.Length() > FssDirector.SkimContactSpeed) {
                npc.damage = npc.defDamage;
            }

            //链式爆裂波：头→尾随航过推进（各端同拍，弹幕只在权威端）
            int total = Math.Max(ctx.TotalSegments, 1);
            float wave = phaseTimer / (float)FssDirector.RipplePassMaxFrames;
            int frontier = (int)(wave * total);
            ctx.PulseKind = 2;
            ctx.PulsePhase = wave;
            while (burstFrontier < frontier && burstFrontier < ctx.Segments.Count) {
                BurstCyst(ctx, npc, burstFrontier);
                burstFrontier++;
            }

            //越身即早退（波未扫完的囊肿留着下次）
            bool overshot = Vector2.Dot(ctx.Target.Center - npc.Center, lockDir) < -FssDirector.RippleOvershoot;
            if (phaseTimer >= FssDirector.RipplePassMaxFrames || overshot) {
                phase = Phase.Brake;
                phaseTimer = 0;
            }
        }

        /// <summary>单颗囊肿爆裂：两翼短弧灵液滴 + 瘪缩记账（充能可读资源）</summary>
        private static void BurstCyst(FssStateContext ctx, NPC npc, int ordinal) {
            if (ordinal >= ctx.Segments.Count) {
                return;
            }
            NPC seg = ctx.Segments[ordinal];
            if (!seg.Alives() || !FssStateContext.IsCystOrdinal(ordinal)
                || (ordinal < ctx.CystSpent.Length && ctx.CystSpent[ordinal] > 0.4f)) {
                return;
            }

            //瘪缩记账（各端同拍写入，绘制层立即读到）
            if (ordinal < ctx.CystSpent.Length) {
                ctx.CystSpent[ordinal] = 1f;
            }

            if (!Main.dedServ) {
                FssVfx.IchorBurst(seg.Center, 1.1f);
                SoundEngine.PlaySound(SoundID.NPCDeath13 with { Volume = 0.55f, Pitch = 0.25f, MaxInstances = 5 }, seg.Center);
            }

            if (VaultUtils.isClient) {
                return;
            }
            //两翼短弧：沿体节法向两侧各洒慢滴（走廊成形在身后）
            int damage = FssDirector.ScaleProjectileDamage(npc, FssDirector.RippleDamage);
            int type = ModContent.ProjectileType<FssIchorGlob>();
            float chainDir = seg.rotation - FssHead.FacingRot;
            for (int i = 0; i < FssDirector.RippleDropsPerCyst; i++) {
                float flank = i % 2 == 0 ? 1f : -1f;
                float spread = Main.rand.NextFloat(-0.3f, 0.3f);
                Vector2 dir = (chainDir + MathHelper.PiOver2 * flank + spread).ToRotationVector2();
                Vector2 vel = dir * FssDirector.RippleDropSpeed * ctx.RampSpeedScale
                    + npc.velocity * 0.18f;
                Projectile.NewProjectile(npc.GetSource_FromAI(), seg.Center, vel, type,
                    damage, 0.4f, Main.myPlayer, 1f);
            }
        }
    }
}
