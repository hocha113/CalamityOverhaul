namespace CalamityOverhaul.Content.Scenarios.Kiyume.Fog
{
    /// <summary>
    /// 横向带的雾色与厚度微调表：世界列 → (雾色, 浓度倍率)。<br/>
    /// 色值由鬼梦色板推导（HORIZON/GROUND_FOG 去饱和压暗，雾是载着血湖颜色的空气，
    /// 不是血湖本身），越往东离湖越远越冷越薄；<br/>
    /// 浓度主曲线是雾线与距离衰减的解析式（KiyumeFogSim），这里的倍率只做手调余量
    /// </summary>
    internal static class KiyumeFogTheme
    {
        //雾色（注释=推导来源）
        private static readonly Vector3 ShoreMist = Rgb(112, 34, 32);   //HORIZON(143,30,14) 去饱和压暗
        private static readonly Vector3 VillageMist = Rgb(84, 30, 30);  //ShoreMist 再压一档
        private static readonly Vector3 GroveMist = Rgb(58, 26, 30);    //离湖变冷，仍在红黑里
        private static readonly Vector3 RidgeMist = Rgb(46, 22, 28);    //远山最淡最冷

        //关键帧三列平行数组（世界列升序），列间线性插值
        private static readonly float[] Cols = [0f, 320f, 620f, 1180f, 1700f, 2500f, 3200f];
        private static readonly float[] Muls = [1.10f, 1.06f, 1f, 0.98f, 0.92f, 0.86f, 0.82f];
        private static readonly Vector3[] Colors = [
            ShoreMist, ShoreMist, ShoreMist, VillageMist, GroveMist, RidgeMist, RidgeMist
        ];

        /// <summary>按世界列（tile 列，可为小数）采样雾色与浓度倍率</summary>
        internal static void Sample(float worldCol, out Vector3 color, out float mul) {
            if (worldCol <= Cols[0]) {
                color = Colors[0];
                mul = Muls[0];
                return;
            }
            int last = Cols.Length - 1;
            if (worldCol >= Cols[last]) {
                color = Colors[last];
                mul = Muls[last];
                return;
            }
            int i = 1;
            while (worldCol > Cols[i]) {
                i++;
            }
            float t = (worldCol - Cols[i - 1]) / (Cols[i] - Cols[i - 1]);
            color = Vector3.Lerp(Colors[i - 1], Colors[i], t);
            mul = MathHelper.Lerp(Muls[i - 1], Muls[i], t);
        }

        private static Vector3 Rgb(byte r, byte g, byte b) => new(r / 255f, g / 255f, b / 255f);
    }
}
