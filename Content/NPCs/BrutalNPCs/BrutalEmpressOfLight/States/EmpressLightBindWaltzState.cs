using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEmpressOfLight.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEmpressOfLight.Rendering;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEmpressOfLight.States
{
    /// <summary>
    /// 光绫缚舞：光笼收拢缚住玩家悬空定身，女皇绕身三段交叉剑舞（全程零接触的优雅处刑），
    /// 辐光爆绽掷出收尾。节拍固定帧不吃TempoScale，运镜与受缚端按常量对表；
    /// 伤害由受缚者本端脚本化结算（见EmpressGrabPerformancePlayer），本状态只管她的舞与世界侧演出
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)EmpressStateIndex.LightBindWaltz, typeof(EmpressStateContext))]
    internal class EmpressLightBindWaltzState : EmpressStateBase
    {
        public override string StateName => "EmpressLightBindWaltz";
        public override EmpressStateIndex StateIndex => EmpressStateIndex.LightBindWaltz;

        #region 节拍表（受缚端/运镜对表用，固定帧）
        /// <summary>缚定拍长：擒住的凝视与屈膝礼</summary>
        internal const int BindHold = 36;
        /// <summary>剑舞段数</summary>
        internal const int PassCount = 3;
        /// <summary>单段剑舞帧长</summary>
        internal const int PassLen = 46;
        /// <summary>终唱起点：聚光</summary>
        internal const int FinaleStart = BindHold + PassCount * PassLen;
        /// <summary>聚光截止，进入爆绽前的屏息</summary>
        internal const int GatherEnd = 206;
        /// <summary>辐光爆绽帧：终结伤+掷出</summary>
        internal const int BurstTick = 218;
        /// <summary>总时长，保底超时出口</summary>
        internal const int TotalTime = 270;
        /// <summary>光笼捕获半径（光笼状态收拢判定同用）</summary>
        internal const float CaptureRadius = 130f;
        /// <summary>投技冷却tick（45秒）</summary>
        internal const int GrabCooldownTicks = 2700;

        //单段内节拍：入位滑翔→迟滞回吸→屏息→点火擦身→硬刹
        private const int GlideEnd = 26;
        private const int IgniteBeat = 32;
        private const int CrossBeat = 37;
        private const float DashSpeed = 74f;
        private const float StationRadius = 380f;

        internal static int PassStart(int k) => BindHold + k * PassLen;
        /// <summary>第k段剑刃擦身帧（受缚端落伤对表）</summary>
        internal static int PassHitTick(int k) => PassStart(k) + CrossBeat;

        //三段交叉穿越的固定方向与侧偏：两记斜升挑剑成X，一记近水平掠顶收束；
        //侧偏保证零接触，第三段微升角避免贯穿后扎进地形
        private static readonly float[] PassAngles = [-0.55f, MathHelper.Pi + 0.55f, -0.12f];
        private static readonly float[] PassSideOffsets = [48f, -48f, -40f];
        #endregion

        private EmpressStateContext Context;
        private int victimIndex = -1;
        private Vector2 serverAnchor;

        public override void OnEnter(EmpressStateContext context) {
            base.OnEnter(context);
            Context = context;
            NPC npc = context.Npc;
            //受缚者身份：捕获时服务端写入npc.target，与状态索引同包同步
            victimIndex = npc.target;
            Player victim = ValidVictim();
            Vector2 vpos = victim?.Center ?? npc.Center;
            serverAnchor = vpos;

            //公平阀：清掉全部残余敌对弹幕，笼中人只面对剑舞本身
            EmpressCast.ClearHostileProjectiles(npc);

            if (victim != null) {
                //光绫缠上（零伤害视觉，同步给旁观者），寿命精确到爆绽帧
                EmpressCast.LightBind(npc, vpos, victimIndex, BurstTick + 2, 0.13f);
                //她瞬步至第一舞位，光尘掩相
                Vector2 station = vpos - PassAngles[0].ToRotationVector2() * StationRadius;
                EmpressMotion.PrismStep(npc, station);
                if (!VaultUtils.isClient) {
                    npc.netUpdate = true;
                }
            }

            //捕获强拍：低音收扣+一记高光
            PlayLocal(SoundID.Item163 with { Volume = 1f, Pitch = -0.35f }, vpos);
            PlayLocal(SoundID.Item160 with { Volume = 0.7f, Pitch = 0.4f }, vpos);
            EmpressMotion.CinematicShake(vpos, 5f, 14);
            if (!VaultUtils.isServer) {
                PulseNear(vpos, 0.5f, 22);
                PRTLoader.NewParticle<PRT_EmpressRipple>(vpos, Vector2.Zero, Color.White, 0.9f)?.Configure(18, 0.13f);
                for (int i = 0; i < 12; i++) {
                    float hue = (0.1f + i / 12f * 0.12f) % 1f;
                    Vector2 spawn = vpos + Main.rand.NextVector2CircularEdge(120f, 120f);
                    PRTLoader.NewParticle<PRT_EmpressSpark>(spawn, (vpos - spawn) * 0.09f,
                        EmpressMotion.Prism(hue, 0.72f), Main.rand.NextFloat(0.7f, 1.1f))?.Configure(16, hue);
                }
            }
        }

        public override IEmpressState OnUpdate(EmpressStateContext context) {
            Context = context;
            NPC npc = context.Npc;
            Timer++;

            Player victim = ValidVictim();

            //异常出口（服务端权威）：受缚者死亡/离场/被外力挪走→立即散绫收场
            if (!VaultUtils.isClient) {
                if (victim == null || Vector2.Distance(victim.Center, serverAnchor) > 640f) {
                    EmpressCast.KillLightBind(victimIndex);
                    return new EmpressConnectorState();
                }
            }

            Vector2 vpos = victim?.Center ?? npc.Center;

            if (Timer < BindHold) {
                BindUpdate(context, npc, vpos);
            }
            else if (Timer < FinaleStart) {
                DanceUpdate(context, npc, vpos);
            }
            else if (Timer < BurstTick) {
                GatherUpdate(context, npc, vpos);
            }
            else {
                ReleaseUpdate(context, npc, vpos);
            }

            EmpressMotion.AmbientGlow(npc, context.DayFormBlend);

            //保底超时出口
            if (Timer >= TotalTime) {
                return new EmpressConnectorState();
            }
            return null;
        }

        public override void OnExit(EmpressStateContext context) {
            base.OnExit(context);
            context.Npc.damage = 0;
            //提前中断（死亡演出/离场打断）时的兜底：散掉残余光绫
            EmpressCast.KillLightBind(victimIndex);
        }

        /// <summary>受缚者仍有效则返回，否则null</summary>
        private Player ValidVictim() {
            if (victimIndex < 0 || victimIndex >= Main.maxPlayers) {
                return null;
            }
            Player p = Main.player[victimIndex];
            return p.Alives() ? p : null;
        }

        /// <summary>带距离门的全屏棱彩脉冲：远处旁观者不吃满屏特效</summary>
        private static void PulseNear(Vector2 pos, float intensity, int life) {
            if (VaultUtils.isServer || Main.LocalPlayer.Distance(pos) > 2000f) {
                return;
            }
            EmpressScreenFX.PushPrismPulse(pos, intensity, life);
        }

        /// <summary>缚定拍：擒住的第一瞬完全静止（威压在于静），后接屈膝礼聚光</summary>
        private void BindUpdate(EmpressStateContext context, NPC npc, Vector2 vpos) {
            if (Timer < 14) {
                npc.velocity *= 0.7f;
                context.Pose = EmpressPose.Idle;
                context.PoseTimer = 0f;
                return;
            }
            context.Pose = EmpressPose.CastBoth;
            context.PoseTimer = 20f;
            float t = (Timer - 14f) / (BindHold - 14f);
            context.SetChargeState(3, t);
            EmpressMotion.HandChargeDust(context.LeftHand, t, context.DayFormBlend);
            EmpressMotion.HandChargeDust(context.RightHand, t, context.DayFormBlend);
            npc.velocity *= 0.9f;
            if (Timer == 14) {
                PlayLocal(SoundID.Item165 with { Volume = 0.6f, Pitch = 0.2f }, npc.Center);
            }
        }

        /// <summary>剑舞拍：入位滑翔→迟滞回吸→屏息→一帧点火擦身而过→硬刹，三段交叉</summary>
        private void DanceUpdate(EmpressStateContext context, NPC npc, Vector2 vpos) {
            int passIdx = Math.Clamp((Timer - BindHold) / PassLen, 0, PassCount - 1);
            int beat = (Timer - BindHold) % PassLen;

            Vector2 dashDir = PassAngles[passIdx].ToRotationVector2();
            Vector2 perp = dashDir.RotatedBy(MathHelper.PiOver2);
            //穿越线带侧偏：刀锋贴着受缚者掠过，永不触身
            Vector2 crossPoint = vpos + perp * PassSideOffsets[passIdx];
            Vector2 station = crossPoint - dashDir * StationRadius;

            if (beat < GlideEnd) {
                //入位滑翔（舞步长引），末段迟滞回吸反向蓄势
                GlideTo(npc, station, 0.05f, 0.12f, 34f);
                if (beat >= 14) {
                    npc.Center += EmpressMotion.ReelBack(-dashDir, (beat - 14) / 12f, 5.4f);
                }
                context.Pose = EmpressPose.Dance;
                //原版日舞臂帧窗口下限10（镜像RadiantDance约定）
                context.PoseTimer = MathHelper.Clamp(beat * 2.4f, 10f, 60f);
                context.SetChargeState(3, beat / (float)GlideEnd * 0.7f);
            }
            else if (beat < IgniteBeat) {
                //屏息：动作全停，辉光拉满
                npc.velocity *= 0.5f;
                context.Pose = EmpressPose.Dance;
                context.PoseTimer = 60f;
                context.SetChargeState(3, 1f);
                if (beat == GlideEnd) {
                    PlayLocal(SoundID.Item160 with { Volume = 0.95f, Pitch = 0.05f + passIdx * 0.13f }, npc.Center);
                }
            }
            else {
                if (beat == IgniteBeat) {
                    //一帧点火：直贯穿越线
                    Vector2 aim = (crossPoint - npc.Center).SafeNormalize(dashDir);
                    npc.velocity = aim * DashSpeed;
                    PulseNear(npc.Center, 0.24f, 12);
                }
                context.Pose = npc.velocity.X < 0f ? EmpressPose.DashLeft : EmpressPose.DashRight;
                context.PoseTimer = MathHelper.Clamp(30f + (beat - IgniteBeat) * 5f, 0f, 90f);
                context.ResetChargeState();

                if (beat == CrossBeat) {
                    CrossFlash(vpos, dashDir, passIdx);
                }
                if (beat > CrossBeat + 2) {
                    //刀已过身，硬刹
                    npc.velocity *= 0.72f;
                }
                //冲刺沿途光尘（客户端）
                if (!VaultUtils.isServer && npc.velocity.Length() > 20f && Main.rand.NextBool(2)) {
                    float hue = (0.5f + passIdx * 0.17f + Main.rand.NextFloat(0.08f)) % 1f;
                    PRTLoader.NewParticle<PRT_EmpressSpark>(npc.Center + Main.rand.NextVector2Circular(30f, 40f),
                        -npc.velocity * 0.06f, EmpressMotion.Prism(hue, 0.66f),
                        Main.rand.NextFloat(0.6f, 1f))?.Configure(14, hue);
                }
            }
        }

        /// <summary>剑刃擦身帧的世界侧演出：交错闪光+光屑扇，各端可见（落伤在受缚端结算）</summary>
        private void CrossFlash(Vector2 vpos, Vector2 dashDir, int passIdx) {
            PlayLocal(SoundID.Item162 with { Volume = 0.75f, Pitch = 0.22f + passIdx * 0.12f, MaxInstances = 3 }, vpos);
            EmpressMotion.CinematicShake(vpos, 3.5f, 10);
            if (VaultUtils.isServer) {
                return;
            }
            PRTLoader.NewParticle<PRT_EmpressRipple>(vpos, Vector2.Zero, Color.White, 0.5f)?.Configure(12, 0.13f);
            Vector2 perp = dashDir.RotatedBy(MathHelper.PiOver2);
            for (int i = 0; i < 10; i++) {
                //垂直于刀线的双侧光屑扇
                float side = i % 2 == 0 ? 1f : -1f;
                float hue = (0.55f + passIdx * 0.17f + i * 0.02f) % 1f;
                Vector2 vel = perp * side * Main.rand.NextFloat(2.5f, 7f) + dashDir * Main.rand.NextFloat(-1.5f, 1.5f);
                PRTLoader.NewParticle<PRT_EmpressSpark>(vpos + Main.rand.NextVector2Circular(16f, 24f), vel,
                    EmpressMotion.Prism(hue, 0.7f), Main.rand.NextFloat(0.7f, 1.15f))?.Configure(18, hue);
            }
        }

        /// <summary>终唱聚光：她升至受缚者上方，万光汇入，72%截止后收势屏息</summary>
        private void GatherUpdate(EmpressStateContext context, NPC npc, Vector2 vpos) {
            GlideTo(npc, vpos + new Vector2(0f, -300f), 0.04f, 0.12f, 26f);
            context.Pose = EmpressPose.CastBoth;
            context.PoseTimer = 20f;
            float t = (Timer - FinaleStart) / (float)(BurstTick - FinaleStart);
            context.SetChargeState(3, t);

            if (Timer == FinaleStart + 2) {
                PlayLocal(SoundID.Item161 with { Volume = 0.9f, Pitch = 0.35f }, npc.Center);
            }
            //聚光只到GatherEnd：爆绽前的12帧完全静默（蓄力语法的屏息）
            if (!VaultUtils.isServer && Timer < GatherEnd && Main.rand.NextFloat() < 0.3f + t * 0.5f) {
                float hue = Main.rand.NextFloat();
                Vector2 spawn = vpos + Main.rand.NextVector2CircularEdge(340f, 340f) * (1f - t * 0.45f);
                PRTLoader.NewParticle<PRT_EmpressSpark>(spawn, (vpos - spawn) * (0.06f + t * 0.05f),
                    EmpressMotion.Prism(hue, 0.72f), Main.rand.NextFloat(0.7f, 1.2f))?.Configure(14, hue);
            }
            if (!VaultUtils.isServer && Timer == GatherEnd) {
                PRTLoader.NewParticle<PRT_EmpressRipple>(vpos, Vector2.Zero, Color.White, 0.6f)?.Configure(12, 0.6f);
            }
        }

        /// <summary>爆绽与回复拍：辐光强拍掷出受缚者，她被反作用推离后敛息退场</summary>
        private void ReleaseUpdate(EmpressStateContext context, NPC npc, Vector2 vpos) {
            if (Timer == BurstTick) {
                //辐光爆绽：终结强拍（服务端生成同步，全端可见）
                EmpressCast.Radiance(npc, vpos, 620f, 44, 0.62f);
                EmpressMotion.CinematicShake(vpos, 9f, 30);
                PlayLocal(SoundID.Item162 with { Volume = 1f, Pitch = 0.1f }, vpos);
                PlayLocal(SoundID.Item163 with { Volume = 1f, Pitch = -0.1f }, vpos);
                //反作用：她也被自己的爆绽轻轻推离
                npc.velocity = new Vector2(0f, -6.5f);
                PulseNear(vpos, 0.9f, 40);
                if (!VaultUtils.isServer) {
                    for (int i = 0; i < 16; i++) {
                        float hue = i / 16f;
                        PRTLoader.NewParticle<PRT_EmpressButterfly>(vpos + Main.rand.NextVector2Circular(40f, 50f),
                            Main.rand.NextVector2Circular(4f, 3.5f) + new Vector2(0f, -1.2f),
                            EmpressMotion.Prism(hue, 0.68f), Main.rand.NextFloat(0.7f, 1.2f))?
                            .Configure(Main.rand.Next(60, 100), hue);
                    }
                }
            }
            //回复拍：缓缓退开，敛息无攻击
            npc.velocity *= 0.94f;
            context.Pose = EmpressPose.Idle;
            context.PoseTimer = 0f;
            context.ResetChargeState();
        }
    }
}
