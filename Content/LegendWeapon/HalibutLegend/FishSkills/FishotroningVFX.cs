using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.FishSkills
{
    /// <summary>骷臂骨屑，chunk=原版骨头大块，chip=Extra_98 细条，AlphaBlend 场景光零发光</summary>
    internal class PRT_FishOtronShard : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "Extra_98";
        public override bool CanPool => true;

        //骨白哑光基调，实际绘制乘场景光
        private static readonly Color BoneTint = new(228, 220, 202);

        private bool chunk;
        private float gravity;
        private float spin;

        public PRT_FishOtronShard Configure(int lifetime, bool bigChunk = false, float gravityStrength = 0.34f) {
            Lifetime = lifetime;
            chunk = bigChunk;
            gravity = gravityStrength;
            return this;
        }

        public override void Reset() {
            base.Reset();
            chunk = false;
            gravity = 0f;
            spin = 0f;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AlphaBlend;
            Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
            spin = Main.rand.NextFloat(0.16f, 0.4f) * (Main.rand.NextBool() ? 1f : -1f);
            //防漏 Configure 兜底
            if (Lifetime <= 0) {
                Lifetime = Main.rand.Next(24, 38);
            }
            if (gravity == 0f) {
                gravity = 0.34f;
            }
        }

        public override void AI() {
            if (Velocity.Y < 15f) {
                Velocity.Y += gravity;
            }
            Velocity.X *= 0.985f;
            Rotation += spin;
            //翻滚随坠落缓慢减速，读作实物而非陀螺
            spin *= 0.988f;
            Opacity = MathHelper.Clamp((1f - LifetimeCompletion) * 2.6f, 0f, 1f);

            //大块骨骸坠落时剥落钙尘
            if (chunk && Main.rand.NextBool(7)) {
                Dust dust = Dust.NewDustPerfect(Position, DustID.Bone
                    , -Velocity * 0.06f, 140, default, Main.rand.NextFloat(0.6f, 0.9f));
                dust.noGravity = false;
            }
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex;
            Vector2 scale;
            if (chunk) {
                Main.instance.LoadProjectile(ProjectileID.Bone);
                tex = TextureAssets.Projectile[ProjectileID.Bone].Value;
                scale = new Vector2(1f, 1f) * Scale;
            }
            else {
                tex = TexValue;
                scale = new Vector2(0.26f, 0.68f) * Scale;
            }

            Vector2 pos = Position - Main.screenPosition;
            Vector2 origin = tex.Size() * 0.5f;
            //场景光×骨白
            Color lit = Lighting.GetColor((int)(Position.X / 16f), (int)(Position.Y / 16f));
            Color body = new Color(lit.R * BoneTint.R / 255, lit.G * BoneTint.G / 255, lit.B * BoneTint.B / 255, (byte)255);

            //旋转拖影
            for (int i = 2; i >= 1; i--) {
                float k = i / 3f;
                spriteBatch.Draw(tex, pos - Velocity * (i * 0.7f), null, body * (Opacity * (0.3f - k * 0.12f))
                    , Rotation - spin * i * 2.2f, origin, scale * (1f - k * 0.12f), SpriteEffects.None, 0f);
            }
            spriteBatch.Draw(tex, pos, null, body * Opacity, Rotation, origin, scale, SpriteEffects.None, 0f);
            return false;
        }
    }

    /// <summary>
    /// 骷臂投掷骨（替换裸 ProjectileID.Bone 的表现层，伤害/击退/数量/散射全部沿用原调用值）
    /// 重力弧＋自旋随速度衰减，飞行 = 历史旋转角残影链＋剥落骨尘，落点碎裂成骨屑
    /// </summary>
    internal class FishotroningBone : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.Bone;

        private float spin;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 5;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 16;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 300;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = false;
        }

        public override void AI() {
            //出手强旋，飞行中随空气阻力缓慢衰减（飞行期有量在演化）
            if (spin == 0f) {
                spin = (0.24f + Projectile.velocity.Length() * 0.007f) * Math.Sign(Projectile.velocity.X == 0f ? 1f : Projectile.velocity.X);
            }
            spin *= 0.995f;
            Projectile.rotation += spin;

            //延迟重力，先直飞一小段再入抛物弧
            Projectile.ai[0]++;
            if (Projectile.ai[0] > 10f) {
                if (Projectile.velocity.Y < 16f) {
                    Projectile.velocity.Y += 0.3f;
                }
            }

            //剥落骨尘∝速度
            if (!VaultUtils.isServer && Projectile.ai[0] % 4 == 0 && Projectile.velocity.Length() > 6f) {
                Dust dust = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(4f, 4f)
                    , DustID.Bone, -Projectile.velocity * 0.08f, 130, default, Main.rand.NextFloat(0.7f, 1.1f));
                dust.noGravity = false;
            }
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            //碎裂，骨屑抛物＋钙尘，低沉碎骨声
            for (int i = 0; i < 3; i++) {
                PRTLoader.NewParticle<PRT_FishOtronShard>(Projectile.Center
                    , new Vector2(Main.rand.NextFloat(-3.5f, 3.5f), Main.rand.NextFloat(-4.5f, -1f))
                    , default, Main.rand.NextFloat(0.6f, 1f))
                    ?.Configure(Main.rand.Next(20, 32));
            }
            for (int i = 0; i < 6; i++) {
                Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.Bone
                    , Main.rand.NextVector2Circular(3f, 3f) - Vector2.UnitY * 1.5f
                    , 120, default, Main.rand.NextFloat(0.9f, 1.4f));
                dust.noGravity = false;
            }
            SoundEngine.PlaySound(SoundID.NPCHit2 with { Volume = 0.45f, Pitch = 0.3f, MaxInstances = 5 }, Projectile.Center);
        }

        public override bool PreDraw(ref Color lightColor) {
            Main.instance.LoadProjectile(ProjectileID.Bone);
            Texture2D tex = TextureAssets.Projectile[ProjectileID.Bone].Value;
            Vector2 origin = tex.Size() * 0.5f;

            //旋转拖影链
            for (int i = Projectile.oldPos.Length - 1; i >= 1; i--) {
                if (Projectile.oldPos[i] == Vector2.Zero) {
                    continue;
                }
                float fade = 1f - i / (float)Projectile.oldPos.Length;
                Vector2 ghostPos = Projectile.oldPos[i] + Projectile.Size * 0.5f - Main.screenPosition;
                Main.EntitySpriteDraw(tex, ghostPos, null, lightColor * (fade * 0.3f)
                    , Projectile.oldRot[i], origin, 1f - i * 0.06f, SpriteEffects.None, 0);
            }

            Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, null, lightColor
                , Projectile.rotation, origin, 1f, SpriteEffects.None, 0);
            return false;
        }
    }
}
