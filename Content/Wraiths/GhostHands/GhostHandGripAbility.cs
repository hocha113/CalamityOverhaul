using CalamityOverhaul.Content.PRTTypes;
using CalamityOverhaul.Content.Wraiths.Core;
using InnoVault.PRT;
using System;
using System.IO;
using Terraria;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.Wraiths.GhostHands
{
    /// <summary>
    /// 赋力「攥」。非 boss 定身、boss 迟滞；光标 120px 空放=犯戒
    /// </summary>
    internal sealed class GhostHandGripAbility : WraithAbility
    {
        /// <summary>光标射程</summary>
        private const float CastRange = 780f;
        /// <summary>目标搜索半径</summary>
        private const float TargetRadius = 120f;
        /// <summary>telegraph 全长 24t</summary>
        internal const int TelegraphTicks = 24;

        public override int CooldownTicks => 1080;
        public override float ErosionCost => 0.09f;
        public override float MasteryWear => 0.012f;
        public override float TabooPenalty => 0.05f;

        /// <summary>非 boss 定身 tick</summary>
        internal static int FreezeTicks(float mastery) => (int)(72f + 156f * mastery);

        /// <summary>boss 迟滞 tick</summary>
        internal static int SlowTicks(float mastery) => (int)(48f + 72f * mastery);

        /// <summary>光标半径内最近可慑目标</summary>
        private static NPC FindTarget(Vector2 aim) {
            NPC best = null;
            float bestSq = TargetRadius * TargetRadius;
            foreach (NPC npc in Main.ActiveNPCs) {
                if (!npc.CanBeChasedBy()) {
                    continue;
                }
                float distSq = Vector2.DistanceSquared(npc.Center, aim);
                if (distSq < bestSq) {
                    bestSq = distSq;
                    best = npc;
                }
            }
            return best;
        }

        public override WraithCastResult Cast(WraithAbilityContext ctx) {
            if (Vector2.Distance(ctx.AimWorld, ctx.Player.Center) > CastRange) {
                //超射程，零代价
                if (GhostHand.GraspTooFar != null) {
                    CombatText.NewText(ctx.Player.Hitbox, Color.DarkGray, GhostHand.GraspTooFar.Value);
                }
                return WraithCastResult.Fail;
            }
            if (FindTarget(ctx.AimWorld) != null) {
                return WraithCastResult.Success;
            }
            //空放犯戒，仍播攥空
            if (GhostHand.TabooEcho != null) {
                CombatText.NewText(ctx.Player.Hitbox, new Color(190, 60, 70), GhostHand.TabooEcho.Value);
            }
            return WraithCastResult.Taboo;
        }

        public override void ExecuteWorld(Player caster, Vector2 aim, float mastery) {
            //aim 复解析，telegraph 期死亡无事
            NPC target = FindTarget(aim);
            target?.GetGlobalNPC<GhostGripGlobalNPC>().BeginTelegraph(TelegraphTicks, mastery, target.boss);
        }

        public override void PlayWorldFx(Player caster, Vector2 aim) {
            NPC target = FindTarget(aim);
            Vector2 grasp = target != null
                ? target.Center - new Vector2(target.direction * (target.width * 0.5f + 14f), 0f)
                : aim;
            int facing = target != null ? target.direction : Math.Sign(aim.X - caster.Center.X);
            if (facing == 0) {
                facing = 1;
            }

            //telegraph 焦雾
            for (int i = 0; i < 6; i++) {
                Vector2 from = grasp + Main.rand.NextVector2CircularEdge(48f, 40f);
                PRTLoader.NewParticle<PRT_Smoke>(from, (grasp - from) * 0.055f,
                    GhostHandDrawHelper.Charcoal * 0.65f, Main.rand.NextFloat(0.14f, 0.22f))
                    ?.Configure(Main.rand.Next(18, 26), 0.5f);
            }
            //幽手 PRT
            PRTLoader.NewParticle<PRT_GhostGrasp>(grasp, Vector2.Zero, GhostHandDrawHelper.Charcoal, 1f)
                ?.Configure(facing, target?.whoAmI ?? -1);

            //boss 迟滞额外脉冲
            if (target?.boss == true) {
                PRTLoader.NewParticle<PRT_StarPulseRing>(grasp, Vector2.Zero, GhostHandDrawHelper.Ember, 0.1f)
                    ?.Configure(0.12f, 1.1f, 30);
                if (Main.LocalPlayer.active && Vector2.DistanceSquared(Main.LocalPlayer.Center, grasp) < 1100f * 1100f) {
                    Main.LocalPlayer.CWR()?.GetScreenShake(2f);
                }
            }
        }
    }

    /// <summary>
    /// 「攥」效果宿主。SendExtraAI 同步计时；服每 10t netUpdate
    /// </summary>
    internal sealed class GhostGripGlobalNPC : GlobalNPC
    {
        /// <summary>活跃期同步间隔帧</summary>
        private const int SyncCarrierInterval = 10;

        public override bool InstancePerEntity => true;

        private int telegraphLeft;
        private int freezeLeft;
        private int slowLeft;
        private float pendingMastery;
        private bool pendingSlow;

        /// <summary>攥握生效中</summary>
        public bool Frozen => freezeLeft > 0;

        private bool AnyActive => telegraphLeft > 0 || freezeLeft > 0 || slowLeft > 0;

        internal void BeginTelegraph(int ticks, float mastery, bool slowVariant) {
            telegraphLeft = ticks;
            pendingMastery = mastery;
            pendingSlow = slowVariant;
        }

        public override bool PreAI(NPC npc) {
            //活跃期 10t 推状态
            if (VaultUtils.isServer && AnyActive && Main.GameUpdateCount % SyncCarrierInterval == 0) {
                npc.netUpdate = true;
            }
            if (telegraphLeft > 0 && --telegraphLeft == 0) {
                //自攥握帧起算
                if (pendingSlow) {
                    slowLeft = GhostHandGripAbility.SlowTicks(pendingMastery);
                }
                else {
                    freezeLeft = GhostHandGripAbility.FreezeTicks(pendingMastery);
                }
                npc.netUpdate = true;
            }
            if (freezeLeft > 0) {
                if (--freezeLeft == 0) {
                    //释放帧再同步
                    npc.netUpdate = true;
                }
                npc.velocity = Vector2.Zero;
                return false;
            }
            return true;
        }

        public override void PostAI(NPC npc) {
            if (slowLeft > 0) {
                slowLeft--;
                npc.velocity *= 0.72f;
            }
        }

        public override void SendExtraAI(NPC npc, BitWriter bitWriter, BinaryWriter binaryWriter) {
            //空闲位打头自清
            bool any = AnyActive;
            bitWriter.WriteBit(any);
            if (!any) {
                return;
            }
            bitWriter.WriteBit(pendingSlow);
            binaryWriter.Write((short)telegraphLeft);
            binaryWriter.Write((short)freezeLeft);
            binaryWriter.Write((short)slowLeft);
            binaryWriter.Write(pendingMastery);
        }

        public override void ReceiveExtraAI(NPC npc, BitReader bitReader, BinaryReader binaryReader) {
            if (!bitReader.ReadBit()) {
                //权威清账则本端归零
                telegraphLeft = 0;
                freezeLeft = 0;
                slowLeft = 0;
                return;
            }
            pendingSlow = bitReader.ReadBit();
            telegraphLeft = binaryReader.ReadInt16();
            freezeLeft = binaryReader.ReadInt16();
            slowLeft = binaryReader.ReadInt16();
            pendingMastery = binaryReader.ReadSingle();
        }
    }
}
