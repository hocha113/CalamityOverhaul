using CalamityOverhaul.Content.ADV.UIEffect;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Common
{
    /// <summary>
    /// 配方展示UI基类
    /// </summary>
    internal abstract class BaseRecipeDisplayUI : UIHandle, ILocalizedModType
    {
        public virtual string LocalizationCategory => "UI";

        public override bool Active => isShowing || fadeProgress > 0f;
        public override float RenderPriority => 0.9f;

        //UI状态(改为实例字段)
        protected bool isShowing = false;
        protected float fadeProgress = 0f;
        protected float expandProgress = 0f;

        //动画参数
        protected const float FadeSpeed = 0.08f;
        protected const float ExpandSpeed = 0.1f;

        //UI尺寸
        protected virtual float UIWidth => 600f;
        protected virtual float UIHeight => 400f;

        //科技效果参数
        protected float techPulseTimer = 0f;
        protected float scanLineProgress = 0f;
        protected float hologramFlicker = 0f;

        //粒子系统
        protected readonly List<TechPRT> techParticles = new();
        protected int particleSpawnTimer = 0;

        //物品悬停检测
        protected readonly List<ItemHoverArea> itemHoverAreas = new();
        protected ItemHoverArea currentHoverItem = null;

        //本地化文本(保持静态,因为所有实例共享本地化文本是合理的)
        protected static LocalizedText UITitle;
        protected static LocalizedText MaterialsRequired;
        protected static LocalizedText CloseHint;
        protected static LocalizedText CraftingStation;

        //配方数据缓存
        protected Recipe cachedRecipe = null;

        /// <summary>
        /// 获取要显示的配方
        /// </summary>
        protected abstract Recipe GetDisplayRecipe();

        /// <summary>
        /// 获取UI标题文本
        /// </summary>
        protected abstract string GetUITitle();

        public override void LogicUpdate() {
            if (!isShowing && fadeProgress <= 0f) {
                return;
            }

            //更新动画
            UpdateAnimations();

            //更新粒子
            UpdateParticles();
        }

        public override void Update() {
            if (!isShowing && fadeProgress <= 0f) {
                return;
            }

            //计算UI位置
            DrawPosition = new Vector2(
                (Main.screenWidth - UIWidth * expandProgress) / 2,
                (Main.screenHeight - UIHeight * expandProgress)
            );

            //更新碰撞盒
            UIHitBox = new Rectangle(
                (int)DrawPosition.X,
                (int)DrawPosition.Y,
                (int)(UIWidth * expandProgress),
                (int)(UIHeight * expandProgress)
            );

            hoverInMainPage = UIHitBox.Intersects(MouseHitBox);

            //处理关闭按钮点击
            if (hoverInMainPage && isShowing) {
                player.mouseInterface = true;

                Rectangle closeButtonRect = GetCloseButtonRect();

                if (closeButtonRect.Intersects(MouseHitBox) && keyLeftPressState == KeyPressState.Pressed) {
                    Hide();
                }
            }

            //更新物品悬停检测
            UpdateItemHover();
        }

        public override void Draw(SpriteBatch spriteBatch) {
            if (fadeProgress <= 0.01f) {
                return;
            }

            //清空物品悬停区域列表
            itemHoverAreas.Clear();

            //绘制背景和边框
            DrawBackground(spriteBatch);

            //绘制标题
            DrawTitle(spriteBatch);

            //绘制配方内容
            DrawRecipeContent(spriteBatch);

            //绘制关闭按钮
            DrawCloseButton(spriteBatch);

            //绘制科技粒子
            DrawTechParticles(spriteBatch);
        }

        #region 动画更新

        protected virtual void UpdateAnimations() {
            //淡入淡出
            if (isShowing) {
                if (fadeProgress < 1f) {
                    fadeProgress = Math.Min(fadeProgress + FadeSpeed, 1f);
                }
                if (expandProgress < 1f) {
                    expandProgress = Math.Min(expandProgress + ExpandSpeed, 1f);
                }
            }
            else {
                if (fadeProgress > 0f) {
                    fadeProgress = Math.Max(fadeProgress - FadeSpeed, 0f);
                }
                if (expandProgress > 0f) {
                    expandProgress = Math.Max(expandProgress - ExpandSpeed, 0f);
                }
            }

            //科技效果计时器
            techPulseTimer += 0.04f;
            scanLineProgress += 0.03f;
            hologramFlicker += 0.06f;

            if (techPulseTimer > MathHelper.TwoPi) techPulseTimer -= MathHelper.TwoPi;
            if (scanLineProgress > 1f) scanLineProgress -= 1f;
            if (hologramFlicker > MathHelper.TwoPi) hologramFlicker -= MathHelper.TwoPi;
        }

        protected virtual void UpdateParticles() {
            if (expandProgress < 0.5f) {
                return;
            }

            //生成粒子
            particleSpawnTimer++;
            if (particleSpawnTimer >= 3 && techParticles.Count < 30) {
                particleSpawnTimer = 0;
                SpawnTechParticle();
            }

            //更新现有粒子
            for (int i = techParticles.Count - 1; i >= 0; i--) {
                if (techParticles[i].Update()) {
                    techParticles.RemoveAt(i);
                }
            }
        }

        protected virtual void SpawnTechParticle() {
            Vector2 spawnPos = new Vector2(
                DrawPosition.X + Main.rand.NextFloat(10, UIWidth * expandProgress - 10),
                DrawPosition.Y + Main.rand.NextFloat(10, UIHeight * expandProgress - 10)
            );

            techParticles.Add(new TechPRT(spawnPos));
        }

        protected virtual void UpdateItemHover() {
            currentHoverItem = null;

            //检测鼠标是否悬停在任何物品区域上
            foreach (var hoverArea in itemHoverAreas) {
                if (hoverArea.Bounds.Intersects(MouseHitBox)) {
                    currentHoverItem = hoverArea;
                    break;
                }
            }

            //设置Main.HoverItem以显示物品提示
            if (currentHoverItem != null && currentHoverItem.Item.type != ItemID.None) {
                Main.HoverItem = currentHoverItem.Item.Clone();
                Main.hoverItemName = currentHoverItem.Item.Name;
            }
        }

        #endregion

        #region 绘制方法

        protected virtual void DrawBackground(SpriteBatch sb) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            if (pixel == null) return;

            float alpha = fadeProgress * 0.95f;
            float flicker = (float)Math.Sin(hologramFlicker * 1.2f) * 0.08f + 0.92f;
            alpha *= flicker;

            Color techColor = new Color(80, 200, 255);
            Color bgColor = new Color(8, 15, 25);

            //阴影
            Rectangle shadowRect = UIHitBox;
            shadowRect.Offset(6, 6);
            sb.Draw(pixel, shadowRect, new Rectangle(0, 0, 1, 1), new Color(0, 0, 0) * (alpha * 0.6f));

            //主背景
            sb.Draw(pixel, UIHitBox, new Rectangle(0, 0, 1, 1), bgColor * alpha);

            //渐变叠加
            for (int i = 0; i < 10; i++) {
                float t = i / 10f;
                Rectangle gradRect = new Rectangle(
                    UIHitBox.X,
                    UIHitBox.Y + (int)(UIHitBox.Height * t),
                    UIHitBox.Width,
                    UIHitBox.Height / 10
                );

                float gradientAlpha = (float)Math.Sin(techPulseTimer + t * 2f) * 0.15f + 0.15f;
                sb.Draw(pixel, gradRect, new Color(80, 120, 160) * (alpha * gradientAlpha));
            }

            //边框
            DrawFrame(sb, UIHitBox, alpha, techColor);

            //扫描线
            DrawScanLines(sb, UIHitBox, alpha);
        }

        protected static void DrawFrame(SpriteBatch sb, Rectangle rect, float alpha, Color color) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            if (pixel == null) return;

            Color frameColor = color * (alpha * 0.9f);

            //外边框
            sb.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, 3), frameColor);
            sb.Draw(pixel, new Rectangle(rect.X, rect.Bottom - 3, rect.Width, 3), frameColor * 0.7f);
            sb.Draw(pixel, new Rectangle(rect.X, rect.Y, 3, rect.Height), frameColor * 0.85f);
            sb.Draw(pixel, new Rectangle(rect.Right - 3, rect.Y, 3, rect.Height), frameColor * 0.85f);

            //内发光
            Rectangle inner = rect;
            inner.Inflate(-6, -6);
            Color glowColor = color * (alpha * 0.2f);
            sb.Draw(pixel, new Rectangle(inner.X, inner.Y, inner.Width, 1), glowColor);
            sb.Draw(pixel, new Rectangle(inner.X, inner.Bottom - 1, inner.Width, 1), glowColor);
            sb.Draw(pixel, new Rectangle(inner.X, inner.Y, 1, inner.Height), glowColor);
            sb.Draw(pixel, new Rectangle(inner.Right - 1, inner.Y, 1, inner.Height), glowColor);

            //角落装饰
            DrawCornerTech(sb, new Vector2(rect.X + 10, rect.Y + 10), color * (alpha * 0.9f), -MathHelper.PiOver2);
            DrawCornerTech(sb, new Vector2(rect.Right - 10, rect.Y + 10), color * (alpha * 0.9f), 0f);
            DrawCornerTech(sb, new Vector2(rect.X + 10, rect.Bottom - 10), color * (alpha * 0.9f), MathHelper.Pi);
            DrawCornerTech(sb, new Vector2(rect.Right - 10, rect.Bottom - 10), color * (alpha * 0.9f), MathHelper.PiOver2);
        }

        protected static void DrawCornerTech(SpriteBatch sb, Vector2 pos, Color color, float rotation) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            if (pixel == null) return;

            float size = 10f;
            sb.Draw(pixel, pos, new Rectangle(0, 0, 1, 1), color, rotation,
                new Vector2(0.5f), new Vector2(size, size * 0.2f), SpriteEffects.None, 0f);
            sb.Draw(pixel, pos, new Rectangle(0, 0, 1, 1), color * 0.7f, rotation + MathHelper.PiOver2,
                new Vector2(0.5f), new Vector2(size, size * 0.2f), SpriteEffects.None, 0f);
        }

        protected virtual void DrawScanLines(SpriteBatch sb, Rectangle rect, float alpha) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            if (pixel == null) return;

            //主扫描线
            float scanY = rect.Y + scanLineProgress * rect.Height;
            for (int layer = 0; layer < 3; layer++) {
                float offset = layer * 1.5f;
                Color scanColor = new Color(80, 220, 255) * (alpha * (0.3f - layer * 0.08f));
                sb.Draw(pixel, new Vector2(rect.X, scanY + offset), new Rectangle(0, 0, 1, 1),
                    scanColor, 0f, Vector2.Zero, new Vector2(rect.Width, 2f + layer), SpriteEffects.None, 0f);
            }
        }

        protected virtual void DrawTitle(SpriteBatch sb) {
            DynamicSpriteFont font = FontAssets.MouseText.Value;
            string title = GetUITitle();
            Vector2 titleSize = font.MeasureString(title);
            Vector2 titlePos = new Vector2(
                DrawPosition.X + (UIWidth * expandProgress - titleSize.X) / 2,
                DrawPosition.Y + 20
            );

            float alpha = fadeProgress * 0.9f;
            Color titleColor = new Color(100, 220, 255);

            //标题发光
            for (int i = 0; i < 4; i++) {
                float angle = MathHelper.TwoPi * i / 4f;
                Vector2 offset = angle.ToRotationVector2() * 1.5f;
                Utils.DrawBorderString(sb, title, titlePos + offset, titleColor * (alpha * 0.5f), 0.9f);
            }

            Utils.DrawBorderString(sb, title, titlePos, Color.White * alpha, 0.9f);

            //标题下划线
            Texture2D pixel = VaultAsset.placeholder2.Value;
            if (pixel != null) {
                sb.Draw(pixel, new Rectangle(
                    (int)(DrawPosition.X + 50),
                    (int)(DrawPosition.Y + 50),
                    (int)(UIWidth * expandProgress - 100),
                    2
                ), new Color(80, 200, 255) * (alpha * 0.6f));
            }
        }

        protected virtual void DrawRecipeContent(SpriteBatch sb) {
            float alpha = fadeProgress * 0.9f;
            DynamicSpriteFont font = FontAssets.MouseText.Value;

            Vector2 contentStart = DrawPosition + new Vector2(50, 80);

            //绘制"所需材料"标题
            Utils.DrawBorderString(sb, MaterialsRequired.Value, contentStart, new Color(100, 220, 255) * alpha, 0.85f);

            //获取配方数据
            if (cachedRecipe == null) {
                cachedRecipe = GetDisplayRecipe();
            }

            if (cachedRecipe == null) {
                return;
            }

            //绘制材料列表
            Vector2 materialPos = contentStart + new Vector2(0, 35);
            float lineHeight = 50f;

            foreach (var ingredient in cachedRecipe.requiredItem) {
                if (ingredient.type == ItemID.None) {
                    continue;
                }

                //绘制物品图标并注册悬停区域
                DrawItemIcon(sb, ingredient, materialPos, alpha);

                //绘制物品名称和数量
                string itemText = $"{ingredient.Name} x{ingredient.stack}";
                Vector2 textPos = materialPos + new Vector2(50, 8);
                Utils.DrawBorderString(sb, itemText, textPos, Color.White * alpha, 0.75f);

                materialPos.Y += lineHeight;
            }

            //绘制合成站点
            materialPos.Y += 20;
            DrawCraftingStation(sb, cachedRecipe, materialPos, alpha);

            //绘制结果物品
            Vector2 resultPos = DrawPosition + new Vector2(UIWidth * expandProgress - 150, 80);
            DrawResultItem(sb, resultPos, alpha);
        }

        protected virtual string GetCraftingStationText(Recipe recipe) {
            if (recipe.requiredTile.Count > 0) {
                //获取合成台的物品ID
                int itemType = TileLoader.GetItemDropFromTypeAndStyle(recipe.requiredTile[0]);
                string tileName = VaultUtils.GetLocalizedItemName(itemType).Value;
                return $"{CraftingStation.Value} {tileName}";
            }
            return CraftingStation.Value;
        }

        protected virtual void DrawCraftingStation(SpriteBatch sb, Recipe recipe, Vector2 position, float alpha) {
            if (recipe.requiredTile.Count <= 0) {
                //没有合成台要求,显示"手工"
                Utils.DrawBorderString(sb, CraftingStation.Value, position, new Color(255, 200, 100) * alpha, 0.75f);
                return;
            }

            //获取合成台物品
            int tileType = recipe.requiredTile[0];
            int itemType = TileLoader.GetItemDropFromTypeAndStyle(tileType);

            if (itemType <= 0 || itemType >= ItemLoader.ItemCount) {
                //无效的物品ID,只显示文本
                string craftingStationText = GetCraftingStationText(recipe);
                Utils.DrawBorderString(sb, craftingStationText, position, new Color(255, 200, 100) * alpha, 0.75f);
                return;
            }

            //创建合成台物品实例
            Item craftingStationItem = new Item(itemType);
            Main.instance.LoadItem(itemType);

            //绘制合成台图标
            DrawItemIcon(sb, craftingStationItem, position + new Vector2(0, 24), alpha);

            //绘制合成台名称
            string stationName = craftingStationItem.Name;
            string stationText = $"{CraftingStation.Value} {stationName}";
            Vector2 textPos = position;
            Utils.DrawBorderString(sb, stationText, textPos, new Color(255, 200, 100) * alpha, 0.85f);
        }

        protected virtual void DrawItemIcon(SpriteBatch sb, Item item, Vector2 position, float alpha) {
            Main.instance.LoadItem(item.type);
            Texture2D itemTexture = TextureAssets.Item[item.type].Value;
            Rectangle sourceRect = itemTexture.Frame(1, 1);

            float scale = 1f;
            if (sourceRect.Width > 40 || sourceRect.Height > 40) {
                scale = 40f / Math.Max(sourceRect.Width, sourceRect.Height);
            }

            //物品图标的实际渲染区域
            Rectangle itemBounds = new Rectangle(
                (int)position.X - 2,
                (int)position.Y - 2,
                44,
                44
            );

            //注册悬停区域
            itemHoverAreas.Add(new ItemHoverArea {
                Item = item,
                Bounds = itemBounds
            });

            //检测是否悬停
            bool isHovering = itemBounds.Intersects(MouseHitBox);
            float hoverGlow = isHovering ? 0.4f : 0.2f;

            //绘制发光背景
            Texture2D pixel = VaultAsset.placeholder2.Value;
            if (pixel != null) {
                sb.Draw(pixel, itemBounds,
                    new Color(80, 200, 255) * (alpha * hoverGlow));

                //悬停时绘制额外边框
                if (isHovering) {
                    DrawItemHoverBorder(sb, itemBounds, alpha);
                }
            }

            //绘制物品
            sb.Draw(itemTexture, position + new Vector2(20, 20), sourceRect,
                Color.White * alpha, 0f, sourceRect.Size() / 2f, scale, SpriteEffects.None, 0f);
        }

        protected virtual void DrawItemHoverBorder(SpriteBatch sb, Rectangle bounds, float alpha) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            if (pixel == null) return;

            Color borderColor = new Color(100, 220, 255) * (alpha * 0.8f);
            int thickness = 2;

            //上
            sb.Draw(pixel, new Rectangle(bounds.X, bounds.Y, bounds.Width, thickness), borderColor);
            //下
            sb.Draw(pixel, new Rectangle(bounds.X, bounds.Bottom - thickness, bounds.Width, thickness), borderColor);
            //左
            sb.Draw(pixel, new Rectangle(bounds.X, bounds.Y, thickness, bounds.Height), borderColor);
            //右
            sb.Draw(pixel, new Rectangle(bounds.Right - thickness, bounds.Y, thickness, bounds.Height), borderColor);
        }

        protected virtual void DrawResultItem(SpriteBatch sb, Vector2 position, float alpha) {
            DynamicSpriteFont font = FontAssets.MouseText.Value;

            //绘制箭头
            string arrow = "=>";
            Vector2 arrowSize = font.MeasureString(arrow);
            Vector2 arrowPos = position + new Vector2(-arrowSize.X - 10, 15);
            Utils.DrawBorderString(sb, arrow, arrowPos, new Color(100, 220, 255) * alpha, 1.2f);

            if (cachedRecipe == null) {
                return;
            }

            //绘制结果物品
            Item resultItem = cachedRecipe.createItem;
            Main.instance.LoadItem(resultItem.type);

            Texture2D itemTexture = TextureAssets.Item[resultItem.type].Value;
            Rectangle sourceRect = itemTexture.Frame(1, 1);

            float scale = 1.5f;
            if (sourceRect.Width > 60 || sourceRect.Height > 60) {
                scale = 60f / Math.Max(sourceRect.Width, sourceRect.Height);
            }

            //结果物品的实际渲染区域
            Rectangle resultBounds = new Rectangle(
                (int)position.X - 5,
                (int)position.Y - 5,
                70,
                70
            );

            //注册悬停区域
            itemHoverAreas.Add(new ItemHoverArea {
                Item = resultItem,
                Bounds = resultBounds
            });

            //检测是否悬停
            bool isHovering = resultBounds.Intersects(MouseHitBox);
            float pulseScale = 1f + (float)Math.Sin(Main.GlobalTimeWrappedHourly * 3f) * 0.1f;
            float hoverGlow = isHovering ? 0.5f : 0.3f;

            //发光背景
            Texture2D pixel = VaultAsset.placeholder2.Value;
            if (pixel != null) {
                sb.Draw(pixel, resultBounds,
                    new Color(100, 220, 255) * (alpha * hoverGlow * pulseScale));

                //悬停时绘制额外边框
                if (isHovering) {
                    DrawItemHoverBorder(sb, resultBounds, alpha);
                }
            }

            //绘制物品
            sb.Draw(itemTexture, position + new Vector2(30, 30), sourceRect,
                Color.White * alpha, 0f, sourceRect.Size() / 2f, scale, SpriteEffects.None, 0f);

            //绘制物品名称
            string itemName = resultItem.Name;
            Vector2 nameSize = font.MeasureString(itemName);
            Vector2 namePos = position + new Vector2((60 - nameSize.X * 0.65f) / 2, 75);
            Utils.DrawBorderString(sb, itemName, namePos, new Color(255, 200, 100) * alpha, 0.65f);
        }

        protected virtual Rectangle GetCloseButtonRect() {
            return new Rectangle(
                UIHitBox.Right - 40,
                UIHitBox.Y + 10,
                30,
                30
            );
        }

        protected virtual void DrawCloseButton(SpriteBatch sb) {
            Rectangle closeButtonRect = GetCloseButtonRect();

            bool isHovering = closeButtonRect.Intersects(MouseHitBox);
            float alpha = fadeProgress * (isHovering ? 1f : 0.7f);

            Texture2D pixel = VaultAsset.placeholder2.Value;
            if (pixel == null) return;

            //按钮背景
            Color buttonColor = new Color(180, 60, 60) * (alpha * 0.8f);
            sb.Draw(pixel, closeButtonRect, buttonColor);

            //按钮边框
            Color borderColor = new Color(255, 100, 100) * alpha;
            sb.Draw(pixel, new Rectangle(closeButtonRect.X, closeButtonRect.Y, closeButtonRect.Width, 2), borderColor);
            sb.Draw(pixel, new Rectangle(closeButtonRect.X, closeButtonRect.Bottom - 2, closeButtonRect.Width, 2), borderColor);
            sb.Draw(pixel, new Rectangle(closeButtonRect.X, closeButtonRect.Y, 2, closeButtonRect.Height), borderColor);
            sb.Draw(pixel, new Rectangle(closeButtonRect.Right - 2, closeButtonRect.Y, 2, closeButtonRect.Height), borderColor);

            //X标记
            DynamicSpriteFont font = FontAssets.MouseText.Value;
            string closeText = "×";
            Vector2 textSize = font.MeasureString(closeText);
            Vector2 textPos = closeButtonRect.Center.ToVector2() - textSize / 2;
            Utils.DrawBorderString(sb, closeText, textPos, Color.White * alpha, 1.2f);

            //悬停提示
            if (isHovering) {
                string hintText = CloseHint.Value;
                Vector2 hintSize = font.MeasureString(hintText);
                Vector2 hintPos = new Vector2(
                    closeButtonRect.Center.X - hintSize.X / 2,
                    closeButtonRect.Bottom + 5
                );
                Utils.DrawBorderString(sb, hintText, hintPos, Color.White * alpha, 0.7f);
            }
        }

        protected virtual void DrawTechParticles(SpriteBatch sb) {
            foreach (var particle in techParticles) {
                particle.Draw(sb, fadeProgress);
            }
        }

        #endregion

        #region 公共接口

        /// <summary>
        /// 显示UI
        /// </summary>
        public void Show() {
            if (isShowing) {
                return;
            }

            isShowing = true;

            //清除缓存,确保显示最新的配方
            cachedRecipe = null;

            //播放音效
            SoundEngine.PlaySound(SoundID.Item8 with {
                Volume = 0.6f,
                Pitch = 0.3f,
                MaxInstances = 2
            });
        }

        /// <summary>
        /// 隐藏UI
        /// </summary>
        public void Hide() {
            if (!isShowing) {
                return;
            }

            isShowing = false;

            //播放音效
            SoundEngine.PlaySound(SoundID.MenuClose with {
                Volume = 0.5f,
                Pitch = 0.2f
            });
        }

        /// <summary>
        /// 检查UI是否正在显示
        /// </summary>
        public bool IsShowing() => isShowing;

        #endregion

        /// <summary>
        /// 物品悬停区域
        /// </summary>
        protected class ItemHoverArea
        {
            public Item Item { get; set; }
            public Rectangle Bounds { get; set; }
        }
    }
}
