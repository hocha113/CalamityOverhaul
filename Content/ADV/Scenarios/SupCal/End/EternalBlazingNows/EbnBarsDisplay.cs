using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
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
            if (EbnBarsDisplay.ShouldDrawCustomLifeBars()) {
                drawText = true;
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

        //用于鼠标悬停检测
        private static readonly List<Rectangle> _heartHitboxes = new();
        private static bool _isHoveringLifeBar = false;

        //受伤抖动效果
        private static int _lastLife = -1;
        private static int _shakeTimer = 0;
        private const int ShakeDuration = 15; //抖动持续帧数
        private static readonly Dictionary<int, float> _heartScales = new(); //每个心脏的缩放值
        private static readonly Dictionary<int, int> _heartDamageTimers = new(); //每个心脏的受伤计时器

        //心跳动画
        private static float _heartbeatPhase = 0f;

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

            //检测受伤
            if (_lastLife > 0 && _currentLife < _lastLife) {
                _shakeTimer = ShakeDuration;
                
                //计算受伤影响的心脏范围
                int lifePerHeart = _maxLife / _totalHearts;
                int damagedHeartStart = _currentLife / lifePerHeart;
                int damagedHeartEnd = _lastLife / lifePerHeart;
                
                //为受影响的心脏添加受伤动画
                for (int i = damagedHeartStart; i <= Math.Min(damagedHeartEnd, _totalHearts - 1); i++) {
                    _heartDamageTimers[i] = 30; //受伤动画持续时间
                }
            }
            _lastLife = _currentLife;

            //更新抖动计时器
            if (_shakeTimer > 0) {
                _shakeTimer--;
            }

            //更新每个心脏的受伤计时器
            List<int> keysToRemove = new();
            foreach (var key in _heartDamageTimers.Keys) {
                _heartDamageTimers[key]--;
                if (_heartDamageTimers[key] <= 0) {
                    keysToRemove.Add(key);
                }
            }
            foreach (var key in keysToRemove) {
                _heartDamageTimers.Remove(key);
            }

            //更新心跳相位
            _heartbeatPhase += 0.08f;
            if (_heartbeatPhase > MathHelper.TwoPi) {
                _heartbeatPhase -= MathHelper.TwoPi;
            }
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
            totalRows = Math.Min(totalRows, MaxRows);

            //计算总宽度（用于右对齐）
            int maxHeartsInAnyRow = Math.Min(MaxHeartsPerRow, _totalHearts);
            int totalWidth = maxHeartsInAnyRow * (heartBackWidth + HeartSpacing) - HeartSpacing;

            //计算起始位置（屏幕右上角偏移，向左对齐）
            int startX = Main.screenWidth - totalWidth - 4;
            int startY = 18;

            //计算全局抖动偏移
            Vector2 globalShakeOffset = Vector2.Zero;
            if (_shakeTimer > 0) {
                float shakeIntensity = (_shakeTimer / (float)ShakeDuration) * 3f;
                globalShakeOffset = new Vector2(
                    Main.rand.NextFloat(-shakeIntensity, shakeIntensity),
                    Main.rand.NextFloat(-shakeIntensity, shakeIntensity)
                );
            }

            //绘制每一排
            for (int row = 0; row < totalRows; row++) {
                int heartsInThisRow = Math.Min(MaxHeartsPerRow, _totalHearts - row * MaxHeartsPerRow);
                
                //计算当前行的Y坐标
                int rowY = startY + row * (heartBackHeight + RowSpacing);

                //绘制当前行的每颗心脏
                for (int i = 0; i < heartsInThisRow; i++) {
                    int heartIndex = row * MaxHeartsPerRow + i;
                    
                    //计算当前心脏的X坐标
                    int heartX = startX + i * (heartBackWidth + HeartSpacing);
                    
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
                        int lifeInThisHeart = Math.Min(_currentLife - heartStartLife, lifePerHeart);
                        fillPercent = (float)lifeInThisHeart / lifePerHeart;
                    }

                    //计算心脏的缩放值（基于生命值百分比）
                    float targetScale = 0.5f + fillPercent * 0.5f; // 范围：0.5 到 1.0
                    
                    //添加心跳动画（仅对有生命值的心脏）
                    if (fillPercent > 0) {
                        float heartbeat = (float)Math.Sin(_heartbeatPhase * 2f) * 0.05f;
                        targetScale += heartbeat;
                    }

                    //平滑过渡到目标缩放
                    if (!_heartScales.ContainsKey(heartIndex)) {
                        _heartScales[heartIndex] = targetScale;
                    }
                    else {
                        _heartScales[heartIndex] = MathHelper.Lerp(_heartScales[heartIndex], targetScale, 0.15f);
                    }
                    float currentScale = _heartScales[heartIndex];

                    //计算个体抖动偏移（受伤的心脏）
                    Vector2 individualShakeOffset = Vector2.Zero;
                    if (_heartDamageTimers.TryGetValue(heartIndex, out int damageTimer)) {
                        float damageShakeIntensity = (damageTimer / 30f) * 4f;
                        individualShakeOffset = new Vector2(
                            (float)Math.Sin(damageTimer * 0.5f) * damageShakeIntensity,
                            (float)Math.Cos(damageTimer * 0.7f) * damageShakeIntensity
                        );
                    }

                    //计算心脏中心点
                    Vector2 heartCenter = new Vector2(
                        heartX + heartBackWidth / 2f,
                        rowY + heartBackHeight / 2f
                    ) + globalShakeOffset + individualShakeOffset;

                    //记录碰撞箱用于鼠标检测（使用缩放后的尺寸）
                    int scaledWidth = (int)(heartBackWidth * currentScale);
                    int scaledHeight = (int)(heartBackHeight * currentScale);
                    Rectangle heartHitbox = new Rectangle(
                        (int)(heartCenter.X - scaledWidth / 2f),
                        (int)(heartCenter.Y - scaledHeight / 2f),
                        scaledWidth,
                        scaledHeight
                    );
                    _heartHitboxes.Add(heartHitbox);

                    //检测鼠标悬停
                    if (heartHitbox.Intersects((Main.MouseScreen - new Vector2(2, 2)).GetRectangle(4))) {
                        _isHoveringLifeBar = true;
                    }

                    //计算背景绘制参数
                    Vector2 backOrigin = new Vector2(heartBackWidth / 2f, heartBackHeight / 2f);
                    
                    //绘制心脏背景
                    spriteBatch.Draw(
                        EbnLifeBack.Value,
                        heartCenter,
                        null,
                        Color.White,
                        0f,
                        backOrigin,
                        currentScale,
                        SpriteEffects.None,
                        0f
                    );

                    //绘制心脏填充部分（同样从中心缩放）
                    if (fillPercent > 0f) {
                        Vector2 fillOrigin = new Vector2(heartFillWidth / 2f, heartFillHeight / 2f);
                        
                        //计算填充部分的颜色（生命值低时变红）
                        Color fillColor = Color.White;
                        if (fillPercent < 0.3f) {
                            //生命值低于30%时，心脏变红并闪烁
                            float pulseIntensity = (float)Math.Sin(Main.GlobalTimeWrappedHourly * 8f) * 0.3f + 0.7f;
                            fillColor = Color.Lerp(Color.Red, Color.White, fillPercent / 0.3f) * pulseIntensity;
                        }
                        
                        spriteBatch.Draw(
                            EbnLife.Value,
                            heartCenter,
                            null,
                            fillColor,
                            0f,
                            fillOrigin,
                            currentScale,
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
            
            //计算百分比
            float lifePercent = (_currentLife / (float)_maxLife) * 100f;
            string percentText = $"({lifePercent:F1}%)";
            
            //计算文本尺寸
            float textScale = 1f;
            Vector2 lifeTextSize = FontAssets.MouseText.Value.MeasureString(lifeText) * textScale;
            Vector2 percentTextSize = FontAssets.MouseText.Value.MeasureString(percentText) * textScale * 0.8f;
            
            //计算总尺寸
            Vector2 totalSize = new Vector2(
                Math.Max(lifeTextSize.X, percentTextSize.X),
                lifeTextSize.Y + percentTextSize.Y + 4
            );
            
            //计算绘制位置（在鼠标右下方）
            Vector2 drawPos = new Vector2(Main.mouseX + 16, Main.mouseY + 16);
            
            //确保不会超出屏幕边界
            if (drawPos.X + totalSize.X > Main.screenWidth) {
                drawPos.X = Main.mouseX - totalSize.X - 16;
            }
            if (drawPos.Y + totalSize.Y > Main.screenHeight) {
                drawPos.Y = Main.mouseY - totalSize.Y - 16;
            }

            //绘制主要生命值文本
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

            //绘制百分比文本（稍小且偏下）
            Vector2 percentPos = drawPos + new Vector2(0, lifeTextSize.Y + 4);
            Color percentColor = lifePercent < 30f ? Color.Red : 
                                lifePercent < 50f ? Color.Yellow : 
                                Color.LimeGreen;
            
            Utils.DrawBorderStringFourWay(
                spriteBatch,
                FontAssets.MouseText.Value,
                percentText,
                percentPos.X,
                percentPos.Y,
                percentColor,
                Color.Black,
                Vector2.Zero,
                textScale * 0.8f
            );
        }
    }
}
