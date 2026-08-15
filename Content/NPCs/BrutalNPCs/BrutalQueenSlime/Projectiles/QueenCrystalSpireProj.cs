using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalQueenSlime.Core;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalQueenSlime.Projectiles
{
    /// <summary>水晶尖塔：地面预兆→拔地而起→定持→碎裂；ai[0]=起始延迟 ai[2]=色相种子</summary>
    internal class QueenCrystalSpireProj : ModProjectile, IPrimitiveDrawable, IAdditiveDrawable
    {
        public override string Texture => CWRConstant.VaultPlaceholder2;

        internal const int SpireDamage = 40;

        private const int TelegraphTime = 26;
        private const int EruptTime = 9;
        private const int HoldTime = 30;
        private const int CrumbleTime = 16;
        private const float SpireHeight = 196f;
        private const float SpireHalfWidth = 30f;

        private ref float Timer => ref Projectile.localAI[0];
        private int Delay => (int)Projectile.ai[0];
        private float HueSeed => Projectile.ai[2];

        /// <summary>当前塔尖伸出高度</summary>
        private float currentHeight;
        private bool grounded;

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 480;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 24;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 500;
            CooldownSlot = ImmunityCooldownID.Bosses;
        }

        public override void AI() {
            //首帧吸附地面
            if (!grounded) {
                grounded = true;
                Projectile.Center = QueenMotion.FindGroundBelow(Projectile.Center - new Vector2(0f, 60f));
                Projectile.velocity = Vector2.Zero;
            }

            Timer++;
            float t = Timer - Delay;
            if (t < 0f) {
                return;
            }

            if (t <= TelegraphTime) {
                //预兆：地面光尘上涌
                currentHeight = 0f;
                if (!VaultUtils.isServer && t % 3 == 0) {
                    Dust d = Dust.NewDustPerfect(Projectile.Center + new Vector2(Main.rand.NextFloat(-SpireHalfWidth, SpireHalfWidth), 2f),
                        DustID.TintableDust, new Vector2(0f, -Main.rand.NextFloat(2f, 5f)), 130,
                        QueenMotion.GetQueenDustColor(), 1.5f);
                    d.noGravity = true;
                }
                Lighting.AddLight(Projectile.Center, QueenMotion.CrystalBlue.ToVector3() * (t / TelegraphTime) * 0.6f);
            }
            else if (t <= TelegraphTime + EruptTime) {
                //拔地：高次幂急速顶出
                float p = (t - TelegraphTime) / EruptTime;
                currentHeight = SpireHeight * QueenMotion.SnapOut(p, 6);
                if (t == TelegraphTime + 1) {
                    SoundEngine.PlaySound(SoundID.Item27 with { Volume = 0.7f, Pitch = -0.1f, MaxInstances = 5 }, Projectile.Center);
                    QueenMotion.Shake(Projectile.Center, 3.2f, 10, "QueenSpire");
                    QueenMotion.CrystalShatterBurst(Projectile.Center - new Vector2(0f, 10f), 0.7f, HueSeed, playSound: false);
                }
            }
            else if (t <= TelegraphTime + EruptTime + HoldTime) {
                currentHeight = SpireHeight;
            }
            else if (t <= TelegraphTime + EruptTime + HoldTime + CrumbleTime) {
                //碎裂缩没
                float p = (t - TelegraphTime - EruptTime - HoldTime) / CrumbleTime;
                currentHeight = SpireHeight * (1f - p * p);
                if (t == TelegraphTime + EruptTime + HoldTime + 1 && !VaultUtils.isServer) {
                    QueenMotion.CrystalShatterBurst(Projectile.Center - new Vector2(0f, SpireHeight * 0.7f), 0.85f, HueSeed);
                }
            }
            else {
                Projectile.Kill();
                return;
            }

            if (currentHeight > 10f) {
                Lighting.AddLight(Projectile.Center - new Vector2(0f, currentHeight * 0.6f),
                    QueenMotion.PrismHue(HueSeed).ToVector3() * 0.5f);
            }
        }

        /// <summary>只有塔体伸出时造成伤害</summary>
        public override bool? CanDamage() {
            float t = Timer - Delay;
            return t > TelegraphTime && t <= TelegraphTime + EruptTime + HoldTime ? null : false;
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (currentHeight < 16f) {
                return false;
            }
            float p = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(),
                Projectile.Center, Projectile.Center - new Vector2(0f, currentHeight),
                SpireHalfWidth * 1.5f, ref p);
        }

        public override bool PreDraw(ref Color lightColor) => false;

        /// <summary>晶塔主体：着色器竖直quad</summary>
        void IPrimitiveDrawable.DrawPrimitives() {
            if (currentHeight < 4f) {
                return;
            }
            Effect effect = EffectLoader.QueenPrismCrystal?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (effect == null || noise == null) {
                return;
            }

            //quad：底边贴地，顶到塔尖，横向留辉光余量
            float halfW = SpireHalfWidth * 2.6f;
            Vector2 basePos = Projectile.Center + new Vector2(0f, 8f);
            Vector2 tip = basePos - new Vector2(0f, currentHeight + 26f);

            VertexPositionColorTexture[] verts = new VertexPositionColorTexture[4];
            verts[0] = new VertexPositionColorTexture(new Vector3(tip.X - halfW, tip.Y, 0f), Color.White, new Vector2(0f, 0f));
            verts[1] = new VertexPositionColorTexture(new Vector3(tip.X + halfW, tip.Y, 0f), Color.White, new Vector2(1f, 0f));
            verts[2] = new VertexPositionColorTexture(new Vector3(basePos.X - halfW, basePos.Y, 0f), Color.White, new Vector2(0f, 1f));
            verts[3] = new VertexPositionColorTexture(new Vector3(basePos.X + halfW, basePos.Y, 0f), Color.White, new Vector2(1f, 1f));

            GraphicsDevice device = Main.graphics.GraphicsDevice;
            BlendState origBlend = device.BlendState;
            RasterizerState origRaster = device.RasterizerState;
            device.BlendState = BlendState.AlphaBlend;
            device.RasterizerState = RasterizerState.CullNone;

            float t = Timer - Delay;
            float grow = MathHelper.Clamp(currentHeight / SpireHeight, 0f, 1f);
            float crumble = MathHelper.Clamp((t - TelegraphTime - EruptTime - HoldTime) / CrumbleTime, 0f, 1f);

            effect.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            effect.Parameters["uMode"]?.SetValue(1f);
            effect.Parameters["uGrow"]?.SetValue(grow);
            effect.Parameters["uShatter"]?.SetValue(crumble);
            effect.Parameters["uCharge"]?.SetValue(0f);
            effect.Parameters["uHueSeed"]?.SetValue(HueSeed);
            effect.Parameters["seed"]?.SetValue(Projectile.whoAmI * 0.173f % 1f);
            //噪声显式绑到 s1（shader 内 register(s1)），参数式绑定废弃
            device.Textures[1] = noise;
            device.SamplerStates[1] = SamplerState.LinearWrap;
            foreach (EffectPass pass in effect.CurrentTechnique.Passes) {
                pass.Apply();
                device.DrawUserPrimitives(PrimitiveType.TriangleStrip, verts, 0, 2);
            }

            device.BlendState = origBlend;
            device.RasterizerState = origRaster;
        }

        /// <summary>基座辉光与预兆亮线</summary>
        void IAdditiveDrawable.DrawAdditiveAfterNon(SpriteBatch spriteBatch) {
            float t = Timer - Delay;
            if (t < 0f) {
                return;
            }
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Vector2 basePos = Projectile.Center - Main.screenPosition;
            Color hue = QueenMotion.PrismHue(HueSeed);

            //预兆期地表亮线爬升
            if (t <= TelegraphTime) {
                float p = t / TelegraphTime;
                spriteBatch.Draw(glow, basePos, null, hue * (0.55f * p), 0f, glow.Size() / 2f,
                    new Vector2(1.5f, 0.24f) * (0.5f + p * 0.7f), SpriteEffects.None, 0f);
                spriteBatch.Draw(glow, basePos, null, Color.White * (0.35f * p), 0f, glow.Size() / 2f,
                    new Vector2(0.8f, 0.14f) * (0.5f + p * 0.7f), SpriteEffects.None, 0f);
                return;
            }

            //塔体常驻基座光
            float baseFade = MathHelper.Clamp(currentHeight / SpireHeight, 0f, 1f);
            spriteBatch.Draw(glow, basePos, null, hue * (0.5f * baseFade), 0f, glow.Size() / 2f,
                new Vector2(1.7f, 0.4f), SpriteEffects.None, 0f);
        }
    }
}
