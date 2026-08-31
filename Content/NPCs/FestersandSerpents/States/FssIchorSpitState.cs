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
    /// A1 灵液扫喷：行进间跟踪 → 吸气微后仰 → 对称双车道齐射痰弹，落点播种脓池。
    /// 车道从内向外张开、中途反转收拢（反转前有升调提示音 + 头部闪光）。
    /// 公平口径：车道对称包夹预测点、中缝（±SpitLaneBase）永不发射 = 站定即安全声明；
    /// 贴脸 &lt; SpitMinDistance 的齐射直接哑火，邀请骑脸压血。
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)FssStateIndex.IchorSpit, typeof(FssStateContext))]
    internal class FssIchorSpitState : FssStateBase
    {
        public override string StateName => "IchorSpit";
        public override FssStateIndex StateIndex => FssStateIndex.IchorSpit;

        private int InhaleEnd => FssDirector.SpitTrackFrames + FssDirector.SpitInhaleFrames;
        private int VolleyEnd => InhaleEnd + FssDirector.SpitVolleys * FssDirector.SpitVolleyGap;

        public override IFssState OnUpdate(FssStateContext ctx) {
            NPC npc = ctx.Npc;
            int t = (int)Timer;

            Vector2 mouth = MouthPos(npc);
            Vector2 aim = (PredictTarget(ctx, 12f) - mouth).SafeNormalize(Vector2.UnitX);

            ctx.Mode = FssMoveMode.Crawl;
            ctx.CrawlDirX = FacingToTarget(ctx);
            ctx.AimAngle = aim.ToRotation();
            ctx.LegCommand = FssLegCommand.March;

            //鳌足护嘴：吸气窗镰横挡杵托颌（合拢度 = 吸气进度），齐射期保持护姿等推开拍
            if (t >= FssDirector.SpitTrackFrames && t <= VolleyEnd) {
                ctx.ClawCommand = FssClawCommand.GuardMouth;
                ctx.ClawPhase = MathHelper.Clamp(
                    (t - FssDirector.SpitTrackFrames) / (float)FssDirector.SpitInhaleFrames, 0f, 1f);
            }

            if (t < FssDirector.SpitTrackFrames) {
                //跟踪段：继续逼近，囊肿渐亮
                ctx.CrawlSpeed = 9f;
                ctx.CystGlow = Math.Max(ctx.CystGlow, t / (float)FssDirector.SpitTrackFrames * 0.5f);
            }
            else if (t < InhaleEnd) {
                //吸气段：微后撤（反向运动即预告），尘埃向口收束
                ctx.CrawlSpeed = -3.5f;
                ctx.CystGlow = Math.Max(ctx.CystGlow, 0.7f);
                ctx.SwallowSuction = Math.Max(ctx.SwallowSuction, 0.6f);
                if (!Main.dedServ && Main.rand.NextBool(2)) {
                    Vector2 from = mouth + Main.rand.NextVector2CircularEdge(60f, 60f);
                    Dust d = Dust.NewDustPerfect(from, DustID.Sand,
                        (mouth - from) * 0.1f, 100, FssVfx.TaintedSand, Main.rand.NextFloat(0.8f, 1.2f));
                    d.noGravity = true;
                }
                if (t == InhaleEnd - 3 && !Main.dedServ) {
                    SoundEngine.PlaySound(SoundID.Item32 with { Volume = 0.6f, Pitch = -0.5f, MaxInstances = 3 }, npc.Center);
                }
            }
            else if (t < VolleyEnd) {
                //齐射段：慢速推进扫射
                ctx.CrawlSpeed = 5f;
                int local = t - InhaleEnd;
                int volley = local / FssDirector.SpitVolleyGap;

                //车道反转提示：反转前一轮升调 + 头闪
                if (volley == FssDirector.SpitReverseVolley - 1 && local % FssDirector.SpitVolleyGap == 0) {
                    ctx.CystGlow = 1f;
                    if (!Main.dedServ) {
                        SoundEngine.PlaySound(SoundID.Item17 with { Volume = 0.7f, Pitch = 0.65f, MaxInstances = 3 }, npc.Center);
                    }
                }

                if (local % FssDirector.SpitVolleyGap == 0) {
                    FireVolley(ctx, npc, mouth, aim, volley);
                }
            }
            else if (t > VolleyEnd + 10) {
                return EndAttack(ctx);
            }

            Timer++;
            //超时保险
            if (t > VolleyEnd + 40) {
                return EndAttack(ctx);
            }
            return null;
        }

        /// <summary>一轮齐射：对称双车道各一发（中缝永空）；贴脸哑火</summary>
        private static void FireVolley(FssStateContext ctx, NPC npc, Vector2 mouth, Vector2 aim, int volley) {
            float dist = Vector2.Distance(npc.Center, ctx.Target.Center);
            if (dist < FssDirector.SpitMinDistance) {
                //贴脸哑火：只呛一口气不吐弹
                if (!Main.dedServ) {
                    FssVfx.FesterTrickle(mouth, 1.5f);
                }
                return;
            }

            //车道序号：先向外张开，反转轮起向内收拢
            int k = volley < FssDirector.SpitReverseVolley
                ? volley
                : FssDirector.SpitVolleys - 1 - volley;
            k = Math.Max(k, 0);
            float lane = FssDirector.SpitLaneBase + k * FssDirector.SpitLaneStep;

            //出手表现（各端本地）：口沫 + 短鞭波 = 逐口后坐；鳌足同拍猛推摊开
            if (!Main.dedServ) {
                FssVfx.IchorBurst(mouth, 0.7f, aim);
                SoundEngine.PlaySound(SoundID.Item17 with { Volume = 0.55f, Pitch = -0.15f, MaxInstances = 5 }, mouth);
            }
            ctx.PulseWhip(4f);
            ctx.ClawBurst = 1f;
            ctx.CystGlow = Math.Max(ctx.CystGlow, 0.55f);

            if (VaultUtils.isClient) {
                return;
            }
            int damage = FssDirector.ScaleProjectileDamage(npc, FssDirector.IchorGlobDamage);
            int type = ModContent.ProjectileType<FssIchorGlob>();
            float speed = FssDirector.IchorGlobSpeed * ctx.RampSpeedScale;
            for (int side = -1; side <= 1; side += 2) {
                Vector2 vel = aim.RotatedBy(lane * side) * speed - new Vector2(0f, 1.2f);
                Projectile.NewProjectile(npc.GetSource_FromAI(), mouth, vel, type, damage, 0.5f, Main.myPlayer);
            }
        }
    }
}
