using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Magic.Elysiums.Disciples;
using CalamityOverhaul.Content.UIs.UIEffect;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.UI.Chat;

namespace CalamityOverhaul.Content.Items.Magic.Elysiums.UI
{
    /// <summary>
    /// 天国极乐·玫瑰窗：手持权杖按住Shift呼出的门徒圣位转盘。
    /// 背景与槽位辉光由 ElysiumHalo 着色器承担，前景为程序化线稿：
    /// 拉丁铭文环、圣徽、悬停详情；拖拽两个席位可行圣职调换
    /// </summary>
    internal class ElysiumRoseWindow : UIHandle, ILocalizedModType
    {
        public string LocalizationCategory => "UI";

        //布局(UI空间px)
        private const float OuterRadius = 200f;
        private const float DiscipleRadius = 140f;
        private const float InnerRadius = 60f;
        private const float SlotHitRadius = 30f;

        //拉丁铭文(席位序)
        private static readonly string[] LatinNames = [
            "PETRUS", "ANDREAS", "IACOBUS", "IOANNES", "PHILIPPUS", "BARTHOLOMAEUS",
            "THOMAS", "MATTHAEUS", "IACOBUS MIN", "THADDAEUS", "SIMON", "IUDAS"
        ];

        private static LocalizedText TitleText;
        private static LocalizedText SeatAliveText;
        private static LocalizedText SeatEmptyText;
        private static LocalizedText SeatMartyredText;
        private static LocalizedText MartyrdomPowerText;
        private static LocalizedText DragHintText;
        private static LocalizedText[] AbilityBriefTexts;

        //动画
        private float fade;
        private float wheelRotation;
        private float inscriptionRotation;
        private float pulseTimer;

        //交互
        private int hoverSeat = -1;
        private bool dragging;
        private int dragSource = -1;
        private int dragTarget = -1;
        private bool wasMouseDown;

        private static float UIScreenW => PlayerInput.RealScreenWidth / Main.UIScale;
        private static float UIScreenH => PlayerInput.RealScreenHeight / Main.UIScale;

        private static Player LocalPlayer => Main.LocalPlayer;

        public override void SetStaticDefaults() {
            TitleText = this.GetLocalization(nameof(TitleText), () => "天国极乐");
            SeatAliveText = this.GetLocalization(nameof(SeatAliveText), () => "在职");
            SeatEmptyText = this.GetLocalization(nameof(SeatEmptyText), () => "空缺");
            SeatMartyredText = this.GetLocalization(nameof(SeatMartyredText), () => "已殉道");
            MartyrdomPowerText = this.GetLocalization(nameof(MartyrdomPowerText), () => "殉道之力 {0}/11");
            DragHintText = this.GetLocalization(nameof(DragHintText), () => "拖动席位可行圣职调换");

            AbilityBriefTexts = new LocalizedText[ElysiumPlayer.SeatCount];
            string[] defaultBriefs = [
                "圣盾就绪时替你挡下一击的部分伤害",
                "向敌群撒下光网，拖缓网中敌人",
                "连锁圣雷在至多四个敌人间跳跃",
                "注视敌人，令其受到的一切伤害提高",
                "祝圣你的弹幕，使其缓缓折向敌人",
                "掷刃剥离敌人护甲并使其显形",
                "令你的攻击在一段时间内必然暴击",
                "祝福敌人，其死亡时迸出奉献金雨",
                "你带伤时献上治愈",
                "降下随机的奇迹",
                "点燃狂热，近旁敌人染上圣焰",
                "全面的厚礼，与背叛的刀"];
            for (int i = 0; i < ElysiumPlayer.SeatCount; i++) {
                string brief = defaultBriefs[i];
                AbilityBriefTexts[i] = this.GetLocalization($"AbilityBrief_{i}", () => brief);
            }
        }

        private static bool WantsOpen
            => LocalPlayer.active && !LocalPlayer.dead
            && LocalPlayer.HeldItem?.type == ModContent.ItemType<Elysium>()
            && Main.keyState.PressingShift();

        public override bool Active => WantsOpen || fade > 0.01f;

        public override void Update() {
            if (WantsOpen) {
                if (fade <= 0.01f) {
                    SoundEngine.PlaySound(SoundID.MenuOpen with { Volume = 0.6f, Pitch = 0.3f });
                }
                fade = Math.Min(1f, fade + 0.08f);
            }
            else {
                fade = Math.Max(0f, fade - 0.06f);
                dragging = false;
                dragSource = -1;
            }
            if (fade < 0.01f) {
                return;
            }

            wheelRotation += 0.002f;
            inscriptionRotation -= 0.0028f;
            pulseTimer += 0.03f;

            DrawPosition = new Vector2(UIScreenW * 0.5f, UIScreenH - OuterRadius - 46f);

            UpdateInteraction();
        }

