using CalamityOverhaul.Content.Tiles;
using CalamityOverhaul.Content.UIs.SupertableUIs;
using InnoVault.TileProcessors;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.TileProcessors
{
    public class TramModuleTP : TileProcessor
    {
        public override int TargetTileID => ModContent.TileType<TransmutationOfMatter>();
        private const int maxleng = 120;
        private float snegs;
        private int Time;
        private bool mouseOnTile;
        internal bool drawGlow;
        internal Color gloaColor;
        private int gloawTime;
        internal int frame;
        [VaultLoaden(CWRConstant.Asset + "Tiles/TransmutationOfMatter")]
        internal static Asset<Texture2D> modeuleBodyAsset = null;
        [VaultLoaden(CWRConstant.UI + "SupertableUIs/TexturePackButtons")]
        internal static Asset<Texture2D> truesFromeAsset = null;
        public Item[] items;
        public const int itemCount_W_X_H = 81;
        internal Vector2 Center => PosInWorld + new Vector2(TransmutationOfMatter.Width, TransmutationOfMatter.Height) * 8;
        private void initializeItems() {
            items = new Item[itemCount_W_X_H];
            for (int i = 0; i < items.Length; i++) {
                items[i] = new Item();
            }
        }

        public override void SetProperty() => initializeItems();

        internal void TPEntityLoadenItems() {
            foreach (var item in items) {//这里给原版物品预加载一下纹理，如果有的话
                if (item == null || item.type == ItemID.None || item.type >= ItemID.Count) {
                    continue;
                }
                Main.instance.LoadItem(item.type);
            }
            SupertableUI.Instance.Items = items;
            if (!VaultUtils.isSinglePlayer) {
                SendData();
            }
        }

        public override void SendData(ModPacket data) {
            for (int i = 0; i < itemCount_W_X_H; i++) {
                if (items[i] == null) {
                    items[i] = new Item(0);
                }
                ItemIO.Send(items[i], data, true);
            }
        }

        public override void ReceiveData(BinaryReader reader, int whoAmI) {
            for (int i = 0; i < itemCount_W_X_H; i++) {
                items[i] = ItemIO.Receive(reader, true);
            }
            SupertableUI.Instance.Items = items;
            SupertableUI.TramTP.SendData();//不要进行网络发送，否则会迭代起来引发网络数据洪流
        }

        public override void SaveData(TagCompound tag) {
            try {
                List<TagCompound> itemTags = [];
                for (int i = 0; i < items.Length; i++) {
                    if (items[i] == null) {
                        items[i] = new Item(0);
                    }
                    itemTags.Add(ItemIO.Save(items[i]));
                }
                tag["itemTags"] = itemTags;
            } catch (Exception ex) { CWRMod.Instance.Logger.Error($"TramModuleTP.SaveData An Error Has Cccurred: {ex.Message}"); }
        }

        private void OldLoadData(TagCompound tag) {
            try {
                if (!tag.TryGet("SupertableUI_ItemDate", out Item[] loadSupUIItems)) {
                    return;
                }

                for (int i = 0; i < loadSupUIItems.Length; i++) {
                    if (loadSupUIItems[i] == null) {
                        loadSupUIItems[i] = new Item(0);
                    }
                }

                items = loadSupUIItems;
            } catch (Exception ex) { CWRMod.Instance.Logger.Error($"TramModuleTP.OldLoadData An Error Has Cccurred: {ex.Message}"); }
        }

        public override void LoadData(TagCompound tag) {
            try {
                //这一段用于适配老存档，防止新版本让老存档物品数据丢失
                OldLoadData(tag);

                if (!tag.TryGet("itemTags", out List<TagCompound> itemTags)) {
                    return;
                }

                List<Item> resultItems = [];
                for (int i = 0; i < itemTags.Count; i++) {
                    var itemTag = itemTags[i];
                    resultItems.Add(ItemIO.Load(itemTag));
                }

                items = [.. resultItems];
            } catch (Exception ex) { CWRMod.Instance.Logger.Error($"TramModuleTP.LoadData An Error Has Cccurred: {ex.Message}"); }
        }

        public override void Update() {
            VaultUtils.ClockFrame(ref frame, 6, 10);
            Time++;
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

            float leng = PosInWorld.Distance(player.Center);
            drawGlow = leng < maxleng && mouseOnTile && !SupertableUI.Instance.Active;
            if (drawGlow) {
                gloawTime++;
                gloaColor = Color.AliceBlue * MathF.Abs(MathF.Sin(gloawTime * 0.04f));
            }
            else {
                gloawTime = 0;
            }

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

            if ((leng >= maxleng || player.dead) && modPlayer.TramTPContrType == WhoAmI) {
                modPlayer.SupertableUIStartBool = false;
                modPlayer.TramTPContrType = -1;
                TPEntityLoadenItems();
                SoundEngine.PlaySound(SoundID.MenuClose with { Pitch = -0.2f });
            }
        }

        public override void OnKill() {
            CWRPlayer modPlayer = Main.LocalPlayer.CWR();
            if (modPlayer.TramTPContrType == WhoAmI) {
                modPlayer.SupertableUIStartBool = false;
                modPlayer.TramTPContrType = -1;
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
