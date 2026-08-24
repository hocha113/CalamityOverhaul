using CalamityOverhaul.Content.Narrative;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;

namespace CalamityOverhaul.Content.MainMenus.Characters
{
    /// <summary>海伦占位定义，全身立绘与专属结局尚未就绪，保持锁定<br/>
    /// 接线步骤: <see cref="Unlocked"/> 换成真实结局旗标、<see cref="Expressions"/> 填全身立绘组</summary>
    internal sealed class HelenCharacter : MenuCharacter
    {
        public override string Key => "Helen";
        public override int SortOrder => 10;
        public override bool Unlocked => false;//等待专属结局接线

        private List<Texture2D> chipFrames;
        public override IList<Texture2D> ChipFrames {
            get {
                if (chipFrames == null && ADVAsset.HelenADV != null) {
                    chipFrames = [ADVAsset.HelenADV];
                }
                return chipFrames;
            }
        }
        public override float ChipScale => 1f / 3f;//104x134->35x45

        public override IList<Texture2D> Expressions => null;//暂无全身立绘

        //深海青身份色
        public override Color AccentDark => new Color(30, 140, 190);
        public override Color AccentBright => new Color(90, 210, 255);
        public override Color BaseShade => new Color(5, 20, 28);
        public override string FallbackName => "海伦";

        public override void ClearRuntime() => chipFrames = null;
    }
}
