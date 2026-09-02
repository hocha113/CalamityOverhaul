using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Rarities
{
    /// <summary>
    /// 本模组自有稀有度基类。进度五档（青金/翠玉/赤铜/鎏金/星银）与四传奇专属档共用：
    /// <see cref="RarityColor"/> 给拾取飘字、[i:] 标签等只读颜色的位置；
    /// 提示框名称行由 <see cref="RarityTooltipRenderer"/> 转到 <see cref="DrawName"/> 自绘特效，
    /// 全程在当前 SpriteBatch 直绘、不切渲染目标
    /// </summary>
    internal abstract class CWRRarity : ModRarity, ILocalizedModType
    {
        public string LocalizationCategory => "Rarities";

        public LocalizedText DisplayName { get; private set; }

        /// <summary>进度档序，1 起；传奇档 100 起。换铸阶梯与扫描器排序用</summary>
        public abstract int Tier { get; }

        /// <summary>静态主色</summary>
        public abstract Color BaseColor { get; }

        public override Color RarityColor => BaseColor;

        public override void SetStaticDefaults() {
            DisplayName = this.GetLocalization("DisplayName", () => Name);
        }

        /// <summary>
        /// 名称行自绘。<paramref name="color"/> 已含 mouseTextColor 衰减（可能被他模 OverrideColor 换色），
        /// <paramref name="scale"/> 为行基准缩放；默认只画纯色+原版阴影
        /// </summary>
        public virtual void DrawName(SpriteBatch sb, Item item, string text, Vector2 pos, Color color, Vector2 scale, float time) {
            RarityNameEffects.DrawPlain(sb, text, pos, color, scale);
        }

        public override void Unload() {
            DisplayName = null;
        }
    }
}
