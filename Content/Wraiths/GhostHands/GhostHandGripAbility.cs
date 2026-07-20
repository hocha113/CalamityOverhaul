using CalamityOverhaul.Content.PRTTypes;
using CalamityOverhaul.Content.Wraiths.Core;
using InnoVault.PRT;
using Microsoft.Xna.Framework;
using System;
using System.IO;
using Terraria;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.Wraiths.GhostHands
{
    /// <summary>
    /// 赋力「攥」（§4）：幽手自目标身后探出攥住。非 boss 完全定身、boss 迟滞，
    /// 时长吃驾驭度（依鬼律 12）。戒律「手不空回」：施放瞬间光标 120px 内无可慑目标
    /// 仍强行借力=犯戒（依鬼律 13，目标在 telegraph 期死亡不算——五指合上的是残骸）。
    /// 定义级单例无状态；冷却/代价由 <c>WraithPlayer</c> 管线统一结算
    /// </summary>
    internal sealed class GhostHandGripAbility : WraithAbility
    {
        /// <summary>光标射程</summary>
        private const float CastRange = 780f;
        /// <summary>目标搜索半径（Cast 与 ExecuteWorld 同源判定）</summary>
        private const float TargetRadius = 120f;
        /// <summary>telegraph 全长：18t 焦雾汇聚（预备拍）+ 6t 急合攥握（打击拍）</summary>
        internal const int TelegraphTicks = 24;

        public override int CooldownTicks => 1080;
        public override float ErosionCost => 0.09f;
        public override float MasteryWear => 0.012f;
        public override float TabooPenalty => 0.05f;

        /// <summary>非 boss 定身时长（tick）：0.22 出厂位≈1.8s，认主 0.85 位≈3.4s</summary>
        internal static int FreezeTicks(float mastery) => (int)(72f + 156f * mastery);

        /// <summary>boss 迟滞时长（tick）：每帧 velocity ×0.72，不冻 AI</summary>
        internal static int SlowTicks(float mastery) => (int)(48f + 72f * mastery);

        /// <summary>光标半径内最近可慑目标（owner 判定与服务器复解析唯一同源）</summary>
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
                //超射程:未施放,零代价
                if (GhostHand.GraspTooFar != null) {
                    CombatText.NewText(ctx.Player.Hitbox, Color.DarkGray, GhostHand.GraspTooFar.Value);
                }
                return WraithCastResult.Fail;
            }
            if (FindTarget(ctx.AimWorld) != null) {
                return WraithCastResult.Success;
            }
            //手不空回:照常施放(幽手攥空的演出)但记犯戒
            if (GhostHand.TabooEcho != null) {
                CombatText.NewText(ctx.Player.Hitbox, new Color(190, 60, 70), GhostHand.TabooEcho.Value);
            }
            return WraithCastResult.Taboo;
        }

        public override void ExecuteWorld(Player caster, Vector2 aim, float mastery) {
            //服务器经 aim 复解析目标,不传实体引用;telegraph 期死亡=五指合上残骸,无事发生
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

            //telegraph:焦雾向目标身后汇聚 + 裂纹微光
            for (int i = 0; i < 6; i++) {
                Vector2 from = grasp + Main.rand.NextVector2CircularEdge(48f, 40f);
                PRTLoader.NewParticle<PRT_Smoke>(from, (grasp - from) * 0.055f,
                    GhostHandDrawHelper.Charcoal * 0.65f, Main.rand.NextFloat(0.14f, 0.22f))
                    ?.Configure(Main.rand.Next(18, 26), 0.5f);
            }
            //幽手本体:自带 18t 汇聚+6t 急合+6t 消散的时间线,攥握帧的 Grab 音与余烬迸散在 PRT 内落拍
            PRTLoader.NewParticle<PRT_GhostGrasp>(grasp, Vector2.Zero, GhostHandDrawHelper.Charcoal, 1f)
                ?.Configure(facing, target?.whoAmI ?? -1);

            //boss 迟滞版:多一环脉冲与轻震屏
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
    /// 「攥」的效果宿主：telegraph 计时与定身/迟滞逐帧执行。字段挂 NPC 实例（InstancePerEntity）。
    /// MP 双轨同步：客户端本就逐帧模拟 NPC AI，三计时字段经 <see cref="SendExtraAI"/> 随
    /// SyncNPC 下发后客户端 <see cref="PreAI"/> 同样冻结/迟滞并自走计时（零漂移）；
    /// 服务器在效果活跃期每 10t 置 <c>netUpdate</c> 作下发载体，起始/释放帧额外各一拍
    /// </summary>
    internal sealed class GhostGripGlobalNPC : GlobalNPC
    {
        /// <summary>活跃期周期同步间隔（帧），SendExtraAI 的下发载体</summary>
        private const int SyncCarrierInterval = 10;

        public override bool InstancePerEntity => true;

        private int telegraphLeft;
        private int freezeLeft;
        private int slowLeft;
        private float pendingMastery;
        private bool pendingSlow;

        /// <summary>攥握生效中（非 boss 定身段）</summary>
        public bool Frozen => freezeLeft > 0;

        private bool AnyActive => telegraphLeft > 0 || freezeLeft > 0 || slowLeft > 0;

        internal void BeginTelegraph(int ticks, float mastery, bool slowVariant) {
            telegraphLeft = ticks;
            pendingMastery = mastery;
            pendingSlow = slowVariant;
        }

        public override bool PreAI(NPC npc) {
            //周期载体:活跃期把状态按 10t 节拍推给客户端,包间空档由客户端自走计时补齐
            if (VaultUtils.isServer && AnyActive && Main.GameUpdateCount % SyncCarrierInterval == 0) {
                npc.netUpdate = true;
            }
            if (telegraphLeft > 0 && --telegraphLeft == 0) {
                //效果自攥握帧起算
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
                    //释放帧再同步一次,客户端立刻拿到复动后的权威状态
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
            //空闲位打头:绝大多数 NPC 同步只花 1 bit;空闲帧照发,客户端读到即自清残留
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
                //权威已清账:本端残留计时一并归零(漏包自愈)
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
