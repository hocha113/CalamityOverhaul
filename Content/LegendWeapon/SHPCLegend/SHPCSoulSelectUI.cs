using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using ThoriumMod.Projectiles.Enemy;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend
{
    /// <summary>
    /// SHPC魂魄选择UI
    /// 科幻风格的六边形环绕选择界面，手持SHPC时按住Shift展开
    /// </summary>
    internal class SHPCSoulSelectUI : UIHandle, ILocalizedModType
    {
        public string LocalizationCategory => "UI";

        #region 常量

        private const float SlotRadius = 120f;
        private const float SlotSize = 40f;
        private const float CenterRadius = 35f;

        #endregion

        #region 本地化

        public static LocalizedText TitleText { get; private set; }
        public static LocalizedText SoulNameLight { get; private set; }
        public static LocalizedText SoulNameNight { get; private set; }
        public static LocalizedText SoulNameFlight { get; private set; }
        public static LocalizedText SoulNameMight { get; private set; }
        public static LocalizedText SoulNameSight { get; private set; }
        public static LocalizedText SoulNameFright { get; private set; }
        public static LocalizedText TooltipSoulPrefix { get; private set; }
        public static LocalizedText StatusActive { get; private set; }
        public static LocalizedText StatusClickToSelect { get; private set; }

        private static LocalizedText[] SoulNameTexts;

        public override void SetStaticDefaults() {
            TitleText = this.GetLocalization(nameof(TitleText), () => "SOUL CORE");
            SoulNameLight = this.GetLocalization(nameof(SoulNameLight), () => "Light");
            SoulNameNight = this.GetLocalization(nameof(SoulNameNight), () => "Night");
            SoulNameFlight = this.GetLocalization(nameof(SoulNameFlight), () => "Flight");
            SoulNameMight = this.GetLocalization(nameof(SoulNameMight), () => "Might");
            SoulNameSight = this.GetLocalization(nameof(SoulNameSight), () => "Sight");
            SoulNameFright = this.GetLocalization(nameof(SoulNameFright), () => "Fright");
            TooltipSoulPrefix = this.GetLocalization(nameof(TooltipSoulPrefix), () => "Soul of ");
            StatusActive = this.GetLocalization(nameof(StatusActive), () => "[ACTIVE]");
            StatusClickToSelect = this.GetLocalization(nameof(StatusClickToSelect), () => "[CLICK TO SELECT]");
            SoulNameTexts = [SoulNameLight, SoulNameNight, SoulNameFlight, SoulNameMight, SoulNameSight, SoulNameFright];
        }

        #endregion

        #region 魂魄数据

        /// <summary>
        /// 六种魂魄的物品ID
        /// </summary>
        private static readonly int[] SoulItemIDs = [
            ItemID.SoulofLight,
            ItemID.SoulofNight,
            ItemID.SoulofFlight,
            ItemID.SoulofMight,
            ItemID.SoulofSight,
            ItemID.SoulofFright
        ];

        /// <summary>
        /// 六种魂魄对应的颜色
        /// </summary>
        private static readonly Color[] SoulColors = [
            new Color(240, 29, 196),
            new Color(123, 29, 220),
            new Color(106, 240, 250),
            new Color(4, 51, 222),
            new Color(79, 255, 124),
            new Color(255, 96, 20)
        ];

        #endregion

        #region 字段

        private float uiFadeAlpha;
        private float rotationAngle;
        private float pulseTimer;
        private float glowTimer;
        private float scanLinePhase;
        private int hoveringIndex = -1;
        private int selectedIndex;
        private bool wasHoldingSHPC;
        private float selectionFlash;
        private readonly List<SoulParticle> particles = [];
        private int particleSpawnTimer;

        //每个槽位的悬停动画进度
        private readonly float[] slotHoverAnim = new float[6];
        //每个槽位的图标浮动相位
        private readonly float[] slotIconPhase = new float[6];
        //外环慢速旋转角度
        private float outerRingRotation;
        //选择波纹动画
        private float selectionRipple;
        private int selectionRippleIndex = -1;
        //能量脉冲流动相位
        private float energyFlowPhase;
        //中心图标旋转角度
        private float centerIconRotation;

        #endregion

        #region 属性

        public static SHPCSoulSelectUI Instance => UIHandleLoader.GetUIHandleOfType<SHPCSoulSelectUI>();

        public override bool Active {
            get {
                bool holdingSHPC = IsHoldingSHPC() && Main.keyState.PressingShift();
                return holdingSHPC || uiFadeAlpha > 0.01f;
            }
        }

        #endregion

        #region 工具方法

        private static bool IsHoldingSHPC() {
            Player p = Main.LocalPlayer;
            Item item = p.HeldItem;
            return item != null && item.type == CWRID.Item_SHPC && item.type > ItemID.None;
        }

        /// <summary>
        /// 通过物品ID查找对应的魂魄索引
        /// </summary>
        private static int FindSoulIndex(int soulType) {
            for (int i = 0; i < SoulItemIDs.Length; i++) {
                if (SoulItemIDs[i] == soulType) return i;
            }
            return 0;
        }

        #endregion

        #region 更新逻辑

        public override void Update() {
            bool holdingSHPC = IsHoldingSHPC() && Main.keyState.PressingShift();

            if (holdingSHPC && !wasHoldingSHPC) {
                SoundEngine.PlaySound(SoundID.Item4 with { Volume = 0.4f, Pitch = 0.5f });
                selectedIndex = FindSoulIndex(SHPCOverride.SelectedSoulType);
            }
            wasHoldingSHPC = holdingSHPC;

            if (holdingSHPC) {
                uiFadeAlpha = Math.Min(1f, uiFadeAlpha + 0.1f);
            }
            else {
                uiFadeAlpha = Math.Max(0f, uiFadeAlpha - 0.08f);
            }

            if (uiFadeAlpha < 0.01f) return;

            rotationAngle += 0.005f;
            pulseTimer += 0.04f;
            glowTimer += 0.03f;
            scanLinePhase += 0.02f;
            outerRingRotation += 0.002f;
            energyFlowPhase += 0.035f;
            centerIconRotation = MathF.Sin(glowTimer * 0.8f) * 0.06f;
            if (selectionFlash > 0) selectionFlash -= 0.06f;
            if (selectionRipple > 0) selectionRipple -= 0.025f;

            if (rotationAngle > MathHelper.TwoPi) rotationAngle -= MathHelper.TwoPi;
            if (pulseTimer > MathHelper.TwoPi) pulseTimer -= MathHelper.TwoPi;
            if (glowTimer > MathHelper.TwoPi) glowTimer -= MathHelper.TwoPi;
            if (scanLinePhase > MathHelper.TwoPi) scanLinePhase -= MathHelper.TwoPi;
            if (outerRingRotation > MathHelper.TwoPi) outerRingRotation -= MathHelper.TwoPi;
            if (energyFlowPhase > MathHelper.TwoPi) energyFlowPhase -= MathHelper.TwoPi;

            DrawPosition = new Vector2(Main.screenWidth / 2f, Main.screenHeight - 200);

            UpdateHovering();
            UpdateSlotAnimations();
            UpdateClick();
            UpdateParticles();
        }

        private void UpdateHovering() {
            hoveringIndex = -1;
            Vector2 mousePos = new(Main.mouseX, Main.mouseY);

            for (int i = 0; i < 6; i++) {
                Vector2 slotPos = GetSlotPosition(i);
                if (Vector2.Distance(mousePos, slotPos) < SlotSize / 2f + 6f) {
                    hoveringIndex = i;
                    player.mouseInterface = true;
                    break;
                }
            }
        }

        private void UpdateSlotAnimations() {
            float hoverSpeed = 0.15f;
            for (int i = 0; i < 6; i++) {
                float target = i == hoveringIndex ? 1f : 0f;
                slotHoverAnim[i] += (target - slotHoverAnim[i]) * hoverSpeed;
                slotIconPhase[i] += 0.03f + i * 0.005f;
            }
        }

        private void UpdateClick() {
            if (hoveringIndex < 0) return;
            if (!Main.mouseLeft || !Main.mouseLeftRelease) return;

            if (hoveringIndex != selectedIndex) {
                selectedIndex = hoveringIndex;
                selectionFlash = 1f;
                selectionRipple = 1f;
                selectionRippleIndex = selectedIndex;
                SHPCOverride.SelectedSoulType = SoulItemIDs[selectedIndex];
                //同步设置到手持的SHPC物品上
                Item heldItem = Main.LocalPlayer.HeldItem;
                if (heldItem != null && heldItem.type == CWRID.Item_SHPC) {
                    CWRRef.SetSHPCStoredSoulType(heldItem, SoulItemIDs[selectedIndex]);
                }
                SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.6f, Pitch = 0.3f });
                //生成选择确认粒子
                SpawnSelectionParticles(GetSlotPosition(selectedIndex), SoulColors[selectedIndex]);
            }
        }

        private Vector2 GetSlotPosition(int index) {
            float angle = MathHelper.TwoPi * index / 6f - MathHelper.PiOver2;
            return DrawPosition + angle.ToRotationVector2() * SlotRadius;
        }

        private void UpdateParticles() {
            particleSpawnTimer++;
            if (particleSpawnTimer >= 8 && particles.Count < 40) {
                particleSpawnTimer = 0;
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                float dist = Main.rand.NextFloat(CenterRadius, SlotRadius + 30f);
                Vector2 pos = DrawPosition + angle.ToRotationVector2() * dist;
                Color c = SoulColors[selectedIndex] * 0.6f;
                particles.Add(new SoulParticle(pos, DrawPosition, c));
            }

            for (int i = particles.Count - 1; i >= 0; i--) {
                if (particles[i].Update()) {
                    particles.RemoveAt(i);
                }
            }
        }

        private void SpawnSelectionParticles(Vector2 center, Color color) {
            for (int i = 0; i < 16; i++) {
                float angle = MathHelper.TwoPi * i / 16f;
                Vector2 pos = center + angle.ToRotationVector2() * Main.rand.NextFloat(5f, 25f);
                particles.Add(new SoulParticle(pos, center, color) { MaxLife = 35, Life = 35 });
            }
        }

        #endregion

        #region 绘制

        public override void Draw(SpriteBatch spriteBatch) {
            if (uiFadeAlpha < 0.01f) return;
            float alpha = uiFadeAlpha;

            DrawBackgroundEffect(spriteBatch, alpha);
            DrawHexRing(spriteBatch, alpha);
            DrawScanLines(spriteBatch, alpha);
            DrawConnectorLines(spriteBatch, alpha);
            DrawSelectionRipple(spriteBatch, alpha);
            DrawSoulSlots(spriteBatch, alpha);
            DrawCenterDisplay(spriteBatch, alpha);

            foreach (var p in particles) {
                p.Draw(spriteBatch, alpha * 0.8f);
            }

            if (hoveringIndex >= 0) {
                DrawHoverTooltip(spriteBatch, alpha);
            }
        }

        /// <summary>
        /// 绘制背景扫描光效
        /// </summary>
        private void DrawBackgroundEffect(SpriteBatch sb, float alpha) {
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow == null) return;

            float pulse = MathF.Sin(pulseTimer) * 0.1f + 0.9f;
            Color outerGlow = SoulColors[selectedIndex] * (alpha * 0.15f * pulse);
            outerGlow.A = 0;
            sb.Draw(glow, DrawPosition, null, outerGlow, 0, glow.Size() / 2, (SlotRadius + 60f) / 32f, SpriteEffects.None, 0);

            //第二层背景光晕，颜色更淡，范围更大
            Color wideGlow = SoulColors[selectedIndex] * (alpha * 0.06f);
            wideGlow.A = 0;
            sb.Draw(glow, DrawPosition, null, wideGlow, 0, glow.Size() / 2, (SlotRadius + 100f) / 32f, SpriteEffects.None, 0);

            Color innerGlow = Color.White * (alpha * 0.08f);
            innerGlow.A = 0;
            sb.Draw(glow, DrawPosition, null, innerGlow, 0, glow.Size() / 2, CenterRadius / 32f * 1.5f, SpriteEffects.None, 0);
        }

        /// <summary>
        /// 绘制六边形外环
        /// </summary>
        private void DrawHexRing(SpriteBatch sb, float alpha) {
            Texture2D px = CWRAsset.Placeholder_White?.Value;
            if (px == null) return;

            Color ringColor = SoulColors[selectedIndex] * (alpha * 0.4f);
            Color dimRingColor = new Color(80, 100, 120) * (alpha * 0.25f);

            //外六边形（带慢速旋转）
            for (int i = 0; i < 6; i++) {
                float angle1 = MathHelper.TwoPi * i / 6f - MathHelper.PiOver2 + outerRingRotation;
                float angle2 = MathHelper.TwoPi * ((i + 1) % 6) / 6f - MathHelper.PiOver2 + outerRingRotation;

                Vector2 p1 = DrawPosition + angle1.ToRotationVector2() * (SlotRadius + 30f);
                Vector2 p2 = DrawPosition + angle2.ToRotationVector2() * (SlotRadius + 30f);
                DrawLine(sb, px, p1, p2, 1.5f, ringColor);
            }

            //第二层外环（更大、更淡，反向旋转）
            Color faintRingColor = SoulColors[selectedIndex] * (alpha * 0.12f);
            for (int i = 0; i < 6; i++) {
                float angle1 = MathHelper.TwoPi * i / 6f - MathHelper.PiOver2 - outerRingRotation * 0.7f;
                float angle2 = MathHelper.TwoPi * ((i + 1) % 6) / 6f - MathHelper.PiOver2 - outerRingRotation * 0.7f;

                Vector2 p1 = DrawPosition + angle1.ToRotationVector2() * (SlotRadius + 45f);
                Vector2 p2 = DrawPosition + angle2.ToRotationVector2() * (SlotRadius + 45f);
                DrawLine(sb, px, p1, p2, 1f, faintRingColor);
            }

            //内六边形
            for (int i = 0; i < 6; i++) {
                float angle1 = MathHelper.TwoPi * i / 6f - MathHelper.PiOver2 + rotationAngle;
                float angle2 = MathHelper.TwoPi * ((i + 1) % 6) / 6f - MathHelper.PiOver2 + rotationAngle;

                Vector2 p1 = DrawPosition + angle1.ToRotationVector2() * (CenterRadius + 10f);
                Vector2 p2 = DrawPosition + angle2.ToRotationVector2() * (CenterRadius + 10f);
                DrawLine(sb, px, p1, p2, 1f, dimRingColor);
            }

            //装饰用小圆点（带光晕）
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            for (int i = 0; i < 6; i++) {
                float angle = MathHelper.TwoPi * i / 6f - MathHelper.PiOver2 + outerRingRotation;
                Vector2 cornerPos = DrawPosition + angle.ToRotationVector2() * (SlotRadius + 30f);
                float dotPulse = MathF.Sin(glowTimer + i * 0.8f) * 0.3f + 0.7f;
                Color dotColor = ringColor * dotPulse;
                sb.Draw(px, cornerPos, new Rectangle(0, 0, 1, 1), dotColor, 0, new Vector2(0.5f), new Vector2(4f), SpriteEffects.None, 0);

                if (glow != null) {
                    Color dotGlow = SoulColors[selectedIndex] * (alpha * 0.15f * dotPulse);
                    dotGlow.A = 0;
                    sb.Draw(glow, cornerPos, null, dotGlow, 0, glow.Size() / 2, 0.12f, SpriteEffects.None, 0);
                }
            }
        }

        /// <summary>
        /// 绘制扫描线效果
        /// </summary>
        private void DrawScanLines(SpriteBatch sb, float alpha) {
            Texture2D px = CWRAsset.Placeholder_White?.Value;
            if (px == null) return;

            //主扫描线
            float scanY = DrawPosition.Y + MathF.Sin(scanLinePhase) * (SlotRadius + 20f);
            Color scanColor = SoulColors[selectedIndex] * (alpha * 0.1f);
            float lineWidth = (SlotRadius + 40f) * 2f;
            sb.Draw(px, new Vector2(DrawPosition.X - lineWidth / 2f, scanY), new Rectangle(0, 0, 1, 1), scanColor, 0, Vector2.Zero, new Vector2(lineWidth, 1.5f), SpriteEffects.None, 0);

            //第二条扫描线（反向、延迟）
            float scanY2 = DrawPosition.Y + MathF.Sin(scanLinePhase + MathHelper.Pi * 0.7f) * (SlotRadius + 15f);
            Color scanColor2 = SoulColors[selectedIndex] * (alpha * 0.05f);
            sb.Draw(px, new Vector2(DrawPosition.X - lineWidth / 2f, scanY2), new Rectangle(0, 0, 1, 1), scanColor2, 0, Vector2.Zero, new Vector2(lineWidth, 1f), SpriteEffects.None, 0);
        }

        /// <summary>
        /// 绘制中心到各槽位的连接线（带能量脉冲流动）
        /// </summary>
        private void DrawConnectorLines(SpriteBatch sb, float alpha) {
            Texture2D px = CWRAsset.Placeholder_White?.Value;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (px == null) return;

            for (int i = 0; i < 6; i++) {
                Vector2 slotPos = GetSlotPosition(i);
                bool isSelected = i == selectedIndex;
                bool isHovering = i == hoveringIndex;

                float lineAlpha = isSelected ? 0.5f : (isHovering ? 0.35f : 0.12f);
                Color lineColor = isSelected ? SoulColors[i] : new Color(80, 100, 120);
                lineColor *= alpha * lineAlpha;

                //虚线连接
                Vector2 dir = (slotPos - DrawPosition).SafeNormalize(Vector2.UnitX);
                float totalDist = Vector2.Distance(DrawPosition, slotPos);
                float dashLen = 8f;
                float gapLen = 5f;
                float traveled = CenterRadius + 15f;

                while (traveled < totalDist - SlotSize / 2f) {
                    Vector2 dashStart = DrawPosition + dir * traveled;
                    float dashEnd = Math.Min(traveled + dashLen, totalDist - SlotSize / 2f);
                    Vector2 dashEndPos = DrawPosition + dir * dashEnd;
                    DrawLine(sb, px, dashStart, dashEndPos, 1f, lineColor);
                    traveled += dashLen + gapLen;
                }

                //选中线上的能量脉冲流动光点
                if (isSelected && glow != null) {
                    float flowDist = totalDist - CenterRadius - SlotSize / 2f;
                    for (int p = 0; p < 3; p++) {
                        float t = ((energyFlowPhase / MathHelper.TwoPi + p / 3f) % 1f);
                        float pointDist = CenterRadius + 15f + t * flowDist;
                        Vector2 pointPos = DrawPosition + dir * pointDist;
                        float pointAlpha = MathF.Sin(t * MathHelper.Pi) * 0.6f;
                        Color pointColor = SoulColors[i] * (alpha * pointAlpha);
                        pointColor.A = 0;
                        sb.Draw(glow, pointPos, null, pointColor, 0, glow.Size() / 2, 0.08f, SpriteEffects.None, 0);
                    }
                }
            }
        }

        /// <summary>
        /// 绘制选中时的波纹扩散效果
        /// </summary>
        private void DrawSelectionRipple(SpriteBatch sb, float alpha) {
            if (selectionRipple <= 0 || selectionRippleIndex < 0) return;

            Texture2D px = CWRAsset.Placeholder_White?.Value;
            if (px == null) return;

            Vector2 rippleCenter = GetSlotPosition(selectionRippleIndex);
            float maxRadius = SlotSize * 1.5f;
            float progress = 1f - selectionRipple;
            float radius = maxRadius * CWRUtils.EaseOutCubic(progress);
            float rippleAlpha = selectionRipple * 0.6f;
            Color rippleColor = SoulColors[selectionRippleIndex] * (alpha * rippleAlpha);

            DrawHexOutline(sb, px, rippleCenter, radius, 1.5f * selectionRipple, rippleColor);
        }

        /// <summary>
        /// 绘制六个魂魄槽位
        /// </summary>
        private void DrawSoulSlots(SpriteBatch sb, float alpha) {
            Texture2D px = CWRAsset.Placeholder_White?.Value;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (px == null) return;

            for (int i = 0; i < 6; i++) {
                Vector2 slotPos = GetSlotPosition(i);
                bool isSelected = i == selectedIndex;
                bool isHovering = i == hoveringIndex;
                float hover = slotHoverAnim[i];

                float slotPulse = MathF.Sin(pulseTimer + i * 0.7f) * 0.08f + 0.92f;
                //悬停时槽位整体放大
                float slotScale = 1f + hover * 0.12f;
                float scaledSlotHalf = SlotSize / 2f * slotScale;

                //槽位背景
                Color bgColor;
                if (isSelected) {
                    float flashMult = selectionFlash > 0 ? (1f + selectionFlash * 0.5f) : 1f;
                    bgColor = Color.Lerp(new Color(20, 22, 28), SoulColors[i], 0.2f * flashMult) * slotPulse;
                }
                else if (isHovering) {
                    bgColor = Color.Lerp(new Color(20, 22, 28), SoulColors[i], 0.15f);
                }
                else {
                    bgColor = new Color(15, 17, 22) * 0.9f;
                }

                DrawHexSlot(sb, px, slotPos, scaledSlotHalf, bgColor * alpha);

                //槽位边框
                Color borderColor = isSelected
                    ? Color.Lerp(SoulColors[i], Color.White, 0.2f + hover * 0.1f)
                    : (isHovering ? Color.Lerp(SoulColors[i], Color.White, 0.15f) : new Color(60, 70, 85));
                float borderThickness = isSelected ? 2.5f : (isHovering ? 2f + hover * 0.5f : 1.5f);
                DrawHexOutline(sb, px, slotPos, scaledSlotHalf, borderThickness, borderColor * alpha);

                //选中标记高亮
                if (isSelected && glow != null) {
                    float glowPulse = MathF.Sin(glowTimer * 2f) * 0.2f + 0.8f;
                    Color glowColor = SoulColors[i] * (alpha * 0.4f * glowPulse);
                    glowColor.A = 0;
                    sb.Draw(glow, slotPos, null, glowColor, 0, glow.Size() / 2, SlotSize / 28f * slotScale, SpriteEffects.None, 0);
                }

                //悬停时额外的外层光晕
                if (hover > 0.01f && glow != null) {
                    Color hoverGlow = SoulColors[i] * (alpha * 0.2f * hover);
                    hoverGlow.A = 0;
                    sb.Draw(glow, slotPos, null, hoverGlow, 0, glow.Size() / 2, SlotSize / 22f * slotScale, SpriteEffects.None, 0);
                }

                //绘制魂魄物品图标（带浮动和微旋转）
                Main.instance.LoadItem(SoulItemIDs[i]);
                Texture2D itemTex = TextureAssets.Item[SoulItemIDs[i]].Value;
                float iconScale = (SlotSize - 16f) / Math.Max(itemTex.Width, itemTex.Height) * slotScale;
                float iconFloat = MathF.Sin(slotIconPhase[i]) * 1.5f;
                float iconRot = isHovering ? MathF.Sin(slotIconPhase[i] * 1.3f) * 0.08f * hover : 0f;
                Vector2 iconPos = slotPos + new Vector2(0, iconFloat);
                Color iconColor = isSelected ? Color.White : (isHovering ? Color.Lerp(Color.White * 0.7f, Color.White, hover) : Color.White * 0.5f);
                VaultUtils.SimpleDrawItem(sb, SoulItemIDs[selectedIndex], iconPos, 32, iconScale * 3, iconRot, iconColor);

                //魂魄名称标签
                string name = SoulNameTexts[i].Value;
                Vector2 nameSize = FontAssets.MouseText.Value.MeasureString(name) * 0.35f;
                Vector2 namePos = slotPos + new Vector2(0, scaledSlotHalf + 10f) - nameSize / 2;
                Color nameColor = isSelected ? SoulColors[i] : (isHovering ? Color.Lerp(new Color(100, 110, 130), Color.White, hover * 0.7f) : new Color(100, 110, 130));
                Utils.DrawBorderString(sb, name, namePos, nameColor * alpha, 0.35f);
            }
        }

        /// <summary>
        /// 绘制中心显示区域
        /// </summary>
        private void DrawCenterDisplay(SpriteBatch sb, float alpha) {
            Texture2D px = CWRAsset.Placeholder_White?.Value;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (px == null) return;

            //中心六边形背景
            Color centerBg = new Color(12, 14, 18) * (alpha * 0.95f);
            DrawHexSlot(sb, px, DrawPosition, CenterRadius, centerBg);

            //中心边框
            float borderPulse = MathF.Sin(pulseTimer * 1.5f) * 0.15f + 0.85f;
            Color centerBorder = SoulColors[selectedIndex] * (alpha * 0.6f * borderPulse);
            DrawHexOutline(sb, px, DrawPosition, CenterRadius, 2f, centerBorder);

            //中心光效（双层，增强光感）
            if (glow != null) {
                float centerPulse = MathF.Sin(glowTimer * 1.5f) * 0.15f + 0.85f;
                Color centerGlow = SoulColors[selectedIndex] * (alpha * 0.25f * centerPulse);
                centerGlow.A = 0;
                sb.Draw(glow, DrawPosition, null, centerGlow, 0, glow.Size() / 2, CenterRadius / 24f, SpriteEffects.None, 0);

                Color centerGlow2 = SoulColors[selectedIndex] * (alpha * 0.1f);
                centerGlow2.A = 0;
                sb.Draw(glow, DrawPosition, null, centerGlow2, centerIconRotation * 2f, glow.Size() / 2, CenterRadius / 18f, SpriteEffects.None, 0);
            }

            //绘制当前选中的魂魄图标(较大，带呼吸缩放和微旋转)
            Main.instance.LoadItem(SoulItemIDs[selectedIndex]);
            Texture2D selectedTex = TextureAssets.Item[SoulItemIDs[selectedIndex]].Value;
            float centerIconScale = (CenterRadius * 1.2f) / Math.Max(selectedTex.Width, selectedTex.Height);
            float iconPulse = MathF.Sin(glowTimer * 2f) * 0.05f + 1f;
            VaultUtils.SimpleDrawItem(Main.spriteBatch, SoulItemIDs[selectedIndex], DrawPosition, 32, centerIconScale * iconPulse * 4, 0, Color.White * alpha);
            //标题文字（带光晕描边）
            string title = TitleText.Value;
            Vector2 titleSize = FontAssets.MouseText.Value.MeasureString(title) * 0.4f;
            Vector2 titlePos = DrawPosition - new Vector2(0, CenterRadius + 18f) - titleSize / 2;
            Color titleColor = SoulColors[selectedIndex] * (alpha * 0.9f);

            float titleGlow = 0.3f + MathF.Sin(glowTimer * 1.5f) * 0.2f;
            Color titleGlowColor = SoulColors[selectedIndex] * (alpha * titleGlow * 0.3f);
            titleGlowColor.A = 0;
            for (int g = 0; g < 4; g++) {
                float angle = MathHelper.TwoPi * g / 4f;
                Vector2 offset = angle.ToRotationVector2() * 1.5f;
                Utils.DrawBorderString(sb, title, titlePos + offset, titleGlowColor, 0.4f);
            }
            Utils.DrawBorderString(sb, title, titlePos, titleColor, 0.4f);
        }

        /// <summary>
        /// 绘制悬停提示信息
        /// </summary>
        private void DrawHoverTooltip(SpriteBatch sb, float alpha) {
            Texture2D px = CWRAsset.Placeholder_White?.Value;
            if (px == null || hoveringIndex < 0) return;

            int idx = hoveringIndex;
            bool isCurrentSelection = idx == selectedIndex;

            Vector2 mousePos = new(Main.mouseX, Main.mouseY);
            Vector2 tipPos = mousePos + new Vector2(18, 18);

            string soulName = TooltipSoulPrefix.Value + SoulNameTexts[idx].Value;
            string status = isCurrentSelection ? StatusActive.Value : StatusClickToSelect.Value;

            float maxWidth = Math.Max(
                FontAssets.MouseText.Value.MeasureString(soulName).X * 0.5f,
                FontAssets.MouseText.Value.MeasureString(status).X * 0.4f
            );
            float tipWidth = maxWidth + 20f;
            float tipHeight = 45f;

            if (tipPos.X + tipWidth > Main.screenWidth) tipPos.X = mousePos.X - tipWidth - 10;
            if (tipPos.Y + tipHeight > Main.screenHeight) tipPos.Y = mousePos.Y - tipHeight - 10;

            Rectangle tipRect = new((int)tipPos.X, (int)tipPos.Y, (int)tipWidth, (int)tipHeight);

            //背景
            Color bgColor = new Color(8, 10, 14) * (alpha * 0.95f);
            sb.Draw(px, tipRect, null, bgColor);

            //边框（带脉冲）
            float borderPulse = MathF.Sin(glowTimer * 2f) * 0.15f + 0.85f;
            Color borderColor = SoulColors[idx] * (alpha * 0.7f * borderPulse);
            sb.Draw(px, new Rectangle(tipRect.X, tipRect.Y, tipRect.Width, 2), null, borderColor);
            sb.Draw(px, new Rectangle(tipRect.X, tipRect.Bottom - 1, tipRect.Width, 1), null, borderColor * 0.5f);
            sb.Draw(px, new Rectangle(tipRect.X, tipRect.Y, 1, tipRect.Height), null, borderColor * 0.6f);
            sb.Draw(px, new Rectangle(tipRect.Right - 1, tipRect.Y, 1, tipRect.Height), null, borderColor * 0.6f);

            //文字
            float y = tipPos.Y + 6;
            Utils.DrawBorderString(sb, soulName, new Vector2(tipPos.X + 8, y), SoulColors[idx] * alpha, 0.5f);
            y += 18;
            Color statusColor = isCurrentSelection ? new Color(100, 255, 140) : new Color(180, 190, 210);
            Utils.DrawBorderString(sb, status, new Vector2(tipPos.X + 8, y), statusColor * alpha, 0.4f);
        }

        #endregion

        #region 辅助绘制

        private static void DrawLine(SpriteBatch sb, Texture2D px, Vector2 start, Vector2 end, float thickness, Color color) {
            Vector2 diff = end - start;
            float length = diff.Length();
            if (length < 1f) return;
            sb.Draw(px, start, new Rectangle(0, 0, 1, 1), color, diff.ToRotation(), Vector2.Zero, new Vector2(length, thickness), SpriteEffects.None, 0f);
        }

        /// <summary>
        /// 绘制六边形填充
        /// </summary>
        private static void DrawHexSlot(SpriteBatch sb, Texture2D px, Vector2 center, float radius, Color color) {
            for (int i = 0; i < 6; i++) {
                float a1 = MathHelper.TwoPi * i / 6f - MathHelper.PiOver2;
                float a2 = MathHelper.TwoPi * ((i + 1) % 6) / 6f - MathHelper.PiOver2;
                Vector2 p1 = center + a1.ToRotationVector2() * radius;
                Vector2 p2 = center + a2.ToRotationVector2() * radius;
                DrawTriangleFill(sb, px, center, p1, p2, color);
            }
        }

        /// <summary>
        /// 绘制六边形边框
        /// </summary>
        private static void DrawHexOutline(SpriteBatch sb, Texture2D px, Vector2 center, float radius, float thickness, Color color) {
            for (int i = 0; i < 6; i++) {
                float a1 = MathHelper.TwoPi * i / 6f - MathHelper.PiOver2;
                float a2 = MathHelper.TwoPi * ((i + 1) % 6) / 6f - MathHelper.PiOver2;
                Vector2 p1 = center + a1.ToRotationVector2() * radius;
                Vector2 p2 = center + a2.ToRotationVector2() * radius;
                DrawLine(sb, px, p1, p2, thickness, color);
            }
        }

        private static void DrawTriangleFill(SpriteBatch sb, Texture2D px, Vector2 p1, Vector2 p2, Vector2 p3, Color color) {
            int steps = 8;
            for (int i = 0; i <= steps; i++) {
                float t = i / (float)steps;
                Vector2 start = Vector2.Lerp(p1, p2, t);
                Vector2 end = Vector2.Lerp(p1, p3, t);
                DrawLine(sb, px, start, end, 2f, color);
            }
        }

        #endregion
    }

    /// <summary>
    /// SHPC魂魄选择UI的数据流粒子
    /// </summary>
    internal class SoulParticle
    {
        public Vector2 Position;
        public Vector2 Target;
        public float Life;
        public float MaxLife;
        public Color ParticleColor;
        private readonly float phase;

        public SoulParticle(Vector2 position, Vector2 target, Color color) {
            Position = position;
            Target = target;
            ParticleColor = color;
            MaxLife = 50 + Main.rand.Next(30);
            Life = MaxLife;
            phase = Main.rand.NextFloat(MathHelper.TwoPi);
        }

        /// <summary>
        /// 返回true表示粒子已死亡需要移除
        /// </summary>
        public bool Update() {
            Life--;
            if (Life <= 0) return true;

            float progress = 1f - Life / MaxLife;
            Vector2 toTarget = (Target - Position).SafeNormalize(Vector2.Zero);
            Position += toTarget * (0.5f + progress * 2f);
            Position += new Vector2(MathF.Sin(phase + progress * 5f), MathF.Cos(phase + progress * 5f)) * 0.3f;
            return false;
        }

        public void Draw(SpriteBatch sb, float alpha) {
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow == null) return;

            float progress = 1f - Life / MaxLife;
            float fadeAlpha = progress < 0.3f ? progress / 0.3f : (progress > 0.7f ? (1f - progress) / 0.3f : 1f);
            float scale = (0.15f + MathF.Sin(progress * MathHelper.Pi) * 0.1f);
            Color drawColor = ParticleColor * (alpha * fadeAlpha * 0.6f);
            drawColor.A = 0;
            sb.Draw(glow, Position, null, drawColor, 0, glow.Size() / 2, scale, SpriteEffects.None, 0);
        }
    }
}
