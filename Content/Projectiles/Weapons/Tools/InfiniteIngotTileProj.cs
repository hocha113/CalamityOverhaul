using CalamityMod.Graphics.Metaballs;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Tools;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Tools
{
    internal class InfiniteIngotTileProj : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;
        public List<Item> items = [];
        private DarkMatterBall darkMatterBall;
        public override void SetDefaults() {
            Projectile.width = Projectile.height = 32;
            Projectile.penetrate = -1;
            Projectile.hostile = true;
            Projectile.friendly = true;
            Projectile.timeLeft = 300;
            darkMatterBall = (DarkMatterBall)new Item(ModContent.ItemType<DarkMatterBall>()).ModItem;
        }

        public void ExTile() {
            if (!Main.zenithWorld) {
                return;
            }
            int maxNum = (int)(12 * Projectile.scale);
            int offset = maxNum / -2;
            int rSquared = maxNum * maxNum / 4;
            Vector2 pos = Projectile.Center / 16;
            for (int x = 0; x < maxNum; x++) {
                for (int y = 0; y < maxNum; y++) {
                    Vector2 pos2 = new Vector2(offset + x, offset + y) + pos;
                    if (pos2.To(pos).LengthSquared() < rSquared) {
                        Tile tile = CWRUtils.GetTile(pos2);
                        if (tile.HasTile) {
                            if (Main.rand.NextBool(6)) {
                                int dorptype = CWRUtils.GetTileDorp(tile);
                                if (dorptype != 0) {
                                    darkMatterBall.DorpItems.Add(new Item(dorptype));
                                }
                            }
                            tile.HasTile = false;
                        }
                        tile.LiquidAmount = 0;
                        tile.WallType = 0;
                    }
                }
            }
        }

        public override void AI() {
            Projectile.scale += 0.01f;
            if (Projectile.timeLeft % 10 == 0) {
                SoundEngine.PlaySound(CWRSound.BlackHole, Projectile.Center);
                ExTile();
            }

            for (int i = 0; i < Main.item.Length; i++) {
                Item item = Main.item[i];
                if (item.Center.To(Projectile.Center).LengthSquared() < 32 * 32) {
                    Main.item[i] = new Item();
                }
            }
            if (Main.netMode != NetmodeID.Server) {
                for (int i = 0; i < 2; i++) {
                    Vector2 spawnPosition = Projectile.Center;
                    StreamGougeMetaball.SpawnParticle(spawnPosition, Main.rand.NextVector2Circular(3f, 3f), 215f * Projectile.scale);
                }

                for (int i = 0; i < 2; i++) {
                    Vector2 spawnPosition = Projectile.Center;
                    StreamGougeMetaball.SpawnParticle(spawnPosition, Main.rand.NextVector2Circular(3f, 3f), 215f * Projectile.scale);
                }
            }
        }

        public override void OnKill(int timeLeft) {
            if (darkMatterBall.DorpItems.Count > 0 && Projectile.IsOwnedByLocalPlayer()) {
                int type = Item.NewItem(Projectile.FromObjectGetParent(), Projectile.Hitbox, darkMatterBall.Item);
                if (VaultUtils.isClient) {
                    NetMessage.SendData(MessageID.SyncItem, -1, -1, null, type, 0f, 0f, 0f, 0, 0, 0);
                }
            }
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            return VaultUtils.CircleIntersectsRectangle(Projectile.Center, Projectile.scale * 96, targetHitbox);
        }
    }
}
