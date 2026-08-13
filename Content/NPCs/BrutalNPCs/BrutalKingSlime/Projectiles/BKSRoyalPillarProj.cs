using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalKingSlime.Rendering;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalKingSlime.Projectiles
{
    /// <summary>
    /// 皇权审判光柱：金线警示→轰落→淡出。锚定地面点，向上延伸<br/>
    /// ai[0]=警示帧数(错相排布) ；判定只在轰落段；服务端生成
    /// </summary>
    internal class BKSRoyalPillarProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private const int StrikeTime = 16;
        private const int FadeTime = 20;
        private const float PillarHeight = 1500f;
        private const float StrikeHalfWidth = 46f;

        private int WarnTime => (int)Projectile.ai[0] <= 0 ? 42 : (int)Projectile.ai[0];
        private int TotalLife => WarnTime + StrikeTime + FadeTime;

        private ref float Timer => ref Projectile.localAI[0];

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 2400;

        public override void SetDefaults() {
            Projectile.width = 60;
            Projectile.height = 60;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 400;
            CooldownSlot = ImmunityCooldownID.Bosses;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            Timer++;
            if (Timer >= TotalLife) {
                Projectile.Kill();
                return;
            }

            if ((int)Timer == 1) {
                KingSlimeGelFX.CrownChime(Projectile.Center, 0.55f, 0.6f);
            }

            //轰落帧
            if ((int)Timer == WarnTime + 1 && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item122 with { Pitch = -0.2f, Volume = 1f, MaxInstances = 4 }, Projectile.Center);
                SoundEngine.PlaySound(SoundID.Item68 with { Pitch = -0.4f, Volume = 0.7f, MaxInstances = 4 }, Projectile.Center);
                KingSlimeGelFX.CameraPunch(Projectile.Center, 5.5f, 14, "BKSRoyalPillar", Vector2.UnitY);
                KingSlimeGelFX.GoldGlint(Projectile.Center, 18, 8f);
                //落点金尘喷
                for (int i = 0; i < 12; i++) {
                    Dust d = Dust.NewDustDirect(Projectile.Center - new Vector2(30f, 8f), 60, 12,
                        DustID.GoldFlame, 0, 0, 100, default, Main.rand.NextFloat(1.2f, 2.2f));
                    d.noGravity = true;
                    d.velocity = new Vector2(Main.rand.NextFloat(-3f, 3f), -Main.rand.NextFloat(3f, 9f));
                }
            }

            float glow = Timer > WarnTime ? 1.4f : 0.4f;
            Lighting.AddLight(Projectile.Center, KingSlimeGelFX.CrownGold.ToVector3() * glow);
            Lighting.AddLight(Projectile.Center - new Vector2(0f, 300f), KingSlimeGelFX.CrownGold.ToVector3() * glow * 0.6f);
        }

        public override bool? CanDamage() {
            float t = Timer - WarnTime;
            return t is > 0 and <= StrikeTime + 6 ? null : false;
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            Rectangle column = new Rectangle(
                (int)(Projectile.Center.X - StrikeHalfWidth), (int)(Projectile.Center.Y - PillarHeight),
                (int)(StrikeHalfWidth * 2f), (int)PillarHeight + 20);
            return column.Intersects(targetHitbox);
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            target.AddBuff(BuffID.OnFire, 90);
        }

        public override bool PreDraw(ref Color lightColor) {
            Effect effect = EffectLoader.KingSlimeRoyalBeam?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (effect == null || noise == null) {
                //着色器不可用：单色光柱回退，绝不许无形判定
                DrawFallback();
                return false;
            }

            //阶段参数
            float phase;
            float warnProg = 0f, strikeProg = 0f, fadeProg = 0f, fadeAlpha = 1f;
            if (Timer <= WarnTime) {
                phase = 0f;
                warnProg = Timer / WarnTime;
            }
            else if (Timer <= WarnTime + StrikeTime) {
                phase = 1f;
                strikeProg = (Timer - WarnTime) / StrikeTime;
                fadeAlpha = 1.35f;
            }
            else {
                phase = 2f;
                fadeProg = (Timer - WarnTime - StrikeTime) / FadeTime;
                fadeAlpha = 1f - fadeProg * 0.9f;
            }

            effect.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            effect.Parameters["fadeAlpha"]?.SetValue(fadeAlpha);
            effect.Parameters["phase"]?.SetValue(phase);
            effect.Parameters["warnProg"]?.SetValue(warnProg);
            effect.Parameters["strikeProg"]?.SetValue(strikeProg);
            effect.Parameters["fadeProg"]?.SetValue(fadeProg);
            effect.Parameters["seed"]?.SetValue(Projectile.whoAmI * 0.193f % 1f);
            effect.Parameters["coreColor"]?.SetValue(new Vector3(1f, 0.98f, 0.9f));
            effect.Parameters["goldColor"]?.SetValue(KingSlimeGelFX.CrownGold.ToVector3());
            effect.Parameters["redColor"]?.SetValue(new Vector3(0.66f, 0.12f, 0.24f));
            effect.Parameters["uNoiseTex"]?.SetValue(noise);

            //quad：uv.x 0=天端 1=落点端
            Vector2 top = Projectile.Center - new Vector2(0f, PillarHeight);
            Vector2 bottom = Projectile.Center + new Vector2(0f, 46f);
            float halfW = 96f;
            VertexPositionColorTexture[] verts = new VertexPositionColorTexture[4];
            verts[0] = new VertexPositionColorTexture(new Vector3(top.X - halfW, top.Y, 0f), Color.White, new Vector2(0f, 0f));
            verts[1] = new VertexPositionColorTexture(new Vector3(top.X + halfW, top.Y, 0f), Color.White, new Vector2(0f, 1f));
            verts[2] = new VertexPositionColorTexture(new Vector3(bottom.X - halfW, bottom.Y, 0f), Color.White, new Vector2(1f, 0f));
            verts[3] = new VertexPositionColorTexture(new Vector3(bottom.X + halfW, bottom.Y, 0f), Color.White, new Vector2(1f, 1f));

            GraphicsDevice device = Main.graphics.GraphicsDevice;
            BlendState origBlend = device.BlendState;
            RasterizerState origRaster = device.RasterizerState;
            device.BlendState = BlendState.Additive;
            device.RasterizerState = RasterizerState.CullNone;

            foreach (EffectPass pass in effect.CurrentTechnique.Passes) {
                pass.Apply();
                device.DrawUserPrimitives(PrimitiveType.TriangleStrip, verts, 0, 2);
            }

            device.BlendState = origBlend;
            device.RasterizerState = origRaster;
            return false;
        }

        /// <summary>无着色器回退：细金线警示/宽金柱轰击</summary>
        private void DrawFallback() {
            Texture2D pixel = InnoVault.VaultAsset.placeholder2?.Value;
            if (pixel == null) {
                return;
            }
            bool striking = Timer > WarnTime && Timer <= WarnTime + StrikeTime + 6;
            float width = striking ? StrikeHalfWidth * 2f : 4f;
            float alpha = striking ? 0.75f
                : Timer <= WarnTime ? 0.3f + 0.15f * (float)System.Math.Sin(Main.GlobalTimeWrappedHourly * 14f)
                : 0.25f * MathHelper.Clamp(1f - (Timer - WarnTime - StrikeTime) / FadeTime, 0f, 1f);
            Vector2 top = Projectile.Center - new Vector2(0f, PillarHeight) - Main.screenPosition;
            Main.spriteBatch.Draw(pixel, top, null, KingSlimeGelFX.CrownGold with { A = 0 } * alpha, 0f,
                new Vector2(pixel.Width * 0.5f, 0f),
                new Vector2(width / pixel.Width, (PillarHeight + 40f) / pixel.Height), SpriteEffects.None, 0f);
        }
    }
}
