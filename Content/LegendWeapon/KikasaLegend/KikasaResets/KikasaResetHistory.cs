using System;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaResets
{
    /// <summary>
    /// 大范围重启的运动历史：客户端与服务器都无条件记录活跃 NPC（位置+姿态）
    /// 与玩家（位置）的环形缓冲。服务器不模拟领域、无法按形态门控，纯拷贝的开销可忽略；
    /// 倒放期间由 <see cref="KikasaReset"/> 暂停记录（最新样本即触发帧），
    /// 倒放结束后旧轨迹作废、整表清空重新积累。
    /// </summary>
    internal static class KikasaResetHistory
    {
        /// <summary>采样间隔（帧）</summary>
        public const int SampleInterval = 3;

        /// <summary>每实体样本容量：覆盖 768 帧，必须大于倒放窗口
        /// <see cref="KikasaReset.RewindWindowFrames"/>：容量不足时倒带后段
        /// 深度越过缓冲，所有实体会钉死在最老样本上不再后退</summary>
        public const int SampleCapacity = 256;

        /// <summary>单帧运动快照：位置之外连姿态一起倒放，才读得出"倒带"而非"拖拽"</summary>
        private struct Sample
        {
            public Vector2 Pos;
            public float Rot;
            public sbyte Dir;
            public sbyte SpriteDir;
        }

        private sealed class Track
        {
            public readonly Sample[] Samples = new Sample[SampleCapacity];
            public int Head = -1;
            public int Count;

            public void Reset() {
                Head = -1;
                Count = 0;
            }

            public void Push(in Sample sample) {
                Head = (Head + 1) % SampleCapacity;
                Samples[Head] = sample;
                if (Count < SampleCapacity) {
                    Count++;
                }
            }

            /// <summary>从最新往回第 index 个样本（0=最新），越界钳到最老</summary>
            public Sample Peek(int index) {
                index = Math.Clamp(index, 0, Count - 1);
                int slot = Head - index;
                if (slot < 0) {
                    slot += SampleCapacity;
                }
                return Samples[slot];
            }
        }

        private static readonly Track[] npcTracks = new Track[Main.maxNPCs];
        private static readonly Track[] playerTracks = new Track[Main.maxPlayers];
        //槽位重用检测：type 变了或从非活跃翻活跃，旧历史立即作废
        private static readonly int[] npcLastType = new int[Main.maxNPCs];
        private static readonly bool[] npcWasActive = new bool[Main.maxNPCs];
        private static readonly bool[] playerWasActive = new bool[Main.maxPlayers];

        //持有门：无人拥有鬼伞时整套采样（含槽位重用检测）停摆，两端按背包判定、结果一致。
        //开门沿清空全部轨迹重新积累——语义注记：鬼伞首次入包后约 13 秒内倒带深度不满，
        //属可接受行为（重启只能由持伞者主动触发）
        private static bool ownerGateOpen;
        private static uint nextOwnerPollFrame;

        private static bool AnyPlayerOwnsKikasa() {
            int type = ModContent.ItemType<KikasaItem>();
            foreach (Player player in Main.ActivePlayers) {
                Item[] inventory = player.inventory;
                for (int i = 0; i < inventory.Length; i++) {
                    Item item = inventory[i];
                    if (item != null && item.type == type && item.stack > 0) {
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>由 <see cref="KikasaResetSystem"/> 两端逐帧驱动</summary>
        internal static void Update() {
            //每 60 帧轮询一次持有门；倒放进行中不关门（HistoryPaused 期间维持原有路径）
            if (Main.GameUpdateCount >= nextOwnerPollFrame) {
                nextOwnerPollFrame = Main.GameUpdateCount + 60;
                bool owned = AnyPlayerOwnsKikasa();
                if (owned && !ownerGateOpen) {
                    Clear();//开门沿：停更期间的旧轨迹作废，重新积累
                }
                ownerGateOpen = owned;
            }
            if (!ownerGateOpen && !KikasaReset.HistoryPaused) {
                return;
            }

            //槽位重用每帧检测；3 帧采样间隙里死而复用的槽不能继承旧轨迹
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                bool active = npc?.active == true;
                if (active && (!npcWasActive[i] || npcLastType[i] != npc.type)) {
                    npcTracks[i]?.Reset();
                }
                npcWasActive[i] = active;
                npcLastType[i] = active ? npc.type : 0;
            }
            //玩家槽位同理：掉线后被新玩家占用的槽不得沿前任的旧轨迹倒放
            for (int i = 0; i < Main.maxPlayers; i++) {
                bool active = Main.player[i]?.active == true;
                if (active && !playerWasActive[i]) {
                    playerTracks[i]?.Reset();
                }
                playerWasActive[i] = active;
            }

            if (KikasaReset.HistoryPaused
                || Main.GameUpdateCount % (uint)SampleInterval != 0) {
                return;
            }
            PushAll();
        }

        private static void PushAll() {
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (npc?.active == true) {
                    (npcTracks[i] ??= new Track()).Push(new Sample {
                        Pos = npc.position,
                        Rot = float.IsFinite(npc.rotation) ? npc.rotation : 0f,
                        Dir = (sbyte)Math.Clamp(npc.direction, -1, 1),
                        SpriteDir = (sbyte)Math.Clamp(npc.spriteDirection, -1, 1),
                    });
                }
            }
            for (int i = 0; i < Main.maxPlayers; i++) {
                Player player = Main.player[i];
                if (player?.active == true && !player.dead) {
                    (playerTracks[i] ??= new Track()).Push(new Sample {
                        Pos = player.position,
                    });
                }
            }
        }

        /// <summary>
        /// 演出起点的强制补采样：常规采样每 3 帧一次，最新样本可能落后触发时刻
        /// 0~2 帧，快速移动的实体会在定格瞬间向后弹一下；两端在 Active 置位时
        /// 各补一帧当下位置，让 age=0 恰好等于触发帧
        /// </summary>
        internal static void ForceSample() => PushAll();

        /// <summary>
        /// 按"距最新样本多少帧"取 NPC 历史运动快照：位置线性、角度最短弧插值，
        /// 朝向取较近样本；无历史返回 false
        /// </summary>
        internal static bool TrySampleNpc(int index, float ageFrames, out Vector2 position,
            out float rotation, out int direction, out int spriteDirection)
            => TrySample(index >= 0 && index < npcTracks.Length
                ? npcTracks[index] : null, ageFrames,
                out position, out rotation, out direction, out spriteDirection);

        /// <summary>按"距最新样本多少帧"取玩家历史位置</summary>
        internal static bool TrySamplePlayer(int who, float ageFrames, out Vector2 position)
            => TrySample(who >= 0 && who < playerTracks.Length
                ? playerTracks[who] : null, ageFrames, out position, out _, out _, out _);

        /// <summary>
        /// 历史深度 ageFrames 当刻的 NPC 速度（像素/帧），由相邻样本差商还原；
        /// 深度越过缓冲时两样本同值、自然得零。落行时用它把"当年的动量"接回去
        /// </summary>
        internal static bool TryNpcVelocityAt(int index, float ageFrames, out Vector2 velocity) {
            velocity = Vector2.Zero;
            Track track = index >= 0 && index < npcTracks.Length ? npcTracks[index] : null;
            if (track == null || track.Count < 2) {
                return false;
            }
            int nearer = (int)(MathF.Max(ageFrames, 0f) / SampleInterval);
            Vector2 newer = track.Peek(nearer).Pos;
            Vector2 older = track.Peek(nearer + 1).Pos;
            velocity = (newer - older) / SampleInterval;
            return true;
        }

        private static bool TrySample(Track track, float ageFrames, out Vector2 position,
            out float rotation, out int direction, out int spriteDirection) {
            position = default;
            rotation = 0f;
            direction = 0;
            spriteDirection = 0;
            if (track == null || track.Count <= 0) {
                return false;
            }
            float f = MathF.Max(ageFrames, 0f) / SampleInterval;
            int nearer = (int)f;
            float t = MathHelper.Clamp(f - nearer, 0f, 1f);
            Sample a = track.Peek(nearer);
            Sample b = track.Peek(nearer + 1);
            position = Vector2.Lerp(a.Pos, b.Pos, t);
            //角度沿最短弧插值，跨 ±π 不打转
            rotation = a.Rot + MathHelper.WrapAngle(b.Rot - a.Rot) * t;
            Sample near = t < 0.5f ? a : b;
            direction = near.Dir;
            spriteDirection = near.SpriteDir;
            return true;
        }

        /// <summary>倒放收场后实体已跳回过去，旧轨迹作废</summary>
        internal static void Clear() {
            foreach (Track track in npcTracks) {
                track?.Reset();
            }
            foreach (Track track in playerTracks) {
                track?.Reset();
            }
            Array.Clear(npcWasActive);
            Array.Clear(npcLastType);
            Array.Clear(playerWasActive);
            //帧计回绕（换世界）时轮询点可能落到遥远未来，这里归零保证立即重判
            nextOwnerPollFrame = 0;
        }
    }
}
