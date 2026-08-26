using CalamityOverhaul.Content.Items.Magic.Elysiums.Disciples;
using CalamityOverhaul.Content.UIs.HudStack;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.GameInput;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.UI.Chat;

namespace CalamityOverhaul.Content.Items.Magic.Elysiums.UI
{
    /// <summary>
    /// 圣位随行HUD(左下堆叠)：手持天国极乐时亮起，
    /// 十二圣位小签一字排开(在职染身份色、殉道燃金覆十字、空缺暗淡)，
    /// 下行殉道之力读数
    /// </summary>
    internal class ElysiumFaithHud : UIHandle, ILocalizedModType, IBottomLeftHud
    {
        public string LocalizationCategory => "UI";

        private const float PipSize = 15f;
        private const float PipGap = 4f;
        private const float RowW = 12 * PipSize + 11 * PipGap;

        private static LocalizedText MartyrdomPowerText;

        private float fade;
        private float pulseTimer;

        private static float UIScreenH => PlayerInput.RealScreenHeight / Main.UIScale;

        public override void SetStaticDefaults() {
            MartyrdomPowerText = this.GetLocalization(nameof(MartyrdomPowerText), () => "殉道之力 {0}/11");
        }

        private static bool WantsShow
            => Main.LocalPlayer.active && !Main.LocalPlayer.dead
            && Main.LocalPlayer.HeldItem?.type == ModContent.ItemType<Elysium>();

        public override bool Active => WantsShow || fade > 0.01f;

        #region 左下堆叠契约
        public bool HudStackActive => Active;
        public int HudStackOrder => 1;
        public Vector2 HudStackAnchor => new(26f, UIScreenH - 74f);
        public float HudStackTopExtent => 30f;
        public float HudStackBottomExtent => 34f;
        #endregion

        public override void Update() {
            fade = WantsShow ? Math.Min(1f, fade + 0.08f) : Math.Max(0f, fade - 0.06f);
            pulseTimer += 0.035f;
        }

        public override void Draw(SpriteBatch spriteBatch) {
            if (fade < 0.01f || !Main.LocalPlayer.TryGetModPlayer(out ElysiumPlayer ep)) {
                return;
            }

            Vector2 anchor = BottomLeftHudStack.ResolveAnchor(this);
            Texture2D px = VaultAsset.placeholder2?.Value;
            if (px == null) {
                return;
            }

            //出场自左滑入
            anchor.X -= (1f - fade) * 24f;

            //十二圣位小签
            for (int i = 0; i < ElysiumPlayer.SeatCount; i++) {
                Vector2 pos = anchor + new Vector2(i * (PipSize + PipGap), 0f);
                DrawPip(spriteBatch, px, ep, i, pos);
            }

            //殉道之力读数
            int energy = ep.MartyrdomEnergy;
            string text = MartyrdomPowerText.Format(energy);
            Color textColor = energy >= 11
                ? Color.Lerp(new Color(255, 224, 130), Color.White, 0.5f + 0.5f * MathF.Sin(pulseTimer * 3f))
                : Color.Lerp(new Color(150, 140, 115), new Color(255, 224, 130), energy / 11f);
            ChatManager.DrawColorCodedStringWithShadow(spriteBatch, FontAssets.MouseText.Value, text,
                anchor + new Vector2(0f, PipSize + 6f), textColor * fade, 0f, Vector2.Zero, Vector2.One * 0.72f);

            //能量细条
            float barW = RowW * 0.72f;
            float fill = energy / 11f;
            var barBg = new Rectangle((int)anchor.X, (int)(anchor.Y + PipSize + 30f), (int)barW, 3);
            spriteBatch.Draw(px, barBg, new Rectangle(0, 0, 1, 1), new Color(40, 34, 22) * (0.8f * fade));
            if (fill > 0f) {
                var barFill = new Rectangle(barBg.X, barBg.Y, (int)(barW * fill), 3);
                spriteBatch.Draw(px, barFill, new Rectangle(0, 0, 1, 1), new Color(255, 216, 110) * (0.9f * fade));
            }
        }

        private void DrawPip(SpriteBatch sb, Texture2D px, ElysiumPlayer ep, int seat, Vector2 pos) {
            DiscipleDef def = DiscipleCatalog.Get(seat);
            bool martyred = ep.Martyred[seat];
            bool alive = ep.IsSeatAlive(seat);
            var rect = new Rectangle((int)pos.X, (int)pos.Y, (int)PipSize, (int)PipSize);

            Color fillColor;
            Color borderColor;
            if (martyred) {
                float pulse = 0.8f + 0.2f * MathF.Sin(pulseTimer * 2f + seat * 0.4f);
                fillColor = new Color(150, 118, 40) * (0.75f * pulse);
                borderColor = new Color(255, 220, 130) * pulse;
            }
            else if (alive) {
                fillColor = def.BodyColor * 0.5f;
                borderColor = def.AccentColor * 0.9f;
            }
            else {
                fillColor = new Color(24, 20, 16) * 0.7f;
                borderColor = new Color(70, 62, 48) * 0.8f;
            }

            sb.Draw(px, rect, new Rectangle(0, 0, 1, 1), fillColor * fade);
            sb.Draw(px, new Rectangle(rect.X, rect.Y, rect.Width, 1), borderColor * fade);
            sb.Draw(px, new Rectangle(rect.X, rect.Bottom - 1, rect.Width, 1), borderColor * fade);
            sb.Draw(px, new Rectangle(rect.X, rect.Y, 1, rect.Height), borderColor * fade);
            sb.Draw(px, new Rectangle(rect.Right - 1, rect.Y, 1, rect.Height), borderColor * fade);

            //约翰(启示钥匙)：常亮白蓝细内框
            if (seat == DiscipleCatalog.JohnSeat && !martyred) {
                Color johnColor = new Color(205, 210, 255) * (0.6f * fade);
                sb.Draw(px, new Rectangle(rect.X + 2, rect.Y + 2, rect.Width - 4, 1), johnColor);
                sb.Draw(px, new Rectangle(rect.X + 2, rect.Bottom - 3, rect.Width - 4, 1), johnColor);
            }

            //殉道十字
            if (martyred) {
                Color crossColor = new Color(255, 232, 160) * (0.95f * fade);
                float cx = rect.X + PipSize * 0.5f;
                float cy = rect.Y + PipSize * 0.5f;
                sb.Draw(px, new Rectangle((int)(cx - 1f), (int)(cy - 5f), 2, 10), crossColor);
                sb.Draw(px, new Rectangle((int)(cx - 4f), (int)(cy - 2f), 8, 2), crossColor);
            }
        }
    }
}
