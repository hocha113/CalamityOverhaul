using static CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaTalismans.KikasaTalismanGlyph;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaTalismans
{
    //首批三符：数值为基线占位，实装手感后再调；
    //赋效+轻代价对齐鬼切铭刻的风味，代价不许大到让符变成负优化。
    //字形随符走（BuildGlyph），中央库只留伞形 fallback

    /// <summary>霖「绵密细雨」：墨雨节拍更密，单滴更轻</summary>
    internal sealed class FuLin : KikasaTalismanDefinition
    {
        public override int SortOrder => 0;

        public override Color InkAccent => new(96, 158, 204);

        public override void ModifyProfile(ref KikasaTalismanProfile profile) {
            profile.RainTempoMul *= 0.80f;
            profile.DropDamageMul *= 0.94f;
        }

        //霖：雨盖下三缕错拍斜雨，雨脚三点渐远——连日不歇的节奏感
        internal override KikasaGlyphStroke[] BuildGlyph() => [
            Canopy(0.12f),
            L(0.09f, -0.44f, -0.28f, -0.58f, 0.26f),
            L(0.09f, -0.04f, -0.20f, -0.16f, 0.42f),
            L(0.09f, 0.36f, -0.26f, 0.26f, 0.20f),
            L(0.07f, 0.58f, -0.02f, 0.50f, 0.34f),
            Dot(0.10f, -0.62f, 0.52f),
            Dot(0.09f, -0.22f, 0.66f),
            Dot(0.10f, 0.16f, 0.50f),
        ];
    }

    /// <summary>潦「积水成潦」：大滴落地必积洼（不须湖倾档），墨洼更久更阔，直击略轻</summary>
    internal sealed class FuLao : KikasaTalismanDefinition
    {
        public override int SortOrder => 1;

        public override Color InkAccent => new(92, 156, 134);

        public override void ModifyProfile(ref KikasaTalismanProfile profile) {
            profile.PuddleUnlock = true;
            profile.PuddleLifeMul *= 1.50f;
            profile.PuddleRadiusMul *= 1.30f;
            profile.DropDamageMul *= 0.95f;
        }

        //潦：一滴垂落，碗形积潦，潦面两圈涟纹，潦缘一勾外溢
        internal override KikasaGlyphStroke[] BuildGlyph() => [
            Canopy(0.11f, -0.52f, 0.46f),
            L(0.08f, 0.00f, -0.34f, 0.00f, -0.02f),
            Dot(0.12f, 0.00f, -0.44f),
            Arc(0.13f, 0.00f, 0.10f, 0.56f, 0.30f, 2.84f, 14),
            Arc(0.09f, 0.00f, 0.22f, 0.34f, 0.48f, 2.66f, 10),
            Arc(0.07f, 0.00f, 0.30f, 0.16f, 0.60f, 2.54f, 8),
            L(0.07f, 0.54f, 0.26f, 0.74f, 0.44f),
        ];
    }

    /// <summary>沛「倾盆一注」：蓄墨更快、墨泉更狠，常雨略缓</summary>
    internal sealed class FuPei : KikasaTalismanDefinition
    {
        public override int SortOrder => 2;

        public override Color InkAccent => new(208, 122, 92);

        public override void ModifyProfile(ref KikasaTalismanProfile profile) {
            profile.ChargeRateMul *= 1.35f;
            profile.GeyserDamageMul *= 1.20f;
            profile.RainTempoMul *= 1.08f;
        }

        //沛：雨盖下一道微斜粗注贯底，旁一缕细流，
        //落点双溅一长一陡（倾出来的水不对称），飞沫两点一大一小
        internal override KikasaGlyphStroke[] BuildGlyph() => [
            Canopy(0.13f, -0.44f, 0.50f),
            L(0.20f, 0.02f, -0.30f, -0.04f, 0.50f),
            L(0.06f, 0.16f, -0.16f, 0.20f, 0.18f),
            L(0.10f, -0.04f, 0.50f, -0.44f, 0.74f),
            L(0.09f, -0.04f, 0.50f, 0.30f, 0.64f),
            Dot(0.12f, -0.56f, 0.46f),
            Dot(0.09f, 0.46f, 0.34f),
        ];
    }
}
