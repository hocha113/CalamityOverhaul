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
    /// 巨钳合击：面向贴脸的玩家站定 → 双螯高举大张（张螯即预告，末段锁向白花闪）→
    /// 一帧前扑、双螯朝锁定的钳点急伸 → 钳合（钳点判定 + 沙夹爆）→ 硬刹收势。
    /// 这条虫此前没有近身答案（喷沙贴脸哑火、冲刺退开拉跑道），骑脸压血是白给；
    /// 合击补上这一口：近钳远喷，hub 只在玩家进 SnapTriggerRange 时选它。
    /// 公平阀声明：张螯 SnapSpreadFrames 帧可读，出手前 SnapLockLead 帧锁点不再追瞄；
    /// 钳点判定只活 SnapHitFrames 帧、半径 SnapRadius；前扑路程短（不追远）；P2 起两钳但第二钳重新张螯。
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)BssStateIndex.PincerSnap, typeof(BssStateContext))]
    internal class BssPincerSnapState : BssStateBase
    {
        public override string StateName => "PincerSnap";
        public override BssStateIndex StateIndex => BssStateIndex.PincerSnap;

        private enum SnapPhase
        {
            Spread,  //张螯预告
            Lunge,   //前扑
            Recover, //收势
        }

        private SnapPhase phase;
        private int snaps;
        private float toward = 1f;
        /// <summary>锁定的前扑方向与钳点（锁向帧后不再更新 = 预告即承诺）</summary>
        private Vector2 lungeDir = Vector2.UnitX;
        private Vector2 snapPoint;
        private bool snapped;

        private int SpreadFrames => snaps == 0 ? BssDirector.SnapSpreadFrames : BssDirector.SnapRespreadFrames;

        public override void OnEnter(BssStateContext ctx) {
            base.OnEnter(ctx);
            phase = SnapPhase.Spread;
            snaps = 0;
            snapped = false;
        }

        public override IBssState OnUpdate(BssStateContext ctx) {
            NPC npc = ctx.Npc;

            switch (phase) {
                case SnapPhase.Spread:
                    UpdateSpread(ctx, npc);
                    break;
                case SnapPhase.Lunge:
                    UpdateLunge(ctx, npc);
                    break;
                case SnapPhase.Recover: {
                    IBssState next = UpdateRecover(ctx, npc);
                    if (next != null) {
                        return next;
                    }
                    break;
                }
            }

            //超时保险兜底
            if (Counter++ > 60 * 4) {
                return EndAttack(ctx);
            }
            return null;
        }

        private void SwitchPhase(SnapPhase next) {
            phase = next;
            Timer = 0;
        }

        /// <summary>张螯：站定面向玩家，双螯举张、身体聚拢上膛；锁向拍前逐帧追瞄钳点，之后死点</summary>
        private void UpdateSpread(BssStateContext ctx, NPC npc) {
            int t = (int)Timer;
            int frames = SpreadFrames;
            float progress = MathHelper.Clamp(t / (float)frames, 0f, 1f);
            toward = FacingToTarget(ctx, 0f);

            if (t <= frames - BssDirector.SnapLockLead) {
                Vector2 predicted = ctx.Target.Center + ctx.Target.velocity * 8f;
                Vector2 to = predicted - npc.Center;
                float dist = MathHelper.Clamp(to.Length(), 180f, BssDirector.SnapReach);
                Vector2 dir = to.SafeNormalize(new Vector2(toward, 0f));
                //俯仰钳制：贴地身份，只咬得到低空
                float pitch = MathHelper.Clamp(MathF.Asin(MathHelper.Clamp(dir.Y, -1f, 1f)), -0.7f, 0.45f);
                float sign = dir.X >= 0f ? 1f : -1f;
                lungeDir = new Vector2(sign * MathF.Cos(pitch), MathF.Sin(pitch));
                snapPoint = npc.Center + lungeDir * dist;
            }
            else if (t == frames - BssDirector.SnapLockLead + 1) {
                //锁点白花闪 + 咔嗒：承诺拍
                ctx.BloomGlow = Math.Max(ctx.BloomGlow, 1f);
                if (!Main.dedServ) {
                    SoundEngine.PlaySound(SoundID.Item102 with { Volume = 0.6f, Pitch = 0.4f, MaxInstances = 3 }, npc.Center);
                }
            }

            ctx.Mode = BssMoveMode.Crawl;
            ctx.CrawlSpeed = 0f;
            ctx.CrawlDirX = toward;
            ctx.LegCommand = BssLegCommand.Brace;
            ctx.ClawCommand = BssClawCommand.Brace;
            ctx.ClawPhase = progress;
            ctx.Compression = MathHelper.Lerp(1f, 0.86f, progress);
            ctx.GatherLevel = progress;
            ctx.AimAngle = lungeDir.ToRotation();
            DeclareJaw(ctx, BssJawCommand.Inhale, progress * 0.8f);

            if (t == 0 && !Main.dedServ) {
                SoundEngine.PlaySound(SoundID.Item102 with { Volume = 0.7f, Pitch = -0.2f, MaxInstances = 2 }, npc.Center);
                BssVfx.Roar(npc.Center, 0.15f, 0.5f);
            }
            //末段绷紧颤抖
            if (!Main.dedServ && progress > 0.65f) {
                npc.position += Main.rand.NextVector2Circular(1.2f, 1.2f);
            }

            Timer++;
            if (t >= frames) {
                Launch(ctx, npc);
                SwitchPhase(SnapPhase.Lunge);
            }
        }

        /// <summary>前扑：一帧定速沿锁向扑出，双螯急伸包夹钳点</summary>
        private void Launch(BssStateContext ctx, NPC npc) {
            npc.velocity = lungeDir * BssDirector.SnapLungeSpeed;
            if (!VaultUtils.isClient) {
                npc.netUpdate = true;
            }
            snapped = false;
            ctx.PulseWhip(9f);
            ctx.PulseGapWave(SerpentChainMath.WaveRelease, 0.12f);
            if (!Main.dedServ) {
                SoundEngine.PlaySound(SoundID.Item1 with { Volume = 0.8f, Pitch = -0.7f, MaxInstances = 2 }, npc.Center);
                BssVfx.SandBurst(npc.Center + new Vector2(0f, 16f), 0.9f);
                BssVfx.Shake(npc.Center, 3f, 900f);
            }
        }

        /// <summary>扑出与钳合：扑帧张口伸螯，末帧钳合出判定，随即硬刹</summary>
        private void UpdateLunge(BssStateContext ctx, NPC npc) {
            int t = (int)Timer;
            ctx.Mode = BssMoveMode.Direct;
            ctx.LegCommand = BssLegCommand.Brace;
            ctx.ClawCommand = BssClawCommand.Snatch;
            ctx.ClawAim = snapPoint;
            npc.rotation = npc.rotation.AngleLerp(lungeDir.ToRotation() + BssHead.FacingRot, 0.5f);
            DeclareJaw(ctx, BssJawCommand.Bite, snapped ? 0f : 1f);

            if (npc.velocity.Length() > BssDirector.DashContactSpeed) {
                npc.damage = npc.defDamage;
            }

            Timer++;
            if (!snapped && t >= BssDirector.SnapLungeFrames) {
                snapped = true;
                Snap(ctx, npc);
            }
            if (t >= BssDirector.SnapLungeFrames + 4) {
                snaps++;
                SwitchPhase(SnapPhase.Recover);
            }
        }

        /// <summary>钳合：钳点判定实体（权威端）+ 硬刹反冲 + 全髋下沉</summary>
        private void Snap(BssStateContext ctx, NPC npc) {
            npc.velocity *= -0.2f;
            ctx.ClawBurst = 1f;
            ctx.JawBurst = 0f;
            ctx.PulseGapWave(SerpentChainMath.WavePress, 0.12f);
            for (int k = 0; k < ctx.StationBob.Length; k++) {
                ctx.StationBob[k] = 0.9f;
            }
            if (!VaultUtils.isClient) {
                int damage = BssDirector.ScaleProjectileDamage(npc, BssDirector.PincerSnapDamage);
                Projectile.NewProjectile(npc.GetSource_FromAI(), snapPoint, Vector2.Zero,
                    ModContent.ProjectileType<BssPincerSnapProj>(), damage, 2f, Main.myPlayer, lungeDir.ToRotation());
                npc.netUpdate = true;
            }
        }

        /// <summary>收势：钳合姿态定住一拍再松开，连钳未满回张螯</summary>
        private IBssState UpdateRecover(BssStateContext ctx, NPC npc) {
            int t = (int)Timer;
            ctx.Mode = BssMoveMode.Crawl;
            ctx.CrawlDirX = FacingToTarget(ctx);
            ctx.CrawlSpeed = t < 8 ? 0f : BssDirector.CrawlTurnSpeed;
            ctx.LegCommand = BssLegCommand.March;
            if (t < 8) {
                ctx.ClawCommand = BssClawCommand.Snatch;
                ctx.ClawAim = snapPoint;
                DeclareJaw(ctx, BssJawCommand.Bite, 0f);
            }

            Timer++;
            if (t >= BssDirector.SnapRecoverFrames) {
                if (snaps < BssDirector.SnapReps(ctx.Phase)
                    && Vector2.Distance(npc.Center, ctx.Target.Center) < BssDirector.SnapTriggerRange * 1.3f) {
                    SwitchPhase(SnapPhase.Spread);
                    return null;
                }
                return EndAttack(ctx);
            }
            return null;
        }
    }
}
