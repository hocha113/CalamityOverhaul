namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Shortswords
{
    /// <summary>
    /// 短剑族公共层。突刺骨架在 <see cref="GsThrustHeldBase"/>（两族共用），
    /// 本类只钉族名与短剑默认手感：贴身快刺、窗口更紧的连刺节奏。<br/>
    /// 族主题锚：1.4.4 突刺手感强化——每把短剑回答「什么金属 + 什么刺法」，
    /// 签名向连刺节奏/格挡反击/刺尖精准三个方向分化
    /// </summary>
    internal abstract class GsShortswordScheme : GsThrustScheme
    {
        public sealed override string GsFamily => "Shortswords";

        /// <summary>短剑默认断拍窗更紧，鼓励贴身连刺</summary>
        protected override int ComboResetFrames => 40;
    }
}
