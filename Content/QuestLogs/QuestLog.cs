using CalamityOverhaul.Content.QuestLogs.Core;
using CalamityOverhaul.Content.QuestLogs.Styles;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.QuestLogs
{
    public class QuestLog : UIHandle
    {
        public static QuestLog Instance => UIHandleLoader.GetUIHandleOfType<QuestLog>();

        public override bool Active => visible || Main.playerInventory;//保持激活以便绘制启动器
        private bool visible;

        public IQuestLogStyle CurrentStyle { get; set; } = new ThermalQuestLogStyle();

        public List<QuestNode> Nodes { get; set; } = new();

        private Vector2 panOffset;
        private float zoom = 1f;
        private bool isDraggingMap;
        private Vector2 dragStartMousePos;
        private Vector2 dragStartPanOffset;

        private Rectangle panelRect;
        private Rectangle launcherRect;
        private bool launcherHovered;

        private int oldScrollWheelValue;

        //任务详情面板相关
        private QuestNode selectedNode;
        private bool showDetailPanel;
        private float detailPanelAlpha;
        private Rectangle detailPanelRect;
        private const int DetailPanelWidth = 500;
        private const int DetailPanelHeight = 600;

        //节点悬停相关
        private QuestNode hoveredNode;

        public QuestLog() {
            //初始化一些测试节点
            Nodes.Add(new QuestNode { 
                ID = "1", 
                Name = "启程", 
                Description = "开始你的旅程", 
                DetailedDescription = "欢迎来到灾厄全知领域！这是你的第一个任务，完成它来熟悉系统。",
                Position = new Vector2(0, 0), 
                IsUnlocked = true,
                Type = QuestType.Main,
                Difficulty = QuestDifficulty.Easy,
                Objectives = new List<QuestObjective> {
                    new QuestObjective { Description = "探索世界", CurrentProgress = 0, RequiredProgress = 1 }
                },
                Rewards = new List<QuestReward> {
                    new QuestReward { ItemType = ItemID.Wood, Amount = 50, Description = "木材奖励" }
                }
            });
            
            Nodes.Add(new QuestNode { 
                ID = "2", 
                Name = "挖掘", 
                Description = "获得第一块矿石", 
                DetailedDescription = "挖掘是生存的基础，去地下寻找有用的矿石吧。",
                Position = new Vector2(150, 0), 
                ParentIDs = new List<string> { "1" },
                Type = QuestType.Main,
                Difficulty = QuestDifficulty.Easy,
                Objectives = new List<QuestObjective> {
                    new QuestObjective { Description = "获得铜矿", CurrentProgress = 0, RequiredProgress = 10 }
                },
                Rewards = new List<QuestReward> {
                    new QuestReward { ItemType = ItemID.CopperPickaxe, Amount = 1, Description = "铜镐奖励" }
                }
            });
            
            Nodes.Add(new QuestNode { 
                ID = "3", 
                Name = "制作", 
                Description = "制作工作台", 
                DetailedDescription = "工作台是所有制作的基础，学会如何制作它。",
                Position = new Vector2(300, 80), 
                ParentIDs = new List<string> { "2" },
                Type = QuestType.Side,
                Difficulty = QuestDifficulty.Normal,
                Objectives = new List<QuestObjective> {
                    new QuestObjective { Description = "制作工作台", CurrentProgress = 0, RequiredProgress = 1 }
                },
                Rewards = new List<QuestReward> {
                    new QuestReward { ItemType = ItemID.Wood, Amount = 100, Description = "木材奖励" }
                }
            });
            
            Nodes.Add(new QuestNode { 
                ID = "4", 
                Name = "战斗", 
                Description = "击败史莱姆", 
                DetailedDescription = "学习战斗技巧，击败你的第一个敌人。",
                Position = new Vector2(300, -80), 
                ParentIDs = new List<string> { "2" },
                Type = QuestType.Main,
                Difficulty = QuestDifficulty.Normal,
                Objectives = new List<QuestObjective> {
                    new QuestObjective { Description = "击败史莱姆", CurrentProgress = 0, RequiredProgress = 5 }
                },
                Rewards = new List<QuestReward> {
                    new QuestReward { ItemType = ItemID.Gel, Amount = 20, Description = "凝胶奖励" }
                }
            });
            
            //设置初始面板大小
            panelRect = new Rectangle(0, 0, 800, 600);
        }

        public override void Update() {
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

            //更新启动器位置(在背包右侧)
            if (Main.playerInventory) {
                launcherRect = new Rectangle(Main.screenWidth / 2 + 120, Main.screenHeight / 5 - 20, 48, 48);
                launcherHovered = launcherRect.Contains(Main.MouseScreen.ToPoint());

                if (launcherHovered) {
                    player.mouseInterface = true;
                    
                    //使用UIHandle的keyLeftPressState接口
                    if (keyLeftPressState == KeyPressState.Pressed) {
                        visible = !visible;
                        if (visible) {
                            //打开时居中
                            panelRect.X = (Main.screenWidth - panelRect.Width) / 2;
                            panelRect.Y = (Main.screenHeight - panelRect.Height) / 2;
                        }
                        else {
                            //关闭时同时关闭详情面板
                            showDetailPanel = false;
                            selectedNode = null;
                        }
                        SoundEngine.PlaySound(visible ? SoundID.MenuOpen : SoundID.MenuClose);
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

            if (!visible) return;

            //更新主UI碰撞箱
            UIHitBox = panelRect;
            hoverInMainPage = UIHitBox.Intersects(MouseHitBox);

            //阻止鼠标穿透
            if (hoverInMainPage) {
                player.mouseInterface = true;
                player.CWR().DontSwitchWeaponTime = 2;
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
                    zoom = MathHelper.Clamp(zoom + zoomChange, 0.5f, 2.0f);
                }
                oldScrollWheelValue = scroll;

                //检测节点悬停
                hoveredNode = null;
                foreach(var node in Nodes) {
                    Vector2 nodePos = GetNodeScreenPos(node.Position);
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
            Rectangle closeButtonRect = new Rectangle(
                detailPanelRect.Right - 40,
                detailPanelRect.Y + 10,
                30,
                30
            );

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
            //这里他妈的要做一次NUll检查，难道这个 selectedNode 可能为null就这么难理解吗
            if (selectedNode is not null && selectedNode.IsCompleted && selectedNode.Rewards != null) {
                Rectangle buttonRect = new Rectangle(
                    detailPanelRect.X + detailPanelRect.Width / 2 - 60,
                    detailPanelRect.Bottom - 60,
                    120,
                    35
                );

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

            foreach (var reward in node.Rewards) {
                if (!reward.Claimed) {
                    //这里添加实际给予玩家物品的逻辑
                    //Item.NewItem(null, player.Center, reward.ItemType, reward.Amount);
                    reward.Claimed = true;
                }
            }
        }

        public override void Draw(SpriteBatch spriteBatch) {
            if (Main.playerInventory) {
                CurrentStyle.DrawLauncher(spriteBatch, new Vector2(launcherRect.X, launcherRect.Y), launcherHovered);
            }

            if (!visible) return;

            CurrentStyle.DrawBackground(spriteBatch, this, panelRect);

            //开启剪裁，防止节点画出面板
            RasterizerState rasterizerState = new RasterizerState { ScissorTestEnable = true };
            
            spriteBatch.End();
            
            //计算剪裁矩形(需要适应UI缩放)
            Vector2 pos = Vector2.Transform(new Vector2(panelRect.X, panelRect.Y), Main.UIScaleMatrix);
            Vector2 size = Vector2.Transform(new Vector2(panelRect.Width, panelRect.Height), Main.UIScaleMatrix) - Vector2.Transform(Vector2.Zero, Main.UIScaleMatrix);
            Rectangle scissorRect = new Rectangle((int)pos.X, (int)pos.Y, (int)size.X, (int)size.Y);
            Rectangle origRect = spriteBatch.GraphicsDevice.ScissorRectangle;
            scissorRect = Rectangle.Intersect(scissorRect, spriteBatch.GraphicsDevice.Viewport.Bounds);
            
            spriteBatch.GraphicsDevice.ScissorRectangle = scissorRect;
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, rasterizerState, null, Main.UIScaleMatrix);

            //绘制连接线
            foreach (var node in Nodes) {
                foreach (var parentID in node.ParentIDs) {
                    var parent = Nodes.Find(n => n.ID == parentID);
                    if (parent != null) {
                        Vector2 start = GetNodeScreenPos(parent.Position);
                        Vector2 end = GetNodeScreenPos(node.Position);
                        CurrentStyle.DrawConnection(spriteBatch, start, end, node.IsUnlocked);
                    }
                }
            }

            //绘制节点
            foreach (var node in Nodes) {
                Vector2 nodePos = GetNodeScreenPos(node.Position);
                bool hovered = hoveredNode == node;
                CurrentStyle.DrawNode(spriteBatch, node, nodePos, zoom, hovered);
            }

            spriteBatch.End();
            spriteBatch.GraphicsDevice.ScissorRectangle = origRect;
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullCounterClockwise, null, Main.UIScaleMatrix);

            //绘制详情面板
            if (showDetailPanel && selectedNode != null && detailPanelAlpha > 0.01f) {
                CurrentStyle.DrawQuestDetail(spriteBatch, selectedNode, detailPanelRect, detailPanelAlpha);
                
                //绘制关闭按钮
                DrawCloseButton(spriteBatch);
            }
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