using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.HackTimes.Protocols
{
    /// <summary>
    /// 数据烙印的前缀剪贴板，跟玩家存档。<br/>
    /// owner 端自治：读源与写靶都在拥有者客户端结算，服务端从不读这份状态，
    /// 所以不需要任何网络同步；死亡、换世界都不清空，只有写靶成功才清
    /// </summary>
    internal sealed class DataBrandPlayer : ModPlayer
    {
        private const string VanillaTag = "DataBrandPrefix";
        private const string ModdedTag = "DataBrandPrefixMod";

        /// <summary>剪贴板里的前缀 id，0 = 空</summary>
        internal int ClipboardPrefix;

        public override void Initialize() => ClipboardPrefix = 0;

        public override void SaveData(TagCompound tag) {
            if (ClipboardPrefix <= 0) return;
            //模组前缀的 id 随加载顺序漂移，存 FullName；原版 id 稳定，直接存数
            if (ClipboardPrefix < PrefixID.Count) {
                tag[VanillaTag] = ClipboardPrefix;
            }
            else if (PrefixLoader.GetPrefix(ClipboardPrefix) is ModPrefix prefix) {
                tag[ModdedTag] = prefix.FullName;
            }
        }

        public override void LoadData(TagCompound tag) {
            ClipboardPrefix = 0;
            if (tag.TryGet(VanillaTag, out int vanillaId)
                && vanillaId > 0 && vanillaId < PrefixID.Count) {
                ClipboardPrefix = vanillaId;
                return;
            }
            //前缀所属模组被卸了就静默丢弃，别留一个指向空气的剪贴板
            if (tag.TryGet(ModdedTag, out string fullName)
                && ModContent.TryFind(fullName, out ModPrefix prefix)) {
                ClipboardPrefix = prefix.Type;
            }
        }
    }

    /// <summary>剪贴板非空时在本机玩家头顶挂一枚前缀标签，风格照骇入状态卡</summary>
    internal sealed class DataBrandHudTag : ModSystem
    {
        private const float FontScale = 0.68f;

        public override void PostDrawInterface(SpriteBatch spriteBatch) {
            if (Main.gameMenu || Main.hideUI) return;
            Player player = Main.LocalPlayer;
            if (player?.active != true || player.dead || player.ghost) return;
            if (!player.TryGetModPlayer(out DataBrandPlayer brand)
                || brand.ClipboardPrefix <= 0) {
                return;
            }
            Texture2D px = VaultAsset.placeholder2?.Value;
            if (px == null) return;

            int id = brand.ClipboardPrefix;
            string prefixName = id < Lang.prefix.Length
                ? Lang.prefix[id].Value : $"#{id}";
            string text = DataBrand.HudTag?.Format(prefixName) ?? prefixName;

            //世界坐标 → 屏幕像素 → UI 坐标（本批次带 UIScaleMatrix）
            Vector2 world = player.Top + new Vector2(0f, -58f);
            Vector2 screen = Vector2.Transform(world - Main.screenPosition,
                Main.GameViewMatrix.TransformationMatrix) / Main.UIScale;

            Vector2 textSize = FontAssets.MouseText.Value.MeasureString(text) * FontScale;
            float w = textSize.X + 16f;
            float h = 20f;
            Rectangle card = new((int)(screen.X - w * 0.5f), (int)(screen.Y - h), (int)w, (int)h);

            DrawCard(spriteBatch, px, card, text);
        }

        private static void DrawCard(SpriteBatch sb, Texture2D px,
            Rectangle r, string text) {
            sb.Draw(px, r, HackTheme.BgPanel * 0.85f);
            sb.Draw(px, new Rectangle(r.X, r.Y, 2, r.Height), HackTheme.AccentAlt);
            //细边框
            Color border = HackTheme.Border * 0.5f;
            sb.Draw(px, new Rectangle(r.X, r.Y, r.Width, 1), border);
            sb.Draw(px, new Rectangle(r.X, r.Bottom - 1, r.Width, 1), border);
            sb.Draw(px, new Rectangle(r.Right - 1, r.Y, 1, r.Height), border);

            Utils.DrawBorderString(sb, text, new Vector2(r.X + 8f, r.Y + 3f),
                HackTheme.AccentAlt, FontScale);
        }
    }
}
