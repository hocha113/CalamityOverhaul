using CalamityOverhaul.Content.NPCs.SeaShrimp.Kinematics;
using Terraria;

namespace CalamityOverhaul.Content.NPCs.SeaShrimp.Rendering
{
    /// <summary>
    /// 骨架位姿残影环:爆发段(尾弹/出拳)每 2 帧快照主剪影部件,
    /// 渲染层以深渊水色暗影重绘——残影与本体同素材(契约5)。
    /// 纯本地表现量,断档超过 20 帧自动清环,防上一次爆发的陈旧位姿闪现
    /// </summary>
    internal class ShrimpPoseTrail
    {
        public const int Slots = 4;

        internal sealed class Snapshot
        {
            public readonly Vector2[] NodePos = new Vector2[5];
            public readonly float[] NodeDir = new float[5];
            public float TailFlare;
            public readonly Vector2[] Shoulder = new Vector2[2];
            public readonly Vector2[] Elbow = new Vector2[2];
            public readonly Vector2[] Wrist = new Vector2[2];
            public readonly float[] UpperRot = new float[2];
            public readonly float[] ForeRot = new float[2];
            public readonly float[] ClawRot = new float[2];
            public bool Valid;
        }

        private readonly Snapshot[] ring = [new(), new(), new(), new()];
        /// <summary>最新快照槽位</summary>
        private int head;
        private uint lastCaptureTick;

        /// <summary>按新旧取快照:age 0=最新 … Slots-1=最旧;无效返回 null</summary>
        public Snapshot Get(int age) {
            Snapshot snap = ring[(head - age % Slots + Slots) % Slots];
            return snap.Valid ? snap : null;
        }

        /// <summary>清环:全部失效</summary>
        public void Clear() {
            foreach (Snapshot snap in ring) {
                snap.Valid = false;
            }
        }

        /// <summary>爆发段每 2 帧捕获一次;断档自动清环</summary>
        public void Capture(ShrimpSkeleton sk, float strength) {
            if (strength <= 0.05f) {
                return;
            }
            if (Main.GameUpdateCount - lastCaptureTick > 20u) {
                Clear();
            }
            if (Main.GameUpdateCount - lastCaptureTick < 2u) {
                return;
            }
            lastCaptureTick = Main.GameUpdateCount;

            head = (head + 1) % Slots;
            Snapshot snap = ring[head];
            for (int i = 0; i < 5; i++) {
                snap.NodePos[i] = sk.Nodes[i].Pos;
                snap.NodeDir[i] = sk.Nodes[i].Dir;
            }
            snap.TailFlare = sk.TailFlare;
            for (int a = 0; a < 2; a++) {
                TwoBoneSolve solve = sk.ArmSolves[a];
                snap.Shoulder[a] = solve.Shoulder;
                snap.Elbow[a] = solve.Elbow;
                snap.Wrist[a] = solve.Wrist;
                snap.UpperRot[a] = solve.UpperDir.ToRotation();
                snap.ForeRot[a] = solve.ForeDir.ToRotation();
                snap.ClawRot[a] = sk.ClawRot[a];
            }
            snap.Valid = true;
        }
    }
}
