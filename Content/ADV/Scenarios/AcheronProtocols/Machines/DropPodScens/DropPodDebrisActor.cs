using InnoVault.Actors;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;

namespace CalamityOverhaul.Content.ADV.Scenarios.AcheronProtocols.Machines.DropPodScens
{
    /// <summary>
    /// 空降仓坠落演出中的残骸障碍物Actor 流程：预警线阶段 → 残骸从屏幕底部向上高速飞过 如果与空降仓碰撞则产生火花粒子效果
    /// </summary>
    internal class DropPodDebrisActor : Actor
    {
        //阶段
        private enum DebrisPhase { Warning, Flying, Done }
        private DebrisPhase phase;

        //预警线
        private int warningTimer;
        private const int WarningDuration = 35;
        private float warningLineWidth;
        private float warningLineAlpha;

        //残骸飞行
        private float flySpeed;
        private float debrisRotation;
        private float debrisRotationSpeed;
        private float debrisScale;
        private int textureIndex;
        private bool flipH;
        private Color debrisTint;

        //碰撞检测
        private bool hasHit;
        private const float HitRadius = 60f;

        //火花粒子
        private readonly List<SparkParticle> sparks = [];
        private const int MaxSparks = 40;

        //能量脉冲计时器
        private float pulseTimer;

        //碰撞闪光
        private float hitFlashTimer;
        private Vector2 hitFlashPos;

        public override void OnSpawn(params object[] args) {
            Width = 20;
            Height = 20;
            DrawExtendMode = 800;
            DrawLayer = ActorDrawLayer.Default;

            phase = DebrisPhase.Warning;
            warningTimer = 0;
            warningLineWidth = 0f;
            warningLineAlpha = 0f;
            hasHit = false;

            flySpeed = Main.rand.NextFloat(14f, 22f);
            debrisRotation = Main.rand.NextFloat(MathHelper.TwoPi);
            debrisRotationSpeed = Main.rand.NextFloat(-0.03f, 0.03f);
            debrisScale = Main.rand.NextFloat(0.5f, 0.9f);
            textureIndex = ExoGoreAssets.Count > 0 ? Main.rand.Next(ExoGoreAssets.Count) : 0;
            flipH = Main.rand.NextBool();

            float brightness = Main.rand.NextFloat(0.7f, 0.9f);
            debrisTint = new Color(brightness, brightness * 0.95f, brightness * 1.05f);
            pulseTimer = Main.rand.NextFloat(MathHelper.TwoPi);
        }

        public override void AI() {
            if (!DropPodWorld.Active) {
                ActorLoader.KillActor(WhoAmI);
                return;
            }

            switch (phase) {
                case DebrisPhase.Warning:
                    UpdateWarning();
                    break;
                case DebrisPhase.Flying:
                    UpdateFlying();
                    break;
                case DebrisPhase.Done:
                    UpdateDone();
                    break;
            }

            pulseTimer += 0.08f;

            //碰撞闪光衰减
            if (hitFlashTimer > 0f) {
                hitFlashTimer -= 0.08f;
            }

            //更新火花粒子
            for (int i = sparks.Count - 1; i >= 0; i--) {
                sparks[i].Update();
                if (sparks[i].IsDead) {
                    sparks.RemoveAt(i);
                }
            }
        }

        private void UpdateWarning() {
            warningTimer++;

            //预警线快速扩展变宽然后收缩
            float progress = (float)warningTimer / WarningDuration;

            if (progress < 0.5f) {
                //快速扩展
                float expandProgress = progress / 0.5f;
                warningLineWidth = MathHelper.Lerp(0f, 8f, MathF.Pow(expandProgress, 0.5f));
                warningLineAlpha = MathHelper.Lerp(0f, 0.9f, expandProgress);
            }
            else {
                //保持并闪烁
                float flickerProgress = (progress - 0.5f) / 0.5f;
                warningLineWidth = 8f - flickerProgress * 4f;
                warningLineAlpha = 0.9f * (1f - flickerProgress * 0.3f);
                //闪烁效果
                warningLineAlpha *= 0.7f + MathF.Sin(warningTimer * 0.8f) * 0.3f;
            }

            if (warningTimer >= WarningDuration) {
                phase = DebrisPhase.Flying;
            }
        }

