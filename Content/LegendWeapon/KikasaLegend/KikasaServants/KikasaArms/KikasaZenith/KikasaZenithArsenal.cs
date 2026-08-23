using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaServants.KikasaArms.KikasaZenith
{
    /// <summary>
    /// 天顶剑的诸剑记忆剑谱：二十柄旅途名剑逐条抄自原版 FinalFractalHelper 的档案
    /// （物品类型 / 专属拖尾色 / 刃长口径），天顶剑自身不入谱——主刀就是它本人。
    /// 供械奴·天顶抽选幻影剑与斩痕取色；抽选确定性（各端一致），取色对任意索引安全
    /// </summary>
    internal static class KikasaZenithArsenal
    {
        internal readonly record struct SwordProfile(int ItemType, Color TrailColor, float BladeLen);

        /// <summary>天顶剑本体的档案色（原版 4956 条目），终结斩与主刀光效用</summary>
        internal static readonly Color ZenithColor = new(178, 255, 180);

        /// <summary>天顶本体刃长口径（原版档案 86f）</summary>
        internal const float ZenithBladeLen = 86f;

        /// <summary>泰拉旅途上的二十柄名剑，顺序照抄原版 _fractalProfiles</summary>
        internal static readonly SwordProfile[] Swords = [
            new(ItemID.Starfury, new Color(236, 62, 192), 48f),
            new(ItemID.BeeKeeper, new Color(255, 231, 69), 48f),
            new(ItemID.LightsBane, new Color(122, 66, 191), 48f),
            new(ItemID.FieryGreatsword, new Color(254, 158, 35), 76f),
            new(ItemID.BladeofGrass, new Color(107, 203, 0), 70f),
            new(ItemID.Excalibur, new Color(236, 200, 19), 70f),
            new(ItemID.TrueExcalibur, new Color(236, 200, 19), 70f),
            new(ItemID.NightsEdge, new Color(179, 54, 201), 70f),
            new(ItemID.TrueNightsEdge, new Color(179, 54, 201), 70f),
            new(ItemID.InfluxWaver, new Color(84, 234, 245), 70f),
            new(ItemID.EnchantedSword, new Color(91, 158, 232), 48f),
            new(ItemID.TheHorsemansBlade, new Color(252, 95, 4), 76f),
            new(ItemID.Meowmere, new Color(254, 194, 250), 76f),
            new(ItemID.StarWrath, new Color(237, 63, 133), 70f),
            new(ItemID.TerraBlade, new Color(80, 222, 122), 70f),
            new(ItemID.Muramasa, new Color(56, 78, 210), 70f),
            new(ItemID.BloodButcherer, new Color(237, 28, 36), 70f),
            new(ItemID.Seedler, new Color(143, 215, 29), 80f),
            new(ItemID.Terragrim, new Color(178, 255, 180), 45f),
            new(ItemID.CopperShortsword, new Color(235, 166, 135), 45f),
        ];

        /// <summary>索引取色：负数或越界一律回天顶色（终结斩用 -1 直指本体）</summary>
        internal static Color ColorOf(int index)
            => index >= 0 && index < Swords.Length ? Swords[index].TrailColor : ZenithColor;

        /// <summary>索引取刃长：越界回天顶口径</summary>
        internal static float BladeLenOf(int index)
            => index >= 0 && index < Swords.Length ? Swords[index].BladeLen : ZenithBladeLen;

        /// <summary>
        /// 确定性抽剑：起点由种子与轮次揉出，步长 7 与 20 互质，
        /// 同一轮连抽不重复且各端一致（不掷 Main.rand）
        /// </summary>
        internal static int Pick(float seed, int wave, int ordinal) {
            int start = (int)MathF.Abs(seed * 977f + wave * 131f);
            return (start + ordinal * 7) % Swords.Length;
        }

        /// <summary>幻影剑绘制缩放：贴图对角折算到档案刃长口径（服务器回退常数，不参与模拟）</summary>
        internal static float DrawScaleOf(int index) {
            if (index < 0 || index >= Swords.Length) {
                return 1f;
            }
            SwordProfile profile = Swords[index];
            float diag = 52f;
            if (!Main.dedServ) {
                Main.instance.LoadItem(profile.ItemType);
                Texture2D tex = TextureAssets.Item[profile.ItemType]?.Value;
                if (tex != null) {
                    diag = MathF.Sqrt(tex.Width * tex.Width + tex.Height * tex.Height);
                }
            }
            return Math.Clamp(profile.BladeLen * 1.15f / MathF.Max(diag, 20f), 0.5f, 1.6f);
        }
    }
}
