using CalamityOverhaul.Common;
using InnoVault.TileProcessors;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.Industrials.ElectricPowers
{
    internal class ItemFilterData : GlobalItem
    {
        //使用 List 代替 HashSet 以保证顺序一致性
        internal List<int> Items = [];
        
        //版本号用于追踪数据变更
        internal int DataVersion = 0;
        
        public override bool InstancePerEntity => true;
        
        public override GlobalItem Clone(Item from, Item to) {
            ItemFilterData itemFilterData = (ItemFilterData)base.Clone(from, to);
            //深拷贝列表，避免引用粘连
            itemFilterData.Items = new List<int>(Items);
            itemFilterData.DataVersion = DataVersion;
            return itemFilterData;
        }
        
        public override bool AppliesToEntity(Item entity, bool lateInstantiation) {
            return entity.type == ModContent.ItemType<ItemFilter>();
        }
        
        /// <summary>
        /// 线程安全地添加物品ID
        /// </summary>
        public bool TryAddItem(int itemID) {
            if (itemID <= ItemID.None) return false;
            if (Items.Contains(itemID)) return false;
            
            Items.Add(itemID);
            DataVersion++;
            return true;
        }
        
        /// <summary>
        /// 线程安全地移除物品ID
        /// </summary>
        public bool TryRemoveItem(int itemID) {
            bool removed = Items.Remove(itemID);
            if (removed) {
                DataVersion++;
            }
            return removed;
        }
        
        /// <summary>
        /// 批量设置物品（原子操作）
        /// </summary>
        public void SetItems(IEnumerable<int> newItems) {
            Items.Clear();
            Items.AddRange(newItems.Where(id => id > ItemID.None).Distinct());
            DataVersion++;
        }
        
        public override void NetSend(Item item, BinaryWriter writer) {
            if (item.type != ItemFilter.ID) {
                return;
            }
            
            writer.Write(DataVersion);
            writer.Write(Items.Count);
            foreach (int itemID in Items) {
                writer.Write(itemID);
            }
        }
        
        public override void NetReceive(Item item, BinaryReader reader) {
            if (item.type != ItemFilter.ID) {
                return;
            }
            
            int receivedVersion = reader.ReadInt32();
            int count = reader.ReadInt32();
            List<int> receivedItems = new(count);
            
            for (int i = 0; i < count; i++) {
                receivedItems.Add(reader.ReadInt32());
            }
            
            //只有当接收到的版本更新时才更新数据
            if (receivedVersion > DataVersion) {
                Items = receivedItems;
                DataVersion = receivedVersion;
            }
        }
        
        public override void SaveData(Item item, TagCompound tag) {
            if (item.type != ItemFilter.ID) {
                return;
            }
            
            Items ??= [];
            tag["_Items"] = Items.ToArray();
            tag["_DataVersion"] = DataVersion;
        }
        
        public override void LoadData(Item item, TagCompound tag) {
            if (item.type != ItemFilter.ID) {
                return;
            }

            try {
                if (tag.TryGet<int[]>("_Items", out var value)) {
                    Items = new List<int>(value);
                }
                else {
                    Items = [];
                }

                if (tag.TryGet<int>("_DataVersion", out int version)) {
                    DataVersion = version;
                }
            } catch (Exception ex) { CWRMod.Instance.Logger.Error($"[ItemFilterData:LoadData] an error has occurred:{ex.Message}"); }           
        }
    }

    internal class ItemFilter : ModItem
    {
        public override string Texture => CWRConstant.ElectricPowers + "ItemFilter";
        public static int ID { get; private set; }
        
        public override void SetStaticDefaults() => ID = Type;
        
        public override void SetDefaults() {
            Item.width = Item.height = 64;
            Item.useTime = Item.useAnimation = 16;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.maxStack = 1; //防止堆叠导致数据混乱
        }
        
        public static ItemFilterData GetData(Item item) => item.GetGlobalItem<ItemFilterData>();
        
        /// <summary>
        /// 从收集器复制过滤列表
        /// </summary>
        private bool HandleCollectorBehavior() {
            Point16 point16 = Main.MouseWorld.ToTileCoordinates16();
            if (!TileProcessorLoader.AutoPositionGetTP<CollectorTP>(point16, out var collectorTP)) {
                return false;
            }

            SoundEngine.PlaySound(CWRSound.Select);

            var data = GetData(Item);
            var sourceData = GetData(collectorTP.ItemFilter);
            
            //原子操作：一次性设置所有物品
            data.SetItems(sourceData.Items);
            
            //网络同步
            if (Main.netMode == NetmodeID.MultiplayerClient) {
                NetMessage.SendData(MessageID.SyncItem, -1, -1, null, Item.whoAmI);
            }

            return true;
        }
        
        /// <summary>
        /// 从箱子复制物品列表
        /// </summary>
        private bool HandleChestBehavior() {
            Point16 point16 = Main.MouseWorld.ToTileCoordinates16();
            if (!VaultUtils.SafeGetTopLeft(point16, out var newPoint)) {
                return false;
            }

            int chestIndex = Chest.FindChest(newPoint.X, newPoint.Y);
            if (chestIndex == -1) {
                return true;
            }

            SoundEngine.PlaySound(CWRSound.Select);

            Chest chest = Main.chest[chestIndex];
            var data = GetData(Item);
            
            //提取箱子中的所有物品类型
            HashSet<int> chestItemTypes = [];
            foreach (var item in chest.item) {
                if (item.type > ItemID.None) {
                    chestItemTypes.Add(item.type);
                }
            }
            
            //原子操作
            data.SetItems(chestItemTypes);

            return true;
        }
        
        public override bool? UseItem(Player player) {
            if (player.whoAmI != Main.myPlayer) {
                return true;
            }
            
            if (ItemFilterUI.Instance.hoverInMainPage) {
                return true;
            }
            
            if (HandleCollectorBehavior()) {
                ItemFilterUI.Instance.Initialize();
                return true;
            }
            
            if (HandleChestBehavior()) {
                ItemFilterUI.Instance.Initialize();
                return true;
            }
            
            ItemFilterUI.Instance.Active = !ItemFilterUI.Instance.Active;
            ItemFilterUI.Instance.ItemFilter = Item;//这里不要赋值克隆版本的物品，否则数据不同步
            SoundEngine.PlaySound(SoundID.MenuOpen);
            return true;
        }
        
        public override bool ConsumeItem(Player player) => false;
        
        public override bool CanRightClick() => Main.mouseItem.type > ItemID.None;
        
        public override void RightClick(Player player) {
            if (Main.mouseItem.type <= ItemID.None) return;
            
            var data = GetData(Item);
            if (data.TryAddItem(Main.mouseItem.type)) {
                //同步到网络
                if (Main.netMode == NetmodeID.MultiplayerClient) {
                    NetMessage.SendData(MessageID.SyncItem, -1, -1, null, Item.whoAmI);
                }
            }
        }
        
        public override void PostDrawInInventory(SpriteBatch spriteBatch, Vector2 position
            , Rectangle frame, Color drawColor, Color itemColor, Vector2 origin, float scale) {
            
            var filterItems = GetData(Item).Items;
            if (filterItems.Count == 0) {
                return;
            }

            const float displayRadius = 35f;
            float angleIncrement = MathHelper.TwoPi / filterItems.Count;
            Vector2 drawCenter = position;

            for (int i = 0; i < filterItems.Count; i++) {
                int itemType = filterItems[i];
                if (itemType <= ItemID.None) continue;

                float currentAngle = angleIncrement * i - MathHelper.PiOver2;
                Vector2 offset = new Vector2((float)Math.Cos(currentAngle), (float)Math.Sin(currentAngle)) * displayRadius;
                Vector2 itemPos = drawCenter + offset;

                VaultUtils.SafeLoadItem(itemType);
                VaultUtils.SimpleDrawItem(spriteBatch, itemType, itemPos, itemWidth: 26, scale * 0.75f, 0, Color.White);
            }
        }
    }

    internal class ItemFilterSlot : UIHandle
    {
        internal int Item;
        internal float sengs;
        internal float hoverSengs;
        internal float slotIndex;
        internal float parentAlpha = 1f; //从父UI接收的透明度
        
        //动画相关
        private float bounceAnimation = 0f;
        private float rotationAnimation = 0f;
        private bool isRemoving = false;
        private float removeAnimation = 1f;
        
        public override LayersModeEnum LayersMode => LayersModeEnum.None;
        public override bool Active => true;
        
        public override void Update() {
            //渐入动画（弹性效果）
            if (sengs < 1f) {
                sengs += 0.15f;
                bounceAnimation = (float)Math.Sin(sengs * MathHelper.Pi) * 0.3f;
            }
            else {
                sengs = 1f;
                bounceAnimation *= 0.9f; //衰减弹跳
            }
            
            sengs = Math.Min(sengs, 1f);
            
            //移除动画
            if (isRemoving) {
                removeAnimation = Math.Max(removeAnimation - 0.2f, 0f);
                rotationAnimation += 0.3f;
                if (removeAnimation <= 0f) {
                    return; //动画完成，等待被真正移除
                }
            }
            
            UIHitBox = DrawPosition.GetRectangle((int)(60 * sengs * removeAnimation));
            hoverInMainPage = UIHitBox.Intersects(MousePosition.GetRectangle(1)) && sengs > 0.8f && !isRemoving;
            
            if (hoverInMainPage) {
                ItemFilterUI.Instance.hoverSlotIndex = slotIndex;
                hoverSengs = Math.Min(hoverSengs + 0.2f, 1f);

                //悬停时的旋转动画
                rotationAnimation = MathHelper.Lerp(rotationAnimation, (float)Math.Sin(Main.GlobalTimeWrappedHourly * 2f) * 0.1f, 0.1f);

                if (keyLeftPressState == KeyPressState.Pressed) {
                    SoundEngine.PlaySound(SoundID.Grab with { Volume = 0.9f, Pitch = -0.1f });
                    isRemoving = true;
                    
                    //生成移除粒子效果
                    if (Main.netMode != NetmodeID.Server) {
                        for (int i = 0; i < 8; i++) {
                            Vector2 velocity = Main.rand.NextVector2Circular(3, 3);
                            Dust dust = Dust.NewDustDirect(
                                UIHitBox.TopLeft(), UIHitBox.Width, UIHitBox.Height,
                                DustID.Electric, velocity.X, velocity.Y, 100, Color.Cyan, 1.2f
                            );
                            dust.noGravity = true;
                            dust.fadeIn = 1.5f;
                        }
                    }
                    
                    //延迟实际移除，等动画播放
                    System.Threading.Tasks.Task.Delay(200).ContinueWith(_ => {
                        if (Main.netMode != NetmodeID.Server) {
                            ItemFilterUI.Instance.RemoveSlot(this);
                        }
                    });
                }
            }
            else {
                hoverSengs = Math.Max(hoverSengs - 0.2f, 0f);
                
                //非悬停时归零旋转
                if (!isRemoving) {
                    rotationAnimation = MathHelper.Lerp(rotationAnimation, 0f, 0.15f);
                }
            }
        }
        
        public override void Draw(SpriteBatch spriteBatch) {
            if (removeAnimation <= 0.01f) return;
            
            //综合透明度
            float totalAlpha = sengs * parentAlpha * removeAnimation;
            if (totalAlpha <= 0.01f) return;
            
            //计算缩放（悬停时放大 + 弹跳效果）
            float baseScale = 1f + bounceAnimation;
            float hoverScale = 1f + hoverSengs * 0.15f;
            float totalScale = baseScale * hoverScale * removeAnimation;
            
            Vector2 center = DrawPosition + UIHitBox.Size() / 2f;
            
            //绘制选中高亮框（悬停时）
            if (hoverSengs > 0.01f) {
                float glowPulse = 1f + (float)Math.Sin(Main.GlobalTimeWrappedHourly * 4f) * 0.1f;
                VaultUtils.DrawBorderedRectangle(spriteBatch, CWRAsset.UI_JAR.Value, 16, 
                    new Rectangle(
                        (int)(center.X - UIHitBox.Width / 2f * totalScale),
                        (int)(center.Y - UIHitBox.Height / 2f * totalScale),
                        (int)(UIHitBox.Width * totalScale),
                        (int)(UIHitBox.Height * totalScale)
                    ),
                    Color.Gold * (hoverSengs * totalAlpha * glowPulse), 
                    Color.Aqua * (hoverSengs * totalAlpha), 
                    1.1f + hoverSengs * 0.1f, Vector2.Zero);
            }
            
            //绘制槽位背景
            Color bgColor = Color.Lerp(Color.Azure, Color.LightCyan, hoverSengs) * totalAlpha;
            Color borderColor = Color.Lerp(Color.Aqua, Color.Cyan, hoverSengs) * totalAlpha;
            
            VaultUtils.DrawBorderedRectangle(spriteBatch, CWRAsset.UI_JAR.Value, 16, 
                new Rectangle(
                    (int)(center.X - UIHitBox.Width / 2f * totalScale),
                    (int)(center.Y - UIHitBox.Height / 2f * totalScale),
                    (int)(UIHitBox.Width * totalScale),
                    (int)(UIHitBox.Height * totalScale)
                ),
                bgColor, borderColor, totalScale, Vector2.Zero);

            //绘制物品图标
            VaultUtils.SafeLoadItem(Item);
            float itemBrightness = 0.6f + 0.4f * hoverSengs;
            Color itemColor = new Color(itemBrightness, itemBrightness, itemBrightness, totalAlpha);
            
            //物品缩放和旋转
            float itemScale = (1.2f + hoverSengs * 0.3f) * totalScale;
            
            VaultUtils.SimpleDrawItem(spriteBatch, Item, center, 
                40, itemScale, rotationAnimation, itemColor);
            
            //移除动画时的粒子轨迹效果
            if (isRemoving && Main.netMode != NetmodeID.Server) {
                float particleChance = 1f - removeAnimation;
                if (Main.rand.NextFloat() < particleChance * 0.5f) {
                    Vector2 particlePos = center + Main.rand.NextVector2Circular(20, 20);
                    Dust dust = Dust.NewDustDirect(
                        particlePos - Vector2.One * 4, 8, 8,
                        DustID.Electric, 0, -1, 100, Color.Cyan, 0.8f
                    );
                    dust.noGravity = true;
                }
            }
            
            //悬停提示文本
            if (hoverSengs > 0.7f) {
                string hintText = ItemFilterUI.HintText2.Value;
                Vector2 textSize = FontAssets.MouseText.Value.MeasureString(hintText);
                Vector2 textPos = center + new Vector2(0, UIHitBox.Height / 2f + 14);
                float textAlpha = (hoverSengs - 0.7f) / 0.3f * totalAlpha;
                
                //文字阴影
                spriteBatch.DrawString(FontAssets.MouseText.Value, hintText, 
                    textPos + Vector2.One, Color.Black * (textAlpha * 0.6f), 0f, 
                    textSize / 2f, 0.7f, SpriteEffects.None, 0f);
                
                //文字主体
                spriteBatch.DrawString(FontAssets.MouseText.Value, hintText, 
                    textPos, Color.White * textAlpha, 0f, 
                    textSize / 2f, 0.7f, SpriteEffects.None, 0f);
            }
        }
    }

    internal class ItemFilterUI : UIHandle, ILocalizedModType
    {
        [VaultLoaden(CWRConstant.UI + "ItemFilterUI")]
        internal static Texture2D UITex = null;
        
        private bool CanOpen;
        internal float sengs;
        internal float hoverSengs;
        internal Item ItemFilter;
        internal float hoverSlotIndex;
        internal const int RowNum = 6;
        internal const int MaxSlots = RowNum * RowNum;
        
        internal static ItemFilterUI Instance => UIHandleLoader.GetUIHandleOfType<ItemFilterUI>();
        internal List<ItemFilterSlot> Slots = [];
        
        //缓存上次的数据版本，避免重复初始化
        private int lastDataVersion = -1;
        
        //动画相关
        private float rotationAnimation = 0f;
        private float scaleAnimation = 0f;
        private Vector2 targetDrawPosition;
        private Vector2 openPosition;
        private bool isFirstOpen = true;

        public static LocalizedText HintText;
        public static LocalizedText HintText2;
        public static LocalizedText CloseHint;
        public override bool Active {
            get => CanOpen || sengs > 0;
            set => CanOpen = value;
        }

        public string LocalizationCategory => "UI";
        public override void SetStaticDefaults() {
            HintText = this.GetLocalization(nameof(HintText), () => "左键点击可添加项目");
            HintText2 = this.GetLocalization(nameof(HintText2), () => "左键点击可移除项目");
            CloseHint = this.GetLocalization(nameof(CloseHint), () => "[ESC]键或[右键点击]可关闭");
        }

        /// <summary>
        /// 添加槽位（带验证和动画）
        /// </summary>
        internal ItemFilterSlot AddSlot(int itemID) {
            if (itemID <= ItemID.None) return null;
            if (Slots.Any(s => s.Item == itemID)) return null;
            if (Slots.Count >= MaxSlots) return null;
            
            ItemFilterSlot slot = new ItemFilterSlot {
                Item = itemID,
                sengs = 0f //从0开始，产生渐入效果
            };
            Slots.Add(slot);
            return slot;
        }
        
        /// <summary>
        /// 移除槽位并同步数据
        /// </summary>
        internal void RemoveSlot(ItemFilterSlot slot) {
            if (Slots.Remove(slot)) {
                var data = ItemFilter.GetGlobalItem<ItemFilterData>();
                data.TryRemoveItem(slot.Item);
                
                //网络同步
                if (Main.netMode == NetmodeID.MultiplayerClient) {
                    NetMessage.SendData(MessageID.SyncItem, -1, -1, null, ItemFilter.whoAmI);
                }
                
                //重新排列剩余槽位的索引，产生流畅的移动效果
                for (int i = 0; i < Slots.Count; i++) {
                    Slots[i].slotIndex = i;
                }
            }
        }
        
        /// <summary>
        /// 初始化UI槽位
        /// </summary>
        public void Initialize() {
            if (ItemFilter == null || ItemFilter.IsAir) return;
            
            var data = ItemFilter.GetGlobalItem<ItemFilterData>();
            
            //只有数据版本变化时才重新初始化
            if (data.DataVersion == lastDataVersion && Slots.Count == data.Items.Count) {
                return;
            }

            Slots.Clear();
            foreach (var itemID in data.Items) {
                AddSlot(itemID);
            }
            
            lastDataVersion = data.DataVersion;
        }
        
        public override void Update() {
            rotationAnimation = 0f;
            //动画更新
            if (CanOpen) {
                //首次打开时记录打开位置
                if (sengs == 0f) {
                    Initialize();
                    openPosition = MousePosition - UITex.Size() / 2f;
                    targetDrawPosition = openPosition;
                    DrawPosition = openPosition;
                    isFirstOpen = true;
                }

                //渐入动画（使用缓出效果）
                float animSpeed = isFirstOpen ? 0.12f : 0.15f;
                sengs = Math.Min(sengs + animSpeed, 1f);

                //缩放动画（弹性效果）
                scaleAnimation = CWRUtils.EaseOutElastic(sengs);
                
                if (sengs >= 1f) {
                    isFirstOpen = false;
                }
            }
            else {
                //渐出动画（使用缓入效果）
                if (sengs > 0f) {
                    sengs = Math.Max(sengs - 0.18f, 0f);

                    //缩放动画
                    scaleAnimation = CWRUtils.EaseInCubic(sengs);
                }
                
                if (sengs == 0f) {
                    Slots.Clear(); //完全关闭后清空槽位，节省性能
                    lastDataVersion = -1;
                }
            }

            hoverSlotIndex = -1;

            //计算UI框体（使用缓动后的缩放）
            float animatedScale = scaleAnimation;
            UIHitBox = DrawPosition.GetRectangle((int)(420 * animatedScale));
            hoverInMainPage = UIHitBox.Intersects(MousePosition.GetRectangle(1)) && sengs > 0.8f;

            //自适应屏幕边界（平滑移动）
            Vector2 clampedPosition = new Vector2(
                MathHelper.Clamp(targetDrawPosition.X, 0, Main.screenWidth - UIHitBox.Width),
                MathHelper.Clamp(targetDrawPosition.Y, 0, Main.screenHeight - UIHitBox.Height)
            );
            DrawPosition = Vector2.Lerp(DrawPosition, clampedPosition, 0.2f);

            //更新所有槽位（带延迟动画）
            for (int i = 0; i < Slots.Count; i++) {
                ItemFilterSlot slot = Slots[i];
                slot.slotIndex = i;
                
                //计算槽位位置（网格布局）
                Vector2 basePosition = DrawPosition + new Vector2(40, 40) * animatedScale;
                int row = i / RowNum;
                int col = i % RowNum;
                
                slot.DrawPosition = basePosition + new Vector2(
                    col * 65 * animatedScale,
                    row * 65 * animatedScale
                );
                
                slot.Update();
            }

            if (hoverInMainPage) {
                player.mouseInterface = true;
                hoverSengs = Math.Min(hoverSengs + 0.15f, 1f);

                //添加物品到过滤器
                if (keyLeftPressState == KeyPressState.Pressed && Main.mouseItem.type > ItemID.None && hoverSlotIndex == -1) {
                    if (Slots.Count < MaxSlots) {
                        var data = ItemFilter.GetGlobalItem<ItemFilterData>();
                        if (data.TryAddItem(Main.mouseItem.type)) {
                            SoundEngine.PlaySound(SoundID.Grab with { Volume = 0.8f, Pitch = 0.1f });
                            AddSlot(Main.mouseItem.type);
                            
                            //同步到网络
                            if (Main.netMode == NetmodeID.MultiplayerClient) {
                                NetMessage.SendData(MessageID.SyncItem, -1, -1, null, ItemFilter.whoAmI);
                            }
                        }
                    }
                    else {
                        //满了，播放提示音
                        SoundEngine.PlaySound(SoundID.MenuClose with { Volume = 0.6f, Pitch = -0.3f });
                    }
                }
                
                //右键快速关闭
                if (keyRightPressState == KeyPressState.Pressed && hoverSlotIndex == -1) {
                    CanOpen = false;
                    SoundEngine.PlaySound(SoundID.MenuClose with { Volume = 0.7f });
                }
            }

            //ESC快捷关闭
            if (CanOpen && Main.keyState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.Escape) 
                && !Main.oldKeyState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.Escape)) {
                CanOpen = false;
                SoundEngine.PlaySound(SoundID.MenuClose with { Volume = 0.7f });
            }
        }
        
        public override void Draw(SpriteBatch spriteBatch) {
            if (sengs <= 0.01f) return;
            
            //计算动画参数
            float animatedScale = scaleAnimation;
            float alpha = sengs;
            Vector2 center = DrawPosition + UITex.Size() / 2f;
            
            //绘制背景阴影（柔和的外发光）
            if (alpha > 0.1f) {
                for (int i = 0; i < 3; i++) {
                    float shadowOffset = (3 - i) * 4f;
                    float shadowAlpha = alpha * 0.15f * (i + 1) / 3f;
                    
                    spriteBatch.Draw(UITex, center, null, 
                        Color.Black * shadowAlpha, rotationAnimation, UITex.Size() / 2f, 
                        animatedScale + shadowOffset / UITex.Width, SpriteEffects.None, 0);
                }
            }
            
            //绘制悬停光晕效果
            if (hoverSengs > 0.01f) {
                float glowPulse = 1f + (float)Math.Sin(Main.GlobalTimeWrappedHourly * 3f) * 0.05f;
                spriteBatch.Draw(UITex, center, null, 
                    Color.BlueViolet with { A = 0 } * (hoverSengs * 0.6f), 
                    rotationAnimation, UITex.Size() / 2f, 
                    animatedScale * (1f + 0.03f * hoverSengs * glowPulse), 
                    SpriteEffects.None, 0);
            }

            //绘制主UI背景
            spriteBatch.Draw(UITex, center, null, 
                Color.White * alpha, rotationAnimation, UITex.Size() / 2f, 
                animatedScale, SpriteEffects.None, 0);
            
            //绘制边框高光（增强立体感）
            if (alpha > 0.3f) {
                Color highlightColor = Color.Lerp(Color.Cyan, Color.White, 0.5f) * (alpha * 0.4f);
                spriteBatch.Draw(UITex, center + new Vector2(-1, -1), null, 
                    highlightColor, rotationAnimation, UITex.Size() / 2f, 
                    animatedScale, SpriteEffects.None, 0);
            }
            
            //绘制槽位计数器
            if (sengs > 0.7f) {
                string countText = $"{Slots.Count}/{MaxSlots}";
                Vector2 textSize = FontAssets.MouseText.Value.MeasureString(countText);
                Vector2 textPos = center + new Vector2(0, UITex.Height / 2f * animatedScale + 10);
                float textAlpha = (sengs - 0.7f) / 0.3f;
                
                //文字阴影
                for (int i = 0; i < 4; i++) {
                    Vector2 offset = new Vector2(
                        (float)Math.Cos(MathHelper.PiOver4 * i * 2) * 2,
                        (float)Math.Sin(MathHelper.PiOver4 * i * 2) * 2
                    );
                    Utils.DrawBorderString(spriteBatch, countText, 
                        textPos + offset - textSize / 2f, Color.Black * (textAlpha * 0.5f), 1f);
                }
                
                //文字主体
                Color textColor = Slots.Count >= MaxSlots 
                    ? Color.Lerp(Color.Yellow, Color.OrangeRed, (float)Math.Sin(Main.GlobalTimeWrappedHourly * 4f) * 0.5f + 0.5f)
                    : Color.LightCyan;
                    
                Utils.DrawBorderString(spriteBatch, countText, 
                    textPos - textSize / 2f, textColor * textAlpha, 1f);
            }
            
            //绘制所有槽位（带透明度）
            foreach (var slot in Slots) {
                slot.parentAlpha = alpha; //传递父级透明度
                slot.Draw(spriteBatch);
            }
            
            //绘制提示文本（当UI打开但没有物品时）
            if (Slots.Count == 0 && sengs > 0.8f) {
                string hintText = HintText.Value;
                Vector2 hintSize = FontAssets.MouseText.Value.MeasureString(hintText);
                Vector2 hintPos = center;
                float hintAlpha = (sengs - 0.8f) / 0.2f * (0.5f + (float)Math.Sin(Main.GlobalTimeWrappedHourly * 2f) * 0.2f);
                
                Utils.DrawBorderString(spriteBatch, hintText, 
                    hintPos + Vector2.One - hintSize / 2f, Color.Black * (hintAlpha * 0.5f), 0.8f);
                    
                Utils.DrawBorderString(spriteBatch, hintText, 
                    hintPos - hintSize / 2f, Color.LightGray * hintAlpha, 0.8f);
            }
            
            //关闭提示（右上角）
            if (hoverInMainPage && sengs > 0.9f) {
                string closeHint = CloseHint.Value;
                Vector2 closeSize = FontAssets.MouseText.Value.MeasureString(closeHint);
                Vector2 closePos = new Vector2(
                    center.X + UITex.Width / 2f * animatedScale - closeSize.X / 2f - 10,
                    center.Y - UITex.Height / 2f * animatedScale - 20
                );
                float closeAlpha = hoverSengs * 0.7f;
                
                Utils.DrawBorderString(spriteBatch, closeHint, 
                    closePos + Vector2.One, Color.Black * closeAlpha, 0.7f);
                    
                Utils.DrawBorderString(spriteBatch, closeHint, 
                    closePos, Color.White * closeAlpha, 0.7f);
            }
        }
    }
}
