using CalamityOverhaul.Content.Items.Magic.Everdeeps;
using CalamityOverhaul.Content.Items.Melee.Abyssrends;
using CalamityOverhaul.Content.NPCs.SeaShrimp.Core;
using CalamityOverhaul.Content.NPCs.SeaShrimp.Projectiles;
using CalamityOverhaul.Content.NPCs.SeaShrimp.Rendering;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.SeaShrimp.States
{
    /// <summary>
    /// 蜕壳转阶段（40%，甲壳类专属演出）：清弹 → 70f 甲壳龟裂（裂纹音与晶屑渐密）→
    /// 旧壳作为实体碎屑崩落四散 + 裸晶形态跃出（Phase=3，体色转亮）→
    /// 落定怒吼收束。全程无敌；转阶段后攻速经冷却风起（公平阀）
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)SeaShrimpStateIndex.MoltTransition, typeof(SeaShrimpStateContext))]
    internal class SeaShrimpMoltTransitionState : SeaShrimpStateBase
    {
        public override string StateName => "MoltTransition";
        public override SeaShrimpStateIndex StateIndex => SeaShrimpStateIndex.MoltTransition;

        private const int CrackEnd = 70;
        private const int SettleFrame = 118;
        private const int Total = 164;

        public override void OnEnter(SeaShrimpStateContext ctx) {
            base.OnEnter(ctx);
            //阶段转换清弹（公平阀）
            if (!VaultUtils.isClient) {
                SeaShrimpBoss.ClearHostileProjectiles();
            }
        }

        public override ISeaShrimpState OnUpdate(SeaShrimpStateContext ctx) {
            NPC npc = ctx.Npc;
            int t = (int)Timer;
            Timer++;
            npc.dontTakeDamage = true;

            //运镜窗口：仅前 20 帧敞开，锚点每帧刷新
            if (t < 20) {
                SeaShrimpCutscenes.ArmMolt(npc.Center);
            }
            SeaShrimpCutscenes.MoltAnchor = npc.Center;

            if (t < CrackEnd) {
                //龟裂段：驻停颤抖，晶屑与裂纹音渐密，晶光拉满
                HoldInPlace(ctx);
                float build = t / (float)CrackEnd;
                ctx.CrystalGlow = MathF.Max(ctx.CrystalGlow, build);
                ctx.SpineCurl = MathF.Sin(t * 0.7f) * 0.06f * build;
                ctx.WaveGain = 0.25f;

                if (!Main.dedServ) {
                    if (t % 14 == 0) {
                        SoundEngine.PlaySound(SoundID.Dig with { Volume = 0.5f + build * 0.4f, Pitch = -0.3f + build * 0.5f, MaxInstances = 3 }, npc.Center);
                        ShakeNearby(npc.Center, 1.5f + build * 2f, 1100f);
                    }
                    if (Main.rand.NextFloat() < 0.2f + build * 0.6f) {
                        Vector2 seam = npc.Center + Main.rand.NextVector2Circular(70f, 44f);
                        PRTLoader.NewParticle<PRT_DefCrystalShard>(seam,
                            new Vector2(Main.rand.NextFloat(-1.4f, 1.4f), -Main.rand.NextFloat(0.6f, 2.2f)),
                            SeaShrimpRenderer.CrystalBlue * 0.9f,
                            Main.rand.NextFloat(0.4f, 0.8f))?.Configure(Main.rand.Next(20, 34), Main.rand.NextFloat(-0.2f, 0.2f));
                    }
                    //崩壳前 12 帧：水被吸向壳体——先内爆吸水，粒子密度反而收干（吸气拍）
                    if (t > CrackEnd - 12 && Main.GameUpdateCount % 2 == 0) {
                        Vector2 from = npc.Center + Main.rand.NextVector2CircularEdge(1f, 1f) * Main.rand.NextFloat(120f, 240f);
                        PRTLoader.NewParticle<PRT_AbyssGlob>(from, (npc.Center - from) * 0.1f,
                            Color.Lerp(SeaShrimpVFX.Deep, SeaShrimpVFX.Body, Main.rand.NextFloat()),
                            Main.rand.NextFloat(0.3f, 0.5f))?.Configure(12, 1.8f);
                    }
                }
                return null;
            }

            if (t == CrackEnd) {
                //崩壳帧：吸入的水轰然炸开——旧壳实体碎屑放射崩落（慢速可读的重力弧），本体跃起
                if (!VaultUtils.isClient) {
                    int damage = SeaShrimpDirector.ScaleProjectileDamage(npc, SeaShrimpDirector.ShellFragDamage);
                    for (int i = 0; i < 10; i++) {
                        float ang = -MathHelper.Pi * (0.12f + 0.76f * i / 9f);
                        Vector2 vel = ang.ToRotationVector2() * (5.4f + (i % 3) * 1.3f);
                        Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center + Main.rand.NextVector2Circular(30f, 20f),
                            vel, ModContent.ProjectileType<SeaShrimpShellFrag>(), damage, 2f, Main.myPlayer,
                            Main.rand.Next(3));
                    }
                    //服务器裁决进入 P3；ai[2] 随 netUpdate 广播
                    ctx.Phase = 3;
                    npc.netUpdate = true;
                }
                ctx.Owner.Locomotion.LaunchBallistic(new Vector2(0f, -11f), 8, 0.86f);
                if (!Main.dedServ) {
                    SoundEngine.PlaySound(SoundID.Shatter with { Volume = 1f, Pitch = -0.2f }, npc.Center);
                    SoundEngine.PlaySound(SoundID.Roar with { Volume = 0.9f, Pitch = 0.3f }, npc.Center);
                    SoundEngine.PlaySound(SoundID.Splash with { Volume = 0.8f, Pitch = -0.2f }, npc.Center);
                    ShakeNearby(npc.Center, 7f);
                    //全场大冲击环 + 滤镜脉冲(0.25 档,层级低于超空化 0.4,满档独留死亡)
                    SeaShrimpAbyssScreen.TriggerImpactFrame(0.25f);
                    ctx.AddRing(npc.Center, 430f, 34, 1f);
                    EverdeepVFX.SplashBurst(npc.Center, Vector2.UnitY * 12f, 1.3f);
                    for (int i = 0; i < 26; i++) {
                        PRTLoader.NewParticle<PRT_DefCrystalShard>(npc.Center + Main.rand.NextVector2Circular(60f, 40f),
                            Main.rand.NextVector2Circular(5f, 4f) - new Vector2(0f, 3f),
                            SeaShrimpRenderer.CrystalBlue,
                            Main.rand.NextFloat(0.5f, 1f))?.Configure(Main.rand.Next(26, 44), Main.rand.NextFloat(-0.3f, 0.3f));
                    }
                }
                return null;
            }

            //蜕壳进度推进：绘制层据此把体色提亮成裸晶形态
            ctx.Molted01 = MathHelper.Clamp(ctx.Molted01 + 0.03f, 0f, 1f);
            ctx.CrystalGlow = MathF.Max(ctx.CrystalGlow, 0.85f);

            if (t < SettleFrame) {
                //跃出段：弹道自治，尾扇全开
                ctx.TailFlare = 1f;
                ctx.SpineCurl = -0.3f * (1f - (t - CrackEnd) / (float)(SettleFrame - CrackEnd));
                return null;
            }

            HoldInPlace(ctx);
            ctx.TailFlare = 0.6f;

            if (t >= Total) {
                //转阶段后攻速风起：冷却抬高一档（公平阀）
                ctx.AttackCooldown = 70;
                return new SeaShrimpHubState();
            }
            return null;
        }
    }
}
