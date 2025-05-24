using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Placeable;
using CalamityOverhaul.Content.RangedModify.Core;
using InnoVault.GameContent.BaseEntity;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.AmmoBoxs
{
    internal abstract class BaseAmmoBox : BaseHeldProj
    {
        public override string Texture => CWRConstant.Item + "Placeable/NapalmBombBox";
        protected bool mouseInBox;
        private bool onDorp;
        private int dorpDistank;
        public int FromeThisTImeID;
        private bool oldDownRight;
        public override void SetDefaults() {
            Projectile.width = 46;
            Projectile.height = 36;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.friendly = true;
            Projectile.penetrate = -1;
            SetAmmoBox();
        }

        public virtual void SetAmmoBox() {

        }

        public override void Initialize() {
            if (VaultUtils.isServer) {
                return;
            }
            Texture2D value = TextureAssets.Projectile[Type].Value;
            Projectile.width = value.Width;
            Projectile.height = value.Height;
            Projectile.netUpdate = true;
        }

        public virtual bool CanClick(Item item) => item.type != ItemID.None && item.CWR().HasCartridgeHolder;

        public virtual void Preprocessing(Player player, Item item) {
            CWRItems cwrItem = item.CWR();
            cwrItem.NumberBullets = cwrItem.AmmoCapacity;
            cwrItem.IsKreload = true;
            cwrItem.NoKreLoadTime = 30;
            int num = 0;
            List<Item> list = [];
            foreach (Item i in cwrItem.MagazineContents) {
                if (i.type != ItemID.None && i.stack > 0) {
                    list.Add(i);
                }
            }
            foreach (Item i in list) {
                if (i.type != ItemID.None && i.stack > 0) {
                    num += i.stack;
                }
            }
            if (num < cwrItem.AmmoCapacity) {
                int ammoType = ItemID.MusketBall;
                if (VaultUtils.AmmoIDToItemIDMapping.TryGetValue(item.useAmmo, out int ammoItemID)) {
                    ammoType = ammoItemID;
                }
                list.Add(new Item(ammoType, cwrItem.AmmoCapacity - num));
            }
            cwrItem.SetMagazine(list);
        }

        public virtual bool ClickBehavior(Player player, CWRItems cwr) => true;

        public override void AI() {
            Projectile.timeLeft = 2;
            Player player = Main.LocalPlayer;
            float inPlayer = player.Distance(Projectile.Center);
            bool rightPrmd = !oldDownRight && DownRight;
            oldDownRight = DownRight;
            mouseInBox = Projectile.Hitbox.Intersects(new Rectangle((int)Main.MouseWorld.X, (int)Main.MouseWorld.Y, 1, 1));
            if (inPlayer < 100 && mouseInBox) {
                player.noThrow = 2;
                player.cursorItemIconEnabled = true;
                if (FromeThisTImeID == 0) {
                    FromeThisTImeID = ModContent.ItemType<AmmoBoxFire>();
                }
                player.cursorItemIconID = FromeThisTImeID;
                if (player.CWR().TryGetInds_BaseFeederGun(out BaseFeederGun gun)) {
                    gun.ShootCoolingValue = gun.CanRightClick ? 10 : 2;//因为和一些枪械的右键功能按键冲突，所以要额外设置一个长一些的时间
                }
                if (rightPrmd && Projectile.IsOwnedByLocalPlayer()) {
                    Item item = player.GetItem();
                    if (CanClick(item)) {
                        Preprocessing(player, item);
                        if (ClickBehavior(player, item.IsAir ? null : item.CWR())) {
                            Projectile.Kill();
                        }
                    }
                    else {
                        player.QuickSpawnItem(Projectile.FromObjectGetParent(), new Item(FromeThisTImeID));
                        Projectile.Kill();
                    }
                    Projectile.netUpdate = true;
                    return;
                }
            }
            if (player.Hitbox.Intersects(Projectile.Hitbox) && !player.controlDown) {
                player.CWR().ReceivingPlatformTime = 2;
            }
            Dorp();
        }

        private static bool TileIndsdm(Tile tile) => tile.HasTile && (Main.tileSolid[tile.TileType] || Main.tileSolidTop[tile.TileType]);

        public virtual void Dorp() {
            Projectile indsdmProj = null;
            for (int i = 0; i < 13; i++) {
                bool boxIndsdm = false;
                foreach (var proj in Main.ActiveProjectiles) {
                    if (proj.whoAmI == Projectile.whoAmI) {
                        continue;
                    }
                    if (proj.position.Y <= Projectile.position.Y) {
                        continue;
                    }
                    if (proj.ModProjectile is BaseAmmoBox box) {
                        if (proj.Hitbox.Intersects(Projectile.Hitbox)) {
                            boxIndsdm = true;
                            indsdmProj = proj;
                        }
                    }
                }

                bool indsdm = false;
                if (TileIndsdm(Framing.GetTileSafely(Projectile.Bottom / 16))
                    || TileIndsdm(Framing.GetTileSafely(Projectile.Bottom / 16))
                    || TileIndsdm(Framing.GetTileSafely(Projectile.BottomRight / 16))) {
                    indsdm = true;
                }

                if (indsdm || boxIndsdm) {
                    if (!onDorp) {
                        SoundEngine.PlaySound(CWRSound.DeploymentSound, Projectile.Center);
                        if (dorpDistank > 16) {
                            int num = dorpDistank / 8;
                            if (num > 33) {
                                num = 33;
                            }
                            for (int z = 0; z < num; z++) {
                                int stompDust = Dust.NewDust(Projectile.BottomLeft, Projectile.width, 4, DustID.Smoke, 0f, 0f, 100, default, 1.5f);
                                Main.dust[stompDust].velocity *= 0.2f;
                            }
                        }
                        onDorp = true;
                        dorpDistank = 0;
                        Projectile.damage = 0;
                    }
                    break;
                }
                else {
                    onDorp = false;
                    Projectile.position.Y += 1;
                    dorpDistank++;
                    Projectile.damage = dorpDistank * 2;
                    if (Projectile.damage > 500) {
                        Projectile.damage = 500;
                    }
                }
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D value = TextureAssets.Projectile[Type].Value;
            Vector2 drawPos = Projectile.BottomLeft - new Vector2(0, value.Height) - Main.screenPosition;
            Main.EntitySpriteDraw(value, drawPos, CWRUtils.GetRec(value)
                , mouseInBox ? Color.Gold : lightColor, Projectile.rotation, Vector2.Zero, Projectile.scale, SpriteEffects.None, 0);
            return false;
        }
    }
}
