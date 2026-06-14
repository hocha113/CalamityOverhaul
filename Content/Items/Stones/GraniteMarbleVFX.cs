using CalamityOverhaul.Common;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace CalamityOverhaul.Content.Items.Stones
{
    /// <summary>
    /// 花岗岩 / 大理石两套装备共用的视觉常量与底层 VFX 助手
    /// <br/>仅封装重复的 <see cref="EffectLoader.GradientTrail"/> 参数装配，不引入任何高层武器框架
    /// </summary>
    internal static class GraniteMarbleVFX
    {
        //资源所在目录（贴图与 .cs 同放在 Content 下，默认自动加载）
        public const string GraniteTex = "CalamityOverhaul/Content/Items/Stones/Granites/";
        public const string MarbleTex = "CalamityOverhaul/Content/Items/Stones/Marbles/";

        //渐变色条：花岗岩用冰蓝水晶能量，大理石用古典金白
        public static Texture2D GraniteBar => CWRAsset.AbsoluteZero_Bar.Value;
        public static Texture2D MarbleBar => CWRAsset.AegisBlade_Bar.Value;

        //主题色
        public static readonly Color GraniteCore = new Color(120, 185, 255);
        public static readonly Color GraniteDeep = new Color(70, 120, 220);
        public static readonly Color GraniteSpark = new Color(150, 210, 255);
        public static readonly Color MarbleCore = new Color(255, 247, 220);
        public static readonly Color MarbleGold = new Color(228, 196, 120);
        public static readonly Color MarbleDust = new Color(214, 210, 196);

        /// <summary>
        /// 为 <see cref="EffectLoader.GradientTrail"/> 装配标准参数；调用方负责设置 BlendState 并执行 Trail.DrawTrail
        /// </summary>
        public static void ApplyGradientTrail(Effect effect, Texture2D gradientBar, Texture2D baseImage) {
            effect.Parameters["transformMatrix"].SetValue(VaultUtils.GetTransfromMatrix());
            effect.Parameters["uTime"].SetValue((float)Main.timeForVisualEffects * 0.08f);
            effect.Parameters["uTimeG"].SetValue(Main.GlobalTimeWrappedHourly * 0.2f);
            effect.Parameters["udissolveS"].SetValue(1f);
            effect.Parameters["uBaseImage"].SetValue(baseImage);
            effect.Parameters["uFlow"].SetValue(CWRAsset.Airflow.Value);
            effect.Parameters["uGradient"].SetValue(gradientBar);
            effect.Parameters["uDissolve"].SetValue(CWRAsset.Extra_193.Value);
        }
    }
}
