using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalQueenSlime.Core;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalQueenSlime.Projectiles
{
    /// <summary>翼压风道：沿滑翔线的推挤风场，无伤纯位移压制；ai[0]=风向角 ai[2]=色相种子</summary>
    internal class QueenGaleFieldProj : ModProjectile, IPrimitiveDrawable
    {
        public override string Texture => CWRConstant.VaultPlaceholder2;

        private const int ArmTime = 30;
        private const int LifeTime = 250;
        private const float LaneLength = 1500f;
        private const float LaneHalfWidth = 128f;
        /// <summary>每帧推挤加速度</summary>
        private const float PushAccel = 0.5f;
        /// <summary>顺风向速度上限，超过不再加力</summary>
        private const float PushSpeedCap = 8.5f;

        private float LaneAngle => Projectile.ai[0];
        private float HueSeed => Projectile.ai[2];
        private ref float Timer => ref Projectile.localAI[0];

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 1600;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 32;
            Projectile.hostile = false;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = LifeTime;
        }

        public override void AI() {
            Timer++;
            Projectile.velocity = Vector2.Zero;

            Vector2 dir = LaneAngle.ToRotationVector2();
            Vector2 perp = dir.RotatedBy(MathHelper.PiOver2);

            //臂展期只演出不推人
            bool armed = Timer > ArmTime;
            float strength = FieldStrength();

            if (armed) {
                //推挤所有处于风道内的玩家(各端本地推自机，标准风场做法)
                foreach (var player in Main.ActivePlayers) {
                    if (player.dead || player.ghost) {
                        continue;
                    }
                    Vector2 rel = player.Center - Projectile.Center;
                    float along = Vector2.Dot(rel, dir);
                    float across = Vector2.Dot(rel, perp);
                    if (System.Math.Abs(along) > LaneLength * 0.5f || System.Math.Abs(across) > LaneHalfWidth) {
                        continue;
                    }
                    //顺风向未达上限才继续加力
                    float alongVel = Vector2.Dot(player.velocity, dir);
                    if (alongVel < PushSpeedCap) {
                        player.velocity += dir * PushAccel * strength;
                    }
                }
            }

            //风纹尘：沿风道漂流
            if (!VaultUtils.isServer && strength > 0.2f) {
                for (int i = 0; i < 2; i++) {
                    Vector2 spawn = Projectile.Center
                        + dir * Main.rand.NextFloat(-LaneLength * 0.5f, LaneLength * 0.5f)
                        + perp * Main.rand.NextFloat(-LaneHalfWidth, LaneHalfWidth);
                    Dust d = Dust.NewDustPerfect(spawn, DustID.TintableDust,
                        dir * Main.rand.NextFloat(6f, 12f) * strength, 160, QueenMotion.GetQueenDustColor(), 1.2f);
                    d.noGravity = true;
                    d.fadeIn = 0.6f;
                }
            }
        }

        /// <summary>风场强度包络：起臂渐入→稳态→末段渐出</summary>
        private float FieldStrength() {
            float fadeIn = MathHelper.Clamp(Timer / (float)ArmTime, 0f, 1f);
            float fadeOut = MathHelper.Clamp(Projectile.timeLeft / 40f, 0f, 1f);
            return fadeIn * fadeOut;
        }

        public override bool? CanDamage() => false;

        public override bool PreDraw(ref Color lightColor) => false;

        /// <summary>风场帷幕(着色器长条quad)</summary>
        void IPrimitiveDrawable.DrawPrimitives() {
            Effect effect = EffectLoader.QueenGaleField?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (effect == null || noise == null) {
                return;
            }

            Vector2 dir = LaneAngle.ToRotationVector2();
            Vector2 perp = dir.RotatedBy(MathHelper.PiOver2);
            Vector2 a = Projectile.Center - dir * LaneLength * 0.5f;
            Vector2 b = Projectile.Center + dir * LaneLength * 0.5f;
            float halfW = LaneHalfWidth * 1.3f;

            VertexPositionColorTexture[] verts = new VertexPositionColorTexture[4];
            verts[0] = new VertexPositionColorTexture((a + perp * halfW).ToVector3(), Color.White, new Vector2(0f, 0f));
            verts[1] = new VertexPositionColorTexture((a - perp * halfW).ToVector3(), Color.White, new Vector2(0f, 1f));
            verts[2] = new VertexPositionColorTexture((b + perp * halfW).ToVector3(), Color.White, new Vector2(1f, 0f));
            verts[3] = new VertexPositionColorTexture((b - perp * halfW).ToVector3(), Color.White, new Vector2(1f, 1f));

            GraphicsDevice device = Main.graphics.GraphicsDevice;
            BlendState origBlend = device.BlendState;
            RasterizerState origRaster = device.RasterizerState;
            device.BlendState = BlendState.Additive;
            device.RasterizerState = RasterizerState.CullNone;

            effect.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            effect.Parameters["uStrength"]?.SetValue(FieldStrength());
            effect.Parameters["uHueSeed"]?.SetValue(HueSeed);
            effect.Parameters["seed"]?.SetValue(Projectile.whoAmI * 0.149f % 1f);
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
    }
}
