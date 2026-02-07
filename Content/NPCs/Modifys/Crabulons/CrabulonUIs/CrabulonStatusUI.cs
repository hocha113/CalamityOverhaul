using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Tools;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.Modifys.Crabulons.CrabulonUIs
{
    /// <summary>
    /// 菌生蟹状态面板UI，荧光蘑菇自然风格
    /// 包含：蹲下/跟随切换、远程呼叫、鞍具管理、血量与饱食度数据
    /// </summary>
    internal class CrabulonStatusUI : UIHandle
    {
        //核心状态
        private ModifyCrabulon modify;
        private bool _shouldBeOpen;
        private float sengs;

        //面板动画
        private float globalTime;
        private float breatheAnim;
        private float shimmerPhase;
        private float panelSlideOffset = 40f;

        //粒子系统
        private readonly List<SporeParticle> sporeParticles = [];
        private float sporeSpawnTimer;

        //按钮悬停动画
        private float crouchHoverAnim;
        private float recallHoverAnim;
        private float saddleHoverAnim;
        private float releaseHoverAnim;
        private float crouchPressAnim;
        private float recallPressAnim;
        private float saddlePressAnim;
        private float releasePressAnim;

        //按钮区域
        private Rectangle crouchButtonRect;
        private Rectangle recallButtonRect;
        private Rectangle releaseButtonRect;
        private Rectangle saddleSlotRect;
        private bool hoveringCrouch;
        private bool hoveringRecall;
        private bool hoveringRelease;
        private bool hoveringSaddle;

        //血量条动画
        private float healthBarAnim;
        private float feedBarAnim;
        private int oldLife = -1;
        private float healthFlashTimer;

        //布局常量
        private const float PanelWidth = 340f;
        private const float PanelHeight = 210f;
        private const float Padding = 14f;
        private const float ButtonHeight = 28f;
        private const float ButtonWidth = 82f;
        private const float BarHeight = 12f;
        private const float SaddleSlotSize = 44f;
        private const float CornerRadius = 10f;

        //荧光蘑菇主题色
        private static readonly Color MushroomCyan = new(0, 200, 220);
        private static readonly Color MushroomBlue = new(30, 120, 200);
        private static readonly Color MushroomGreen = new(50, 210, 130);
        private static readonly Color MushroomDarkBg = new(12, 18, 28);
        private static readonly Color MushroomMidBg = new(18, 30, 45);
        private static readonly Color MushroomBorderColor = new(40, 140, 180);
        private static readonly Color MushroomTextColor = new(180, 240, 255);
        private static readonly Color MushroomHighlight = new(100, 255, 220);

        //孢子粒子结构
        private struct SporeParticle
        {
            public Vector2 Position;
            public Vector2 Velocity;
            public float Life;
            public float MaxLife;
            public float Size;
            public float Rotation;
            public float RotationSpeed;
            public Color BaseColor;
        }

        public override bool Active {
            get {
                _shouldBeOpen = FindClosestCrabulon() && !modify.Mount;
                return _shouldBeOpen || sengs > 0.01f;
            }
        }

        private bool FindClosestCrabulon() {
            if (player == null) {
                return false;
            }

            if (!player.TryGetOverride<CrabulonPlayer>(out var crabulonPlayer)) {
                modify = null;
                return false;
            }

            if (crabulonPlayer == null) {
                return false;
            }

            List<ModifyCrabulon> modifys = crabulonPlayer.ModifyCrabulons;
            ModifyCrabulon closestModify = null;
            float minDistSq = 90000f;

            foreach (var hover in modifys) {
                if (hover is null || !hover.npc.Alives() || !hover.Owner.Alives() || hover.Owner.whoAmI != player.whoAmI) {
                    continue;
                }

                float distSq = hover.npc.DistanceSQ(player.Center);
                if (distSq < minDistSq) {
                    minDistSq = distSq;
                    closestModify = hover;
                }
            }

            modify = closestModify;
            return modify != null;
        }

        public override void Update() {
            globalTime += 0.016f;

            sengs = MathHelper.Lerp(sengs, _shouldBeOpen ? 1f : 0f, 0.12f);
            if (sengs < 0.01f) {
                sengs = 0f;
                sporeParticles.Clear();
                return;
            }

            //面板滑入
            float targetSlide = _shouldBeOpen ? 0f : 40f;
            panelSlideOffset += (targetSlide - panelSlideOffset) * 0.12f;

            //呼吸与流光
            breatheAnim = MathF.Sin(globalTime * 1.8f) * 0.5f + 0.5f;
            shimmerPhase = globalTime * 1.5f;

            //面板位置：屏幕底部居中偏下
            Vector2 panelCenter = new(Main.screenWidth / 2f, Main.screenHeight - PanelHeight / 2f - 30f + panelSlideOffset);
            DrawPosition = panelCenter - new Vector2(PanelWidth, PanelHeight) / 2f;
            Size = new Vector2(PanelWidth, PanelHeight);
            UIHitBox = new Rectangle((int)DrawPosition.X, (int)DrawPosition.Y, (int)PanelWidth, (int)PanelHeight);
            hoverInMainPage = UIHitBox.Intersects(MouseHitBox);

            if (hoverInMainPage && sengs > 0.5f) {
                player.mouseInterface = true;
            }

            //按钮区域计算
            float btnY = DrawPosition.Y + PanelHeight - Padding - ButtonHeight;
            float btnStartX = DrawPosition.X + Padding;
            float btnSpacing = 6f;

            crouchButtonRect = new Rectangle((int)btnStartX, (int)btnY, (int)ButtonWidth, (int)ButtonHeight);
            recallButtonRect = new Rectangle((int)(btnStartX + ButtonWidth + btnSpacing), (int)btnY, (int)ButtonWidth, (int)ButtonHeight);
            releaseButtonRect = new Rectangle((int)(btnStartX + (ButtonWidth + btnSpacing) * 2), (int)btnY, (int)ButtonWidth, (int)ButtonHeight);

            //鞍具槽位在右侧
            float saddleX = DrawPosition.X + PanelWidth - Padding - SaddleSlotSize;
            float saddleY = DrawPosition.Y + PanelHeight - Padding - SaddleSlotSize;
            saddleSlotRect = new Rectangle((int)saddleX, (int)saddleY, (int)SaddleSlotSize, (int)SaddleSlotSize);

            //悬停检测
            hoveringCrouch = crouchButtonRect.Contains(MouseHitBox) && sengs > 0.5f;
            hoveringRecall = recallButtonRect.Contains(MouseHitBox) && sengs > 0.5f;
            hoveringRelease = releaseButtonRect.Contains(MouseHitBox) && sengs > 0.5f;
            hoveringSaddle = saddleSlotRect.Contains(MouseHitBox) && sengs > 0.5f;

            //按钮悬停动画
            float hoverSpeed = 0.15f;
            crouchHoverAnim += ((hoveringCrouch ? 1f : 0f) - crouchHoverAnim) * hoverSpeed;
            recallHoverAnim += ((hoveringRecall ? 1f : 0f) - recallHoverAnim) * hoverSpeed;
            releaseHoverAnim += ((hoveringRelease ? 1f : 0f) - releaseHoverAnim) * hoverSpeed;
            saddleHoverAnim += ((hoveringSaddle ? 1f : 0f) - saddleHoverAnim) * hoverSpeed;
            crouchPressAnim *= 0.85f;
            recallPressAnim *= 0.85f;
            releasePressAnim *= 0.85f;
            saddlePressAnim *= 0.85f;

            //血量条平滑
            if (modify != null && modify.npc.Alives()) {
                float targetHealth = modify.npc.lifeMax > 0 ? (float)modify.npc.life / modify.npc.lifeMax : 0f;
                healthBarAnim += (targetHealth - healthBarAnim) * 0.1f;

                float maxFeed = CrabulonConstants.MaxFeedValue;
                float targetFeed = Math.Clamp(modify.FeedValue / maxFeed, 0f, 1f);
                feedBarAnim += (targetFeed - feedBarAnim) * 0.1f;

                if (oldLife == -1) oldLife = modify.npc.life;
                if (modify.npc.life < oldLife) healthFlashTimer = 20f;
                oldLife = modify.npc.life;
            }
            if (healthFlashTimer > 0) healthFlashTimer--;

            //点击处理
            if (keyLeftPressState == KeyPressState.Pressed && sengs > 0.8f && modify != null) {
                if (hoveringCrouch && !modify.Mount) {
                    crouchPressAnim = 1f;
                    SoundEngine.PlaySound(CWRSound.ButtonZero);
                    modify.Crouch = !modify.Crouch;
                    modify.SendNetWork();
                }
                else if (hoveringRecall && !modify.Mount) {
                    recallPressAnim = 1f;
                    OnRecall();
                }
                else if (hoveringRelease && !modify.Mount) {
                    releasePressAnim = 1f;
                    OnRelease();
                }
                else if (hoveringSaddle) {
                    saddlePressAnim = 1f;
                    OnSaddleInteract();
                }
            }

            //粒子更新
            UpdateSporeParticles();
            if (sengs > 0.3f && _shouldBeOpen) {
                sporeSpawnTimer += 1f;
                if (sporeSpawnTimer > 5f) {
                    SpawnSporeParticle();
                    sporeSpawnTimer = 0f;
                }
            }
        }

        private void OnRecall() {
            if (modify == null || !modify.npc.Alives() || !modify.Owner.Alives()) return;

            SoundEngine.PlaySound(SoundID.Item8 with { Volume = 0.6f, Pitch = 0.3f });
            modify.npc.Center = player.Center + new Vector2(0, CrabulonConstants.TeleportSpawnHeight);
            modify.npc.netUpdate = true;

            for (int i = 0; i < 60; i++) {
                Vector2 dustPos = modify.npc.Bottom + new Vector2(Main.rand.NextFloat(-modify.npc.width, modify.npc.width), 0);
                int dust = Dust.NewDust(dustPos, 4, 4, DustID.BlueFairy, 0f, -2f, 100, default, 1.5f);
                Main.dust[dust].velocity *= 0.5f;
                Main.dust[dust].velocity.Y *= 300f / Main.rand.NextFloat(160, 230);
            }
        }

        private void OnSaddleInteract() {
            if (modify == null) return;

            Item heldItem = player.GetItem();

            //手持鞍具→装上
            if (heldItem.type == ModContent.ItemType<MushroomSaddle>()) {
                if (modify.SaddleItem.Alives()) {
                    //交换：旧鞍具掉落
                    VaultUtils.SpwanItem(modify.npc.FromObjectGetParent(), modify.npc.Top, new Vector2(32), modify.SaddleItem);
                    modify.SaddleItem.TurnToAir();
                }
                modify.SaddleItem = heldItem.Clone();
                heldItem.TurnToAir();
                SoundEngine.PlaySound(CWRSound.PutSaddle, player.Center);
                modify.SendNetWork();
            }
            //手上为空或非鞍具→卸鞍
            else if (modify.SaddleItem.Alives()) {
                //如果正在骑乘，先下马
                if (modify.Mount) {
                    modify.CloseMount();
                }
                VaultUtils.SpwanItem(modify.npc.FromObjectGetParent(), modify.npc.Top, new Vector2(32), modify.SaddleItem);
                modify.SaddleItem.TurnToAir();
                SoundEngine.PlaySound(SoundID.Grab with { Volume = 0.6f });
                modify.SendNetWork();
            }
        }

        private void OnRelease() {
            if (modify == null || !modify.npc.Alives()) return;

            string name = modify.npc.GivenOrTypeName;
            modify.ReleaseTame();
            SoundEngine.PlaySound(SoundID.NPCDeath1 with { Volume = 0.5f, Pitch = -0.3f });

            string message = string.Format(ModifyCrabulon.ReleasedText.Value, name);
            CombatText.NewText(player.Hitbox, new Color(100, 200, 255), message);
        }

        #region 粒子系统

        private void UpdateSporeParticles() {
            for (int i = sporeParticles.Count - 1; i >= 0; i--) {
                var p = sporeParticles[i];
                p.Life -= 0.016f;
                p.Position += p.Velocity;
                p.Velocity *= 0.97f;
                p.Velocity.Y -= 0.015f;
                p.Velocity.X += MathF.Sin(globalTime * 3f + p.Rotation) * 0.01f;
                p.Rotation += p.RotationSpeed;
                sporeParticles[i] = p;
                if (p.Life <= 0f) sporeParticles.RemoveAt(i);
            }
        }

        private void SpawnSporeParticle() {
            if (sporeParticles.Count > 25) return;
            var p = new SporeParticle {
                Position = new Vector2(
                    DrawPosition.X + Main.rand.NextFloat(Size.X),
                    DrawPosition.Y + Size.Y
                ),
                Velocity = new Vector2(Main.rand.NextFloat(-0.2f, 0.2f), Main.rand.NextFloat(-1.2f, -0.4f)),
                Life = Main.rand.NextFloat(1.5f, 3f),
                MaxLife = 0f,
                Size = Main.rand.NextFloat(2f, 4f),
                Rotation = Main.rand.NextFloat(MathHelper.TwoPi),
                RotationSpeed = Main.rand.NextFloat(-0.03f, 0.03f),
                BaseColor = Color.Lerp(MushroomCyan, MushroomGreen, Main.rand.NextFloat())
            };
            p.MaxLife = p.Life;
            sporeParticles.Add(p);
        }

        #endregion

        #region 绘制

        public override void Draw(SpriteBatch spriteBatch) {
            if (sengs <= 0f || modify == null) return;

            float alpha = Math.Min(sengs * 1.5f, 1f);

            DrawBackdrop(spriteBatch, alpha * 0.35f);
            DrawSporeParticles(spriteBatch, alpha);
            DrawPanel(spriteBatch, alpha);
            DrawContent(spriteBatch, alpha);
            DrawHoverInfo(spriteBatch, alpha, 1f);
        }

        private void DrawBackdrop(SpriteBatch spriteBatch, float alpha) {
            Texture2D pixel = CWRAsset.Placeholder_White.Value;
            int gradientHeight = 260;
            int startY = Main.screenHeight - gradientHeight;
            for (int i = 0; i < gradientHeight; i += 2) {
                float t = i / (float)gradientHeight;
                float gradientAlpha = t * t * alpha;
                Rectangle line = new(0, startY + i, Main.screenWidth, 2);
                spriteBatch.Draw(pixel, line, new Rectangle(0, 0, 1, 1), Color.Black * gradientAlpha);
            }
        }

        private void DrawSporeParticles(SpriteBatch spriteBatch, float alpha) {
            Texture2D softGlow = CWRAsset.SoftGlow.Value;
            foreach (var p in sporeParticles) {
                float lifeRatio = p.Life / p.MaxLife;
                float particleAlpha = lifeRatio * alpha * 0.6f;
                float size = p.Size * (0.5f + lifeRatio * 0.5f);
                Color color = p.BaseColor with { A = 0 } * particleAlpha;
                spriteBatch.Draw(softGlow, p.Position, null, color, p.Rotation,
                    softGlow.Size() / 2f, size * 0.06f, SpriteEffects.None, 0f);
            }
        }

        private void DrawPanel(SpriteBatch spriteBatch, float alpha) {
            Rectangle panelRect = UIHitBox;

            //阴影
            for (int i = 3; i >= 1; i--) {
                Rectangle shadowRect = panelRect;
                shadowRect.Offset(i, i * 2);
                DrawRoundedRect(spriteBatch, shadowRect, Color.Black * (alpha * 0.12f * (4 - i) / 3f), CornerRadius + i);
            }

            //背景渐变
            DrawGradientRoundedRect(spriteBatch, panelRect, MushroomDarkBg * (alpha * 0.95f), MushroomMidBg * (alpha * 0.95f), CornerRadius);

            //内发光
            float innerGlow = 0.08f + breatheAnim * 0.06f;
            DrawInnerGlow(spriteBatch, panelRect, MushroomCyan * (alpha * innerGlow), CornerRadius, 16);

            //流光边框
            DrawAnimatedBorder(spriteBatch, panelRect, alpha);

            //顶部高光条
            Rectangle highlightBar = new(panelRect.X + 16, panelRect.Y + 2, panelRect.Width - 32, 2);
            float highlightAlpha = 0.3f + breatheAnim * 0.15f;
            DrawHorizontalGradient(spriteBatch, highlightBar,
                Color.Transparent, MushroomCyan * (alpha * highlightAlpha), Color.Transparent);

            //角落菌丝装饰
            DrawCornerMycelium(spriteBatch, panelRect, alpha);
        }

        private void DrawContent(SpriteBatch spriteBatch, float alpha) {
            if (!modify.npc.Alives()) return;

            float contentAlpha = alpha * Math.Clamp(sengs * 2f - 0.5f, 0f, 1f);
            if (contentAlpha < 0.01f) return;

            //标题区
            DrawTitle(spriteBatch, contentAlpha);

            //分割线
            Vector2 divStart = DrawPosition + new Vector2(Padding, 32);
            Vector2 divEnd = divStart + new Vector2(PanelWidth - Padding * 2, 0);
            DrawAnimatedDivider(spriteBatch, divStart, divEnd, contentAlpha);

            //血量条
            DrawHealthBar(spriteBatch, contentAlpha);

            //饱食度条
            DrawFeedBar(spriteBatch, contentAlpha);

            //按钮
            if (!modify.Mount) {
                string crouchLabel = modify.Crouch ? ModifyCrabulon.CrouchAltText.Value : ModifyCrabulon.CrouchText.Value;
                DrawMushroomButton(spriteBatch, crouchButtonRect, crouchLabel, crouchHoverAnim, crouchPressAnim, contentAlpha, MushroomCyan);

                DrawMushroomButton(spriteBatch, recallButtonRect, ModifyCrabulon.RecallText.Value, recallHoverAnim, recallPressAnim, contentAlpha, MushroomGreen);

                DrawMushroomButton(spriteBatch, releaseButtonRect, ModifyCrabulon.ReleaseText.Value, releaseHoverAnim, releasePressAnim, contentAlpha, new Color(200, 80, 80));
            }

            //鞍具槽
            DrawSaddleSlot(spriteBatch, contentAlpha);
        }

        private void DrawTitle(SpriteBatch spriteBatch, float alpha) {
            Vector2 titlePos = DrawPosition + new Vector2(Padding, 10);
            string title = "Crabulon";
            if (modify.npc.Alives()) {
                title = modify.npc.GivenOrTypeName;
            }

            //标题光晕
            float titleGlow = 0.4f + breatheAnim * 0.3f;
            Color glowColor = MushroomCyan with { A = 0 } * (alpha * titleGlow * 0.3f);
            for (int i = 0; i < 6; i++) {
                float angle = MathHelper.TwoPi * i / 6f + globalTime * 0.4f;
                Vector2 offset = angle.ToRotationVector2() * (2f + breatheAnim * 1.5f);
                Utils.DrawBorderString(spriteBatch, title, titlePos + offset, glowColor, 0.85f);
            }

            Color titleColor = Color.Lerp(MushroomTextColor, MushroomHighlight, breatheAnim * 0.2f);
            Utils.DrawBorderString(spriteBatch, title, titlePos, titleColor * alpha, 0.85f);

            //状态标签
            string statusTag = modify.Crouch ? ModifyCrabulon.StatusRestText.Value : modify.Mount ? ModifyCrabulon.StatusMountText.Value : ModifyCrabulon.StatusFollowText.Value;
            Color tagColor = modify.Crouch ? new Color(100, 180, 255) : modify.Mount ? new Color(255, 200, 100) : MushroomGreen;
            float tagPulse = 0.7f + MathF.Sin(globalTime * 2.5f) * 0.3f;
            Vector2 tagPos = titlePos + new Vector2(FontAssets.MouseText.Value.MeasureString(title).X * 0.85f + 10, 3);
            Utils.DrawBorderString(spriteBatch, statusTag, tagPos, tagColor * (alpha * tagPulse), 0.6f);
        }

        private void DrawHealthBar(SpriteBatch spriteBatch, float alpha) {
            Texture2D pixel = CWRAsset.Placeholder_White.Value;
            float barX = DrawPosition.X + Padding;
            float barY = DrawPosition.Y + 42;
            float barWidth = PanelWidth - Padding * 2 - SaddleSlotSize - 16;

            //标签
            string hpLabel = $"HP: {modify.npc.life}/{modify.npc.lifeMax}";
            Utils.DrawBorderString(spriteBatch, hpLabel, new Vector2(barX, barY - 2), MushroomTextColor * alpha, 0.65f);

            barY += 16;
            Rectangle barBg = new((int)barX, (int)barY, (int)barWidth, (int)BarHeight);

            //背景
            DrawRoundedRect(spriteBatch, barBg, new Color(8, 12, 20) * (alpha * 0.9f), 4f);

            //填充
            int fillWidth = (int)(barWidth * healthBarAnim);
            if (fillWidth > 0) {
                Rectangle fillRect = new((int)barX, (int)barY, fillWidth, (int)BarHeight);
                Color hpColor = healthBarAnim > 0.5f
                    ? Color.Lerp(MushroomGreen, MushroomCyan, (healthBarAnim - 0.5f) * 2f)
                    : Color.Lerp(new Color(255, 80, 80), MushroomGreen, healthBarAnim * 2f);
                DrawRoundedRect(spriteBatch, fillRect, hpColor * (alpha * 0.9f), 4f);

                //发光层
                Color glowColor = hpColor with { A = 0 } * (alpha * 0.35f);
                Rectangle glowRect = fillRect;
                glowRect.Inflate(0, 2);
                spriteBatch.Draw(pixel, glowRect, new Rectangle(0, 0, 1, 1), glowColor);
            }

            //受伤闪烁
            if (healthFlashTimer > 0) {
                float flash = healthFlashTimer / 20f;
                DrawRoundedRect(spriteBatch, barBg, Color.White * (alpha * flash * 0.3f), 4f);
            }

            //边框
            DrawRoundedRectBorder(spriteBatch, barBg, MushroomBorderColor * (alpha * 0.5f), 4f, 1);
        }

        private void DrawFeedBar(SpriteBatch spriteBatch, float alpha) {
            Texture2D pixel = CWRAsset.Placeholder_White.Value;
            float barX = DrawPosition.X + Padding;
            float barY = DrawPosition.Y + 76;
            float barWidth = PanelWidth - Padding * 2 - SaddleSlotSize - 16;

            float maxFeed = CrabulonConstants.MaxFeedValue;
            string feedLabel = $"Feed: {(int)modify.FeedValue}/{(int)maxFeed}";
            Utils.DrawBorderString(spriteBatch, feedLabel, new Vector2(barX, barY - 2), MushroomTextColor * alpha, 0.65f);

            barY += 16;
            Rectangle barBg = new((int)barX, (int)barY, (int)barWidth, (int)BarHeight);

            DrawRoundedRect(spriteBatch, barBg, new Color(8, 12, 20) * (alpha * 0.9f), 4f);

            int fillWidth = (int)(barWidth * feedBarAnim);
            if (fillWidth > 0) {
                Rectangle fillRect = new((int)barX, (int)barY, fillWidth, (int)BarHeight);
                Color feedColor = Color.Lerp(new Color(60, 180, 100), MushroomCyan, feedBarAnim);
                DrawRoundedRect(spriteBatch, fillRect, feedColor * (alpha * 0.9f), 4f);

                Color glowColor = feedColor with { A = 0 } * (alpha * 0.3f);
                Rectangle glowRect = fillRect;
                glowRect.Inflate(0, 2);
                spriteBatch.Draw(pixel, glowRect, new Rectangle(0, 0, 1, 1), glowColor);
            }

            DrawRoundedRectBorder(spriteBatch, barBg, MushroomBorderColor * (alpha * 0.5f), 4f, 1);
        }

        private void DrawSaddleSlot(SpriteBatch spriteBatch, float alpha) {
            Texture2D pixel = CWRAsset.Placeholder_White.Value;

            //槽位背景
            Color slotBg = Color.Lerp(new Color(15, 22, 35), new Color(20, 35, 50), saddleHoverAnim);
            DrawRoundedRect(spriteBatch, saddleSlotRect, slotBg * (alpha * 0.95f), 6f);

            //内发光
            if (saddleHoverAnim > 0.01f) {
                DrawInnerGlow(spriteBatch, saddleSlotRect, MushroomCyan * (alpha * saddleHoverAnim * 0.15f), 6f, 8);
            }

            //边框
            Color borderColor = Color.Lerp(MushroomBorderColor, MushroomHighlight, saddleHoverAnim);
            DrawRoundedRectBorder(spriteBatch, saddleSlotRect, borderColor * (alpha * 0.7f), 6f, 1);

            //物品图标
            Vector2 slotCenter = saddleSlotRect.Center.ToVector2();
            if (modify.SaddleItem.Alives()) {
                float itemFloat = MathF.Sin(globalTime * 1.5f) * 2f;
                Vector2 itemPos = slotCenter + new Vector2(0, itemFloat);

                //发光底
                Texture2D softGlow = CWRAsset.SoftGlow.Value;
                float glowIntensity = 0.5f + MathF.Sin(globalTime * 2f) * 0.2f;
                spriteBatch.Draw(softGlow, itemPos, null, MushroomCyan with { A = 0 } * (alpha * 0.2f * glowIntensity),
                    0f, softGlow.Size() / 2f, 0.5f, SpriteEffects.None, 0f);

                VaultUtils.SimpleDrawItem(spriteBatch, modify.SaddleItem.type, itemPos,
                    modify.SaddleItem.width, 0.85f, 0f, Color.White * alpha);
            }
            else {
                //空槽位提示
                Color emptyColor = MushroomBorderColor * (alpha * (0.3f + breatheAnim * 0.1f));
                string emptyLabel = "+";
                Vector2 emptySize = FontAssets.MouseText.Value.MeasureString(emptyLabel) * 0.8f;
                Utils.DrawBorderString(spriteBatch, emptyLabel, slotCenter - emptySize / 2f + new Vector2(0, 2), emptyColor, 0.8f);
            }

            //鞍具标签
            string saddleLabel = ModifyCrabulon.SaddleText.Value;
            Vector2 labelSize = FontAssets.MouseText.Value.MeasureString(saddleLabel) * 0.55f;
            Vector2 labelPos = new(saddleSlotRect.X + SaddleSlotSize / 2f - labelSize.X / 2f, saddleSlotRect.Y - 14);
            Utils.DrawBorderString(spriteBatch, saddleLabel, labelPos, MushroomTextColor * (alpha * 0.7f), 0.55f);

            //按压效果
            if (saddlePressAnim > 0.01f) {
                DrawRoundedRect(spriteBatch, saddleSlotRect, Color.White * (alpha * saddlePressAnim * 0.2f), 6f);
            }
        }

        private void DrawMushroomButton(SpriteBatch spriteBatch, Rectangle rect, string text,
            float hoverAnim, float pressAnim, float alpha, Color accentColor) {

            Rectangle drawRect = rect;
            if (pressAnim > 0.01f) {
                drawRect.Y += (int)(pressAnim * 2f);
            }

            int expand = (int)(hoverAnim * 2f);
            drawRect.Inflate(expand, expand / 2);

            //背景
            Color bgTop = Color.Lerp(new Color(15, 25, 40), new Color(25, 45, 65), hoverAnim);
            Color bgBottom = Color.Lerp(new Color(10, 18, 30), new Color(18, 35, 50), hoverAnim);
            DrawGradientRoundedRect(spriteBatch, drawRect, bgTop * (alpha * 0.95f), bgBottom * (alpha * 0.95f), 5f);

            //边框
            Color borderColor = Color.Lerp(MushroomBorderColor * 0.6f, accentColor, hoverAnim);
            DrawRoundedRectBorder(spriteBatch, drawRect, borderColor * alpha, 5f, 1);

            //悬停内发光
            if (hoverAnim > 0.01f) {
                DrawInnerGlow(spriteBatch, drawRect, accentColor * (alpha * hoverAnim * 0.12f), 5f, 8);
            }

            //文字
            Vector2 textSize = FontAssets.MouseText.Value.MeasureString(text) * 0.75f;
            Vector2 textPos = drawRect.Center.ToVector2() - textSize / 2f + new Vector2(0, 2);

            Utils.DrawBorderString(spriteBatch, text, textPos + new Vector2(1, 1),
                Color.Black * (alpha * 0.4f), 0.75f);

            Color textColor = Color.Lerp(MushroomTextColor * 0.8f, Color.White, hoverAnim);
            Utils.DrawBorderString(spriteBatch, text, textPos, textColor * alpha, 0.75f);

            if (hoverAnim > 0.3f) {
                Color textGlow = accentColor with { A = 0 } * (alpha * (hoverAnim - 0.3f) * 0.4f);
                Utils.DrawBorderString(spriteBatch, text, textPos, textGlow, 0.75f);
            }
        }

        private void DrawAnimatedBorder(SpriteBatch spriteBatch, Rectangle rect, float alpha) {
            //基础边框
            DrawRoundedRectBorder(spriteBatch, rect, MushroomBorderColor * (alpha * 0.6f), CornerRadius, 1);

            //流光
            Texture2D pixel = CWRAsset.Placeholder_White.Value;
            float shimmerPos = (shimmerPhase % 4f) / 4f;
            int perimeter = (rect.Width + rect.Height) * 2;

            for (int i = 0; i < 2; i++) {
                float offset = (shimmerPos + i * 0.5f) % 1f;
                Vector2 pos = GetPointOnRectPerimeter(rect, offset);
                float intensity = MathF.Sin(offset * MathHelper.Pi) * 0.7f;
                Color shimmerColor = MushroomCyan with { A = 0 } * (alpha * intensity);

                Texture2D softGlow = CWRAsset.SoftGlow.Value;
                spriteBatch.Draw(softGlow, pos, null, shimmerColor * 0.5f,
                    0f, softGlow.Size() / 2f, 0.1f, SpriteEffects.None, 0f);

                //拖尾
                for (int j = 1; j <= 4; j++) {
                    float trailOffset = (offset - j * 0.008f + 1f) % 1f;
                    Vector2 trailPos = GetPointOnRectPerimeter(rect, trailOffset);
                    float trailIntensity = intensity * (1f - j / 5f);
                    spriteBatch.Draw(pixel, trailPos, new Rectangle(0, 0, 1, 1),
                        shimmerColor * trailIntensity * 0.4f, 0f, new Vector2(0.5f),
                        new Vector2(5f - j, 2.5f - j * 0.3f), SpriteEffects.None, 0f);
                }
            }
        }

        private void DrawCornerMycelium(SpriteBatch spriteBatch, Rectangle rect, float alpha) {
            Texture2D pixel = CWRAsset.Placeholder_White.Value;
            float ornamentAlpha = alpha * (0.4f + breatheAnim * 0.3f);
            Color ornamentColor = MushroomCyan with { A = 0 } * ornamentAlpha;

            Vector2[] corners = [
                new(rect.X + 6, rect.Y + 6),
                new(rect.Right - 6, rect.Y + 6),
                new(rect.X + 6, rect.Bottom - 6),
                new(rect.Right - 6, rect.Bottom - 6)
            ];

            for (int i = 0; i < 4; i++) {
                //菌丝点
                Texture2D softGlow = CWRAsset.SoftGlow.Value;
                float pulse = MathF.Sin(globalTime * 2f + i * 1.5f) * 0.3f + 0.7f;
                spriteBatch.Draw(softGlow, corners[i], null, ornamentColor * pulse,
                    0f, softGlow.Size() / 2f, 0.08f, SpriteEffects.None, 0f);

                //菌丝线
                for (int j = 0; j < 3; j++) {
                    float rayRot = j * MathHelper.PiOver2 + i * MathHelper.PiOver4 + globalTime * 0.2f;
                    Vector2 rayDir = rayRot.ToRotationVector2();
                    spriteBatch.Draw(pixel, corners[i] + rayDir * 3f, new Rectangle(0, 0, 1, 1),
                        ornamentColor * 0.4f, rayRot, new Vector2(0f, 0.5f), new Vector2(6f, 1f), SpriteEffects.None, 0f);
                }
            }
        }

        private void DrawAnimatedDivider(SpriteBatch spriteBatch, Vector2 start, Vector2 end, float alpha) {
            Texture2D pixel = CWRAsset.Placeholder_White.Value;
            float length = (end - start).Length();
            if (length < 1f) return;

            spriteBatch.Draw(pixel, start, new Rectangle(0, 0, 1, 1), MushroomBorderColor * (alpha * 0.4f), 0f,
                Vector2.Zero, new Vector2(length, 1f), SpriteEffects.None, 0f);

            float shimmerT = (globalTime * 0.4f) % 1f;
            Vector2 shimmerPos = Vector2.Lerp(start, end, shimmerT);
            Texture2D softGlow = CWRAsset.SoftGlow.Value;
            spriteBatch.Draw(softGlow, shimmerPos, null, MushroomCyan with { A = 0 } * (alpha * 0.5f), 0f,
                softGlow.Size() / 2f, new Vector2(0.15f, 0.04f), SpriteEffects.None, 0f);
        }

        /// <summary>
        /// 绘制鼠标悬浮时的提示信息
        /// </summary>
        private void DrawHoverInfo(SpriteBatch spriteBatch, float alpha, float baseScale) {
            if (modify == null || !modify.hoverNPC) return;
            if (player.Alives() && player.CWR().IsRotatingDuringDash) return;

            Item currentItem = player.GetItem();
            Item saddleToDraw = null;
            string hoverContent = "";
            bool canDraw = false;

            if (currentItem.type == ModContent.ItemType<MushroomSaddle>()) {
                canDraw = true;
                saddleToDraw = currentItem;
                hoverContent = modify.SaddleItem.Alives() ? ModifyCrabulon.ChangeSaddleText.Value : ModifyCrabulon.MountHoverText.Value;
            }
            else if (modify.SaddleItem.Alives()) {
                canDraw = true;
                saddleToDraw = modify.SaddleItem;
                hoverContent = modify.Mount ? ModifyCrabulon.DismountText.Value : ModifyCrabulon.RideHoverText.Value;
            }

            if (!canDraw) return;

            Vector2 itemPos = MousePosition + new Vector2(0, 32);
            if (saddleToDraw.Alives()) {
                saddleToDraw.BeginDyeEffectForUI(saddleToDraw.CWR().DyeItemID);
                VaultUtils.SimpleDrawItem(spriteBatch, saddleToDraw.type, itemPos, 32, 1f, 0, Color.White * alpha);
                saddleToDraw.EndDyeEffectForUI();
            }

            Color textColor = VaultUtils.MultiStepColorLerp(Math.Abs(MathF.Sin(Main.GameUpdateCount * 0.02f)), MushroomBlue, MushroomCyan);
            Vector2 hoverSize = FontAssets.MouseText.Value.MeasureString(hoverContent) * baseScale * 0.9f;
            Vector2 hoverPos = itemPos + new Vector2(0, 36);

            Utils.DrawBorderStringFourWay(
                spriteBatch, FontAssets.MouseText.Value,
                hoverContent, hoverPos.X, hoverPos.Y,
                textColor * alpha, Color.Black * alpha,
                hoverSize / 2, baseScale
            );
        }

        #endregion

        #region 绘制辅助方法

        private static Vector2 GetPointOnRectPerimeter(Rectangle rect, float t) {
            float perimeter = (rect.Width + rect.Height) * 2f;
            float dist = t * perimeter;

            if (dist < rect.Width)
                return new Vector2(rect.X + dist, rect.Y);
            dist -= rect.Width;
            if (dist < rect.Height)
                return new Vector2(rect.Right, rect.Y + dist);
            dist -= rect.Height;
            if (dist < rect.Width)
                return new Vector2(rect.Right - dist, rect.Bottom);
            dist -= rect.Width;
            return new Vector2(rect.X, rect.Bottom - dist);
        }

        private static void DrawRoundedRect(SpriteBatch sb, Rectangle rect, Color color, float radius) {
            Texture2D pixel = CWRAsset.Placeholder_White.Value;
            int r = (int)Math.Min(radius, Math.Min(rect.Width, rect.Height) / 2f);

            Rectangle center = new(rect.X + r, rect.Y, rect.Width - r * 2, rect.Height);
            sb.Draw(pixel, center, new Rectangle(0, 0, 1, 1), color);

            Rectangle left = new(rect.X, rect.Y + r, r, rect.Height - r * 2);
            Rectangle right = new(rect.Right - r, rect.Y + r, r, rect.Height - r * 2);
            sb.Draw(pixel, left, new Rectangle(0, 0, 1, 1), color);
            sb.Draw(pixel, right, new Rectangle(0, 0, 1, 1), color);

            for (int i = 0; i < r; i++) {
                float t = i / (float)r;
                int cornerWidth = (int)(r * MathF.Sqrt(1f - (1f - t) * (1f - t)));
                sb.Draw(pixel, new Rectangle(rect.X + r - cornerWidth, rect.Y + i, cornerWidth, 1), new Rectangle(0, 0, 1, 1), color);
                sb.Draw(pixel, new Rectangle(rect.Right - r, rect.Y + i, cornerWidth, 1), new Rectangle(0, 0, 1, 1), color);
                sb.Draw(pixel, new Rectangle(rect.X + r - cornerWidth, rect.Bottom - 1 - i, cornerWidth, 1), new Rectangle(0, 0, 1, 1), color);
                sb.Draw(pixel, new Rectangle(rect.Right - r, rect.Bottom - 1 - i, cornerWidth, 1), new Rectangle(0, 0, 1, 1), color);
            }
        }

        private static void DrawGradientRoundedRect(SpriteBatch sb, Rectangle rect, Color topColor, Color bottomColor, float radius) {
            int r = (int)Math.Min(radius, Math.Min(rect.Width, rect.Height) / 2f);
            for (int i = 0; i < rect.Height; i++) {
                float t = i / (float)rect.Height;
                Color color = Color.Lerp(topColor, bottomColor, t);
                int y = rect.Y + i;
                int inset = 0;

                if (i < r) {
                    float cornerT = i / (float)r;
                    inset = (int)(r * (1f - MathF.Sqrt(1f - (1f - cornerT) * (1f - cornerT))));
                }
                else if (i > rect.Height - r) {
                    float cornerT = (rect.Height - i) / (float)r;
                    inset = (int)(r * (1f - MathF.Sqrt(1f - (1f - cornerT) * (1f - cornerT))));
                }

                Rectangle line = new(rect.X + inset, y, rect.Width - inset * 2, 1);
                sb.Draw(CWRAsset.Placeholder_White.Value, line, new Rectangle(0, 0, 1, 1), color);
            }
        }

        private static void DrawRoundedRectBorder(SpriteBatch sb, Rectangle rect, Color color, float radius, int thickness) {
            Texture2D pixel = CWRAsset.Placeholder_White.Value;
            int r = (int)Math.Min(radius, Math.Min(rect.Width, rect.Height) / 2f);

            sb.Draw(pixel, new Rectangle(rect.X + r, rect.Y, rect.Width - r * 2, thickness), new Rectangle(0, 0, 1, 1), color);
            sb.Draw(pixel, new Rectangle(rect.X + r, rect.Bottom - thickness, rect.Width - r * 2, thickness), new Rectangle(0, 0, 1, 1), color);
            sb.Draw(pixel, new Rectangle(rect.X, rect.Y + r, thickness, rect.Height - r * 2), new Rectangle(0, 0, 1, 1), color);
            sb.Draw(pixel, new Rectangle(rect.Right - thickness, rect.Y + r, thickness, rect.Height - r * 2), new Rectangle(0, 0, 1, 1), color);

            DrawCornerArc(sb, new Vector2(rect.X + r, rect.Y + r), r, MathHelper.Pi, MathHelper.PiOver2, color, thickness);
            DrawCornerArc(sb, new Vector2(rect.Right - r, rect.Y + r), r, -MathHelper.PiOver2, MathHelper.PiOver2, color, thickness);
            DrawCornerArc(sb, new Vector2(rect.X + r, rect.Bottom - r), r, MathHelper.PiOver2, MathHelper.PiOver2, color, thickness);
            DrawCornerArc(sb, new Vector2(rect.Right - r, rect.Bottom - r), r, 0, MathHelper.PiOver2, color, thickness);
        }

        private static void DrawCornerArc(SpriteBatch sb, Vector2 center, float radius, float startAngle, float sweep, Color color, int thickness) {
            Texture2D pixel = CWRAsset.Placeholder_White.Value;
            int segments = Math.Max(4, (int)(radius * sweep / 2f));
            for (int i = 0; i <= segments; i++) {
                float angle = startAngle + sweep * i / segments;
                Vector2 pos = center + angle.ToRotationVector2() * radius;
                sb.Draw(pixel, pos, new Rectangle(0, 0, 1, 1), color, 0f, new Vector2(0.5f), thickness, SpriteEffects.None, 0f);
            }
        }

        private static void DrawInnerGlow(SpriteBatch sb, Rectangle rect, Color color, float radius, int glowSize) {
            for (int i = 0; i < glowSize; i++) {
                float t = i / (float)glowSize;
                float glowAlpha = (1f - t) * (1f - t);
                Color glowColor = color * glowAlpha;
                Rectangle glowRect = rect;
                glowRect.Inflate(-i, -i);
                if (glowRect.Width > 0 && glowRect.Height > 0) {
                    DrawRoundedRectBorder(sb, glowRect, glowColor, Math.Max(0, radius - i), 1);
                }
            }
        }

        private static void DrawHorizontalGradient(SpriteBatch sb, Rectangle rect, Color left, Color center, Color right) {
            Texture2D pixel = CWRAsset.Placeholder_White.Value;
            for (int i = 0; i < rect.Width; i++) {
                float t = i / (float)rect.Width;
                Color color = t < 0.5f
                    ? Color.Lerp(left, center, t * 2f)
                    : Color.Lerp(center, right, (t - 0.5f) * 2f);
                sb.Draw(pixel, new Rectangle(rect.X + i, rect.Y, 1, rect.Height), new Rectangle(0, 0, 1, 1), color);
            }
        }

        #endregion
    }
}
