using Terraria;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalBrainOfCthulhu.Core
{
    /// <summary>
    /// 飞眼编队通道：脑状态机每帧写入（各端本地由同步输入推演，结果一致），飞眼 AI 读取
    /// FrameStamp 过期即回退闲逛，脑消失自动失效
    /// </summary>
    internal static class BrainFormationChannel
    {
        public const int ModeNone = 0;
        public const int ModeCage = 1;
        public const int ModeLance = 2;

        public static int Mode { get; private set; }
        /// <summary>牢笼中心</summary>
        public static Vector2 CageCenter { get; private set; }
        /// <summary>牢笼当前半径</summary>
        public static float CageRadius { get; private set; }
        /// <summary>环旋转相位</summary>
        public static float SpinPhase { get; private set; }
        /// <summary>逃生缺口方向（弧度），负值无缺口</summary>
        public static float GapAngle { get; private set; } = -10f;
        /// <summary>缺口半宽（弧度）</summary>
        public static float GapHalfWidth { get; private set; }
        /// <summary>辐条数</summary>
        public static int SpokeCount { get; private set; } = 2;
        /// <summary>辐条伸展 0~1</summary>
        public static float SpokeReach { get; private set; }
        /// <summary>本帧编队是否带伤害（收缩脉冲/扫压窗口）</summary>
        public static bool DamageOn { get; private set; }
        /// <summary>编队成员上限（用于槽位归一）</summary>
        public static int SlotCount { get; private set; } = 1;

        private static uint frameStamp;

        /// <summary>本帧是否有效</summary>
        public static bool Fresh => Main.GameUpdateCount - frameStamp <= 2;

        public static void PushCage(Vector2 center, float radius, float spinPhase,
            float gapAngle, float gapHalfWidth, bool damageOn, int slotCount) {
            Mode = ModeCage;
            CageCenter = center;
            CageRadius = radius;
            SpinPhase = spinPhase;
            GapAngle = gapAngle;
            GapHalfWidth = gapHalfWidth;
            DamageOn = damageOn;
            SlotCount = System.Math.Max(slotCount, 1);
            frameStamp = Main.GameUpdateCount;
        }

        public static void PushLance(Vector2 center, float spinPhase, int spokeCount,
            float reach, bool damageOn, int slotCount) {
            Mode = ModeLance;
            CageCenter = center;
            SpinPhase = spinPhase;
            SpokeCount = System.Math.Max(spokeCount, 1);
            SpokeReach = MathHelper.Clamp(reach, 0f, 1f);
            DamageOn = damageOn;
            SlotCount = System.Math.Max(slotCount, 1);
            frameStamp = Main.GameUpdateCount;
        }

        public static void Clear() {
            Mode = ModeNone;
            frameStamp = 0;
        }
    }
}
