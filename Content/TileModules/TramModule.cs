using CalamityOverhaul.Content.TileModules.Core;
using CalamityOverhaul.Content.Tiles;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.TileModules
{
    internal class TramModule : BaseTileModule, ICWRLoader
    {
        public override int TargetTileID => ModContent.TileType<TransmutationOfMatter>();
        private const int maxleng = 300;
        private float snegs;
        private int Time;
        private bool mouseOnTile;
        internal static Asset<Texture2D> modeuleBodyAsset;
        internal static Asset<Texture2D> truesFromeAsset;
        internal Vector2 Center => PosInWorld + new Vector2(TransmutationOfMatter.Width, TransmutationOfMatter.Height) * 8;
        void ICWRLoader.LoadAsset() {
            modeuleBodyAsset = CWRUtils.GetT2DAsset(CWRConstant.Asset + "Tiles/TransmutationOfMatter");
            truesFromeAsset = CWRUtils.GetT2DAsset(CWRConstant.UI + "SupertableUIs/TexturePackButtons");
        }
        void ICWRLoader.UnLoad() => modeuleBodyAsset = truesFromeAsset = null;
        public override void Update() {
            Player player = Main.LocalPlayer;
            if (!player.active || Main.myPlayer != player.whoAmI) {
                return;
            }

            if (mouseOnTile || snegs > 0) {
                Lighting.AddLight(Center, Color.White.ToVector3());
            }

            CWRPlayer modPlayer = player.CWR();

            if (CWRUtils.isServer) {
                return;
            }

            Rectangle tileRec = new Rectangle(Position.X * 16, Position.Y * 16, BloodAltar.Width * 18, BloodAltar.Height * 18);
            mouseOnTile = tileRec.Intersects(new Rectangle((int)Main.MouseWorld.X, (int)Main.MouseWorld.Y, 1, 1));
            Time++;
            if (modPlayer.InspectOmigaTime > 0) {
                if (snegs < 1) {
                    snegs += 0.1f;
                }
            }
            else {
                if (snegs > 0) {
                    snegs -= 0.1f;
                    if (snegs < 0) {
                        snegs = 0;
                    }
                }
            }

            if (!modPlayer.SupertableUIStartBool) {
                return;
            }

            float leng = PosInWorld.Distance(player.Center);
            if ((leng >= maxleng || player.dead) && modPlayer.TETramContrType == WhoAmI) {
                modPlayer.SupertableUIStartBool = false;
                modPlayer.TETramContrType = -1;
                SoundEngine.PlaySound(SoundID.MenuClose with { Pitch = -0.2f });
            }
        }

        public override void OnKill() {
            CWRPlayer modPlayer = Main.LocalPlayer.CWR();
            if (modPlayer.TETramContrType == WhoAmI) {
                modPlayer.SupertableUIStartBool = false;
                modPlayer.TETramContrType = -1;
            }
        }

        public override void Draw(SpriteBatch spriteBatch) {
            if (snegs <= 0) {
                return;
            }
            float truesTime = MathF.Sin(Time * 0.14f);
            Rectangle rec = new Rectangle(0, 0, truesFromeAsset.Width() / 2, truesFromeAsset.Height() / 2);
            Vector2 pos = Center + new Vector2(0, -40) - new Vector2(2, truesTime * 8);
            pos -= Main.screenPosition;
            spriteBatch.Draw(truesFromeAsset.Value, pos, rec, Color.Gold * snegs
                , MathHelper.Pi, rec.Size() / 2, 1f + truesTime * 0.1f, SpriteEffects.None, 0);
        }
    }
}
