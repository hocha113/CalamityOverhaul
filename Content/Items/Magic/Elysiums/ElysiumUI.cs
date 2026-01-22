using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using ReLogic.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.UI.Chat;

namespace CalamityOverhaul.Content.Items.Magic.Elysiums
{
    /// <summary>
    /// 天国极乐门徒UI
    /// 圆形转盘式布局，宗教玫瑰窗风格
    /// </summary>
    internal class ElysiumUI : UIHandle, ILocalizedModType
    {
        #region 常量与配置

        //UI尺寸
        private const float OuterRadius = 200f;      //外环半径
        private const float InnerRadius = 60f;       //内环半径
        private const float DiscipleRadius = 140f;   //门徒环半径
        private const float DiscipleSlotSize = 44f;  //门徒槽位大小

        //拉丁文铭文(12门徒对应)
        private static readonly string[] LatinNames = [
            "PETRUS",       //彼得
            "ANDREAS",      //安德鲁
            "IACOBUS",      //雅各布
            "IOANNES",      //约翰
            "PHILIPPUS",    //腓力
            "BARTHOLOMAEUS",//巴多罗买
            "THOMAS",       //多马
            "MATTHAEUS",    //马太
            "IACOBUS·MIN",  //小雅各
            "THADDAEUS",    //达太
            "SIMON",        //西门
            "IUDAS"         //犹大
        ];

        //门徒能力简述
        private static readonly string[] AbilityBriefs = [
            "磐石之盾",
            "渔网束缚",
            "雷霆审判",
            "启示视野",
            "圣光引导",
            "真言揭示",
            "怀疑之触",
            "财富祝福",
            "奉献治愈",
            "奇迹显现",
            "狂热之力",
            "背叛契约"
        ];

        #endregion

        #region 字段

        //动画变量
        private float rotationAngle;        //转盘旋转角度
        private float pulseTimer;           //脉冲计时器
        private float glowTimer;            //光晕计时器
        private float rayTimer;             //光线计时器
        private float inscriptionRotation;  //铭文环旋转

        //UI状态
        private float uiFadeAlpha;
        private int hoveringDiscipleIndex = -1;
        private bool wasHoldingElysium;

        //拖动系统
        private bool isDragging = false;              //是否正在拖动
        private int dragSourceIndex = -1;             //拖动源门徒索引
        private int dragTargetIndex = -1;             //拖动目标门徒索引
        private Vector2 dragCurrentPos;               //当前拖动位置
        private Vector2 dragStartPos;                 //拖动起始位置
        private float dragAlpha = 0f;                 //拖动图标透明度
        private bool wasMouseDown = false;            //上一帧鼠标状态

        //交换动画
        private bool isSwapAnimating = false;         //是否正在播放交换动画
        private float swapAnimProgress = 0f;          //交换动画进度
        private int swapFromIndex = -1;               //交换源索引
        private int swapToIndex = -1;                 //交换目标索引
        private Vector2 swapFromPos;                  //交换源位置
        private Vector2 swapToPos;                    //交换目标位置

        //连接线动画
        private readonly List<DragConnectionLine> connectionLines = [];

        //粒子系统
        private readonly List<HolyLightPRT> holyParticles = [];
        private int particleSpawnTimer;

        //纹理引用
        [VaultLoaden(CWRConstant.UI + "Elysium/RoseWindow")]
        private static Asset<Texture2D> RoseWindowTex;
        [VaultLoaden(CWRConstant.UI + "Elysium/CrossMark")]
        private static Asset<Texture2D> CrossMarkTex;

        #endregion

        #region 本地化

        public string LocalizationCategory => "UI";

        protected static LocalizedText TitleText;
        protected static LocalizedText DiscipleCountText;
        protected static LocalizedText TotalBonusText;
        protected static LocalizedText DamageText;
        protected static LocalizedText DefenseText;
        protected static LocalizedText CritText;
        protected static LocalizedText RegenText;
        protected static LocalizedText EmptySlotText;
        protected static LocalizedText JudasWarningText;
        protected static LocalizedText BonusLabel;
        protected static LocalizedText AbilityLabel;
        protected static LocalizedText DragHintText;
        protected static LocalizedText SwapSuccessText;
        protected static LocalizedText SwapFailText;
        protected static LocalizedText CannotSwapText;

        public override void SetStaticDefaults() {
            TitleText = this.GetLocalization(nameof(TitleText), () => "天国极乐");
            DiscipleCountText = this.GetLocalization(nameof(DiscipleCountText), () => "门徒");
            TotalBonusText = this.GetLocalization(nameof(TotalBonusText), () => "圣恩加持");
            DamageText = this.GetLocalization(nameof(DamageText), () => "伤害");
            DefenseText = this.GetLocalization(nameof(DefenseText), () => "防御");
            CritText = this.GetLocalization(nameof(CritText), () => "暴击");
            RegenText = this.GetLocalization(nameof(RegenText), () => "恢复");
            EmptySlotText = this.GetLocalization(nameof(EmptySlotText), () => "空缺");
            JudasWarningText = this.GetLocalization(nameof(JudasWarningText), () => "警告：犹大的背叛潜伏于此");
            BonusLabel = this.GetLocalization(nameof(BonusLabel), () => "被动");
            AbilityLabel = this.GetLocalization(nameof(AbilityLabel), () => "能力");
            DragHintText = this.GetLocalization(nameof(DragHintText), () => "拖动门徒可变换其身份");
            SwapSuccessText = this.GetLocalization(nameof(SwapSuccessText), () => "身份转换完成");
            SwapFailText = this.GetLocalization(nameof(SwapFailText), () => "转换失败");
            CannotSwapText = this.GetLocalization(nameof(CannotSwapText), () => "无法转换至此位置");
        }

        #endregion

        #region 属性

        public static ElysiumUI Instance => UIHandleLoader.GetUIHandleOfType<ElysiumUI>();

        public override bool Active {
            get {
                //当玩家持有天国极乐时显示UI
                bool holdingElysium = player.HeldItem?.type == ModContent.ItemType<Elysium>() && Main.keyState.PressingShift();
                return holdingElysium || uiFadeAlpha > 0.01f;
            }
        }

        #endregion

        #region 更新逻辑

        public override void Update() {
            bool holdingElysium = player.HeldItem?.type == ModContent.ItemType<Elysium>() && Main.keyState.PressingShift();

            //检测切换
            if (holdingElysium && !wasHoldingElysium) {
                SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.5f, Pitch = 0.3f });
            }
            wasHoldingElysium = holdingElysium;

            //淡入淡出
            if (holdingElysium) {
                uiFadeAlpha = Math.Min(1f, uiFadeAlpha + 0.08f);
            }
            else {
                uiFadeAlpha = Math.Max(0f, uiFadeAlpha - 0.06f);
            }

            if (uiFadeAlpha < 0.01f) return;

            //更新动画
            rotationAngle += 0.002f; //缓慢旋转
            pulseTimer += 0.03f;
            glowTimer += 0.025f;
            rayTimer += 0.015f;
            inscriptionRotation -= 0.003f; //反向旋转铭文环

