using CalamityOverhaul.Content.Industrials.MaterialFlow.Pipelines;
using CalamityOverhaul.Content.Industrials.Modifys;
using InnoVault.TileProcessors;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.Industrials
{
    public abstract class MachineTP : TileProcessor
    {
        public MachineData MachineData { get; set; }
        public virtual float MaxUEValue => 1000;
        public virtual int TargetItem => ItemID.None;
        public virtual bool CanDrop => true;
        public int Efficiency = 2;
        public virtual MachineData GetGeneratorDataInds() => new MachineData();
        public sealed override void SetProperty() {
            MachineData ??= GetGeneratorDataInds();
            PlaceNet = true;//因为下面会进行一些初始化的数值加载，所以开启放置时的网络协调
            if (TrackItem != null) {
                MachineData.UEvalue = TrackItem.CWR().UEValue;
                if (MachineData.UEvalue > MaxUEValue) {
                    MachineData.UEvalue = MaxUEValue;
                }
            }
            SetMachine();
        }

        public virtual void SetMachine() {

        }

        public sealed override void Update() {
            if (Efficiency > 0) {
                UpdateConductive();
            }
            UpdateMachine();
        }

        public virtual void UpdateMachine() {

        }

        public override void SendData(ModPacket data) {
            MachineData?.SendData(data);
        }

        public override void ReceiveData(BinaryReader reader, int whoAmI) {
            MachineData?.ReceiveData(reader, whoAmI);
        }

        public override void SaveData(TagCompound tag) {
            MachineData?.SaveData(tag);
        }

        public override void LoadData(TagCompound tag) {
            MachineData?.LoadData(tag);
        }

        public void DropItem(int id) => DropItem(new Item(id));

        public void DropItem(Item item) {
            int type = Item.NewItem(new EntitySource_WorldEvent(), HitBox, item);
            if (VaultUtils.isServer) {
                NetMessage.SendData(MessageID.SyncItem, -1, -1, null, type, 0f, 0f, 0f, 0, 0, 0);
            }
        }

        public void UpdateConductive() {
            // 存储所有相关物块（机械物块本身 + 相邻管道）
            List<BaseUEPipelineTP> connectedTiles = new List<BaseUEPipelineTP>();
            int tileWidth = Width / 16;
            int tileHeight = Height / 16;
            // 检测四周的管道（上下左右）
            // 上边界
            for (int i = Position.X; i < Position.X + tileWidth; i++) {
                Point16 point = new Point16(i, Position.Y - 1);
                if (TileProcessorLoader.ByPositionGetTP(point, out var tp) && tp is BaseUEPipelineTP pipelineTP) {
                    connectedTiles.Add(pipelineTP);
                }
            }

            // 下边界
            for (int i = Position.X; i < Position.X + tileWidth; i++) {
                Point16 point = new Point16(i, Position.Y + tileHeight);
                if (TileProcessorLoader.ByPositionGetTP(point, out var tp) && tp is BaseUEPipelineTP pipelineTP) {
                    connectedTiles.Add(pipelineTP);
                }
            }

            // 左边界
            for (int j = Position.Y; j < Position.Y + tileHeight; j++) {
                Point16 point = new Point16(Position.X - 1, j);
                if (TileProcessorLoader.ByPositionGetTP(point, out var tp) && tp is BaseUEPipelineTP pipelineTP) {
                    connectedTiles.Add(pipelineTP);
                }
            }

            // 右边界
            for (int j = Position.Y; j < Position.Y + tileHeight; j++) {
                Point16 point = new Point16(Position.X + tileWidth, j);
                if (TileProcessorLoader.ByPositionGetTP(point, out var tp) && tp is BaseUEPipelineTP pipelineTP) {
                    connectedTiles.Add(pipelineTP);
                }
            }

            // 去重（防止重复添加同一管道）
            connectedTiles = [.. connectedTiles.Distinct()];

            // 如果没有相邻管道，直接返回
            if (connectedTiles.Count == 0) {
                return;
            }

            // 计算总电量和平均电量
            float totalUE = 0f;
            foreach (var tile in connectedTiles) {
                if (tile.MachineData != null) {
                    totalUE += tile.MachineData.UEvalue;
                }
            }
            float averageUE = totalUE / connectedTiles.Count;

            // 考虑效率限制，平衡电量
            float efficiency = this.Efficiency;
            foreach (var tile in connectedTiles) {
                if (tile.MachineData == null)
                    continue;

                float transferUE = Math.Min(efficiency, Math.Abs(tile.MachineData.UEvalue - averageUE));
                if (tile.MachineData.UEvalue > averageUE) {
                    tile.MachineData.UEvalue -= transferUE;
                }
                else {
                    tile.MachineData.UEvalue += transferUE;
                }
            }
        }

        public sealed override void OnKill() {
            if (!VaultUtils.isClient && CanDrop && TargetItem > ItemID.None) {
                Item item = new Item(TargetItem);
                item.CWR().UEValue = MachineData.UEvalue;
                DropItem(item);
            }

            MachineKill();
        }

        public virtual void MachineKill() {

        }

        /// <summary>
        /// 不会自动调用，需要在子类中手动调用
        /// </summary>
        public virtual void DrawChargeBar() {
            if (!HoverTP) {
                return;
            }

            Vector2 drawPos = CenterInWorld + new Vector2(0, Height / 2 + 20) - Main.screenPosition;
            int uiBarByWidthSengs = (int)(ChargingStationTP.BarFull.Value.Width * (MachineData.UEvalue / MaxUEValue));
            // 绘制温度相关的图像
            Rectangle fullRec = new Rectangle(0, 0, uiBarByWidthSengs, ChargingStationTP.BarFull.Value.Height);
            Main.spriteBatch.Draw(ChargingStationTP.BarTop.Value, drawPos, null, Color.White, 0, ChargingStationTP.BarTop.Size() / 2, 1, SpriteEffects.None, 0);
            Main.spriteBatch.Draw(ChargingStationTP.BarFull.Value, drawPos + new Vector2(10, 0), fullRec, Color.White, 0, ChargingStationTP.BarTop.Size() / 2, 1, SpriteEffects.None, 0);

            if (Main.keyState.PressingShift()) {
                string textContent = (((int)MachineData.UEvalue) + "/" + ((int)MaxUEValue) + "UE").ToString();
                Vector2 textSize = FontAssets.MouseText.Value.MeasureString(textContent);
                Utils.DrawBorderStringFourWay(Main.spriteBatch, FontAssets.MouseText.Value, textContent
                            , drawPos.X - textSize.X / 2 + 18, drawPos.Y, Color.White, Color.Black, new Vector2(0.3f), 0.6f);
            }
        }
    }
}
