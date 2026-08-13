using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalQueenSlime.Core;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalQueenSlime.Projectiles
{
    /// <summary>水晶吊灯：空中物化→悬摆蓄能→预闪→坠落→触地绽裂放射碎晶；ai[0]=起始延迟 ai[2]=色相种子</summary>
    internal class QueenChandelierProj : ModProjectile, IPrimitiveDrawable, IAdditiveDrawable
    {
        public override string Texture => CWRConstant.VaultPlaceholder2;

        internal const int BurstDamage = 36;

        private const int MaterializeTime = 36;
        private const int HangTime = 66;
        private const int FlashTime = 12;
        private const float BodyRadius = 52f;

        private ref float Timer => ref Projectile.localAI[0];
        private int Delay => (int)Projectile.ai[0];
        private float HueSeed => Projectile.ai[2];

        /// <summary>0物化 1悬摆 2预闪 3坠落</summary>
        private int Stage {
            get {
                float t = Timer - Delay;
                if (t <= MaterializeTime) {
                    return 0;
                }
                if (t <= MaterializeTime + HangTime) {
                    return 1;
                }
                if (t <= MaterializeTime + HangTime + FlashTime) {
                    return 2;
                }
                return 3;
            }
        }

        private float MaterializeP => MathHelper.Clamp((Timer - Delay) / MaterializeTime, 0f, 1f);

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 320;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 56;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 700;
            CooldownSlot = ImmunityCooldownID.Bosses;
        }

        public override void AI() {
            Timer++;
            float t = Timer - Delay;
            if (t < 0f) {
                Projectile.velocity = Vector2.Zero;
                return;
            }

            switch (Stage) {
                case 0://物化
                    Projectile.velocity = Vector2.Zero;
                    if (t == 2) {
                        SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.55f, Pitch = 0.25f, MaxInstances = 5 }, Projectile.Center);
                    }
                    break;
                case 1://悬摆
                    Projectile.velocity = new Vector2((float)Math.Sin((Timer + Projectile.whoAmI * 13) * 0.06f) * 0.5f, 0f);
                    break;
                case 2://预闪定身
                    Projectile.velocity = Vector2.Zero;
                    if (t == MaterializeTime + HangTime + 1) {
                        SoundEngine.PlaySound(SoundID.Item4 with { Volume = 0.6f, Pitch = 0.7f, MaxInstances = 5 }, Projectile.Center);
                    }
                    break;
                case 3://坠落
                    Projectile.velocity.Y += 0.62f;
                    if (Projectile.velocity.Y > 21f) {
                        Projectile.velocity.Y = 21f;
                    }
                    Projectile.tileCollide = true;
                    if (!VaultUtils.isServer && Main.rand.NextBool(3)) {
                        Dust d = Dust.NewDustPerfect(Projectile.Top + Main.rand.NextVector2Circular(20f, 8f),
                            DustID.TintableDust, -Projectile.velocity * 0.12f, 130, QueenMotion.GetQueenDustColor(), 1.4f);
                        d.noGravity = true;
                    }
                    break;
            }

            Lighting.AddLight(Projectile.Center, QueenMotion.PrismHue(HueSeed).ToVector3() * (0.35f + 0.4f * MaterializeP));
        }

        /// <summary>只有坠落段有接触伤害</summary>
        public override bool? CanDamage() => Stage == 3 ? null : false;

        public override void OnKill(int timeLeft) {
            //只有坠落段的死亡才绽裂放射(被清场/悬挂期移除时安静消散，防公平阀反而放弹)
            if (Stage != 3) {
                if (!VaultUtils.isServer) {
                    QueenMotion.CrystalShatterBurst(Projectile.Center, 0.6f, HueSeed, playSound: false);
                }
                return;
            }

            //触地绽裂：放射碎晶(服务端)+演出(客户端)
            if (!VaultUtils.isClient) {
                for (int i = 0; i < 6; i++) {
                    float angle = MathHelper.Pi + MathHelper.Pi * i / 5f;
                    Vector2 vel = angle.ToRotationVector2() * 8.4f;
                    vel.Y = -Math.Abs(vel.Y) * 0.8f - 1.5f;
                    Projectile.NewProjectile(Projectile.GetSource_FromAI(), Projectile.Center, vel,
                        ModContent.ProjectileType<QueenShardProj>(), QueenShardProj.ShardDamage, 0f, Main.myPlayer,
                        (int)QueenShardProj.Mode.Shard, 0f, HueSeed + i * 0.14f);
                }
            }
            if (!VaultUtils.isServer) {
                QueenMotion.CrystalShatterBurst(Projectile.Center, 1.35f, HueSeed);
                QueenMotion.Shake(Projectile.Center, 4.5f, 12, "QueenChandelier");
                SoundEngine.PlaySound(SoundID.Item167 with { Volume = 0.6f, Pitch = 0.35f, MaxInstances = 4 }, Projectile.Center);
            }
        }

        public override bool PreDraw(ref Color lightColor) => false;

        /// <summary>吊灯晶簇本体(着色器倒锥quad)</summary>
        void IPrimitiveDrawable.DrawPrimitives() {
            Effect effect = EffectLoader.QueenPrismCrystal?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (effect == null || noise == null || Timer - Delay < 0f) {
                return;
            }

            float half = BodyRadius * 2.1f;
            Vector2 c = Projectile.Center;
            VertexPositionColorTexture[] verts = new VertexPositionColorTexture[4];
            verts[0] = new VertexPositionColorTexture(new Vector3(c.X - half, c.Y - half, 0f), Color.White, new Vector2(0f, 0f));
            verts[1] = new VertexPositionColorTexture(new Vector3(c.X + half, c.Y - half, 0f), Color.White, new Vector2(1f, 0f));
            verts[2] = new VertexPositionColorTexture(new Vector3(c.X - half, c.Y + half, 0f), Color.White, new Vector2(0f, 1f));
            verts[3] = new VertexPositionColorTexture(new Vector3(c.X + half, c.Y + half, 0f), Color.White, new Vector2(1f, 1f));

            GraphicsDevice device = Main.graphics.GraphicsDevice;
            BlendState origBlend = device.BlendState;
            RasterizerState origRaster = device.RasterizerState;
            device.BlendState = BlendState.AlphaBlend;
            device.RasterizerState = RasterizerState.CullNone;

            float t = Timer - Delay;
            float charge = Stage switch {
                0 => 0f,
                1 => MathHelper.Clamp((t - MaterializeTime) / HangTime, 0f, 1f) * 0.7f,
                2 => 0.7f + 0.3f * ((t - MaterializeTime - HangTime) / FlashTime),
                _ => 1f,
            };

            effect.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            effect.Parameters["uMode"]?.SetValue(2f);
            effect.Parameters["uGrow"]?.SetValue(MaterializeP);
            effect.Parameters["uShatter"]?.SetValue(0f);
            effect.Parameters["uCharge"]?.SetValue(charge);
            effect.Parameters["uHueSeed"]?.SetValue(HueSeed);
            effect.Parameters["seed"]?.SetValue(Projectile.whoAmI * 0.211f % 1f);
            effect.Parameters["uNoiseTex"]?.SetValue(noise);
            foreach (EffectPass pass in effect.CurrentTechnique.Passes) {
                pass.Apply();
                device.DrawUserPrimitives(PrimitiveType.TriangleStrip, verts, 0, 2);
            }

            device.BlendState = origBlend;
            device.RasterizerState = origRaster;
        }

        /// <summary>蓄能辉光/预闪</summary>
        void IAdditiveDrawable.DrawAdditiveAfterNon(SpriteBatch spriteBatch) {
            float t = Timer - Delay;
            if (t < 0f) {
                return;
            }
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Texture2D star = CWRAsset.StarTexture.Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Color hue = QueenMotion.PrismHue(HueSeed);

            if (Stage == 2) {
                //预闪：白热快闪
                float flashP = (t - MaterializeTime - HangTime) / FlashTime;
                float blink = 0.5f + 0.5f * (float)Math.Sin(flashP * MathHelper.Pi * 6f);
                spriteBatch.Draw(glow, drawPos, null, Color.White * (0.75f * blink), 0f, glow.Size() / 2f, 1.7f, SpriteEffects.None, 0f);
                spriteBatch.Draw(star, drawPos, null, Color.White * (0.8f * blink), flashP * 2f, star.Size() / 2f, 0.5f, SpriteEffects.None, 0f);
            }
            else {
                float p = MaterializeP;
                spriteBatch.Draw(glow, drawPos, null, hue * (0.4f * p), 0f, glow.Size() / 2f, 1.3f, SpriteEffects.None, 0f);
            }

            //坠落速度线
            if (Stage == 3) {
                float speed = Projectile.velocity.Length();
                float stretch = MathHelper.Clamp(speed * 0.06f, 0f, 1.4f);
                spriteBatch.Draw(glow, drawPos - Projectile.velocity * 0.8f, null, hue * 0.55f,
                    Projectile.velocity.ToRotation() - MathHelper.PiOver2, glow.Size() / 2f,
                    new Vector2(0.5f, 0.7f + stretch), SpriteEffects.None, 0f);
            }
        }
    }
}
