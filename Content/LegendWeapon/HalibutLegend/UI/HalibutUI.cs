using CalamityOverhaul.Common;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using static CalamityOverhaul.Content.LegendWeapon.HalibutLegend.UI.HalibutUIAsset;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.UI
{
    [VaultLoaden(CWRConstant.UI + "Halibut/")]//用反射标签加载对应文件夹下的所有资源
    internal static class HalibutUIAsset
    {
        //按钮，大小46*26
        public static Texture2D Button;
        //大比目鱼的头像图标，放置在屏幕左下角作为UI的入口，大小74*74
        public static Texture2D Head;
        //左侧边栏，大小218*42
        public static Texture2D LeftSidebar;
        //面板，大小242*214
        public static Texture2D Panel;
        //图标栏，大小60*52
        public static Texture2D PictureSlot;
        //技能图标，大小170*34，共五帧，对应五种技能的图标
        public static Texture2D Skillcon;
        public static Texture2D LeftButton;
        public static Texture2D RightButton;
    }

    /// <summary>
    /// 技能图标飞行实体，用于新技能解锁时的视觉效果
    /// </summary>
    internal class SkillIconEntity
    {
        public FishSkill FishSkill;
        public Vector2 Position;
        public Vector2 Velocity;
        public float Scale = 0.5f;
        public float Rotation;
        public float RotationSpeed;
        public float Alpha = 1f;
        public int LifeTime;
        public int MaxLifeTime = 60; // 1秒的飞行时间
        
        // 贝塞尔曲线控制点
        private Vector2 startPos;
        private Vector2 endPos;
        private Vector2 controlPoint1;
        private Vector2 controlPoint2;
        
        public SkillIconEntity(FishSkill fishSkill, Vector2 start, Vector2 end)
        {
            FishSkill = fishSkill;
            startPos = start;
            endPos = end;
            
            // 创建贝塞尔曲线控制点，产生优雅的弧线飞行轨迹
            Vector2 mid = (start + end) / 2;
            float distance = Vector2.Distance(start, end);
            
            // 第一个控制点：向上偏移
            controlPoint1 = start + new Vector2(distance * 0.2f, -distance * 0.4f);
            // 第二个控制点：继续向上但开始向目标靠近
            controlPoint2 = mid + new Vector2(distance * 0.1f, -distance * 0.3f);
            
            Position = start;
            RotationSpeed = (Main.rand.NextFloat() - 0.5f) * 0.2f; // 随机旋转速度
            LifeTime = 0;
        }
        
        /// <summary>
        /// 三次贝塞尔曲线插值
        /// </summary>
        private static Vector2 CubicBezier(float t, Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3)
        {
            float u = 1 - t;
            float tt = t * t;
            float uu = u * u;
            float uuu = uu * u;
            float ttt = tt * t;
            
            Vector2 p = uuu * p0; // (1-t)^3 * P0
            p += 3 * uu * t * p1; // 3(1-t)^2 * t * P1
            p += 3 * u * tt * p2; // 3(1-t) * t^2 * P2
            p += ttt * p3; // t^3 * P3
            
            return p;
        }
        
        /// <summary>
        /// 缓动函数：EaseOutCubic - 快速开始，缓慢结束
        /// </summary>
        private static float EaseOutCubic(float t)
        {
            return 1 - (float)Math.Pow(1 - t, 3);
        }
        
        public bool Update()
        {
            LifeTime++;
            
            // 计算进度（0到1）
            float progress = (float)LifeTime / MaxLifeTime;
            float easedProgress = EaseOutCubic(progress);
            
            // 使用贝塞尔曲线计算位置
            Position = CubicBezier(easedProgress, startPos, controlPoint1, controlPoint2, endPos);
            
            // 旋转效果
            Rotation += RotationSpeed;
            
            // 缩放动画：先放大再缩小
            if (progress < 0.3f)
            {
                Scale = 0.5f + (progress / 0.3f) * 0.5f; // 0.5 -> 1.0
            }
            else
            {
                Scale = 1.0f - ((progress - 0.3f) / 0.7f) * 0.4f; // 1.0 -> 0.6
            }
            
            // 透明度：到达终点前保持不透明，最后快速淡出
            if (progress < 0.85f)
            {
                Alpha = 1f;
            }
            else
            {
                Alpha = 1f - ((progress - 0.85f) / 0.15f);
            }
            
            return LifeTime >= MaxLifeTime;
        }
        
        public void Draw(SpriteBatch spriteBatch)
        {
            if (FishSkill?.Icon == null) return;
            
            Vector2 iconSize = new Vector2(Skillcon.Width, Skillcon.Height / 5);
            Vector2 origin = iconSize / 2;
            
            // 绘制发光外圈
            Color glowColor = Color.Gold with { A = 0 } * (Alpha * 0.6f);
            for (int i = 0; i < 4; i++)
            {
                float angle = MathHelper.TwoPi * i / 4 + Rotation;
                Vector2 offset = new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)) * 3f;
                spriteBatch.Draw(FishSkill.Icon, Position + offset, null, glowColor, Rotation, origin, Scale * 1.1f, SpriteEffects.None, 0);
            }
            
            // 绘制主图标
            Color mainColor = Color.White * Alpha;
            spriteBatch.Draw(FishSkill.Icon, Position, null, mainColor, Rotation, origin, Scale, SpriteEffects.None, 0);
            
            // 绘制星星粒子效果
            if (LifeTime % 3 == 0 && Alpha > 0.5f)
            {
                float starScale = Main.rand.NextFloat(0.3f, 0.6f);
                Vector2 starOffset = new Vector2(Main.rand.NextFloat(-10, 10), Main.rand.NextFloat(-10, 10));
                Color starColor = Color.Lerp(Color.Gold, Color.White, Main.rand.NextFloat()) with { A = 0 } * (Alpha * 0.8f);
                // 这里可以绘制星星贴图，如果没有就用简单的圆点代替
            }
        }
    }

    internal class HalibutUIHead : UIHandle
    {
        public static HalibutUIHead Instance => UIHandleLoader.GetUIHandleOfType<HalibutUIHead>();
        private bool _active;
        public override bool Active {
            get {
                if (!Main.playerInventory || !_active) {
                    _active = player.GetItem().type == HalibutOverride.ID;
                }
                return _active;
            }
        }
        public bool Open;
        public FishSkill FishSkill;
        public override void Update() {
            Size = Head.Size();
            DrawPosition = new Vector2(-4, Main.screenHeight - Size.Y);
            UIHitBox = DrawPosition.GetRectangle(Size);
            hoverInMainPage = UIHitBox.Intersects(MouseHitBox);

            if (hoverInMainPage) {
                player.mouseInterface = true;
                if (keyLeftPressState == KeyPressState.Pressed) {
                    Open = !Open;
                    SoundEngine.PlaySound(CWRSound.ButtonZero);
                }
            }

            HalibutUILeftSidebar.Instance.Update();
            HalibutUIPanel.Instance.Update();
        }

        public override void Draw(SpriteBatch spriteBatch) {
            HalibutUIPanel.Instance.Draw(spriteBatch);
            HalibutUILeftSidebar.Instance.Draw(spriteBatch);

            spriteBatch.Draw(Head, UIHitBox, Color.White);

            if (FishSkill != null) {//添加一个14的偏移量让这个技能图标刚好覆盖眼睛
                spriteBatch.Draw(FishSkill.Icon, DrawPosition + new Vector2(14), null, Color.White);
            }
        }
    }

    internal class HalibutUILeftSidebar : UIHandle
    {
        public static HalibutUILeftSidebar Instance => UIHandleLoader.GetUIHandleOfType<HalibutUILeftSidebar>();
        public override LayersModeEnum LayersMode => LayersModeEnum.None;//不被自动更新，需要手动调用Update和Draw
        public float Sengs;
        public override void Update() {
            if (HalibutUIHead.Instance.Open) {
                if (Sengs < 1f) {
                    Sengs += 0.1f;
                }
            }
            else {
                if (Sengs > 0f && HalibutUIPanel.Instance.Sengs <= 0f) {//面板完全关闭后侧边栏才开始关闭
                    Sengs -= 0.1f;
                }
            }

            Sengs = Math.Clamp(Sengs, 0f, 1f);

            Size = LeftSidebar.Size();
            int topHeight = (int)((Size.Y - 60) * Sengs);//侧边栏要抬高的距离，减60是为了让侧边栏别完全升出来
            DrawPosition = new Vector2(0, Main.screenHeight - HalibutUIHead.Instance.Size.Y - 20 - topHeight);//28刚好是侧边栏顶部高礼帽的高度
            UIHitBox = DrawPosition.GetRectangle(Size);
            hoverInMainPage = UIHitBox.Intersects(MouseHitBox);


        }
        public override void Draw(SpriteBatch spriteBatch) {
            //他妈的我不知道为什么这里裁剪画布不生效，那么就这么将就着画吧，反正插穿鱼头也没人在意，就像他妈的澳大利亚人要打多少只袋鼠一样无聊
            spriteBatch.Draw(LeftSidebar, UIHitBox, Color.White);
        }
    }

    internal class HalibutUIPanel : UIHandle
    {
        public static HalibutUIPanel Instance => UIHandleLoader.GetUIHandleOfType<HalibutUIPanel>();
        public override LayersModeEnum LayersMode => LayersModeEnum.None;//不被自动更新，需要手动调用Update和Draw
        public List<SkillSlot> halibutUISkillSlots = [];
        public LeftButtonUI leftButton = new LeftButtonUI();
        public RightButtonUI rightButton = new RightButtonUI();
        public float Sengs;
        
        // 滚动相关字段
        public int scrollOffset = 0; // 目标滚动偏移量
        private float currentScrollOffset = 0f; // 当前实际滚动偏移量（用于平滑动画）
        private float scrollVelocity = 0f; // 滚动速度（用于弹簧效果）
        public const int maxVisibleSlots = 3; // 最多同时显示3个技能槽位
        
        // 动画参数
        private const float ScrollStiffness = 0.3f; // 弹簧刚度
        private const float ScrollDamping = 0.7f; // 阻尼系数
        private const float ScrollThreshold = 0.01f; // 停止阈值
        
        // 粒子系统
        public List<SkillIconEntity> flyingParticles = [];
        
        // 待激活的技能槽位（粒子到达后才激活）
        private Dictionary<SkillSlot, int> pendingSlots = []; // 槽位 -> 对应的粒子索引
        
        /// <summary>
        /// 添加新技能并触发飞行动画
        /// </summary>
        public void AddSkillWithAnimation(FishSkill fishSkill, Vector2 startPosition)
        {
            // 计算目标位置（列表中的位置）
            int futureIndex = halibutUISkillSlots.Count;
            int visibleIndex = futureIndex - scrollOffset;
            
            // 如果新技能会在可见范围内，计算其目标位置
            Vector2 targetPos;
            if (visibleIndex >= 0 && visibleIndex < maxVisibleSlots)
            {
                targetPos = DrawPosition + new Vector2(52, 30);
                targetPos.X += visibleIndex * (Skillcon.Width + 4);
                targetPos += new Vector2(Skillcon.Width / 2, Skillcon.Height / 10); // 图标中心
            }
            else
            {
                // 如果不在可见范围，就飞向面板右侧（暗示有更多内容）
                targetPos = DrawPosition + new Vector2(Size.X - 30, 30 + Skillcon.Height / 10);
            }
            
            // 创建粒子
            SkillIconEntity particle = new SkillIconEntity(fishSkill, startPosition, targetPos);
            flyingParticles.Add(particle);
            
            // 创建技能槽位，但标记为未激活状态
            SkillSlot newSlot = new SkillSlot() 
            { 
                FishSkill = fishSkill,
                appearProgress = 0f,
                isAppearing = false // 等待粒子到达后再开始出现动画
            };
            
            halibutUISkillSlots.Add(newSlot);
            
            // 记录这个槽位需要等待对应的粒子
            int particleIndex = flyingParticles.Count - 1;
            pendingSlots[newSlot] = particleIndex;
            
            // 播放音效
            SoundEngine.PlaySound(SoundID.Item4); // 魔法音效
        }
        
        /// <summary>
        /// 弹簧阻尼平滑函数
        /// </summary>
        private float SmoothDamp(float current, float target, ref float velocity, float deltaTime)
        {
            float delta = target - current;
            
            // 弹簧力
            float springForce = delta * ScrollStiffness;
            
            // 阻尼力
            float dampingForce = velocity * ScrollDamping;
            
            // 更新速度
            velocity += (springForce - dampingForce) * deltaTime;
            
            // 更新位置
            float newValue = current + velocity;
            
            // 如果非常接近目标，直接设置为目标
            if (Math.Abs(delta) < ScrollThreshold && Math.Abs(velocity) < ScrollThreshold)
            {
                velocity = 0;
                return target;
            }
            
            return newValue;
        }
        
        public override void Update() {
            halibutUISkillSlots ??= [];
            pendingSlots ??= [];
            
            // 确保滚动偏移量在有效范围内
            int maxOffset = Math.Max(0, halibutUISkillSlots.Count - maxVisibleSlots);
            scrollOffset = Math.Clamp(scrollOffset, 0, maxOffset);
            
            // 平滑滚动动画（弹簧阻尼效果）
            currentScrollOffset = SmoothDamp(currentScrollOffset, scrollOffset, ref scrollVelocity, 1f);
            
            // 更新飞行粒子，并检查是否有槽位需要激活
            for (int i = flyingParticles.Count - 1; i >= 0; i--)
            {
                if (flyingParticles[i].Update())
                {
                    // 粒子生命结束，播放到达音效
                    SoundEngine.PlaySound(SoundID.Grab with { Pitch = 0.5f, Volume = 0.5f });
                    
                    // 激活对应的槽位
                    foreach (var kvp in pendingSlots)
                    {
                        if (kvp.Value == i)
                        {
                            kvp.Key.isAppearing = true; // 开始播放出现动画
                            kvp.Key.appearProgress = 0f;
                            pendingSlots.Remove(kvp.Key);
                            break;
                        }
                    }
                    
                    flyingParticles.RemoveAt(i);
                    
                    // 更新剩余粒子的索引映射
                    Dictionary<SkillSlot, int> updatedPending = [];
                    foreach (var kvp in pendingSlots)
                    {
                        int newIndex = kvp.Value > i ? kvp.Value - 1 : kvp.Value;
                        updatedPending[kvp.Key] = newIndex;
                    }
                    pendingSlots = updatedPending;
                }
            }
            
            if (HalibutUILeftSidebar.Instance.Sengs >= 1f && HalibutUIHead.Instance.Open) {//侧边栏完全打开后才开始打开面板
                if (Sengs < 1f) {
                    Sengs += 0.1f;
                }
            }
            else {
                if (Sengs > 0f) {
                    Sengs -= 0.1f;
                }
            }

            Sengs = Math.Clamp(Sengs, 0f, 1f);

            Size = Panel.Size();
            int leftWeith = (int)(20 - 200 * (1f - Sengs));
            int topHeight = (int)Size.Y;
            if (HalibutUILeftSidebar.Instance.Sengs < 1f) {
                topHeight = (int)(Size.Y * HalibutUILeftSidebar.Instance.Sengs);
            }
            DrawPosition = new Vector2(leftWeith, Main.screenHeight - topHeight);
            UIHitBox = DrawPosition.GetRectangle(Size);
            hoverInMainPage = UIHitBox.Intersects(MouseHitBox);

            if (hoverInMainPage) {
                player.mouseInterface = true;
            }

            leftButton.DrawPosition = DrawPosition + new Vector2(16, 36);//硬编码调的位置偏移，别问为什么是这个值，问就是刚刚好
            leftButton.Update();
            rightButton.DrawPosition = DrawPosition + new Vector2(Size.X - 40, 36);//硬编码调的位置偏移，别问为什么是这个值，问就是刚刚好
            rightButton.Update();

            StudySlot.Instance.DrawPosition = DrawPosition + new Vector2(80, Size.Y / 2);
            StudySlot.Instance.Update();

            // 更新所有技能槽位（使用平滑的滚动偏移）
            float slotWidth = Skillcon.Width + 4;
            float baseX = 52;
            
            for (int i = 0; i < halibutUISkillSlots.Count; i++)
            {
                var slot = halibutUISkillSlots[i];
                
                // 计算每个槽位的目标位置（基于平滑的滚动偏移）
                float relativePosition = i - currentScrollOffset;
                float targetX = baseX + relativePosition * slotWidth;
                
                slot.DrawPosition = DrawPosition + new Vector2(targetX, 30);
                slot.RelativeIndex = relativePosition; // 用于判断是否在可见范围内
                slot.Update();
            }
        }
        
        public override void Draw(SpriteBatch spriteBatch) {
            var rasterizer = Main.Rasterizer;
            rasterizer.ScissorTestEnable = true;
            Main.instance.GraphicsDevice.RasterizerState.ScissorTestEnable = true;
            Rectangle clipping = new(20, 0, Main.screenWidth, Main.screenHeight);//别问我这个X=20是干什么的，问就是手调的，问就是刚刚好
            Main.instance.GraphicsDevice.ScissorRectangle = VaultUtils.GetClippingRectangle(spriteBatch, clipping);//这里进行必要的裁剪画布设置，避免绘制出不适合出现的部分
            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState
                    , DepthStencilState.None, rasterizer, null, Main.UIScaleMatrix);

            spriteBatch.Draw(Panel, UIHitBox, Color.White);

            leftButton.Draw(spriteBatch);
            rightButton.Draw(spriteBatch);

            StudySlot.Instance.Draw(spriteBatch);

            // 裁剪区域：只在面板内绘制技能图标
            Rectangle originalScissor = spriteBatch.GraphicsDevice.ScissorRectangle;
            Rectangle scissorRect = new Rectangle(
                (int)(DrawPosition.X + 40), // 左边界（留出按钮空间）
                (int)(DrawPosition.Y + 20), // 上边界
                (int)(Size.X - 80), // 宽度（减去两侧按钮）
                (int)(Size.Y - 40) // 高度
            );
            
            RasterizerState rasterizerState = new RasterizerState { ScissorTestEnable = true };
            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, 
                             DepthStencilState.None, rasterizerState, null, Main.UIScaleMatrix);
            spriteBatch.GraphicsDevice.ScissorRectangle = scissorRect;

            // 绘制所有技能槽位（带透明度渐变）
            for (int i = 0; i < halibutUISkillSlots.Count; i++)
            {
                var slot = halibutUISkillSlots[i];
                
                // 计算透明度：边缘的图标逐渐淡出
                float alpha = 1f;
                if (slot.RelativeIndex < 0)
                {
                    alpha = Math.Max(0, 1f + slot.RelativeIndex); // 左侧淡出
                }
                else if (slot.RelativeIndex > maxVisibleSlots - 1)
                {
                    alpha = Math.Max(0, maxVisibleSlots - slot.RelativeIndex); // 右侧淡出
                }
                
                slot.DrawAlpha = Math.Clamp(alpha, 0f, 1f);
                slot.Draw(spriteBatch);
            }

            // 恢复正常绘制
            spriteBatch.GraphicsDevice.ScissorRectangle = originalScissor;
            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState
                    , DepthStencilState.None, RasterizerState.CullNone, null, Main.UIScaleMatrix);
            
            // 绘制飞行粒子（在最上层）
            foreach (var particle in flyingParticles)
            {
                particle.Draw(spriteBatch);
            }

            rasterizer.ScissorTestEnable = false;
            Main.instance.GraphicsDevice.RasterizerState.ScissorTestEnable = false;//他妈的要恢复，不然UI就鸡巴全没了
            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState
                    , DepthStencilState.None, rasterizer, null, Main.UIScaleMatrix);
        }
    }

    internal class StudySlot : UIHandle
    {
        public static StudySlot Instance => UIHandleLoader.GetUIHandleOfType<StudySlot>();
        public override LayersModeEnum LayersMode => LayersModeEnum.None;//不被自动更新，需要手动调用Update和Draw
        public Item Item = new Item();

        //研究相关字段
        private int researchTimer = 0; //当前研究时间（帧数）
        private const int ResearchDuration = 7200; //研究总时长（2分钟 = 7200帧，60fps * 120秒）
        private bool isResearching = false; //是否正在研究

        public override void Update() {
            Item ??= new Item();
            Size = PictureSlot.Size();
            UIHitBox = DrawPosition.GetRectangle(Size);
            hoverInMainPage = UIHitBox.Intersects(MouseHitBox);

            if (hoverInMainPage) {
                if (keyLeftPressState == KeyPressState.Pressed) {
                    SoundEngine.PlaySound(SoundID.Grab);

                    //如果正在研究中，则取出物品并停止研究
                    if (isResearching && Item.Alives() && Item.type > ItemID.None) {
                        Main.mouseItem = Item.Clone();
                        Item.TurnToAir();
                        isResearching = false;
                        researchTimer = 0;
                    }
                    //否则放入新物品并开始研究
                    else if (Main.mouseItem.Alives() && Main.mouseItem.type > ItemID.None) {
                        Item = Main.mouseItem.Clone();
                        Main.mouseItem.TurnToAir();
                        isResearching = true;
                        researchTimer = 0;
                    }
                    //如果槽位有物品但鼠标没有，则取出
                    else if (Item.Alives() && Item.type > ItemID.None) {
                        Main.mouseItem = Item.Clone();
                        Item.TurnToAir();
                        isResearching = false;
                        researchTimer = 0;
                    }
                }
            }

            //更新研究进度
            if (isResearching && Item.Alives() && Item.type > ItemID.None) {
                researchTimer+=100;

                //研究完成
                if (researchTimer >= ResearchDuration) {
                    SoundEngine.PlaySound(SoundID.ResearchComplete);
                    isResearching = false;
                    researchTimer = 0;

                    if (FishSkill.UnlockFishs.TryGetValue(Item.type, out FishSkill fishSkill)) {
                        // 使用新的动画方法，从研究槽位中心飞出
                        Vector2 startPos = DrawPosition + new Vector2(26, 26); // 物品图标中心
                        HalibutUIPanel.Instance.AddSkillWithAnimation(fishSkill, startPos);
                    }

                    Item.TurnToAir();
                }
            }
        }

        [VaultLoaden(CWRConstant.UI + "Halibut/")]
        private static Texture2D PictureSlotMask = null;
        public override void Draw(SpriteBatch spriteBatch) {
            spriteBatch.Draw(PictureSlot, UIHitBox, Color.White);

            //计算研究进度
            float pct = isResearching && researchTimer > 0 ? researchTimer / (float)ResearchDuration : 0f;

            Vector2 barTopLeft = DrawPosition + new Vector2(8, 10);

            //绘制研究的物品
            if (Item.Alives() && Item.type > ItemID.None) {
                Color itemColor = Color.White;
                if (isResearching) {
                    //研究中的物品添加发光效果
                    float glow = (float)Math.Sin(Main.GlobalTimeWrappedHourly * 4f) * 0.2f + 0.8f;
                    itemColor = Color.White * glow;
                }
                VaultUtils.SimpleDrawItem(spriteBatch, Item.type, DrawPosition + new Vector2(26, 26), 40, 1f, 0, itemColor);

                //使用颜色渐变表示进度
                Color fillColor = Color.Lerp(Color.Peru, Color.Gold, pct);
                if (isResearching) {
                    //添加脉动效果
                    float pulse = (float)Math.Sin(Main.GlobalTimeWrappedHourly * 3f) * 0.1f + 0.9f;
                    fillColor *= pulse;
                }

                int recOffsetY = (int)(PictureSlotMask.Height * (1f - pct));
                Rectangle rectangle = new Rectangle(0, recOffsetY, PictureSlotMask.Width, PictureSlotMask.Height - recOffsetY);
                spriteBatch.Draw(PictureSlotMask, barTopLeft + new Vector2(0, recOffsetY), rectangle, fillColor, 0, Vector2.Zero, 1f, SpriteEffects.None, 0);
            }

            //绘制研究剩余时间文本
            if (isResearching && hoverInMainPage) {
                int remainingSeconds = (ResearchDuration - researchTimer) / 60;
                int minutes = remainingSeconds / 60;
                int seconds = remainingSeconds % 60;
                string timeText = $"{minutes:D2}:{seconds:D2}";

                Vector2 textPos = DrawPosition + new Vector2(30, -0);
                Vector2 textSize = FontAssets.MouseText.Value.MeasureString(timeText);
                Utils.DrawBorderString(spriteBatch, timeText, textPos - textSize / 2, Color.Gold, 0.8f);
            }

            //绘制进度百分比
            if (isResearching && pct > 0) {
                string percentText = $"{(int)(pct * 100)}%";
                Vector2 percentPos = DrawPosition + new Vector2(30, 65);
                Vector2 percentSize = FontAssets.MouseText.Value.MeasureString(percentText);
                Utils.DrawBorderString(spriteBatch, percentText, percentPos - percentSize / 2, Color.White, 0.7f);
            }
        }
    }

    internal class SkillSlot : UIHandle
    {
        public static SkillSlot Instance => UIHandleLoader.GetUIHandleOfType<SkillSlot>();
        public override LayersModeEnum LayersMode => LayersModeEnum.None;//不被自动更新，需要手动调用Update和Draw
        public FishSkill FishSkill;
        public float hoverSengs;
        public float RelativeIndex; // 相对于可见范围的位置
        public float DrawAlpha = 1f; // 绘制透明度
        
        // 出现动画相关
        public float appearProgress = 0f; // 出现进度（0到1）
        public bool isAppearing = false; // 是否正在播放出现动画
        private const float AppearDuration = 20f; // 出现动画持续帧数
        
        public override void Update() {
            Size = new Vector2(Skillcon.Width, Skillcon.Height / 5);
            UIHitBox = DrawPosition.GetRectangle((int)Size.X, (int)(Size.Y));
            
            // 更新出现动画
            if (isAppearing)
            {
                appearProgress += 1f / AppearDuration;
                if (appearProgress >= 1f)
                {
                    appearProgress = 1f;
                    isAppearing = false;
                }
            }
            
            // 只有完全出现后才响应交互
            hoverInMainPage = UIHitBox.Intersects(MouseHitBox) && DrawAlpha > 0.5f && appearProgress >= 1f;

            if (hoverInMainPage) {
                if (hoverSengs < 1f) {
                    hoverSengs += 0.1f;
                }
                if (keyLeftPressState == KeyPressState.Pressed) {
                    SoundEngine.PlaySound(SoundID.Grab);
                    HalibutUIHead.Instance.FishSkill = FishSkill;
                }
            }
            else {
                if (hoverSengs > 0f) {
                    hoverSengs -= 0.1f;
                }
            }
            
            hoverSengs = Math.Clamp(hoverSengs, 0f, 1f);
        }
        
        /// <summary>
        /// 缓动函数：EaseOutBack - 带有回弹效果的缓出
        /// </summary>
        private float EaseOutBack(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            return 1f + c3 * (float)Math.Pow(t - 1, 3) + c1 * (float)Math.Pow(t - 1, 2);
        }
        
        public override void Draw(SpriteBatch spriteBatch) {
            if (FishSkill == null) {
                return;
            }

            // 如果正在出现动画中，应用特殊效果
            float finalAlpha = DrawAlpha;
            float scale = 1f;
            float rotation = 0f;
            
            if (appearProgress < 1f)
            {
                // 使用EaseOutBack缓动，产生弹性效果
                float easedProgress = EaseOutBack(appearProgress);
                
                // 缩放从0.3开始，带有超调效果（可能超过1.0）
                scale = 0.3f + easedProgress * 0.7f;
                
                // 透明度渐入
                finalAlpha *= appearProgress;
                
                // 轻微的旋转效果
                rotation = (1f - appearProgress) * 0.5f;
            }
            
            Color baseColor = Color.White * finalAlpha;
            Color glowColor = Color.Gold with { A = 0 } * hoverSengs * finalAlpha;
            
            Vector2 center = DrawPosition + Size / 2;
            Vector2 origin = Size / 2;
            
            // 绘制悬停发光效果
            spriteBatch.Draw(FishSkill.Icon, center, null, glowColor, rotation, origin, scale * 1.2f, SpriteEffects.None, 0);
            
            // 绘制主图标
            spriteBatch.Draw(FishSkill.Icon, center, null, baseColor, rotation, origin, scale, SpriteEffects.None, 0);
            
            // 如果正在出现，绘制额外的光圈效果
            if (appearProgress < 1f && appearProgress > 0.2f)
            {
                float ringProgress = (appearProgress - 0.2f) / 0.8f;
                float ringScale = 0.5f + ringProgress * 1.5f;
                float ringAlpha = (1f - ringProgress) * 0.6f;
                Color ringColor = Color.Gold with { A = 0 } * ringAlpha * finalAlpha;
                
                spriteBatch.Draw(FishSkill.Icon, center, null, ringColor, rotation, origin, scale * ringScale, SpriteEffects.None, 0);
            }
        }
    }

    internal class LeftButtonUI : UIHandle
    {
        public static LeftButtonUI Instance => UIHandleLoader.GetUIHandleOfType<LeftButtonUI>();
        public override LayersModeEnum LayersMode => LayersModeEnum.None;//不被自动更新，需要手动调用Update和Draw
        public float hoverSengs;
        
        public override void Update() {
            Size = LeftButton.Size();
            UIHitBox = DrawPosition.GetRectangle(Size);
            hoverInMainPage = UIHitBox.Intersects(MouseHitBox);
            
            bool canScroll = HalibutUIPanel.Instance.scrollOffset > 0;
            
            if (hoverInMainPage && canScroll) {
                if (hoverSengs < 1f) {
                    hoverSengs += 0.15f;
                }
                if (keyLeftPressState == KeyPressState.Pressed) {
                    SoundEngine.PlaySound(CWRSound.ButtonZero with { Pitch = -0.2f });
                    // 向左滚动（减少偏移量）
                    HalibutUIPanel.Instance.scrollOffset--;
                }
            }
            else {
                if (hoverSengs > 0f) {
                    hoverSengs -= 0.1f;
                }
            }
            
            hoverSengs = Math.Clamp(hoverSengs, 0f, 1f);
        }

        public override void Draw(SpriteBatch spriteBatch) {
            // 根据是否能继续向左滚动来调整颜色
            bool canScroll = HalibutUIPanel.Instance.scrollOffset > 0;
            Color buttonColor = canScroll ? Color.White : Color.Gray * 0.5f;
            
            // 绘制发光效果
            if (canScroll)
            {
                spriteBatch.Draw(LeftButton, DrawPosition + Size / 2, null, Color.Gold with { A = 0 } * hoverSengs, 0, Size / 2, 1.2f, SpriteEffects.None, 0);
            }
            
            // 绘制按钮
            spriteBatch.Draw(LeftButton, DrawPosition, null, buttonColor);
        }
    }

    internal class RightButtonUI : UIHandle
    {
        public static RightButtonUI Instance => UIHandleLoader.GetUIHandleOfType<RightButtonUI>();
        public override LayersModeEnum LayersMode => LayersModeEnum.None;//不被自动更新，需要手动调用Update和Draw
        public float hoverSengs;
        
        public override void Update() {
            Size = RightButton.Size();
            UIHitBox = DrawPosition.GetRectangle(Size);
            hoverInMainPage = UIHitBox.Intersects(MouseHitBox);
            
            int maxOffset = Math.Max(0, HalibutUIPanel.Instance.halibutUISkillSlots.Count - HalibutUIPanel.maxVisibleSlots);
            bool canScroll = HalibutUIPanel.Instance.scrollOffset < maxOffset;
            
            if (hoverInMainPage && canScroll) {
                if (hoverSengs < 1f) {
                    hoverSengs += 0.15f;
                }
                if (keyLeftPressState == KeyPressState.Pressed) {
                    SoundEngine.PlaySound(CWRSound.ButtonZero with { Pitch = 0.2f });
                    // 向右滚动（增加偏移量）
                    HalibutUIPanel.Instance.scrollOffset++;
                }
            }
            else {
                if (hoverSengs > 0f) {
                    hoverSengs -= 0.1f;
                }
            }
            
            hoverSengs = Math.Clamp(hoverSengs, 0f, 1f);
        }

        public override void Draw(SpriteBatch spriteBatch) {
            // 根据是否能继续向右滚动来调整颜色
            int maxOffset = Math.Max(0, HalibutUIPanel.Instance.halibutUISkillSlots.Count - HalibutUIPanel.maxVisibleSlots);
            bool canScroll = HalibutUIPanel.Instance.scrollOffset < maxOffset;
            Color buttonColor = canScroll ? Color.White : Color.Gray * 0.5f;
            
            // 绘制发光效果
            if (canScroll)
            {
                spriteBatch.Draw(RightButton, DrawPosition + Size / 2, null, Color.Gold with { A = 0 } * hoverSengs, 0, Size / 2, 1.2f, SpriteEffects.None, 0);
            }
            
            // 绘制按钮
            spriteBatch.Draw(RightButton, DrawPosition, null, buttonColor);
        }
    }
}