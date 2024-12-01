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
        private bool onDorp;
        protected int maxFrameNum = 1;
        protected Vector2 drawOffsetPos;
        public int FromeThisTImeID;

        public override void SetDefaults() {
            Projectile.width = 46;
            Projectile.height = 36;
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
            for (int i = 0; i < 13; i++) {
                Tile tile = CWRUtils.GetTile(Projectile.Bottom / 16);
                if (!(tile.HasTile && (Main.tileSolid[tile.TileType] || Main.tileSolidTop[tile.TileType]))) {
                    Projectile.position.Y += 1;
                }
                else {
                    if (!onDorp) {
                        for (int z = 0; z < 33; z++) {
                            int stompDust = Dust.NewDust(Projectile.BottomLeft, Projectile.width, 4, DustID.Smoke, 0f, 0f, 100, default, 1.5f);
                            Main.dust[stompDust].velocity *= 0.2f;
                        }
                        onDorp = true;
                    }
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
                if (player.PressKey(false) && Projectile.IsOwnedByLocalPlayer()) {
                    Item item = player.GetItem();
                    if (CanClick(item)) {
                        Preprocessing(player, item);
                        if (ClickBehavior(player, item.CWR())) {
                            Projectile.Kill();
                        }
                    }
                    else {
                        player.QuickSpawnItem(Projectile.FromObjectGetParent(), new Item(FromeThisTImeID));
                        Projectile.Kill();
                    }
                    return;
                }
            }
            Dorp();
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D value = CWRUtils.GetT2DValue(Texture);
            Vector2 drawPos = Projectile.BottomLeft - new Vector2(0, value.Height) - Main.screenPosition;
            Main.EntitySpriteDraw(value, drawPos, CWRUtils.GetRec(value)
                , onProj ? Color.Gold : lightColor, Projectile.rotation, Vector2.Zero, Projectile.scale, SpriteEffects.None, 0);
            return false;
        }
    }
}
