using CalamityOverhaul.Common;
using CalamityOverhaul.Content.DamageModify;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Ranged.TiroFinales
{
    /// <summary>
    /// 终曲巨弹。巨炮轰出的金色魔力核:白热芯+金色对流+丝带螺旋壳(TechFinale)，
    /// 复利加速贯穿一切，沿途甩星屑，命中/撞地绽双层金环与星芒暴雨。<br/>
    /// ai0=巨炮缩放(出生视觉种子)
    /// </summary>
    internal class TiroFinaleBlast : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        [VaultLoaden(CWRConstant.Masking + "SoftGlow")]
        private static Asset<Texture2D> GlowTex = null;
        [VaultLoaden(CWRConstant.Masking + "StarTexture_White")]
        private static Asset<Texture2D> StarTex = null;

        /// <summary>可见半径(px)基准</summary>
        private const float VisR = 46f;

        private ref float Time => ref Projectile.localAI[2];

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 12;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 56;
            Projectile.friendly = true;
            Projectile.DamageType = RangedMagicDamageClass.Instance;
            Projectile.penetrate = -1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 16;
            Projectile.timeLeft = 240;
            Projectile.extraUpdates = 1;
        }

        /// <summary>出生膨胀包络:6 tick 从 0.4 顶到 1，带一点过冲</summary>
        private float BirthPop => TiroFinaleHeld.EaseOutBack(MathHelper.Clamp(Time / 12f, 0f, 1f)) * 0.6f + 0.4f;

        public override void AI() {
            Time++;
            //复利加速:魔力洪流推着炮弹越滚越快
            if (Projectile.velocity.Length() < 40f) {
                Projectile.velocity *= 1.012f;
            }
            Projectile.rotation = Projectile.velocity.ToRotation();
            Lighting.AddLight(Projectile.Center, new Vector3(1f, 0.85f, 0.46f) * 1.1f);

            if (VaultUtils.isServer) {
                return;
            }
            //沿途甩星屑与金烬
            if (Main.rand.NextBool(2)) {
                Vector2 shedPos = Projectile.Center + Main.rand.NextVector2Circular(VisR * 0.7f, VisR * 0.7f);
                PRTLoader.NewParticle<PRT_Spark>(shedPos, -Projectile.velocity * 0.12f + Main.rand.NextVector2Circular(1.4f, 1.4f)
                    , new Color(255, 216, 122), Main.rand.NextFloat(0.34f, 0.6f))
                    ?.Configure(true, Main.rand.Next(10, 18));
            }
            if (Main.rand.NextBool(7)) {
                PRTLoader.NewParticle<PRT_Sparkle>(Projectile.Center + Main.rand.NextVector2Circular(VisR, VisR)
                    , -Projectile.velocity * 0.05f, new Color(255, 240, 190), Main.rand.NextFloat(0.4f, 0.7f))
                    ?.Configure(new Color(255, 204, 96), Main.rand.Next(10, 16), 0.04f, 0.7f);
            }
        }

        public override bool OnTileCollide(Vector2 oldVelocity) => true;

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (VaultUtils.isServer) {
                return;
            }
            PRTLoader.NewParticle<PRT_Sparkle>(target.Center, Vector2.Zero
                , new Color(255, 246, 210), Main.rand.NextFloat(1f, 1.4f))
                ?.Configure(new Color(255, 198, 88), 16, 0.05f, 1.1f);
            for (int i = 0; i < 5; i++) {
                PRTLoader.NewParticle<PRT_Spark>(target.Center, Main.rand.NextVector2Circular(6f, 6f)
                    , new Color(255, 218, 126), Main.rand.NextFloat(0.4f, 0.66f))
                    ?.Configure(true, Main.rand.Next(12, 20));
            }
        }

        public override void OnKill(int timeLeft) {
            SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.8f, Pitch = -0.2f }, Projectile.Center);
            SoundEngine.PlaySound(SoundID.Item122 with { Volume = 0.5f, Pitch = -0.35f }, Projectile.Center);
            if (VaultUtils.isServer) {
                return;
            }
            if (Main.LocalPlayer.Distance(Projectile.Center) < 1200f) {
                Main.LocalPlayer.CWR().GetScreenShake(6.5f);
            }

            //双层金环:外环快张,内环滞后
            PRTLoader.NewParticle<PRT_DWave>(Projectile.Center, Vector2.Zero
                , new Color(255, 214, 120) * 0.8f, 0.3f)?.Configure(Vector2.One, 0f, 2.6f, 18);
            PRTLoader.NewParticle<PRT_DWave>(Projectile.Center, Vector2.Zero
                , new Color(255, 240, 190) * 0.7f, 0.14f)?.Configure(Vector2.One, 0f, 1.5f, 24);

            //星芒暴雨+火花喷散,余韵活得比弹体久
            for (int i = 0; i < 7; i++) {
                Vector2 vel = Main.rand.NextVector2CircularEdge(5f, 5f) * Main.rand.NextFloat(0.4f, 1f);
                PRTLoader.NewParticle<PRT_Sparkle>(Projectile.Center + Main.rand.NextVector2Circular(24f, 24f)
                    , vel, new Color(255, 242, 196), Main.rand.NextFloat(0.6f, 1.1f))
                    ?.Configure(new Color(255, 202, 92), Main.rand.Next(16, 28), 0.06f, 1f);
            }
            for (int i = 0; i < 16; i++) {
                Vector2 vel = Main.rand.NextVector2CircularEdge(9f, 9f) * Main.rand.NextFloat(0.3f, 1f);
                PRTLoader.NewParticle<PRT_Spark>(Projectile.Center, vel, new Color(255, 214, 118)
                    , Main.rand.NextFloat(0.4f, 0.72f))?.Configure(true, Main.rand.Next(14, 26));
            }
            Lighting.AddLight(Projectile.Center, new Vector3(1f, 0.88f, 0.5f) * 2f);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D glow = GlowTex?.Value;
            Texture2D star = StarTex?.Value;
            Texture2D canvas = VaultAsset.placeholder2?.Value;
            float pop = BirthPop;
            float visR = VisR * pop;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            float speed = Projectile.velocity.Length();
            float stretch = 1f + MathHelper.Clamp(speed * 0.02f, 0f, 0.55f);

            //残影拖影:旋转涂抹+位置重影(加光,轻量精灵)
            if (glow != null && star != null) {
                for (int k = 6; k >= 2; k -= 2) {
                    Vector2 gp = Projectile.oldPos[k];
                    if (gp == Vector2.Zero) {
                        continue;
                    }
                    float ga = (1f - k / 8f) * 0.3f;
                    Vector2 pos = gp + Projectile.Size * 0.5f - Main.screenPosition;
                    Main.EntitySpriteDraw(glow, pos, null, (new Color(255, 198, 96) with { A = 0 }) * ga, 0f
                        , glow.Size() * 0.5f, visR * 2f / glow.Width * 0.9f, SpriteEffects.None, 0);
                }
            }

            bool shaderOk = TiroFinaleRenderer.ShaderReady && canvas != null;
            if (shaderOk) {
                //批切换:实体批→Immediate 预乘 AlphaBlend 画 TechFinale 圆盘
                Main.spriteBatch.End();
                Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearClamp
                    , DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

                Effect effect = EffectLoader.TiroFinaleFX.Value;
                effect.CurrentTechnique = effect.Techniques["TechFinale"];
                effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
                effect.Parameters["uColDeep"]?.SetValue(TiroFinaleRenderer.ColDeep.ToVector3());
                effect.Parameters["uColMid"]?.SetValue(TiroFinaleRenderer.ColMid.ToVector3());
                effect.Parameters["uColBright"]?.SetValue(TiroFinaleRenderer.ColBright.ToVector3());
                effect.Parameters["uColHot"]?.SetValue(TiroFinaleRenderer.ColHot.ToVector3());
                effect.Parameters["uAlpha"]?.SetValue(1f);
                effect.Parameters["uForm"]?.SetValue(1f);
                effect.Parameters["uFire"]?.SetValue(MathHelper.Clamp(1.2f - Time / 20f, 0f, 1f));
                effect.Parameters["uLit"]?.SetValue(1f);
                effect.Parameters["uSeed"]?.SetValue(Projectile.ai[0] * 3.1f + 1.7f);
                effect.Parameters["uOpen"]?.SetValue(1f);
                effect.Parameters["uCharge"]?.SetValue(MathHelper.Clamp(speed / 40f, 0f, 1f));
                GraphicsDevice gd = Main.instance.GraphicsDevice;
                gd.Textures[1] = CWRAsset.PerlinNoise.Value;
                gd.SamplerStates[1] = SamplerState.LinearWrap;
                effect.CurrentTechnique.Passes[0].Apply();

                float quadPx = visR / TiroFinaleRenderer.CircleDiskFrac * 2f;
                Main.spriteBatch.Draw(canvas, drawPos, null, Color.White, Projectile.rotation
                    , canvas.Size() * 0.5f, new Vector2(quadPx * stretch, quadPx) / canvas.Width, SpriteEffects.None, 0f);

                Main.spriteBatch.End();
                Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState
                    , DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
            }
            else if (glow != null && star != null) {
                //回退:结构承体=四芒星实色体+辉光衬底+白热小芯
                Main.EntitySpriteDraw(glow, drawPos, null, (new Color(255, 190, 90) with { A = 0 }) * 0.75f
                    , 0f, glow.Size() * 0.5f, visR * 2.6f / glow.Width, SpriteEffects.None, 0);
                Main.EntitySpriteDraw(star, drawPos, null, (new Color(255, 216, 120) with { A = 255 }) * 0.9f
                    , Projectile.rotation + Time * 0.05f, star.Size() * 0.5f, visR * 2f / (star.Width * 0.69f), SpriteEffects.None, 0);
                Main.EntitySpriteDraw(star, drawPos, null, (Color.White with { A = 0 }) * 0.8f
                    , Projectile.rotation - Time * 0.07f, star.Size() * 0.5f, visR * 1.1f / (star.Width * 0.69f), SpriteEffects.None, 0);
            }
            return false;
        }
    }
}
