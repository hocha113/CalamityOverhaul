using CalamityOverhaul.Content.Narrative;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace CalamityOverhaul.Content.MainMenus.Characters
{
    /// <summary>沈幽占位定义，专属结局尚未就绪，保持锁定<br/>
    /// 接线步骤: <see cref="Unlocked"/> 换成真实结局旗标、<see cref="Expressions"/> 填立绘组（全身立绘 Shenyo.png 258x544 已就位）</summary>
    internal sealed class ShenyoCharacter : MenuCharacter
    {
        public override string Key => "Shenyo";
        public override int SortOrder => 20;
        public override bool Unlocked => false;//等待专属结局接线

        private List<Texture2D> chipFrames;
        public override IList<Texture2D> ChipFrames {
            get {
                if (chipFrames == null && ADVAsset.Shenyo_Scrutiny != null) {
                    chipFrames = [ADVAsset.Shenyo_Scrutiny];
                }
                return chipFrames;
            }
        }
        public override float ChipScale => 0.5f;//96x92 居中裁 74x92->37x46

        /// <summary>对齐硫火芯片源幅，左右各切 11px 发丝</summary>
        public override Rectangle? GetChipSource(Texture2D tex) {
            const int w = 74, h = 92;
            int srcW = Math.Min(w, tex.Width);
            int srcH = Math.Min(h, tex.Height);
            return new Rectangle((tex.Width - srcW) / 2, (tex.Height - srcH) / 2, srcW, srcH);
        }

        public override IList<Texture2D> Expressions => null;//锁定期不展示立绘

        //鬼湖湿墨色板，承 ShenyoMenuTheme.AccentWater 系
        public override Color AccentDark => new Color(64, 104, 116);
        public override Color AccentBright => new Color(136, 202, 216);
        public override Color BaseShade => new Color(8, 12, 15);
        public override string FallbackName => "沈幽";

        public override void ClearRuntime() => chipFrames = null;
    }
}
