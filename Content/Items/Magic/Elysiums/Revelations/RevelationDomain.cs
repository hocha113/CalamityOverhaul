using CalamityOverhaul.Common;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Magic.Elysiums.Revelations
{
    /// <summary>
    /// 天国领域：启示录降临时展开的圣域。
    /// 开幕演出(前80帧)：白光吞屏渐褪、领域自中心撑开、光尘如雨升腾；
    /// 稳态：天体圣域盘随行主人，环内光尘缓缓飘落。
    /// 增益接线在 <see cref="ElysiumPlayer.UpdateEquips"/>；此弹幕存活即启示录可见真相
    /// </summary>
    internal class RevelationDomain : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        public const float DomainRadius = 620f;
        private const int OpeningTime = 80;

        private int timer;
        private float ExpandProgress => VaultUtils.EaseOutCubic(Math.Min(timer / (float)OpeningTime, 1f));
        private Player Owner => Main.player[Projectile.owner];

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 2000;

        public override void SetDefaults() {
            Projectile.width = 60;
            Projectile.height = 60;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 120;
            Projectile.netImportant = true;
        }

        public override void AI() {
            if (!Owner.active || Owner.dead) {
                Projectile.Kill();
                return;
            }
            Projectile.timeLeft = 120;
            timer++;
            Projectile.Center = Vector2.Lerp(Projectile.Center, Owner.Center, 0.2f);

            //主人端：启示录终止即收
            if (Projectile.IsOwnedByLocalPlayer()
                && (!Owner.TryGetModPlayer(out ElysiumPlayer ep) || !ep.IsRevelationActive)) {
                Projectile.Kill();
                return;
            }

            //开幕拍序
            if (timer == 1) {
                SoundEngine.PlaySound(SoundID.Item122 with { Volume = 1.4f, Pitch = -0.3f }, Owner.Center);
                SoundEngine.PlaySound(SoundID.Item105 with { Volume = 1.1f, Pitch = 0.3f }, Owner.Center);
                SoundEngine.PlaySound(SoundID.Item4 with { Volume = 1f, Pitch = -0.1f }, Owner.Center);
                Owner.CWR().ScreenShakeValue = Math.Max(Owner.CWR().ScreenShakeValue, 6f);
            }

            //开幕光尘如雨升腾
            if (!Main.dedServ && timer < OpeningTime && timer % 2 == 0) {
                float radius = DomainRadius * ExpandProgress;
                Vector2 pos = Owner.Center + Main.rand.NextVector2Circular(radius, radius * 0.6f);
                PRTLoader.NewParticle<PRT_Light>(pos, new Vector2(0f, -Main.rand.NextFloat(2f, 5f))
                    , new Color(255, 238, 190), Main.rand.NextFloat(0.24f, 0.42f))?.Configure(Main.rand.Next(24, 40), 0.9f);
            }

            //稳态环境：环内光雨缓落
            if (!Main.dedServ && timer >= OpeningTime && Main.rand.NextBool(3)) {
                Vector2 pos = Owner.Center + new Vector2(Main.rand.NextFloat(-DomainRadius, DomainRadius), -Main.rand.NextFloat(200f, 380f));
                PRTLoader.NewParticle<PRT_Light>(pos, new Vector2(Main.rand.NextFloat(-0.3f, 0.3f), Main.rand.NextFloat(0.8f, 1.8f))
                    , new Color(255, 232, 175), Main.rand.NextFloat(0.16f, 0.3f))?.Configure(Main.rand.Next(30, 50), 0.7f);
            }

            float glow = 0.8f * ExpandProgress;
            Lighting.AddLight(Owner.Center, glow, glow * 0.94f, glow * 0.8f);
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            Texture2D canvas = VaultAsset.placeholder2?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            Effect effect = EffectLoader.CelestialDomain?.Value;
            if (canvas == null) {
                return false;
            }

            //开幕白光吞屏：全屏白闪快速退潮
            if (timer < 34) {
                float flash = (1f - timer / 34f);
                flash *= flash;
                sb.Draw(canvas, new Rectangle(0, 0, Main.screenWidth, Main.screenHeight)
                    , new Rectangle(0, 0, 1, 1), Color.White * (flash * 0.85f));
            }

            if (effect == null || noise == null) {
                return false;
            }

            float quadSize = (DomainRadius + 120f) * 2f * ExpandProgress;
            if (quadSize < 20f) {
                return false;
            }

            effect.CurrentTechnique = effect.Techniques["CelestialDomainPass"];
            effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            effect.Parameters["fadeAlpha"]?.SetValue(0.55f);
            effect.Parameters["expandProgress"]?.SetValue(ExpandProgress);
            effect.Parameters["revelationIntensity"]?.SetValue(0.75f + 0.25f * MathF.Sin(Main.GlobalTimeWrappedHourly * 1.4f));
            effect.Parameters["coreColor"]?.SetValue(new Vector3(1f, 0.97f, 0.9f));
            effect.Parameters["haloColor"]?.SetValue(new Vector3(1f, 0.83f, 0.45f));
            effect.Parameters["divineColor"]?.SetValue(new Vector3(0.6f, 0.75f, 1f));
            effect.Parameters["gloryColor"]?.SetValue(new Vector3(0.75f, 0.6f, 0.95f));

            Main.graphics.GraphicsDevice.Textures[1] = noise;
            Main.graphics.GraphicsDevice.SamplerStates[1] = SamplerState.LinearWrap;

            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.Additive,
                SamplerState.LinearClamp, DepthStencilState.None,
                RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
            effect.CurrentTechnique.Passes[0].Apply();
            sb.Draw(canvas, Projectile.Center - Main.screenPosition, null, Color.White, 0f,
                canvas.Size() * 0.5f, quadSize, SpriteEffects.None, 0f);
            sb.End();

            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                Main.DefaultSamplerState, DepthStencilState.None,
                Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
            return false;
        }

        public override void OnKill(int timeLeft) {
            SoundEngine.PlaySound(SoundID.Item8 with { Volume = 1.1f, Pitch = -0.15f }, Projectile.Center);
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < 16; i++) {
                PRTLoader.NewParticle<PRT_Light>(Owner.Center + Main.rand.NextVector2Circular(160f, 120f)
                    , new Vector2(0f, -Main.rand.NextFloat(1f, 3f)), new Color(255, 240, 205)
                    , Main.rand.NextFloat(0.2f, 0.38f))?.Configure(Main.rand.Next(24, 44), 0.85f);
            }
        }
    }
}
