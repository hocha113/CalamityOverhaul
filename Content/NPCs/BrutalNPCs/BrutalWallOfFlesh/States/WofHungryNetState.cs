using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalWallOfFlesh.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalWallOfFlesh.Rendering;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalWallOfFlesh.States
{
    /// <summary>
    /// 饥饿者系绳网：饥饿者飞到墙前编成垂直拦截网，肉链只在成对节点间通电，
    /// 对与对之间是可穿越的窗口。网随墙推进——穿窗而过，或击杀节点撕开永久缺口
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)WofStateIndex.HungryNet, typeof(WofStateContext))]
    internal class WofHungryNetState : WofStateBase
    {
        public override string StateName => "HungryNet";
        public override WofStateIndex StateIndex => WofStateIndex.HungryNet;

        private const int Outro = 30;

        /// <summary>网节点上限(留缝隙的公平阀)</summary>
        internal static int MaxNetNodes(int phase) => phase >= 3 ? 7 : 6;

        /// <summary>网当前是否已通电(编织完成)</summary>
        internal static bool NetArmed(NPC wall) {
            return WallOfFleshAI.GetStateIndex(wall) == WofStateIndex.HungryNet
                && wall.ai[3] >= WofDirector.NetWeaveFrames;
        }

        public override void OnEnter(WofStateContext context) {
            base.OnEnter(context);
            //节点不足则先补员(服务端)
            if (!VaultUtils.isClient) {
                context.Npc.ai[3] = 0f;
                List<NPC> hungries = context.CollectHungries();
                if (hungries.Count < WofDirector.NetMinHungries) {
                    WofPhaseTransitionState.RespawnHungries(context);
                }
            }
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.NPCHit18 with { Pitch = -0.3f, Volume = 1f }, context.Npc.Center);
            }
        }

        public override IWofState OnUpdate(WofStateContext context) {
            NPC npc = context.Npc;
            Timer++;
            //编织进度写ai[3](服务端权威，随NPC同步；饥饿者读它判断编织/通电)
            if (!VaultUtils.isClient) {
                npc.ai[3] = Timer;
            }

            int weaveEnd = WofDirector.NetWeaveFrames;
            int holdEnd = weaveEnd + WofDirector.NetHoldFrames;
            int totalEnd = holdEnd + Outro;

            if (Timer <= weaveEnd) {
                //编织期：墙放缓，饥饿者入位；节点不足则放弃结网(公平阀，不空转7秒)
                if (!VaultUtils.isClient && Timer == 6 && context.CollectHungries().Count < 2) {
                    return new WofAdvanceState();
                }
                float p = Timer / (float)weaveEnd;
                context.AdvanceFactor = 0.5f;
                context.WallFlush = 0.4f + 0.2f * p;
                context.MouthCommand = 2;
                if (Timer == weaveEnd - 12 && !VaultUtils.isServer) {
                    //通电预告：绷紧的湿响
                    SoundEngine.PlaySound(SoundID.Item171 with { Pitch = -0.5f, Volume = 0.9f }, npc.Center);
                }
                return null;
            }

            if (Timer <= holdEnd) {
                //推网期：网在前墙在后，双重死线挤压
                context.AdvanceFactor = 0.85f;
                context.WallFlush = 0.55f;
                UpdateLinkDamage(context);
                //通电首帧
                if (Timer == weaveEnd + 1 && !VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.NPCDeath13 with { Pitch = -0.4f, Volume = 1f }, npc.Center);
                    WofMotionFX.CameraPunch(npc.Center, 3f, 10, "WofNetArm");
                }
                return null;
            }

            //收网：链条散解，饥饿者回鞭
            context.AdvanceFactor = 0.8f;
            if (Timer == holdEnd + 1 && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.NPCHit18 with { Pitch = 0.1f, Volume = 0.9f }, npc.Center);
                //链条崩解成血雾(与判定同源的成对枚举)
                List<Vector2> nodes = CollectNetNodePositions(context.Npc, context.Phase);
                for (int i = 0; i + 1 < nodes.Count; i += 2) {
                    Vector2 mid = (nodes[i] + nodes[i + 1]) * 0.5f;
                    WofMotionFX.SpawnBloodBurst(mid, 0.6f);
                }
            }
            if (Timer >= totalEnd) {
                return new WofAdvanceState();
            }
            return null;
        }

        /// <summary>
        /// 链条伤害：只在成对节点(0-1、2-3...)间拉链，对与对之间留出可穿越的窗口——
        /// 网是筛子不是墙。判定为本地玩家自伤模型
        /// </summary>
        private void UpdateLinkDamage(WofStateContext context) {
            if (Main.dedServ) {
                return;
            }
            NPC npc = context.Npc;
            List<Vector2> nodes = CollectNetNodePositions(npc, context.Phase);
            if (nodes.Count < 2) {
                return;
            }
            int damage = WallOfFleshAI.ScaleDamage(npc, WofDirector.NetLinkDamage);
            PlayerDeathReason reason = PlayerDeathReason.ByCustomReason(
                WallOfFleshAI.NetDeathReason.Format(Main.LocalPlayer.name));
            for (int i = 0; i + 1 < nodes.Count; i += 2) {
                if (WofWallField.HurtLocalPlayerNearSegment(nodes[i], nodes[i + 1], 15f, damage, reason)) {
                    break;
                }
            }
        }

        /// <summary>
        /// 收集网节点位置：按 whoAmI 升序取前N只活跃饥饿者。
        /// 槽位本就按 whoAmI 秩铺开，天然自上而下有序且逐帧稳定(不按坐标重排，避免呼吸期换对)
        /// </summary>
        internal static List<Vector2> CollectNetNodePositions(NPC wall, int phase) {
            List<NPC> members = [];
            foreach (var n in Main.ActiveNPCs) {
                if (n.type == NPCID.TheHungry) {
                    members.Add(n);
                }
            }
            members.Sort((a, b) => a.whoAmI.CompareTo(b.whoAmI));
            int cap = MaxNetNodes(phase);
            if (members.Count > cap) {
                members.RemoveRange(cap, members.Count - cap);
            }
            List<Vector2> nodes = [];
            foreach (NPC member in members) {
                nodes.Add(member.Center);
            }
            return nodes;
        }

        /// <summary>
        /// 计算某只饥饿者的网槽位；返回是否参与结网。
        /// rank 按 whoAmI 升序，槽位沿墙域高度均分，带呼吸波动
        /// </summary>
        internal static bool TryGetNetSlot(NPC wall, NPC hungry, int phase, out Vector2 slotPos) {
            slotPos = default;
            List<NPC> members = [];
            foreach (var n in Main.ActiveNPCs) {
                if (n.type == NPCID.TheHungry) {
                    members.Add(n);
                }
            }
            members.Sort((a, b) => a.whoAmI.CompareTo(b.whoAmI));
            int rank = members.IndexOf(hungry);
            int cap = MaxNetNodes(phase);
            if (rank < 0 || rank >= cap) {
                return false;
            }
            int total = System.Math.Min(members.Count, cap);

            float faceX = WofWallField.WallFaceX(wall);
            float slotX = faceX + wall.direction * WofDirector.NetForwardOffset;
            float yFrac = (rank + 1) / (float)(total + 1);
            float slotY = MathHelper.Lerp(WofWallField.Top + 40f, WofWallField.Bottom - 40f, yFrac);
            //呼吸波动：网面缓缓起伏，可读且有机
            slotY += (float)System.Math.Sin(Main.GameUpdateCount * 0.03f + rank * 1.7f) * 26f;
            slotPos = new Vector2(slotX, slotY);
            return true;
        }
    }
}