        private void UpdateFlying() {
            //向上飞行（Y减小）
            Position.Y -= flySpeed;
            debrisRotation += debrisRotationSpeed;

            //预警线淡出
            warningLineAlpha *= 0.85f;
            warningLineWidth *= 0.9f;

            //碰撞检测：检查是否与空降仓碰撞
            if (!hasHit) {
                var podActors = ActorLoader.GetActiveActors<DropPodActor>();
                foreach (var podActor in podActors) {
                    Vector2 podCenter = podActor.Center;
                    float dist = Vector2.Distance(Center, podCenter);
                    if (dist < HitRadius + 30f) {
                        OnHitPod(podCenter);
                        hasHit = true;
                        break;
                    }
                }
            }

            //飞出屏幕上方后销毁
            Vector2 screenPos = Center - Main.screenPosition;
            if (screenPos.Y < -400) {
                if (sparks.Count == 0) {
                    phase = DebrisPhase.Done;
                }
            }
        }

        private void UpdateDone() {
            //等待所有火花消失后销毁
            if (sparks.Count == 0) {
                ActorLoader.KillActor(WhoAmI);
            }
        }

        /// <summary>

        /// 残骸击中空降仓时的效果

        /// </summary>
        private void OnHitPod(Vector2 podCenter) {
            //在碰撞点生成大量火花粒子
            Vector2 hitPoint = (Center + podCenter) * 0.5f;
            int sparkCount = Main.rand.Next(20, MaxSparks);

            for (int i = 0; i < sparkCount; i++) {
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                float speed = Main.rand.NextFloat(3f, 12f);
                Vector2 sparkVel = angle.ToRotationVector2() * speed;

                sparkVel.Y -= Main.rand.NextFloat(2f, 6f);

                Color sparkColor = Color.Lerp(
                    new Color(255, 220, 100),
                    new Color(255, 140, 40),
                    Main.rand.NextFloat());

                sparks.Add(new SparkParticle {
                    Position = hitPoint + Main.rand.NextVector2Circular(10f, 10f),
                    Velocity = sparkVel,
                    Color = sparkColor,
                    Alpha = Main.rand.NextFloat(0.7f, 1f),
                    Scale = Main.rand.NextFloat(1.5f, 4f),
                    Life = 0,
                    MaxLife = Main.rand.Next(15, 40)
                });
            }

            //碰撞闪光：在撞击点产生短暂白色闪光
            hitFlashTimer = 1f;
            hitFlashPos = hitPoint;

            //残骸碰撞后速度微偏转，模拟物理擦撞效果
            flySpeed *= 0.85f;
            debrisRotationSpeed += Main.rand.NextFloat(-0.08f, 0.08f);

            //给空降仓增加额外的震动
            Player player = Main.LocalPlayer;
            if (player != null && player.active) {
                player.CWR().GetScreenShake(8f);
            }
        }

        public override bool PreDraw(SpriteBatch spriteBatch, ref Color drawColor) {
            //绘制预警线
            if (phase == DebrisPhase.Warning || phase == DebrisPhase.Flying && warningLineAlpha > 0.01f) {
                DrawWarningLine(spriteBatch);
            }

            //绘制残骸
            if (phase == DebrisPhase.Flying) {
                DrawDebris(spriteBatch);
            }

            //绘制碰撞闪光
            if (hitFlashTimer > 0f) {
                DrawHitFlash(spriteBatch);
            }

            //绘制火花粒子
            DrawSparks(spriteBatch);

            return false;
        }

        private void DrawWarningLine(SpriteBatch sb) {
            if (warningLineAlpha < 0.01f) return;
            Texture2D px = VaultAsset.placeholder2.Value;

            Vector2 lineScreenX = new Vector2(Center.X - Main.screenPosition.X, 0);
            float lineHeight = Main.screenHeight;

            //主预警线：红色竖线
            Color warningColor = new Color(255, 60, 60) * warningLineAlpha;
            sb.Draw(px, lineScreenX, new Rectangle(0, 0, 1, 1), warningColor,
                0f, new Vector2(0.5f, 0f), new Vector2(warningLineWidth, lineHeight), SpriteEffects.None, 0f);

            //外层辉光
            Color glowColor = new Color(255, 100, 60) * (warningLineAlpha * 0.4f);
            sb.Draw(px, lineScreenX, new Rectangle(0, 0, 1, 1), glowColor,
                0f, new Vector2(0.5f, 0f), new Vector2(warningLineWidth * 3f, lineHeight), SpriteEffects.None, 0f);

            //中心亮线
            Color coreColor = new Color(255, 200, 150) * (warningLineAlpha * 0.8f);
            sb.Draw(px, lineScreenX, new Rectangle(0, 0, 1, 1), coreColor,
                0f, new Vector2(0.5f, 0f), new Vector2(MathF.Max(1f, warningLineWidth * 0.3f), lineHeight), SpriteEffects.None, 0f);
        }

