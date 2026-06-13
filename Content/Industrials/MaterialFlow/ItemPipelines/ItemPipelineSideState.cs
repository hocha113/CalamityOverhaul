using InnoVault.Storages;
using InnoVault.TileProcessors;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;

namespace CalamityOverhaul.Content.Industrials.MaterialFlow.ItemPipelines
{
    /// <summary>物流管道侧连接，快验+8帧全扫，变连标脏路由</summary>
    internal class ItemPipelineSideState
    {
        /// <summary>当前管道位置(由所有者每帧同步)</summary>
        internal Point16 Position;
        /// <summary>方向偏移</summary>
        internal readonly Point16 Offset;
        /// <summary>方向索引(0上1下2左3右)</summary>
        internal readonly int DirectionIndex;
        /// <summary>核心管道TP引用</summary>
        internal ItemPipelineTP CoreTP;
        /// <summary>连接的外部TP(可能是非管道)</summary>
        internal TileProcessor ExternalTP;
        /// <summary>连接的管道(若是管道连接)</summary>
        internal ItemPipelineTP LinkedPipeline;
        /// <summary>连接的存储提供者</summary>
        private IStorageProvider linkedStorage;
        /// <summary>连接类型</summary>
        internal ItemPipelineLinkType LinkType { get; private set; } = ItemPipelineLinkType.None;
        /// <summary>是否可以绘制连接臂(由所有者读取)</summary>
        internal bool CanDraw { get; private set; }

        //快速验证剩余帧数, 减少完整扫描频率
        private int validationFramesRemaining;
        private const int FullScanInterval = 8;
        //侧位之间稍微错峰, 避免所有管道在同一帧执行 FullScan
        private static int s_phaseAccumulator;

        public ItemPipelineSideState(Point16 offset, int directionIndex) {
            Offset = offset;
            DirectionIndex = directionIndex;
            //初始相位错峰, 让 FullScan 在不同帧分散执行
            unchecked { validationFramesRemaining = (s_phaseAccumulator++ & 0x7); }
        }

        /// <summary>每帧入口，快验或全扫</summary>
        public void UpdateConnectionState() {
            if (FastValidate()) {
                return;
            }
            FullScan();
        }

        /// <summary>廉价复验缓存连接</summary>
        private bool FastValidate() {
            if (validationFramesRemaining <= 0) {
                return false;
            }
            validationFramesRemaining--;

            switch (LinkType) {
                case ItemPipelineLinkType.Pipeline:
                    if (LinkedPipeline != null && LinkedPipeline.Active && LinkedPipeline.Position == Position + Offset) {
                        //邻居形状变需更新臂遮挡
                        UpdateDrawState();
                        return true;
                    }
                    return false;
                case ItemPipelineLinkType.Storage:
                    if (linkedStorage != null && linkedStorage.IsValid) {
                        return true;
                    }
                    return false;
                case ItemPipelineLinkType.None:
                    //无连接冷却几帧再扫
                    return true;
            }
            return false;
        }

        /// <summary>tile→TP→存储工厂全扫</summary>
        private void FullScan() {
            ItemPipelineLinkType prevLinkType = LinkType;

            ExternalTP = null;
            LinkedPipeline = null;
            linkedStorage = null;
            LinkType = ItemPipelineLinkType.None;
            CanDraw = false;

            Point16 checkPos = Position + Offset;

            Tile tile = Framing.GetTileSafely(checkPos);
            if (!tile.HasTile) {
                CheckForChest(checkPos);
            }
            else if (VaultUtils.SafeGetTopLeft(checkPos, out var topLeft)
                     && TileProcessorLoader.TP_Point_To_Instance.TryGetValue(topLeft, out ExternalTP)
                     && ExternalTP != null && ExternalTP.Active) {
                if (ExternalTP is ItemPipelineTP otherPipeline) {
                    LinkedPipeline = otherPipeline;
                    LinkType = ItemPipelineLinkType.Pipeline;
                    CanDraw = true;
                    UpdateDrawState();
                }
                else if (ExternalTP is IStorageProvider storageTP) {
                    linkedStorage = storageTP;
                    LinkType = ItemPipelineLinkType.Storage;
                    CanDraw = true;
                }
                else {
                    //非管道非存储 TP，走箱子工厂兜底
                    CheckForChest(checkPos);
                }
            }
            else {
                CheckForChest(checkPos);
            }

            //全扫后进入冷却
            validationFramesRemaining = FullScanInterval;

            //连接类型变则标脏
            if (prevLinkType != LinkType) {
                ItemPipelineNetwork.MarkDirty();
            }
        }

        /// <summary>箱子等非 TP 存储</summary>
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

        /// <summary>按邻居形状决定臂可见</summary>
        private void UpdateDrawState() {
            if (LinkedPipeline == null) {
                CanDraw = false;
                return;
            }
            //十字/拐角/三通已画满，本侧不重复
            CanDraw = LinkedPipeline.Shape != ItemPipelineShape.Cross
                      && LinkedPipeline.Shape != ItemPipelineShape.Corner
                      && LinkedPipeline.Shape != ItemPipelineShape.ThreeWay;
        }

        /// <summary>强制下次全扫</summary>
        public void Invalidate() {
            validationFramesRemaining = 0;
        }

        /// <summary>运行时取存储，失效返回 null</summary>
        public IStorageProvider GetStorageProvider() {
            if (LinkType != ItemPipelineLinkType.Storage) {
                return null;
            }
            if (linkedStorage != null && linkedStorage.IsValid) {
                return linkedStorage;
            }
            //失效, 下次完整扫描会清掉
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

            Color lightingColor = VaultUtils.MultiStepColorLerp(0.5f, Color.YellowGreen, Lighting.GetColor(Position.ToPoint()));
            Color tintColor = lightingColor;
            if (CoreTP.Mode != ItemPipelineMode.Normal) {
                tintColor = Color.Lerp(lightingColor, CoreTP.GetModeColor(), 0.3f);
            }

            spriteBatch.Draw(ItemPipelineTP.PipelineChannelSide.Value, drawPos + orig, null, tintColor, drawRot, orig, 1, SpriteEffects.None, 0);
        }
    }
}
