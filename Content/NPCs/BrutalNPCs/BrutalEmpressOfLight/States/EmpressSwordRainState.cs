using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEmpressOfLight.Core;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEmpressOfLight.States
{
    /// <summary>
    /// 剑雨阵：光剑编队在她身后展开成冠弧，悬停锁定，
    /// 而后自一端向另一端涟漪式齐射，一整排杀意的波浪
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)EmpressStateIndex.SwordRain, typeof(EmpressStateContext))]
    internal class EmpressSwordRainState : EmpressStateBase
    {
        public override string StateName => "EmpressSwordRain";
        public override EmpressStateIndex StateIndex => EmpressStateIndex.SwordRain;

        private int VolleyCount => Context.IsSecondPhase ? 3 : 2;
        private int BladesPerVolley => Context.IsSecondPhase ? 14 : 12;
        /// <summary>单轮时长：召唤+悬停+齐射尾</summary>
        private int VolleyTime => Context.Scaled(118);
        private int TotalTime => VolleyCount * VolleyTime + Context.Scaled(50);

        /// <summary>剑的基础悬停帧（含瞄准窗），错拍在此之上叠加</summary>
        private const int BaseHover = 56;
        private const int SummonStagger = 2;

        private EmpressStateContext Context;

        public override IEmpressState OnUpdate(EmpressStateContext context) {
            Context = context;
            NPC npc = context.Npc;
            Player target = context.Target;
            Timer++;

            int volleyIdx = Timer / VolleyTime;
            int beat = Timer % VolleyTime;
            bool casting = volleyIdx < VolleyCount;

            //每轮换边：她swoop到玩家另一侧，剑阵跟着换朝向
            if (target.Alives()) {
                int side = volleyIdx % 2 == 0 ? -1 : 1;
                Vector2 dest = target.Center + new Vector2(side * 360f, -340f) + EmpressMotion.Breathing(0.9f);
                GlideTo(npc, dest, 0.02f, 0.088f, 22f);
            }

            if (casting) {
                int summonWindow = BladesPerVolley * SummonStagger;
                if (beat < summonWindow + 8) {
                    //召唤窗：右手扬起，一把接一把落位
                    context.Pose = EmpressPose.CastRight;
                    context.PoseTimer = 20f;
                    context.SetChargeState(2, beat / (float)summonWindow * 0.6f);
                    EmpressMotion.HandChargeDust(context.RightHand, beat / (float)summonWindow * 0.6f, context.DayFormBlend);
                }
                else if (beat > BaseHover - 8 && beat < BaseHover + BladesPerVolley * 4) {
                    //齐射窗：双手一压
                    context.Pose = EmpressPose.CastBoth;
                    context.PoseTimer = 20f;
                    context.ResetChargeState();
                }
                else {
                    context.Pose = EmpressPose.Idle;
                    context.PoseTimer = 0f;
                }

                //逐把召唤：冠弧上错拍落位
                if (beat < summonWindow && beat % SummonStagger == 0) {
                    int bladeIdx = beat / SummonStagger;
                    SummonBlade(context, npc, target, volleyIdx, bladeIdx);
                }

                if (beat == 2) {
                    PlayLocal(SoundID.Item161 with { Volume = 0.6f, Pitch = 0.3f }, npc.Center);
                }
            }
            else {
                context.Pose = EmpressPose.Idle;
                context.PoseTimer = 0f;
            }

            EmpressMotion.AmbientGlow(npc, context.DayFormBlend);

            if (Timer >= TotalTime) {
                return new EmpressConnectorState();
            }
            return null;
        }

        /// <summary>在冠弧上落一把剑：悬停时长带涟漪错拍，从一端荡向另一端</summary>
        private void SummonBlade(EmpressStateContext context, NPC npc, Player target, int volleyIdx, int bladeIdx) {
            if (VaultUtils.isClient || !target.Alives()) {
                return;
            }
            int count = BladesPerVolley;
            //冠弧：她头顶张开的200°扇
            float arcSpan = MathHelper.ToRadians(200f);
            float arcStart = -MathHelper.PiOver2 - arcSpan * 0.5f;
            float angle = arcStart + arcSpan * (bladeIdx / (float)(count - 1));
            Vector2 pos = npc.Center + angle.ToRotationVector2() * 205f;

            //错拍：齐射按落位顺序涟漪推进；奇数轮反向荡回
            int rippleIdx = volleyIdx % 2 == 0 ? bladeIdx : count - 1 - bladeIdx;
            int hover = context.Scaled(BaseHover) - bladeIdx * SummonStagger + rippleIdx * 4;
            float hue = bladeIdx / (float)count;

            EmpressCast.Blade(npc, pos, hover, context.BladeDamage, hue, PickVolleyTarget(npc, volleyIdx));
        }

        /// <summary>多人公平：逐轮轮换锁定目标，压力在在场玩家间轮转</summary>
        private static int PickVolleyTarget(NPC npc, int volleyIdx) {
            int seen = 0;
            int fallback = npc.target;
            for (int i = 0; i < Main.maxPlayers; i++) {
                Player p = Main.player[i];
                if (!p.active || p.dead || npc.Distance(p.Center) > 2800f) {
                    continue;
                }
                if (seen == volleyIdx % System.Math.Max(CountNearPlayers(npc), 1)) {
                    return i;
                }
                seen++;
            }
            return fallback;
        }

        private static int CountNearPlayers(NPC npc) {
            int count = 0;
            for (int i = 0; i < Main.maxPlayers; i++) {
                Player p = Main.player[i];
                if (p.active && !p.dead && npc.Distance(p.Center) <= 2800f) {
                    count++;
                }
            }
            return count;
        }
    }
}
