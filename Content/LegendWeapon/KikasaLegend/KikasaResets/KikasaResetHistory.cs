using System;
using Terraria;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaResets
{
    /// <summary>
    /// 大范围重启的位置历史：客户端与服务器都无条件记录活跃 NPC 与玩家的位置环形缓冲。
    /// 服务器不模拟领域、无法按形态门控，纯位置拷贝的开销可忽略；
    /// 倒放期间由 <see cref="KikasaReset"/> 暂停记录（最新样本即触发帧），
    /// 倒放结束后旧轨迹作废、整表清空重新积累。
    /// </summary>
    internal static class KikasaResetHistory
    {
        /// <summary>采样间隔（帧）</summary>
        public const int SampleInterval = 3;

        /// <summary>每实体样本容量：覆盖 384 帧，大于倒放窗口 <see cref="KikasaReset.RewindWindowFrames"/></summary>
        public const int SampleCapacity = 128;

        private sealed class Track
        {
            public readonly Vector2[] Samples = new Vector2[SampleCapacity];
            public int Head = -1;
            public int Count;

            public void Reset() {
                Head = -1;
                Count = 0;
            }

            public void Push(Vector2 position) {
                Head = (Head + 1) % SampleCapacity;
                Samples[Head] = position;
                if (Count < SampleCapacity) {
                    Count++;
                }
            }

            /// <summary>从最新往回第 index 个样本（0=最新），越界钳到最老</summary>
            public Vector2 Peek(int index) {
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

        /// <summary>由 <see cref="KikasaResetSystem"/> 两端逐帧驱动</summary>
        internal static void Update() {
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
                    (npcTracks[i] ??= new Track()).Push(npc.position);
                }
            }
            for (int i = 0; i < Main.maxPlayers; i++) {
                Player player = Main.player[i];
                if (player?.active == true && !player.dead) {
                    (playerTracks[i] ??= new Track()).Push(player.position);
                }
            }
        }

        /// <summary>
        /// 演出起点的强制补采样：常规采样每 3 帧一次，最新样本可能落后触发时刻
        /// 0~2 帧，快速移动的实体会在定格瞬间向后弹一下；两端在 Active 置位时
        /// 各补一帧当下位置，让 age=0 恰好等于触发帧
        /// </summary>
        internal static void ForceSample() => PushAll();

        /// <summary>按"距最新样本多少帧"取 NPC 历史位置，样本间线性插值；无历史返回 false</summary>
        internal static bool TrySampleNpc(int index, float ageFrames, out Vector2 position)
            => TrySample(index >= 0 && index < npcTracks.Length
                ? npcTracks[index] : null, ageFrames, out position);

        /// <summary>按"距最新样本多少帧"取玩家历史位置</summary>
        internal static bool TrySamplePlayer(int who, float ageFrames, out Vector2 position)
            => TrySample(who >= 0 && who < playerTracks.Length
                ? playerTracks[who] : null, ageFrames, out position);

        private static bool TrySample(Track track, float ageFrames, out Vector2 position) {
            position = default;
            if (track == null || track.Count <= 0) {
                return false;
            }
            float f = MathF.Max(ageFrames, 0f) / SampleInterval;
            int nearer = (int)f;
            Vector2 a = track.Peek(nearer);
            Vector2 b = track.Peek(nearer + 1);
            position = Vector2.Lerp(a, b, MathHelper.Clamp(f - nearer, 0f, 1f));
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
        }
    }
}
