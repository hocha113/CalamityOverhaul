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
        /// <summary>
        /// 待机位:true 时机器脱离电网(不参与 <see cref="UpdateConductive"/> 均衡)且业务冻结
        /// (不执行 <see cref="UpdateMachine"/>),电量与内部状态保持原值;绘制与悬停不受影响。
        /// 默认 false 恒启用。由机关接口器/电网总闸在权威端翻转,翻转方负责 SendData 传播;
        /// 本位追加在基类包尾与存档键 "MachineDisabled" 上,两端对称
        /// </summary>
        public bool Disabled;
        /// <summary>
        /// 周期性权威锚定间隔(帧)：机器状态在各端本地模拟，事件同步之外由服务器按此节奏
        /// 推一次全量包纠偏累计漂移；按 WhoAmI 错峰。返回 0 关闭。
        /// 管道类不参与，数量大，且其电量经压差均衡向机器锚点自愈。
        /// 三重抑制控制空闲流量：UE未变化时降为四倍间隔的保活；范围内无玩家时不发
        /// </summary>
        public virtual int NetAnchorIntervalTicks => 300;
        /// <summary>锚定的玩家接近半径(像素)，范围内无玩家则跳过发送(远处工厂零流量)</summary>
        private const float AnchorPlayerRange = 4000f;
        /// <summary>上次锚定发送时的UE值，NaN=尚未发送过</summary>
        private float lastAnchoredUE = float.NaN;
        /// <summary>距上次锚定发送的帧数，用于静止机器的低频保活</summary>
        private int ticksSinceAnchorSend;
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
            //待机短路:脱网+冻结,只跳业务两步;锚定照常走,Disabled 状态本身仍需纠偏同步
            if (!Disabled) {
                if (Efficiency > 0) {
                    UpdateConductive();
                }
                UpdateMachine();
            }

            //周期性权威锚定(见 NetAnchorIntervalTicks)；SendData 内部已处理并行阶段转主线程
            int anchorInterval = NetAnchorIntervalTicks;
            if (anchorInterval > 0 && VaultUtils.isServer && MachineData != null
                && this is not BaseUEPipelineTP) {
                ticksSinceAnchorSend++;
                if ((Main.GameUpdateCount + (uint)(WhoAmI * 13)) % (uint)anchorInterval == 0) {
                    //UE静止的机器降为四倍间隔保活；变化中的机器全速纠偏
                    bool changed = float.IsNaN(lastAnchoredUE)
                        || Math.Abs(MachineData.UEvalue - lastAnchoredUE) > 0.01f;
                    if ((changed || ticksSinceAnchorSend >= anchorInterval * 4) && AnyPlayerInAnchorRange()) {
                        lastAnchoredUE = MachineData.UEvalue;
                        ticksSinceAnchorSend = 0;
                        SendData();
                    }
                }
            }
        }

        /// <summary>锚定范围内是否有玩家；玩家位置由主线程更早写入，并行读取安全</summary>
        private bool AnyPlayerInAnchorRange() {
            float rangeSQ = AnchorPlayerRange * AnchorPlayerRange;
            foreach (Player player in Main.ActivePlayers) {
                if (player.position.DistanceSQ(PosInWorld) < rangeSQ) {
                    return true;
                }
            }
            return false;
        }

        public virtual void UpdateMachine() {

        }

        public override void SendData(ModPacket data) {
            MachineData?.SendData(data);
            //待机位追加包尾:无条件写读,不依赖 MachineData 是否为空,两端绝对对称;
            //全部 base 先行的子类字段整体后移一字节,序列不错位
            data.Write(Disabled);
        }

        public override void ReceiveData(BinaryReader reader, int whoAmI) {
            MachineData?.ReceiveData(reader, whoAmI);
            Disabled = reader.ReadBoolean();
        }

        public override void SaveData(TagCompound tag) {
            MachineData?.SaveData(tag);
            //仅待机中才落键,旧档无键 TryGet 默认 false,加载零迁移
            if (Disabled) {
                tag["MachineDisabled"] = true;
            }
        }

        public override void LoadData(TagCompound tag) {
            MachineData?.LoadData(tag);
            Disabled = tag.TryGet("MachineDisabled", out bool disabled) && disabled;
        }

        public void DropItem(int id) => DropItem(new Item(id));

        public void DropItem(Item item) {
            //并行阶段延后到主线程
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

                Main.spriteBatch.Draw(value, drawPos, new Rectangle(0, 0, width + 4, height + 4), Color.Black, 0, new Vector2((width + 4) / 2, (height + 4) / 2), 1f, SpriteEffects.None, 0f);
                Main.spriteBatch.Draw(value, drawPos, new Rectangle(0, 0, width, height), new Color(50, 50, 50), 0, new Vector2(width / 2, height / 2), 1f, SpriteEffects.None, 0f);
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
