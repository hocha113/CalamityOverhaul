using System;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.VolleyBows
{
    /// <summary>
    /// 编队位形纯函数库：输入编队类型与索引，输出确定性的出膛偏移。
    /// 无任何随机源，同一输入永远同一输出，联机各端与生成端天然一致
    /// </summary>
    internal static class FormationLib
    {
        /// <summary>
        /// 取第 i 支箭（共 n 支）的出膛偏移。
        /// side：垂直弹道方向的偏移（px，正=弹道左手侧）；
        /// back：沿弹道反向的后移（px，负=前移）；
        /// rotOff：速度角偏移（弧度，仅 Cone 使用，此时 spread 解释为总扇角度数）
        /// </summary>
        public static void Get(GsVolleyFormation formation, int i, int n, float spread,
            out float side, out float back, out float rotOff) {
            side = 0f;
            back = 0f;
            rotOff = 0f;
            if (n <= 1) {
                return;
            }
            float center = (n - 1) * 0.5f;
            float k = i - center;

            switch (formation) {
                case GsVolleyFormation.Line:
                    side = k * spread;
                    break;
                case GsVolleyFormation.Wedge:
                    side = k * spread;
                    back = MathF.Abs(k) * spread * 0.8f;
                    break;
                case GsVolleyFormation.Echelon:
                    side = k * spread;
                    back = (k + center) * spread * 0.55f;
                    break;
                case GsVolleyFormation.Cross: {
                    //奇数含中锋（槽 0），偶数纯四向；槽序：中、左、右、前、后，逐环外扩
                    int slot = n % 2 == 1 ? i : i + 1;
                    if (slot == 0) {
                        break;
                    }
                    int ring = (slot - 1) / 4 + 1;
                    int arm = (slot - 1) % 4;
                    float d = spread * ring;
                    switch (arm) {
                        case 0: side = d; break;
                        case 1: side = -d; break;
                        case 2: back = -d; break;
                        default: back = d * 0.7f; break;
                    }
                    break;
                }
                case GsVolleyFormation.Cone:
                    //spread 此处为总扇角（度）：均分到两侧
                    rotOff = MathHelper.ToRadians(spread) * (k / center) * 0.5f;
                    break;
            }
        }

        /// <summary>编队主箭索引（全伤位），其余为副箭</summary>
        public static int MainIndex(int n) => (n - 1) / 2;
    }
}
