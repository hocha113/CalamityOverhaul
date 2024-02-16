using CalamityMod;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.TileEntitys;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.Enums;
using Terraria.GameContent.ObjectInteractions;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace CalamityOverhaul.Content.Tiles
{
    internal class BloodAltar : ModTile
    {
        public override string Texture => CWRConstant.Asset + "Tiles/" + "BloodAltar";
        public const int Width = 4;
        public const int Height = 3;
        public const int OriginOffsetX = 1;
        public const int OriginOffsetY = 1;
        public const int SheetSquare = 18;

        public override void SetStaticDefaults() {
            Main.tileLighted[Type] = true;
            Main.tileFrameImportant[Type] = true;
            Main.tileNoAttach[Type] = true;
            Main.tileLavaDeath[Type] = false;
            Main.tileWaterDeath[Type] = false;
            TileObjectData.newTile.CopyFrom(TileObjectData.Style3x3);
            TileObjectData.newTile.Width = Width;
            TileObjectData.newTile.Height = Height;
            TileObjectData.newTile.Origin = new Point16(OriginOffsetX, OriginOffsetY);
            TileObjectData.newTile.AnchorBottom = new AnchorData(AnchorType.SolidTile | AnchorType.SolidWithTop | AnchorType.SolidSide, TileObjectData.newTile.Width, 0);
            TileObjectData.newTile.CoordinateHeights = new int[] { 16, 16, 16 };
            TileObjectData.newTile.LavaDeath = false;
            ModTileEntity te = ModContent.GetInstance<BloodAltarEntity>();
            TileObjectData.newTile.HookPostPlaceMyPlayer = new PlacementHook(te.Hook_AfterPlacement, -1, 0, true);
            TileObjectData.newTile.UsesCustomCanPlace = true;
            TileObjectData.addTile(Type);
            AddMapEntry(Color.Red, CWRUtils.SafeGetItemName<Items.Placeable.BloodAltar>());
            AnimationFrameHeight = 54;

            AdjTiles = new int[] {
                TileID.DemonAltar
            };
        }

        public override bool CanExplode(int i, int j) => false;

        public override bool CreateDust(int i, int j, ref int type) {
            Dust.NewDust(new Vector2(i, j) * 16f, 16, 16, DustID.Electric);
            return false;
        }

        public override bool HasSmartInteract(int i, int j, SmartInteractScanSettings settings) => true;

        public override void NumDust(int i, int j, bool fail, ref int num) => num = fail ? 1 : 3;

        public override void KillMultiTile(int i, int j, int frameX, int frameY) {
            ModContent.GetInstance<BloodAltarEntity>().Kill(i, j);
            Main.dayTime = true;
            if (Main.bloodMoon) {
                Main.bloodMoon = false;
            }
        }

        public override bool RightClick(int i, int j) {
            Vector2 Center = new Vector2(i, j) * 16 + new Vector2(8 * Width, 8 * Height);

            BloodAltarEntity bloodAltarEntity = CalamityUtils.FindTileEntity<BloodAltarEntity>(i, j, Width, Height, SheetSquare);
            if (bloodAltarEntity != null)
                bloodAltarEntity.OnBoolMoon = !bloodAltarEntity.OnBoolMoon;

            if (CWRUtils.isClient) {//这段代码用于在多人模式下进行的功能性补救，天杀的为什么物块实体在多人模式会消失？
                if (!Main.bloodMoon) {
                    SoundEngine.PlaySound(SoundID.Roar, Center);
                    for (int o = 0; o < 63; o++) {
                        Vector2 vr = new Vector2(Main.rand.Next(-12, 12), Main.rand.Next(-23, -3));
                        Dust.NewDust(Center - new Vector2(16, 16), 32, 32, DustID.Blood, vr.X, vr.Y
                            , Scale: Main.rand.NextFloat(1.2f, 3.1f));
                    }
                    Main.dayTime = false;
                    Main.bloodMoon = true;
                    CalamityNetcode.SyncWorld();
                }
                else {
                    SoundEngine.PlaySound(CWRSound.Peuncharge, Center);
                    for (int o = 0; o < 133; o++) {
                        Vector2 vr = new Vector2(0, Main.rand.Next(-33, -3));
                        Dust.NewDust(Center - new Vector2(16, 16), 32, 32, DustID.Blood, vr.X, vr.Y
                        , Scale: Main.rand.NextFloat(0.7f, 1.3f));
                    }
                    Main.dayTime = true;
                    Main.bloodMoon = false;
                    CalamityNetcode.SyncWorld();
                }
            }
            
            TileEntity.InitializeAll();
            Recipe.FindRecipes();
            return true;
        }

        int ClinetIndexFrame;
        public override bool PreDraw(int i, int j, SpriteBatch spriteBatch) {
            Tile t = Main.tile[i, j];
            int frameXPos = t.TileFrameX;
            int frameYPos = t.TileFrameY;

            BloodAltarEntity bloodAltarEntity = CalamityUtils.FindTileEntity<BloodAltarEntity>(i, j, Width, Height, SheetSquare);

            if (bloodAltarEntity != null) {
                frameYPos += bloodAltarEntity.frameIndex % 4 * (Height * SheetSquare);
            }
            else if (CWRUtils.isClient) {
                CWRUtils.ClockFrame(ref ClinetIndexFrame, 6, 3);
                frameYPos += ClinetIndexFrame % 4 * (Height * SheetSquare);
            }    

            Texture2D tex = ModContent.Request<Texture2D>(Texture).Value;
            Vector2 offset = Main.drawToScreen ? Vector2.Zero : new Vector2(Main.offScreenRange);
            Vector2 drawOffset = new Vector2(i * 16 - Main.screenPosition.X, j * 16 - Main.screenPosition.Y) + offset;
            Color drawColor = Lighting.GetColor(i, j);

            if (!t.IsHalfBlock && t.Slope == 0)
                spriteBatch.Draw(tex, drawOffset, new Rectangle(frameXPos, frameYPos, 16, 16)
                    , drawColor, 0.0f, Vector2.Zero, 1f, SpriteEffects.None, 0.0f);
            else if (t.IsHalfBlock)
                spriteBatch.Draw(tex, drawOffset + Vector2.UnitY * 8f, new Rectangle(frameXPos, frameYPos, 16, 16)
                    , drawColor, 0.0f, Vector2.Zero, 1f, SpriteEffects.None, 0.0f);
            return false;
        }
    }
}
