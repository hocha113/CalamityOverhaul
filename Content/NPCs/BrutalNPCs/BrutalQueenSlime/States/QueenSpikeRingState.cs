using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalQueenSlime.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalQueenSlime.Projectiles;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalQueenSlime.States
{
    /// <summary>
    /// 尖刺环阵(二阶段场控)：五芒棱晶节点环围场，节点按跑马灯顺序轮射瞄准尖刺扇。
    /// 击碎节点=该拍永久静默(公平阀，继承牢笼的"破坏开生路")；皇后沿环外掠影游走助攻。
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)QueenSlimeStateIndex.SpikeRing, typeof(QueenSlimeStateContext))]
    internal class QueenSpikeRingState : QueenSlimeStateBase
    {
        public override string StateName => "SpikeRing";
        public override QueenSlimeStateIndex StateIndex => QueenSlimeStateIndex.SpikeRing;

        #region 节奏与公平常量
        private const int NodeCount = 5;
        private const float RingRadius = 440f;
        private const int MaterializeTime = 45;
        private const int VolleyTime = 260;
        private const int TailTime = 34;
        private const int TotalTime = MaterializeTime + VolleyTime + TailTime;
        /// <summary>跑马灯轮射拍(节点依次开火，永不齐射——公平阀)</summary>
        private int VolleyGap(QueenSlimeStateContext ctx) => ctx.IsDeathMode ? 21 : 26;
        /// <summary>皇后自补扇周期</summary>
        private const int QueenFanPeriod = 88;
        #endregion

        /// <summary>环心(服务端捕获；客户端由节点均值重建)</summary>
        private Vector2 ringCenter;
        private bool centerCaptured;

        public QueenSpikeRingState() {
        }

        public override void OnEnter(QueenSlimeStateContext context) {
            base.OnEnter(context);
            NPC npc = context.Npc;
            DisableContactDamage(npc);
            npc.noGravity = true;
            npc.noTileCollide = true;
            centerCaptured = false;
        }

        public override IQueenSlimeState OnUpdate(QueenSlimeStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;

            Timer++;
            DisableContactDamage(npc);

            //环心：服务端首帧捕获并布节点；客户端由节点均值重建
            if (!centerCaptured) {
                if (!VaultUtils.isClient) {
                    ringCenter = player.Center;
                    centerCaptured = true;
                    SpawnRing(context);
                }
                else {
                    List<NPC> found = context.CollectPrismNodes();
                    if (found.Count >= 3) {
                        Vector2 sum = Vector2.Zero;
                        foreach (var n in found) {
                            sum += n.Center;
                        }
                        ringCenter = sum / found.Count;
                        centerCaptured = true;
                    }
                    else {
                        ringCenter = player.Center;
                    }
                }
            }

            UpdateQueenOrbit(context);

            //物化段
            if (Timer <= MaterializeTime) {
                float p = Timer / (float)MaterializeTime;
                context.SetChargeState(1, p);
                if (Timer == 6) {
                    SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.85f, Pitch = -0.1f }, ringCenter);
                }
                return null;
            }

            int volleyT = (int)Timer - MaterializeTime;

            //轮射段(服务端)：跑马灯槽位依次开扇，被毁槽位静默
            if (volleyT < VolleyTime && volleyT % VolleyGap(context) == 0 && !VaultUtils.isClient) {
                FireMarqueeVolley(context, volleyT / VolleyGap(context));
            }

            //皇后自补扇(服务端)
            if (volleyT < VolleyTime && volleyT % QueenFanPeriod == QueenFanPeriod / 2 && !VaultUtils.isClient) {
                QueenMotion.SpawnSpikeFan(npc, npc.Center, player.Center, 3, 0.2f, 8.4f,
                    QueenCrystalSpikeProj.SpikeDamage, 0.62f);
            }
            context.PrismShimmer = Math.Max(context.PrismShimmer, 0.4f);

            //收环：碎节点退场(无害演出)
            if (volleyT == VolleyTime && !VaultUtils.isClient) {
                foreach (var n in context.CollectPrismNodes()) {
                    QueenMotion.ScriptKill(n);
                }
            }

            if (Timer >= TotalTime && !VaultUtils.isClient) {
                return new QueenAerialBalletState();
            }
            return null;
        }

        /// <summary>皇后环外掠影游走：轨道角随时间推进，弹簧快追+侧倾</summary>
        private void UpdateQueenOrbit(QueenSlimeStateContext context) {
            NPC npc = context.Npc;
            float orbitAngle = -MathHelper.PiOver2 + (float)Timer * 0.017f;
            //轨道半径带呼吸，读作滑步而非匀速圆规
            float r = RingRadius + 190f + (float)Math.Sin(Timer * 0.05f) * 60f;
            Vector2 anchor = ringCenter + orbitAngle.ToRotationVector2() * r;
            QueenMotion.SpringHover(npc, anchor, 0.02f, 0.1f, 24f);
            QueenMotion.FlightLean(npc);
            context.PoseCommand = 5;
            context.WingFlapBoost = MathHelper.Clamp(npc.velocity.Length() / 15f, 0.3f, 1.2f);
            FaceTarget(npc, context.Target.Center);
        }

        /// <summary>布环(服务端)：清残留节点后按五芒布位，寿命盖过全程</summary>
        private void SpawnRing(QueenSlimeStateContext context) {
            foreach (var stale in context.CollectPrismNodes()) {
                QueenMotion.ScriptKill(stale);
            }
            for (int i = 0; i < NodeCount; i++) {
                float angle = MathHelper.TwoPi * i / NodeCount - MathHelper.PiOver2;
                Vector2 pos = ringCenter + angle.ToRotationVector2() * RingRadius;
                NPC node = QueenMotion.SpawnMinion(context.Npc, NPCID.QueenSlimeMinionBlue,
                    QueenMinionRole.PrismNode, i, pos, QueenSlimeMinionAI.PrismNodeLife());
                if (node != null) {
                    node.ai[3] = TotalTime + 20;
                    node.netUpdate = true;
                }
            }
        }

        /// <summary>跑马灯轮射(服务端)：本拍槽位的节点若已被击碎则整拍静默——缺口永久生效</summary>
        private void FireMarqueeVolley(QueenSlimeStateContext context, int volleyIndex) {
            int slot = volleyIndex % NodeCount;
            foreach (var n in context.CollectPrismNodes()) {
                if ((int)n.ai[1] != slot) {
                    continue;
                }
                int count = context.IsDeathMode ? 6 : 5;
                QueenMotion.SpawnSpikeFan(n, n.Center, context.Target.Center, count, 0.3f, 8.6f,
                    QueenCrystalSpikeProj.SpikeDamage, slot * 0.2f);
                return;
            }
        }

        public override void OnExit(QueenSlimeStateContext context) {
            base.OnExit(context);
            DisableContactDamage(context.Npc);
        }
    }
}
