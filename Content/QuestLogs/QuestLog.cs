using CalamityOverhaul.Content.QuestLogs.Core;
using CalamityOverhaul.Content.QuestLogs.Styles;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System.Collections.Generic;
using Terraria;

namespace CalamityOverhaul.Content.QuestLogs
{
    public class QuestLog : UIHandle
    {
        public static QuestLog Instance => UIHandleLoader.GetUIHandleOfType<QuestLog>();

        public override bool Active => visible || Main.playerInventory; //保持激活以便绘制启动器
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

        public QuestLog() {
            //初始化一些测试节点
            Nodes.Add(new QuestNode { ID = "1", Name = "启程", Description = "开始你的旅程", Position = new Vector2(0, 0), IsUnlocked = true });
            Nodes.Add(new QuestNode { ID = "2", Name = "挖掘", Description = "获得第一块矿石", Position = new Vector2(100, 0), ParentIDs = new List<string> { "1" } });
            Nodes.Add(new QuestNode { ID = "3", Name = "制作", Description = "制作工作台", Position = new Vector2(200, 50), ParentIDs = new List<string> { "2" } });
            Nodes.Add(new QuestNode { ID = "4", Name = "战斗", Description = "击败史莱姆", Position = new Vector2(200, -50), ParentIDs = new List<string> { "2" } });
            
            //设置初始面板大小
            panelRect = new Rectangle(0, 0, 800, 600);
        }

        public override void Update() {
            //更新启动器位置 (在背包右侧)
            if (Main.playerInventory) {
                //简单的定位
                launcherRect = new Rectangle(Main.screenWidth / 2 + 120, Main.screenHeight / 5 - 20, 40, 40);
                launcherHovered = launcherRect.Contains(Main.MouseScreen.ToPoint());

                if (launcherHovered) {
                    Main.LocalPlayer.mouseInterface = true;
                    if (Main.mouseLeft && Main.mouseLeftRelease) {
                        visible = !visible;
                        if (visible) {
                            //打开时居中
                            panelRect.X = (Main.screenWidth - panelRect.Width) / 2;
                            panelRect.Y = (Main.screenHeight - panelRect.Height) / 2;
                        }
                    }
                }
            }
            else {
                //背包关闭时，如果任务书是依附于背包的，则关闭
                //这里假设任务书是独立的，但启动器只在背包显示
                //如果用户希望任务书像FTB那样独立存在，可以不关闭
                //但为了体验一致性，如果通过背包打开，通常随背包关闭
                //不过FTB任务书通常有快捷键打开
                //暂时保持打开状态直到手动关闭或按ESC
                if (visible && Main.keyState.IsKeyDown(Keys.Escape)) {
                    visible = false;
                }
            }

            if (!visible) return;

            //阻止鼠标穿透
            if (panelRect.Contains(Main.MouseScreen.ToPoint())) {
                Main.LocalPlayer.mouseInterface = true;
            }

            //处理地图拖拽
            if (panelRect.Contains(Main.MouseScreen.ToPoint())) {
                //滚轮缩放
                int scroll = Mouse.GetState().ScrollWheelValue;
                if (scroll != oldScrollWheelValue) {
                    float zoomChange = (scroll - oldScrollWheelValue) > 0 ? 0.1f : -0.1f;
                    zoom = MathHelper.Clamp(zoom + zoomChange, 0.5f, 2.0f);
                }
                oldScrollWheelValue = scroll;

                if (Main.mouseLeft) {
                    if (!isDraggingMap) {
                        //检查是否点击了节点，如果点击了节点则不拖拽地图(后续添加节点交互)
                        bool clickedNode = false;
                        foreach(var node in Nodes) {
                            Vector2 nodePos = GetNodeScreenPos(node.Position);
                            if (Vector2.Distance(Main.MouseScreen, nodePos) < 20 * zoom) {
                                clickedNode = true;
                                break;
                            }
                        }

                        if (!clickedNode) {
                            isDraggingMap = true;
                            dragStartMousePos = Main.MouseScreen;
                            dragStartPanOffset = panOffset;
                        }
                    }
                    else {
                        Vector2 diff = Main.MouseScreen - dragStartMousePos;
                        panOffset = dragStartPanOffset + diff;
                    }
                }
                else {
                    isDraggingMap = false;
                }
            }
            else {
                isDraggingMap = false;
                //确保滚轮值同步，防止下次进入时跳变
                oldScrollWheelValue = Mouse.GetState().ScrollWheelValue;
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
            
            //保存当前的批次设置
            spriteBatch.End();
            
            //计算剪裁矩形 (需要适应UI缩放)
            Vector2 pos = Vector2.Transform(new Vector2(panelRect.X, panelRect.Y), Main.UIScaleMatrix);
            Vector2 size = Vector2.Transform(new Vector2(panelRect.Width, panelRect.Height), Main.UIScaleMatrix) - Vector2.Transform(Vector2.Zero, Main.UIScaleMatrix);
            Rectangle scissorRect = new Rectangle((int)pos.X, (int)pos.Y, (int)size.X, (int)size.Y);
            Rectangle origRect = spriteBatch.GraphicsDevice.ScissorRectangle;
            //限制在屏幕范围内
            scissorRect = Rectangle.Intersect(scissorRect, spriteBatch.GraphicsDevice.Viewport.Bounds);
            
            //重新开始绘制，应用剪裁
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
                //简单的悬停检测
                bool hovered = Vector2.Distance(Main.MouseScreen, nodePos) < 20 * zoom; 
                CurrentStyle.DrawNode(spriteBatch, node, nodePos, zoom, hovered);
            }

            spriteBatch.End();
            spriteBatch.GraphicsDevice.ScissorRectangle = origRect;
            //恢复默认绘制设置
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullCounterClockwise, null, Main.UIScaleMatrix);
        }

        private Vector2 GetNodeScreenPos(Vector2 nodePos) {
            Vector2 center = new Vector2(panelRect.X + panelRect.Width / 2, panelRect.Y + panelRect.Height / 2);
            return center + panOffset + nodePos * zoom;
        }
    }
}