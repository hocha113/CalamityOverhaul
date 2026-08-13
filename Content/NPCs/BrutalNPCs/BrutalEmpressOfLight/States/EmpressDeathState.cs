using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEmpressOfLight.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEmpressOfLight.Rendering;
using InnoVault.PRT;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEmpressOfLight.States
{
    /// <summary>
    /// 光之消散：踉跄→升空展翼（极光在身后垂落）→万光归一的屏息→
    /// 绽散成一场光蝶雨。她不是被杀死的，是回到光里去了
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)EmpressStateIndex.Death, typeof(EmpressStateContext))]
    internal class EmpressDeathState : EmpressStateBase
    {
        public override string StateName => "EmpressDeath";
        public override EmpressStateIndex StateIndex => EmpressStateIndex.Death;

        //演出节点（供运镜对表）
        internal const int StaggerEnd = 70;
        internal const int AscendEnd = 170;
        internal const int GatherEnd = 235;
        internal const int TotalTime = 335;

        public override void OnEnter(EmpressStateContext context) {
            base.OnEnter(context);
            NPC npc = context.Npc;
            context.DeathPerformanceFinished = false;
            //公平阀：清弹幕，谢幕不带刀
            EmpressCast.ClearHostileProjectiles(npc);
            npc.dontTakeDamage = true;
            npc.damage = 0;
            if (npc.life < 1) {
                npc.life = 1;
            }
            npc.velocity *= 0.3f;
            for (int i = 0; i < npc.buffType.Length; i++) {
                npc.buffTime[i] = 0;
            }
            PlayLocal(SoundID.Item161 with { Volume = 0.9f, Pitch = -0.4f }, npc.Center);
            PlayLocal(SoundID.NPCDeath62 with { Volume = 0.55f, Pitch = 0.3f }, npc.Center);
        }

        public override IEmpressState OnUpdate(EmpressStateContext context) {
            NPC npc = context.Npc;
            Timer++;

            npc.dontTakeDamage = true;
            npc.damage = 0;
            if (npc.life < 1) {
                npc.life = 1;
            }

            if (Timer < StaggerEnd) {
                StaggerUpdate(context, npc);
            }
            else if (Timer < AscendEnd) {
                AscendUpdate(context, npc);
            }
            else if (Timer < GatherEnd) {
                GatherUpdate(context, npc);
            }
            else {
                DissolveUpdate(context, npc);
            }

            //演出终帧：放行真死（服务端/单机）
            if (Timer >= TotalTime && !VaultUtils.isClient) {
                context.DeathPerformanceFinished = true;
                npc.dontTakeDamage = false;
                npc.life = 0;
                npc.HitEffect();
                npc.checkDead();
                npc.netUpdate = true;
            }
            return null;
        }

        public override void OnExit(EmpressStateContext context) {
            base.OnExit(context);
            if (!context.DeathPerformanceFinished && context.Npc != null) {
                context.Npc.dontTakeDamage = false;
            }
        }

        /// <summary>踉跄：光从她的轮廓裂出，身形下坠又强撑住</summary>
        private void StaggerUpdate(EmpressStateContext context, NPC npc) {
            context.Pose = EmpressPose.Idle;
            context.PoseTimer = 0f;

            //下坠与挣扎：两次下沉两次撑起
            float sag = (float)System.Math.Sin(Timer * 0.09f);
            npc.velocity = new Vector2((float)System.Math.Sin(Timer * 0.23f) * 0.8f, 0.6f + sag * 0.9f);

            if (!VaultUtils.isServer) {
                //轮廓裂光
                if (Timer % 4 == 0) {
                    float hue = Main.rand.NextFloat();
                    Vector2 crack = npc.Center + Main.rand.NextVector2Circular(46f, 66f);
                    PRTLoader.NewParticle<PRT_EmpressSpark>(crack, Main.rand.NextVector2Circular(2.5f, 2.5f),
                        EmpressMotion.Prism(hue, 0.72f), Main.rand.NextFloat(0.8f, 1.4f))?.Configure(20, hue);
                }
                if (Timer % 16 == 0) {
                    PRTLoader.NewParticle<PRT_EmpressRipple>(npc.Center, Vector2.Zero, Color.White, 0.5f)?
                        .Configure(16, Main.rand.NextFloat());
                    EmpressMotion.CinematicShake(npc.Center, 2f, 8);
                }
            }
            if (Timer == StaggerEnd - 12) {
                PlayLocal(SoundID.Item161 with { Volume = 0.8f, Pitch = 0.15f }, npc.Center);
            }
        }

        /// <summary>升空展翼：她缓缓上升，极光帘幕在身后垂落，光雨自天顶倾下</summary>
        private void AscendUpdate(EmpressStateContext context, NPC npc) {
            context.Pose = EmpressPose.Spawn;
            context.PoseTimer = MathHelper.Clamp(Timer - StaggerEnd, 0f, 100f);

            float t = (Timer - StaggerEnd) / (float)(AscendEnd - StaggerEnd);
            npc.velocity = Vector2.Lerp(npc.velocity, new Vector2(0f, -3.4f * (1f - t * 0.4f)), 0.05f);

            //死亡极光：纯演出，零伤害
            if (Timer == StaggerEnd + 12 && !VaultUtils.isClient) {
                EmpressCast.Aurora(npc, npc.Center + new Vector2(-360f, -160f), 0.6f, 0.06f, 250, 0);
                EmpressCast.Aurora(npc, npc.Center + new Vector2(360f, -160f), 2.8f, -0.06f, 250, 0);
            }
            if (Timer == StaggerEnd + 14) {
                PlayLocal(SoundID.Item165 with { Volume = 0.85f, Pitch = -0.2f }, npc.Center);
            }

            if (!VaultUtils.isServer) {
                EmpressScreenFX.DeclareAmbient(0.3f + t * 0.3f);
                //光streams向上汇入她
                if (Timer % 2 == 0) {
                    float hue = Main.rand.NextFloat();
                    Vector2 spawn = npc.Center + new Vector2(Main.rand.NextFloat(-260f, 260f), Main.rand.NextFloat(120f, 300f));
                    PRTLoader.NewParticle<PRT_EmpressSpark>(spawn, (npc.Center - spawn) * 0.03f,
                        EmpressMotion.Prism(hue, 0.66f), Main.rand.NextFloat(0.6f, 1.1f))?.Configure(30, hue);
                }
            }
        }

        /// <summary>万光归一：全场光尘坍缩入她，末12帧完全静默——绽散前的屏息</summary>
        private void GatherUpdate(EmpressStateContext context, NPC npc) {
            context.Pose = EmpressPose.Transform;
            //上限58：避开原版变身绘制在ai[1]≥60后的本体隐没窗（此处她必须始终可见）
            context.PoseTimer = MathHelper.Clamp((Timer - AscendEnd) / (float)(GatherEnd - AscendEnd) * 58f, 0f, 58f);

            npc.velocity *= 0.9f;
            float t = (Timer - AscendEnd) / (float)(GatherEnd - AscendEnd);
            context.SetChargeState(3, t);

            bool silence = Timer > GatherEnd - 12;
            //屏息始点：一圈白色收束涟漪标记"万光归一"完成
            if (!VaultUtils.isServer && Timer == GatherEnd - 12) {
                PRTLoader.NewParticle<PRT_EmpressRipple>(npc.Center, Vector2.Zero, Color.White, 0.8f)?
                    .Configure(12, 0.6f);
            }
            if (!VaultUtils.isServer && !silence) {
                EmpressScreenFX.DeclareAmbient(0.6f + t * 0.3f);
                if (Main.rand.NextFloat() < 0.45f + t * 0.5f) {
                    float hue = Main.rand.NextFloat();
                    Vector2 spawn = npc.Center + Main.rand.NextVector2CircularEdge(420f, 440f) * (1f - t * 0.55f);
                    PRTLoader.NewParticle<PRT_EmpressSpark>(spawn, (npc.Center - spawn) * (0.05f + t * 0.07f),
                        EmpressMotion.Prism(hue, 0.74f), Main.rand.NextFloat(0.8f, 1.3f))?.Configure(14, hue);
                }
            }
            if (Timer == AscendEnd + 8) {
                PlayLocal(SoundID.Item161 with { Volume = 0.9f, Pitch = 0.3f }, npc.Center);
            }
        }

        /// <summary>绽散：一场光蝶雨。三重辉光爆放错拍绽开，她的身形化进光里</summary>
        private void DissolveUpdate(EmpressStateContext context, NPC npc) {
            context.Pose = EmpressPose.Idle;
            context.PoseTimer = 0f;
            context.ResetChargeState();
            npc.velocity *= 0.92f;

            int dissolveT = Timer - GatherEnd;

            //身形淡出
            npc.Opacity = MathHelper.Clamp(1f - dissolveT / 55f, 0f, 1f);

            if (dissolveT == 1) {
                //绽散主拍
                EmpressCast.Radiance(npc, npc.Center, 720f, 44, 0.62f);
                EmpressMotion.CinematicShake(npc.Center, 10f, 34);
                if (!VaultUtils.isServer) {
                    EmpressScreenFX.PushPrismPulse(npc.Center, 1f, 46);
                    //光蝶雨：她的形体散成振翅的光
                    for (int i = 0; i < 56; i++) {
                        float hue = i / 56f;
                        Vector2 vel = Main.rand.NextVector2Circular(6f, 5f) + new Vector2(0f, -1.5f);
                        PRTLoader.NewParticle<PRT_EmpressButterfly>(npc.Center + Main.rand.NextVector2Circular(50f, 70f),
                            vel, EmpressMotion.Prism(hue, 0.68f), Main.rand.NextFloat(0.8f, 1.5f))?
                            .Configure(Main.rand.Next(70, 120), hue);
                    }
                }
                PlayLocal(SoundID.Item162 with { Volume = 1f, Pitch = 0.15f }, npc.Center);
                PlayLocal(SoundID.Item163 with { Volume = 1f }, npc.Center);
            }
            if ((dissolveT == 10 || dissolveT == 20) && !VaultUtils.isClient) {
                //错拍次级绽放
                float radius = dissolveT == 10 ? 440f : 260f;
                EmpressCast.Radiance(npc, npc.Center + Main.rand.NextVector2Circular(40f, 40f), radius, 34, 0.3f + dissolveT * 0.02f);
            }

            //第二波光蝶：主拍余韵里迟到的振翅，升向天幕
            if (dissolveT == 26 && !VaultUtils.isServer) {
                for (int i = 0; i < 18; i++) {
                    float hue = Main.rand.NextFloat();
                    PRTLoader.NewParticle<PRT_EmpressButterfly>(npc.Center + Main.rand.NextVector2Circular(140f, 120f),
                        new Vector2(Main.rand.NextFloat(-2f, 2f), Main.rand.NextFloat(-3.5f, -1.2f)),
                        EmpressMotion.Prism(hue, 0.66f), Main.rand.NextFloat(0.6f, 1.1f))?
                        .Configure(Main.rand.Next(80, 130), hue);
                }
            }

            if (!VaultUtils.isServer) {
                float decay = MathHelper.Clamp(1f - dissolveT / 90f, 0f, 1f);
                EmpressScreenFX.DeclareAmbient(0.7f * decay);
                //持续的光羽余落
                if (dissolveT < 70 && Timer % 2 == 0) {
                    float hue = Main.rand.NextFloat();
                    PRTLoader.NewParticle<PRT_EmpressPetalDust>(npc.Center + Main.rand.NextVector2Circular(90f, 110f),
                        new Vector2(Main.rand.NextFloat(-1f, 1f), Main.rand.NextFloat(-1.8f, -0.4f)),
                        EmpressMotion.Prism(hue, 0.64f), Main.rand.NextFloat(0.5f, 1f))?.Configure(50, hue);
                }
            }
        }
    }
}
