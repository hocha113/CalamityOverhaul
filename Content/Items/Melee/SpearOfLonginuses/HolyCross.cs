using CalamityOverhaul.Common;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee.SpearOfLonginuses
{
    /// <summary>
    /// 玩家手持朗基努斯之枪时悬浮于身下的圣神十字架，
    /// 用于显示当前的圣神能量条以及已蓄积的立场层数（与盗贼系统解耦）
    /// </summary>
    internal class HolyCross : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;

        //十字架尺寸（像素）
        private const int VerticalLength = 44;
        private const int VerticalThickness = 6;
        private const int HorizontalLength = 26;
        private const int HorizontalThickness = 6;

        private float spawnProgress;
        private float pulsePhase;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 32;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 2;
        }

        public override bool ShouldUpdatePosition() => false;

        public override bool? CanDamage() => false;

        private Player Owner => Main.player[Projectile.owner];

        public override void AI() {
            Player player = Owner;

            //玩家不再持枪、死亡或离开时立即销毁
            if (!player.active || player.dead
                || player.HeldItem?.type != SpearOfLonginus.ID
                || player.CountProjectilesOfID<LonginusHeld>() == 0) {
                Projectile.Kill();
                return;
            }

            //出现动画
            if (spawnProgress < 1f) {
                spawnProgress = MathHelper.Clamp(spawnProgress + 0.05f, 0f, 1f);
            }
            pulsePhase += 0.08f;

            //悬浮于玩家身下，随重力方向调整
            float verticalOffset = 70f * player.gravDir;
            float bob = (float)Math.Sin(Main.GameUpdateCount * 0.05f) * 3f;
            Projectile.Center = player.Center + new Vector2(0, verticalOffset + bob * player.gravDir);
            Projectile.timeLeft = 2;

            //简单的环境光照
            if (player.HeldItem.ModItem is SpearOfLonginus longinus) {
                float fill = longinus.ChargeGrade >= SpearOfLonginus.MaxChargeGrade
                    ? 1f
                    : longinus.HolyEnergy / (float)SpearOfLonginus.HolyEnergyMax;
                float lightIntensity = 0.4f + fill * 0.8f;
                Lighting.AddLight(Projectile.Center
                    , 1.0f * lightIntensity, 0.85f * lightIntensity, 0.35f * lightIntensity);

                //当能量接近上限时偶尔散发金色尘屑
                if (fill > 0.85f && Main.rand.NextBool(4)) {
                    Vector2 dustPos = Projectile.Center + Main.rand.NextVector2Circular(VerticalLength / 2f, VerticalLength / 2f);
                    Dust d = Dust.NewDustPerfect(dustPos, DustID.GoldCoin, Main.rand.NextVector2Circular(1f, 1f) - new Vector2(0, 1f), 0
                        , default, Main.rand.NextFloat(0.7f, 1.1f));
                    d.noGravity = true;
                    d.fadeIn = 0.6f;
                }
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            if (!CWRServerConfig.Instance.WeaponHandheldDisplay) {
                return false;
            }
            if (Owner?.HeldItem?.ModItem is not SpearOfLonginus longinus) {
                return false;
            }

            //圣神能量进度（达到最大立场后保持满）
            float fill = longinus.ChargeGrade >= SpearOfLonginus.MaxChargeGrade
                ? 1f
                : MathHelper.Clamp(longinus.HolyEnergy / (float)SpearOfLonginus.HolyEnergyMax, 0f, 1f);

            float appear = CWRUtils.EaseOutBack(spawnProgress);
            float pulse = 1f + (float)Math.Sin(pulsePhase) * 0.06f;

            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            float dir = Owner.gravDir;

            Texture2D pixel = TextureAssets.MagicPixel.Value;

            Color frameColor = new Color(60, 40, 20) * (0.7f * appear);
            Color emptyFillColor = new Color(120, 90, 40) * (0.65f * appear);
            Color hotFillColor = Color.Lerp(new Color(255, 215, 90), Color.White, fill * 0.6f) * appear;
            Color glowColor = (Color.Gold with { A = 0 }) * (fill * 0.9f * appear);

            //底层柔光
            Texture2D glowTex = CWRUtils.GetT2DValue(CWRConstant.Masking + "SoftGlow");
            if (glowTex != null) {
                float glowScale = (VerticalLength * 0.018f) * pulse * appear;
                Main.spriteBatch.Draw(glowTex, drawPos, null, glowColor, 0f
                    , glowTex.Size() / 2f, glowScale, SpriteEffects.None, 0);
            }

            //绘制十字架（外框+内填）。先画整体的"空"色，再按 fill 从下向上覆盖"热"色
            DrawCross(pixel, drawPos, dir, appear * pulse, emptyFillColor, frameColor, fillRatio: 0f, hotFillColor);
            DrawCross(pixel, drawPos, dir, appear * pulse, hotFillColor, frameColor, fillRatio: fill, hotFillColor);

            //顶部冠状光点：每一层立场显示一个光珠环绕十字架顶端
            DrawChargeOrbs(pixel, drawPos, dir, appear, longinus.ChargeGrade);

            return false;
        }

        private static void DrawCross(Texture2D pixel, Vector2 center, float dir, float scale
            , Color fillColor, Color frameColor, float fillRatio, Color overrideFillColor) {
            int vLen = (int)Math.Round(VerticalLength * scale);
            int vThk = (int)Math.Max(2, Math.Round(VerticalThickness * scale));
            int hLen = (int)Math.Round(HorizontalLength * scale);
            int hThk = (int)Math.Max(2, Math.Round(HorizontalThickness * scale));

            //十字架的整体几何：竖条以(0,0)为中心，横条偏上 1/4 处
            int vTopY = -vLen / 2;
            int hY = -vLen / 6 - hThk / 2;//横条略偏上

            Rectangle vertical = new Rectangle((int)center.X - vThk / 2
                , (int)center.Y + (int)(vTopY * dir)
                , vThk, vLen);
            Rectangle horizontal = new Rectangle((int)center.X - hLen / 2
                , (int)center.Y + (int)(hY * dir)
                , hLen, hThk);

            if (fillRatio <= 0f) {
                //先画完整的"空"底色
                Main.spriteBatch.Draw(pixel, vertical, fillColor);
                Main.spriteBatch.Draw(pixel, horizontal, fillColor);

                //画外框（1像素厚度的描边）
                DrawRectOutline(pixel, vertical, frameColor);
                DrawRectOutline(pixel, horizontal, frameColor);
                return;
            }

            //画"热"色填充，从底部向上根据 fillRatio 上涨
            //竖条
            int filledHeight = (int)Math.Round(vertical.Height * fillRatio);
            if (filledHeight > 0) {
                Rectangle vFill;
                if (dir > 0) {
                    vFill = new Rectangle(vertical.X, vertical.Y + vertical.Height - filledHeight, vertical.Width, filledHeight);
                }
                else {
                    vFill = new Rectangle(vertical.X, vertical.Y, vertical.Width, filledHeight);
                }
                Main.spriteBatch.Draw(pixel, vFill, overrideFillColor);
            }
            //横条仅在 fill 越过它所处高度时才填充
            float horizontalTriggerRatio = (float)(vertical.Bottom - horizontal.Top) / vertical.Height;
            if (dir < 0) {
                horizontalTriggerRatio = (float)(horizontal.Bottom - vertical.Top) / vertical.Height;
            }
            if (fillRatio >= horizontalTriggerRatio * 0.9f) {
                Main.spriteBatch.Draw(pixel, horizontal, overrideFillColor);
            }
        }

        private static void DrawRectOutline(Texture2D pixel, Rectangle rect, Color color) {
            Main.spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, 1), color);
            Main.spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Bottom - 1, rect.Width, 1), color);
            Main.spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, 1, rect.Height), color);
            Main.spriteBatch.Draw(pixel, new Rectangle(rect.Right - 1, rect.Y, 1, rect.Height), color);
        }

        private void DrawChargeOrbs(Texture2D pixel, Vector2 center, float dir, float appear, int chargeGrade) {
            if (chargeGrade <= 0) {
                return;
            }
            Texture2D glowTex = CWRUtils.GetT2DValue(CWRConstant.Masking + "SoftGlow");

            float radius = (HorizontalLength * 0.55f + 6f) * appear;
            float baseRot = pulsePhase * 0.6f;

            for (int i = 0; i < chargeGrade; i++) {
                float angle = baseRot + MathHelper.TwoPi * i / SpearOfLonginus.MaxChargeGrade;
                Vector2 offset = angle.ToRotationVector2() * radius;
                offset.Y *= dir;
                Vector2 orbPos = center + offset;

                Color orbColor = Color.Lerp(Color.Gold, Color.OrangeRed
                    , (float)Math.Sin(pulsePhase + i) * 0.5f + 0.5f);

                if (glowTex != null) {
                    Color halo = orbColor with { A = 0 };
                    Main.spriteBatch.Draw(glowTex, orbPos, null, halo * (0.8f * appear), 0f
                        , glowTex.Size() / 2f, 0.18f * appear, SpriteEffects.None, 0);
                }

                Rectangle orbRect = new Rectangle((int)orbPos.X - 2, (int)orbPos.Y - 2, 4, 4);
                Main.spriteBatch.Draw(pixel, orbRect, orbColor * appear);
            }
        }
    }
}
