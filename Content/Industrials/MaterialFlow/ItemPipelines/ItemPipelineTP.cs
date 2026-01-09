using CalamityOverhaul.Content.Industrials.MaterialFlow.Pipelines;
using CalamityOverhaul.Content.Industrials.Storage;
using InnoVault.TileProcessors;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.Industrials.MaterialFlow.ItemPipelines
{
    /// <summary>
    /// 物流管道TileProcessor
    /// </summary>
    [VaultLoaden(CWRConstant.Asset + "MaterialFlow")]
    internal class ItemPipelineTP : TileProcessor, ICWRLoader
    {
        #region 资源和本地化
        public override int TargetTileID => ModContent.TileType<ItemPipelineTile>();
        public static Asset<Texture2D> Pipeline { get; private set; }
        public static Asset<Texture2D> PipelineSide { get; private set; }
        public static Asset<Texture2D> PipelineCorner { get; private set; }
        public static Asset<Texture2D> PipelineCornerSide { get; private set; }
        public static Asset<Texture2D> PipelineCross { get; private set; }
        public static Asset<Texture2D> PipelineCrossSide { get; private set; }
        public static Asset<Texture2D> PipelineChannel { get; private set; }
        public static Asset<Texture2D> PipelineChannelSide { get; private set; }
        public static Asset<Texture2D> PipelineThreeCrutches { get; private set; }
        public static Asset<Texture2D> PipelineThreeCrutchesSide { get; private set; }

        public static LocalizedText ModeNormalText { get; private set; }
        public static LocalizedText ModeOutputText { get; private set; }
        public static LocalizedText ModeInputText { get; private set; }
        public static LocalizedText NotEndpointHintText { get; private set; }

        void ICWRLoader.SetupData() {
            ModeNormalText = Language.GetOrRegister($"Mods.CalamityOverhaul.UI.ItemPipeline.Normal", () => "普通");
            ModeOutputText = Language.GetOrRegister($"Mods.CalamityOverhaul.UI.ItemPipeline.Output", () => "输出");
            ModeInputText = Language.GetOrRegister($"Mods.CalamityOverhaul.UI.ItemPipeline.Input", () => "输入");
            NotEndpointHintText = Language.GetOrRegister($"Mods.CalamityOverhaul.UI.ItemPipeline.NotEndpointHint", () => "只能在管道末端设置输入输出");
        }

        void ICWRLoader.UnLoadData() { }
        #endregion

        #region 形状查找表(复用电力管道逻辑)
        private const int UP = 1, DOWN = 2, LEFT = 4, RIGHT = 8;

        private static readonly (ItemPipelineShape shape, int rotation)[] ShapeLookup = new (ItemPipelineShape, int)[16];

        static ItemPipelineTP() {
            for (int mask = 0; mask < 16; mask++) {
                ShapeLookup[mask] = CalculateShape(mask);
            }
        }

        private static (ItemPipelineShape, int) CalculateShape(int mask) {
            int count = CountBits(mask);
            return count switch {
                4 => (ItemPipelineShape.Cross, 0),
                3 => (ItemPipelineShape.ThreeWay, GetThreeWayRotation(mask)),
                2 => IsOpposite(mask) ? (ItemPipelineShape.Straight, (mask & (UP | DOWN)) != 0 ? 0 : 1)
                                      : (ItemPipelineShape.Corner, GetCornerRotation(mask)),
                _ => (ItemPipelineShape.Endpoint, 0)
            };
        }

        private static int CountBits(int n) => (n & 1) + ((n >> 1) & 1) + ((n >> 2) & 1) + ((n >> 3) & 1);
        private static bool IsOpposite(int mask) => mask == (UP | DOWN) || mask == (LEFT | RIGHT);

        private static int GetThreeWayRotation(int mask) {
            if ((mask & UP) == 0) return 0;
            if ((mask & DOWN) == 0) return 1;
            if ((mask & LEFT) == 0) return 2;
            return 3;
        }

        private static int GetCornerRotation(int mask) {
            if ((mask & (UP | RIGHT)) == (UP | RIGHT)) return 0;
            if ((mask & (DOWN | RIGHT)) == (DOWN | RIGHT)) return 1;
            if ((mask & (UP | LEFT)) == (UP | LEFT)) return 2;
            return 3;
        }
        #endregion

        #region 字段
        /// <summary>
        /// 管道基础颜色(棕黄色调，区别于电力管道)
        /// </summary>
        public Color BaseColor => new Color(180, 140, 90);

        /// <summary>
        /// 当前管道模式
        /// </summary>
        public ItemPipelineMode Mode { get; private set; } = ItemPipelineMode.Normal;

        /// <summary>
        /// 当前管道形状
        /// </summary>
        public ItemPipelineShape Shape { get; private set; } = ItemPipelineShape.Endpoint;

        /// <summary>
        /// 是否是管道端点(只连接0或1个其他管道)
        /// </summary>
        public bool IsEndpoint => GetPipelineConnectionCount() <= 1;

        /// <summary>
        /// 获取连接的存储方向索引(用于箭头指示)，无存储返回-1
        /// </summary>
        public int StorageDirectionIndex {
            get {
                for (int i = 0; i < 4; i++) {
                    if (SideStates[i].LinkType == ItemPipelineLinkType.Storage) {
                        return i;
                    }
                }
                return -1;
            }
        }

        /// <summary>
        /// 形状旋转ID
        /// </summary>
        public int ShapeRotationID { get; private set; } = 0;

        /// <summary>
        /// 四个方向的连接状态(0上1下2左3右)
        /// </summary>
        internal List<ItemPipelineSideState> SideStates { get; private set; }

        /// <summary>
        /// 管道内正在传输的物品
        /// </summary>
        internal TransportingItem? CurrentItem { get; set; } = null;

        /// <summary>
        /// 输出模式的抽取冷却
        /// </summary>
        private int extractCooldown = 0;

        /// <summary>
        /// 抽取间隔(帧)
        /// </summary>
        private const int ExtractInterval = 30;

        //缓存连接掩码
        private int lastConnectionMask = -1;
        #endregion

        #region 初始化和更新
        public override void SetProperty() {
            SideStates = [
                new ItemPipelineSideState(new Point16(0, -1), 0),//上
                new ItemPipelineSideState(new Point16(0, 1), 1), //下
                new ItemPipelineSideState(new Point16(-1, 0), 2),//左
                new ItemPipelineSideState(new Point16(1, 0), 3)  //右
            ];
        }

        public override void Update() {
            //更新连接状态
            foreach (var side in SideStates) {
                side.CoreTP = this;
                side.Position = Position;
                side.UpdateConnectionState();
            }

            //计算形状
            UpdateShape();

            //根据模式执行不同逻辑
            switch (Mode) {
                case ItemPipelineMode.Output:
                    UpdateOutputMode();
                    break;
                case ItemPipelineMode.Input:
                    UpdateInputMode();
                    break;
                case ItemPipelineMode.Normal:
                    UpdateNormalMode();
                    break;
            }

            //更新传输中的物品
            UpdateTransportingItem();
        }

        private void UpdateShape() {
            int connectionMask = 0;
            if (SideStates[0].LinkType == ItemPipelineLinkType.Pipeline) connectionMask |= UP;
            if (SideStates[1].LinkType == ItemPipelineLinkType.Pipeline) connectionMask |= DOWN;
            if (SideStates[2].LinkType == ItemPipelineLinkType.Pipeline) connectionMask |= LEFT;
            if (SideStates[3].LinkType == ItemPipelineLinkType.Pipeline) connectionMask |= RIGHT;

            if (connectionMask != lastConnectionMask) {
                var (shape, rotation) = ShapeLookup[connectionMask];
                Shape = shape;
                ShapeRotationID = rotation;
                lastConnectionMask = connectionMask;

                //如果从端点变为中继点，自动重置为普通模式
                if (Mode != ItemPipelineMode.Normal && !IsEndpoint) {
                    Mode = ItemPipelineMode.Normal;
                    SendData();
                }
            }
        }

        /// <summary>
        /// 获取连接的管道数量
        /// </summary>
        private int GetPipelineConnectionCount() {
            int count = 0;
            foreach (var side in SideStates) {
                if (side.LinkType == ItemPipelineLinkType.Pipeline) {
                    count++;
                }
            }
            return count;
        }
        #endregion

        #region 模式逻辑
        /// <summary>
        /// 输出模式:从连接的存储中抽取物品
        /// </summary>
        private void UpdateOutputMode() {
            //已有物品在传输中，等待传输完成
            if (CurrentItem.HasValue) {
                return;
            }

            //冷却中
            if (extractCooldown > 0) {
                extractCooldown--;
                return;
            }

            //查找连接的存储
            foreach (var side in SideStates) {
                if (side.LinkType != ItemPipelineLinkType.Storage) {
                    continue;
                }

                var storage = side.GetStorageProvider();
                if (storage == null || !storage.IsValid) {
                    continue;
                }

                //从存储中取出第一个可用物品
                foreach (var storedItem in storage.GetStoredItems()) {
                    if (storedItem == null || storedItem.IsAir) {
                        continue;
                    }

                    //查找可达的输入点
                    Point16 targetInput = FindNearestInputPoint();
                    if (targetInput == Point16.NegativeOne) {
                        continue;//没有输入点，不抽取
                    }

                    //尝试取出1个物品
                    Item withdrawn = storage.WithdrawItem(storedItem.type, 1);
                    if (withdrawn != null && !withdrawn.IsAir) {
                        CurrentItem = new TransportingItem(withdrawn.type, withdrawn.stack, withdrawn.prefix) {
                            TargetPosition = targetInput,
                            SourceDirection = side.DirectionIndex
                        };
                        extractCooldown = ExtractInterval;
                        break;
                    }
                }

                if (CurrentItem.HasValue) {
                    break;
                }
            }
        }

        /// <summary>
        /// 输入模式:接收物品并存入连接的存储
        /// </summary>
        private void UpdateInputMode() {
            if (!CurrentItem.HasValue) {
                return;
            }

            var item = CurrentItem.Value;
            if (item.Progress < 1f) {
                return;//物品还没到达中心
            }

            //查找连接的存储并存入
            foreach (var side in SideStates) {
                if (side.LinkType != ItemPipelineLinkType.Storage) {
                    continue;
                }

                var storage = side.GetStorageProvider();
                if (storage == null || !storage.IsValid || !storage.HasSpace) {
                    continue;
                }

                Item toDeposit = new Item(item.ItemType, item.Stack);
                toDeposit.prefix = (byte)item.Prefix;

                if (storage.CanAcceptItem(toDeposit) && storage.DepositItem(toDeposit)) {
                    CurrentItem = null;
                    storage.PlayDepositAnimation();
                    break;
                }
            }

            //如果没有存储空间，物品会卡在这里
        }

        /// <summary>
        /// 普通模式:传递物品到下一个管道
        /// </summary>
        private void UpdateNormalMode() {
            //普通管道只负责传递，不主动抽取或存入
        }
        #endregion

        #region 物品传输
        /// <summary>
        /// 更新正在传输的物品
        /// </summary>
        private void UpdateTransportingItem() {
            if (!CurrentItem.HasValue) {
                return;
            }

            var item = CurrentItem.Value;
            item.Progress += item.Speed;

            //物品到达管道中心(50%)或末端(100%)
            if (item.Progress >= 1f) {
                item.Progress = 1f;

                //如果是输入点，由UpdateInputMode处理存入
                if (Mode == ItemPipelineMode.Input) {
                    CurrentItem = item;
                    return;
                }

                //传递到下一个管道
                if (TryPassToNextPipeline(ref item)) {
                    CurrentItem = null;
                }
                else {
                    //无法传递，物品卡住
                    CurrentItem = item;
                }
            }
            else {
                CurrentItem = item;
            }
        }

        /// <summary>
        /// 尝试将物品传递到下一个管道
        /// </summary>
        private bool TryPassToNextPipeline(ref TransportingItem item) {
            //查找可用的下一个管道(排除来源方向)
            for (int i = 0; i < 4; i++) {
                if (i == item.SourceDirection) {
                    continue;//不往回走
                }

                var side = SideStates[i];
                if (side.LinkType != ItemPipelineLinkType.Pipeline) {
                    continue;
                }

                if (side.LinkedPipeline != null && !side.LinkedPipeline.CurrentItem.HasValue) {
                    //传递物品
                    item.Progress = 0f;
                    item.SourceDirection = GetOppositeDirection(i);
                    side.LinkedPipeline.CurrentItem = item;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 获取相反方向
        /// </summary>
        private static int GetOppositeDirection(int dir) {
            return dir switch {
                0 => 1,//上->下
                1 => 0,//下->上
                2 => 3,//左->右
                3 => 2,//右->左
                _ => -1
            };
        }

        /// <summary>
        /// 查找最近的输入点
        /// </summary>
        private Point16 FindNearestInputPoint() {
            //简单实现:广度优先搜索连接的管道网络
            HashSet<Point16> visited = [];
            Queue<ItemPipelineTP> queue = new();
            queue.Enqueue(this);
            visited.Add(Position);

            while (queue.Count > 0) {
                var current = queue.Dequeue();

                if (current.Mode == ItemPipelineMode.Input && current != this) {
                    return current.Position;
                }

                foreach (var side in current.SideStates) {
                    if (side.LinkType == ItemPipelineLinkType.Pipeline && side.LinkedPipeline != null) {
                        if (!visited.Contains(side.LinkedPipeline.Position)) {
                            visited.Add(side.LinkedPipeline.Position);
                            queue.Enqueue(side.LinkedPipeline);
                        }
                    }
                }
            }

            return Point16.NegativeOne;
        }
        #endregion

        #region 模式切换
        /// <summary>
        /// 循环切换管道模式(只有端点可以切换)
        /// </summary>
        public void CycleMode() {
            //只有端点才能切换模式
            if (!IsEndpoint) {
                SoundEngine.PlaySound(SoundID.MenuClose with { Volume = 0.5f }, CenterInWorld);
                //显示提示文本
                string hintText = NotEndpointHintText?.Value ?? "只能在管道末端设置输入输出";
                CombatText.NewText(HitBox, new Color(255, 100, 100), hintText);
                return;
            }

            Mode = Mode switch {
                ItemPipelineMode.Normal => ItemPipelineMode.Output,
                ItemPipelineMode.Output => ItemPipelineMode.Input,
                ItemPipelineMode.Input => ItemPipelineMode.Normal,
                _ => ItemPipelineMode.Normal
            };

            //播放切换音效
            SoundEngine.PlaySound(SoundID.MenuTick, CenterInWorld);

            //显示模式文本
            string modeText = Mode switch {
                ItemPipelineMode.Normal => ModeNormalText?.Value ?? "普通",
                ItemPipelineMode.Output => ModeOutputText?.Value ?? "输出",
                ItemPipelineMode.Input => ModeInputText?.Value ?? "输入",
                _ => ""
            };

            CombatText.NewText(HitBox, GetModeColor(), modeText);
            SendData();
        }

        /// <summary>
        /// 获取模式对应的颜色
        /// </summary>
        public Color GetModeColor() {
            return Mode switch {
                ItemPipelineMode.Output => new Color(255, 180, 80),//橙色
                ItemPipelineMode.Input => new Color(80, 180, 255),//蓝色
                _ => BaseColor
            };
        }
        #endregion

        #region 网络同步和存储
        public override void SendData(ModPacket data) {
            data.Write((byte)Mode);
            data.Write(CurrentItem.HasValue);
            if (CurrentItem.HasValue) {
                var item = CurrentItem.Value;
                data.Write(item.ItemType);
                data.Write(item.Stack);
                data.Write(item.Prefix);
                data.Write(item.Progress);
            }
        }

        public override void ReceiveData(BinaryReader reader, int whoAmI) {
            Mode = (ItemPipelineMode)reader.ReadByte();
            bool hasItem = reader.ReadBoolean();
            if (hasItem) {
                var item = new TransportingItem {
                    ItemType = reader.ReadInt32(),
                    Stack = reader.ReadInt32(),
                    Prefix = reader.ReadInt32(),
                    Progress = reader.ReadSingle()
                };
                CurrentItem = item;
            }
            else {
                CurrentItem = null;
            }
        }

        public override void SaveData(TagCompound tag) {
            tag["ItemPipeline_Mode"] = (int)Mode;
            if (CurrentItem.HasValue) {
                var item = CurrentItem.Value;
                tag["ItemPipeline_ItemType"] = item.ItemType;
                tag["ItemPipeline_Stack"] = item.Stack;
                tag["ItemPipeline_Prefix"] = item.Prefix;
            }
        }

        public override void LoadData(TagCompound tag) {
            if (tag.TryGet("ItemPipeline_Mode", out int mode)) {
                Mode = (ItemPipelineMode)mode;
            }
            if (tag.TryGet("ItemPipeline_ItemType", out int itemType) && itemType > 0) {
                int stack = tag.GetInt("ItemPipeline_Stack");
                int prefix = tag.GetInt("ItemPipeline_Prefix");
                CurrentItem = new TransportingItem(itemType, stack, prefix);
            }
        }

        public override void OnKill() {
            //掉落正在传输的物品
            if (CurrentItem.HasValue && !VaultUtils.isClient) {
                var item = CurrentItem.Value;
                Item drop = new Item(item.ItemType, item.Stack);
                drop.prefix = (byte)item.Prefix;
                int type = Item.NewItem(new EntitySource_WorldEvent(), HitBox, drop);
                if (VaultUtils.isServer) {
                    NetMessage.SendData(MessageID.SyncItem, -1, -1, null, type);
                }
            }
        }
        #endregion

        #region 绘制
        public override void PreTileDraw(SpriteBatch spriteBatch) {
            if (Shape == ItemPipelineShape.Cross) return;

            foreach (var side in SideStates) {
                if (side.CanDraw && side.LinkType != ItemPipelineLinkType.Pipeline) {
                    side.Draw(spriteBatch);
                }
            }
        }

        public override void Draw(SpriteBatch spriteBatch) {
            //绘制连接臂
            if (Shape != ItemPipelineShape.Cross) {
                foreach (var side in SideStates) {
                    if (side.CanDraw && side.LinkType == ItemPipelineLinkType.Pipeline) {
                        side.Draw(spriteBatch);
                    }
                }
            }

            Vector2 drawPos = PosInWorld - Main.screenPosition;
            Color modeColor = GetModeColor();
            Color lightingColor = Lighting.GetColor(Position.ToPoint());

            //绘制管道本体(中空，不填充内部)
            switch (Shape) {
                case ItemPipelineShape.Cross:
                    DrawCross(spriteBatch, drawPos, modeColor, lightingColor);
                    break;
                case ItemPipelineShape.ThreeWay:
                    DrawThreeWay(spriteBatch, drawPos, modeColor, lightingColor);
                    break;
                case ItemPipelineShape.Corner:
                    DrawCorner(spriteBatch, drawPos, modeColor, lightingColor);
                    break;
                case ItemPipelineShape.Endpoint:
                    DrawEndpoint(spriteBatch, drawPos, modeColor, lightingColor);
                    break;
            }

            //绘制传输中的物品
            DrawTransportingItem(spriteBatch);

            //绘制模式指示器
            DrawModeIndicator(spriteBatch);
        }

        private void DrawCross(SpriteBatch spriteBatch, Vector2 drawPos, Color modeColor, Color lightingColor) {
            Vector2 center = CenterInWorld - Main.screenPosition;
            Vector2 origin = PipelineCross.Size() / 2;
            //只绘制外壳，不填充
            spriteBatch.Draw(PipelineCrossSide.Value, center, null, lightingColor, 0, origin, 1, SpriteEffects.None, 0);
        }

        private void DrawThreeWay(SpriteBatch spriteBatch, Vector2 drawPos, Color modeColor, Color lightingColor) {
            Rectangle rect = PipelineThreeCrutches.Value.GetRectangle(ShapeRotationID, 4);
            spriteBatch.Draw(PipelineThreeCrutchesSide.Value, drawPos, rect, lightingColor, 0, Vector2.Zero, 1, SpriteEffects.None, 0);
        }

        private void DrawCorner(SpriteBatch spriteBatch, Vector2 drawPos, Color modeColor, Color lightingColor) {
            Rectangle rect = PipelineCorner.Value.GetRectangle(ShapeRotationID, 4);
            spriteBatch.Draw(PipelineCornerSide.Value, drawPos, rect, lightingColor, 0, Vector2.Zero, 1, SpriteEffects.None, 0);
        }

        private void DrawEndpoint(SpriteBatch spriteBatch, Vector2 drawPos, Color modeColor, Color lightingColor) {
            int linkCount = 0;
            int nonPipeLinkCount = 0;
            foreach (var side in SideStates) {
                if (side.LinkType != ItemPipelineLinkType.None) {
                    linkCount++;
                    if (side.LinkType != ItemPipelineLinkType.Pipeline) {
                        nonPipeLinkCount++;
                    }
                }
            }

            if (linkCount != 2 || nonPipeLinkCount == 2 || linkCount == 0) {
                spriteBatch.Draw(PipelineSide.Value, drawPos.GetRectangle(Size), lightingColor);
            }
        }

        /// <summary>
        /// 绘制传输中的物品
        /// </summary>
        private void DrawTransportingItem(SpriteBatch spriteBatch) {
            if (!CurrentItem.HasValue) return;

            var item = CurrentItem.Value;
            if (item.ItemType <= 0) return;

            Main.instance.LoadItem(item.ItemType);

            //计算物品位置(根据进度和来源方向)
            Vector2 center = CenterInWorld - Main.screenPosition;
            Vector2 offset = Vector2.Zero;

            if (item.SourceDirection >= 0) {
                Vector2 dirOffset = item.SourceDirection switch {
                    0 => new Vector2(0, -8),//从上来
                    1 => new Vector2(0, 8), //从下来
                    2 => new Vector2(-8, 0),//从左来
                    3 => new Vector2(8, 0), //从右来
                    _ => Vector2.Zero
                };
                offset = Vector2.Lerp(dirOffset, Vector2.Zero, item.Progress);
            }

            VaultUtils.SimpleDrawItem(spriteBatch, item.ItemType, center + offset, 20, 0.6f, 0, Color.White);
        }

        /// <summary>
        /// 绘制模式指示器(箭头指向存储对象)
        /// </summary>
        private void DrawModeIndicator(SpriteBatch spriteBatch) {
            if (Mode == ItemPipelineMode.Normal) return;

            Vector2 center = CenterInWorld - Main.screenPosition;
            Color indicatorColor = GetModeColor();

            //呼吸闪烁效果
            float pulse = (float)System.Math.Sin(Main.GlobalTimeWrappedHourly * 4f) * 0.3f + 0.7f;
            Texture2D px = VaultAsset.placeholder2.Value;

            //获取存储方向，绘制箭头
            int storageDir = StorageDirectionIndex;
            if (storageDir >= 0) {
                //根据方向计算箭头旋转角度
                //输入模式:箭头指向存储(物品进入存储)
                //输出模式:箭头背离存储(物品从存储出来)
                float baseRotation = storageDir switch {
                    0 => -MathHelper.PiOver2,//上
                    1 => MathHelper.PiOver2, //下
                    2 => MathHelper.Pi,      //左
                    3 => 0,                  //右
                    _ => 0
                };

                //输出模式箭头方向相反
                if (Mode == ItemPipelineMode.Output) {
                    baseRotation += MathHelper.Pi;
                }

                DrawArrow(spriteBatch, center, baseRotation, indicatorColor * pulse);
            }
            else {
                //没有存储连接时显示小方块
                Rectangle indicatorRect = new Rectangle((int)(center.X - 2), (int)(center.Y - 2), 4, 4);
                spriteBatch.Draw(px, indicatorRect, indicatorColor * pulse);
            }
        }

        /// <summary>
        /// 绘制箭头
        /// </summary>
        private static void DrawArrow(SpriteBatch spriteBatch, Vector2 center, float rotation, Color color) {
            Texture2D px = VaultAsset.placeholder2.Value;

            //箭头由三条线组成:主轴和两个斜线
            Vector2 direction = rotation.ToRotationVector2();
            Vector2 perpendicular = (rotation + MathHelper.PiOver2).ToRotationVector2();

            //箭头参数
            float arrowLength = 6f;
            float headLength = 3f;
            float headWidth = 2.5f;

            //箭头起点和终点
            Vector2 start = center - direction * arrowLength * 0.5f;
            Vector2 end = center + direction * arrowLength * 0.5f;

            //绘制主轴
            DrawLine(spriteBatch, px, start, end, color, 2);

            //绘制箭头头部两条斜线
            Vector2 headBase = end - direction * headLength;
            Vector2 headLeft = headBase + perpendicular * headWidth;
            Vector2 headRight = headBase - perpendicular * headWidth;

            DrawLine(spriteBatch, px, end, headLeft, color, 2);
            DrawLine(spriteBatch, px, end, headRight, color, 2);
        }

        /// <summary>
        /// 绘制线条
        /// </summary>
        private static void DrawLine(SpriteBatch spriteBatch, Texture2D texture, Vector2 start, Vector2 end, Color color, int thickness) {
            Vector2 delta = end - start;
            float length = delta.Length();
            float rotation = (float)System.Math.Atan2(delta.Y, delta.X);

            Rectangle destRect = new Rectangle((int)start.X, (int)start.Y, (int)length, thickness);
            spriteBatch.Draw(texture, destRect, null, color, rotation, new Vector2(0, thickness / 2f), SpriteEffects.None, 0);
        }
        #endregion
    }
}
