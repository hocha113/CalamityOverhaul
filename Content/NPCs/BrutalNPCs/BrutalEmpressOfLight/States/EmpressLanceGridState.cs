using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEmpressOfLight.Core;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEmpressOfLight.States
{
    /// <summary>
    /// 以太枪骑网格：整片战场铺开平行预告线，按行错拍执行
    /// 一波贯穿沿网格荡过去；网格胞是安全区，读格站位是解法
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)EmpressStateIndex.LanceGrid, typeof(EmpressStateContext))]
    internal class EmpressLanceGridState : EmpressStateBase
    {
        public override string StateName => "EmpressLanceGrid";
        public override EmpressStateIndex StateIndex => EmpressStateIndex.LanceGrid;

        private int WaveCount => Context.IsSecondPhase ? 3 : 2;
        private int WaveInterval => Context.Scaled(64);
        private int TailTime => Context.Scaled(110);
        private int TotalTime => WaveCount * WaveInterval + TailTime;

        /// <summary>行距：留给玩家的胞宽（约3个玩家身位）</summary>
        private const float LaneSpacing = 172f;
        /// <summary>双轨半距：同行一对平行矛的间隔，胞内安全区仍≥100px</summary>
        private const float RailGap = 20f;
        private const int BaseTelegraph = 48;
        /// <summary>错拍步长：一行比上一行迟这么多帧发射</summary>
        private const int StaggerStep = 5;

        private EmpressStateContext Context;

        public override IEmpressState OnUpdate(EmpressStateContext context) {
            Context = context;
            NPC npc = context.Npc;
            Player target = context.Target;
            Timer++;

            //她退到侧上方观礼位，网格是她的乐谱
            if (target.Alives()) {
                int side = npc.Center.X > target.Center.X ? 1 : -1;
                GlideTo(npc, target.Center + new Vector2(side * 420f, -330f) + EmpressMotion.Breathing(0.6f), 0.014f, 0.09f, 13f);
            }

            int waveIdx = Timer / WaveInterval;
            int beat = Timer % WaveInterval;
            bool casting = waveIdx < WaveCount;

            //双手长引，掌心蓄力随波推进
            if (casting) {
                context.Pose = EmpressPose.CastBoth;
                context.PoseTimer = 20f;
                float chargeT = beat / (float)WaveInterval;
                context.SetChargeState(3, chargeT * 0.8f);
                EmpressMotion.HandChargeDust(context.LeftHand, chargeT * 0.8f, context.DayFormBlend);
                EmpressMotion.HandChargeDust(context.RightHand, chargeT * 0.8f, context.DayFormBlend);
            }
            else {
                context.Pose = EmpressPose.Idle;
                context.PoseTimer = 0f;
            }

            //每波起手铺格
            if (casting && beat == 8) {
                CastWave(context, npc, target, waveIdx);
            }

            EmpressMotion.AmbientGlow(npc, context.DayFormBlend);

            if (Timer >= TotalTime) {
                return new EmpressConnectorState();
            }
            return null;
        }

        /// <summary>铺一波网格：0横排 1竖列 2对角（仅P2第三波）</summary>
        private void CastWave(EmpressStateContext context, NPC npc, Player target, int waveIdx) {
            PlayLocal(SoundID.Item162 with { Volume = 1f, Pitch = -0.15f + waveIdx * 0.12f }, npc.Center);
            EmpressMotion.Shake(npc.Center, 3f, 10);

            if (VaultUtils.isClient || !target.Alives()) {
                return;
            }

            Vector2 anchor = target.Center;
            int orientation = waveIdx % 3;
            //修罗模式多铺一行
            int halfLanes = context.IsAsuraMode ? 5 : 4;

            //权威端掷骰：本波从哪一侧灌入（编码进弹幕生成位，天然同步）
            bool fromPositive = Main.rand.NextBool();

            switch (orientation) {
                case 0: {
                    //横排：贯穿走X向；网格对齐玩家+半距，脚下永远是胞心
                    float gridBase = anchor.Y + LaneSpacing * 0.5f;
                    for (int lane = -halfLanes; lane <= halfLanes; lane++) {
                        float y = gridBase + lane * LaneSpacing;
                        float dirSign = fromPositive ? -1f : 1f;
                        float x = anchor.X - dirSign * 1350f;
                        float angle = dirSign > 0 ? 0f : MathHelper.Pi;
                        int stagger = (lane + halfLanes) * StaggerStep;
                        float hue = (lane + halfLanes) / (float)(halfLanes * 2 + 1);
                        //双轨枪骑：同行一对平行矛错1拍，轨对读作实心行，胞间距不变
                        EmpressCast.Lance(npc, new Vector2(x, y - RailGap), angle, context.LanceDamage, hue,
                            context.Scaled(BaseTelegraph) + stagger);
                        EmpressCast.Lance(npc, new Vector2(x, y + RailGap), angle, context.LanceDamage,
                            (hue + 0.05f) % 1f, context.Scaled(BaseTelegraph) + stagger + 3);
                    }
                    break;
                }
                case 1: {
                    //竖列：贯穿走Y向，自上而下
                    float gridBase = anchor.X + LaneSpacing * 0.5f;
                    int halfCols = halfLanes + 1;
                    for (int lane = -halfCols; lane <= halfCols; lane++) {
                        float x = gridBase + lane * LaneSpacing;
                        float y = anchor.Y - 1150f;
                        int stagger = (lane + halfCols) * StaggerStep;
                        float hue = 0.5f + (lane + halfCols) / (float)(halfCols * 2 + 1) * 0.5f;
                        EmpressCast.Lance(npc, new Vector2(x - RailGap, y), MathHelper.PiOver2, context.LanceDamage, hue,
                            context.Scaled(BaseTelegraph) + stagger);
                        EmpressCast.Lance(npc, new Vector2(x + RailGap, y), MathHelper.PiOver2, context.LanceDamage,
                            (hue + 0.05f) % 1f, context.Scaled(BaseTelegraph) + stagger + 3);
                    }
                    break;
                }
                default: {
                    //对角波（P2终章）：45°斜灌，错拍自中心向两侧展开
                    float diagAngle = fromPositive ? MathHelper.PiOver4 : MathHelper.Pi - MathHelper.PiOver4;
                    Vector2 dir = diagAngle.ToRotationVector2();
                    Vector2 perp = dir.RotatedBy(MathHelper.PiOver2);
                    for (int lane = -halfLanes; lane <= halfLanes; lane++) {
                        Vector2 pos = anchor + perp * (lane * LaneSpacing + LaneSpacing * 0.5f) - dir * 1350f;
                        int stagger = System.Math.Abs(lane) * (StaggerStep + 2);
                        float hue = 0.75f + lane / (float)(halfLanes * 2 + 1) * 0.5f;
                        EmpressCast.Lance(npc, pos - perp * RailGap, diagAngle, context.LanceDamage, hue,
                            context.Scaled(BaseTelegraph + 8) + stagger);
                        EmpressCast.Lance(npc, pos + perp * RailGap, diagAngle, context.LanceDamage,
                            (hue + 0.05f) % 1f, context.Scaled(BaseTelegraph + 8) + stagger + 3);
                    }
                    break;
                }
            }
        }
    }
}
