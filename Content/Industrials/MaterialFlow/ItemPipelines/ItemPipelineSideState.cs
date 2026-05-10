using InnoVault.Storages;
using InnoVault.TileProcessors;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;

namespace CalamityOverhaul.Content.Industrials.MaterialFlow.ItemPipelines
{
    /// <summary>
    /// 物流管道侧面连接状态
    /// <para>设计要点：</para>
    /// <list type="bullet">
    /// <item><b>快速验证(每帧)</b>：上一次扫描确认的连接，仅做一次 Active/IsValid 廉价校验；连接没变则直接复用。</item>
    /// <item><b>完整重扫(节流)</b>：每隔 <see cref="FullScanInterval"/> 帧或快速验证失败时，做一次 tile/字典/箱子工厂全扫描。</item>
    /// <item><b>主动标脏</b>：连接类型变化时立刻通知 <see cref="ItemPipelineNetwork"/> 重建路由。</item>
    /// </list>
    /// </summary>
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

        /// <summary>
        /// 主入口：每帧调用，自动选择"快速验证"或"完整扫描"
        /// </summary>
        public void UpdateConnectionState() {
            if (FastValidate()) {
                return;
            }
            FullScan();
        }

        /// <summary>
        /// 廉价复验缓存的连接，连接仍有效返回 true
        /// </summary>
        private bool FastValidate() {
            if (validationFramesRemaining <= 0) {
                return false;
            }
            validationFramesRemaining--;

            switch (LinkType) {
                case ItemPipelineLinkType.Pipeline:
                    if (LinkedPipeline != null && LinkedPipeline.Active && LinkedPipeline.Position == Position + Offset) {
                        //邻居管道形状可能在变化(Cross/Corner/ThreeWay), 需更新本侧的绘制掩盖
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
                    //无连接则跳过几帧再重扫(玩家可能刚刚放置了新方块)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 完整扫描:tile -> TP -> 存储工厂三级匹配
        /// </summary>
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
                    //邻居是 tile + TP 但既非管道也非存储, 仍尝试存储工厂兜底
                    CheckForChest(checkPos);
                }
            }
            else {
                CheckForChest(checkPos);
            }

            //完整扫描完成后, 给一段冷却期不再重复昂贵扫描
            validationFramesRemaining = FullScanInterval;

            //连接类型有变化, 通知网络管理器重建路由
            if (prevLinkType != LinkType) {
                ItemPipelineNetwork.MarkDirty();
            }
        }

        /// <summary>
        /// 通过存储工厂查找箱子等非TP存储
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
        /// 根据邻居管道的形状更新自身连接臂的绘制可见性
        /// </summary>
        private void UpdateDrawState() {
            if (LinkedPipeline == null) {
                CanDraw = false;
                return;
            }
            //十字/拐角/三通本身已经把这条臂画完整了，本侧不要重复绘制
            CanDraw = LinkedPipeline.Shape != ItemPipelineShape.Cross
                      && LinkedPipeline.Shape != ItemPipelineShape.Corner
                      && LinkedPipeline.Shape != ItemPipelineShape.ThreeWay;
        }

        /// <summary>
        /// 强制下一次 UpdateConnectionState 重新扫描(避免快路径遗漏)
        /// </summary>
        public void Invalidate() {
            validationFramesRemaining = 0;
        }

        /// <summary>
        /// 获取存储提供者(运行时校验有效性)
        /// </summary>
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
