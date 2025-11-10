using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System.Collections.Generic;
using Terraria;
using Terraria.GameContent;
using Terraria.GameContent.UI.ResourceSets;
using Terraria.ModLoader;
using Terraria.UI;

namespace CalamityOverhaul.Content.ADV.Scenarios.SupCal.End.EternalBlazingNows
{
    internal class EbnBarsDisplaySet : ModResourceOverlay
    {
        public override bool PreDrawResourceDisplay(PlayerStatsSnapshot snapshot
            , IPlayerResourcesDisplaySet displaySet, bool drawingLife, ref Color textColor, out bool drawText) {
            drawText = true;
            if (EbnBarsDisplay.ShouldDrawCustomLifeBars()) {
                return false;
            }
            return base.PreDrawResourceDisplay(snapshot, displaySet, drawingLife, ref textColor, out drawText);
        }

        public override bool DisplayHoverText(PlayerStatsSnapshot snapshot, IPlayerResourcesDisplaySet displaySet, bool drawingLife) {
            if (EbnBarsDisplay.ShouldDrawCustomLifeBars()) {
                return false;
            }
            return base.DisplayHoverText(snapshot, displaySet, drawingLife);
        }
    }

    internal class EbnBarsDisplay : ModSystem
    {
        //反射加载心脏的纹理
        [VaultLoaden(CWRConstant.ADV)]
        public static Asset<Texture2D> EbnLife;//单颗心脏的填充部分，大小22*22
        [VaultLoaden(CWRConstant.ADV)]
        public static Asset<Texture2D> EbnLifeBack;//单颗心脏的背景部分，大小30*30，也就是说，边框宽度是4

        //多排血条配置
        private const int MaxHeartsPerRow = 10;    //每行最多显示的心脏数
        private const int MaxRows = 3;             //最多显示的行数
        private const int HeartSpacing = 2;        //心脏之间的间距
        private const int RowSpacing = 4;          //行与行之间的间距

        //用于存储血条状态的变量
        private int _totalHearts;
        private int _currentLife;
        private int _maxLife;
        private float _lifePercent;
        private PlayerStatsSnapshot _preparedSnapshot;

        //用于鼠标悬停检测
        private static readonly List<Rectangle> _heartHitboxes = new();
        private static bool _isHoveringLifeBar = false;

        public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers) {
            //检查是否应该绘制自定义血条
            if (!ShouldDrawCustomLifeBars()) {
                return;
            }
            //找到血条和魔力条的资源显示层并禁用它
            int resourceIndex = layers.FindIndex(layer => layer.Name.Equals("Vanilla: Resource Bars"));
            if (resourceIndex != -1) {
                layers[resourceIndex].Active = false; // 完全禁用原版血条
            }

            //在合适的位置插入自定义血条层
            int inventoryIndex = layers.FindIndex(layer => layer.Name.Equals("Vanilla: Inventory"));
            if (inventoryIndex == -1) {
                return;
            }
            //在物品栏层之前插入自定义血条层
            layers.Insert(inventoryIndex, new LegacyGameInterfaceLayer(
                "CWRMod: Ebn Life Bars",
                delegate {
                    //准备数据
                    PreDrawResources(new PlayerStatsSnapshot(Main.LocalPlayer));
                    DrawLife(Main.spriteBatch);
                    return true;
                },
                InterfaceScaleType.UI
            ));
        }

        /// <summary>
        /// 判断是否应该绘制自定义血条
        /// </summary>
        internal static bool ShouldDrawCustomLifeBars() {
            //检查玩家是否存在且处于EBN状态
            if (Main.LocalPlayer == null || !Main.LocalPlayer.active)
                return false;

            if (!Main.LocalPlayer.TryGetModPlayer<EbnPlayer>(out var ebnPlayer))
                return false;

            return ebnPlayer.IsEbn;
        }

        public void PreDrawResources(PlayerStatsSnapshot snapshot) {
            _totalHearts = snapshot.AmountOfLifeHearts;
            _currentLife = snapshot.Life;
            _maxLife = snapshot.LifeMax;
            _lifePercent = (float)snapshot.Life / snapshot.LifeMax;
            _preparedSnapshot = snapshot;
        }