            if (rotationAngle > MathHelper.TwoPi) rotationAngle -= MathHelper.TwoPi;
            if (pulseTimer > MathHelper.TwoPi) pulseTimer -= MathHelper.TwoPi;
            if (glowTimer > MathHelper.TwoPi) glowTimer -= MathHelper.TwoPi;
            if (rayTimer > MathHelper.TwoPi) rayTimer -= MathHelper.TwoPi;
            if (inscriptionRotation < -MathHelper.TwoPi) inscriptionRotation += MathHelper.TwoPi;

            //固定位置在屏幕右侧
            DrawPosition = new Vector2(Main.screenWidth / 2f, Main.screenHeight - OuterRadius - 40);

            //检测门徒槽位悬停
            UpdateHovering();

            //更新拖动逻辑
            UpdateDragging();

            //更新交换动画
            UpdateSwapAnimation();

            //更新连接线
            UpdateConnectionLines();

            //更新粒子
            UpdateParticles();
        }

        private void UpdateHovering() {
            //如果正在拖动，不更新普通悬停
            if (isDragging) {
                dragTargetIndex = -1;
                Vector2 mousePos = new(Main.mouseX, Main.mouseY);

                for (int i = 0; i < 12; i++) {
                    if (i == dragSourceIndex) continue;

                    float angle = MathHelper.TwoPi * i / 12f - MathHelper.PiOver2 + rotationAngle;
                    Vector2 slotPos = DrawPosition + angle.ToRotationVector2() * DiscipleRadius;

                    if (Vector2.Distance(mousePos, slotPos) < DiscipleSlotSize / 2f + 10f) {
                        dragTargetIndex = i;
                        break;
                    }
                }

                player.mouseInterface = true;
                return;
            }

            hoveringDiscipleIndex = -1;
            Vector2 mousePos2 = new(Main.mouseX, Main.mouseY);

            for (int i = 0; i < 12; i++) {
                float angle = MathHelper.TwoPi * i / 12f - MathHelper.PiOver2 + rotationAngle;
                Vector2 slotPos = DrawPosition + angle.ToRotationVector2() * DiscipleRadius;

                if (Vector2.Distance(mousePos2, slotPos) < DiscipleSlotSize / 2f + 5f) {
                    hoveringDiscipleIndex = i;
                    player.mouseInterface = true;
                    break;
                }
            }
        }

        /// <summary>
        /// 更新拖动逻辑
        /// </summary>
        private void UpdateDragging() {
            if (!player.TryGetModPlayer<ElysiumPlayer>(out var ep)) return;

            bool mouseDown = Main.mouseLeft;
            Vector2 mousePos = new(Main.mouseX, Main.mouseY);

            //开始拖动
            if (mouseDown && !wasMouseDown && !isDragging && !isSwapAnimating) {
                if (hoveringDiscipleIndex >= 0 && ep.HasDiscipleOfType(ElysiumPlayer.DiscipleTypes[hoveringDiscipleIndex])) {
                    isDragging = true;
                    dragSourceIndex = hoveringDiscipleIndex;
                    dragStartPos = GetSlotPosition(dragSourceIndex);
                    dragCurrentPos = mousePos;
                    dragAlpha = 0f;

                    SoundEngine.PlaySound(SoundID.MenuTick with { Volume = 0.6f, Pitch = 0.2f });
                }
            }

            //更新拖动位置
            if (isDragging) {
                dragCurrentPos = Vector2.Lerp(dragCurrentPos, mousePos, 0.3f);
                dragAlpha = Math.Min(1f, dragAlpha + 0.15f);

                //添加连接线粒子
                if (Main.GameUpdateCount % 3 == 0) {
                    Vector2 startPos = GetSlotPosition(dragSourceIndex);
                    connectionLines.Add(new DragConnectionLine(startPos, dragCurrentPos, GetDiscipleColor(dragSourceIndex)));
                }
            }

            //结束拖动
            if (!mouseDown && wasMouseDown && isDragging) {
                CompleteDrag(ep);
            }

            wasMouseDown = mouseDown;
        }

        /// <summary>
        /// 完成拖动操作
        /// </summary>
        private void CompleteDrag(ElysiumPlayer ep) {
            isDragging = false;

            if (dragTargetIndex >= 0 && dragTargetIndex != dragSourceIndex) {
                //检查目标槽位是否可以交换
                bool sourceHasDisciple = ep.HasDiscipleOfType(ElysiumPlayer.DiscipleTypes[dragSourceIndex]);
                bool targetHasDisciple = ep.HasDiscipleOfType(ElysiumPlayer.DiscipleTypes[dragTargetIndex]);

                if (sourceHasDisciple) {
                    //执行交换
                    bool success = ep.SwapDiscipleToSlot(dragSourceIndex, dragTargetIndex);

                    if (success) {
                        //播放成功交换动画
                        StartSwapAnimation(dragSourceIndex, dragTargetIndex);
                        SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.8f, Pitch = 0.4f });

                        CombatText.NewText(player.Hitbox, new Color(255, 220, 150), SwapSuccessText.Value);
                    }
                    else {
                        //交换失败
                        SoundEngine.PlaySound(SoundID.Item16 with { Volume = 0.5f, Pitch = -0.3f });
                        CombatText.NewText(player.Hitbox, new Color(200, 100, 100), SwapFailText.Value);
                    }
                }
            }
            else {
                //取消拖动音效
                SoundEngine.PlaySound(SoundID.MenuClose with { Volume = 0.4f });
            }

            //重置拖动状态
            dragSourceIndex = -1;
            dragTargetIndex = -1;
            dragAlpha = 0f;
        }

        /// <summary>
        /// 开始交换动画
        /// </summary>
        private void StartSwapAnimation(int fromIndex, int toIndex) {
            isSwapAnimating = true;
            swapAnimProgress = 0f;
            swapFromIndex = fromIndex;
            swapToIndex = toIndex;
            swapFromPos = GetSlotPosition(fromIndex);
            swapToPos = GetSlotPosition(toIndex);

            //生成交换粒子
            SpawnSwapParticles(swapFromPos, swapToPos, GetDiscipleColor(fromIndex), GetDiscipleColor(toIndex));
        }

        /// <summary>
        /// 更新交换动画
        /// </summary>
        private void UpdateSwapAnimation() {
            if (!isSwapAnimating) return;

            swapAnimProgress += 0.05f;

            if (swapAnimProgress >= 1f) {
                isSwapAnimating = false;
                swapFromIndex = -1;
                swapToIndex = -1;
            }
        }

        /// <summary>
        /// 生成交换粒子效果
        /// </summary>
        private void SpawnSwapParticles(Vector2 from, Vector2 to, Color colorFrom, Color colorTo) {
            //从源到目标的弧形粒子
            int particleCount = 15;
            for (int i = 0; i < particleCount; i++) {
                float t = i / (float)particleCount;
                Vector2 pos = Vector2.Lerp(from, to, t);
                //添加弧形偏移
                float arcOffset = MathF.Sin(t * MathHelper.Pi) * 30f;
                Vector2 perpendicular = (to - from).SafeNormalize(Vector2.UnitX).RotatedBy(MathHelper.PiOver2);
                pos += perpendicular * arcOffset;

                holyParticles.Add(new HolyLightPRT(pos, Vector2.Lerp(from, to, 0.5f)) {
                    ParticleColor = Color.Lerp(colorFrom, colorTo, t),
                    MaxLife = 40 + i * 2,
                    Life = 40 + i * 2
                });
            }
        }

        /// <summary>
        /// 更新连接线
        /// </summary>
        private void UpdateConnectionLines() {
            for (int i = connectionLines.Count - 1; i >= 0; i--) {
                if (connectionLines[i].Update()) {
                    connectionLines.RemoveAt(i);
                }
            }
        }

        /// <summary>
        /// 获取槽位位置
        /// </summary>
        private Vector2 GetSlotPosition(int index) {
            float angle = MathHelper.TwoPi * index / 12f - MathHelper.PiOver2 + rotationAngle;
            return DrawPosition + angle.ToRotationVector2() * DiscipleRadius;
        }

        private void UpdateParticles() {
            particleSpawnTimer++;

            //根据门徒数量生成粒子
            if (player.TryGetModPlayer<ElysiumPlayer>(out var ep)) {
                int count = ep.GetDiscipleCount();
                if (particleSpawnTimer >= 15 - count && holyParticles.Count < 30 + count * 3) {
                    particleSpawnTimer = 0;
                    float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                    float dist = Main.rand.NextFloat(InnerRadius, OuterRadius);
                    Vector2 pos = DrawPosition + angle.ToRotationVector2() * dist;
                    holyParticles.Add(new HolyLightPRT(pos, DrawPosition));
                }
            }

            //更新粒子
            for (int i = holyParticles.Count - 1; i >= 0; i--) {
                if (holyParticles[i].Update()) {
                    holyParticles.RemoveAt(i);
                }
            }
        }

        #endregion

        #region 绘制

        public override void Draw(SpriteBatch spriteBatch) {
            if (uiFadeAlpha < 0.01f) return;

            float alpha = uiFadeAlpha;

            //绘制底层光晕
            DrawBackgroundGlow(spriteBatch, alpha);

            //绘制圣光射线
            DrawHolyRays(spriteBatch, alpha);

            //绘制外环装饰
            DrawOuterRing(spriteBatch, alpha);

            //绘制拉丁文铭文环
            DrawInscriptionRing(spriteBatch, alpha);

            //绘制门徒转盘
            DrawDiscipleWheel(spriteBatch, alpha);

            //绘制中心区域
            DrawCenterArea(spriteBatch, alpha);

            //绘制十字架装饰
            DrawCrossDecorations(spriteBatch, alpha);

            //绘制粒子
            foreach (var p in holyParticles) {
                p.Draw(spriteBatch, alpha * 0.7f);
            }

            //绘制悬停信息
            if (hoveringDiscipleIndex >= 0 && !isDragging) {
                DrawDiscipleTooltip(spriteBatch, alpha);
            }

            //绘制拖动提示
            DrawDragHint(spriteBatch, alpha);

            //绘制连接线
            DrawConnectionLines(spriteBatch, alpha);

            //绘制拖动中的门徒
            if (isDragging) {
                DrawDraggingDisciple(spriteBatch, alpha);
            }

            //绘制交换动画
            if (isSwapAnimating) {
                DrawSwapAnimation(spriteBatch, alpha);
            }

            //绘制犹大警告
            DrawJudasWarning(spriteBatch, alpha);
        }

        /// <summary>
        /// 绘制背景光晕
        /// </summary>
        private void DrawBackgroundGlow(SpriteBatch sb, float alpha) {
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow == null) return;

            float pulse = (float)Math.Sin(pulseTimer) * 0.15f + 0.85f;

            //最外层柔和光晕
            Color outerGlow = new Color(255, 230, 180, 0) * alpha;
            sb.Draw(glow, DrawPosition, null, outerGlow, 0, glow.Size() / 2, OuterRadius / 32f * 1.5f, SpriteEffects.None, 0);

            //中层金色光晕
            Color midGlow = new Color(255, 200, 100, 0) * alpha;
            sb.Draw(glow, DrawPosition, null, midGlow, 0, glow.Size() / 2, OuterRadius / 32f, SpriteEffects.None, 0);

            //内层白色光晕
            Color innerGlow = Color.White with { A = 0 } * alpha;
            sb.Draw(glow, DrawPosition, null, innerGlow, 0, glow.Size() / 2, InnerRadius / 32f * 1.2f, SpriteEffects.None, 0);
        }

        /// <summary>
        /// 绘制圣光射线
        /// </summary>
        private void DrawHolyRays(SpriteBatch sb, float alpha) {
            Texture2D px = CWRAsset.Placeholder_White?.Value;
            if (px == null) return;

            int rayCount = 24;
            for (int i = 0; i < rayCount; i++) {
                float angle = MathHelper.TwoPi * i / rayCount + rayTimer;
                float rayLength = OuterRadius * (0.8f + (float)Math.Sin(rayTimer * 2 + i * 0.5f) * 0.2f);
                float rayAlpha = 0.08f + (float)Math.Sin(rayTimer * 3 + i) * 0.04f;

                Vector2 start = DrawPosition + angle.ToRotationVector2() * InnerRadius * 0.5f;
                Vector2 end = DrawPosition + angle.ToRotationVector2() * rayLength;

                //渐变射线
                Color rayColor = new Color(255, 220, 150) * (alpha * rayAlpha);
                DrawGradientLine(sb, px, start, end, 2f, rayColor, rayColor * 0.1f);
            }
        }

        /// <summary>
        /// 绘制外环装饰
        /// </summary>
        private void DrawOuterRing(SpriteBatch sb, float alpha) {
            Texture2D px = CWRAsset.Placeholder_White?.Value;
            if (px == null) return;

            //外环主体
            int segments = 72;
            float thickness = 3f;
            Color ringColor = new Color(200, 170, 120) * (alpha * 0.6f);

            for (int i = 0; i < segments; i++) {
                float angle1 = MathHelper.TwoPi * i / segments;
                float angle2 = MathHelper.TwoPi * (i + 1) / segments;

                Vector2 p1 = DrawPosition + angle1.ToRotationVector2() * OuterRadius;
                Vector2 p2 = DrawPosition + angle2.ToRotationVector2() * OuterRadius;

                DrawLine(sb, px, p1, p2, thickness, ringColor);
            }

            //内环
            Color innerRingColor = new Color(220, 190, 140) * (alpha * 0.5f);
            for (int i = 0; i < segments; i++) {
                float angle1 = MathHelper.TwoPi * i / segments;
                float angle2 = MathHelper.TwoPi * (i + 1) / segments;

                Vector2 p1 = DrawPosition + angle1.ToRotationVector2() * (InnerRadius + 10f);
                Vector2 p2 = DrawPosition + angle2.ToRotationVector2() * (InnerRadius + 10f);

                DrawLine(sb, px, p1, p2, 2f, innerRingColor);
            }

            //12分割线(玫瑰窗式)
            for (int i = 0; i < 12; i++) {
                float angle = MathHelper.TwoPi * i / 12f - MathHelper.PiOver2;
                Vector2 inner = DrawPosition + angle.ToRotationVector2() * (InnerRadius + 15f);
                Vector2 outer = DrawPosition + angle.ToRotationVector2() * (OuterRadius - 5f);

                float lineAlpha = i == hoveringDiscipleIndex ? 0.8f : 0.3f;
                Color lineColor = new Color(180, 150, 100) * (alpha * lineAlpha);
                DrawLine(sb, px, inner, outer, 1f, lineColor);
            }
        }

        /// <summary>
        /// 绘制拉丁文铭文环
        /// </summary>
        private void DrawInscriptionRing(SpriteBatch sb, float alpha) {
            float inscriptionRadius = OuterRadius - 18f;

            for (int i = 0; i < 12; i++) {
                float angle = MathHelper.TwoPi * i / 12f - MathHelper.PiOver2 + inscriptionRotation;
                Vector2 pos = DrawPosition + angle.ToRotationVector2() * inscriptionRadius;

                string text = LatinNames[i];
                Vector2 textSize = FontAssets.MouseText.Value.MeasureString(text) * 0.35f;

                //文字随角度旋转
                float textRot = angle + MathHelper.PiOver2;

                Color textColor = new Color(180, 160, 120) * (alpha * 0.5f);

                //检查是否是已获得的门徒
                if (player.TryGetModPlayer<ElysiumPlayer>(out var ep)) {
                    if (ep.HasDiscipleOfType(ElysiumPlayer.DiscipleTypes[i])) {
                        textColor = new Color(255, 220, 150) * (alpha * 0.8f);
                    }
                }

                //特殊处理犹大
                if (i == 11 && player.TryGetModPlayer<ElysiumPlayer>(out var ep2) && ep2.GetDiscipleCount() == 12) {
                    float flash = (float)Math.Sin(glowTimer * 4) * 0.5f + 0.5f;
                    textColor = Color.Lerp(textColor, new Color(150, 50, 50), flash);
                }

                DrawRotatedText(sb, text, pos, textRot, textColor, 0.35f);
            }
        }

        /// <summary>
        /// 绘制门徒转盘
        /// </summary>
        private void DrawDiscipleWheel(SpriteBatch sb, float alpha) {
            Texture2D px = CWRAsset.Placeholder_White?.Value;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (px == null) return;

            player.TryGetModPlayer<ElysiumPlayer>(out var ep);

            for (int i = 0; i < 12; i++) {
                float angle = MathHelper.TwoPi * i / 12f - MathHelper.PiOver2 + rotationAngle;
                Vector2 slotPos = DrawPosition + angle.ToRotationVector2() * DiscipleRadius;

                bool hasDisciple = ep != null && ep.HasDiscipleOfType(ElysiumPlayer.DiscipleTypes[i]);
                bool isHovering = hoveringDiscipleIndex == i;
                bool isJudas = i == 11;
                bool isDragSource = isDragging && dragSourceIndex == i;
                bool isDragTarget = isDragging && dragTargetIndex == i;

                //槽位背景
                float slotPulse = (float)Math.Sin(pulseTimer + i * 0.5f) * 0.1f + 0.9f;
                Color bgColor;

                if (isDragSource) {
                    //拖动源槽位变暗
                    bgColor = new Color(15, 12, 10) * 0.6f;
                }
                else if (isDragTarget) {
                    //拖动目标槽位高亮
                    float targetPulse = (float)Math.Sin(glowTimer * 5) * 0.2f + 0.8f;
                    bgColor = Color.Lerp(new Color(40, 35, 25), GetDiscipleColor(i), 0.3f * targetPulse);
                }
                else if (hasDisciple) {
                    if (isJudas && ep.GetDiscipleCount() == 12) {
                        //犹大特殊颜色(危险)
                        float dangerFlash = (float)Math.Sin(glowTimer * 3) * 0.3f + 0.7f;
                        bgColor = Color.Lerp(new Color(60, 20, 20), new Color(100, 30, 30), dangerFlash);
                    }
                    else {
                        bgColor = new Color(40, 35, 25) * slotPulse;
                    }
                }
                else {
                    bgColor = new Color(20, 18, 15) * 0.8f;
                }

                if (isHovering && !isDragging) {
                    bgColor = Color.Lerp(bgColor, new Color(60, 50, 35), 0.4f);
                }

                //绘制槽位圆形背景
                DrawFilledCircle(sb, px, slotPos, DiscipleSlotSize / 2f, bgColor * alpha);

                //槽位边框
                Color borderColor;
                if (isDragTarget) {
                    //拖动目标边框高亮脉冲
                    float targetPulse = (float)Math.Sin(glowTimer * 6) * 0.3f + 0.7f;
                    borderColor = Color.Lerp(GetDiscipleColor(dragSourceIndex), GetDiscipleColor(i), 0.5f) * targetPulse;
                    borderColor = Color.Lerp(borderColor, Color.White, 0.3f);
                }
                else if (isDragSource) {
                    //拖动源边框变暗
                    borderColor = GetDiscipleColor(i) * 0.4f;
                }
                else if (hasDisciple) {
                    borderColor = GetDiscipleColor(i);
                    if (isHovering) borderColor = Color.Lerp(borderColor, Color.White, 0.3f);
                }
                else {
                    borderColor = new Color(80, 70, 50) * 0.6f;
                }

                float borderThickness = isDragTarget ? 3f : 2f;
                DrawCircleOutline(sb, px, slotPos, DiscipleSlotSize / 2f, borderThickness, borderColor * alpha);

                //门徒图标或空缺标记
                if (hasDisciple && !isDragSource) {
                    //绘制门徒纹理
                    int projType = ElysiumPlayer.DiscipleTypes[i];
                    string texPath = GetDiscipleTexturePath(i);
                    Texture2D discipleTex = ModContent.Request<Texture2D>(texPath).Value;

                    float iconScale = (DiscipleSlotSize - 12f) / Math.Max(discipleTex.Width, discipleTex.Height);
                    Color iconColor = Color.White * alpha;
                    if (isJudas && ep.GetDiscipleCount() == 12) {
                        float shadowPulse = (float)Math.Sin(glowTimer * 2) * 0.2f + 0.8f;
                        iconColor = Color.Lerp(iconColor, new Color(150, 100, 100), 1f - shadowPulse);
                    }

                    //如果是拖动目标，稍微缩小显示
                    if (isDragTarget) {
                        iconScale *= 0.8f;
                        iconColor *= 0.7f;
                    }

                    sb.Draw(discipleTex, slotPos, null, iconColor, 0, discipleTex.Size() / 2, iconScale, SpriteEffects.None, 0);

                    //光晕效果
                    if (glow != null) {
                        Color glowColor = GetDiscipleColor(i) * (alpha * 0.3f * slotPulse);
                        glowColor.A = 0;
                        sb.Draw(glow, slotPos, null, glowColor, 0, glow.Size() / 2, DiscipleSlotSize / 32f, SpriteEffects.None, 0);
                    }
                }
                else if (isDragSource) {
                    //拖动源显示虚影
                    string texPath = GetDiscipleTexturePath(i);
                    Texture2D discipleTex = ModContent.Request<Texture2D>(texPath).Value;

                    float iconScale = (DiscipleSlotSize - 12f) / Math.Max(discipleTex.Width, discipleTex.Height);
                    Color ghostColor = GetDiscipleColor(i) * (alpha * 0.25f);

                    sb.Draw(discipleTex, slotPos, null, ghostColor, 0, discipleTex.Size() / 2, iconScale * 0.9f, SpriteEffects.None, 0);
                }
                else {
                    //空缺标记(十字虚线)
                    Color emptyColor = new Color(60, 50, 40) * (alpha * 0.5f);

                    //如果是拖动目标且目标为空，显示加号提示
                    if (isDragTarget) {
                        float plusPulse = (float)Math.Sin(glowTimer * 4) * 0.3f + 0.7f;
                        emptyColor = GetDiscipleColor(dragSourceIndex) * (alpha * 0.6f * plusPulse);
                    }

                    float crossSize = 12f;
                    sb.Draw(px, slotPos, null, emptyColor, 0, new Vector2(0.5f), new Vector2(crossSize, 2f), SpriteEffects.None, 0);
                    sb.Draw(px, slotPos, null, emptyColor, MathHelper.PiOver2, new Vector2(0.5f), new Vector2(crossSize, 2f), SpriteEffects.None, 0);
                }

                //序号标记
                string numText = (i + 1).ToString();
                Vector2 numPos = slotPos + new Vector2(0, DiscipleSlotSize / 2f + 8f);
                Color numColor = hasDisciple ? new Color(200, 180, 140) : new Color(100, 90, 70);
                if (isDragTarget) {
                    numColor = Color.Lerp(numColor, GetDiscipleColor(dragSourceIndex), 0.5f);
                }
                Vector2 numSize = FontAssets.MouseText.Value.MeasureString(numText) * 0.4f;
                Utils.DrawBorderString(sb, numText, numPos - numSize / 2, numColor * alpha, 0.4f);
            }
        }

        /// <summary>
        /// 绘制中心区域
        /// </summary>
        private void DrawCenterArea(SpriteBatch sb, float alpha) {
            Texture2D px = CWRAsset.Placeholder_White?.Value;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (px == null) return;

            //中心圆形背景
            Color centerBg = new Color(25, 22, 18) * (alpha * 0.9f);
            DrawFilledCircle(sb, px, DrawPosition, InnerRadius, centerBg);

            //中心边框
            float borderPulse = (float)Math.Sin(pulseTimer * 1.5f) * 0.2f + 0.8f;
            Color centerBorder = new Color(200, 170, 120) * (alpha * 0.7f * borderPulse);
            DrawCircleOutline(sb, px, DrawPosition, InnerRadius, 2f, centerBorder);

            //获取门徒数据
            int discipleCount = 0;
            float damageBonus = 0, defenseBonus = 0, critBonus = 0, regenBonus = 0;

            if (player.TryGetModPlayer<ElysiumPlayer>(out var ep)) {
                discipleCount = ep.GetDiscipleCount();

                //计算总增益(与ElysiumPlayer中的逻辑对应)
                if (discipleCount == 11) {
                    damageBonus = 25;
                    critBonus = 15;
                    defenseBonus = 20;
                    regenBonus = 5;
                }
                else if (discipleCount == 12) {
                    damageBonus = 50;
                    critBonus = 30;
                    defenseBonus = 40;
                    regenBonus = 10;
                }
                else if (discipleCount > 0) {
                    float ratio = discipleCount / 12f;
                    damageBonus = 15 * ratio;
                    critBonus = 10 * ratio;
                    defenseBonus = 15 * ratio;
                }
            }

            //标题
            string title = TitleText.Value;
            Vector2 titleSize = FontAssets.MouseText.Value.MeasureString(title) * 0.5f;
            Vector2 titlePos = DrawPosition - new Vector2(0, 35);
            Color titleColor = new Color(255, 230, 180) * alpha;
            Utils.DrawBorderString(sb, title, titlePos - titleSize / 2, titleColor, 0.5f);

            //门徒计数
            string countText = $"{discipleCount}/12";
            Vector2 countSize = FontAssets.MouseText.Value.MeasureString(countText) * 0.7f;
            Color countColor = discipleCount == 12 ?
                Color.Lerp(new Color(255, 200, 100), new Color(200, 80, 80), (float)Math.Sin(glowTimer * 3) * 0.5f + 0.5f) :
                new Color(255, 220, 150);
            Utils.DrawBorderString(sb, countText, DrawPosition - countSize / 2 - new Vector2(0, 8), countColor * alpha, 0.7f);

            //门徒标签
            string labelText = DiscipleCountText.Value;
            Vector2 labelSize = FontAssets.MouseText.Value.MeasureString(labelText) * 0.4f;
            Utils.DrawBorderString(sb, labelText, DrawPosition - labelSize / 2 + new Vector2(0, 12), new Color(180, 160, 130) * alpha, 0.4f);

            //增益信息(在中心下方)
            if (discipleCount > 0) {
                Vector2 bonusStart = DrawPosition + new Vector2(0, 32);
                float lineHeight = 11f;
                float fontSize = 0.35f;
                Color bonusColor = new Color(200, 180, 140) * alpha;

                if (damageBonus > 0) {
                    string dmgText = $"+{damageBonus:F0}% {DamageText.Value}";
                    Vector2 dmgSize = FontAssets.MouseText.Value.MeasureString(dmgText) * fontSize;
                    Utils.DrawBorderString(sb, dmgText, bonusStart - dmgSize / 2, bonusColor, fontSize);
                }
            }
        }

        /// <summary>
        /// 绘制十字架装饰
        /// </summary>
        private void DrawCrossDecorations(SpriteBatch sb, float alpha) {
            Texture2D px = CWRAsset.Placeholder_White?.Value;
            if (px == null) return;

            //四个方向的十字架
            float[] angles = [0, MathHelper.PiOver2, MathHelper.Pi, MathHelper.Pi * 1.5f];
            float crossDist = OuterRadius + 15f;

            foreach (float angle in angles) {
                Vector2 crossPos = DrawPosition + angle.ToRotationVector2() * crossDist;
                DrawSmallCross(sb, px, crossPos, 10f, new Color(180, 150, 100) * (alpha * 0.6f));
            }
        }

        /// <summary>
        /// 绘制门徒悬停提示
        /// </summary>
        private void DrawDiscipleTooltip(SpriteBatch sb, float alpha) {
            Texture2D px = CWRAsset.Placeholder_White?.Value;
            if (px == null || hoveringDiscipleIndex < 0) return;

            int idx = hoveringDiscipleIndex;
            bool hasDisciple = false;

            if (player.TryGetModPlayer<ElysiumPlayer>(out var ep)) {
                hasDisciple = ep.HasDiscipleOfType(ElysiumPlayer.DiscipleTypes[idx]);
            }

            //提示框位置
            Vector2 mousePos = new(Main.mouseX, Main.mouseY);
            Vector2 tooltipPos = mousePos + new Vector2(20, 20);

            //提示框内容
            string name = ElysiumPlayer.DiscipleNames[idx];
            string latinName = LatinNames[idx];
            string ability = AbilityBriefs[idx];
            string status = hasDisciple ? "已追随" : EmptySlotText.Value;

            //计算提示框大小
            float maxWidth = 0;
            maxWidth = Math.Max(maxWidth, FontAssets.MouseText.Value.MeasureString(name).X * 0.55f);
            maxWidth = Math.Max(maxWidth, FontAssets.MouseText.Value.MeasureString(latinName).X * 0.4f);
            maxWidth = Math.Max(maxWidth, FontAssets.MouseText.Value.MeasureString($"{AbilityLabel.Value}: {ability}").X * 0.45f);

            float tooltipWidth = maxWidth + 24f;
            float tooltipHeight = 75f;

            //确保不超出屏幕
            if (tooltipPos.X + tooltipWidth > Main.screenWidth) {
                tooltipPos.X = mousePos.X - tooltipWidth - 10;
            }
            if (tooltipPos.Y + tooltipHeight > Main.screenHeight) {
                tooltipPos.Y = mousePos.Y - tooltipHeight - 10;
            }

            Rectangle tooltipRect = new((int)tooltipPos.X, (int)tooltipPos.Y, (int)tooltipWidth, (int)tooltipHeight);

            //背景
            Color bgColor = new Color(15, 12, 10) * (alpha * 0.95f);
            sb.Draw(px, tooltipRect, null, bgColor);

            //边框
            Color borderColor = hasDisciple ? GetDiscipleColor(idx) : new Color(100, 80, 60);
            borderColor *= alpha * 0.8f;
            sb.Draw(px, new Rectangle(tooltipRect.X, tooltipRect.Y, tooltipRect.Width, 2), null, borderColor);
            sb.Draw(px, new Rectangle(tooltipRect.X, tooltipRect.Bottom - 2, tooltipRect.Width, 2), null, borderColor * 0.6f);
            sb.Draw(px, new Rectangle(tooltipRect.X, tooltipRect.Y, 2, tooltipRect.Height), null, borderColor * 0.8f);
            sb.Draw(px, new Rectangle(tooltipRect.Right - 2, tooltipRect.Y, 2, tooltipRect.Height), null, borderColor * 0.8f);

            //内容
            float y = tooltipPos.Y + 8;
            Color nameColor = hasDisciple ? GetDiscipleColor(idx) : new Color(150, 130, 100);
            Utils.DrawBorderString(sb, name, new Vector2(tooltipPos.X + 10, y), nameColor * alpha, 0.55f);
            y += 18;

            Color latinColor = new Color(140, 120, 90) * alpha;
            Utils.DrawBorderString(sb, latinName, new Vector2(tooltipPos.X + 10, y), latinColor, 0.4f);
            y += 14;

            Color abilityColor = new Color(200, 180, 140) * alpha;
            Utils.DrawBorderString(sb, $"{AbilityLabel.Value}: {ability}", new Vector2(tooltipPos.X + 10, y), abilityColor, 0.45f);
            y += 16;

            Color statusColor = hasDisciple ? new Color(150, 200, 150) : new Color(150, 120, 100);
            Utils.DrawBorderString(sb, status, new Vector2(tooltipPos.X + 10, y), statusColor * alpha, 0.4f);
        }

        /// <summary>
        /// 绘制犹大警告
        /// </summary>
        private void DrawJudasWarning(SpriteBatch sb, float alpha) {
            if (!player.TryGetModPlayer<ElysiumPlayer>(out var ep)) return;
            if (ep.GetDiscipleCount() != 12) return;

            float flash = (float)Math.Sin(glowTimer * 4) * 0.4f + 0.6f;
            string warning = JudasWarningText.Value;
            Vector2 warningSize = FontAssets.MouseText.Value.MeasureString(warning) * 0.4f;
            Vector2 warningPos = DrawPosition + new Vector2(0, OuterRadius + 25f) - warningSize / 2;

            Color warningColor = new Color(200, 80, 60) * (alpha * flash);
            Utils.DrawBorderString(sb, warning, warningPos, warningColor, 0.4f);
        }

        /// <summary>
        /// 绘制拖动提示
        /// </summary>
        private void DrawDragHint(SpriteBatch sb, float alpha) {
            if (!player.TryGetModPlayer<ElysiumPlayer>(out var ep)) return;
            if (ep.GetDiscipleCount() == 0) return;
            if (isDragging || isSwapAnimating) return;

            //在悬停门徒时显示拖动提示
            if (hoveringDiscipleIndex >= 0 && ep.HasDiscipleOfType(ElysiumPlayer.DiscipleTypes[hoveringDiscipleIndex])) {
                string hint = DragHintText.Value;
                Vector2 hintSize = FontAssets.MouseText.Value.MeasureString(hint) * 0.35f;
                Vector2 hintPos = DrawPosition + new Vector2(0, -OuterRadius - 20f) - hintSize / 2;

                float pulse = (float)Math.Sin(glowTimer * 3) * 0.2f + 0.8f;
                Color hintColor = new Color(200, 180, 140) * (alpha * 0.7f * pulse);
                Utils.DrawBorderString(sb, hint, hintPos, hintColor, 0.35f);
            }
        }

        /// <summary>
        /// 绘制连接线
        /// </summary>
        private void DrawConnectionLines(SpriteBatch sb, float alpha) {
            foreach (var line in connectionLines) {
                line.Draw(sb, alpha);
            }
        }

        /// <summary>
        /// 绘制正在拖动的门徒
        /// </summary>
        private void DrawDraggingDisciple(SpriteBatch sb, float alpha) {
            if (dragSourceIndex < 0) return;

            Texture2D px = CWRAsset.Placeholder_White?.Value;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (px == null) return;

            float effectiveAlpha = alpha * dragAlpha;

            //绘制源槽位的虚线框（表示原位置）
            Vector2 sourcePos = GetSlotPosition(dragSourceIndex);
            Color outlineColor = GetDiscipleColor(dragSourceIndex) * (effectiveAlpha * 0.5f);
            DrawDashedCircle(sb, px, sourcePos, DiscipleSlotSize / 2f + 3f, 2f, outlineColor);

            //绘制从源到当前位置的连接线
            Color lineColor = GetDiscipleColor(dragSourceIndex) * (effectiveAlpha * 0.6f);
            DrawGradientLine(sb, px, sourcePos, dragCurrentPos, 2f, lineColor * 0.3f, lineColor);

            //如果有有效目标，绘制目标高亮
            if (dragTargetIndex >= 0) {
                Vector2 targetPos = GetSlotPosition(dragTargetIndex);

                //目标槽位高亮
                float highlightPulse = (float)Math.Sin(glowTimer * 5) * 0.3f + 0.7f;
                Color highlightColor = GetDiscipleColor(dragTargetIndex) * (effectiveAlpha * highlightPulse);

                if (glow != null) {
                    sb.Draw(glow, targetPos, null, highlightColor with { A = 0 }, 0, glow.Size() / 2, DiscipleSlotSize / 20f, SpriteEffects.None, 0);
                }

                DrawCircleOutline(sb, px, targetPos, DiscipleSlotSize / 2f + 5f, 3f, highlightColor);

                //绘制箭头指示
                DrawArrowIndicator(sb, px, dragCurrentPos, targetPos, highlightColor);

                //显示目标门徒名称
                string targetName = ElysiumPlayer.DiscipleNames[dragTargetIndex];
                Vector2 nameSize = FontAssets.MouseText.Value.MeasureString(targetName) * 0.4f;
                Vector2 namePos = targetPos - new Vector2(0, DiscipleSlotSize / 2f + 18f) - nameSize / 2;
                Utils.DrawBorderString(sb, targetName, namePos, highlightColor, 0.4f);
            }

            //绘制拖动中的门徒图标
            string texPath = GetDiscipleTexturePath(dragSourceIndex);
            Texture2D discipleTex = ModContent.Request<Texture2D>(texPath).Value;

            //图标光晕
            if (glow != null) {
                Color glowColor = GetDiscipleColor(dragSourceIndex) * (effectiveAlpha * 0.6f);
                glowColor.A = 0;
                sb.Draw(glow, dragCurrentPos, null, glowColor, 0, glow.Size() / 2, DiscipleSlotSize / 24f, SpriteEffects.None, 0);
            }

            //图标本身（稍大，带脉冲）
            float iconPulse = 1.1f + (float)Math.Sin(glowTimer * 4) * 0.1f;
            float iconScale = (DiscipleSlotSize - 8f) / Math.Max(discipleTex.Width, discipleTex.Height) * iconPulse;
            sb.Draw(discipleTex, dragCurrentPos, null, Color.White * effectiveAlpha, 0, discipleTex.Size() / 2, iconScale, SpriteEffects.None, 0);

            //门徒名称跟随
            string sourceName = ElysiumPlayer.DiscipleNames[dragSourceIndex];
            Vector2 sourceNameSize = FontAssets.MouseText.Value.MeasureString(sourceName) * 0.45f;
            Vector2 sourceNamePos = dragCurrentPos + new Vector2(0, DiscipleSlotSize / 2f + 5f) - sourceNameSize / 2;
            Utils.DrawBorderString(sb, sourceName, sourceNamePos, GetDiscipleColor(dragSourceIndex) * effectiveAlpha, 0.45f);
        }

        /// <summary>
        /// 绘制交换动画
        /// </summary>
        private void DrawSwapAnimation(SpriteBatch sb, float alpha) {
            if (swapFromIndex < 0 || swapToIndex < 0) return;

            Texture2D glow = CWRAsset.SoftGlow?.Value;
            Texture2D px = CWRAsset.Placeholder_White?.Value;
            if (glow == null || px == null) return;

            //使用缓动函数使动画更流畅
            float eased = EaseOutBack(swapAnimProgress);
            float fadeAlpha = 1f - swapAnimProgress;

            //计算当前动画位置
            Vector2 fromCurrentPos = Vector2.Lerp(swapFromPos, swapToPos, eased);
            Vector2 toCurrentPos = Vector2.Lerp(swapToPos, swapFromPos, eased);

            //绘制弧形轨迹
            float arcHeight = 40f * (float)Math.Sin(swapAnimProgress * MathHelper.Pi);
            Vector2 perpendicular = (swapToPos - swapFromPos).SafeNormalize(Vector2.UnitX).RotatedBy(MathHelper.PiOver2);
            fromCurrentPos += perpendicular * arcHeight;
            toCurrentPos -= perpendicular * arcHeight;

            //绘制移动中的光球
            Color fromColor = GetDiscipleColor(swapFromIndex) * (alpha * fadeAlpha);
            Color toColor = GetDiscipleColor(swapToIndex) * (alpha * fadeAlpha);

            float glowScale = 0.8f + (float)Math.Sin(swapAnimProgress * MathHelper.Pi) * 0.4f;
            sb.Draw(glow, fromCurrentPos, null, fromColor with { A = 0 }, 0, glow.Size() / 2, glowScale, SpriteEffects.None, 0);
            sb.Draw(glow, toCurrentPos, null, toColor with { A = 0 }, 0, glow.Size() / 2, glowScale, SpriteEffects.None, 0);

            //绘制连接两个位置的神圣光线
            if (swapAnimProgress < 0.7f) {
                Color lineColor = Color.Lerp(fromColor, toColor, 0.5f) * (1f - swapAnimProgress / 0.7f);
                DrawGradientLine(sb, px, fromCurrentPos, toCurrentPos, 2f, lineColor, lineColor * 0.3f);
            }
        }

        /// <summary>
        /// 绘制虚线圆
        /// </summary>
        private static void DrawDashedCircle(SpriteBatch sb, Texture2D px, Vector2 center, float radius, float thickness, Color color) {
            int segments = 24;
            for (int i = 0; i < segments; i += 2) { //每隔一个绘制，形成虚线效果
                float angle1 = MathHelper.TwoPi * i / segments;
                float angle2 = MathHelper.TwoPi * (i + 1) / segments;

                Vector2 p1 = center + angle1.ToRotationVector2() * radius;
                Vector2 p2 = center + angle2.ToRotationVector2() * radius;

                DrawLine(sb, px, p1, p2, thickness, color);
            }
        }

        /// <summary>
        /// 绘制箭头指示器
        /// </summary>
        private static void DrawArrowIndicator(SpriteBatch sb, Texture2D px, Vector2 from, Vector2 to, Color color) {
            Vector2 direction = (to - from).SafeNormalize(Vector2.UnitX);
            Vector2 arrowTip = to - direction * 25f;

            //箭头两翼
            float arrowAngle = 0.4f;
            float arrowLength = 12f;
            Vector2 wing1 = arrowTip - direction.RotatedBy(arrowAngle) * arrowLength;
            Vector2 wing2 = arrowTip - direction.RotatedBy(-arrowAngle) * arrowLength;

            DrawLine(sb, px, arrowTip, wing1, 2f, color);
            DrawLine(sb, px, arrowTip, wing2, 2f, color);
        }

        /// <summary>
        /// 缓出回弹缓动函数
        /// </summary>
        private static float EaseOutBack(float t) {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            return 1f + c3 * MathF.Pow(t - 1f, 3f) + c1 * MathF.Pow(t - 1f, 2f);
        }

        #endregion

        #region 辅助绘制方法

        private static void DrawLine(SpriteBatch sb, Texture2D px, Vector2 start, Vector2 end, float thickness, Color color) {
            Vector2 diff = end - start;
            float length = diff.Length();
            if (length < 1f) return;
            sb.Draw(px, start, new Rectangle(0, 0, 1, 1), color, diff.ToRotation(), Vector2.Zero, new Vector2(length, thickness), SpriteEffects.None, 0f);
        }

        private static void DrawGradientLine(SpriteBatch sb, Texture2D px, Vector2 start, Vector2 end, float thickness, Color startColor, Color endColor) {
            Vector2 diff = end - start;
            float length = diff.Length();
            if (length < 1f) return;

            int segments = Math.Max(1, (int)(length / 10));
            for (int i = 0; i < segments; i++) {
                float t1 = i / (float)segments;
                float t2 = (i + 1) / (float)segments;
                Vector2 p1 = Vector2.Lerp(start, end, t1);
                Vector2 p2 = Vector2.Lerp(start, end, t2);
                Color c = Color.Lerp(startColor, endColor, (t1 + t2) / 2f);
                DrawLine(sb, px, p1, p2, thickness, c);
            }
        }

        private static void DrawFilledCircle(SpriteBatch sb, Texture2D px, Vector2 center, float radius, Color color) {
            //用矩形近似圆形
            int segments = 24;
            for (int i = 0; i < segments; i++) {
                float angle1 = MathHelper.TwoPi * i / segments;
                float angle2 = MathHelper.TwoPi * (i + 1) / segments;

                Vector2 p1 = center + angle1.ToRotationVector2() * radius;
                Vector2 p2 = center + angle2.ToRotationVector2() * radius;

                //绘制三角形扇形
                DrawTriangle(sb, px, center, p1, p2, color);
            }
        }

        private static void DrawTriangle(SpriteBatch sb, Texture2D px, Vector2 p1, Vector2 p2, Vector2 p3, Color color) {
            //简化处理：用线段填充
            int steps = 10;
            for (int i = 0; i <= steps; i++) {
                float t = i / (float)steps;
                Vector2 start = Vector2.Lerp(p1, p2, t);
                Vector2 end = Vector2.Lerp(p1, p3, t);
                DrawLine(sb, px, start, end, 2f, color);
            }
        }

        private static void DrawCircleOutline(SpriteBatch sb, Texture2D px, Vector2 center, float radius, float thickness, Color color) {
            int segments = 48;
            for (int i = 0; i < segments; i++) {
                float angle1 = MathHelper.TwoPi * i / segments;
                float angle2 = MathHelper.TwoPi * (i + 1) / segments;

                Vector2 p1 = center + angle1.ToRotationVector2() * radius;
                Vector2 p2 = center + angle2.ToRotationVector2() * radius;

                DrawLine(sb, px, p1, p2, thickness, color);
            }
        }

        private static void DrawSmallCross(SpriteBatch sb, Texture2D px, Vector2 center, float size, Color color) {
            float half = size / 2;
            float thickness = 2f;
            sb.Draw(px, center, null, color, 0, new Vector2(0.5f), new Vector2(size, thickness), SpriteEffects.None, 0);
            sb.Draw(px, center, null, color, MathHelper.PiOver2, new Vector2(0.5f), new Vector2(size, thickness), SpriteEffects.None, 0);
        }

        private static void DrawRotatedText(SpriteBatch sb, string text, Vector2 pos, float rotation, Color color, float scale) {
            DynamicSpriteFont font = FontAssets.MouseText.Value;
            Vector2 origin = font.MeasureString(text) / 2f;

            //使用ChatManager绘制带旋转的文本
            ChatManager.DrawColorCodedStringWithShadow(sb, font, text, pos, color, rotation, origin, new Vector2(scale));
        }

        private static Color GetDiscipleColor(int index) {
            return index switch {
                0 => new Color(255, 215, 0),    //彼得：金色
                1 => new Color(100, 149, 237),  //安德鲁：蓝
                2 => new Color(255, 255, 100),  //雅各布：黄
                3 => new Color(200, 200, 255),  //约翰：白蓝
                4 => new Color(255, 255, 200),  //腓力：浅金
                5 => new Color(200, 100, 255),  //巴多罗买：紫
                6 => new Color(255, 165, 0),    //多马：橙
                7 => new Color(255, 215, 100),  //马太：金
                8 => new Color(100, 255, 100),  //雅各：绿
                9 => new Color(255, 200, 255),  //达太：粉
                10 => new Color(255, 100, 100), //西门：红
                11 => new Color(80, 80, 80),    //犹大：灰黑
                _ => Color.White
            };
        }

        private static string GetDiscipleTexturePath(int index) {
            string[] names = [
                "SimonPeter", "Andrew", "James", "John",
                "Philip", "Bartholomew", "Thomas", "Matthew",
                "Lesser", "Jude", "Zealot", "JudasIscariot"
            ];
            return $"CalamityOverhaul/Content/Items/Magic/Elysiums/{names[index]}";
        }

        #endregion
    }

    #region 粒子类

    /// <summary>
    /// 圣光粒子
    /// </summary>
    internal class HolyLightPRT
    {
        public Vector2 Position;
        public Vector2 Target;
        public float Life;
        public float MaxLife;
        public float Phase;
        public Color ParticleColor;

        public HolyLightPRT(Vector2 pos, Vector2 target) {
            Position = pos;
            Target = target;
            MaxLife = Main.rand.Next(60, 100);
            Life = MaxLife;
            Phase = Main.rand.NextFloat(MathHelper.TwoPi);
            ParticleColor = Color.Lerp(new Color(255, 230, 180), new Color(255, 200, 100), Main.rand.NextFloat());
        }

        public bool Update() {
            Life--;
            float t = 1f - Life / MaxLife;

            //缓慢向中心漂移
            Vector2 toTarget = Target - Position;
            if (toTarget.Length() > 5f) {
                Position += toTarget.SafeNormalize(Vector2.Zero) * 0.3f;
            }

            //轻微摆动
            Position += new Vector2((float)Math.Sin(Phase + t * 5f) * 0.3f, (float)Math.Cos(Phase + t * 3f) * 0.2f);

            return Life <= 0;
        }

        public void Draw(SpriteBatch sb, float alpha) {
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow == null) return;

            float lifeRatio = Life / MaxLife;
            float scale = lifeRatio * 0.15f + 0.05f;
            Color drawColor = ParticleColor * (lifeRatio * alpha);
            drawColor.A = 0;

            sb.Draw(glow, Position, null, drawColor, 0, glow.Size() / 2, scale, SpriteEffects.None, 0);
        }
    }

    /// <summary>
    /// 拖动连接线粒子
    /// </summary>
    internal class DragConnectionLine
    {
        public Vector2 Start;
        public Vector2 End;
        public Color LineColor;
        public float Life;
        public float MaxLife;

        public DragConnectionLine(Vector2 start, Vector2 end, Color color) {
            Start = start;
            End = end;
            LineColor = color;
            MaxLife = 15f;
            Life = MaxLife;
        }

        public bool Update() {
            Life--;
            return Life <= 0;
        }

        public void Draw(SpriteBatch sb, float alpha) {
            Texture2D px = CWRAsset.Placeholder_White?.Value;
            if (px == null) return;

            float lifeRatio = Life / MaxLife;
            Color drawColor = LineColor * (lifeRatio * alpha * 0.4f);

            //绘制渐变线段
            Vector2 diff = End - Start;
            float length = diff.Length();
            if (length < 1f) return;

            sb.Draw(px, Start, new Rectangle(0, 0, 1, 1), drawColor * 0.3f, diff.ToRotation(), Vector2.Zero, new Vector2(length, 1.5f * lifeRatio), SpriteEffects.None, 0f);
        }
    }

    #endregion
}
