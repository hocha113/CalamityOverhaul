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
    /// <summary>皇后通用弹珠：折射碎晶(直线)/凝胶珍珠(弧线)/圆舞珠(外扩-凝滞-向心)</summary>
    internal class QueenShardProj : ModProjectile, IAdditiveDrawable
    {
        public override string Texture => CWRConstant.VaultPlaceholder2;

        internal enum Mode : int
        {
            /// <summary>折射碎晶，直线速射</summary>
            Shard = 0,
            /// <summary>凝胶珍珠，重力弧线</summary>
            Pearl = 1,
            /// <summary>圆舞珠：外扩→凝滞→向心收拢</summary>
            Converge = 2,
        }

        internal const int ShardDamage = 30;
        internal const int PearlDamage = 28;

        private const int ConvergeOutTime = 26;
        private const int ConvergeFreezeTime = 38;

        private Mode ProjMode => (Mode)(int)Projectile.ai[0];
        private float HueSeed => Projectile.ai[2];
        private ref float Timer => ref Projectile.localAI[0];

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 8;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 14;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 480;
            CooldownSlot = ImmunityCooldownID.Bosses;
        }

        public override void AI() {
            Timer++;

            switch (ProjMode) {
                case Mode.Shard:
                    //直线：出膛10帧复合加速，撞地即碎
                    if (Timer < 10) {
                        Projectile.velocity *= 1.045f;
                    }
                    Projectile.tileCollide = Timer > 8;
                    Projectile.rotation = Projectile.velocity.ToRotation();
                    break;

                case Mode.Pearl:
                    //重力弧线
                    Projectile.velocity.Y += 0.21f;
                    if (Projectile.velocity.Y > 11f) {
                        Projectile.velocity.Y = 11f;
                    }
                    Projectile.tileCollide = Timer > 12;
                    Projectile.rotation += Projectile.velocity.X * 0.03f;
                    break;

                case Mode.Converge:
                    UpdateConverge();
                    break;
            }

            Lighting.AddLight(Projectile.Center, QueenMotion.PrismHue(HueSeed).ToVector3() * 0.35f);

            //拖尾光尘
            if (!VaultUtils.isServer && Main.rand.NextBool(4) && Projectile.velocity.Length() > 1f) {
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.TintableDust,
                    -Projectile.velocity * 0.15f, 150, QueenMotion.GetQueenDustColor(), 1f);
                d.noGravity = true;
            }
        }

        /// <summary>外扩减速→原地凝滞闪烁→向心收拢</summary>
        private void UpdateConverge() {
            //首帧记录发射原点(AI先于位移执行，此刻仍在出生点)
            if (Timer == 1) {
                Projectile.localAI[1] = Projectile.Center.X;
                Projectile.localAI[2] = Projectile.Center.Y;
            }

            if (Timer <= ConvergeOutTime) {
                Projectile.velocity *= 0.915f;
            }
            else if (Timer <= ConvergeOutTime + ConvergeFreezeTime) {
                Projectile.velocity *= 0.7f;
                //凝滞末拍向心预备(轻微回缩)
                if (Timer == ConvergeOutTime + ConvergeFreezeTime - 4) {
                    Vector2 origin = new Vector2(Projectile.localAI[1], Projectile.localAI[2]);
                    Projectile.velocity = (Projectile.Center - origin).SafeNormalize(Vector2.UnitY) * 1.6f;
                }
            }
            else if (Timer == ConvergeOutTime + ConvergeFreezeTime + 1) {
                //向心收拢
                Vector2 origin = new Vector2(Projectile.localAI[1], Projectile.localAI[2]);
                Projectile.velocity = (origin - Projectile.Center).SafeNormalize(Vector2.UnitY) * 10.6f;
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item27 with { Volume = 0.4f, Pitch = 0.65f, MaxInstances = 3 }, Projectile.Center);
                }
            }
            else {
                //收拢途中穿过原点后不再折返，直至超时
                Projectile.tileCollide = true;
            }
            Projectile.rotation += 0.08f;
        }

        /// <summary>凝滞段无伤(可穿行)，其余按模式常规</summary>
        public override bool? CanDamage() {
            if (ProjMode == Mode.Converge && Timer > ConvergeOutTime && Timer <= ConvergeOutTime + ConvergeFreezeTime) {
                return false;
            }
            return null;
        }

        public override bool OnTileCollide(Vector2 oldVelocity) => true;

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            if (ProjMode == Mode.Pearl) {
                QueenMotion.GelSplashBurst(Projectile.Center, 0.55f, 3);
            }
            else {
                QueenMotion.CrystalShatterBurst(Projectile.Center, 0.4f, HueSeed, playSound: false);
                SoundEngine.PlaySound(SoundID.Item27 with { Volume = 0.32f, Pitch = 0.5f, MaxInstances = 4 }, Projectile.Center);
            }
        }

        public override bool PreDraw(ref Color lightColor) => false;

        /// <summary>加色层绘制(EndEntityDraw 真 Additive 批，染色须带 alpha)</summary>
        void IAdditiveDrawable.DrawAdditiveAfterNon(SpriteBatch spriteBatch) {
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Texture2D star = CWRAsset.StarGlow01.Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Color hue = QueenMotion.PrismHue(HueSeed);
            bool pearl = ProjMode == Mode.Pearl;
            float freezeTwinkle = 1f;
            if (ProjMode == Mode.Converge && Timer > ConvergeOutTime && Timer <= ConvergeOutTime + ConvergeFreezeTime) {
                freezeTwinkle = 0.7f + 0.3f * (float)Math.Sin(Timer * 0.5f + Projectile.whoAmI);
            }

            //残影链
            for (int i = Projectile.oldPos.Length - 1; i >= 1; i--) {
                if (Projectile.oldPos[i] == Vector2.Zero) {
                    continue;
                }
                Vector2 ghostPos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                float fade = (1f - i / (float)Projectile.oldPos.Length) * 0.4f;
                spriteBatch.Draw(glow, ghostPos, null, hue * (fade * freezeTwinkle), 0f,
                    glow.Size() / 2f, 0.34f * fade + 0.1f, SpriteEffects.None, 0f);
            }

            //本体：速度拉伸光核
            float speed = Projectile.velocity.Length();
            float stretch = MathHelper.Clamp(speed * 0.045f, 0f, 0.8f);
            Vector2 bodyScale = pearl
                ? new Vector2(0.42f, 0.42f)
                : new Vector2(0.3f - stretch * 0.1f, 0.3f + stretch * 0.35f);
            float bodyRot = speed > 0.5f && !pearl ? Projectile.velocity.ToRotation() - MathHelper.PiOver2 : Projectile.rotation;

            spriteBatch.Draw(glow, drawPos, null, hue * (0.9f * freezeTwinkle), bodyRot, glow.Size() / 2f, bodyScale, SpriteEffects.None, 0f);
            spriteBatch.Draw(glow, drawPos, null, Color.White * (0.68f * freezeTwinkle), bodyRot, glow.Size() / 2f, bodyScale * 0.5f, SpriteEffects.None, 0f);
            //晶面星芒
            spriteBatch.Draw(star, drawPos, null, hue * (0.85f * freezeTwinkle),
                Projectile.rotation + Timer * 0.04f, star.Size() / 2f, pearl ? 0.24f : 0.34f, SpriteEffects.None, 0f);
        }
    }
}