        public void DrawLife(SpriteBatch spriteBatch) {
            if (Main.dedServ || EbnLifeBack == null || EbnLife == null || EbnLife.IsDisposed || EbnLifeBack.IsDisposed)
                return;

            //清空之前的碰撞箱记录
            _heartHitboxes.Clear();
            _isHoveringLifeBar = false;

            //心脏尺寸
            int heartBackWidth = EbnLifeBack.Width();
            int heartBackHeight = EbnLifeBack.Height();
            int heartFillWidth = EbnLife.Width();
            int heartFillHeight = EbnLife.Height();

            //计算需要绘制多少排
            int totalRows = (_totalHearts + MaxHeartsPerRow - 1) / MaxHeartsPerRow;
            totalRows = System.Math.Min(totalRows, MaxRows);

            //计算总宽度（用于右对齐）
            int maxHeartsInAnyRow = System.Math.Min(MaxHeartsPerRow, _totalHearts);
            int totalWidth = maxHeartsInAnyRow * (heartBackWidth + HeartSpacing) - HeartSpacing;

            //计算起始位置（屏幕右上角偏移，向左对齐）
            int startX = Main.screenWidth - totalWidth - 22; // 22是右侧边距
            int startY = 18;

            //计算填充部分相对于背景的偏移（居中对齐）
            int fillOffsetX = (heartBackWidth - heartFillWidth) / 2;
            int fillOffsetY = (heartBackHeight - heartFillHeight) / 2;

            //绘制每一排
            for (int row = 0; row < totalRows; row++) {
                int heartsInThisRow = System.Math.Min(MaxHeartsPerRow, _totalHearts - row * MaxHeartsPerRow);
                
                //计算当前行的Y坐标
                int rowY = startY + row * (heartBackHeight + RowSpacing);

                //绘制当前行的每颗心脏
                for (int i = 0; i < heartsInThisRow; i++) {
                    int heartIndex = row * MaxHeartsPerRow + i;
                    
                    //计算当前心脏的X坐标
                    int heartX = startX + i * (heartBackWidth + HeartSpacing);
                    
                    //记录碰撞箱用于鼠标检测
                    Rectangle heartHitbox = new Rectangle(heartX, rowY, heartBackWidth, heartBackHeight);
                    _heartHitboxes.Add(heartHitbox);

                    //检测鼠标悬停
                    if (heartHitbox.Intersects((Main.MouseScreen - new Vector2(2, 2)).GetRectangle(4))) {
                        _isHoveringLifeBar = true;
                    }

                    //绘制心脏背景
                    Vector2 backPos = new Vector2(heartX, rowY);
                    spriteBatch.Draw(
                        EbnLifeBack.Value,
                        backPos,
                        null,
                        Color.White,
                        0f,
                        Vector2.Zero,
                        1f,
                        SpriteEffects.None,
                        0f
                    );

                    //计算当前心脏应该填充多少
                    int lifePerHeart = _maxLife / _totalHearts;
                    int heartStartLife = heartIndex * lifePerHeart;
                    int heartEndLife = (heartIndex + 1) * lifePerHeart;
                    
                    //如果是最后一颗心，要处理可能的余数
                    if (heartIndex == _totalHearts - 1) {
                        heartEndLife = _maxLife;
                        lifePerHeart = heartEndLife - heartStartLife;
                    }

                    //计算填充百分比
                    float fillPercent = 0f;
                    if (_currentLife > heartStartLife) {
                        int lifeInThisHeart = System.Math.Min(_currentLife - heartStartLife, lifePerHeart);
                        fillPercent = (float)lifeInThisHeart / lifePerHeart;
                    }

                    //绘制心脏填充部分
                    if (fillPercent > 0f) {
                        Vector2 fillPos = new Vector2(heartX + fillOffsetX, rowY + fillOffsetY);
                        
                        //使用源矩形来实现部分填充效果（从下往上填充）
                        int visibleHeight = (int)(heartFillHeight * fillPercent);
                        int cropTop = heartFillHeight - visibleHeight;
                        
                        Rectangle sourceRect = new Rectangle(0, cropTop, heartFillWidth, visibleHeight);
                        
                        //调整绘制位置以对齐裁剪后的纹理
                        fillPos.Y += cropTop;
                        
                        spriteBatch.Draw(
                            EbnLife.Value,
                            fillPos,
                            sourceRect,
                            Color.White,
                            0f,
                            Vector2.Zero,
                            1f,
                            SpriteEffects.None,
                            0f
                        );
                    }
                }
            }

            //绘制鼠标悬停时的详细信息
            if (_isHoveringLifeBar) {
                DrawLifeTooltip(spriteBatch);
            }
        }

        /// <summary>
        /// 绘制生命值详细信息提示
        /// </summary>
        private void DrawLifeTooltip(SpriteBatch spriteBatch) {
            //格式化生命值文本
            string lifeText = $"{_currentLife}/{_maxLife}";
            
            //计算文本尺寸
            float textScale = 1f;
            Vector2 textSize = FontAssets.MouseText.Value.MeasureString(lifeText) * textScale;
            
            //计算绘制位置（在鼠标右下方）
            Vector2 drawPos = new Vector2(Main.mouseX + 16, Main.mouseY + 16);
            
            //确保不会超出屏幕边界
            if (drawPos.X + textSize.X > Main.screenWidth) {
                drawPos.X = Main.mouseX - textSize.X - 16;
            }
            if (drawPos.Y + textSize.Y > Main.screenHeight) {
                drawPos.Y = Main.mouseY - textSize.Y - 16;
            }

            //绘制文本（带边框效果）
            Utils.DrawBorderStringFourWay(
                spriteBatch,
                FontAssets.MouseText.Value,
                lifeText,
                drawPos.X,
                drawPos.Y,
                Color.White,
                Color.Black,
                Vector2.Zero,
                textScale
            );
        }
    }
}
