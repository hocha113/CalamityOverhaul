using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.DataStructures;
using Terraria.Enums;
using Terraria.GameContent.Drawing;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace CalamityOverhaul.Content.NPCs.SeaShrimp
{
    /// <summary>渊晶海虾圣物，大师模式击杀必掉的摆件</summary>
    internal class SeaShrimpRelic : ModItem
    {
        public override string Texture => CWRConstant.NPC + "SeaShrimp/SeaShrimpRelic";

        public override void SetDefaults() {
            Item.DefaultToPlaceableTile(ModContent.TileType<SeaShrimpRelicTile>());
            Item.width = 30;
            Item.height = 40;
            Item.rare = ItemRarityID.Master;
            Item.master = true;
            Item.value = Item.sellPrice(0, 1);
        }
    }

    /// <summary>
    /// 圣物瓦。3x4 规格与两帧朝向照原版大师圣物；瓦面只画底座，
    /// 金虾雕像不在瓦面上，由 <see cref="SpecialDraw"/> 悬浮绘制并上下浮动
    /// </summary>
    internal class SeaShrimpRelicTile : ModTile
    {
        public override string Texture => CWRConstant.NPC + "SeaShrimp/SeaShrimpRelicTile";

        //客户端 PostSetupContent 加载,服务端为空,绘制侧判空
        [VaultLoaden(CWRConstant.NPC + "SeaShrimp/SeaShrimpRelicStatue")]
        public static Asset<Texture2D> StatueTex = null;

        private const int FrameWidth = 18 * 3;
        private const int FrameHeight = 18 * 4;

        public override void SetStaticDefaults() {
            RegisterItemDrop(ModContent.ItemType<SeaShrimpRelic>());

            Main.tileShine[Type] = 400;
            Main.tileFrameImportant[Type] = true;
            TileID.Sets.InteractibleByNPCs[Type] = true;

            TileObjectData.newTile.CopyFrom(TileObjectData.Style3x4);
            TileObjectData.newTile.LavaDeath = false;
            TileObjectData.newTile.DrawYOffset = 2;
            TileObjectData.newTile.Direction = TileObjectDirection.PlaceLeft;
            TileObjectData.newTile.StyleHorizontal = false;
            TileObjectData.newAlternate.CopyFrom(TileObjectData.newTile);
            TileObjectData.newAlternate.Direction = TileObjectDirection.PlaceRight;
            TileObjectData.addAlternate(1);
            TileObjectData.addTile(Type);

            AddMapEntry(new Color(233, 207, 94), Language.GetText("MapObject.Relic"));
        }

        public override bool CreateDust(int i, int j, ref int type) => false;

        public override void DrawEffects(int i, int j, SpriteBatch spriteBatch, ref TileDrawInfo drawData) {
            //雕像不在瓦面上,把左上角注册成特殊绘制点交给 SpecialDraw
            if (drawData.tileFrameX % FrameWidth == 0 && drawData.tileFrameY % FrameHeight == 0) {
                Main.instance.TilesRenderer.AddSpecialPoint(i, j, TileDrawing.TileCounterType.CustomNonSolid);
            }
        }

        public override void SpecialDraw(int i, int j, SpriteBatch spriteBatch) {
            Tile tile = Main.tile[i, j];
            if (!tile.HasTile || StatueTex?.Value == null) {
                return;
            }

            Texture2D texture = StatueTex.Value;
            Rectangle frame = texture.Frame();
            Vector2 origin = frame.Size() / 2f;
            Vector2 worldPos = new Point(i, j).ToWorldCoordinates(24f, 64f);
            Color color = Lighting.GetColor(i, j);
            //按放置朝向翻转雕像
            bool flipped = tile.TileFrameY / FrameHeight != 0;
            SpriteEffects effects = flipped ? SpriteEffects.FlipHorizontally : SpriteEffects.None;

            //雕像悬浮在底座上方,缓慢上下浮动
            float bob = MathF.Sin(Main.GlobalTimeWrappedHourly * MathHelper.TwoPi / 5f);
            Vector2 drawPos = worldPos - Main.screenPosition + new Vector2(0f, -44f + bob * 4f);
            spriteBatch.Draw(texture, drawPos, frame, color, 0f, origin, 1f, effects, 0f);

            //周期辉光环,加色不遮暗
            float pulse = MathF.Sin(Main.GlobalTimeWrappedHourly * MathHelper.TwoPi / 2f) * 0.3f + 0.7f;
            Color glow = color;
            glow.A = 0;
            glow *= 0.1f * pulse;
            float ring = 6f + bob * 2f;
            for (float k = 0f; k < 1f; k += 1f / 6f) {
                spriteBatch.Draw(texture, drawPos + (MathHelper.TwoPi * k).ToRotationVector2() * ring
                    , frame, glow, 0f, origin, 1f, effects, 0f);
            }
        }
    }
}
