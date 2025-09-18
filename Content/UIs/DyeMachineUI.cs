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
    #region 资源
    [VaultLoaden(CWRConstant.UI + "DyeMachineUI")]
    internal static class DyeMachineAsset
    {
        public static Texture2D BeDyeSymbol;
        public static Texture2D BeDyeSymbolAlt;
        public static Texture2D DyeSymbol;
        public static Texture2D DyeSymbolAlt;
        public static Texture2D OutputSymbol;
        public static Texture2D OutputSymbolAlt;
        public static Texture2D DyeVatSlot;
        public static Texture2D DyeVatUI;       
        public static Texture2D SpectrometerSlot;
        public static Texture2D SpectrometerUI;
        [VaultLoaden(CWRConstant.Masking)]
        public static Texture2D SoftGlow;
    }
    #endregion

    #region 基类与通用组件

    /// <summary>
    /// 染色机器UI的抽象基类，处理通用窗口逻辑
    /// </summary>
    internal abstract class BaseDyeMachineUI : UIHandle
    {
        internal bool CanOpen;
        internal float sengs;
        internal float hoverSengs;

        internal virtual Texture2D UITex => (this is SpectrometerUI) ? DyeMachineAsset.SpectrometerUI : DyeMachineAsset.DyeVatUI;

        public abstract BaseDyeMachineSlot DyeSlot { get; }
        public abstract BaseDyeMachineSlot BeDyedItem { get; }
        public abstract BaseDyeMachineSlot ResultDyedItem { get; }

        public object TileEntity { get; set; }

        public override bool Active {
            get { return CanOpen || sengs > 0f; }
            set { CanOpen = value; }
        }

        public override void Update() {
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
            sengs = MathHelper.Clamp(sengs, 0, 1f);
            Size = new Vector2(280 * sengs);
            DrawPosition = new Vector2(Main.screenWidth / 2, Main.screenHeight / 12 * 9) - Size / 2;
            UIHitBox = DrawPosition.GetRectangle(Size);
            hoverInMainPage = UIHitBox.Intersects(MousePosition.GetRectangle(1)) && sengs > 0.8f;
            DrawPosition.X = MathHelper.Clamp(DrawPosition.X, 0, Main.screenWidth - UIHitBox.Width);
            DrawPosition.Y = MathHelper.Clamp(DrawPosition.Y, 0, Main.screenHeight - UIHitBox.Height);

            if (hoverInMainPage) {
                player.mouseInterface = true;
            }

            Vector2 offsetTopLeft = new Vector2(60, 60) * sengs;
            DyeSlot.DrawPosition = DrawPosition + offsetTopLeft;
            DyeSlot.Update();
            BeDyedItem.DrawPosition = DyeSlot.DrawPosition + new Vector2(0, 100) * sengs;
            BeDyedItem.Update();
            ResultDyedItem.DrawPosition = DyeSlot.DrawPosition + new Vector2(100, 50) * sengs;
            ResultDyedItem.Update();
        }

        public override void Draw(SpriteBatch spriteBatch) {
            if (hoverSengs > 0) {
                spriteBatch.Draw(UITex, UIHitBox.OffsetSize(6, 6), Color.Gold * hoverSengs);
            }
            spriteBatch.Draw(UITex, UIHitBox, Color.White);
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
        //引入父级UI的引用，用于解耦，替代单例调用
        public BaseDyeMachineUI ParentUI;
        internal Item Item = new();
        internal float sengs;
        internal float hoverSengs;
        internal float slotIndex;
        public virtual Texture2D SlotTex => (ParentUI is SpectrometerUI) ? DyeMachineAsset.SpectrometerSlot : DyeMachineAsset.DyeVatSlot;
        public abstract Texture2D SymbolTex { get; }
        public abstract Texture2D SymbolTexAlt { get; }
        public override LayersModeEnum LayersMode => LayersModeEnum.None;
        public override bool Active => true;
        public virtual bool CanCheckLeft(Item heldItem) => true;
        public virtual bool PreCheckLeft(Item heldItem) => true;
        public virtual void UpdateSlot() { }

        public override void Update() {
            if (sengs < 1f) {
                sengs += 0.1f;
            }

            Size = new Vector2(60);
            UIHitBox = DrawPosition.GetRectangle(Size * sengs);
            hoverInMainPage = UIHitBox.Intersects(MousePosition.GetRectangle(1)) && sengs > 0.8f;
            if (hoverInMainPage) {
                ItemFilterUI.Instance.hoverSlotIndex = slotIndex;
                if (hoverSengs < 1f) {
                    hoverSengs += 0.1f;
                }

                if (keyLeftPressState == KeyPressState.Pressed) {
                    Item heldItem = Main.mouseItem;
                    if (PreCheckLeft(heldItem)) {
                        if (heldItem.type != ItemID.None) {
                            if (CanCheckLeft(heldItem)) {
                                if (Item.type == ItemID.None) {
                                    SoundEngine.PlaySound(SoundID.Grab);
                                    Item = heldItem.Clone();
                                    Item.stack = 1;
                                    heldItem.stack--;
                                    if (heldItem.stack <= 0) {
                                        heldItem.TurnToAir();
                                    }
                                }
                                else {
                                    SoundEngine.PlaySound(SoundID.MenuTick);
                                }
                            }
                            else {
                                SoundEngine.PlaySound(SoundID.MenuTick);
                            }
                        }
                        else {
                            if (Item.type != ItemID.None) {
                                SoundEngine.PlaySound(SoundID.Grab);
                                Main.mouseItem = Item.Clone();
                                Item.TurnToAir();
                            }
                        }
                    }
                }
            }
            else {
                if (hoverSengs > 0f) {
                    hoverSengs -= 0.1f;
                }
            }
        }

        public override void Draw(SpriteBatch spriteBatch) {
            Color drawColor;
            if (hoverSengs > 0) {
                drawColor = Color.Gold with { A = 0 } * hoverSengs;
                spriteBatch.Draw(DyeMachineAsset.SoftGlow, DrawPosition + Size / 2, null
                    , drawColor, 0, DyeMachineAsset.SoftGlow.Size() / 2f, 1.62f, SpriteEffects.None, 0);
            }

            drawColor = Color.White;
            spriteBatch.Draw(SlotTex, DrawPosition, null, drawColor
                , 0, Vector2.Zero, 1f, SpriteEffects.None, 0);

            if (Item.type == ItemID.None) {
                drawColor = Color.White * (1f - hoverSengs);
                spriteBatch.Draw(SymbolTex, DrawPosition + Size / 2, null, drawColor
                , 0, SymbolTex.Size() / 2f, 1f, SpriteEffects.None, 0);
                drawColor = Color.White * hoverSengs;
                spriteBatch.Draw(SymbolTexAlt, DrawPosition + Size / 2, null, drawColor
                    , 0, SymbolTex.Size() / 2f, 1f, SpriteEffects.None, 0);
            }

            if (hoverInMainPage && Item.type > ItemID.None) {
                Main.HoverItem = Item.Clone();
                Main.hoverItemName = Item.Name;
            }

            VaultUtils.SafeLoadItem(Item);
            float mode = 0.6f + 0.4f * hoverSengs;
            Color itemColor = new Color(mode, mode, mode, 1f) * sengs;

            //预览效果和最终效果的绘制逻辑移动到具体的子类中
            DrawItemWithEffect(spriteBatch, itemColor);
        }

        //将物品绘制逻辑提取为虚方法，方便子类重写以添加特效
        protected virtual void DrawItemWithEffect(SpriteBatch spriteBatch, Color color) {
            if (Item.type > ItemID.None) {
                CWRItems.AddByDyeEffectByUI(Item, Item.CWR().DyeItemID);
            }
            VaultUtils.SimpleDrawItem(spriteBatch, Item.type, DrawPosition + UIHitBox.Size() / 2, 64, 1.2f + hoverSengs * 0.2f, 0f, color);
            if (Item.type > ItemID.None) {
                CWRItems.CloseByDyeEffectByUI();
            }
        }
    }

    /// <summary>
    /// 通用化的“待染色物品”槽，包含进度条逻辑
    /// </summary>
    internal class BeDyedItemSlot : BaseDyeMachineSlot
    {
        public float DyeProgress = 0f;

        public override Texture2D SymbolTex => DyeMachineAsset.BeDyeSymbol;

        public override Texture2D SymbolTexAlt => DyeMachineAsset.BeDyeSymbolAlt;

        protected override void DrawItemWithEffect(SpriteBatch spriteBatch, Color color) {
            //通过ParentUI访问染料槽，实现解耦
            int beDye = ParentUI.DyeSlot.Item.type;
            CWRItems.AddByDyeEffectByUI(Item, beDye);

            base.DrawItemWithEffect(spriteBatch, color);

            CWRItems.CloseByDyeEffectByUI();

            //绘制进度条
            if (DyeProgress > 0f && DyeProgress < 1f) {
                Rectangle hitbox = UIHitBox;
                int progressHeight = (int)(hitbox.Height * DyeProgress);
                Rectangle progressBar = new Rectangle(hitbox.X, hitbox.Y + hitbox.Height - progressHeight, hitbox.Width, progressHeight);
                spriteBatch.Draw(TextureAssets.MagicPixel.Value, progressBar, Color.Green * 0.5f);
            }
        }
    }

    /// <summary>
    /// 通用化的“染色结果”槽
    /// </summary>
    internal class ResultDyedItemSlot : BaseDyeMachineSlot
    {
        public override Texture2D SymbolTex => DyeMachineAsset.OutputSymbol;

        public override Texture2D SymbolTexAlt => DyeMachineAsset.OutputSymbolAlt;

        //重写预检查，不允许放入任何物品，只允许取出
        public override bool PreCheckLeft(Item heldItem) {
            if (heldItem.type == ItemID.None && Item.type != ItemID.None) {
                SoundEngine.PlaySound(SoundID.Grab);
                Main.mouseItem = Item.Clone();
                Item.TurnToAir();
            }
            return false;
        }

        protected override void DrawItemWithEffect(SpriteBatch spriteBatch, Color color) {
            if (Item.type > ItemID.None) {
                //从物品自身获取应用的染料ID
                CWRItems.AddByDyeEffectByUI(Item, Item.CWR().DyeItemID);

                base.DrawItemWithEffect(spriteBatch, color);

                CWRItems.CloseByDyeEffectByUI();
            }
            else {
                //如果没有物品，就调用基类方法绘制空槽
                base.DrawItemWithEffect(spriteBatch, color);
            }
        }
    }

    #endregion

    #region 染缸 UI 实现

    internal class DyeVatUI : BaseDyeMachineUI
    {
        internal DyeVatTP DyeVatTP;

        //实例化插槽
        private readonly DyeVatDyeSlot _dyeSlot = new();
        private readonly BeDyedItemSlot _beDyedItem = new();
        private readonly ResultDyedItemSlot _resultDyedItem = new();

        //实现基类的抽象属性
        public override BaseDyeMachineSlot DyeSlot => _dyeSlot;
        public override BaseDyeMachineSlot BeDyedItem => _beDyedItem;
        public override BaseDyeMachineSlot ResultDyedItem => _resultDyedItem;

        internal static DyeVatUI Instance => UIHandleLoader.GetUIHandleOfType<DyeVatUI>();

        //构造函数中进行依赖注入
        public DyeVatUI() {
            _dyeSlot.ParentUI = this;
            _beDyedItem.ParentUI = this;
            _resultDyedItem.ParentUI = this;
        }
    }

    internal class DyeVatDyeSlot : BaseDyeMachineSlot
    {
        public override Texture2D SymbolTex => DyeMachineAsset.DyeSymbol;

        public override Texture2D SymbolTexAlt => DyeMachineAsset.DyeSymbolAlt;

        public override bool CanCheckLeft(Item heldItem) => heldItem.dye > 0 || heldItem.IsWaterBucket();

        public override void UpdateSlot() {
            //通过ParentUI安全地获取其他槽位，并进行类型转换
            if (ParentUI.BeDyedItem is not BeDyedItemSlot dyedItemSlot) {
                return;
            }
            BaseDyeMachineSlot resultDyedItemSlot = ParentUI.ResultDyedItem;

            if (Item.type > ItemID.None && dyedItemSlot.Item.type > ItemID.None && resultDyedItemSlot.Item.type == ItemID.None) {
                dyedItemSlot.DyeProgress += 0.0006f;

                if (dyedItemSlot.DyeProgress >= 1f) {
                    dyedItemSlot.DyeProgress = 0f;
                    resultDyedItemSlot.Item = dyedItemSlot.Item.Clone();

                    if (!Item.IsWaterBucket()) {
                        resultDyedItemSlot.Item.CWR().DyeItemID = Item.type;
                        Item.stack--;
                        if (Item.stack <= 0) {
                            Item.TurnToAir();
                        }
                    }
                    else {
                        resultDyedItemSlot.Item.CWR().DyeItemID = 0;
                        if (Item.consumable) {
                            Item.TurnToAir();
                        }
                    }
                    dyedItemSlot.Item.TurnToAir();
                    SoundEngine.PlaySound(SoundID.Item37);
                }
            }
            else {
                if (dyedItemSlot.DyeProgress > 0f) {
                    dyedItemSlot.DyeProgress = 0f;
                }
            }
        }
    }

    #endregion

    #region 光谱仪 UI 实现

    internal class SpectrometerUI : BaseDyeMachineUI
    {
        internal SpectrometerTP SpectrometerTP;

        //实例化插槽
        private readonly SpectrometerDyeSlot _dyeSlot = new();
        private readonly BeDyedItemSlot _beDyedItem = new();
        private readonly ResultDyedItemSlot _resultDyedItem = new();

        //实现基类的抽象属性
        public override BaseDyeMachineSlot DyeSlot => _dyeSlot;
        public override BaseDyeMachineSlot BeDyedItem => _beDyedItem;
        public override BaseDyeMachineSlot ResultDyedItem => _resultDyedItem;

        internal static SpectrometerUI Instance => UIHandleLoader.GetUIHandleOfType<SpectrometerUI>();

        //构造函数中进行依赖注入
        public SpectrometerUI() {
            _dyeSlot.ParentUI = this;
            _beDyedItem.ParentUI = this;
            _resultDyedItem.ParentUI = this;
        }
    }

    internal class SpectrometerDyeSlot : BaseDyeMachineSlot
    {
        public override Texture2D SymbolTex => DyeMachineAsset.DyeSymbol;

        public override Texture2D SymbolTexAlt => DyeMachineAsset.DyeSymbolAlt;

        public override bool CanCheckLeft(Item heldItem) => heldItem.dye > 0 || heldItem.IsWaterBucket();

        public override void UpdateSlot() {
            if (ParentUI is not SpectrometerUI spectrometerUI || spectrometerUI.BeDyedItem is not BeDyedItemSlot dyedItemSlot) {
                return;
            }
            BaseDyeMachineSlot resultDyedItemSlot = spectrometerUI.ResultDyedItem;

            if (Item.type > ItemID.None && dyedItemSlot.Item.type > ItemID.None && resultDyedItemSlot.Item.type == ItemID.None) {
                var tp = spectrometerUI.SpectrometerTP;
                if (tp != null && tp.MachineData.UEvalue > 1f) {
                    tp.MachineData.UEvalue--;
                    dyedItemSlot.DyeProgress += 0.002f;
                }

                if (dyedItemSlot.DyeProgress >= 1f) {
                    dyedItemSlot.DyeProgress = 0f;
                    resultDyedItemSlot.Item = dyedItemSlot.Item.Clone();

                    if (!Item.IsWaterBucket()) {
                        resultDyedItemSlot.Item.CWR().DyeItemID = Item.type;
                    }
                    else {
                        resultDyedItemSlot.Item.CWR().DyeItemID = 0;
                        if (Item.consumable) {
                            Item.TurnToAir();
                        }
                    }
                    dyedItemSlot.Item.TurnToAir();
                    SoundEngine.PlaySound(SoundID.Item37);
                }
            }
            else {
                if (dyedItemSlot.DyeProgress > 0f) {
                    dyedItemSlot.DyeProgress = 0f;
                }
            }
        }
    }

    #endregion
}