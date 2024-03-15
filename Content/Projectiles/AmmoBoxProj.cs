using CalamityMod;
using CalamityOverhaul.Common;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles
{
    internal class AmmoBoxProj : ModProjectile
    {
        public override string Texture => CWRConstant.Item + "Placeable/AmmoBox";
        bool onProj;
        public override void SetDefaults() {
            Projectile.width = 46;
            Projectile.height = 46;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.MaxUpdates = 8;
        }

        public override void AI() {
            CWRUtils.ClockFrame(ref Projectile.frame, 10, 38);
            Projectile.timeLeft = 2;
            Player player = Main.player[Projectile.owner];
            float inPlayer = player.Distance(Projectile.Center);
            bool inMouse = Projectile.Hitbox.Intersects(new Rectangle((int)Main.MouseWorld.X, (int)Main.MouseWorld.Y, 1, 1));
            onProj = inPlayer < 100 && inMouse;
            if (onProj) {
                player.noThrow = 2;
                player.cursorItemIconEnabled = true;
                player.cursorItemIconID = ModContent.ItemType<Items.Placeable.AmmoBoxFire>();
                if (player.PressKey(false)) {
                    Item item = player.ActiveItem();
                    if (item.type != ItemID.None && item.CWR().HasCartridgeHolder) {
                        SoundEngine.PlaySound(CWRSound.loadTheRounds, Projectile.Center);
                        CWRItems cWR = item.CWR();
                        cWR.NumberBullets = cWR.AmmoCapacity;
                        cWR.IsKreload = true;
                        cWR.NoKreLoadTime += 30;
                        int num = 0;
                        List<Item> list = new List<Item>();
                        foreach (Item i in cWR.MagazineContents) {
                            if (i.type != ItemID.None && i.stack > 0) {
                                list.Add(i);
                            }
                        }
                        foreach(Item i in list) {
                            if (i.type != ItemID.None && i.stack > 0) {
                                num += i.stack;
                            }
                        }
                        if (num < cWR.AmmoCapacity) {
                            list.Add(new Item(item.useAmmo == AmmoID.Rocket ? ItemID.RocketI : ItemID.MusketBall, cWR.AmmoCapacity - num));
                        }
                        cWR.MagazineContents = list.ToArray();
                        cWR.AmmoCapacityInFire = true;
                        Projectile.Kill();
                    }
                }
            }
            Tile tile = CWRUtils.GetTile(Projectile.Bottom / 16);
            if (!(tile.HasTile && (Main.tileSolid[tile.TileType] || Main.tileSolidTop[tile.TileType]))) {
                //for (int i = 0; i < 8; i++) {
                //    Projectile.position.Y += 1;
                //    tile = CWRUtils.GetTile(Projectile.Bottom / 16);
                //    if (tile.HasSolidTile()) {
                //        break;
                //    }
                //}
                Projectile.position.Y += 1;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D value = CWRUtils.GetT2DValue(Texture);
            Main.EntitySpriteDraw(value, Projectile.Center - Main.screenPosition, CWRUtils.GetRec(value, Projectile.frame, 39)
                , onProj ? Color.Gold : Color.White, Projectile.rotation, CWRUtils.GetOrig(value, 39) + new Vector2(0, -2), Projectile.scale, SpriteEffects.None, 0);
            return false;
        }
    }
}
