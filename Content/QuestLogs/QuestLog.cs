using CalamityOverhaul.Common;
using CalamityOverhaul.Content.QuestLogs.Core;
using CalamityOverhaul.Content.QuestLogs.Styles;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using ReLogic.Content;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.QuestLogs
{
    public class QuestLog : UIHandle, ILocalizedModType
    {
        [VaultLoaden(CWRConstant.UI)]
        public static Asset<Texture2D> QuestLogStart = null;
        public static QuestLog Instance => UIHandleLoader.GetUIHandleOfType<QuestLog>();

        public override bool Active => (visible || openScale > 0.01f || Main.playerInventory) && CWRServerConfig.Instance.QuestLog;
        internal bool visible;
        public float MainPanelAlpha => mainPanelAlpha;
        private float mainPanelAlpha;
        private float openScale;
        private Rectangle mainCloseButtonRect;

        public IQuestLogStyle CurrentStyle { get; set; } = new HotwindQuestLogStyle();

        public IReadOnlyCollection<QuestNode> Nodes => QuestNode.AllQuests;

        private Vector2 panOffset;
        private float zoom = 1f;
        private bool isDraggingMap;
        private Vector2 dragStartMousePos;
        private Vector2 dragStartPanOffset;

        private Rectangle panelRect;
        private int oldScrollWheelValue;

        //任务详情面板相关
        private QuestNode selectedNode;
        private QuestNode selectedNodeTransfers;
        private bool showDetailPanel;
        private float detailPanelAlpha;
        private Rectangle detailPanelRect;
        private const int DetailPanelWidth = 500;
        private const int DetailPanelHeight = 600;

        //节点悬停相关
        private QuestNode hoveredNode;

        //进度条相关
        public bool ShowProgressBar { get; set; } = true;

        public string LocalizationCategory => "UI";

        //启动图标
        private QuestLogLauncher launcher;

        public static LocalizedText ObjectiveText;
        public static LocalizedText RewardText;
        public static LocalizedText ReceiveAwardText;
        public static LocalizedText ProgressText;

        public QuestLog() {
            //初始化启动图标
            launcher = new QuestLogLauncher();
            //设置初始面板大小
            panelRect = new Rectangle(0, 0, 800, 600);
        }

        public override void SetStaticDefaults() {
            ObjectiveText = this.GetLocalization(nameof(ObjectiveText), () => "任务目标");
            RewardText = this.GetLocalization(nameof(RewardText), () => "任务奖励");
            ReceiveAwardText = this.GetLocalization(nameof(ReceiveAwardText), () => "领取奖励");
            ProgressText = this.GetLocalization(nameof(ProgressText), () => "任务完成比例");
        }

        public override void LogicUpdate() {
            //更新启动器位置和状态
            if (Main.playerInventory) {
                if (launcher.IsHovered) {
                    if (keyLeftPressState == KeyPressState.Pressed) {
                        visible = !visible;
                        if (!visible) {
                            //关闭时同时关闭详情面板
                            showDetailPanel = false;
                            selectedNode = null;
                        }
                        launcher.PlayClickSound(visible);
                    }
                }
            }
            else {
                //使用键盘输入检测关闭
                if (visible && Main.keyState.IsKeyDown(Keys.Escape) && Main.oldKeyState.IsKeyUp(Keys.Escape)) {
                    if (showDetailPanel) {
                        //如果详情面板开启，先关闭详情面板
                        showDetailPanel = false;
                        selectedNode = null;
                        SoundEngine.PlaySound(SoundID.MenuClose);
                    }
                    else {
                        //否则关闭主面板
                        visible = false;
                        SoundEngine.PlaySound(SoundID.MenuClose);
                    }
                }
            }
            CurrentStyle?.UpdateStyle();
        }

        public override void Update() {
            //更新动画状态
            if (visible) {
                openScale = MathHelper.Lerp(openScale, 1f, 0.14f);
                mainPanelAlpha = MathHelper.Lerp(mainPanelAlpha, 1f, 0.14f);
            }
            else {
                openScale = MathHelper.Lerp(openScale, 0f, 0.14f);
                mainPanelAlpha = MathHelper.Lerp(mainPanelAlpha, 0f, 0.14f);
            }

            //更新详情面板透明度
            if (showDetailPanel) {
                if (detailPanelAlpha < 1f) {
                    detailPanelAlpha += 0.1f;
                }
            }
            else {
                if (detailPanelAlpha > 0f) {
                    detailPanelAlpha -= 0.1f;
                }
            }

            //更新启动器位置和状态
            if (Main.playerInventory) {
                launcher.Update(new Vector2(570, 36), visible);
                if (launcher.IsHovered) {
                    player.mouseInterface = true;
                }
            }

            if (openScale <= 0.01f && !visible) return;

            //打开时居中
            panelRect.X = (Main.screenWidth - panelRect.Width) / 2;
            panelRect.Y = (Main.screenHeight - panelRect.Height) / 2;

            //更新主UI碰撞箱
            UIHitBox = panelRect;
            hoverInMainPage = UIHitBox.Intersects(MouseHitBox);

            //阻止鼠标穿透
            if (hoverInMainPage) {
                player.mouseInterface = true;
                player.CWR().DontSwitchWeaponTime = 2;
            }

            //关闭按钮逻辑
            mainCloseButtonRect = new Rectangle(panelRect.Right - 35, panelRect.Y + 5, 30, 30);
            if (mainCloseButtonRect.Contains(MouseHitBox.Location)) {
                player.mouseInterface = true;
                if (keyLeftPressState == KeyPressState.Pressed) {
                    visible = false;
                    SoundEngine.PlaySound(SoundID.MenuClose);
                }
            }

            //如果详情面板开启，优先处理详情面板交互
            if (showDetailPanel && detailPanelAlpha > 0.5f) {
                UpdateDetailPanel();
                return;
            }

            //处理地图拖拽和缩放
            if (hoverInMainPage) {
                //滚轮缩放
                int scroll = Mouse.GetState().ScrollWheelValue;
                if (scroll != oldScrollWheelValue) {
                    float zoomChange = (scroll - oldScrollWheelValue) > 0 ? 0.1f : -0.1f;
                    float oldZoom = zoom;
                    float newZoom = MathHelper.Clamp(zoom + zoomChange, 0.5f, 2.0f);

                    if (oldZoom != newZoom) {
                        Vector2 center = new Vector2(panelRect.X + panelRect.Width / 2, panelRect.Y + panelRect.Height / 2);
                        Vector2 relativeMouse = Main.MouseScreen - center;
                        panOffset = relativeMouse - (relativeMouse - panOffset) * (newZoom / oldZoom);
                        zoom = newZoom;
                    }
                }
                oldScrollWheelValue = scroll;

                //检测节点悬停
                hoveredNode = null;
                foreach (var node in Nodes) {
                    Vector2 nodePos = GetNodeScreenPos(node.CalculatedPosition);
                    float nodeSize = 24 * zoom;
                    if (Vector2.Distance(Main.MouseScreen, nodePos) < nodeSize) {
                        hoveredNode = node;
                        break;
                    }
                }

                //使用UIHandle的keyLeftPressState处理点击
                if (keyLeftPressState == KeyPressState.Pressed) {
                    if (hoveredNode != null) {
                        //点击了节点，打开详情面板
                        selectedNode = hoveredNode;
                        showDetailPanel = true;
                        SoundEngine.PlaySound(SoundID.MenuTick);

                        //计算详情面板位置(居中)
                        detailPanelRect = new Rectangle(
                            (Main.screenWidth - DetailPanelWidth) / 2,
                            (Main.screenHeight - DetailPanelHeight) / 2,
                            DetailPanelWidth,
                            DetailPanelHeight
                        );
                    }
                    else {
                        //没点击节点，开始拖拽地图
                        isDraggingMap = true;
                        dragStartMousePos = Main.MouseScreen;
                        dragStartPanOffset = panOffset;
                    }
                }

                //处理拖拽
                if (keyLeftPressState == KeyPressState.Held && isDraggingMap) {
                    Vector2 diff = Main.MouseScreen - dragStartMousePos;
                    panOffset = dragStartPanOffset + diff;
                }

                //释放拖拽
                if (keyLeftPressState == KeyPressState.Released) {
                    isDraggingMap = false;
                }
            }
            else {
                isDraggingMap = false;
                hoveredNode = null;
                oldScrollWheelValue = Mouse.GetState().ScrollWheelValue;
            }
        }

        private void UpdateDetailPanel() {
            if (selectedNode == null) return;

            //阻止鼠标穿透
            if (detailPanelRect.Contains(Main.MouseScreen.ToPoint())) {
                player.mouseInterface = true;
            }

            //检查关闭按钮点击
            Rectangle closeButtonRect = CurrentStyle.GetCloseButtonRect(detailPanelRect);

            bool hoverCloseButton = closeButtonRect.Contains(Main.MouseScreen.ToPoint());
            if (hoverCloseButton) {
                player.mouseInterface = true;
                //使用UIHandle的keyLeftPressState接口
                if (keyLeftPressState == KeyPressState.Pressed) {
                    showDetailPanel = false;
                    selectedNode = null;
                    SoundEngine.PlaySound(SoundID.MenuClose);
                }
            }

            //检查领取奖励按钮点击
            if (selectedNode is not null && selectedNode.IsCompleted && selectedNode.Rewards != null && selectedNode.Rewards.Exists(r => !r.Claimed)) {
                Rectangle buttonRect = CurrentStyle.GetRewardButtonRect(detailPanelRect);

                bool hoverRewardButton = buttonRect.Contains(Main.MouseScreen.ToPoint());
                if (hoverRewardButton) {
                    player.mouseInterface = true;
                    //使用UIHandle的keyLeftPressState接口
                    if (keyLeftPressState == KeyPressState.Pressed) {
                        ClaimRewards(selectedNode);
                        SoundEngine.PlaySound(SoundID.Grab);
                    }
                }
            }
        }

        private void ClaimRewards(QuestNode node) {
            if (node.Rewards == null) return;

            Player player = Main.LocalPlayer;
            foreach (var reward in node.Rewards) {
                if (!reward.Claimed) {
                    player.QuickSpawnItem(player.GetSource_GiftOrReward(), reward.ItemType, reward.Amount);
                    reward.Claimed = true;
                }
            }
        }

        public override void Draw(SpriteBatch spriteBatch) {
            //绘制启动图标
            if (Main.playerInventory) {
                launcher.Draw(spriteBatch, visible);
            }

            if (openScale <= 0.01f && !visible) return;

            CurrentStyle.DrawBackground(spriteBatch, this, panelRect);

            //绘制主面板关闭按钮
            DrawMainCloseButton(spriteBatch);

            //开启剪裁，防止节点画出面板
            RasterizerState rasterizerState = new RasterizerState { ScissorTestEnable = true };

            spriteBatch.End();

            //计算剪裁矩形(需要适应UI缩放)
            int margin = 4;//界面的边框为4像素宽
            Vector2 pos = Vector2.Transform(new Vector2(panelRect.X + margin, panelRect.Y + margin), Main.UIScaleMatrix);
            Vector2 size = Vector2.Transform(new Vector2(panelRect.Width - margin * 2, panelRect.Height - margin * 2), Main.UIScaleMatrix) - Vector2.Transform(Vector2.Zero, Main.UIScaleMatrix);
            Rectangle scissorRect = new Rectangle((int)pos.X, (int)pos.Y, (int)size.X, (int)size.Y);
            Rectangle origRect = spriteBatch.GraphicsDevice.ScissorRectangle;
            scissorRect = Rectangle.Intersect(scissorRect, spriteBatch.GraphicsDevice.Viewport.Bounds);

            spriteBatch.GraphicsDevice.ScissorRectangle = scissorRect;
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, rasterizerState, null, Main.UIScaleMatrix);

            //绘制连接线
            foreach (var node in Nodes) {
                foreach (var parentID in node.ParentIDs) {
                    var parent = QuestNode.GetQuest(parentID);
                    if (parent != null) {
                        Vector2 start = GetNodeScreenPos(parent.CalculatedPosition);
                        Vector2 end = GetNodeScreenPos(node.CalculatedPosition);
                        CurrentStyle.DrawConnection(spriteBatch, start, end, node.IsUnlocked, mainPanelAlpha);
                    }
                }
            }

            //绘制节点
            foreach (var node in Nodes) {
                Vector2 nodePos = GetNodeScreenPos(node.CalculatedPosition);
                bool hovered = hoveredNode == node;
                if (node.PreDraw(spriteBatch, nodePos, zoom, hovered, mainPanelAlpha)) {
                    CurrentStyle.DrawNode(spriteBatch, node, nodePos, zoom, hovered, mainPanelAlpha);
                }
                node.PostDraw(spriteBatch, nodePos, zoom, hovered, mainPanelAlpha);
            }

            spriteBatch.End();
            spriteBatch.GraphicsDevice.ScissorRectangle = origRect;
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullCounterClockwise, null, Main.UIScaleMatrix);

            //绘制详情面板
            if (showDetailPanel || detailPanelAlpha > 0.01f) {
                if (selectedNode is not null) {
                    selectedNodeTransfers = selectedNode;
                }
                if (selectedNodeTransfers is not null) {
                    CurrentStyle.DrawQuestDetail(spriteBatch, selectedNodeTransfers, detailPanelRect, detailPanelAlpha);
                }
                //绘制关闭按钮
                DrawCloseButton(spriteBatch);
            }
            else {
                selectedNodeTransfers = null;
            }

            //绘制进度条
            CurrentStyle.DrawProgressBar(spriteBatch, this, panelRect);
        }

        private void DrawMainCloseButton(SpriteBatch spriteBatch) {
            bool hovered = mainCloseButtonRect.Contains(Main.MouseScreen.ToPoint());
            Color buttonColor = hovered ? new Color(255, 100, 100) : new Color(200, 80, 80);

            Texture2D pixel = VaultAsset.placeholder2.Value;
            spriteBatch.Draw(pixel, mainCloseButtonRect, buttonColor * mainPanelAlpha);

            //绘制X符号
            string closeText = "×";
            Vector2 textSize = FontAssets.MouseText.Value.MeasureString(closeText);
            Vector2 textPos = new Vector2(
                mainCloseButtonRect.X + mainCloseButtonRect.Width / 2,
                mainCloseButtonRect.Y + mainCloseButtonRect.Height / 2
            );
            Utils.DrawBorderString(spriteBatch, closeText, textPos, Color.White * mainPanelAlpha, 1.2f, 0.5f, 0.5f);
        }

        private void DrawCloseButton(SpriteBatch spriteBatch) {
            Rectangle closeButtonRect = new Rectangle(
                detailPanelRect.Right - 40,
                detailPanelRect.Y + 10,
                30,
                30
            );

            bool hovered = closeButtonRect.Contains(Main.MouseScreen.ToPoint());
            Color buttonColor = hovered ? new Color(255, 100, 100) : new Color(200, 80, 80);

            Texture2D pixel = VaultAsset.placeholder2.Value;
            spriteBatch.Draw(pixel, closeButtonRect, buttonColor * detailPanelAlpha);

            //绘制X符号
            string closeText = "×";
            Vector2 textSize = FontAssets.MouseText.Value.MeasureString(closeText);
            Vector2 textPos = new Vector2(
                closeButtonRect.X + closeButtonRect.Width / 2,
                closeButtonRect.Y + closeButtonRect.Height / 2
            );
            Utils.DrawBorderString(spriteBatch, closeText, textPos, Color.White * detailPanelAlpha, 1.2f, 0.5f, 0.5f);
        }

        private Vector2 GetNodeScreenPos(Vector2 nodePos) {
            Vector2 center = new Vector2(panelRect.X + panelRect.Width / 2, panelRect.Y + panelRect.Height / 2);
            return center + panOffset + nodePos * zoom;
        }
    }
}