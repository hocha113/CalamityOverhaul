using CalamityOverhaul.Content.Tiles;
using CalamityOverhaul.Content.UIs.SupertableUIs;
using InnoVault.TileProcessors;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.TileModules
{
    internal class TramModuleTP : TileProcessor, ICWRLoader
    {
        public override int TargetTileID => ModContent.TileType<TransmutationOfMatter>();
        private const int maxleng = 120;
        private float snegs;
        private int Time;
        private bool mouseOnTile;
        internal static Asset<Texture2D> modeuleBodyAsset;
        internal static Asset<Texture2D> truesFromeAsset;
        public Item[] items;
        public const int itemCount = 81;
        internal Vector2 Center => PosInWorld + new Vector2(TransmutationOfMatter.Width, TransmutationOfMatter.Height) * 8;
        void ICWRLoader.LoadAsset() {
            modeuleBodyAsset = CWRUtils.GetT2DAsset(CWRConstant.Asset + "Tiles/TransmutationOfMatter");
            truesFromeAsset = CWRUtils.GetT2DAsset(CWRConstant.UI + "SupertableUIs/TexturePackButtons");
        }
        void ICWRLoader.UnLoadData() => modeuleBodyAsset = truesFromeAsset = null;
        private void initializeItems() {
            items = new Item[itemCount];
            for (int i = 0; i < items.Length; i++) {
                items[i] = new Item();
            }
        }
        public override void SetProperty() => initializeItems();
        internal void tpEntityLoadenItems() {
            SupertableUI.Instance.items = items;
            if (!VaultUtils.isSinglePlayer) {
                SendData();
            }
        }
        public override void SendData(ModPacket data) {
            for (int i = 0; i < itemCount; i++) {
                if (items[i] == null) {
                    items[i] = new Item(0);
                }
                data.Write(items[i].type);
                data.Write(items[i].stack);
            }
        }
        public override void ReceiveData(BinaryReader reader, int whoAmI) {
            for (int i = 0; i < itemCount; i++) {
                int type = reader.ReadInt32();
                int stack = reader.ReadInt32();
                if (type < 0 || type >= ItemLoader.ItemCount) {
                    type = ItemID.None;
                }
                items[i] = new Item(type);
                if (type > 0) {
                    items[i].stack = stack;
                }
            }
        }
        public override void SaveData(TagCompound tag) {
            for (int i = 0; i < items.Length; i++) {
                if (items[i] == null) {
                    items[i] = new Item(0);
                }
            }
            tag.Add("SupertableUI_ItemDate", items);
        }
        public override void LoadData(TagCompound tag) {
            if (tag.TryGet("SupertableUI_ItemDate", out Item[] loadSupUIItems)) {
                for (int i = 0; i < loadSupUIItems.Length; i++) {
                    if (loadSupUIItems[i] == null) {
                        loadSupUIItems[i] = new Item(0);
                    }
                }
                items = loadSupUIItems;
            }
        }

        public override void Update() {
            Player player = Main.LocalPlayer;
            if (!player.active || Main.myPlayer != player.whoAmI) {
                return;
            }

            if (mouseOnTile || snegs > 0) {
                Lighting.AddLight(Center, Color.White.ToVector3());
            }

            CWRPlayer modPlayer = player.CWR();

            if (VaultUtils.isServer) {
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
                tpEntityLoadenItems();
                SoundEngine.PlaySound(SoundID.MenuClose with { Pitch = -0.2f });
            }
        }

        public override void OnKill() {
            CWRPlayer modPlayer = Main.LocalPlayer.CWR();
            if (modPlayer.TETramContrType == WhoAmI) {
                modPlayer.SupertableUIStartBool = false;
                modPlayer.TETramContrType = -1;
            }
            if (!VaultUtils.isClient) {
                foreach (var item in items) {
                    if (item.IsAir) {
                        continue;
                    }
                    int type = Item.NewItem(new EntitySource_WorldGen(), Center, item);
                    if (VaultUtils.isServer) {
                        NetMessage.SendData(MessageID.SyncItem, -1, -1, null, type, 0f, 0f, 0f, 0, 0, 0);
                    }
                }
            }
            initializeItems();
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
