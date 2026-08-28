using CalamityOverhaul.Content.NPCs.SeaShrimp.Core;
using CalamityOverhaul.Content.NPCs.SeaShrimp.Projectiles;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.SeaShrimp.States
{
    /// <summary>
    /// 尾扇水弹三连：蝎式卷尾——脊柱后卷把尾扇甩到背上方、扇面全张，
    /// 三轮五连扇形水弹。声明式缺口：弹间角距 BoltAngleGap 即逃逸通道，
    /// 齐射期间本体驻停（几何稳定），每轮出膛前 8 帧锁向
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)SeaShrimpStateIndex.WaterVolley, typeof(SeaShrimpStateContext))]
    internal class SeaShrimpWaterVolleyState : SeaShrimpStateBase
    {
        public override string StateName => "WaterVolley";
        public override SeaShrimpStateIndex StateIndex => SeaShrimpStateIndex.WaterVolley;

        private const int CurlEnd = 30;
        private static readonly int[] FireFrames = [36, 50, 64];
        private const int Total = 92;

        private Vector2 volleyAim = Vector2.UnitX;

        public override ISeaShrimpState OnUpdate(SeaShrimpStateContext ctx) {
            NPC npc = ctx.Npc;
            int t = (int)Timer;
            Timer++;
            HoldInPlace(ctx);

            //全程蝎式卷尾持姿
            float curlIn = MathHelper.Clamp(t / (float)CurlEnd, 0f, 1f);
            ctx.SpineCurl = -0.92f * (curlIn * curlIn * (3f - 2f * curlIn));
            ctx.TailFlare = curlIn;
            ctx.WaveGain = 0.35f;
            ctx.CrystalGlow = MathF.Max(ctx.CrystalGlow, curlIn * 0.6f);

            Vector2 tailPos = ctx.Owner.Skeleton.Nodes[4].Pos
                + ctx.Owner.Skeleton.Nodes[4].Forward * 14f;

            if (t == 2 && !Main.dedServ) {
                SoundEngine.PlaySound(SoundID.Item21 with { Volume = 0.5f, Pitch = -0.35f, MaxInstances = 2 }, npc.Center);
            }

            foreach (int fire in FireFrames) {
                //每轮：出膛前 8 帧锁向并亮预告扇
                if (t == fire - 8) {
                    volleyAim = (PredictTarget(ctx, 12f) - tailPos).SafeNormalize(Vector2.UnitX);
                }
                if (t >= fire - 8 && t < fire) {
                    float a = (t - (fire - 8)) / 8f * 0.45f;
                    int half = SeaShrimpDirector.BoltsPerVolley / 2;
                    for (int i = -half; i <= half; i++) {
                        Vector2 dir = volleyAim.RotatedBy(i * SeaShrimpDirector.BoltAngleGap);
                        ctx.AddTelegraph(tailPos + dir * 20f, dir, 190f, a, 0.55f);
                    }
                }
                if (t == fire) {
                    //出膛：尾扇回抖一口（发射器反坐），弹间角距=声明缺口
                    ctx.TailFlare = 0.55f;
                    if (!Main.dedServ) {
                        SoundEngine.PlaySound(SoundID.Item85 with { Volume = 0.7f, Pitch = 0.15f, MaxInstances = 3 }, tailPos);
                        ShakeNearby(npc.Center, 2.5f);
                    }
                    if (!VaultUtils.isClient) {
                        int damage = SeaShrimpDirector.ScaleProjectileDamage(npc, SeaShrimpDirector.WaterBoltDamage);
                        int half = SeaShrimpDirector.BoltsPerVolley / 2;
                        for (int i = -half; i <= half; i++) {
                            Vector2 dir = volleyAim.RotatedBy(i * SeaShrimpDirector.BoltAngleGap);
                            float speed = SeaShrimpDirector.WaterBoltSpeed - MathF.Abs(i) * 0.5f;
                            Projectile.NewProjectile(npc.GetSource_FromAI(), tailPos, dir * speed,
                                ModContent.ProjectileType<SeaShrimpWaterBolt>(), damage, 1.5f, Main.myPlayer);
                        }
                    }
                }
            }

            if (t >= Total) {
                return EndAttack(ctx, 52);
            }
            return null;
        }
    }
}
