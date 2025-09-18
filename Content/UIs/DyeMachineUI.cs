using CalamityOverhaul.Content.Industrials.ElectricPowers;
using CalamityOverhaul.Content.Industrials.Modifys;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.UIs
{
    #region 基类
    /// <summary>
    /// 染色机器UI的抽象基类，处理通用窗口逻辑
    /// </summary>
    internal abstract class BaseDyeMachineUI : UIHandle
    {
        //是否可以打开UI
        internal bool CanOpen;
        //UI缩放动画的进度值
        internal float sengs;
        //鼠标悬停在UI上时的发光动画进度
        internal float hoverSengs;

        //定义三个功能槽的抽象属性，子类必须实现它们
        public abstract BaseDyeMachineSlot DyeSlot { get; }
        public abstract BaseDyeMachineSlot BeDyedItem { get; }
        public abstract BaseDyeMachineSlot ResultDyedItem { get; }

        //通过一个object类型的属性来持有对应的物块实体，增加通用性
        public object TileEntity { get; set; }

        public override bool Active {
            get {
                return CanOpen || sengs > 0f;
            }
            set {
                CanOpen = value;
            }
        }

        public override void Update() {
            //处理UI的打开和关闭动画
            if (CanOpen) {
                if (sengs < 1f) {
                    sengs += 0.1f;
                }
            }
            else {
                if (sengs > 0f) {
                    sengs -= 0.1f;
                }
            }

            //确保动画进度在0和1之间
            sengs = MathHelper.Clamp(sengs, 0, 1f);

            //根据动画进度设置UI大小
            Size = new Vector2(280 * sengs);

            //设置UI位置在屏幕中下方
            DrawPosition = new Vector2(Main.screenWidth / 2, Main.screenHeight / 12 * 9) - Size / 2;

            //更新UI的碰撞箱
            UIHitBox = DrawPosition.GetRectangle(Size);
            //检测鼠标是否悬停在主窗口上
            hoverInMainPage = UIHitBox.Intersects(MousePosition.GetRectangle(1)) && sengs > 0.8f;

            //防止UI拖动到屏幕外
            DrawPosition.X = MathHelper.Clamp(DrawPosition.X, 0, Main.screenWidth - UIHitBox.Width);
            DrawPosition.Y = MathHelper.Clamp(DrawPosition.Y, 0, Main.screenHeight - UIHitBox.Height);

            //如果鼠标在UI上，则占用鼠标交互
            if (hoverInMainPage) {
                player.mouseInterface = true;
            }

            //根据UI缩放，动态调整各个插槽的位置
            Vector2 offsetTopLeft = new Vector2(60, 60) * sengs;
            DyeSlot.DrawPosition = DrawPosition + offsetTopLeft;
            DyeSlot.Update();
            BeDyedItem.DrawPosition = DyeSlot.DrawPosition + new Vector2(0, 100) * sengs;
            BeDyedItem.Update();
            ResultDyedItem.DrawPosition = DyeSlot.DrawPosition + new Vector2(100, 50) * sengs;
            ResultDyedItem.Update();
        }

        public override void Draw(SpriteBatch spriteBatch) {
            //如果鼠标悬停，绘制一个额外的金色边框作为高亮
            if (hoverSengs > 0) {
                VaultUtils.DrawBorderedRectangle(spriteBatch, CWRAsset.DraedonContactPanel.Value, 16, UIHitBox, Color.Gold * hoverSengs, Color.Aqua * hoverSengs, 1.01f, Vector2.Zero);
            }
            //绘制UI主面板
            VaultUtils.DrawBorderedRectangle(spriteBatch, CWRAsset.DraedonContactPanel.Value, 12, UIHitBox, Color.Azure, Color.Aqua, 1, Vector2.Zero);
            //绘制所有插槽
            DyeSlot.Draw(spriteBatch);
            BeDyedItem.Draw(spriteBatch);
            ResultDyedItem.Draw(spriteBatch);
        }
    }

    /// <summary>
    /// 染色机器中物品槽的抽象基类
    /// </summary>
    internal abstract class BaseDyeMachineSlot : UIHandle
    {
        //槽内存储的物品
        internal Item Item = new();
        //槽位缩放动画的进度
        internal float sengs;
        //鼠标悬停在槽上时的发光动画进度
        internal float hoverSengs;
        //槽位的索引（未使用，但保留）
        internal float slotIndex;
        public override LayersModeEnum LayersMode => LayersModeEnum.None;
        public override bool Active => true;
        //能否在左键点击时放入物品
        public virtual bool CanCheckLeft(Item heldItem) => true;
        //在进行放入/取出操作前的预检查
        public virtual bool PreCheckLeft(Item heldItem) => true;
        //每个槽的特殊更新逻辑，应当在逻辑帧中被调用
        public virtual void UpdateSlot() {

        }
        public override void Update() {
            //处理槽位的出现动画
            if (sengs < 1f) {
                sengs += 0.1f;
            }
            //更新槽位的碰撞箱
            UIHitBox = DrawPosition.GetRectangle((int)(60 * sengs));
            //检测鼠标是否悬停在槽位上
            hoverInMainPage = UIHitBox.Intersects(MousePosition.GetRectangle(1)) && sengs > 0.8f;
            if (hoverInMainPage) {
                //设置高亮效果
                ItemFilterUI.Instance.hoverSlotIndex = slotIndex;
                if (hoverSengs < 1f) {
                    hoverSengs += 0.1f;
                }

                //处理鼠标左键点击逻辑
                if (keyLeftPressState == KeyPressState.Pressed) {
                    Item heldItem = Main.mouseItem;
                    //进行预检查
                    if (PreCheckLeft(heldItem)) {
                        //如果手上有物品
                        if (heldItem.type != ItemID.None) {
                            //检查是否能放入
                            if (CanCheckLeft(heldItem)) {
                                //如果槽位是空的
                                if (Item.type == ItemID.None) {
                                    SoundEngine.PlaySound(SoundID.Grab);
                                    //将物品放入槽位（只放一个）
                                    Item = heldItem.Clone();
                                    Item.stack = 1;
                                    heldItem.stack--;
                                    if (heldItem.stack <= 0) {
                                        heldItem.TurnToAir();
                                    }
                                }
                                else {
                                    //如果槽位有物品，播放一个提示音
                                    SoundEngine.PlaySound(SoundID.MenuTick);
                                }
                            }
                            else {
                                //不能放入，播放提示音
                                SoundEngine.PlaySound(SoundID.MenuTick);
                            }
                        }
                        else {
                            //如果手上没有物品
                            if (Item.type != ItemID.None) {
                                //从槽位中取出物品
                                SoundEngine.PlaySound(SoundID.Grab);
                                Main.mouseItem = Item.Clone();
                                Item.TurnToAir();
                            }
                        }
                    }
                }
            }
            else {
                //鼠标未悬停时，逐渐减弱高亮效果
                if (hoverSengs > 0f) {
                    hoverSengs -= 0.1f;
                }
            }
        }
        public override void Draw(SpriteBatch spriteBatch) {
            //绘制高亮边框
            if (hoverSengs > 0) {
                VaultUtils.DrawBorderedRectangle(spriteBatch, CWRAsset.UI_JAR.Value, 16, UIHitBox, Color.Gold * hoverSengs, Color.Aqua * hoverSengs, 1.1f, Vector2.Zero);
            }
            //绘制槽位背景
            VaultUtils.DrawBorderedRectangle(spriteBatch, CWRAsset.UI_JAR.Value, 16, UIHitBox, Color.Azure, Color.Aqua, 1, Vector2.Zero);

            if (hoverInMainPage && Item.type > ItemID.None) {
                Main.HoverItem = Item.Clone();
                Main.hoverItemName = Item.Name;
            }

            //安全加载物品纹理
            VaultUtils.SafeLoadItem(Item);
            //根据动画进度和悬停状态计算物品的亮度和缩放
            float mode = 0.6f + 0.4f * hoverSengs;
            Color itemColor = new Color(mode, mode, mode, 1f) * sengs;
            //绘制物品
            VaultUtils.SimpleDrawItem(spriteBatch, Item.type, DrawPosition + UIHitBox.Size() / 2, 64, 1.2f + hoverSengs * 0.2f, 0f, itemColor);
        }
    }

    /// <summary>
    /// “待染色物品”槽的抽象基类，处理通用进度条逻辑
    /// </summary>
    internal abstract class BaseBeDyedItemSlot : BaseDyeMachineSlot
    {
        //染色进度，范围从0到1
        public float DyeProgress = 0f;

        /// <summary>
        /// 绘制进度条的通用方法
        /// </summary>
        protected void DrawProgress(SpriteBatch spriteBatch) {
            //如果染色正在进行中，绘制一个进度条
            if (DyeProgress > 0f && DyeProgress < 1f) {
                //获取槽的矩形区域
                Rectangle hitbox = UIHitBox;
                //根据进度计算进度条的高度
                int progressHeight = (int)(hitbox.Height * DyeProgress);
                //定义进度条的绘制区域（从下往上填充）
                Rectangle progressBar = new Rectangle(hitbox.X, hitbox.Y + hitbox.Height - progressHeight, hitbox.Width, progressHeight);
                //使用半透明的绿色绘制进度条
                spriteBatch.Draw(TextureAssets.MagicPixel.Value, progressBar, Color.Green * 0.5f);
            }
        }
    }

    #endregion

    #region 染缸 UI 实现

    /// <summary>
    /// 染缸（DyeVat）的UI主窗口
    /// </summary>
    internal class DyeVatUI : BaseDyeMachineUI
    {
        //染缸的物块实体，用于逻辑交互
        internal DyeVatTP DyeVatTP;
        //染料插槽
        internal DyeVatDyeSlot _dyeSlot = new();
        //待染色物品插槽
        internal DyeVatBeDyedItemSlot _beDyedItem = new();
        //染色结果物品插槽
        internal DyeVatResultDyedItemSlot _resultDyedItem = new();

        //实现基类的抽象属性
        public override BaseDyeMachineSlot DyeSlot => _dyeSlot;
        public override BaseDyeMachineSlot BeDyedItem => _beDyedItem;
        public override BaseDyeMachineSlot ResultDyedItem => _resultDyedItem;

        //通过单例模式获取UI实例
        internal static DyeVatUI Instance => UIHandleLoader.GetUIHandleOfType<DyeVatUI>();
    }

    /// <summary>
    /// 染缸的染料插槽
    /// </summary>
    internal class DyeVatDyeSlot : BaseDyeMachineSlot
    {
        //重写方法，只允许染料（dye > 0）放入
        public override bool CanCheckLeft(Item heldItem) => heldItem.dye > 0 || heldItem.IsWaterBucket();
        //染料槽是驱动整个染色过程的核心
        public override void UpdateSlot() {
            //获取对其他槽位的引用
            DyeVatBeDyedItemSlot dyedItemSlot = DyeVatUI.Instance._beDyedItem;
            DyeVatResultDyedItemSlot resultDyedItemSlot = DyeVatUI.Instance._resultDyedItem;

            //检查条件：染料槽和待染色槽都有物品，并且结果槽是空的
            if (Item.type > ItemID.None && dyedItemSlot.Item.type > ItemID.None && resultDyedItemSlot.Item.type == ItemID.None) {
                //染缸不消耗电力，直接增加染色进度，但速度更慢
                dyedItemSlot.DyeProgress += 0.0006f;

                if (dyedItemSlot.DyeProgress >= 1f) {
                    //染色完成
                    dyedItemSlot.DyeProgress = 0f; //重置进度

                    //将被染色的物品克隆到结果槽
                    resultDyedItemSlot.Item = dyedItemSlot.Item.Clone();
                    if (!Item.IsWaterBucket()) {
                        //设置物品的自定义染色ID，这个ID来自染料槽中的染料
                        resultDyedItemSlot.Item.CWR().ByDye = Item.type;
                        //染缸会消耗染料
                        Item.stack--;
                        if (Item.stack <= 0) {
                            Item.TurnToAir();
                        }
                    }
                    else {
                        resultDyedItemSlot.Item.CWR().ByDye = 0;//如果是水桶类型物品，就洗掉染色
                        if (Item.consumable) {
                            Item.TurnToAir();//水桶会被消耗的，但无限水桶不会
                        }
                    }

                    //消耗掉原物品
                    dyedItemSlot.Item.TurnToAir();

                    //播放一个完成的音效
                    SoundEngine.PlaySound(SoundID.Item37);
                }
            }
            else {
                //如果缺少物品（比如被玩家中途取走），则重置进度
                if (dyedItemSlot.DyeProgress > 0f) {
                    dyedItemSlot.DyeProgress = 0f;
                }
            }
        }
    }

    /// <summary>
    /// 染缸的待染色物品插槽
    /// </summary>
    internal class DyeVatBeDyedItemSlot : BaseBeDyedItemSlot
    {
        public override void Draw(SpriteBatch spriteBatch) {
            //先绘制基础的槽和物品
            base.Draw(spriteBatch);
            //应用预览效果
            int beDye = DyeVatUI.Instance.DyeSlot.Item.type;
            CWRItems.AddByDyeEffectByUI(Item, beDye);
            //绘制进度条
            DrawProgress(spriteBatch);
            //关闭预览效果
            CWRItems.CloseByDyeEffectByUI(beDye);
        }
    }

    /// <summary>
    /// 染缸的染色结果物品插槽
    /// </summary>
    internal class DyeVatResultDyedItemSlot : BaseDyeMachineSlot
    {
        //重写预检查，不允许放入任何物品，只允许取出
        public override bool PreCheckLeft(Item heldItem) {
            if (heldItem.type == ItemID.None && Item.type != ItemID.None) {
                SoundEngine.PlaySound(SoundID.Grab);
                Main.mouseItem = Item.Clone();
                Item.TurnToAir();
            }
            return false;
        }

        public override void Draw(SpriteBatch spriteBatch) {
            //因为结果物品也需要展示染色效果，所以需要重写Draw方法
            //绘制高亮边框
            if (hoverSengs > 0) {
                VaultUtils.DrawBorderedRectangle(spriteBatch, CWRAsset.UI_JAR.Value, 16, UIHitBox, Color.Gold * hoverSengs, Color.Aqua * hoverSengs, 1.1f, Vector2.Zero);
            }
            //绘制槽位背景
            VaultUtils.DrawBorderedRectangle(spriteBatch, CWRAsset.UI_JAR.Value, 16, UIHitBox, Color.Azure, Color.Aqua, 1, Vector2.Zero);

            if (hoverInMainPage && Item.type > ItemID.None) {
                Main.HoverItem = Item.Clone();
                Main.hoverItemName = Item.Name;
            }

            //安全加载物品纹理
            VaultUtils.SafeLoadItem(Item);
            //根据动画进度和悬停状态计算物品的亮度和缩放
            float mode = 0.6f + 0.4f * hoverSengs;
            Color itemColor = new Color(mode, mode, mode, 1f) * sengs;

            if (Item.type > ItemID.None) {
                //应用最终的染色效果进行绘制
                int beDye = Item.CWR().ByDye; //从物品自身获取应用的染料ID
                CWRItems.AddByDyeEffectByUI(Item, beDye);
            }
            
            //绘制物品
            VaultUtils.SimpleDrawItem(spriteBatch, Item.type, DrawPosition + UIHitBox.Size() / 2, 64, 1.2f + hoverSengs * 0.2f, 0f, itemColor);

            if (Item.type > ItemID.None) {
                CWRItems.CloseByDyeEffectByUI(Item.CWR().ByDye);
            }
        }
    }
    #endregion

    #region 光谱仪 UI 实现

    /// <summary>
    /// 光谱仪（染色机）的UI主窗口 (重构后)
    /// </summary>
    internal class SpectrometerUI : BaseDyeMachineUI
    {
        //光谱仪的物块实体，用于逻辑交互
        internal SpectrometerTP SpectrometerTP;
        //染料插槽
        internal SpectrometerDyeSlot _dyeSlot = new();
        //待染色物品插槽
        internal SpectrometerbeDyedItemSlot _beDyedItem = new();
        //染色结果物品插槽
        internal SpectrometerResultDyedItemSlot _resultDyedItem = new();

        //实现基类的抽象属性
        public override BaseDyeMachineSlot DyeSlot => _dyeSlot;
        public override BaseDyeMachineSlot BeDyedItem => _beDyedItem;
        public override BaseDyeMachineSlot ResultDyedItem => _resultDyedItem;

        //通过单例模式获取UI实例
        internal static SpectrometerUI Instance => UIHandleLoader.GetUIHandleOfType<SpectrometerUI>();
    }

    /// <summary>
    /// 光谱仪的染料插槽
    /// </summary>
    internal class SpectrometerDyeSlot : BaseDyeMachineSlot
    {
        //重写方法，只允许染料（dye > 0）放入
        public override bool CanCheckLeft(Item heldItem) => heldItem.dye > 0 || heldItem.IsWaterBucket();
        //染料槽是驱动整个染色过程的核心
        public override void UpdateSlot() {
            //获取对其他槽位的引用
            SpectrometerbeDyedItemSlot dyedItemSlot = SpectrometerUI.Instance._beDyedItem;
            SpectrometerResultDyedItemSlot resultDyedItemSlot = SpectrometerUI.Instance._resultDyedItem;

            //检查条件：染料槽和待染色槽都有物品，并且结果槽是空的
            if (Item.type > ItemID.None && dyedItemSlot.Item.type > ItemID.None && resultDyedItemSlot.Item.type == ItemID.None) {
                var tp = SpectrometerUI.Instance.SpectrometerTP;
                if (tp != null && tp.MachineData.UEvalue > 1f) {
                    //光谱仪消耗电力，染色速度快，且不消耗染料
                    tp.MachineData.UEvalue--;
                    //增加染色进度
                    dyedItemSlot.DyeProgress += 0.002f;
                }

                if (dyedItemSlot.DyeProgress >= 1f) {
                    //染色完成
                    dyedItemSlot.DyeProgress = 0f; //重置进度

                    //将被染色的物品克隆到结果槽
                    resultDyedItemSlot.Item = dyedItemSlot.Item.Clone();
                    if (!Item.IsWaterBucket()) {
                        //设置物品的自定义染色ID，这个ID来自染料槽中的染料
                        resultDyedItemSlot.Item.CWR().ByDye = Item.type;
                    }
                    else {
                        resultDyedItemSlot.Item.CWR().ByDye = 0;
                        if (Item.consumable) {
                            Item.TurnToAir();//水桶会被消耗的，但无限水桶不会
                        }
                    }

                    //消耗掉原物品
                    dyedItemSlot.Item.TurnToAir();

                    //播放一个完成的音效
                    SoundEngine.PlaySound(SoundID.Item37);
                }
            }
            else {
                //如果缺少物品（比如被玩家中途取走），则重置进度
                if (dyedItemSlot.DyeProgress > 0f) {
                    dyedItemSlot.DyeProgress = 0f;
                }
            }
        }
    }

    /// <summary>
    /// 光谱仪的待染色物品插槽
    /// </summary>
    internal class SpectrometerbeDyedItemSlot : BaseBeDyedItemSlot
    {
        public override void Draw(SpriteBatch spriteBatch) {
            //先绘制基础的槽和物品
            base.Draw(spriteBatch);
            //应用预览效果
            int beDye = SpectrometerUI.Instance.DyeSlot.Item.type;
            CWRItems.AddByDyeEffectByUI(Item, beDye);
            //绘制进度条
            DrawProgress(spriteBatch);
            //关闭预览效果
            CWRItems.CloseByDyeEffectByUI(beDye);
        }
    }

    /// <summary>
    /// 光谱仪的染色结果物品插槽
    /// </summary>
    internal class SpectrometerResultDyedItemSlot : BaseDyeMachineSlot
    {
        //重写预检查，不允许放入任何物品，只允许取出
        public override bool PreCheckLeft(Item heldItem) {
            if (heldItem.type == ItemID.None && Item.type != ItemID.None) {
                SoundEngine.PlaySound(SoundID.Grab);
                Main.mouseItem = Item.Clone();
                Item.TurnToAir();
            }
            return false;
        }

        public override void Draw(SpriteBatch spriteBatch) {
            //因为结果物品也需要展示染色效果，所以需要重写Draw方法
            //绘制高亮边框
            if (hoverSengs > 0) {
                VaultUtils.DrawBorderedRectangle(spriteBatch, CWRAsset.UI_JAR.Value, 16, UIHitBox, Color.Gold * hoverSengs, Color.Aqua * hoverSengs, 1.1f, Vector2.Zero);
            }
            //绘制槽位背景
            VaultUtils.DrawBorderedRectangle(spriteBatch, CWRAsset.UI_JAR.Value, 16, UIHitBox, Color.Azure, Color.Aqua, 1, Vector2.Zero);

            if (hoverInMainPage && Item.type > ItemID.None) {
                Main.HoverItem = Item.Clone();
                Main.hoverItemName = Item.Name;
            }

            //安全加载物品纹理
            VaultUtils.SafeLoadItem(Item);
            //根据动画进度和悬停状态计算物品的亮度和缩放
            float mode = 0.6f + 0.4f * hoverSengs;
            Color itemColor = new Color(mode, mode, mode, 1f) * sengs;

            if (Item.type > ItemID.None) {
                //应用最终的染色效果进行绘制
                int beDye = Item.CWR().ByDye; //从物品自身获取应用的染料ID
                CWRItems.AddByDyeEffectByUI(Item, beDye);
            }
            
            //绘制物品
            VaultUtils.SimpleDrawItem(spriteBatch, Item.type, DrawPosition + UIHitBox.Size() / 2, 64, 1.2f + hoverSengs * 0.2f, 0f, itemColor);
            if (Item.type > ItemID.None) {
                CWRItems.CloseByDyeEffectByUI(Item.CWR().ByDye);
            }
        }
    }
    #endregion
}