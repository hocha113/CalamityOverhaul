using CalamityOverhaul.Common;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using static CalamityOverhaul.Content.LegendWeapon.HalibutLegend.UI.HalibutUIAsset;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.UI
{
    [VaultLoaden(CWRConstant.UI + "Halibut/")]//用反射标签加载对应文件夹下的所有资源
    internal static class HalibutUIAsset
    {
        //奈落之眼纹理，共两帧动画，第一帧是闭眼，第二帧是睁眼，单帧大小40(宽)*26(高)
        public static Texture2D SeaEye = null;
        //按钮，大小46*26
        public static Texture2D Button = null;
        //大比目鱼的头像图标，放置在屏幕左下角作为UI的入口，大小74*74
        public static Texture2D Head = null;
        //左侧边栏，大小218*42
        public static Texture2D LeftSidebar = null;
        //面板，大小242*214
        public static Texture2D Panel = null;
        //提示面板，大小214*206
        public static Texture2D TooltipPanel = null;
        //图标栏，大小60*52
        public static Texture2D PictureSlot = null;
        //技能图标，大小170*34，共五帧，对应五种技能的图标
        public static Texture2D Skillcon = null;
        //左侧方向按钮
        public static Texture2D LeftButton = null;
        //右侧方向按钮
        public static Texture2D RightButton = null;
        //下划线花边纹理
        public static Texture2D TooltiplineBorder = null;
    }

    internal class HalibutUIHead : UIHandle
    {
        [VaultLoaden("@InnoVault/Effects/")]
        private static Asset<Effect> GearProgress { get; set; }
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
        public static ref FishSkill FishSkill => ref player.GetModPlayer<HalibutUISave>().FishSkill;

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
            DomainUI.Instance.Update(); // 更新领域UI

            //反正这样加载是没问题的，你就看跑不跑得起来吧！
            if (FishSkill != null && player.TryGetOverride<HalibutPlayer>(out var halibutPlayer)) {
                halibutPlayer.SkillID = FishSkill.ID;
            }
        }

        public override void Draw(SpriteBatch spriteBatch) {
            DomainUI.Instance.Draw(spriteBatch);
            HalibutUIPanel.Instance.Draw(spriteBatch);
            HalibutUILeftSidebar.Instance.Draw(spriteBatch);

            spriteBatch.Draw(Head, UIHitBox, Color.White);

            if (FishSkill == null) {
                return;
            }

            GearProgress.Value.Parameters["Progress"].SetValue(1f - FishSkill.CooldownRatio);
            GearProgress.Value.Parameters["Rotation"].SetValue(-MathHelper.PiOver2);
            Main.spriteBatch.End();
            Main.spriteBatch.Begin(0, BlendState.AlphaBlend, null, null, null, GearProgress.Value, Main.UIScaleMatrix);
            spriteBatch.Draw(FishSkill.Icon, DrawPosition + new Vector2(14), null, Color.White);//添加一个14的偏移量让这个技能图标刚好覆盖眼睛
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
        #region Data
        public static HalibutUIPanel Instance => UIHandleLoader.GetUIHandleOfType<HalibutUIPanel>();
        public override LayersModeEnum LayersMode => LayersModeEnum.None;//不被自动更新，需要手动调用Update和Draw
        public List<SkillSlot> halibutUISkillSlots => player.GetModPlayer<HalibutUISave>().halibutUISkillSlots;
        public LeftButtonUI leftButton = new LeftButtonUI();
        public RightButtonUI rightButton = new RightButtonUI();
        public float Sengs;

        //滚动相关字段
        public int scrollOffset = 0; //目标滚动偏移量
        private float currentScrollOffset = 0f; //当前实际滚动偏移量（用于平滑动画）
        private float scrollVelocity = 0f; //滚动速度（用于弹簧效果）
        public const int maxVisibleSlots = 3; //最多同时显示3个技能槽位

        //动画参数
        private const float ScrollStiffness = 0.3f; //弹簧刚度
        private const float ScrollDamping = 0.7f; //阻尼系数
        private const float ScrollThreshold = 0.01f; //停止阈值

        //粒子系统
        public List<SkillIconEntity> flyingParticles = [];

        //待激活的技能槽位（粒子到达后才激活）
        private Dictionary<SkillSlot, int> pendingSlots = []; //槽位 -> 对应的粒子索引
        #endregion
        public static void FishSkillTooltip(Item item, List<TooltipLine> tooltips) {
            if (!Main.LocalPlayer.TryGetOverride<HalibutPlayer>(out var halibutPlayer) || !halibutPlayer.HasHalibut) {
                return;
            }
            if (!FishSkill.UnlockFishs.TryGetValue(item.type, out FishSkill fishSkill)) {
                return;
            }
            //水色渐变：更柔和且高对比度
            float ft = (Main.LocalPlayer.miscCounter % 120) / 120f;
            float wave = (float)Math.Sin(ft * MathHelper.TwoPi) * 0.5f + 0.5f; //0-1
            Color mainA = new Color(40, 140, 190);
            Color mainB = new Color(120, 230, 255);
            Color accent = Color.Lerp(mainA, mainB, wave);
            Color accent2 = Color.Lerp(mainA, mainB, 0.35f + wave * 0.3f);

            var line = new TooltipLine(CWRMod.Instance, "FishSkillTooltip"
                , HalibutPlayer.UnlockedSkills.Contains(fishSkill) ? HalibutText.Instance.FishOnStudied.Value : HalibutText.Instance.FishByStudied.Value) {
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
            HalibutPlayer.UnlockedSkills.Add(fishSkill);
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
                targetPos += new Vector2(Skillcon.Width / 2, Skillcon.Height / 10); //图标中心
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

            //记录这个槽位需要等待对应的粒子
            int particleIndex = flyingParticles.Count - 1;
            pendingSlots[newSlot] = particleIndex;

            //播放音效
            SoundEngine.PlaySound(SoundID.Item4); //魔法音效
        }

        /// <summary>
        /// 弹簧阻尼平滑函数
        /// </summary>
        private static float SmoothDamp(float current, float target, ref float velocity, float deltaTime) {
            float delta = target - current;

            //弹簧力
            float springForce = delta * ScrollStiffness;

            //阻尼力
            float dampingForce = velocity * ScrollDamping;

            //更新速度
            velocity += (springForce - dampingForce) * deltaTime;

            //更新位置
            float newValue = current + velocity;

            //如果非常接近目标，直接设置为目标
            if (Math.Abs(delta) < ScrollThreshold && Math.Abs(velocity) < ScrollThreshold) {
                velocity = 0;
                return target;
            }

            return newValue;
        }

        public override void Update() {
            pendingSlots ??= [];

            //确保滚动偏移量在有效范围内
            int maxOffset = Math.Max(0, halibutUISkillSlots.Count - maxVisibleSlots);
            scrollOffset = Math.Clamp(scrollOffset, 0, maxOffset);

            //平滑滚动动画（弹簧阻尼效果）
            currentScrollOffset = SmoothDamp(currentScrollOffset, scrollOffset, ref scrollVelocity, 1f);

            //更新飞行粒子，并检查是否有槽位需要激活
            for (int i = flyingParticles.Count - 1; i >= 0; i--) {
                if (flyingParticles[i].Update()) {
                    //粒子生命结束，播放到达音效
                    SoundEngine.PlaySound(SoundID.Grab with { Pitch = 0.5f, Volume = 0.5f });

                    //激活对应的槽位
                    foreach (var kvp in pendingSlots) {
                        if (kvp.Value == i) {
                            kvp.Key.isAppearing = true; //开始播放出现动画
                            kvp.Key.appearProgress = 0f;
                            pendingSlots.Remove(kvp.Key);
                            break;
                        }
                    }

                    flyingParticles.RemoveAt(i);

                    //更新剩余粒子的索引映射
                    Dictionary<SkillSlot, int> updatedPending = [];
                    foreach (var kvp in pendingSlots) {
                        int newIndex = kvp.Value > i ? kvp.Value - 1 : kvp.Value;
                        updatedPending[kvp.Key] = newIndex;
                    }
                    pendingSlots = updatedPending;
                }
            }

            //面板展开/收起逻辑（协调介绍面板）
            if (HalibutUILeftSidebar.Instance.Sengs >= 1f && HalibutUIHead.Instance.Open) {
                //侧边栏完全打开后才开始打开面板
                if (Sengs < 1f) {
                    Sengs += 0.1f;
                }
            }
            else {
                //准备收起面板
                if (Sengs > 0f) {
                    //如果介绍面板正在显示，先强制隐藏它
                    if (SkillTooltipPanel.Instance.IsShowing) {
                        SkillTooltipPanel.Instance.ForceHide();
                    }

                    //等待介绍面板完全收起后，主面板才开始收起
                    if (SkillTooltipPanel.Instance.IsFullyClosed) {
                        Sengs -= 0.1f;
                    }
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

            //更新所有技能槽位（使用平滑的滚动偏移）
            float slotWidth = Skillcon.Width + 4;
            float baseX = 52;

            //检查是否有任何技能槽位被悬停
            bool anySlotHovered = false;

            for (int i = 0; i < halibutUISkillSlots.Count; i++) {
                var slot = halibutUISkillSlots[i];

                //计算每个槽位的目标位置（基于平滑的滚动偏移）
                float relativePosition = i - currentScrollOffset;
                float targetX = baseX + relativePosition * slotWidth;

                slot.DrawPosition = DrawPosition + new Vector2(targetX, 30);
                slot.RelativeIndex = relativePosition; //用于判断是否在可见范围内
                slot.Update();

                if (slot.hoverInMainPage) {
                    anySlotHovered = true;
                }
            }

            //如果没有槽位被悬停，隐藏介绍面板（带延迟）
            if (!anySlotHovered) {
                SkillTooltipPanel.Instance.Hide();
            }

            //更新介绍面板
            SkillTooltipPanel.Instance.Update();
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

            //先绘制介绍面板（在主面板后面）
            SkillTooltipPanel.Instance.Draw(spriteBatch);

            //绘制主面板
            spriteBatch.Draw(Panel, UIHitBox, Color.White);

            leftButton.Draw(spriteBatch);
            rightButton.Draw(spriteBatch);

            //绘制下划线花边，效果一般，考虑删
            //spriteBatch.Draw(TooltiplineBorder, DrawPosition + new Vector2(20, 58), null, Color.White, 0, Vector2.Zero, 1f, SpriteEffects.None, 0);

            StudySlot.Instance.Draw(spriteBatch);

            //裁剪区域：只在面板内绘制技能图标
            Rectangle originalScissor = spriteBatch.GraphicsDevice.ScissorRectangle;
            Rectangle scissorRect = new Rectangle(
                (int)(DrawPosition.X + 40), //左边界（留出按钮空间）
                (int)(DrawPosition.Y + 20), //上边界
                (int)(Size.X - 80), //宽度（减去两侧按钮）
                (int)(Size.Y - 40) //高度
            );

            RasterizerState rasterizerState = new RasterizerState { ScissorTestEnable = true };
            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
                             DepthStencilState.None, rasterizerState, null, Main.UIScaleMatrix);
            spriteBatch.GraphicsDevice.ScissorRectangle = scissorRect;

            //绘制所有技能槽位（带透明度渐变）
            for (int i = 0; i < halibutUISkillSlots.Count; i++) {
                var slot = halibutUISkillSlots[i];

                //计算透明度：边缘的图标逐渐淡出
                float alpha = 1f;
                if (slot.RelativeIndex < 0) {
                    alpha = Math.Max(0, 1f + slot.RelativeIndex); //左侧淡出
                }
                else if (slot.RelativeIndex > maxVisibleSlots - 1) {
                    alpha = Math.Max(0, maxVisibleSlots - slot.RelativeIndex); //右侧淡出
                }

                slot.DrawAlpha = Math.Clamp(alpha, 0f, 1f);
                slot.Draw(spriteBatch);
            }

            //恢复裁剪矩形
            spriteBatch.GraphicsDevice.ScissorRectangle = originalScissor;
            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState
                    , DepthStencilState.None, rasterizer, null, Main.UIScaleMatrix);

            //绘制飞行粒子（在最上层）
            foreach (var particle in flyingParticles) {
                particle.Draw(spriteBatch);
            }

            //恢复RasterizerState
            rasterizer.ScissorTestEnable = false;
            Main.instance.GraphicsDevice.RasterizerState.ScissorTestEnable = false;//他妈的要恢复，不然UI就鸡巴全没了
            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState
                    , DepthStencilState.None, rasterizer, null, Main.UIScaleMatrix);
        }
    }
}