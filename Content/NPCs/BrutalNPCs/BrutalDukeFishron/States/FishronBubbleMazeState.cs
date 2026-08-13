using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDukeFishron.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDukeFishron.Rendering;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDukeFishron.States
{
    /// <summary>
    /// 气泡迷宫：吐出成阵的驻停气泡封锁走位。
    /// 一阶段双帘留缺口，二阶段起围框走廊；风漂让走廊缓慢迁移
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)FishronStateIndex.BubbleMaze, typeof(FishronStateContext))]
    internal class FishronBubbleMazeState : FishronStateBase
    {
        public override string StateName => "BubbleMaze";
        public override FishronStateIndex StateIndex => FishronStateIndex.BubbleMaze;

        private const int SpitInterval = 5;
        private const int LingerTime = 46;
        /// <summary>全场气泡容量上限</summary>
        private const int BubbleCap = 46;
        /// <summary>气泡出膛速度</summary>
        private const float BubbleFlySpeed = 17f;

        //服务端专用：本轮的格点队列
        private readonly List<Vector2> slots = [];
        private int spitIndex;
        private int hoverSide;

        public FishronBubbleMazeState() {
        }

        public override void OnEnter(FishronStateContext context) {
            base.OnEnter(context);
            slots.Clear();
            spitIndex = 0;
            hoverSide = Math.Sign(context.Npc.Center.X - context.Target.Center.X);
            if (hoverSide == 0) {
                hoverSide = 1;
            }
            //服务端排布格点
            if (!VaultUtils.isClient) {
                BuildSlots(context);
            }
            SoundEngine.PlaySound(SoundID.Zombie20 with { Volume = 0.85f, Pitch = -0.1f, MaxInstances = 3 }, context.Npc.Center);
        }

        /// <summary>格点排布：一阶段双垂帘各留缺口；二阶段起矩形围框留两侧走廊</summary>
        private void BuildSlots(FishronStateContext context) {
            Player player = context.Target;
            Vector2 anchor = player.Center + player.velocity * 10f;

            if (context.Phase == 1) {
                //双垂帘：x = ±470，每帘 10 格，随机挖 2 连格缺口
                for (int side = -1; side <= 1; side += 2) {
                    int gapStart = Main.rand.Next(2, 7);
                    for (int i = 0; i < 10; i++) {
                        if (i == gapStart || i == gapStart + 1) {
                            continue;
                        }
                        slots.Add(anchor + new Vector2(side * 470f, -430f + i * 95f));
                    }
                }
            }
            else {
                //围框：四边各摆一排，随机开两侧走廊
                int corridorA = Main.rand.Next(4);
                int corridorB = (corridorA + Main.rand.Next(1, 4)) % 4;
                const float half = 560f;
                for (int edge = 0; edge < 4; edge++) {
                    bool corridor = edge == corridorA || edge == corridorB;
                    int count = 7;
                    int gapStart = corridor ? Main.rand.Next(1, count - 2) : -10;
                    for (int i = 0; i < count; i++) {
                        if (corridor && (i == gapStart || i == gapStart + 1)) {
                            continue;
                        }
                        float t = -half + i * (half * 2f / (count - 1));
                        Vector2 pos = edge switch {
                            0 => anchor + new Vector2(t, -half),
                            1 => anchor + new Vector2(half, t),
                            2 => anchor + new Vector2(t, half),
                            _ => anchor + new Vector2(-half, t),
                        };
                        slots.Add(pos);
                    }
                }
            }

            //按到嘴距离排序，吐泡顺序有扇面推进感
            Vector2 mouth = context.Npc.Center;
            slots.Sort((a, b) => Vector2.DistanceSquared(a, mouth).CompareTo(Vector2.DistanceSquared(b, mouth)));
        }

        public override IFishronState OnUpdate(FishronStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;

            //侧位悬停，吐泡的后坐让它轻轻退
            Vector2 hoverGoal = player.Center + new Vector2(hoverSide * 420f, -140f);
            SetMovement(context, hoverGoal, 7.5f, 0.42f);

            Timer++;

            //吐泡节拍（服务端逐格点火）
            bool stillSpitting = false;
            if (!VaultUtils.isClient && spitIndex < slots.Count) {
                stillSpitting = true;
                if (Timer % SpitInterval == 0) {
                    int burst = context.Phase >= 2 ? 3 : 2;
                    for (int k = 0; k < burst && spitIndex < slots.Count; k++) {
                        SpitBubble(npc, slots[spitIndex]);
                        spitIndex++;
                    }
                    npc.velocity -= (slots[Math.Max(spitIndex - 1, 0)] - npc.Center)
                        .SafeNormalize(Vector2.Zero) * 2.6f;
                    npc.netUpdate = true;
                }
            }

            //吐息期视觉（各端按计时对齐节拍）
            int spitWindow = (context.Phase == 1 ? 16 : 22) * SpitInterval / 2 + 30;
            if (Timer < spitWindow) {
                context.FrameCommand = 1;
                context.SetChargeState(2, MathHelper.Clamp(Timer / (float)spitWindow, 0f, 1f));
                if (!VaultUtils.isServer && Timer % SpitInterval == 0) {
                    SoundEngine.PlaySound(SoundID.NPCDeath19 with { Volume = 0.5f, Pitch = 0.2f, MaxInstances = 5 }, npc.Center);
                    FishronMotionFX.SpawnSprayCone(npc.Center + DirectionToTarget(context) * 40f,
                        DirectionToTarget(context), 2, 3f, 7f, 0.5f, 0.8f);
                }
            }

            //吐完并驻留片刻后离场
            if (Timer >= spitWindow + LingerTime && !stillSpitting) {
                if (!VaultUtils.isClient) {
                    return new FishronHoverState();
                }
                //客户端等待服务端切换
                if (Timer >= spitWindow + LingerTime + 60) {
                    return new FishronHoverState();
                }
            }

            return null;
        }

        /// <summary>朝格点吐出一枚迷宫气泡（模式1），抵达后驻停</summary>
        private static void SpitBubble(NPC npc, Vector2 slot) {
            if (CountBubbles() >= BubbleCap) {
                return;
            }
            Vector2 mouth = npc.Center + (slot - npc.Center).SafeNormalize(Vector2.UnitX) * (npc.width * 0.4f);
            int idx = NPC.NewNPC(npc.GetSource_FromAI(), (int)mouth.X, (int)mouth.Y, NPCID.DetonatingBubble);
            if (idx < 0 || idx >= Main.maxNPCs) {
                return;
            }
            NPC bubble = Main.npc[idx];
            float travelFrames = Math.Max(Vector2.Distance(mouth, slot) / BubbleFlySpeed, 4f);
            bubble.ai[0] = 1f;
            bubble.ai[1] = travelFrames;
            bubble.velocity = (slot - mouth).SafeNormalize(Vector2.UnitY) * BubbleFlySpeed;
            bubble.netUpdate = true;
        }

        /// <summary>全场气泡计数（RingSpin 共用）</summary>
        internal static int CountBubbles() {
            int count = 0;
            foreach (var n in Main.ActiveNPCs) {
                if (n.type == NPCID.DetonatingBubble) {
                    count++;
                }
            }
            return count;
        }
    }
}
