namespace CalamityOverhaul.Content.Scenarios.Dungeonworld.Fog
{
    /// <summary>
    /// 层带浓度曲线与雾色表：世界行 → (基础浓度, 雾色)。<br/>
    /// 色值由 DungeonworldLoadTheme.BandAccents 七层强调色推导（向中性灰/骨白方向去饱和——
    /// 雾是"载着层色的空气"，不是层色本身），不发明新色彩身份；<br/>
    /// 行号区间对 DungeonworldMetrics.Bands 现值（2000×6000），调层带只改关键帧表（FOG.md §3）
    /// </summary>
    internal static class DungeonworldFogTheme
    {
        //雾色（注释=推导来源：BandAccents 原色 → 处理方式）
        private static readonly Vector3 PaperGray = Rgb(152, 138, 116);   //III 纸墨褐(138,107,63)提灰
        private static readonly Vector3 WetGreen = Rgb(86, 124, 104);     //IV 沼绿(63,116,88)提灰
        private static readonly Vector3 BoneWhite = Rgb(208, 196, 168);   //V 骨白(199,185,149)提亮
        private static readonly Vector3 RustOrange = Rgb(156, 96, 54);    //VI 炉锈橙(158,85,39)微提
        private static readonly Vector3 NetherViolet = Rgb(104, 94, 170); //VII 冥紫(94,85,168)微提
        private static readonly Vector3 AbyssViolet = Rgb(46, 40, 88);    //VII 冥紫压向 Abyss(5,7,14)

        //关键帧三列平行数组（世界行升序），行间线性插值；L1/L2 硬零
        private static readonly float[] Rows = [
            0f, 384f, 520f, 1600f, 1810f, 2680f, 2860f,
            4080f, 4290f, 5240f, 5420f, 5560f, 5720f, 6000f
        ];
        private static readonly float[] Densities = [
            0f, 0f, 0.30f, 0.34f, 0.46f, 0.48f, 0.58f,
            0.60f, 0.72f, 0.74f, 0.30f, 0.32f, 0.70f, 0.92f
        ];
        private static readonly Vector3[] Colors = [
            PaperGray, PaperGray, PaperGray, PaperGray, WetGreen, WetGreen, BoneWhite,
            BoneWhite, RustOrange, RustOrange, NetherViolet, NetherViolet, AbyssViolet, AbyssViolet
        ];

        /// <summary>按世界行（tile 行，可为小数）采样基础浓度与雾色</summary>
        internal static void Sample(float worldRow, out float density, out Vector3 color) {
            if (worldRow <= Rows[0]) {
                density = Densities[0];
                color = Colors[0];
                return;
            }
            int last = Rows.Length - 1;
            if (worldRow >= Rows[last]) {
                density = Densities[last];
                color = Colors[last];
                return;
            }
            //14 个关键帧线性走查（每雾元行只采一次，成本可忽略）
            int i = 1;
            while (worldRow > Rows[i]) {
                i++;
            }
            float t = (worldRow - Rows[i - 1]) / (Rows[i] - Rows[i - 1]);
            density = MathHelper.Lerp(Densities[i - 1], Densities[i], t);
            color = Vector3.Lerp(Colors[i - 1], Colors[i], t);
        }

        private static Vector3 Rgb(byte r, byte g, byte b) => new(r / 255f, g / 255f, b / 255f);
    }
}
