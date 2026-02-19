using CalamityOverhaul.Common;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using static CalamityOverhaul.Content.LegendWeapon.HalibutLegend.UI.HalibutUIAsset;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.UI
{
    internal class HalibutUIHead : UIHandle
    {
        [VaultLoaden("@InnoVault/Effects/")]
        private static Asset<Effect> GearProgress { get; set; }
        public static HalibutUIHead Instance => UIHandleLoader.GetUIHandleOfType<HalibutUIHead>();
        private bool _active;
        public override bool Active {
            get {
                if (!Main.playerInventory || !_active) {
                    var item = player.GetItem();
                    _active = item.Alives() && item.type == HalibutOverride.ID;
                }
                return _active;
            }
        }
        public bool Open;
        public static ref FishSkill FishSkill => ref player.GetModPlayer<HalibutSave>().FishSkill;

        public static void SaveData(TagCompound tag) {
            HalibutUIPanel.Instance.SaveUIData(tag);
            DomainUI.Instance.SaveUIData(tag);
        }

        public static void LoadData(TagCompound tag) {
            HalibutUIPanel.Instance.LoadUIData(tag);
            DomainUI.Instance.LoadUIData(tag);
        }

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
            DomainUI.Instance.Update();
            ResurrectionUI.Instance.Update();//更新复苏条
            SkillLibraryUI.Instance.Update();//更新技能库
        }

        public override void LogicUpdate() {
            //技能快捷切换
            HandleSkillSwitching();
        }

        /// <summary>
        /// 处理技能快捷切换逻辑
        /// </summary>
        private static void HandleSkillSwitching() {
            if (!player.TryGetModPlayer<HalibutSave>(out var save)) {
                return;
            }

            if (save.halibutUISkillSlots.Count == 0) {
                return;
            }

            //获取当前技能索引
            int currentIndex = -1;
            if (FishSkill != null) {
                for (int i = 0; i < save.halibutUISkillSlots.Count; i++) {
                    if (save.halibutUISkillSlots[i].FishSkill == FishSkill) {
                        currentIndex = i;
                        break;
                    }
                }
            }

            bool switchLeft = CWRKeySystem.Halibut_Skill_L.JustPressed;
            bool switchRight = CWRKeySystem.Halibut_Skill_R.JustPressed;

            if (!switchLeft && !switchRight) {
                return;
            }

            //计算新索引
            int newIndex = currentIndex;
            if (switchLeft) {
                newIndex = currentIndex <= 0 ? save.halibutUISkillSlots.Count - 1 : currentIndex - 1;
            }
            else if (switchRight) {
                newIndex = currentIndex >= save.halibutUISkillSlots.Count - 1 ? 0 : currentIndex + 1;
            }

            //切换技能
            if (newIndex >= 0 && newIndex < save.halibutUISkillSlots.Count) {
                var newSkillSlot = save.halibutUISkillSlots[newIndex];
                if (newSkillSlot.FishSkill != null && newSkillSlot.FishSkill != FishSkill) {
                    FishSkill = newSkillSlot.FishSkill;

                    //触发切换动画
                    SkillRender.SwitchingSkill = FishSkill;
                    SkillRender.SwitchAnimProgress = 0f;
                    SkillRender.SwitchAnimTimer = 0;

                    //同步技能列表滚动位置
                    SyncSkillListScroll(newIndex);

                    //播放切换音效
                    SoundEngine.PlaySound(SoundID.MenuTick with { Pitch = 0.3f, Volume = 0.7f });
                }
            }
        }

        /// <summary>
        /// 同步技能列表滚动位置,确保选中的技能在可见范围内
        /// </summary>
        private static void SyncSkillListScroll(int targetIndex) {
            var panel = HalibutUIPanel.Instance;
            if (panel == null) {
                return;
            }

            int maxOffset = Math.Max(0, panel.halibutUISkillSlots.Count - HalibutUIPanel.maxVisibleSlots);
            int targetOffset = panel.scrollOffset;

            //如果目标索引在左侧不可见区域
            if (targetIndex < panel.scrollOffset) {
                targetOffset = targetIndex;
            }
            //如果目标索引在右侧不可见区域
            else if (targetIndex >= panel.scrollOffset + HalibutUIPanel.maxVisibleSlots) {
                targetOffset = targetIndex - HalibutUIPanel.maxVisibleSlots + 1;
            }

            //限制在有效范围内
            targetOffset = Math.Clamp(targetOffset, 0, maxOffset);

            //如果需要滚动
            if (targetOffset != panel.scrollOffset) {
                int scrollDelta = targetOffset - panel.scrollOffset;
                panel.QueueScroll(scrollDelta);
            }
        }

        public override void Draw(SpriteBatch spriteBatch) {
            //技能库在最下层（避免遮挡其他UI）
            SkillLibraryUI.Instance.Draw(spriteBatch);
            DomainUI.Instance.Draw(spriteBatch);
            HalibutUIPanel.Instance.Draw(spriteBatch);
            HalibutUILeftSidebar.Instance.Draw(spriteBatch);
            ResurrectionUI.Instance.Draw(spriteBatch);//绘制复苏条

            spriteBatch.Draw(Head, UIHitBox, Color.White);

            HalibutUILeftSidebar.Instance.PostDraw(spriteBatch);

            //绘制拖拽中的技能图标（在所有UI之上）
            HalibutUIPanel.Instance.DrawDraggingSlot(spriteBatch);
            SkillLibraryUI.Instance.DoDrawDraggingSlot(spriteBatch);

            if (FishSkill == null) {
                return;
            }

            GearProgress.Value.Parameters["Progress"].SetValue(1f - FishSkill.CooldownRatio);
            GearProgress.Value.Parameters["Rotation"].SetValue(-MathHelper.PiOver2);
            Main.spriteBatch.End();
            Main.spriteBatch.Begin(0, BlendState.AlphaBlend, null, null, null, GearProgress.Value, Main.UIScaleMatrix);
            spriteBatch.Draw(FishSkill.Icon, DrawPosition + new Vector2(28, 22), null, Color.White);//添加一个14的偏移量让这个技能图标刚好覆盖眼睛
            Main.spriteBatch.End();
            Main.spriteBatch.Begin(0, BlendState.AlphaBlend, null, null, null, null, Main.UIScaleMatrix);
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
            DrawPosition = new Vector2(0, Main.screenHeight - HalibutUIHead.Instance.Size.Y - 14 - topHeight);//14刚好是侧边栏顶部高礼帽的高度
            UIHitBox = DrawPosition.GetRectangle(Size);
            hoverInMainPage = UIHitBox.Intersects(MouseHitBox);
        }
        public override void Draw(SpriteBatch spriteBatch) {
            RasterizerState rasterizerState = new RasterizerState { ScissorTestEnable = true };
            RasterizerState normalState = new RasterizerState() { ScissorTestEnable = false };
            Rectangle viedutRect = new Rectangle(0, 0, Main.screenWidth, Main.screenHeight - 20);
            //进行矩形画布裁剪绘制
            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.PointClamp,
                             DepthStencilState.None, rasterizerState, null, Main.UIScaleMatrix);
            Rectangle originalScissor = spriteBatch.GraphicsDevice.ScissorRectangle;
            Rectangle newScissorRect = VaultUtils.GetClippingRectangle(spriteBatch, viedutRect);
            spriteBatch.GraphicsDevice.ScissorRectangle = newScissorRect;
            //他妈的我不知道为什么这里裁剪画布不生效，那么就这么将就着画吧，反正插穿鱼头也没人在意，就像他妈的澳大利亚人要打多少只袋鼠一样无聊
            spriteBatch.Draw(LeftSidebar, UIHitBox, Color.White);

            //恢复画布
            spriteBatch.GraphicsDevice.ScissorRectangle = originalScissor;
            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
                             DepthStencilState.None, normalState, null, Main.UIScaleMatrix);
        }
        public void PostDraw(SpriteBatch spriteBatch) {
            spriteBatch.Draw(Cap, DrawPosition + new Vector2(4, 0), null, Color.White, 0, Vector2.Zero, 1f, SpriteEffects.None, 0);
        }
    }

    internal class HalibutUIPanel : UIHandle
    {
        #region Data
        public static HalibutUIPanel Instance => UIHandleLoader.GetUIHandleOfType<HalibutUIPanel>();
        public override LayersModeEnum LayersMode => LayersModeEnum.None;//不被自动更新，需要手动调用Update和Draw
        public List<SkillSlot> halibutUISkillSlots => player.GetModPlayer<HalibutSave>().halibutUISkillSlots;
        public LeftButtonUI leftButton = new LeftButtonUI();
        public RightButtonUI rightButton = new RightButtonUI();
        public float Sengs;

        //滚动相关字段
        public int scrollOffset = 0;//目标滚动偏移量(当前段目标)
        public const int scrollStep = 3;//每次按钮指令的步长(逻辑步长, 将被拆分为逐槽动画)
        private float currentScrollOffset = 0f;//当前实际滚动偏移量（用于平滑动画）
        private float scrollVelocity = 0f;//滚动速度（用于弹簧效果）
        public const int maxVisibleSlots = 3;//最多同时显示3个技能槽位
        //分段滚动队列
        private int queuedScrollSteps = 0; //尚未执行的增量(正右负左)
        private bool segmentInProgress = false; //当前是否在执行单步滚动动画

        //动画参数
        private const float ScrollStiffness = 0.35f;//弹簧刚度(稍增, 单步更紧凑)
        private const float ScrollDamping = 0.72f;//阻尼系数(与刚度相协调)
        private const float ScrollThreshold = 0.015f;//停止阈值(更严格, 防止抖动)

        //粒子系统
        public List<SkillIconEntity> flyingParticles = [];

        //待激活的技能槽位（粒子到达后才激活）
        private Dictionary<SkillSlot, int> pendingSlots = [];//槽位 -> 对应的粒子索引

        /// <summary>
        /// 注册待激活的技能槽位，粒子到达后触发出现动画
        /// </summary>
        public void RegisterPendingSlot(SkillSlot slot, int particleIndex) {
            pendingSlots ??= [];
            pendingSlots[slot] = particleIndex;
        }

        private SkillSlot draggingSlot;//当前拖拽中的槽位
        /// <summary>
        /// 是否有槽位正在被拖拽
        /// </summary>
        public bool IsDragging => draggingSlot != null;
        private Vector2 dragOffset;//鼠标相对槽位中心偏移
        private float dragVisualX;//拖拽视觉X
        private int dragOriginalIndex = -1;//开始拖拽时原索引
        private int dragInsertIndex = -1;//实时插入索引
        private int dragHoldTimer = 0;//按住计时器
        private const int DragHoldDelay = 8;//按住多少帧后开始拖拽
        #endregion
        public static void FishSkillTooltip(Item item, List<TooltipLine> tooltips) {
            if (!Main.LocalPlayer.TryGetOverride<HalibutPlayer>(out var halibutPlayer) || !halibutPlayer.HasHalubut) {
                return;
            }
            if (!FishSkill.UnlockFishs.TryGetValue(item.type, out FishSkill fishSkill)) {
                return;
            }
            //水色渐变：更柔和且高对比度
            float ft = (Main.LocalPlayer.miscCounter % 120) / 120f;
            float wave = (float)Math.Sin(ft * MathHelper.TwoPi) * 0.5f + 0.5f;//0-1
            Color mainA = new Color(40, 140, 190);
            Color mainB = new Color(120, 230, 255);
            Color accent = Color.Lerp(mainA, mainB, wave);
            Color accent2 = Color.Lerp(mainA, mainB, 0.35f + wave * 0.3f);

            bool unlock = false;
            if (player.TryGetModPlayer<HalibutSave>(out var save)) {
                unlock = save.unlockSkills.Contains(fishSkill);
            }

            var line = new TooltipLine(CWRMod.Instance, "FishSkillTooltip"
                , unlock ? HalibutText.Instance.FishOnStudied.Value : HalibutText.Instance.FishByStudied.Value) {
                OverrideColor = accent
            };
            tooltips.Add(line);

            line = new TooltipLine(CWRMod.Instance, "FishSkillTooltip2", fishSkill.Studied.Value) {
                OverrideColor = accent2
            };
            tooltips.Add(line);
        }

        public static SkillSlot AddSkillSlot(FishSkill fishSkill, float appearProgress) {
            SkillSlot newSlot = new() {
                FishSkill = fishSkill,
                appearProgress = appearProgress,
                isAppearing = false
            };
            return newSlot;
        }

        /// <summary>
        /// 添加新技能并触发飞行动画
        /// </summary>
        public void AddSkillWithAnimation(FishSkill fishSkill, Vector2 startPosition) {
            //计算目标位置（列表中的位置）
            int futureIndex = halibutUISkillSlots.Count;
            int visibleIndex = futureIndex - scrollOffset;

            //如果新技能会在可见范围内，计算其目标位置
            Vector2 targetPos;
            if (visibleIndex >= 0 && visibleIndex < maxVisibleSlots) {
                targetPos = DrawPosition + new Vector2(52, 30);
                targetPos.X += visibleIndex * (Skillcon.Width + 4);
                targetPos += new Vector2(Skillcon.Width / 2, Skillcon.Height / 10);//图标中心
            }
            else {
                //如果不在可见范围，就飞往面板右侧（暗示有更多内容）
                targetPos = DrawPosition + new Vector2(Size.X - 30, 30 + Skillcon.Height / 10);
            }

            //创建粒子
            SkillIconEntity particle = new SkillIconEntity(fishSkill, startPosition, targetPos);
            flyingParticles.Add(particle);

            //创建技能槽位，但标记为未激活状态
            SkillSlot newSlot = AddSkillSlot(fishSkill, 0f);
            halibutUISkillSlots.Add(newSlot);
            if (player.Alives() && player.TryGetModPlayer<HalibutSave>(out var save)) {
                save.unlockSkills.Add(fishSkill);
            }

            //记录这个槽位需要等待对应的粒子
            int particleIndex = flyingParticles.Count - 1;
            pendingSlots[newSlot] = particleIndex;

            //播放音效
            SoundEngine.PlaySound(SoundID.Item4);//魔法音效
        }

        /// <summary>
        /// 弹簧阻尼平滑函数
        /// </summary>
        private static float SmoothDamp(float current, float target, ref float velocity, float deltaTime) {
            float delta = target - current;
            float springForce = delta * ScrollStiffness;//弹簧力
            float dampingForce = velocity * ScrollDamping;//阻尼力
            velocity += (springForce - dampingForce) * deltaTime;//更新速度
            float newValue = current + velocity;//更新位置
            if (Math.Abs(delta) < ScrollThreshold && Math.Abs(velocity) < ScrollThreshold) {
                velocity = 0;
                return target;
            }
            return newValue;
        }

        /// <summary>
        /// 请求滚动指定步数(可为正/负, 将被拆分为单步动画)
        /// </summary>
        /// <param name="steps"></param>
        public void QueueScroll(int steps) {
            if (steps == 0) return;
            queuedScrollSteps += steps;
            //限制队列不要溢出到无效区域
            int maxOffset = Math.Max(0, halibutUISkillSlots.Count - maxVisibleSlots);
            int projected = scrollOffset + queuedScrollSteps;
            if (projected < 0) queuedScrollSteps -= projected; //剪裁左侧
            if (projected > maxOffset) queuedScrollSteps -= (projected - maxOffset); //剪裁右侧
        }

        //拆分执行一个单步，如果存在队列
        private void TryStartNextScrollSegment() {
            if (segmentInProgress) return;
            if (queuedScrollSteps == 0) return;
            int dir = Math.Sign(queuedScrollSteps);
            int maxOffset = Math.Max(0, halibutUISkillSlots.Count - maxVisibleSlots);
            int newTarget = Math.Clamp(scrollOffset + dir, 0, maxOffset);
            if (newTarget == scrollOffset) { //无法再滚动, 丢弃该方向剩余队列
                queuedScrollSteps = 0;
                return;
            }
            scrollOffset = newTarget; //设定新的段目标
            queuedScrollSteps -= dir; //消耗一格
            segmentInProgress = true;
            //段开始音效(轻提示)
            SoundEngine.PlaySound(SoundID.MenuTick with { Volume = 0.4f, Pitch = dir > 0 ? 0.15f : -0.15f });
        }

        private void RollerUpdate() {
            if (!hoverInMainPage) {
                return;
            }
            player.CWR().DontSwitchWeaponTime = 5;//阻止切换武器
            if (!SkillAreaHover()) {
                return;
            }
            int delta = PlayerInput.ScrollWheelDeltaForUI;//使用tModLoader提供的UI滚轮增量
            if (delta == 0) {
                return;
            }
            int steps = delta > 0 ? 1 : -1;//这里固定每次滚动一个单位，避免有些人的鼠标滚轮设置太过灵敏
            int old = scrollOffset;
            scrollOffset -= steps;//滚轮上推向左, 下拉向右
            int maxOff = Math.Max(0, halibutUISkillSlots.Count - maxVisibleSlots);
            if (scrollOffset < 0) {
                scrollOffset = 0;
            }
            if (scrollOffset > maxOff) {
                scrollOffset = maxOff;
            }
            if (scrollOffset != old) {
                float pitch = steps > 0 ? 0.15f : -0.15f;//方向性提示
                SoundEngine.PlaySound(SoundID.MenuTick with { Pitch = pitch });
            }
        }

        public override void Update() {
            pendingSlots ??= [];

            //确保滚动偏移量在有效范围内(段目标)
            int maxOffset = Math.Max(0, halibutUISkillSlots.Count - maxVisibleSlots);
            scrollOffset = Math.Clamp(scrollOffset, 0, maxOffset);

            //若当前段动画接近完成则允许启动下一段
            if (segmentInProgress) {
                if (Math.Abs(currentScrollOffset - scrollOffset) < 0.05f && Math.Abs(scrollVelocity) < 0.02f) {
                    segmentInProgress = false;
                }
            }
            //尝试开启下一段
            TryStartNextScrollSegment();

            //平滑滚动动画（弹簧阻尼效果） toward scrollOffset
            currentScrollOffset = SmoothDamp(currentScrollOffset, scrollOffset, ref scrollVelocity, 1f);

            //更新飞行粒子，并检查是否有槽位需要激活
            for (int i = flyingParticles.Count - 1; i >= 0; i--) {
                if (flyingParticles[i].Update()) {
                    SoundEngine.PlaySound(SoundID.Grab with { Pitch = 0.5f, Volume = 0.5f });//粒子到达音效
                    foreach (var kvp in pendingSlots) {
                        if (kvp.Value == i) {
                            kvp.Key.isAppearing = true;//开始播放出现动画
                            kvp.Key.appearProgress = 0f;
                            pendingSlots.Remove(kvp.Key);
                            break;
                        }
                    }
                    flyingParticles.RemoveAt(i);
                    Dictionary<SkillSlot, int> updatedPending = [];//更新剩余粒子的索引映射
                    foreach (var kvp in pendingSlots) {
                        int newIndex = kvp.Value > i ? kvp.Value - 1 : kvp.Value;
                        updatedPending[kvp.Key] = newIndex;
                    }
                    pendingSlots = updatedPending;
                }
            }

            //面板展开/收起逻辑（协调介绍面板）
            if (HalibutUILeftSidebar.Instance.Sengs >= 1f && HalibutUIHead.Instance.Open) {
                if (Sengs < 1f) {
                    Sengs += 0.1f;//侧边栏完全打开后才开始打开面板
                }
            }
            else {
                if (Sengs > 0f) {
                    if (SkillTooltipPanel.Instance.IsShowing) {
                        SkillTooltipPanel.Instance.ForceHide();//如果介绍面板正在显示，先强制隐藏它
                    }
                    if (SkillTooltipPanel.Instance.IsFullyClosed) {
                        Sengs -= 0.1f;//等待介绍面板完全收起后，主面板才开始收起
                    }
                }
            }

            Sengs = Math.Clamp(Sengs, 0f, 1f);

            //面板关闭时兜底清除悬停状态
            if (Sengs <= 0f) {
                SkillSlot.ClearHoveredState();
            }

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

            //滚轮横向滚动逻辑
            RollerUpdate();

            leftButton.DrawPosition = DrawPosition + new Vector2(16, 36);//硬编码调的位置偏移，别问为什么是这个值，问就是刚刚好
            leftButton.Update();
            rightButton.DrawPosition = DrawPosition + new Vector2(Size.X - 40, 36);//硬编码调的位置偏移，别问为什么是这个值，问就是刚刚好
            rightButton.Update();

            StudySlot.Instance.DrawPosition = DrawPosition + new Vector2(80, Size.Y / 2);
            StudySlot.Instance.Update();

            float slotWidth = Skillcon.Width + 4;//更新所有技能槽位（使用平滑的滚动偏移）
            float baseX = 52;
            bool anySlotHovered = false;//检查是否有任何技能槽位被悬停
            //拖拽起始检测
            if (draggingSlot == null && Main.mouseLeft) {
                dragHoldTimer++;
            }
            else if (!Main.mouseLeft) {
                dragHoldTimer = 0;
            }
            for (int i = 0; i < halibutUISkillSlots.Count; i++) {
                var slot = halibutUISkillSlots[i];
                float relativePosition = i - currentScrollOffset;//计算每个槽位的目标位置（基于平滑的滚动偏移）
                float targetX = baseX + relativePosition * slotWidth;
                Vector2 slotPos = DrawPosition + new Vector2(targetX, 30);
                if (draggingSlot == null || slot != draggingSlot) {
                    //非拖拽中的槽位做平滑过渡
                    slot.DrawPosition = Vector2.Lerp(slot.DrawPosition, slotPos, 0.4f);
                }
                slot.RelativeIndex = relativePosition;//用于判断是否在可见范围内
                slot.Update();
                if (slot.hoverInMainPage && draggingSlot == null && Main.mouseLeft && dragHoldTimer >= DragHoldDelay && !SkillLibraryUI.Instance.IsDragging) {
                    draggingSlot = slot;
                    dragOriginalIndex = i;
                    dragOffset = Main.MouseScreen - slot.DrawPosition;
                    dragVisualX = slot.DrawPosition.X;
                    slot.beingDragged = true;
                    SoundEngine.PlaySound(SoundID.Grab with { Pitch = 0.25f });
                }
                if (slot.hoverInMainPage) {
                    anySlotHovered = true;
                }
            }
            //兜底：如果HoveredSlot引用了不在主面板列表中的槽位，强制清除
            if (SkillSlot.HoveredSlot != null && !halibutUISkillSlots.Contains(SkillSlot.HoveredSlot)) {
                SkillSlot.ClearHoveredState();
            }
            if (!anySlotHovered) {
                SkillTooltipPanel.Instance.Hide();//如果没有槽位被悬停，隐藏介绍面板（带延迟）
            }
            SkillTooltipPanel.Instance.Update();//更新介绍面板
            if (draggingSlot != null) {
                //更新拖拽位置
                Vector2 mouse = Main.MouseScreen - dragOffset;
                dragVisualX = MathHelper.Lerp(dragVisualX, mouse.X, 0.5f);
                draggingSlot.DrawPosition = new Vector2(dragVisualX, MathHelper.Lerp(draggingSlot.DrawPosition.Y, mouse.Y, 0.5f));

                //拖拽时清除悬停状态，防止提示窗口残留
                SkillSlot.ClearHoveredState();
                SkillTooltipPanel.Instance.ForceHide();

                //检测是否悬停在技能库区域，设置高亮
                bool hoveringLibrary = SkillLibraryUI.Instance.Sengs > 0.5f &&
                    SkillLibraryUI.Instance.UIHitBox.Contains(MouseHitBox);
                SkillLibraryUI.Instance.IsDragHighlighted = hoveringLibrary;

                //计算插入索引（依据拖拽中心X）
                float centerX = draggingSlot.DrawPosition.X + draggingSlot.Size.X / 2 - (DrawPosition.X + baseX);
                float logicalIndexF = centerX / slotWidth + currentScrollOffset;
                int logicalIndex = (int)Math.Round(logicalIndexF);
                logicalIndex = Math.Clamp(logicalIndex, 0, halibutUISkillSlots.Count - 1);
                dragInsertIndex = logicalIndex;
                if (logicalIndex != dragOriginalIndex && !hoveringLibrary) {
                    //为其他槽位腾出空间动画（仅在不悬停技能库时）
                    for (int i = 0; i < halibutUISkillSlots.Count; i++) {
                        var slot = halibutUISkillSlots[i];
                        if (slot == draggingSlot) {
                            continue;
                        }
                        int targetIndex = i;
                        if (dragOriginalIndex < logicalIndex) {
                            if (i > dragOriginalIndex && i <= logicalIndex) {
                                targetIndex = i - 1;
                            }
                        }
                        else if (dragOriginalIndex > logicalIndex) {
                            if (i >= logicalIndex && i < dragOriginalIndex) {
                                targetIndex = i + 1;
                            }
                        }
                        float rel = targetIndex - currentScrollOffset;
                        float tx = baseX + rel * slotWidth;
                        Vector2 newPos = DrawPosition + new Vector2(tx, 30);
                        slot.DrawPosition = Vector2.Lerp(slot.DrawPosition, newPos, 0.35f);
                    }
                }
                if (!Main.mouseLeftRelease) {
                    //保持拖拽
                }
                else {
                    //释放
                    draggingSlot.beingDragged = false;

                    //检测是否释放到技能库区域
                    if (hoveringLibrary) {
                        //移动到技能库（带动画）
                        Vector2 startPos = draggingSlot.DrawPosition + draggingSlot.Size / 2;
                        SkillLibraryUI.Instance.MoveToLibraryWithAnimation(draggingSlot, startPos);
                    }
                    else if (dragInsertIndex != dragOriginalIndex && dragInsertIndex >= 0 && hoverInMainPage) {
                        //在主面板内重新排序
                        halibutUISkillSlots.Remove(draggingSlot);
                        halibutUISkillSlots.Insert(dragInsertIndex, draggingSlot);
                        SoundEngine.PlaySound(SoundID.MenuTick with { Pitch = 0.4f });
                    }
                    else if (!hoverInMainPage && !hoveringLibrary) {
                        //释放到UI外，返回原位（槽位会自动插值回去）
                        SoundEngine.PlaySound(SoundID.MenuClose with { Volume = 0.3f, Pitch = 0.2f });
                    }

                    draggingSlot = null;
                    dragOriginalIndex = -1;
                    dragInsertIndex = -1;
                    dragHoldTimer = 0;
                }
            }
        }

        private bool SkillAreaHover() {
            if (Sengs <= 0f) {
                return false;
            }
            Rectangle area = new Rectangle(
                (int)(DrawPosition.X + 40),
                (int)(DrawPosition.Y + 20),
                (int)(Size.X - 80),
                46
            );
            return area.Contains(MouseHitBox);
        }

        public void MoveSlotToFront(SkillSlot slot) {
            if (slot == null) {
                return;
            }
            int idx = halibutUISkillSlots.IndexOf(slot);
            if (idx <= 0) {
                return;
            }
            halibutUISkillSlots.RemoveAt(idx);
            halibutUISkillSlots.Insert(0, slot);
            //重新设置出现动画：被移动的放大闪动（动画期间不响应悬停）
            slot.appearProgress = 0f;
            slot.isAppearing = true;
            SkillSlot.ClearHoveredState();
            //为了视觉平滑, 将滚动偏移重置到0并快速过渡
            scrollOffset = 0;
            //辅以轻微提示音
            SoundEngine.PlaySound(SoundID.MenuTick with { Pitch = 0.3f });
        }

        /// <summary>
        /// 绘制拖拽中的技能槽位（在所有UI之上）
        /// </summary>
        public void DrawDraggingSlot(SpriteBatch spriteBatch) {
            if (draggingSlot?.FishSkill?.Icon == null) {
                return;
            }

            Vector2 center = draggingSlot.DrawPosition + draggingSlot.Size / 2;
            Vector2 origin = draggingSlot.Size / 2;

            //发光效果
            Color glowColor = Color.Gold with { A = 0 } * 0.7f;
            spriteBatch.Draw(draggingSlot.FishSkill.Icon, center, null, glowColor, 0f, origin, 1.35f, SpriteEffects.None, 0);

            //主图标
            spriteBatch.Draw(draggingSlot.FishSkill.Icon, center, null, Color.White, 0f, origin, 1.15f, SpriteEffects.None, 0);
        }

        public override void Draw(SpriteBatch spriteBatch) {
            RasterizerState rasterizerState = new RasterizerState { ScissorTestEnable = true };
            RasterizerState normalState = new RasterizerState() { ScissorTestEnable = false };
            Rectangle originalScissor = spriteBatch.GraphicsDevice.ScissorRectangle;//裁剪区域：只在面板内绘制技能图标
            Rectangle clipping = new(20, 0, Main.screenWidth, Main.screenHeight - 20);//别问我这个X=20是干什么的，问就是手调的，问就是刚刚好

            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, Main.DefaultSamplerState
                    , DepthStencilState.None, rasterizerState, null, Main.UIScaleMatrix);
            spriteBatch.GraphicsDevice.ScissorRectangle = VaultUtils.GetClippingRectangle(spriteBatch, clipping);//这里进行必要的裁剪画布设置，避免绘制出不适合出现的部分

            SkillTooltipPanel.Instance.Draw(spriteBatch);//先绘制介绍面板（在主面板后面）
            spriteBatch.Draw(Panel, UIHitBox, Color.White);//绘制主面板
            leftButton.Draw(spriteBatch);
            rightButton.Draw(spriteBatch);
            StudySlot.Instance.Draw(spriteBatch);

            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.PointClamp,
                             DepthStencilState.None, rasterizerState, null, Main.UIScaleMatrix);
            Rectangle scissorRect = new Rectangle(
                20,
                (int)(DrawPosition.Y + 20),
                (int)(Size.X),
                (int)(Size.Y - 40)
            );
            spriteBatch.GraphicsDevice.ScissorRectangle = VaultUtils.GetClippingRectangle(spriteBatch, scissorRect);
            for (int i = 0; i < halibutUISkillSlots.Count; i++) {
                var slot = halibutUISkillSlots[i];
                //跳过拖拽中的槽位（单独绘制在最上层）
                if (slot == draggingSlot) {
                    continue;
                }
                float alpha = 1f;//计算透明度：边缘的图标逐渐淡出
                if (slot.RelativeIndex < 0) {
                    alpha = Math.Max(0, 1f + slot.RelativeIndex);//左侧淡出
                }
                else if (slot.RelativeIndex > maxVisibleSlots - 1) {
                    alpha = Math.Max(0, maxVisibleSlots - slot.RelativeIndex);//右侧淡出
                }
                slot.DrawAlpha = Math.Clamp(alpha, 0f, 1f);
                slot.Draw(spriteBatch);
            }

            spriteBatch.GraphicsDevice.ScissorRectangle = originalScissor;//他妈的要恢复，不然UI就鸡巴全没了
            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState
                    , DepthStencilState.None, normalState, null, Main.UIScaleMatrix);

            foreach (var particle in flyingParticles) {
                particle.Draw(spriteBatch);//绘制飞行粒子（在最上层）
            }
            //绘制快捷提示(在粒子之后保证可见)
            SkillSlot.HoveredSlot?.DrawHint(spriteBatch);
        }
    }
}