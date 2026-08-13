using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEmpressOfLight.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEmpressOfLight.Rendering;
using InnoVault.PRT;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEmpressOfLight.States
{
    /// <summary>暮光降临：光尘自天幕垂落汇聚→显形→辉光绽放</summary>
    [InnoVault.StateMachines.VaultState((int)EmpressStateIndex.Intro, typeof(EmpressStateContext))]
    internal class EmpressIntroState : EmpressStateBase
    {
        public override string StateName => "EmpressIntro";
        public override EmpressStateIndex StateIndex => EmpressStateIndex.Intro;

        private const int GatherEnd = 34;
        private const int MaterializeEnd = 150;
        private const int BloomFrame = 152;
        private const int TotalTime = 192;

        public override void OnEnter(EmpressStateContext context) {
            base.OnEnter(context);
            NPC npc = context.Npc;
            npc.velocity = new Vector2(0f, 4.5f);
            npc.Opacity = 0f;
            npc.dontTakeDamage = true;
            npc.damage = 0;
        }

        public override IEmpressState OnUpdate(EmpressStateContext context) {
            NPC npc = context.Npc;
            Timer++;

            npc.velocity *= 0.95f;
            npc.dontTakeDamage = Timer < MaterializeEnd + 20;
            npc.damage = 0;

            //姿态：登场双臂缓抬（原版 ai0=0 语义）
            context.Pose = EmpressPose.Spawn;
            context.PoseTimer = Timer;

            if (Timer == 10) {
                PlayLocal(SoundID.Item161, npc.Center);
            }

            if (Timer < GatherEnd) {
                //第一乐句：夜色里光尘自四方垂落
                npc.Opacity = 0f;
                if (!VaultUtils.isServer && Timer % 2 == 0) {
                    float hue = Main.rand.NextFloat();
                    Vector2 spawn = npc.Center + new Vector2(Main.rand.NextFloat(-420f, 420f), Main.rand.NextFloat(-380f, -120f));
                    PRTLoader.NewParticle<PRT_EmpressPetalDust>(spawn, new Vector2(0f, Main.rand.NextFloat(1.2f, 2.6f)),
                        EmpressMotion.Prism(hue, 0.6f), Main.rand.NextFloat(0.5f, 0.9f))?.Configure(40, hue);
                }
                //舞台幕布：显形前两侧升起零伤极光帘（纯装饰，随显形淡去）
                if (Timer == GatherEnd - 4 && !VaultUtils.isClient) {
                    EmpressCast.Aurora(npc, npc.Center + new Vector2(-320f, -160f), 0.7f, 0.12f, 210, 0);
                    EmpressCast.Aurora(npc, npc.Center + new Vector2(320f, -160f), 3.9f, -0.12f, 210, 0);
                }
            }
            else if (Timer < MaterializeEnd) {
                //第二乐句：轮廓自光尘中显形
                npc.Opacity = MathHelper.Clamp((Timer - GatherEnd) / (float)(MaterializeEnd - GatherEnd), 0f, 1f);

                if (!VaultUtils.isServer) {
                    //向心光丝，密度随显形推进
                    float p = npc.Opacity;
                    if (Main.rand.NextFloat() < 0.35f + p * 0.4f) {
                        float hue = (Timer / 150f + Main.rand.NextFloat(0.12f)) % 1f;
                        Vector2 spawn = npc.Center + Main.rand.NextVector2CircularEdge(190f, 220f) * (1.1f - p * 0.5f);
                        PRTLoader.NewParticle<PRT_EmpressSpark>(spawn, (npc.Center - spawn) * 0.055f,
                            EmpressMotion.Prism(hue, 0.66f), Main.rand.NextFloat(0.6f, 1.1f))?.Configure(22, hue);
                    }
                    //周身垂落的光雨
                    if (Timer % 3 == 0) {
                        float hue = Main.rand.NextFloat();
                        PRTLoader.NewParticle<PRT_EmpressPetalDust>(
                            npc.Center + new Vector2(Main.rand.NextFloat(-150f, 150f), -170f),
                            new Vector2(0f, Main.rand.NextFloat(1f, 2f)),
                            EmpressMotion.Prism(hue, 0.6f), Main.rand.NextFloat(0.4f, 0.8f))?.Configure(34, hue);
                    }
                }
                context.SetChargeState(3, npc.Opacity * 0.6f);
            }
            else {
                npc.Opacity = 1f;
            }

            if (Timer == BloomFrame) {
                //第三乐句：辉光绽放，战斗开始的宣告——双层辉光+光蝶四散+全屏棱彩
                EmpressCast.Radiance(npc, npc.Center, 480f, 30, 0.62f);
                EmpressCast.Radiance(npc, npc.Center, 220f, 22, 0.12f);
                EmpressMotion.Shake(npc.Center, 6.5f, 20);
                PlayLocal(SoundID.Item163 with { Volume = 0.9f }, npc.Center);
                PlayLocal(SoundID.Item161 with { Volume = 0.7f, Pitch = 0.2f }, npc.Center);
                if (!VaultUtils.isServer) {
                    EmpressScreenFX.PushPrismPulse(npc.Center, 0.7f, 30);
                    for (int i = 0; i < 10; i++) {
                        float bh = i / 10f;
                        PRTLoader.NewParticle<PRT_EmpressButterfly>(npc.Center + Main.rand.NextVector2Circular(60f, 60f),
                            (MathHelper.TwoPi / 10f * i).ToRotationVector2() * Main.rand.NextFloat(2.5f, 5f),
                            EmpressMotion.Prism(bh, 0.68f), Main.rand.NextFloat(0.6f, 1f))?.Configure(70, bh);
                    }
                }
            }

            if (Timer >= TotalTime) {
                npc.dontTakeDamage = false;
                return new EmpressConnectorState();
            }
            return null;
        }

        public override void OnExit(EmpressStateContext context) {
            base.OnExit(context);
            context.Npc.dontTakeDamage = false;
            context.Npc.Opacity = 1f;
        }
    }
}
