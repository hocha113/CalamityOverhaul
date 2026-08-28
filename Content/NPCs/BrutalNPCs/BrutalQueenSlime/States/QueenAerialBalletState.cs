using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalQueenSlime.Core;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalQueenSlime.States
{
    /// <summary>
    /// 二阶段枢纽·掠影冲刺链：悬定凝视→蓄势后拉→一帧全速掠移→硬刹钉位，连做数拍后选招。
    /// 冲刺仅位移不带伤(枢纽是连接拍)；缺员插队分裂召唤，投技冷却好插队囚舞。
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)QueenSlimeStateIndex.AerialBallet, typeof(QueenSlimeStateContext))]
    internal class QueenAerialBalletState : QueenSlimeStateBase
    {
        public override string StateName => "AerialBallet";
        public override QueenSlimeStateIndex StateIndex => QueenSlimeStateIndex.AerialBallet;

        #region 掠影节拍常量
        private const int PoiseEnd = 14;
        private const int PullbackEnd = 20;
        private const int GlideEnd = 32;
        private const int FlitPeriod = 46;
        #endregion

        private int FlitCount(QueenSlimeStateContext ctx) {
            int baseCount = ctx.IsAsuraMode ? 2 : 3;
            //大招后节奏提速
            return ctx.UltFired ? Math.Max(baseCount - 1, 2) : baseCount;
        }

        /// <summary>本拍冲刺方向(发射帧锁定)</summary>
        private Vector2 flitDir = Vector2.UnitX;
        private float flitSpeed;

        public QueenAerialBalletState() {
        }

        public override void OnEnter(QueenSlimeStateContext context) {
            base.OnEnter(context);
            NPC npc = context.Npc;
            DisableContactDamage(npc);
            npc.noGravity = true;
            npc.noTileCollide = true;
        }

        public override IQueenSlimeState OnUpdate(QueenSlimeStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;

            Timer++;
            DisableContactDamage(npc);

            int flitIndex = (int)Timer / FlitPeriod;
            int flitT = (int)Timer % FlitPeriod;

            if (flitIndex >= FlitCount(context)) {
                QueenMotion.FlitBrake(npc, 0.88f);
                context.PoseCommand = 5;
                if (!VaultUtils.isClient) {
                    return ChooseNextAttack(context);
                }
                return null;
            }

            //本拍锚点：左右交替绕玩家，高度带呼吸(确定性，各端一致)
            int side = flitIndex % 2 == 0 ? 1 : -1;
            Vector2 anchor = player.Center + new Vector2(
                side * (392f + 70f * (float)Math.Sin(flitIndex * 1.7f)),
                -288f - 62f * (float)Math.Cos(flitIndex * 2.3f));

            if (flitT < PoiseEnd) {
                //悬定凝视：轻弹簧慢靠，读作"选落点"
                QueenMotion.SpringHover(npc, npc.Center + (anchor - npc.Center) * 0.1f, 0.012f, 0.12f, 8f);
                FaceTarget(npc, player.Center);
                QueenMotion.FlightLean(npc);
                context.PoseCommand = 5;
                context.WingFlapBoost = 0.6f;
                //锁定本拍冲刺线(此后不再改向——预备即承诺)
                if (flitT == PoiseEnd - 1) {
                    flitDir = (anchor - npc.Center).SafeNormalize(Vector2.UnitX);
                    flitSpeed = MathHelper.Clamp(Vector2.Distance(npc.Center, anchor) / 9f, 17f, 33f);
                }
            }
            else if (flitT < PullbackEnd) {
                //蓄势后拉(吸气拍)
                QueenMotion.FlitPullback(npc, flitDir, (flitT - PoiseEnd) / (float)(PullbackEnd - PoiseEnd), 2.4f);
                context.PoseCommand = 5;
                context.WingFlapBoost = 1.4f;
            }
            else if (flitT == PullbackEnd) {
                //一帧全速掠移
                QueenMotion.FlitLaunch(npc, flitDir, flitSpeed);
                context.PushSquash(0.5f);
                context.AfterimageBoost = 1f;
                SoundEngine.PlaySound(SoundID.Item160 with { Volume = 0.5f, Pitch = 0.5f, MaxInstances = 3 }, npc.Center);
            }
            else if (flitT < GlideEnd) {
                //直线掠行：不转向，速度层全开
                context.AfterimageBoost = Math.Max(context.AfterimageBoost, 0.8f);
                context.WingFlapBoost = 1.6f;
                context.PrismShimmer = Math.Max(context.PrismShimmer, 0.5f);
                QueenMotion.FlightLean(npc, 0.045f, 0.5f);
                context.PoseCommand = 5;
            }
            else {
                //硬刹钉位
                QueenMotion.FlitBrake(npc, 0.74f);
                QueenMotion.FlightLean(npc);
                FaceTarget(npc, player.Center);
                context.PoseCommand = 5;
                context.WingFlapBoost = 0.8f;
            }

            return null;
        }

        /// <summary>二阶段选招(服务端)：投技→分裂召唤→手排压迫/场控交替环</summary>
        private IQueenSlimeState ChooseNextAttack(QueenSlimeStateContext context) {
            if (QueenCrystalPrisonWaltzState.CanTrigger(context)) {
                return new QueenCrystalPrisonWaltzState();
            }
            if (QueenGelSplitSummonState.NeedSummon(context)) {
                return new QueenGelSplitSummonState();
            }
            IQueenSlimeState[] cycle = [
                new QueenSkySpikeCascadeState(),
                new QueenCrystalDiveStompState(),
                new QueenWingGaleWaltzState(),
                new QueenSpikeRingState(),
                new QueenCrystalDiveStompState(),
                new QueenChandelierFallState(),
                new QueenPrismVolleyState(),
                new QueenCrystalDiveStompState(),
            ];
            IQueenSlimeState next = cycle[context.AttackPhaseIndex % cycle.Length];
            context.AttackPhaseIndex++;
            return next;
        }
    }
}