        private void UpdateInteraction() {
            if (!LocalPlayer.TryGetModPlayer(out ElysiumPlayer ep)) {
                return;
            }

            Vector2 mouse = new(Main.mouseX, Main.mouseY);
            bool mouseDown = Main.mouseLeft;

            //悬停判定
            int hovered = -1;
            for (int i = 0; i < ElysiumPlayer.SeatCount; i++) {
                if (Vector2.Distance(mouse, GetSlotPos(i)) < SlotHitRadius) {
                    hovered = i;
                    break;
                }
            }
            hoverSeat = hovered;
            if (hovered >= 0 || dragging) {
                player.mouseInterface = true;
            }

            //拖拽起手：只允许从已转化未殉道的席位拖起
            if (mouseDown && !wasMouseDown && !dragging && hovered >= 0
                && ep.SeatConverted[hovered] && !ep.Martyred[hovered]) {
                dragging = true;
                dragSource = hovered;
                SoundEngine.PlaySound(SoundID.MenuTick with { Volume = 0.6f, Pitch = 0.2f });
            }

            if (dragging) {
                dragTarget = -1;
                for (int i = 0; i < ElysiumPlayer.SeatCount; i++) {
                    if (i != dragSource && Vector2.Distance(mouse, GetSlotPos(i)) < SlotHitRadius + 8f) {
                        dragTarget = i;
                        break;
                    }
                }
            }

            //拖拽收手：落在有效席位上则调换
            if (!mouseDown && wasMouseDown && dragging) {
                dragging = false;
                if (dragTarget >= 0 && ep.SwapSeats(dragSource, dragTarget)) {
                    SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.8f, Pitch = 0.4f });
                }
                else {
                    SoundEngine.PlaySound(SoundID.MenuClose with { Volume = 0.4f });
                }
                dragSource = -1;
                dragTarget = -1;
            }

