using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Magic.Elysiums
{
    /// <summary>
    /// 天国极乐手持弹幕，实现左键攻击(化蛇术)
    /// </summary>
    internal class ElysiumHeld : ModProjectile
    {
        public override string Texture => "CalamityOverhaul/Content/Items/Magic/Elysiums/Elysium";

        private Player Owner => Main.player[Projectile.owner];

        //蓄力时间
        private ref float ChargeTime => ref Projectile.ai[0];

        //攻击冷却
        private ref float AttackCooldown => ref Projectile.ai[1];

        //视觉效果相关
        private float staffRotation = 0f;
        private float glowPulse = 0f;
        private List<HolyRingData> holyRings = [];

        //圣环数据
        private class HolyRingData
        {
            public float Radius;
            public float MaxRadius;
            public float Life;
            public float MaxLife;
            public float Rotation;
            public Color RingColor;
        }

        [VaultLoaden(CWRConstant.Masking + "SoftGlow")]
        private static Asset<Texture2D> GlowAsset = null;

        public override void SetDefaults() {
            Projectile.width = 50;
            Projectile.height = 50;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.DamageType = DamageClass.Magic;
        }

        public override void AI() {
            if (!Owner.active || Owner.dead) {
                Projectile.Kill();
                return;
            }

            //保持武器
            Owner.heldProj = Projectile.whoAmI;
            Projectile.timeLeft = 2;

            //检查是否继续引导
            if (!Owner.channel) {
                //释放攻击
                if (ChargeTime > 30) {
                    ReleaseSnakeConversion();
                }
                Projectile.Kill();
                return;
            }

            //更新位置和朝向
            UpdatePositionAndRotation();

            //蓄力
            ChargeTime++;
            glowPulse += 0.1f;

            //蓄力特效
            SpawnChargeEffects();

            //更新圣环
            UpdateHolyRings();

            //消耗法力
            if (ChargeTime % 30 == 0) {
                if (!Owner.CheckMana(Owner.inventory[Owner.selectedItem], -5, true)) {
                    Projectile.Kill();
                    return;
                }
            }

            //动态照明(黑白色调)
            float intensity = 0.5f + (float)Math.Sin(glowPulse) * 0.2f;
            Lighting.AddLight(Projectile.Center, intensity, intensity, intensity);
        }

        /// <summary>
        /// 更新位置和旋转
        /// </summary>
        private void UpdatePositionAndRotation() {
            Vector2 toMouse = Main.MouseWorld - Owner.Center;
            float targetRot = toMouse.ToRotation();

            //平滑旋转
            staffRotation = MathHelper.Lerp(staffRotation, targetRot, 0.15f);
            Projectile.rotation = staffRotation + MathHelper.PiOver4;

            //权杖位置
            Projectile.Center = Owner.Center + staffRotation.ToRotationVector2() * 30f;

            //玩家朝向
            Owner.direction = Math.Sign(toMouse.X);
            if (Owner.direction == 0) Owner.direction = 1;

            //固定玩家动作
            Owner.itemTime = Owner.itemAnimation = 2;
            Owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, staffRotation - MathHelper.PiOver2);
        }

        /// <summary>
        /// 生成蓄力特效
        /// </summary>
        private void SpawnChargeEffects() {
            //蓄力阶段性特效
            if (ChargeTime == 30) {
                SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.8f, Pitch = 0.5f }, Projectile.Center);
                SpawnHolyRing(100f, 30, Color.White);
            }
            if (ChargeTime == 60) {
                SoundEngine.PlaySound(SoundID.Item29 with { Volume = 1f, Pitch = 0.3f }, Projectile.Center);
                SpawnHolyRing(150f, 40, Color.Gold);
            }
            if (ChargeTime == 90) {
                SoundEngine.PlaySound(SoundID.Item29 with { Volume = 1.2f, Pitch = 0f }, Projectile.Center);
                SpawnHolyRing(200f, 50, new Color(255, 255, 200));
            }

            //持续粒子效果
            if (ChargeTime > 20 && ChargeTime % 3 == 0) {
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                float dist = 50f + ChargeTime * 0.5f;
                Vector2 spawnPos = Owner.Center + angle.ToRotationVector2() * dist;
                Vector2 vel = (Owner.Center - spawnPos).SafeNormalize(Vector2.Zero) * 3f;

                //黑白双色粒子
                int dustType = Main.rand.NextBool() ? DustID.SilverFlame : DustID.Shadowflame;
                Dust d = Dust.NewDustPerfect(spawnPos, dustType, vel, 100, default, 1.5f);
                d.noGravity = true;
            }

            //十字架光芒
            if (ChargeTime > 60 && ChargeTime % 10 == 0) {
                SpawnCrossLight(Owner.Center);
            }
        }

        /// <summary>
        /// 生成圣环
        /// </summary>
        private void SpawnHolyRing(float maxRadius, int lifetime, Color color) {
            holyRings.Add(new HolyRingData {
                Radius = 0,
                MaxRadius = maxRadius,
                Life = 0,
                MaxLife = lifetime,
                Rotation = Main.rand.NextFloat(MathHelper.TwoPi),
                RingColor = color
            });
        }

        /// <summary>
        /// 更新圣环
        /// </summary>
        private void UpdateHolyRings() {
            for (int i = holyRings.Count - 1; i >= 0; i--) {
                var ring = holyRings[i];
                ring.Life++;
                ring.Radius = MathHelper.Lerp(0, ring.MaxRadius, ring.Life / ring.MaxLife);
                ring.Rotation += 0.05f;

                if (ring.Life >= ring.MaxLife) {
                    holyRings.RemoveAt(i);
                }
            }
        }

        /// <summary>
        /// 生成十字架光芒
        /// </summary>
        private void SpawnCrossLight(Vector2 center) {
            //垂直光线
            for (int i = -3; i <= 3; i++) {
                Vector2 pos = center + new Vector2(0, i * 15);
                Dust d = Dust.NewDustPerfect(pos, DustID.GoldFlame, Vector2.Zero, 100, Color.White, 1.2f);
                d.noGravity = true;
            }
            //水平光线
            for (int i = -2; i <= 2; i++) {
                Vector2 pos = center + new Vector2(i * 15, -15);
                Dust d = Dust.NewDustPerfect(pos, DustID.GoldFlame, Vector2.Zero, 100, Color.White, 1.2f);
                d.noGravity = true;
            }
        }

        /// <summary>
        /// 释放化蛇术攻击
        /// </summary>
        private void ReleaseSnakeConversion() {
            SoundEngine.PlaySound(SoundID.Item117 with { Volume = 1.5f, Pitch = -0.3f }, Owner.Center);

            //计算攻击范围和威力
            float chargeRatio = Math.Min(ChargeTime / 120f, 1f);
            float radius = 200f + chargeRatio * 200f;
            int damage = Projectile.damage + (int)(chargeRatio * Projectile.damage);

            //生成化蛇波动弹幕
            Projectile.NewProjectile(
                Projectile.GetSource_FromThis(),
                Owner.Center,
                Vector2.Zero,
                ModContent.ProjectileType<SnakeConversionWave>(),
                damage,
                Projectile.knockBack,
                Owner.whoAmI,
                radius,
                chargeRatio
            );

            //大范围圣光爆发特效
            for (int i = 0; i < 60; i++) {
                float angle = MathHelper.TwoPi * i / 60f;
                Vector2 vel = angle.ToRotationVector2() * (8f + chargeRatio * 4f);
                int dustType = i % 2 == 0 ? DustID.SilverFlame : DustID.Shadowflame;
                Dust d = Dust.NewDustPerfect(Owner.Center, dustType, vel, 100, default, 2f);
                d.noGravity = true;
            }

            //十字架爆发
            for (int j = 0; j < 4; j++) {
                float baseAngle = MathHelper.PiOver2 * j;
                for (int i = 0; i < 10; i++) {
                    Vector2 pos = Owner.Center + baseAngle.ToRotationVector2() * (i * 20f);
                    Dust d = Dust.NewDustPerfect(pos, DustID.GoldFlame, baseAngle.ToRotationVector2() * 5f, 100, Color.White, 2f);
                    d.noGravity = true;
                }
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            //获取权杖纹理
            Texture2D staffTex = ModContent.Request<Texture2D>(Texture).Value;

            //绘制圣环
            DrawHolyRings(sb);

            //绘制蓄力光晕
            if (ChargeTime > 20 && GlowAsset?.IsLoaded == true) {
                float glowScale = 0.5f + (ChargeTime / 120f) * 1f;
                float pulse = (float)Math.Sin(glowPulse) * 0.2f + 0.8f;

                //白色外层光晕
                Color outerGlow = Color.White with { A = 0 } * 0.3f * pulse;
                sb.Draw(GlowAsset.Value, drawPos, null, outerGlow, 0, GlowAsset.Value.Size() / 2, glowScale * 2f, SpriteEffects.None, 0);

                //金色内层光晕
                Color innerGlow = new Color(255, 215, 100) with { A = 0 } * 0.5f * pulse;
                sb.Draw(GlowAsset.Value, drawPos, null, innerGlow, 0, GlowAsset.Value.Size() / 2, glowScale, SpriteEffects.None, 0);
            }

            //绘制权杖
            Vector2 origin = new Vector2(staffTex.Width * 0.1f, staffTex.Height * 0.9f);
            SpriteEffects effect = Owner.direction > 0 ? SpriteEffects.None : SpriteEffects.FlipHorizontally;
            float drawRot = Projectile.rotation;
            if (Owner.direction < 0) {
                drawRot += MathHelper.Pi;
            }

            sb.Draw(staffTex, drawPos, null, lightColor, drawRot, origin, 1f, effect, 0);

            return false;
        }

        /// <summary>
        /// 绘制圣环
        /// </summary>
        private void DrawHolyRings(SpriteBatch sb) {
            Texture2D pixel = CWRAsset.Placeholder_White?.Value;
            if (pixel == null) return;

            Vector2 center = Owner.Center - Main.screenPosition;

            foreach (var ring in holyRings) {
                float alpha = 1f - (ring.Life / ring.MaxLife);
                Color ringColor = ring.RingColor with { A = 0 } * alpha * 0.6f;

                int segments = 36;
                for (int i = 0; i < segments; i++) {
                    float angle = ring.Rotation + MathHelper.TwoPi * i / segments;
                    float nextAngle = ring.Rotation + MathHelper.TwoPi * (i + 1) / segments;

                    Vector2 start = center + angle.ToRotationVector2() * ring.Radius;
                    Vector2 end = center + nextAngle.ToRotationVector2() * ring.Radius;

                    DrawLine(sb, pixel, start, end, 2f, ringColor);
                }

                //绘制十字标记
                for (int j = 0; j < 4; j++) {
                    float crossAngle = ring.Rotation + MathHelper.PiOver2 * j;
                    Vector2 crossPos = center + crossAngle.ToRotationVector2() * ring.Radius;
                    DrawSmallCross(sb, pixel, crossPos, 8f, ringColor);
                }
            }
        }

        /// <summary>
        /// 绘制线段
        /// </summary>
        private static void DrawLine(SpriteBatch sb, Texture2D pixel, Vector2 start, Vector2 end, float thickness, Color color) {
            Vector2 diff = end - start;
            float length = diff.Length();
            if (length < 1f) return;
            sb.Draw(pixel, start, new Rectangle(0, 0, 1, 1), color, diff.ToRotation(), Vector2.Zero, new Vector2(length, thickness), SpriteEffects.None, 0f);
        }

        /// <summary>
        /// 绘制小十字
        /// </summary>
        private static void DrawSmallCross(SpriteBatch sb, Texture2D pixel, Vector2 center, float size, Color color) {
            float half = size / 2;
            float thickness = 2f;
            //垂直
            sb.Draw(pixel, center - new Vector2(thickness / 2, half), null, color, 0, Vector2.Zero, new Vector2(thickness, size), SpriteEffects.None, 0);
            //水平
            sb.Draw(pixel, center - new Vector2(half, thickness / 2), null, color, 0, Vector2.Zero, new Vector2(size, thickness), SpriteEffects.None, 0);
        }
    }
}
