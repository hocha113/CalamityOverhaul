namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Core
{
    /// <summary>
    /// 仪式帷幕屏效状态（各端本地推导，无网络）<br/>
    /// 状态每帧调用 <see cref="SetVeil"/> 声明目标；未声明则自然消散
    /// </summary>
    internal static class CultistScreenFX
    {
        /// <summary>帷幕当前强度 0~1，缓动逼近目标</summary>
        public static float VeilIntensity { get; private set; }
        /// <summary>帷幕目标强度，每帧自衰减</summary>
        private static float veilGoal;
        /// <summary>帷幕圆心（世界坐标）</summary>
        public static Vector2 VeilWorldCenter { get; private set; }
        /// <summary>元素染色</summary>
        public static Vector3 VeilTint { get; private set; } = new(1f, 0.7f, 0.35f);
        /// <summary>符环带半径（世界px）</summary>
        public static float BandRadiusPx { get; private set; } = 620f;
        /// <summary>白闪 0~1，快衰减</summary>
        public static float Flash { get; private set; }
        /// <summary>死亡去饱和 0~1</summary>
        public static float BreakDesat { get; set; }

        public static bool HasAny => VeilIntensity > 0.012f || Flash > 0.012f || BreakDesat > 0.012f;

        /// <summary>声明本帧帷幕目标（状态每帧调用保持）</summary>
        public static void SetVeil(float goal, Vector2 worldCenter, Color tint, float bandRadiusPx = 620f) {
            if (goal > veilGoal) {
                veilGoal = MathHelper.Clamp(goal, 0f, 1f);
            }
            VeilWorldCenter = worldCenter;
            VeilTint = tint.ToVector3();
            BandRadiusPx = bandRadiusPx;
        }

        /// <summary>推白闪</summary>
        public static void PushFlash(float amount) {
            if (amount > Flash) {
                Flash = MathHelper.Clamp(amount, 0f, 1f);
            }
        }

        /// <summary>每帧推进：强度缓动，目标/白闪/去饱和自衰减</summary>
        public static void Update() {
            VeilIntensity = MathHelper.Lerp(VeilIntensity, veilGoal, 0.08f);
            veilGoal *= 0.93f;
            Flash *= 0.86f;
            BreakDesat *= 0.975f;
            if (BreakDesat < 0.004f) {
                BreakDesat = 0f;
            }
            if (VeilIntensity < 0.004f) {
                VeilIntensity = 0f;
            }
            if (Flash < 0.004f) {
                Flash = 0f;
            }
        }

        /// <summary>卸载/战斗结束清空</summary>
        public static void Clear() {
            VeilIntensity = 0f;
            veilGoal = 0f;
            Flash = 0f;
            BreakDesat = 0f;
        }
    }
}
