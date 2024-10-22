using CalamityMod;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.Core;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.AmmoBoxs
{
    internal abstract class BaseAmmoBox : ModProjectile
    {
        public override string Texture => CWRConstant.Item + "Placeable/NapalmBombBox";

        private bool onProj;
        protected int maxFrameNum = 1;
        protected Vector2 drawOffsetPos;
        public int FromeThisTImeID;

        public override void SetDefaults() {
            Projectile.width = 46;
            Projectile.height = 46;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            SetAmmoBox();
        }

        public virtual void SetAmmoBox() {

        }

        public virtual bool CanClick(Item item) => item.type != ItemID.None && item.CWR().HasCartridgeHolder;

        public virtual void Preprocessing(Player player, Item item) {
            CWRItems cwr = item.CWR();
            cwr.NumberBullets = cwr.AmmoCapacity;
            cwr.IsKreload = true;
            cwr.NoKreLoadTime += 30;
            int num = 0;
            List<Item> list = [];
            foreach (Item i in cwr.MagazineContents) {
                if (i.type != ItemID.None && i.stack > 0) {
                    list.Add(i);
                }
            }
            foreach (Item i in list) {
                if (i.type != ItemID.None && i.stack > 0) {
                    num += i.stack;
                }
            }
            if (num < cwr.AmmoCapacity) {
                int ammoType = item.useAmmo == AmmoID.Rocket ? ItemID.RocketI : ItemID.MusketBall;
                if (item.useAmmo == AmmoID.FallenStar) {
                    ammoType = ItemID.FallenStar;
                }
                if (item.useAmmo == AmmoID.Gel) {
                    ammoType = ItemID.Gel;
                }
                list.Add(new Item(ammoType, cwr.AmmoCapacity - num));
            }
            cwr.MagazineContents = list.ToArray();
        }

        public virtual bool ClickBehavior(Player player, CWRItems cwr) => true;

        public virtual void ClockFrame() => CWRUtils.ClockFrame(ref Projectile.frame, 10, maxFrameNum - 1);

        public virtual void Dorp() {
            for (int i = 0; i < 9; i++) {
                Tile tile = CWRUtils.GetTile(Projectile.Bottom / 16);
                if (!(tile.HasTile && (Main.tileSolid[tile.TileType] || Main.tileSolidTop[tile.TileType]))) {
                    Projectile.position.Y += 1;
                }
                else {
                    break;
                }
            }
        }

        public override bool PreAI() {
            ClockFrame();
            return true;
        }

        public override void AI() {
            Projectile.timeLeft = 2;
            Player player = Main.player[Projectile.owner];
            float inPlayer = player.Distance(Projectile.Center);
            bool inMouse = Projectile.Hitbox.Intersects(new Rectangle((int)Main.MouseWorld.X, (int)Main.MouseWorld.Y, 1, 1));
            onProj = inPlayer < 100 && inMouse;
            if (onProj) {
                player.noThrow = 2;
                player.cursorItemIconEnabled = true;
                if (FromeThisTImeID == 0) {
                    FromeThisTImeID = ModContent.ItemType<Items.Placeable.AmmoBoxFire>();
                }
                player.cursorItemIconID = FromeThisTImeID;
                if (player.CWR().TryGetInds_BaseFeederGun(out BaseFeederGun gun)) {
                    gun.ShootCoolingValue = gun.CanRightClick ? 10 : 2;//因为和一些枪械的右键功能按键冲突，所以要额外设置一个长一些的时间
                }
                if (player.PressKey(false)) {
                    Item item = player.ActiveItem();
                    if (CanClick(item)) {
                        Preprocessing(player, item);
                        if (ClickBehavior(player, item.CWR())) {
                            Projectile.Kill();
                        }
                    }
                }
            }
            Dorp();
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D value = CWRUtils.GetT2DValue(Texture);
            Main.EntitySpriteDraw(value, Projectile.Center - Main.screenPosition + drawOffsetPos, CWRUtils.GetRec(value, Projectile.frame, maxFrameNum)
                , onProj ? Color.Gold : Color.White, Projectile.rotation, CWRUtils.GetOrig(value, maxFrameNum) + new Vector2(0, -2), Projectile.scale, SpriteEffects.None, 0);
            return false;
        }
    }
}