        private void DrawDebris(SpriteBatch sb) {
            Texture2D tex = ExoGoreAssets.GetTexture(textureIndex);
            if (tex == null) return;

            Vector2 origin = tex.Size() * 0.5f;
            Vector2 drawPos = Center - Main.screenPosition;
            SpriteEffects fx = flipH ? SpriteEffects.FlipHorizontally : SpriteEffects.None;

            float pulse = MathF.Sin(pulseTimer) * 0.3f + 0.7f;

            //能量光璀：温暖的橙红色脉冲光晕，让残骸在漆黑太空中突出
            if (CWRAsset.SoftGlow != null && !CWRAsset.SoftGlow.IsDisposed) {
                Texture2D glow = CWRAsset.SoftGlow.Value;
                float glowRadius = MathF.Max(tex.Width, tex.Height) * debrisScale * 0.7f;
                float glowScale = glowRadius / (glow.Width * 0.5f);
                Color glowColor = new Color(255, 120, 40) * (0.35f * pulse);
                sb.Draw(glow, drawPos, null, glowColor with { A = 0 }, 0f,
                    glow.Size() * 0.5f, glowScale, SpriteEffects.None, 0f);
            }

            //暖色边缘光（rim light）：橙红色，与背景的冷蓝色形成对比
            float rimPulse = 0.6f + pulse * 0.4f;
            Color rimColor = new Color(255, 100, 30) * (0.7f * rimPulse);
            float rimOffset = 2.5f;
            for (int i = 0; i < 4; i++) {
                float angle = MathHelper.PiOver2 * i;
                Vector2 off = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * rimOffset;
                sb.Draw(tex, drawPos + off, null, rimColor, debrisRotation, origin, debrisScale, fx, 0f);
            }

            //主体：全亮度绘制
            sb.Draw(tex, drawPos, null, Color.White, debrisRotation, origin, debrisScale, fx, 0f);

            //科技感订位括号标记：四角的L形括号
            DrawTargetBrackets(sb, drawPos, tex, pulse);

            //运动模糊拖影：温暖色调，与背景残骸的冷色拖影区分
            for (int t = 1; t <= 4; t++) {
                Vector2 trailPos = drawPos + new Vector2(0, flySpeed * t * 2f);
                float trailAlpha = 1f - t / 5f;
                Color trailColor = Color.Lerp(debrisTint, new Color(255, 140, 50), 0.3f) * (trailAlpha * 0.25f);
                sb.Draw(tex, trailPos, null, trailColor, debrisRotation, origin, debrisScale, fx, 0f);
            }
        }

