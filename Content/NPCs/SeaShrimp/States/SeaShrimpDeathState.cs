using CalamityOverhaul.Content.NPCs.SeaShrimp.Core;
using CalamityOverhaul.Content.NPCs.SeaShrimp.Rendering;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.SeaShrimp.States
{
    /// <summary>
    /// 死亡演出（~330f 完整弧线）：踉跄 → 晶体沿体节逐一熄灭（每处一响）→
    /// 前倾拖螯 → 30f 向心收束（水被吸入，全场静默）→ 终极空泡内爆
    /// （全场唯一一次 impact frame + 震屏收在这一拍）→ 长静默沉底 → 放行真死。
    /// 全程无敌锁血；结束时服务器执行真击杀走掉落
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)SeaShrimpStateIndex.Death, typeof(SeaShrimpStateContext))]
    internal class SeaShrimpDeathState : SeaShrimpStateBase
    {
        public override string StateName => "Death";
        public override SeaShrimpStateIndex StateIndex => SeaShrimpStateIndex.Death;

        private const int StaggerEnd = 70;
        private const int DimEnd = 190;
        private const int DroopEnd = 250;
        private const int ImplodeFrame = 270;
        private const int Total = 330;

        public override void OnEnter(SeaShrimpStateContext ctx) {
            base.OnEnter(ctx);
            if (!VaultUtils.isClient) {
                SeaShrimpBoss.ClearHostileProjectiles();
            }
        }

        public override ISeaShrimpState OnUpdate(SeaShrimpStateContext ctx) {
            NPC npc = ctx.Npc;
            int t = (int)Timer;
            Timer++;
            npc.dontTakeDamage = true;
            npc.damage = 0;

            //运镜窗口：仅前 20 帧敞开
            if (t < 20) {
                SeaShrimpCutscenes.ArmDeath(npc.Center);
            }
            SeaShrimpCutscenes.DeathAnchor = npc.Center;

            if (t < StaggerEnd) {
                //踉跄：低速左右趔趄，晶光乱闪
                float side = (t / 24) % 2 == 0 ? 1f : -1f;
                ctx.Owner.Locomotion.RequestCrawlTo(npc.Center + new Vector2(side * 90f, 0f), 0.3f, 0f);
                ctx.SpineCurl = MathF.Sin(t * 0.31f) * 0.12f;
                ctx.WaveGain = 0.5f;
                float flicker = 0.4f + 0.5f * MathF.Abs(MathF.Sin(t * 0.9f + npc.whoAmI));
                ctx.CrystalGlow = MathF.Max(ctx.CrystalGlow, flicker);
                if (t % 18 == 0 && !Main.dedServ) {
                    SoundEngine.PlaySound(SoundID.NPCHit4 with { Volume = 0.4f, Pitch = -0.5f, MaxInstances = 2 }, npc.Center);
                }
                return null;
            }

            if (t < DimEnd) {
                //晶体逐节熄灭：从尾到头，每 24f 一处炸裂暗下
                HoldInPlace(ctx);
                float dim = (t - StaggerEnd) / (float)(DimEnd - StaggerEnd);
                ctx.DeathGloom = MathF.Max(ctx.DeathGloom, dim * 0.62f);
                SeaShrimpAbyssScreen.PushGloom(dim * 0.5f);

                int beat = t - StaggerEnd;
                if (beat % 24 == 0 && !Main.dedServ) {
                    int nodeIdx = Math.Max(4 - beat / 24, 0);
                    Vector2 nodePos = ctx.Owner.Skeleton.Nodes[nodeIdx].Pos;
                    SoundEngine.PlaySound(SoundID.Shatter with { Volume = 0.55f, Pitch = -0.4f, MaxInstances = 3 }, nodePos);
                    ShakeNearby(npc.Center, 2f);
                    for (int i = 0; i < 7; i++) {
                        PRTLoader.NewParticle<PRT_DefCrystalShard>(nodePos + Main.rand.NextVector2Circular(24f, 18f),
                            new Vector2(Main.rand.NextFloat(-2f, 2f), -Main.rand.NextFloat(0.5f, 2.4f)),
                            SeaShrimpRenderer.CrystalBlue * (1f - dim * 0.5f),
                            Main.rand.NextFloat(0.4f, 0.8f))?.Configure(Main.rand.Next(22, 38), Main.rand.NextFloat(-0.25f, 0.25f));
                    }
                }
                return null;
            }

            if (t < DroopEnd) {
                //前倾拖螯：双螯瘫向身前地面，尾扇合拢
                HoldInPlace(ctx);
                ctx.DeathGloom = MathF.Max(ctx.DeathGloom, 0.62f);
                SeaShrimpAbyssScreen.PushGloom(0.55f);
                Vector2 forward = ctx.Owner.Skeleton.Nodes[0].Forward;
                Vector2 down = ctx.Owner.Skeleton.HeadDown;
                for (int a = 0; a < 2; a++) {
                    ctx.Claws[a] = new ClawDirective {
                        Mode = ClawMode.Hold,
                        Target = npc.Center + forward * (80f - a * 26f) + down * 64f,
                        Spring = 0.06f,
                        Damping = 0.9f,
                        ClawOpen = 0f,
                    };
                }
                ctx.TailFlare = 0f;
                ctx.SpineCurl = 0.14f;
                return null;
            }

            if (t < ImplodeFrame) {
                //收束拍：水与光被吸入头晶，全场静默（爆前的吸气）
                HoldInPlace(ctx);
                float suck = (t - DroopEnd) / (float)(ImplodeFrame - DroopEnd);
                ctx.CrystalGlow = suck;
                ctx.DeathGloom = MathF.Max(ctx.DeathGloom, 0.62f + suck * 0.2f);
                SeaShrimpAbyssScreen.PushGloom(0.55f + suck * 0.25f);
                if (!Main.dedServ && t % 2 == 0) {
                    Vector2 headPos = ctx.Owner.Skeleton.Nodes[0].Pos;
                    Vector2 from = headPos + Main.rand.NextVector2CircularEdge(1f, 1f) * Main.rand.NextFloat(90f, 220f);
                    PRTLoader.NewParticle<PRT_Spark>(from, (headPos - from) * 0.09f,
                        Color.Lerp(SeaShrimpRenderer.CrystalBlue, Color.White, suck),
                        Main.rand.NextFloat(0.4f, 0.8f))?.Configure(false, Main.rand.Next(12, 20));
                }
                return null;
            }

            if (t == ImplodeFrame) {
                //终极空泡内爆：全场唯一一次 impact frame，震屏预算收在这一拍
                if (!Main.dedServ) {
                    SeaShrimpAbyssScreen.TriggerImpactFrame(1f);
                    SoundEngine.PlaySound(SoundID.Item94 with { Volume = 1f, Pitch = -0.4f }, npc.Center);
                    SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.9f, Pitch = -0.6f }, npc.Center);
                    ShakeNearby(npc.Center, 14f, 1900f);
                    PRTLoader.NewParticle<PRT_StarPulseRing>(npc.Center, Vector2.Zero,
                        Color.White, 1f)?.Configure(2.4f, 0.05f, 18);
                    PRTLoader.NewParticle<PRT_StarPulseRing>(npc.Center, Vector2.Zero,
                        SeaShrimpRenderer.CrystalBlue, 1f)?.Configure(0.2f, 2.6f, 22);
                    for (int i = 0; i < 24; i++) {
                        PRTLoader.NewParticle<PRT_SHPCCoralBubble>(npc.Center + Main.rand.NextVector2Circular(90f, 60f),
                            new Vector2(Main.rand.NextFloat(-1f, 1f), -Main.rand.NextFloat(1f, 3f)),
                            Color.White * 0.8f, Main.rand.NextFloat(0.4f, 0.9f))?.Configure(Main.rand.Next(30, 60));
                    }
                }
                return null;
            }

            //长静默：壳体缓缓沉底，什么声音都没有
            ctx.Owner.Locomotion.RequestScripted();
            npc.velocity = new Vector2(npc.velocity.X * 0.92f, MathF.Min(npc.velocity.Y + 0.06f, 1.4f));
            ctx.DeathGloom = 1f;
            ctx.CrystalGlow = 0f;
            ctx.BodyAlpha = MathHelper.Clamp(1f - (t - ImplodeFrame) / 90f * 0.3f, 0.7f, 1f);
            SeaShrimpAbyssScreen.PushGloom(0.8f);

            if (t >= Total) {
                ctx.DeathPerformanceFinished = true;
                if (!VaultUtils.isClient) {
                    //放行真死：走原版击杀管线（掉落/旗标/尸块）
                    npc.StrikeInstantKill();
                }
            }
            return null;
        }
    }
}
