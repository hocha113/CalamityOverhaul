using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalGolem.Projectiles
{
    /// <summary>神庙尖刺球：尖刺球机关吊顶落下，受重力沿地弹跳滚动，寿命尽了沉回石缝。
    /// 本体=原版 Projectile_185，拖尾=同贴图残影（同材质、同宽）</summary>
    internal class GolemSpikyBall : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.SpikyBallTrap;

        private const int LifeFrames = 170;
        private const int FadeFrames = 22;
        private const float Gravity = 0.24f;
        private const float MaxFall = 11f;

        private float FadeFactor => MathHelper.Clamp(Projectile.timeLeft / (float)FadeFrames, 0f, 1f);

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 6;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 14;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = LifeFrames;
            Projectile.netImportant = true;
        }

        public override void AI() {
            Projectile.velocity.Y = Math.Min(Projectile.velocity.Y + Gravity, MaxFall);
            //滚动：转角跟横速走，落地慢滚、腾空快转
            Projectile.rotation += Projectile.velocity.X * 0.09f + Math.Sign(Projectile.velocity.X) * 0.02f;
            Lighting.AddLight(Projectile.Center, new Vector3(0.32f, 0.18f, 0.05f) * FadeFactor);

            //快速滚动时刮出火星（铁刺蹭石地）
            if (!Main.dedServ && Projectile.velocity.Y == 0f && Math.Abs(Projectile.velocity.X) > 2.5f && Main.rand.NextBool(5)) {
                Dust spark = Dust.NewDustPerfect(Projectile.Bottom, DustID.Torch,
                    new Vector2(-Projectile.velocity.X * 0.2f, -Main.rand.NextFloat(0.5f, 1.5f)), 0, default, 0.9f);
                spark.noGravity = true;
            }
            //地面摩擦：滚着滚着停下来，不是永动
            if (Projectile.velocity.Y == 0f) {
                Projectile.velocity.X *= 0.985f;
            }
        }

        /// <summary>弹跳：横向反弹保留八成，纵向反弹吃掉三成，慢下来后贴地滚</summary>
        public override bool OnTileCollide(Vector2 oldVelocity) {
            bool bounced = false;
            if (Projectile.velocity.X != oldVelocity.X) {
                Projectile.velocity.X = -oldVelocity.X * 0.8f;
                bounced = Math.Abs(oldVelocity.X) > 1.5f;
            }
            if (Projectile.velocity.Y != oldVelocity.Y && oldVelocity.Y > 1.2f) {
                Projectile.velocity.Y = -oldVelocity.Y * 0.7f;
                bounced = true;
            }
            if (bounced && !Main.dedServ) {
                SoundEngine.PlaySound(SoundID.Dig with { Pitch = 0.2f, Volume = 0.45f }, Projectile.Center);
                for (int i = 0; i < 3; i++) {
                    Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height,
                        DustID.Stone, 0f, -1f, 70, default, 1f);
                    dust.velocity *= 0.5f;
                }
            }
            return false;
        }

        /// <summary>收尾淡出期不再伤人（视觉窄于判定就该关掉判定）</summary>
        public override bool? CanDamage() => Projectile.timeLeft > FadeFrames ? null : false;

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Tink with { Pitch = -0.2f, Volume = 0.5f }, Projectile.Center);
            for (int i = 0; i < 4; i++) {
                PRTLoader.NewParticle<PRT_MarbleChip>(Projectile.Center,
                    Main.rand.NextVector2Circular(2f, 1.5f) - Vector2.UnitY * 1.2f,
                    new Color(122, 104, 78), Main.rand.NextFloat(0.5f, 0.9f)).Configure(32);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Vector2 origin = tex.Size() / 2f;
            float fade = FadeFactor;
            //淡出期沉入地缝：贴图整体下沉并压扁
            float sink = (1f - fade) * 6f;
            Vector2 squash = new(1f, MathHelper.Lerp(0.55f, 1f, fade));

            //残影拖尾：同一张贴图按旧位旧角淡画（同材质、同宽的拖尾）
            for (int i = Projectile.oldPos.Length - 1; i > 0; i--) {
                if (Projectile.oldPos[i] == Vector2.Zero) {
                    continue;
                }
                float ghost = (1f - i / (float)Projectile.oldPos.Length) * 0.4f * fade;
                Vector2 pos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Main.EntitySpriteDraw(tex, pos, null, lightColor * ghost, Projectile.oldRot[i], origin, squash, SpriteEffects.None, 0);
            }

            Vector2 drawPos = Projectile.Center + new Vector2(0f, sink) - Main.screenPosition;
            Main.EntitySpriteDraw(tex, drawPos, null, lightColor * fade, Projectile.rotation, origin, squash, SpriteEffects.None, 0);
            return false;
        }
    }
}