        private void DrawTargetBrackets(SpriteBatch sb, Vector2 drawPos, Texture2D tex, float pulse) {
            Texture2D px = VaultAsset.placeholder2.Value;

            float halfW = tex.Width * debrisScale * 0.5f + 8f;
            float halfH = tex.Height * debrisScale * 0.5f + 8f;
            float bracketLen = MathF.Min(halfW, halfH) * 0.4f;
            float bracketThickness = 2f;
            Color bracketColor = new Color(255, 160, 60) * (0.7f * pulse);

            //左上角
            sb.Draw(px, drawPos + new Vector2(-halfW, -halfH), new Rectangle(0, 0, 1, 1), bracketColor,
                0f, Vector2.Zero, new Vector2(bracketLen, bracketThickness), SpriteEffects.None, 0f);
            sb.Draw(px, drawPos + new Vector2(-halfW, -halfH), new Rectangle(0, 0, 1, 1), bracketColor,
                0f, Vector2.Zero, new Vector2(bracketThickness, bracketLen), SpriteEffects.None, 0f);

            //右上角
            sb.Draw(px, drawPos + new Vector2(halfW - bracketLen, -halfH), new Rectangle(0, 0, 1, 1), bracketColor,
                0f, Vector2.Zero, new Vector2(bracketLen, bracketThickness), SpriteEffects.None, 0f);
            sb.Draw(px, drawPos + new Vector2(halfW - bracketThickness, -halfH), new Rectangle(0, 0, 1, 1), bracketColor,
                0f, Vector2.Zero, new Vector2(bracketThickness, bracketLen), SpriteEffects.None, 0f);

            //左下角
            sb.Draw(px, drawPos + new Vector2(-halfW, halfH - bracketThickness), new Rectangle(0, 0, 1, 1), bracketColor,
                0f, Vector2.Zero, new Vector2(bracketLen, bracketThickness), SpriteEffects.None, 0f);
            sb.Draw(px, drawPos + new Vector2(-halfW, halfH - bracketLen), new Rectangle(0, 0, 1, 1), bracketColor,
                0f, Vector2.Zero, new Vector2(bracketThickness, bracketLen), SpriteEffects.None, 0f);

            //右下角
            sb.Draw(px, drawPos + new Vector2(halfW - bracketLen, halfH - bracketThickness), new Rectangle(0, 0, 1, 1), bracketColor,
                0f, Vector2.Zero, new Vector2(bracketLen, bracketThickness), SpriteEffects.None, 0f);
            sb.Draw(px, drawPos + new Vector2(halfW - bracketThickness, halfH - bracketLen), new Rectangle(0, 0, 1, 1), bracketColor,
                0f, Vector2.Zero, new Vector2(bracketThickness, bracketLen), SpriteEffects.None, 0f);
        }

        private void DrawHitFlash(SpriteBatch sb) {
            if (CWRAsset.SoftGlow == null || CWRAsset.SoftGlow.IsDisposed) return;
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Vector2 drawPos = hitFlashPos - Main.screenPosition;

            float t = hitFlashTimer; //1 → 0
            float flashScale = (1f - t) * 0.8f + 0.3f; //快速扩散
            float flashAlpha = t * t; //快速衰减

            //内层白色核心闪光
            Color coreColor = Color.White * (flashAlpha * 0.9f);
            sb.Draw(glow, drawPos, null, coreColor with { A = 0 }, 0f,
                glow.Size() * 0.5f, flashScale * 0.5f, SpriteEffects.None, 0f);

            //外层橙红色扩散辉光
            Color outerColor = new Color(255, 160, 60) * (flashAlpha * 0.6f);
            sb.Draw(glow, drawPos, null, outerColor with { A = 0 }, 0f,
                glow.Size() * 0.5f, flashScale * 1.2f, SpriteEffects.None, 0f);
        }

        private void DrawSparks(SpriteBatch sb) {
            if (sparks.Count == 0) return;
            if (CWRAsset.SoftGlow == null || CWRAsset.SoftGlow.IsDisposed) return;
            Texture2D glow = CWRAsset.SoftGlow.Value;

            foreach (var spark in sparks) {
                float lifeRatio = (float)spark.Life / spark.MaxLife;
                float alpha = spark.Alpha * (1f - lifeRatio * lifeRatio);
                Color c = spark.Color * alpha;
                float scale = spark.Scale * (1f - lifeRatio * 0.5f) * 0.06f;
                Vector2 drawPos = spark.Position - Main.screenPosition;

                //火花拉伸：沿速度方向
                float sparkRot = spark.Velocity.ToRotation() + MathHelper.PiOver2;
                float stretch = MathF.Min(spark.Velocity.Length() * 0.15f, 2f);
                Vector2 sparkScale = new Vector2(scale, scale * (1f + stretch));

                sb.Draw(glow, drawPos, null, c with { A = 0 }, sparkRot, glow.Size() * 0.5f, sparkScale, SpriteEffects.None, 0f);
            }
        }

        private class SparkParticle
        {
            public Vector2 Position;
            public Vector2 Velocity;
            public Color Color;
            public float Alpha;
            public float Scale;
            public int Life;
            public int MaxLife;
            public bool IsDead => Life >= MaxLife;

            public void Update() {
                Life++;
                Position += Velocity;
                Velocity *= 0.94f;
                Velocity.Y -= 0.65f;
                Velocity.X += Main.rand.NextFloat(-0.2f, 0.2f);
            }
        }
    }
}
