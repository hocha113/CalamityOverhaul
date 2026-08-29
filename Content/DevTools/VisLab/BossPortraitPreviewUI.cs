#if DEBUG
using CalamityOverhaul.Content.NPCs.BloomsandSerpents;
using CalamityOverhaul.Content.NPCs.FestersandSerpents.Rendering;
using CalamityOverhaul.Content.NPCs.SeaShrimp.Rendering;
using CalamityOverhaul.OtherMods.BossChecklist;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;

namespace CalamityOverhaul.Content.DevTools.VisLab
{
    /// <summary>
    /// 三 Boss 图鉴头像沙盒预览（<c>/vlab run bossportraits</c>）：
    /// 并排绘制海虾/荒花/脓蕾三张实时头像，页面尺寸对齐 BossChecklist 实机页比例。
    /// <see cref="MaskedPreview"/> 由 job fields 注入，切进度隐藏剪影自查
    /// </summary>
    internal sealed class BossPortraitPreviewUI : UIHandle
    {
        /// <summary>剪影自查开关（vlab job fields 反射注入）</summary>
        public bool MaskedPreview;

        public override bool Active => IsOpen;

        public override void Draw(SpriteBatch spriteBatch) {
            const int PageW = 375;
            const int PageH = 480;
            const int Gap = 16;
            const int X0 = 30;
            const int Y0 = 90;
            Color mask = MaskedPreview ? Color.Black : Color.White;

            BossPortraitStage.Draw(spriteBatch, new Rectangle(X0, Y0, PageW, PageH), mask,
                SeaShrimpPortraitActor.Instance);
            BossPortraitStage.Draw(spriteBatch, new Rectangle(X0 + PageW + Gap, Y0, PageW, PageH), mask,
                BssPortraitActor.Instance);
            BossPortraitStage.Draw(spriteBatch, new Rectangle(X0 + (PageW + Gap) * 2, Y0, PageW, PageH), mask,
                FssPortraitActor.Instance);
        }
    }
}
#endif
