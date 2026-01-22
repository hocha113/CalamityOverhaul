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
        }

        #endregion

        #region 属性

        public static ElysiumUI Instance => UIHandleLoader.GetUIHandleOfType<ElysiumUI>();

        public override bool Active {
            get {
                //当玩家持有天国极乐时显示UI
                bool holdingElysium = player.HeldItem?.type == ModContent.ItemType<Elysium>();
                return holdingElysium || uiFadeAlpha > 0.01f;
            }
        }

        #endregion

        #region 更新逻辑

        public override void Update() {
            bool holdingElysium = player.HeldItem?.type == ModContent.ItemType<Elysium>();

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

            //更新粒子
            UpdateParticles();
        }

        private void UpdateHovering() {
            hoveringDiscipleIndex = -1;
            Vector2 mousePos = new(Main.mouseX, Main.mouseY);

            for (int i = 0; i < 12; i++) {
                float angle = MathHelper.TwoPi * i / 12f - MathHelper.PiOver2 + rotationAngle;
                Vector2 slotPos = DrawPosition + angle.ToRotationVector2() * DiscipleRadius;

                if (Vector2.Distance(mousePos, slotPos) < DiscipleSlotSize / 2f + 5f) {
                    hoveringDiscipleIndex = i;
                    player.mouseInterface = true;
                    break;
                }
            }
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
            if (hoveringDiscipleIndex >= 0) {
                DrawDiscipleTooltip(spriteBatch, alpha);
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
            Color outerGlow = new Color(255, 230, 180, 0);
            sb.Draw(glow, DrawPosition, null, outerGlow, 0, glow.Size() / 2, OuterRadius / 32f * 1.5f, SpriteEffects.None, 0);

            //中层金色光晕
            Color midGlow = new Color(255, 200, 100, 0);
            sb.Draw(glow, DrawPosition, null, midGlow, 0, glow.Size() / 2, OuterRadius / 32f, SpriteEffects.None, 0);

            //内层白色光晕
            Color innerGlow = Color.White with { A = 0 };
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

                //槽位背景
                float slotPulse = (float)Math.Sin(pulseTimer + i * 0.5f) * 0.1f + 0.9f;
                Color bgColor;

                if (hasDisciple) {
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

                if (isHovering) {
                    bgColor = Color.Lerp(bgColor, new Color(60, 50, 35), 0.4f);
                }

                //绘制槽位圆形背景
                DrawFilledCircle(sb, px, slotPos, DiscipleSlotSize / 2f, bgColor * alpha);

                //槽位边框
                Color borderColor;
                if (hasDisciple) {
                    borderColor = GetDiscipleColor(i);
                    if (isHovering) borderColor = Color.Lerp(borderColor, Color.White, 0.3f);
                }
                else {
                    borderColor = new Color(80, 70, 50) * 0.6f;
                }
                DrawCircleOutline(sb, px, slotPos, DiscipleSlotSize / 2f, 2f, borderColor * alpha);

                //门徒图标或空缺标记
                if (hasDisciple) {
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

                    sb.Draw(discipleTex, slotPos, null, iconColor, 0, discipleTex.Size() / 2, iconScale, SpriteEffects.None, 0);

                    //光晕效果
                    if (glow != null) {
                        Color glowColor = GetDiscipleColor(i) * (alpha * 0.3f * slotPulse);
                        glowColor.A = 0;
                        sb.Draw(glow, slotPos, null, glowColor, 0, glow.Size() / 2, DiscipleSlotSize / 32f, SpriteEffects.None, 0);
                    }
                }
                else {
                    //空缺标记(十字虚线)
                    Color emptyColor = new Color(60, 50, 40) * (alpha * 0.5f);
                    float crossSize = 12f;
                    sb.Draw(px, slotPos, null, emptyColor, 0, new Vector2(0.5f), new Vector2(crossSize, 2f), SpriteEffects.None, 0);
                    sb.Draw(px, slotPos, null, emptyColor, MathHelper.PiOver2, new Vector2(0.5f), new Vector2(crossSize, 2f), SpriteEffects.None, 0);
                }

                //序号标记
                string numText = (i + 1).ToString();
                Vector2 numPos = slotPos + new Vector2(0, DiscipleSlotSize / 2f + 8f);
                Color numColor = hasDisciple ? new Color(200, 180, 140) : new Color(100, 90, 70);
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

    #endregion
}
