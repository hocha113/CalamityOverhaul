using CalamityOverhaul.Content.Industrials;
using CalamityOverhaul.Content.Industrials.ElectricPowers;
using CalamityOverhaul.Content.Industrials.Modifys;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.UIs
{
    #region 资源与常量
    [VaultLoaden(CWRConstant.UI + "DyeMachineUI")]
    internal static class DyeMachineAsset
    {
        [VaultLoaden(CWRConstant.Masking)]
        public static Texture2D SoftGlow = null;
        public static Texture2D BeDyeSymbol = null;
        public static Texture2D BeDyeSymbolAlt = null;
        public static Texture2D DyeSymbol = null;
        public static Texture2D DyeSymbolAlt = null;
        public static Texture2D OutputSymbol = null;
        public static Texture2D OutputSymbolAlt = null;
        public static Texture2D DyeDroplets = null;
        public static Texture2D OutputSymbolArrows = null;
        public static Texture2D DyeVatSlot = null;
        public static Texture2D DyeVatUI = null;
        public static Texture2D SpectrometerSlot = null;
        public static Texture2D SpectrometerUI = null;
    }

    /// <summary>
    /// 存储UI相关的常量，便于统一管理和调整
    /// </summary>
    internal static class DyeMachineConstants
    {
        //动画
        public const float AnimationSpeed = 0.18f; //所有UI动画的缓动速度
        public const float HoverScaleMultiplier = 1.1f; //鼠标悬停时物品槽的放大倍数

        //尺寸
        public static readonly Vector2 MainUISize = new(280, 280);
        public static readonly Vector2 SlotSize = new(60, 60);

        //染色速率
        public const float DyeVatRate = 0.0012f; //染缸染色速率
        public const float SpectrometerRate = 0.004f; //光谱仪染色速率
    }
    #endregion

    #region 基类与通用组件

    /// <summary>
    /// 染色机器UI的抽象基类
    /// </summary>
    internal abstract class BaseDyeMachineUI : UIHandle
    {
        internal bool CanOpen;
        internal float sengs;
        internal virtual Texture2D UITex => (this is SpectrometerUI) ? DyeMachineAsset.SpectrometerUI : DyeMachineAsset.DyeVatUI;

        public abstract BaseDyeMachineSlot DyeSlot { get; }
        public abstract BaseDyeMachineSlot BeDyedItem { get; }
        public abstract BaseDyeMachineSlot ResultDyedItem { get; }
        public BaseDyeTP DyeTP { get; set; }

        public override bool Active {
            get { return CanOpen || sengs > 0.01f; } //防止sengs过小时UI仍在活动
            set { CanOpen = value; }
        }

        public override void Update() {
            //使用Lerp进行平滑过渡，优化动画手感
            float targetSengs = CanOpen ? 1f : 0f;
            sengs = MathHelper.Lerp(sengs, targetSengs, DyeMachineConstants.AnimationSpeed);
            if (Math.Abs(sengs - targetSengs) < 0.01f) {
                sengs = targetSengs;
            }

            Size = DyeMachineConstants.MainUISize * sengs;
            DrawPosition = new Vector2(Main.screenWidth / 2, Main.screenHeight / 12 * 9) - Size / 2;
            UIHitBox = DrawPosition.GetRectangle(Size);
            hoverInMainPage = UIHitBox.Intersects(MousePosition.GetRectangle(1)) && sengs > 0.8f;
            DrawPosition.X = MathHelper.Clamp(DrawPosition.X, 0, Main.screenWidth - UIHitBox.Width);
            DrawPosition.Y = MathHelper.Clamp(DrawPosition.Y, 0, Main.screenHeight - UIHitBox.Height);

            if (hoverInMainPage) {
                player.mouseInterface = true;
            }

            //更新插槽位置和状态
            Vector2 center = DrawPosition + Size / 2 - DyeMachineConstants.SlotSize / 2 * sengs;
            Vector2 SengsOffset = DyeMachineConstants.SlotSize / 2 * (1f - sengs) * -1f;//这个变量用于让收缩时对齐中心
            DyeSlot.DrawPosition = center + new Vector2(-60, -60) * sengs + SengsOffset;
            DyeSlot.Update();
            BeDyedItem.DrawPosition = center + new Vector2(-60, 60) * sengs + SengsOffset;
            BeDyedItem.Update();
            ResultDyedItem.DrawPosition = center + new Vector2(60, 0) * sengs + SengsOffset;
            ResultDyedItem.Update();
        }

        public override void Draw(SpriteBatch spriteBatch) {
            if (sengs <= 0) return; //完全关闭后不绘制

            spriteBatch.Draw(UITex, UIHitBox, Color.White * sengs);

            float activationThreshold = 0.8f;
            if (sengs >= activationThreshold) {
                float normalizedProgress = (sengs - activationThreshold) / (1f - activationThreshold);
                Color drawColor = Color.White * normalizedProgress * 0.6f;
                drawColor.R /= 2;
                spriteBatch.Draw(DyeMachineAsset.DyeDroplets, DrawPosition + new Vector2(66, 120)
                    , null, drawColor, 0, Vector2.Zero, normalizedProgress, SpriteEffects.None, 0);
                spriteBatch.Draw(DyeMachineAsset.OutputSymbolArrows, DrawPosition + new Vector2(120, 174)
                    , null, drawColor, 0, Vector2.Zero, normalizedProgress, SpriteEffects.None, 0);
            }

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
        public BaseDyeMachineUI ParentUI;
        internal Item Item = new();
        internal float sengs;
        internal float hoverSengs;
        internal float scale; //用于实现悬停放大动画
        public virtual Texture2D SlotTex => (ParentUI is SpectrometerUI) ? DyeMachineAsset.SpectrometerSlot : DyeMachineAsset.DyeVatSlot;
        public abstract Texture2D SymbolTex { get; }
        public abstract Texture2D SymbolTexAlt { get; }

        public override LayersModeEnum LayersMode => LayersModeEnum.None;
        public override bool Active => true;
        public virtual bool CanCheckLeft(Item heldItem) => true;
        public virtual bool PreCheckLeft(Item heldItem) => true;
        public virtual void UpdateSlot() { }

        public override void Update() {
            //更新插槽出现动画
            sengs = ParentUI.sengs;
            Size = DyeMachineConstants.SlotSize;
            UIHitBox = DrawPosition.GetRectangle(Size * sengs * scale); //碰撞箱也跟随缩放

            bool lastHover = hoverInMainPage;
            hoverInMainPage = UIHitBox.Intersects(MousePosition.GetRectangle(1)) && sengs > 0.8f;

            if (hoverInMainPage && !lastHover) {
                SoundEngine.PlaySound(SoundID.MenuTick); //鼠标首次悬停时播放音效
            }

            //缓动动画更新
            float targetHoverSengs = hoverInMainPage ? 1f : 0f;
            float targetScale = hoverInMainPage ? DyeMachineConstants.HoverScaleMultiplier : 1f;
            hoverSengs = MathHelper.Lerp(hoverSengs, targetHoverSengs, DyeMachineConstants.AnimationSpeed);
            scale = MathHelper.Lerp(scale, targetScale, DyeMachineConstants.AnimationSpeed);

            //处理物品交互
            if (hoverInMainPage && keyLeftPressState == KeyPressState.Pressed) {
                HandleItemSlotting();
            }
        }

        /// <summary>
        /// 处理复杂的物品放入、取出和交换逻辑
        /// </summary>
        private void HandleItemSlotting() {
            if (PreCheckLeft(Main.mouseItem)) {
                //情况1: 玩家手上有物品
                if (Main.mouseItem.type != ItemID.None) {
                    if (CanCheckLeft(Main.mouseItem)) {
                        SoundEngine.PlaySound(SoundID.Grab);
                        //与槽内物品交换
                        Utils.Swap(ref Item, ref Main.mouseItem);
                    }
                    else {
                        SoundEngine.PlaySound(SoundID.MenuClose); //用更明确的失败音效
                    }
                }
                //情况2: 玩家手空，槽内有物品
                else if (Item.type != ItemID.None) {
                    SoundEngine.PlaySound(SoundID.Grab);
                    //直接取出
                    Main.mouseItem = Item.Clone();
                    Item.TurnToAir();
                }
            }

            PostHandleItemSlotting();
        }

        public virtual void PostHandleItemSlotting() {

        }

        public override void Draw(SpriteBatch spriteBatch) {
            if (sengs <= 0) return;

            Vector2 origin = Size / 2f;
            Vector2 drawPos = DrawPosition + origin;

            DrawHoverGlow(spriteBatch, drawPos, origin);
            DrawSlotBackground(spriteBatch, drawPos, origin);

            if (Item.type == ItemID.None) {
                DrawEmptySymbol(spriteBatch, drawPos, origin);
            }

            if (hoverInMainPage && Item.type > ItemID.None) {
                Main.HoverItem = Item.Clone();
                Main.hoverItemName = Item.Name;
            }

            VaultUtils.SafeLoadItem(Item);
            float mode = 0.6f + 0.4f * hoverSengs;
            Color itemColor = new Color(mode, mode, mode, 1f) * sengs;

            DrawItemWithEffect(spriteBatch, itemColor);
        }

        private void DrawHoverGlow(SpriteBatch spriteBatch, Vector2 drawPos, Vector2 origin) {
            if (hoverSengs > 0) {
                Color glowColor = Color.Gold with { A = 0 } * hoverSengs;
                spriteBatch.Draw(DyeMachineAsset.SoftGlow, drawPos, null, glowColor, 0, DyeMachineAsset.SoftGlow.Size() / 2f, scale * 1.62f, SpriteEffects.None, 0);
            }
        }

        private void DrawSlotBackground(SpriteBatch spriteBatch, Vector2 drawPos, Vector2 origin) {
            spriteBatch.Draw(SlotTex, drawPos, null, Color.White * sengs, 0, origin, scale, SpriteEffects.None, 0);
        }

        private void DrawEmptySymbol(SpriteBatch spriteBatch, Vector2 drawPos, Vector2 origin) {
            float symbolScale = scale * (1 + (hoverSengs * 0.1f)); //符号也跟随轻微放大
            Vector2 symbolOrigin = SymbolTex.Size() / 2f;
            //淡入淡出效果
            spriteBatch.Draw(SymbolTex, drawPos, null, Color.White * (1f - hoverSengs) * sengs, 0, symbolOrigin, symbolScale, SpriteEffects.None, 0);
            spriteBatch.Draw(SymbolTexAlt, drawPos, null, Color.White * hoverSengs * sengs, 0, symbolOrigin, symbolScale, SpriteEffects.None, 0);
        }

        protected void DrawItemStack(SpriteBatch spriteBatch) {
            if (Item.stack <= 1) {
                return;
            }
            string stack = Item.stack.ToString();
            Vector2 stackSize = FontAssets.MouseText.Value.MeasureString(stack);
            Vector2 drawPos = DrawPosition + Size / 2 + new Vector2(0, 24 + 6 * hoverSengs);
            Utils.DrawBorderStringFourWay(spriteBatch, FontAssets.MouseText.Value, stack
                , drawPos.X, drawPos.Y, Color.White, Color.Black, stackSize / 2, 0.9f + 0.1f * hoverSengs);
        }

        protected virtual void DrawItemWithEffect(SpriteBatch spriteBatch, Color color) {
            if (Item.Alives()) {
                Item.BeginDyeEffectForUI(Item.CWR().DyeItemID);
                VaultUtils.SimpleDrawItem(spriteBatch, Item.type, DrawPosition + Size / 2, 64, (1.2f + hoverSengs * 0.2f) * sengs, 0f, color);
                Item.EndDyeEffectForUI();
                DrawItemStack(spriteBatch);
            }
        }
    }

    #endregion

    #region 模板方法模式应用

    /// <summary>
    /// 通用化的“待染色物品”槽，包含进度条逻辑
    /// </summary>
    internal class BeDyedItemSlot : BaseDyeMachineSlot
    {
        public float DyeProgress = 0f;
        public override Texture2D SymbolTex => DyeMachineAsset.BeDyeSymbol;
        public override Texture2D SymbolTexAlt => DyeMachineAsset.BeDyeSymbolAlt;

        protected override void DrawItemWithEffect(SpriteBatch spriteBatch, Color color) {
            base.DrawItemWithEffect(spriteBatch, color);

            if (DyeProgress > 0f && DyeProgress < 1f) {
                Rectangle hitbox = UIHitBox;
                int progressHeight = (int)(hitbox.Height * DyeProgress);
                Rectangle progressBar = new Rectangle(hitbox.X, hitbox.Y + hitbox.Height - progressHeight, hitbox.Width, progressHeight);
                spriteBatch.Draw(TextureAssets.MagicPixel.Value, progressBar, Color.Green * 0.5f);
            }
        }

        public sealed override void PostHandleItemSlotting() {
            ParentUI.DyeTP.BeDyedItem = Item;//操作结果关联TP实体物品槽
            ParentUI.DyeTP.SendData();
        }
    }

    /// <summary>
    /// 通用化的“染色结果”槽
    /// </summary>
    internal class ResultDyedItemSlot : BaseDyeMachineSlot
    {
        public override Texture2D SymbolTex => DyeMachineAsset.OutputSymbol;
        public override Texture2D SymbolTexAlt => DyeMachineAsset.OutputSymbolAlt;

        public override bool PreCheckLeft(Item heldItem) => heldItem.type == ItemID.None;

        public sealed override void PostHandleItemSlotting() {
            ParentUI.DyeTP.ResultDyedItem = Item;//操作结果关联TP实体物品槽
            ParentUI.DyeTP.SendData();
        }
    }

    /// <summary>
    /// 染料槽的基类，使用模板方法模式定义染色流程
    /// </summary>
    internal abstract class BaseDyeSlot : BaseDyeMachineSlot
    {
        public override Texture2D SymbolTex => DyeMachineAsset.DyeSymbol;
        public override Texture2D SymbolTexAlt => DyeMachineAsset.DyeSymbolAlt;
        public override bool CanCheckLeft(Item heldItem) => heldItem.dye > 0 || heldItem.IsWaterBucket();

        /// <summary>
        /// 模板方法：定义了染色的标准流程骨架
        /// </summary>
        public sealed override void UpdateSlot() {
            if (CanStartDyeing(out BeDyedItemSlot dyedItemSlot, out BaseDyeMachineSlot resultSlot)) {
                if (ConsumeResources()) {
                    UpdateProgress(dyedItemSlot);
                    if (dyedItemSlot.DyeProgress >= 1f) {
                        FinishDyeing(dyedItemSlot, resultSlot);
                    }
                }
            }
            else if (ParentUI.BeDyedItem is BeDyedItemSlot itemSlot && itemSlot.DyeProgress > 0) {
                //如果条件不满足，平滑地回退进度
                itemSlot.DyeProgress = MathHelper.Lerp(itemSlot.DyeProgress, 0, 0.05f);
            }
        }

        public sealed override void PostHandleItemSlotting() {
            ParentUI.DyeTP.DyeSlotItem = Item;//操作结果关联TP实体物品槽
            ParentUI.DyeTP.SendData();
        }

        public virtual bool CanStartDyeing(out BeDyedItemSlot dyedItemSlot, out BaseDyeMachineSlot resultSlot) {
            dyedItemSlot = ParentUI.BeDyedItem as BeDyedItemSlot;
            resultSlot = ParentUI.ResultDyedItem;
            return dyedItemSlot != null && resultSlot != null &&
                   Item.type > ItemID.None &&
                   dyedItemSlot.Item.type > ItemID.None &&
                   resultSlot.Item.type == ItemID.None;
        }

        public virtual void FinishDyeing(BeDyedItemSlot dyedItemSlot, BaseDyeMachineSlot resultSlot) {
            dyedItemSlot.DyeProgress = 0f;
            resultSlot.Item = dyedItemSlot.Item.Clone();
            if (!Item.IsWaterBucket()) {
                resultSlot.Item.CWR().DyeItemID = Item.type;
            }
            else {
                resultSlot.Item.CWR().DyeItemID = 0; //水桶用于洗去染色
                if (Item.consumable && --Item.stack <= 0) {
                    Item.TurnToAir();
                }
            }
            dyedItemSlot.Item.TurnToAir();
            SoundEngine.PlaySound(SoundID.Item37);

            //操作结果关联TP实体物品槽
            ParentUI.DyeTP.DyeSlotItem = Item;
            ParentUI.DyeTP.BeDyedItem = dyedItemSlot.Item;
            ParentUI.DyeTP.ResultDyedItem = resultSlot.Item;
            ParentUI.DyeTP.SendData();
        }

        //子类必须实现的差异化步骤
        protected abstract bool ConsumeResources();
        protected abstract void UpdateProgress(BeDyedItemSlot dyedItemSlot);
    }

    #endregion

    #region 具体UI实现

    internal class DyeVatUI : BaseDyeMachineUI
    {
        private readonly DyeVatDyeSlot _dyeSlot = new();
        private readonly BeDyedItemSlot _beDyedItem = new();
        private readonly ResultDyedItemSlot _resultDyedItem = new();

        public override BaseDyeMachineSlot DyeSlot => _dyeSlot;
        public override BaseDyeMachineSlot BeDyedItem => _beDyedItem;
        public override BaseDyeMachineSlot ResultDyedItem => _resultDyedItem;
        internal static DyeVatUI Instance => UIHandleLoader.GetUIHandleOfType<DyeVatUI>();

        public DyeVatUI() {
            _dyeSlot.ParentUI = this;
            _beDyedItem.ParentUI = this;
            _resultDyedItem.ParentUI = this;
        }
    }

    internal class DyeVatDyeSlot : BaseDyeSlot
    {
        protected override bool ConsumeResources() => true;

        public override void FinishDyeing(BeDyedItemSlot dyedItemSlot, BaseDyeMachineSlot resultSlot) {
            base.FinishDyeing(dyedItemSlot, resultSlot);
            //染缸染色会消耗染料
            if (!Item.IsWaterBucket()) {
                Item.stack--;
                if (Item.stack <= 0) {
                    Item.TurnToAir();
                }
            }
        }

        protected override void UpdateProgress(BeDyedItemSlot dyedItemSlot) {
            dyedItemSlot.DyeProgress += DyeMachineConstants.DyeVatRate;
        }
    }

    internal class SpectrometerUI : BaseDyeMachineUI
    {
        private readonly SpectrometerDyeSlot _dyeSlot = new();
        private readonly BeDyedItemSlot _beDyedItem = new();
        private readonly ResultDyedItemSlot _resultDyedItem = new();

        public override BaseDyeMachineSlot DyeSlot => _dyeSlot;
        public override BaseDyeMachineSlot BeDyedItem => _beDyedItem;
        public override BaseDyeMachineSlot ResultDyedItem => _resultDyedItem;
        internal static SpectrometerUI Instance => UIHandleLoader.GetUIHandleOfType<SpectrometerUI>();

        public SpectrometerUI() {
            _dyeSlot.ParentUI = this;
            _beDyedItem.ParentUI = this;
            _resultDyedItem.ParentUI = this;
        }
    }

    internal class SpectrometerDyeSlot : BaseDyeSlot
    {
        //实现模板方法的具体步骤
        protected override bool ConsumeResources() {
            if (ParentUI is SpectrometerUI { DyeTP: SpectrometerTP tp } && tp.MachineData.UEvalue > 1f) {
                tp.MachineData.UEvalue--;
                tp.workTime = 10;
                return true; //电力充足
            }
            return false; //电力不足
        }

        protected override void UpdateProgress(BeDyedItemSlot dyedItemSlot) {
            dyedItemSlot.DyeProgress += DyeMachineConstants.SpectrometerRate;
        }
    }

    #endregion
}