using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Dungeonworld.NPCs
{
    /// <summary>
    /// 横贯拉锁：一道横穿战场的鬼链。先垂着晃 40 帧当预告（链条哗啦作响、锚点粉光），
    /// 一帧绷直成伤害线撑 70 帧，再锈解崩散。几何全由 spawn 包携带：
    /// 位置=中点，ai[0]=倾角，ai[1]=半长；相位走本地计时，迟入场只会看到更长的预告（安全方向）
    /// </summary>
    internal class GaolCrossChain : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private const int SnapAt = 40;
        private const int TautEnd = 110;
        private const int LifeTotal = 130;

        private ref float Life => ref Projectile.localAI[0];
        private float LineRot => Projectile.ai[0];
        private float HalfLen => Projectile.ai[1];

        private Vector2 LineDir => LineRot.ToRotationVector2();
        private Vector2 EndA => Projectile.Center - LineDir * HalfLen;
        private Vector2 EndB => Projectile.Center + LineDir * HalfLen;

        private float Seed => Projectile.identity * 0.7391f % 3.71f;

        public override void SetDefaults() {
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.aiStyle = -1;
            Projectile.timeLeft = LifeTotal;
        }

        public override void AI() {
            Life++;
            Projectile.velocity = Vector2.Zero;
            int t = (int)Life;

            if (t == 2 || t == 22) {
                //预告链声：越临近绷直调越高
                SoundEngine.PlaySound(SoundID.Item37 with {
                    Volume = 0.45f,
                    Pitch = t == 2 ? -0.7f : -0.4f,
                    MaxInstances = 3
                }, Projectile.Center);
            }

            if (t == SnapAt) {
                //绷直拍：脆响双声 + 沿线火花 + 近线震屏
                SoundEngine.PlaySound(SoundID.Item37 with { Volume = 0.8f, Pitch = 0.15f, MaxInstances = 2 }, Projectile.Center);
                SoundEngine.PlaySound(SoundID.NPCHit4 with { Volume = 0.6f, Pitch = -0.2f, MaxInstances = 2 }, Projectile.Center);
                if (!Main.dedServ) {
                    for (int k = 0; k < 12; k++) {
                        Vector2 pos = Vector2.Lerp(EndA, EndB, k / 11f);
                        PRTLoader.NewParticle<PRT_Spark>(pos,
                            new Vector2(0f, Main.rand.NextFloat(-1.6f, 1.6f)).RotatedBy(LineRot),
                            Color.Lerp(DeepGaolWraith.GaolPink, Color.White, Main.rand.NextFloat(0.5f)),
                            Main.rand.NextFloat(0.35f, 0.6f))?.Configure(true, Main.rand.Next(8, 14));
                    }
                    if (Main.LocalPlayer != null && Vector2.Distance(Main.LocalPlayer.Center, Projectile.Center) < 900f) {
                        Main.LocalPlayer.CWR()?.GetScreenShake(3f);
                    }
                }
            }

            //锈解期：链身簌簌掉屑
            if (!Main.dedServ && t > TautEnd && t % 3 == 0) {
                Vector2 pos = Vector2.Lerp(EndA, EndB, Main.rand.NextFloat());
                PRTLoader.NewParticle<PRT_GhostRainDrop>(pos,
                    new Vector2(0f, Main.rand.NextFloat(0.8f, 1.8f)),
                    DeepGaolWraith.IronDeep * 0.8f, Main.rand.NextFloat(0.3f, 0.5f))
                    ?.Configure(Main.rand.Next(14, 24), 0f);
            }

            if (t >= SnapAt) {
                Lighting.AddLight(Projectile.Center, 0.2f, 0.08f, 0.14f);
            }
        }

        /// <summary>只有绷紧段是伤害线；预告与锈解不打人</summary>
        public override bool? CanDamage() => Life >= SnapAt && Life < TautEnd + 4 ? null : false;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            float _ = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(),
                EndA, EndB, 14f, ref _);
        }

        //==================== 绘制：垂链预告 → 绷直铁线 → 锈解 ====================

        public override bool PreDraw(ref Color lightColor) {
            Texture2D chainTex = TextureAssets.Chain22?.Value;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (chainTex == null || glow == null) {
                return false;
            }
            SpriteBatch sb = Main.spriteBatch;
            int t = (int)Life;

            //松弛度：预告全松，绷直 3 帧收干
            float slack = t < SnapAt ? 1f : MathF.Max(0f, 1f - (t - SnapAt) / 3f);
            //绷直后的余震，指数衰减
            float vibe = t >= SnapAt ? MathF.Exp(-(t - SnapAt) * 0.14f) * 3.4f : 0f;
            //透明度：预告半隐带脉冲，锈解淡出
            float alpha = t < SnapAt
                ? 0.4f + 0.12f * MathF.Sin(t * 0.35f + Seed)
                : MathHelper.Clamp((LifeTotal - t) / 16f, 0f, 1f);

            Vector2 a = EndA;
            Vector2 b = EndB;
            Vector2 dir = LineDir;
            Vector2 perp = dir.RotatedBy(MathHelper.PiOver2);
            float len = HalfLen * 2f;
            float linkStep = MathF.Max(10f, chainTex.Height - 2f);
            int links = Math.Min((int)(len / linkStep) + 1, 120);
            Vector2 origin = chainTex.Size() * 0.5f;
            Color tint = lightColor.MultiplyRGB(DeepGaolWraith.IronMul) * alpha;

            Vector2 prev = a;
            for (int k = 1; k <= links; k++) {
                float u = k / (float)links;
                //垂度弧 + 呼吸晃 + 绷直余震
                float sag = MathF.Sin(MathHelper.Pi * u) * (34f * slack)
                    + MathF.Sin(Main.GlobalTimeWrappedHourly * 3.4f + u * 7f + Seed) * (5f * slack)
                    + MathF.Sin(t * 2.6f + u * 9f) * vibe * MathF.Sin(MathHelper.Pi * u);
                Vector2 p = a + dir * (u * len) + perp * sag;
                Vector2 seg = p - prev;
                if (seg.Length() >= 2f) {
                    sb.Draw(chainTex, (prev + p) * 0.5f - Main.screenPosition, null, tint,
                        seg.ToRotation() + MathHelper.PiOver2, origin, 1f, SpriteEffects.None, 0f);
                }
                prev = p;
            }

            //两端锚点粉光 + 绷紧期沿线细光（A=0 加色）
            Vector2 gOrigin = glow.Size() * 0.5f;
            Color anchorGlow = (DeepGaolWraith.GaolPink with { A = 0 }) * (0.6f * alpha);
            sb.Draw(glow, a - Main.screenPosition, null, anchorGlow, 0f, gOrigin,
                new Vector2(14f * 2f / glow.Width), SpriteEffects.None, 0f);
            sb.Draw(glow, b - Main.screenPosition, null, anchorGlow, 0f, gOrigin,
                new Vector2(14f * 2f / glow.Width), SpriteEffects.None, 0f);
            if (t >= SnapAt && t < TautEnd + 6) {
                sb.Draw(glow, Projectile.Center - Main.screenPosition, null,
                    (DeepGaolWraith.GaolPink with { A = 0 }) * (0.3f * alpha), LineRot, gOrigin,
                    new Vector2(len * 1.05f / glow.Width, 8f / glow.Height), SpriteEffects.None, 0f);
            }
            return false;
        }
    }
}
