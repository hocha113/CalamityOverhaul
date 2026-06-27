using CalamityOverhaul.Content.Industrials.MaterialFlow.Pipelines;
using InnoVault.Concurrent;
using InnoVault.TileProcessors;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.IO;
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
        private readonly HashSet<BaseUEPipelineTP> _connectedTilesCache = new();
        public MachineData MachineData { get; set; }
        public virtual float MaxUEValue => 1000;
        public virtual int TargetItem => ItemID.None;
        public virtual bool CanDrop => true;
        public int Efficiency = 2;
        public virtual MachineData GetGeneratorDataInds() => new MachineData();
        public sealed override void SetProperty() {
            MachineData ??= GetGeneratorDataInds();
            PlaceNet = true;//放置联网，初始化 UE
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

        /// <summary>机器与相邻管道/机器互相作用(能量扩散)，按连通岛屿并行更新</summary>
        public override ParallelExecutionKind ParallelKind => ParallelExecutionKind.Grouped;

        /// <summary>声明机器外缘一圈的邻接格，使相连的机器/管道落入同一并行岛屿(岛内串行，跨岛并行)</summary>
        public override void CollectGroupLinks(ref TPGroupLinkBuilder builder) {
            int tileWidth = Width / 16;
            int tileHeight = Height / 16;
            for (int i = Position.X; i < Position.X + tileWidth; i++) {
                builder.Link(i, Position.Y - 1);
                builder.Link(i, Position.Y + tileHeight);
            }
            for (int j = Position.Y; j < Position.Y + tileHeight; j++) {
                builder.Link(Position.X - 1, j);
                builder.Link(Position.X + tileWidth, j);
            }
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
            //并行阶段把物品生成与发包延迟到主线程执行(串行阶段则立即执行)
            DeferSpawnItem(new EntitySource_WorldEvent(), HitBox, item, type => {
                if (VaultUtils.isServer) {
                    NetMessage.SendData(MessageID.SyncItem, -1, -1, null, type, 0f, 0f, 0f, 0, 0, 0);
                }
            });
        }

        public virtual void ExtraConductive(Point16 point, TileProcessor tp) {

        }

        public virtual void PostUpdateConductive() {

        }

        public void CheckPoint(Point16 point) {
            if (TileProcessorLoader.ByPositionGetTP(point, out var tp)) {
                ExtraConductive(point, tp);
                if (tp is BaseUEPipelineTP pipelineTP)
                    _connectedTilesCache.Add(pipelineTP);
            }
            else {
                ExtraConductive(point, null);
            }
        }

        public void UpdateConductive() {
            _connectedTilesCache.Clear();

            int tileWidth = Width / 16;
            int tileHeight = Height / 16;

            //上下
            for (int i = Position.X; i < Position.X + tileWidth; i++) {
                CheckPoint(new Point16(i, Position.Y - 1));
                CheckPoint(new Point16(i, Position.Y + tileHeight));
            }

            //左右
            for (int j = Position.Y; j < Position.Y + tileHeight; j++) {
                CheckPoint(new Point16(Position.X - 1, j));
                CheckPoint(new Point16(Position.X + tileWidth, j));
            }

            if (_connectedTilesCache.Count == 0)
                return;

            //汇总相邻管道 UE
            float totalUE = 0f;
            foreach (var tile in _connectedTilesCache) {
                if (tile.MachineData != null)
                    totalUE += tile.MachineData.UEvalue;
            }
            float averageUE = totalUE / _connectedTilesCache.Count;

            //压差均衡
            float efficiency = this.Efficiency;
            foreach (var tile in _connectedTilesCache) {
                if (tile.MachineData == null)
                    continue;

                float diff = tile.MachineData.UEvalue - averageUE;
                float transferUE = Math.Min(efficiency, Math.Abs(diff));
                tile.MachineData.UEvalue -= Math.Sign(diff) * transferUE;
            }

            PostUpdateConductive();
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

        /// <summary>子类手动调用，非自动</summary>
        public virtual void DrawChargeBar() {
            if (!HoverTP) {
                return;
            }

            Vector2 drawPos = CenterInWorld + new Vector2(0, Height / 2 + 20) - Main.screenPosition;

            if (CWRRef.Has) {
                int uiBarByWidthSengs = (int)(CWRAsset.BarFull.Value.Width * (MachineData.UEvalue / MaxUEValue));
                //Calamity UE 条
                Rectangle fullRec = new Rectangle(0, 0, uiBarByWidthSengs, CWRAsset.BarFull.Value.Height);
                Main.spriteBatch.Draw(CWRAsset.BarTop.Value, drawPos, null, Color.White, 0, CWRAsset.BarTop.Size() / 2, 1, SpriteEffects.None, 0);
                Main.spriteBatch.Draw(CWRAsset.BarFull.Value, drawPos + new Vector2(10, 0), fullRec, Color.White, 0, CWRAsset.BarTop.Size() / 2, 1, SpriteEffects.None, 0);
            }
            else {
                //无 Calamity 降级条
                Texture2D value = VaultAsset.placeholder2.Value;
                int width = 60;
                int height = 12;
                float ratio = MachineData.UEvalue / MaxUEValue;
                ratio = MathHelper.Clamp(ratio, 0f, 1f);

                //绘制背景边框
                Main.spriteBatch.Draw(value, drawPos, new Rectangle(0, 0, width + 4, height + 4), Color.Black, 0, new Vector2((width + 4) / 2, (height + 4) / 2), 1f, SpriteEffects.None, 0f);
                //绘制背景底色
                Main.spriteBatch.Draw(value, drawPos, new Rectangle(0, 0, width, height), new Color(50, 50, 50), 0, new Vector2(width / 2, height / 2), 1f, SpriteEffects.None, 0f);
                //绘制能量条
                Main.spriteBatch.Draw(value, drawPos - new Vector2(width / 2, height / 2), new Rectangle(0, 0, (int)(width * ratio), height), Color.Lerp(Color.Red, Color.Lime, ratio), 0, Vector2.Zero, 1f, SpriteEffects.None, 0f);
            }

            if (Main.keyState.PressingShift()) {
                string textContent = (((int)MachineData.UEvalue) + "/" + ((int)MaxUEValue) + "UE").ToString();
                Vector2 textSize = FontAssets.MouseText.Value.MeasureString(textContent);
                Utils.DrawBorderStringFourWay(Main.spriteBatch, FontAssets.MouseText.Value, textContent
                            , drawPos.X - textSize.X / 2 + 18, drawPos.Y, Color.White, Color.Black, new Vector2(0.3f), 0.6f);
            }
        }
    }
}
