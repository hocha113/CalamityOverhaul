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
    /// A3 黏疮布点：立身后仰（8 次幂迟滞蓄力）→ 一帧甩头齐抛黏疮，落点包夹玩家。
    /// 黏疮黏附砖面鼓胀升调，逐颗错拍喷竖直灵液泉，近地再留小池（播种）。
    /// 公平口径：落点间距 CystSpacing 即站缝逃生道（泉柱威胁面窄于缝宽）、
    /// 鼓胀升调即逐颗预告、引信错拍 = 顺序可读的喷发波。
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)FssStateIndex.StickyCyst, typeof(FssStateContext))]
    internal class FssStickyCystState : FssStateBase
    {
        public override string StateName => "StickyCyst";
        public override FssStateIndex StateIndex => FssStateIndex.StickyCyst;

        private int FlingFrame => FssDirector.CystWindupFrames;
        private int ExitFrame => FssDirector.CystWindupFrames + FssDirector.CystRecoverFrames;

        public override IFssState OnUpdate(FssStateContext ctx) {
            NPC npc = ctx.Npc;
            int t = (int)Timer;

            if (t < FlingFrame) {
                //蓄力后仰：8 次幂迟滞——大半时间纹丝不动，末几帧猛然吸满
                ctx.Mode = FssMoveMode.Crawl;
                ctx.CrawlSpeed = 0f;
                ctx.CrawlDirX = FacingToTarget(ctx);
                ctx.LegCommand = FssLegCommand.Raise;
                float w = MathF.Pow(t / (float)FlingFrame, 8f);
                ctx.FrontRaise = MathHelper.Clamp(0.3f + w * 0.7f, 0f, 1f);
                //头仰角：面向目标向上抬 + 末段猛拉
                float face = ctx.CrawlDirX >= 0f ? 0f : MathHelper.Pi;
                ctx.AimAngle = face - ctx.CrawlDirX * (0.5f + w * 0.75f);
                ctx.CystGlow = Math.Max(ctx.CystGlow, 0.3f + w * 0.7f);
                ctx.Compression = Math.Min(ctx.Compression, 1f - w * 0.1f);
                if (t == FlingFrame - 4 && !Main.dedServ) {
                    SoundEngine.PlaySound(SoundID.Item32 with { Volume = 0.7f, Pitch = -0.35f, MaxInstances = 3 }, npc.Center);
                }
            }
            else if (t == FlingFrame) {
                //一帧甩头齐抛 + 前冲脉冲（蓄了 24 帧的力要看得见地发出去）
                FlingCysts(ctx, npc);
                ctx.Mode = FssMoveMode.Direct;
                npc.velocity = new Vector2(ctx.CrawlDirX * 7.5f, -2.2f);
            }
            else if (t <= FlingFrame + 6) {
                //甩后随动：头快速前压（后仰→前甩的完整弧），身体惯性滑一小段。
                //Hold 模式不碰旋转（Direct 会按速度向覆写手动压头），减速由 Hold 自带
                ctx.Mode = FssMoveMode.Hold;
                ctx.LegCommand = FssLegCommand.Raise;
                ctx.FrontRaise *= 0.55f;
                float face = ctx.CrawlDirX >= 0f ? 0f : MathHelper.Pi;
                float forward = face + ctx.CrawlDirX * 0.5f;
                npc.rotation = npc.rotation.AngleLerp(forward + FssHead.FacingRot, 0.45f);
            }
            else {
                //收势回爬
                ctx.Mode = FssMoveMode.Crawl;
                ctx.CrawlSpeed = 6f;
                ctx.CrawlDirX = FacingToTarget(ctx);
                ctx.LegCommand = FssLegCommand.March;
                if (t > ExitFrame) {
                    return EndAttack(ctx);
                }
            }

            Timer++;
            //超时保险
            if (t > ExitFrame + 30) {
                return EndAttack(ctx);
            }
            return null;
        }

        /// <summary>齐抛黏疮：落点以玩家为中心按 CystSpacing 包夹（间距即逃生声明）</summary>
        private static void FlingCysts(FssStateContext ctx, NPC npc) {
            ctx.PulseWhip(10f);
            ctx.FrontRaise = 1f;
            if (!Main.dedServ) {
                FssVfx.Roar(npc.Center, -0.5f, 0.9f);
                FssVfx.Shake(npc.Center, 5f, 1300f);
                FssVfx.IchorBurst(npc.Center, 1.4f, -Vector2.UnitY);
            }

            if (VaultUtils.isClient || !ctx.Target.Alives()) {
                return;
            }

            int count = FssDirector.CystCount(ctx.Phase);
            int damage = FssDirector.ScaleProjectileDamage(npc, FssDirector.GeyserDamage);
            int type = ModContent.ProjectileType<FssStickyCyst>();
            Vector2 mouth = npc.Center + (npc.rotation - FssHead.FacingRot).ToRotationVector2() * 30f * npc.scale;
            float grav = 0.34f;
            float lobT = FssDirector.CystLobFrames;

            for (int i = 0; i < count; i++) {
                //落点：以玩家为中心的对称包夹（0 号在头顶，其余左右错开）
                float xOff = (i - (count - 1) * 0.5f) * FssDirector.CystSpacing
                    + Main.rand.NextFloat(-24f, 24f);
                Vector2 targetGround = new(ctx.Target.Center.X + xOff,
                    FssVfx.FindGroundY(ctx.Target.Center - new Vector2(-xOff, 200f)));
                //抛物线反解出手速度（固定飞行帧数 = 齐抛齐落的可读节奏）。
                //不乘弹速爬坡阀：反解速度一缩落点就飘，承诺的包夹位便失真——
                //本招的公平由蓄力预告与落点间距承担
                Vector2 delta = targetGround - mouth;
                Vector2 vel = new(delta.X / lobT, delta.Y / lobT - 0.5f * grav * lobT);
                //引信错拍：由近及远顺序喷发
                float fuse = FssDirector.CystSwellFrames + i * FssDirector.CystFuseStagger;
                Projectile.NewProjectile(npc.GetSource_FromAI(), mouth, vel, type,
                    damage, 0.5f, Main.myPlayer, fuse);
            }
        }
    }
}
