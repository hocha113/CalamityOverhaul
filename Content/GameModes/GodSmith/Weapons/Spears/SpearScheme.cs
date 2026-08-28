using CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Shortswords;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Spears
{
    /// <summary>
    /// 长矛族公共层。突刺骨架引用短剑族的 <see cref="GsThrustHeldBase"/>（两族共用突刺框架），
    /// 本类只钉族名与长矛默认手感：中距压制、断拍窗更松。<br/>
    /// 族主题锚：禁用原版 spear AI 直挂，一律接管——每把矛回答「什么材质 + 什么枪法」，
    /// 签名向蓄力长刺/横扫变式/驻场压制三个方向分化；骑枪三件归本族（冲锋机制特化）
    /// </summary>
    internal abstract class GsSpearScheme : GsThrustScheme
    {
        public sealed override string GsFamily => "Spears";

        /// <summary>长矛节奏更沉，断拍窗放宽</summary>
        protected override int ComboResetFrames => 56;
    }
}
