using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaTalismans
{
    /// <summary>
    /// 唤雨符物品载体：入包即 Unlock 录入符箧；上绳只在湖心景祈雨绳。<br/>
    /// 无贴图，图标为程序化符纸（KikasaTalisman.fx 雨浸纸 + <see cref="KikasaTalismanGlyph"/> 湿墨符文）。<br/>
    /// 符纸不消耗，绳上点选仅作样板挂符
    /// </summary>
    internal abstract class KikasaTalismanItem : ModItem, ILocalizedModType
    {
        public override string LocalizationCategory => "Items";

        /// <summary>绑定符 Key，须与 <see cref="KikasaTalismanDefinition.Key"/> 一致</summary>
        public abstract string TalismanKey { get; }

        /// <summary>来历残句，由物品本地化域统一注册</summary>
        public LocalizedText Origin { get; private set; }
        /// <summary>实际赋效说明</summary>
        public LocalizedText Power { get; private set; }
        /// <summary>实际负担说明</summary>
        public LocalizedText Burden { get; private set; }

        public override string Texture => CWRConstant.VaultPlaceholder2;

        private static readonly Dictionary<string, int> keyToType = [];
        private static readonly Dictionary<string, KikasaTalismanItem> keyToItem = [];

        public sealed override void SetStaticDefaults() {
            keyToType[TalismanKey] = Type;
            keyToItem[TalismanKey] = this;
            if (KikasaTalismanRegistry.TryGet(TalismanKey, out KikasaTalismanDefinition definition)) {
                definition.BindLocalization(this);
            }
        }

        public override void Unload() {
            Origin = null;
            Power = null;
            Burden = null;
            keyToType.Clear();
            keyToItem.Clear();
        }

        /// <summary>把定义绑定到同 Key 符纸的官方物品本地化入口</summary>
        internal static bool TryBindLocalization(KikasaTalismanDefinition definition) {
            if (definition == null || !keyToItem.TryGetValue(definition.Key, out KikasaTalismanItem item)) {
                return false;
            }
            definition.BindLocalization(item);
            return true;
        }

        public override void SetDefaults() {
            Origin ??= this.GetLocalization(nameof(Origin), () => "...");
            Power ??= this.GetLocalization(nameof(Power), () => "...");
            Burden ??= this.GetLocalization(nameof(Burden), () => "...");
            Item.width = 32;
            Item.height = 32;
            Item.maxStack = 1;
            Item.rare = ItemRarityID.Orange;
            Item.value = Item.sellPrice(silver: 50);
        }

        public override void ModifyTooltips(List<TooltipLine> tooltips) {
            tooltips.Add(new TooltipLine(Mod, "KikasaFuOrigin", Origin.Value) {
                OverrideColor = new Color(146, 146, 152),
            });
            tooltips.Add(new TooltipLine(Mod, "KikasaFuPower", Power.Value) {
                OverrideColor = new Color(126, 168, 196),
            });
            tooltips.Add(new TooltipLine(Mod, "KikasaFuBurden", Burden.Value) {
                OverrideColor = new Color(174, 110, 110),
            });
        }

        public override void UpdateInventory(Player player) {
            KikasaTalismanOwned.Unlock(player, TalismanKey);
        }

        /// <summary>Key → 符纸物品 Type；未注册返回 0</summary>
        public static int ItemTypeForKey(string key)
            => key != null && keyToType.TryGetValue(key, out int type) ? type : 0;

        public override bool PreDrawInInventory(SpriteBatch spriteBatch, Vector2 position
            , Rectangle frame, Color drawColor, Color itemColor, Vector2 origin, float scale) {
            DrawTalisman(spriteBatch, position, scale, drawColor.A / 255f, inWorld: false);
            return false;
        }

        public override bool PreDrawInWorld(SpriteBatch spriteBatch, Color lightColor, Color alphaColor
            , ref float rotation, ref float scale, int whoAmI) {
            Vector2 center = Item.Center - Main.screenPosition;
            //暗处可寻：alpha 兜底 + 一点雨青冷背光
            float a = MathHelper.Max(lightColor.A / 255f, 0.35f);
            DrawBacklight(spriteBatch, center, 30f * scale, a * 0.30f);
            DrawTalisman(spriteBatch, center, scale, a, inWorld: true);
            return false;
        }

        private static void DrawBacklight(SpriteBatch sb, Vector2 center, float radius, float alpha) {
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Color add = new(KikasaTalismanPaperDraw.Sheen.R, KikasaTalismanPaperDraw.Sheen.G,
                KikasaTalismanPaperDraw.Sheen.B, 0);
            sb.Draw(glow, center, null, add * (alpha * 0.55f), 0f, glow.Size() * 0.5f,
                radius * 2f / glow.Width, SpriteEffects.None, 0f);
        }

        private void DrawTalisman(SpriteBatch sb, Vector2 center, float scale, float alpha, bool inWorld) {
            float time = Main.GlobalTimeWrappedHourly;
            float seed = Type * 0.71f;
            //随风轻摆 + 底缘潮息
            float sway = MathF.Sin(time * (inWorld ? 2.0f : 1.3f) + seed) * (inWorld ? 0.09f : 0.045f);
            float soak = 0.20f + 0.06f * MathF.Sin(time * 1.1f + seed * 3f);

            Vector2 size = new Vector2(19f, 34f) * scale;
            Vector2 down = (MathHelper.PiOver2 + sway).ToRotationVector2();
            Vector2 top = center - down * size.Y * 0.5f;

            if (inWorld) {
                KikasaTalismanPaperDraw.DrawWorld(sb, top, sway, size, alpha, soak, time + seed);
            }
            else {
                KikasaTalismanPaperDraw.DrawUI(sb, top, sway, size, alpha, soak, time + seed);
            }

            //符文湿墨：身份色由定义给出，未注册走兜底伞章
            Color accent = KikasaTalismanRegistry.TryGet(TalismanKey, out KikasaTalismanDefinition def)
                ? def.InkAccent : KikasaTalismanPaperDraw.Sheen;
            Vector2 glyphCenter = top + down * size.Y * 0.40f;
            KikasaTalismanGlyph.DrawInk(sb, TalismanKey, glyphCenter, size.X * 1.18f,
                alpha, KikasaTalismanPaperDraw.Ink, accent, time, sway);

            //顶端结绳孔：一粒墨点
            sb.Draw(VaultAsset.placeholder2.Value, top + down * 3f, new Rectangle(0, 0, 1, 1),
                KikasaTalismanPaperDraw.Ink * (alpha * 0.85f), MathHelper.PiOver4 + sway,
                new Vector2(0.5f), new Vector2(2.4f * scale), SpriteEffects.None, 0f);
        }
    }

    //合成三符（霖/潦/沛）的符纸物品随定义同住 Roster/FuLin|FuLao|FuPei.cs，此处只留基类
}
