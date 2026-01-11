using InnoVault.Storages;
using InnoVault.TileProcessors;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;

namespace CalamityOverhaul.Content.Industrials.MaterialFlow.ItemPipelines
{
    /// <summary>
    /// 物流管道侧面连接状态
    /// </summary>
    internal class ItemPipelineSideState
    {
        /// <summary>
        /// 当前管道位置
        /// </summary>
        internal Point16 Position;

        /// <summary>
        /// 方向偏移
        /// </summary>
        internal readonly Point16 Offset;

        /// <summary>
        /// 方向索引(0上1下2左3右)
        /// </summary>
        internal readonly int DirectionIndex;

        /// <summary>
        /// 核心管道TP引用
        /// </summary>
        internal ItemPipelineTP CoreTP;

        /// <summary>
        /// 连接的外部TP
        /// </summary>
        internal TileProcessor ExternalTP;

        /// <summary>
        /// 连接的管道(如果是管道连接)
        /// </summary>
        internal ItemPipelineTP LinkedPipeline;

        /// <summary>
        /// 连接的存储提供者(如果是存储连接)
        /// </summary>
        private IStorageProvider linkedStorage;

        /// <summary>
        /// 连接类型
        /// </summary>
        internal ItemPipelineLinkType LinkType { get; private set; } = ItemPipelineLinkType.None;

        /// <summary>
        /// 是否可以绘制连接臂
        /// </summary>
        internal bool CanDraw { get; private set; }

        public ItemPipelineSideState(Point16 offset, int directionIndex) {
            Offset = offset;
            DirectionIndex = directionIndex;
        }

        /// <summary>
        /// 更新连接状态
        /// </summary>
        public void UpdateConnectionState() {
            //重置状态
            ExternalTP = null;
            LinkedPipeline = null;
            linkedStorage = null;
            LinkType = ItemPipelineLinkType.None;
            CanDraw = false;

            Point16 checkPos = Position + Offset;

            //获取相邻物块
            Tile tile = Framing.GetTileSafely(checkPos);
            if (!tile.HasTile) {
                //检查是否有箱子
                CheckForChest(checkPos);
                return;
            }

            if (!VaultUtils.SafeGetTopLeft(checkPos, out var topLeft)) {
                return;
            }

            //使用TileProcessorLoader查询TP
            if (TileProcessorLoader.TP_Point_To_Instance.TryGetValue(topLeft, out ExternalTP)) {
                if (ExternalTP != null && ExternalTP.Active) {
                    //检查是否是物流管道
                    if (ExternalTP is ItemPipelineTP otherPipeline) {
                        LinkedPipeline = otherPipeline;
                        LinkType = ItemPipelineLinkType.Pipeline;
                        CanDraw = true;
                        UpdateDrawState();
                        return;
                    }

                    //检查是否实现了存储接口
                    if (ExternalTP is IStorageProvider storageTP) {
                        linkedStorage = storageTP;
                        LinkType = ItemPipelineLinkType.Storage;
                        CanDraw = true;
                        return;
                    }
                }
            }

            //检查箱子
            CheckForChest(checkPos);
        }

        /// <summary>
        /// 检查位置是否有箱子
        /// </summary>
        private void CheckForChest(Point16 checkPos) {
            if (!VaultUtils.SafeGetTopLeft(checkPos, out var pos)) {
                return;
            }

            var inds = StorageLoader.GetStorageTargetByPoint(pos);

            if (inds != null) {
                linkedStorage = inds;
                LinkType = ItemPipelineLinkType.Storage;
                CanDraw = true;
            }
        }

        /// <summary>
        /// 更新绘制状态
        /// </summary>
        private void UpdateDrawState() {
            if (!CanDraw || LinkedPipeline == null) {
                return;
            }

            //如果连接的管道是拐角/十字/三通，避免重复绘制
            if (LinkedPipeline.Shape is ItemPipelineShape.Cross or ItemPipelineShape.Corner or ItemPipelineShape.ThreeWay) {
                CanDraw = false;
            }
        }

        /// <summary>
        /// 获取存储提供者
        /// </summary>
        public IStorageProvider GetStorageProvider() {
            if (LinkType != ItemPipelineLinkType.Storage) {
                return null;
            }

            //验证存储是否仍然有效
            if (linkedStorage != null && linkedStorage.IsValid) {
                return linkedStorage;
            }

            return null;
        }

        /// <summary>
        /// 绘制连接臂
        /// </summary>
        public void Draw(SpriteBatch spriteBatch) {
            if (CoreTP == null) {
                return;
            }

            Vector2 drawPos = CoreTP.PosInWorld + Offset.ToVector2() * 16 - Main.screenPosition;
            float drawRot = Offset.ToVector2().ToRotation();
            Vector2 orig = ItemPipelineTP.PipelineChannel.Size() / 2;

            //绘制管道连接臂外壳(中空，不填充)
            Color lightingColor = VaultUtils.MultiStepColorLerp(0.5f, Color.YellowGreen, Lighting.GetColor(Position.ToPoint()));

            //根据模式添加颜色
            Color tintColor = lightingColor;
            if (CoreTP.Mode != ItemPipelineMode.Normal) {
                tintColor = Color.Lerp(lightingColor, CoreTP.GetModeColor(), 0.3f);
            }

            spriteBatch.Draw(ItemPipelineTP.PipelineChannelSide.Value, drawPos + orig, null, tintColor, drawRot, orig, 1, SpriteEffects.None, 0);
        }
    }
}