            wasMouseDown = mouseDown;
        }

        private Vector2 GetSlotPos(int seat) {
            float angle = MathHelper.TwoPi * seat / ElysiumPlayer.SeatCount - MathHelper.PiOver2 + wheelRotation;
            return DrawPosition + angle.ToRotationVector2() * DiscipleRadius;
        }

        #region 绘制
        public override void Draw(SpriteBatch spriteBatch) {
            if (fade < 0.01f || !LocalPlayer.TryGetModPlayer(out ElysiumPlayer ep)) {
                return;
            }

            spriteBatch.End();
            DrawHaloBackground(spriteBatch, ep);
            DrawSlotAuras(spriteBatch, ep);
            DrawCenterPanel(spriteBatch);
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                SamplerState.LinearClamp, DepthStencilState.None,
                RasterizerState.CullCounterClockwise, null, Main.UIScaleMatrix);

            DrawInscriptionRing(spriteBatch);
            DrawSeats(spriteBatch, ep);
            DrawCenterText(spriteBatch, ep);
            if (dragging) {
                DrawDragGhost(spriteBatch);
            }
            else if (hoverSeat >= 0) {
                DrawHoverPanel(spriteBatch, ep, hoverSeat);
            }
        }

        private void DrawHaloBackground(SpriteBatch sb, ElysiumPlayer ep) {
            Effect effect = EffectLoader.ElysiumHalo?.Value;
            Texture2D canvas = VaultAsset.placeholder2?.Value;
            Texture2D noise = CWRAsset.Extra_193?.Value;
            if (effect == null || canvas == null || noise == null) {
                return;
            }

            float canvasSize = (OuterRadius + 40f) * 2f;
            float halfCanvas = canvasSize * 0.5f;

            effect.CurrentTechnique = effect.Techniques["HaloBackground"];
            effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            effect.Parameters["fadeAlpha"]?.SetValue(fade);
            effect.Parameters["discipleRatio"]?.SetValue(ep.AliveDiscipleCount / (float)ElysiumPlayer.SeatCount);
            effect.Parameters["rotationAngle"]?.SetValue(wheelRotation);
            effect.Parameters["pulsePhase"]?.SetValue(pulseTimer);
            effect.Parameters["hoverSector"]?.SetValue((float)hoverSeat);
            effect.Parameters["outerR"]?.SetValue(OuterRadius / halfCanvas * 0.5f);
            effect.Parameters["discipleR"]?.SetValue(DiscipleRadius / halfCanvas * 0.5f);
            effect.Parameters["innerR"]?.SetValue(InnerRadius / halfCanvas * 0.5f);
            SetPalette(effect);

            Main.graphics.GraphicsDevice.Textures[1] = noise;
            Main.graphics.GraphicsDevice.SamplerStates[1] = SamplerState.LinearWrap;

            sb.Begin(SpriteSortMode.Immediate, BlendState.Additive,
                SamplerState.LinearClamp, DepthStencilState.None,
                RasterizerState.CullNone, null, Main.UIScaleMatrix);
            effect.CurrentTechnique.Passes[0].Apply();
            sb.Draw(canvas, DrawPosition, null, Color.White, 0f,
                canvas.Size() * 0.5f, canvasSize, SpriteEffects.None, 0f);
            sb.End();
        }

        private void DrawSlotAuras(SpriteBatch sb, ElysiumPlayer ep) {
            Effect effect = EffectLoader.ElysiumHalo?.Value;
            Texture2D canvas = VaultAsset.placeholder2?.Value;
            if (effect == null || canvas == null) {
                return;
            }

            effect.CurrentTechnique = effect.Techniques["SlotAura"];
            SetPalette(effect);

            sb.Begin(SpriteSortMode.Immediate, BlendState.Additive,
                SamplerState.LinearClamp, DepthStencilState.None,
                RasterizerState.CullNone, null, Main.UIScaleMatrix);

            const float auraSize = 74f;
            for (int i = 0; i < ElysiumPlayer.SeatCount; i++) {
                DiscipleDef def = DiscipleCatalog.Get(i);
                effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
                effect.Parameters["fadeAlpha"]?.SetValue(fade);
                effect.Parameters["slotColor"]?.SetValue(def.BodyColor.ToVector3());
                effect.Parameters["slotActive"]?.SetValue(ep.IsSeatAlive(i) ? 1f : 0f);
                effect.Parameters["slotHover"]?.SetValue(hoverSeat == i && !dragging ? 1f : 0f);
                effect.Parameters["slotDragSource"]?.SetValue(dragging && dragSource == i ? 1f : 0f);
                effect.Parameters["slotDragTarget"]?.SetValue(dragging && dragTarget == i ? 1f : 0f);
                effect.Parameters["slotPhase"]?.SetValue(i * 0.5f);
                effect.CurrentTechnique.Passes[0].Apply();
                sb.Draw(canvas, GetSlotPos(i), null, Color.White, 0f,
                    canvas.Size() * 0.5f, auraSize, SpriteEffects.None, 0f);
            }
            sb.End();
        }

        private void DrawCenterPanel(SpriteBatch sb) {
            Effect effect = EffectLoader.ElysiumHalo?.Value;
            Texture2D canvas = VaultAsset.placeholder2?.Value;
            if (effect == null || canvas == null) {
                return;
            }

            effect.CurrentTechnique = effect.Techniques["CenterPanel"];
            effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            effect.Parameters["fadeAlpha"]?.SetValue(fade);
            effect.Parameters["pulsePhase"]?.SetValue(pulseTimer);
            SetPalette(effect);

            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend,
                SamplerState.LinearClamp, DepthStencilState.None,
                RasterizerState.CullNone, null, Main.UIScaleMatrix);
            effect.CurrentTechnique.Passes[0].Apply();
            sb.Draw(canvas, DrawPosition, null, Color.White, 0f,
                canvas.Size() * 0.5f, (InnerRadius + 15f) * 2f, SpriteEffects.None, 0f);
            sb.End();
        }

        private static void SetPalette(Effect effect) {
            effect.Parameters["warmGold"]?.SetValue(new Vector3(1f, 0.863f, 0.588f));
            effect.Parameters["brightGold"]?.SetValue(new Vector3(1f, 0.784f, 0.392f));
            effect.Parameters["holyWhite"]?.SetValue(new Vector3(1f, 0.98f, 0.94f));
        }

        /// <summary>拉丁铭文环：反向缓转的圣名</summary>
        private void DrawInscriptionRing(SpriteBatch sb) {
            float radius = OuterRadius - 16f;
            for (int i = 0; i < ElysiumPlayer.SeatCount; i++) {
                float angle = MathHelper.TwoPi * i / ElysiumPlayer.SeatCount - MathHelper.PiOver2 + inscriptionRotation;
                Vector2 pos = DrawPosition + angle.ToRotationVector2() * radius;
                string label = LatinNames[i];
                Vector2 size = FontAssets.MouseText.Value.MeasureString(label) * 0.5f;
                Color color = new Color(220, 200, 150) * (0.5f * fade);
                ChatManager.DrawColorCodedStringWithShadow(sb, FontAssets.MouseText.Value, label,
                    pos - size * 0.5f, color, angle + MathHelper.PiOver2, Vector2.Zero, Vector2.One * 0.5f);
            }
        }

        /// <summary>席位圣徽：在职亮色、空缺暗淡轮廓、殉道者金色并覆十字</summary>
        private void DrawSeats(SpriteBatch sb, ElysiumPlayer ep) {
            for (int i = 0; i < ElysiumPlayer.SeatCount; i++) {
                Vector2 pos = GetSlotPos(i);
                DrawSeatGlyph(sb, ep, i, pos, 15f, fade);
            }
        }

        private void DrawSeatGlyph(SpriteBatch sb, ElysiumPlayer ep, int seat, Vector2 pos, float scale, float alpha) {
            DiscipleDef def = DiscipleCatalog.Get(seat);
            SvgPath path = SvgPathPen.Path(def.EmblemPath);
            if (path == null) {
                return;
            }

            bool martyred = ep.Martyred[seat];
            bool alive = ep.IsSeatAlive(seat);
            bool converted = ep.SeatConverted[seat];

            Color glyphColor;
            float glyphAlpha;
            if (martyred) {
                float pulse = 0.8f + 0.2f * MathF.Sin(pulseTimer * 2f + seat * 0.5f);
                glyphColor = new Color(255, 216, 120) * pulse;
                glyphAlpha = 0.95f;
            }
            else if (alive || converted) {
                glyphColor = def.AccentColor;
                glyphAlpha = 0.95f;
            }
            else {
                glyphColor = def.BodyColor * 0.4f;
                glyphAlpha = 0.45f;
            }

            SvgPathPen.Stroke(sb, path, pos, scale, 0f, glyphColor with { A = 0 } * (glyphAlpha * alpha)
                , 1.4f, glyphAlpha * alpha
                , core: alive || martyred ? Color.White with { A = 0 } * (0.5f * alpha) : null);

            //殉道十字覆盖
            if (martyred) {
                Texture2D px = VaultAsset.placeholder2?.Value;
                if (px != null) {
                    Color crossColor = new Color(255, 226, 140) with { A = 0 } * (0.9f * alpha);
                    sb.Draw(px, pos + new Vector2(-1f, -12f), new Rectangle(0, 0, 1, 1), crossColor
                        , 0f, Vector2.Zero, new Vector2(2f, 24f), SpriteEffects.None, 0f);
                    sb.Draw(px, pos + new Vector2(-8f, -5f), new Rectangle(0, 0, 1, 1), crossColor
                        , 0f, Vector2.Zero, new Vector2(16f, 2f), SpriteEffects.None, 0f);
                }
            }
        }

        private void DrawCenterText(SpriteBatch sb, ElysiumPlayer ep) {
            string title = TitleText.Value;
            Vector2 titleSize = FontAssets.MouseText.Value.MeasureString(title) * 0.82f;
            ChatManager.DrawColorCodedStringWithShadow(sb, FontAssets.MouseText.Value, title,
                DrawPosition - new Vector2(titleSize.X * 0.5f, 26f), new Color(255, 236, 190) * fade,
                0f, Vector2.Zero, Vector2.One * 0.82f);

            string count = $"{ep.AliveDiscipleCount}/{ElysiumPlayer.SeatCount}";
            Vector2 countSize = FontAssets.MouseText.Value.MeasureString(count) * 0.7f;
            ChatManager.DrawColorCodedStringWithShadow(sb, FontAssets.MouseText.Value, count,
                DrawPosition - new Vector2(countSize.X * 0.5f, -2f), new Color(220, 208, 175) * fade,
                0f, Vector2.Zero, Vector2.One * 0.7f);

            int energy = ep.MartyrdomEnergy;
            if (energy > 0) {
                string power = MartyrdomPowerText.Format(energy);
                Vector2 powerSize = FontAssets.MouseText.Value.MeasureString(power) * 0.62f;
                Color powerColor = Color.Lerp(new Color(180, 160, 120), new Color(255, 224, 130), energy / 11f);
                ChatManager.DrawColorCodedStringWithShadow(sb, FontAssets.MouseText.Value, power,
                    DrawPosition - new Vector2(powerSize.X * 0.5f, -22f), powerColor * fade,
                    0f, Vector2.Zero, Vector2.One * 0.62f);
            }

            //底部拖拽提示
            string hint = DragHintText.Value;
            Vector2 hintSize = FontAssets.MouseText.Value.MeasureString(hint) * 0.56f;
            ChatManager.DrawColorCodedStringWithShadow(sb, FontAssets.MouseText.Value, hint,
                new Vector2(DrawPosition.X - hintSize.X * 0.5f, DrawPosition.Y + OuterRadius + 14f),
                new Color(170, 155, 125) * (0.7f * fade), 0f, Vector2.Zero, Vector2.One * 0.56f);
        }

        /// <summary>拖拽随行徽影与牵引线</summary>
        private void DrawDragGhost(SpriteBatch sb) {
            if (dragSource < 0) {
                return;
            }
            Vector2 mouse = new(Main.mouseX, Main.mouseY);
            DiscipleDef def = DiscipleCatalog.Get(dragSource);

            Texture2D px = VaultAsset.placeholder2?.Value;
            if (px != null) {
                Vector2 from = GetSlotPos(dragSource);
                Vector2 seg = mouse - from;
                float len = seg.Length();
                if (len > 2f) {
                    sb.Draw(px, from, new Rectangle(0, 0, 1, 1), def.AccentColor with { A = 0 } * (0.45f * fade)
                        , seg.ToRotation(), new Vector2(0f, 0.5f), new Vector2(len, 1.6f), SpriteEffects.None, 0f);
                }
            }

            SvgPath path = SvgPathPen.Path(def.EmblemPath);
            if (path != null) {
                SvgPathPen.Stroke(sb, path, mouse, 16f, 0f, def.AccentColor with { A = 0 } * (0.9f * fade)
                    , 1.5f, 0.9f * fade, core: Color.White with { A = 0 } * (0.55f * fade));
            }
        }

        /// <summary>悬停详情：门徒名、席位状态、能力一句</summary>
        private void DrawHoverPanel(SpriteBatch sb, ElysiumPlayer ep, int seat) {
            DiscipleDef def = DiscipleCatalog.Get(seat);
            string name = Elysium.DiscipleNameTexts[seat].Value;
            string state = ep.Martyred[seat] ? SeatMartyredText.Value
                : ep.IsSeatAlive(seat) ? SeatAliveText.Value : SeatEmptyText.Value;
            string brief = AbilityBriefTexts[seat].Value;

            Vector2 mouse = new(Main.mouseX + 18, Main.mouseY + 6);
            var font = FontAssets.MouseText.Value;
            Vector2 nameSize = font.MeasureString(name) * 0.8f;
            Vector2 stateSize = font.MeasureString(state) * 0.62f;
            Vector2 briefSize = font.MeasureString(brief) * 0.66f;
            float panelW = MathF.Max(MathF.Max(nameSize.X, briefSize.X + stateSize.X + 12f), 90f) + 20f;
            float panelH = nameSize.Y + briefSize.Y + 16f;

            Texture2D px = VaultAsset.placeholder2?.Value;
            if (px != null) {
                var rect = new Rectangle((int)mouse.X, (int)mouse.Y, (int)panelW, (int)panelH);
                sb.Draw(px, rect, new Rectangle(0, 0, 1, 1), new Color(16, 13, 8) * (0.88f * fade));
                Color border = def.BodyColor * (0.8f * fade);
                sb.Draw(px, new Rectangle(rect.X, rect.Y, rect.Width, 1), border);
                sb.Draw(px, new Rectangle(rect.X, rect.Bottom - 1, rect.Width, 1), border);
                sb.Draw(px, new Rectangle(rect.X, rect.Y, 1, rect.Height), border);
                sb.Draw(px, new Rectangle(rect.Right - 1, rect.Y, 1, rect.Height), border);
            }

            ChatManager.DrawColorCodedStringWithShadow(sb, font, name,
                mouse + new Vector2(10f, 6f), def.AccentColor * fade, 0f, Vector2.Zero, Vector2.One * 0.8f);
            ChatManager.DrawColorCodedStringWithShadow(sb, font, state,
                mouse + new Vector2(10f + nameSize.X + 8f, 10f), new Color(200, 190, 165) * fade,
                0f, Vector2.Zero, Vector2.One * 0.62f);
            ChatManager.DrawColorCodedStringWithShadow(sb, font, brief,
                mouse + new Vector2(10f, 8f + nameSize.Y), new Color(225, 214, 185) * fade,
                0f, Vector2.Zero, Vector2.One * 0.66f);
        }
        #endregion
    }
}
