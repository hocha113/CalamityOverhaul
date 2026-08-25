using CalamityOverhaul.Common;
using CalamityOverhaul.Content.DamageModify;
using InnoVault.PRT;
using InnoVault.Trails;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee.CursedflameBloodfists
{
    /// <summary>
    /// 飞行火焰拳。拳锋在前、燃烧的断口在后，绿焰拖尾从断口一路拉出去
    /// </summary>
    internal class CursedflameFistProj : ModProjectile, IPrimitiveDrawable
    {
        public override string Texture => CursedflameFX.FistTexture;
        public override LocalizedText DisplayName => VaultUtils.GetLocalizedItemName<CursedflameBloodfist>();

        private const int LifeSpan = 36;
        /// <summary>出手后这么多帧才开始吃地形，贴着掩体灌拳不至于全废</summary>
        private const int TileGrace = 6;

        private Trail trail;

        private int Age => LifeSpan - Projectile.timeLeft;
        /// <summary>拖尾摆动相位，用 identity 起种，各端一致</summary>
        private float WobbleSeed => Projectile.identity * 0.618f;
        private float Fade => MathHelper.Clamp(Projectile.timeLeft / 9f, 0f, 1f);

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailingMode[Type] = 2;
            //拖尾长度就是这条缓存的长度，短拖尾靠它，不靠在着色器里硬淡出
            ProjectileID.Sets.TrailCacheLength[Type] = 7;
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 26;
            Projectile.aiStyle = -1;
            Projectile.friendly = true;
            Projectile.DamageType = MeleeMagicDamageClass.Instance;
            Projectile.penetrate = 2;
            Projectile.timeLeft = LifeSpan;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 12;
            Projectile.scale = 0.95f;
        }

        public override void AI() {
            int age = Age;
            if (age == TileGrace) {
                Projectile.tileCollide = true;
            }

            //出手前段还在加速，中段巡航，末段泄力，全程不许匀速直线
            if (age < 5) {
                Projectile.velocity *= 1.05f;
            }
            else if (age > 19) {
                Projectile.velocity *= 0.955f;
            }

            //侧向微摆，读成一记带腕力的直拳而不是一条激光。左右手打出来的摆向相反
            float side = Projectile.ai[0] < 0f ? -1f : 1f;
            float swing = MathF.Sin((age * 0.62f) + WobbleSeed) * (1f - (age / (float)LifeSpan)) * 1.05f;
            Projectile.position += Projectile.velocity.SafeNormalize(Vector2.UnitX)
                .RotatedBy(MathHelper.PiOver2) * (swing * side);

            //出手一瞬间胀一下再落回，末段随淡出一起缩
            Projectile.scale = age < 3
                ? MathHelper.Lerp(0.62f, 1.12f, age / 3f)
                : MathHelper.Lerp(0.95f, 0.62f, 1f - Fade);
            Projectile.rotation = Projectile.velocity.ToRotation() + CursedflameFX.FistRotationOffset;
            Projectile.alpha = (int)((1f - Fade) * 210f);

            Lighting.AddLight(Projectile.Center, CursedflameFX.FlameGreen.ToVector3() * 0.6f * Fade);
            ShedFlame(age);
        }

        /// <summary>断口一路掉火舌与余烬，拖尾不能只靠一条带子撑</summary>
        private void ShedFlame(int age) {
            if (VaultUtils.isServer) {
                return;
            }
            Vector2 stump = Projectile.Center - (Projectile.velocity.SafeNormalize(Vector2.UnitX) * 10f * Projectile.scale);
            Vector2 back = -Projectile.velocity.SafeNormalize(Vector2.UnitX);

            Vector2 lick = back.RotatedByRandom(0.45);
            PRTLoader.NewParticle<PRT_CursedTongue>(stump, (lick * Main.rand.NextFloat(0.8f, 2.2f)) + (Projectile.velocity * 0.14f)
                , CursedflameFX.FlameGreen, Main.rand.NextFloat(0.3f, 0.5f) * Fade)
                .Configure(lick, Main.rand.NextFloat(0.75f, 1.3f), Main.rand.Next(4, 8));

            if (age % 2 == 0) {
                PRTLoader.NewParticle<PRT_CursedEmber>(stump + Main.rand.NextVector2Circular(5f, 5f)
                    , (back * Main.rand.NextFloat(1.2f, 3.4f)).RotatedByRandom(0.6)
                    , CursedflameFX.FlameGreen, Main.rand.NextFloat(0.6f, 1f) * Fade)
                    .Configure(Main.rand.Next(14, 24));
            }
        }

        public override bool OnTileCollide(Vector2 oldVelocity) => true;

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.CursedInferno, 240);
            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.DD2_BetsyFireballImpact with {
                Pitch = 0.55f,
                Volume = 0.3f,
                MaxInstances = 4,
                SoundLimitBehavior = SoundLimitBehavior.ReplaceOldest
            }, target.Center);
            Burst(target.Center, 5);
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            //残火比弹幕活得久，一串拳打过去地上还烧着
            Burst(Projectile.Center, 7);
        }

        private void Burst(Vector2 at, int count) {
            for (int i = 0; i < count; i++) {
                Vector2 dir = Main.rand.NextVector2Unit();
                PRTLoader.NewParticle<PRT_CursedTongue>(at + (dir * Main.rand.NextFloat(2f, 9f))
                    , dir * Main.rand.NextFloat(1.2f, 3.4f)
                    , CursedflameFX.FlameGreen, Main.rand.NextFloat(0.34f, 0.6f))
                    .Configure(dir, Main.rand.NextFloat(0.8f, 1.4f), Main.rand.Next(5, 10));
                PRTLoader.NewParticle<PRT_CursedEmber>(at + Main.rand.NextVector2Circular(8f, 8f)
                    , dir * Main.rand.NextFloat(1.6f, 5f)
                    , CursedflameFX.FlameGreen, Main.rand.NextFloat(0.7f, 1.25f))
                    .Configure(Main.rand.Next(16, 30), 0.05f);
            }
        }

        /// <summary>拖尾整体透明度，压得很低才不会盖住拳头本体</summary>
        private const float TrailAlpha = 0.3f;

        public float GetWidthFunc(float completionRatio)
            => Projectile.scale * 26f * Projectile.Opacity * (1f - completionRatio);

        public Color GetColorFunc(Vector2 completionRatio) {
            //拖尾只走绿，从亮绿收到深绿，橙色留给火舌与余烬
            Color color = Color.Lerp(CursedflameFX.FlameGreen, CursedflameFX.TrailDeep
                , MathHelper.Clamp(completionRatio.X * 1.2f, 0f, 1f));
            return color * (Projectile.Opacity * TrailAlpha * (1f - (completionRatio.X * 0.5f)));
        }

        void IPrimitiveDrawable.DrawPrimitives() {
            if (Projectile.oldPos == null || Projectile.oldPos.Length == 0
                || CursedflameFX.Gradient == null || CursedflameFX.Voronoi == null) {
                return;
            }

            Vector2[] positions = new Vector2[Projectile.oldPos.Length];
            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero) {
                    Projectile.oldPos[i] = Projectile.Center;
                }
                positions[i] = Projectile.oldPos[i] + (Projectile.Size * 0.5f);
            }

            trail ??= new Trail(positions, GetWidthFunc, GetColorFunc);
            trail.TrailPositions = positions;

            Effect effect = EffectLoader.GradientTrail.Value;
            effect.Parameters["transformMatrix"].SetValue(VaultUtils.GetTransfromMatrix());
            effect.Parameters["uTime"].SetValue((float)Main.timeForVisualEffects * 0.1f);
            effect.Parameters["uTimeG"].SetValue(Main.GlobalTimeWrappedHourly * 0.25f);
            effect.Parameters["udissolveS"].SetValue(1f);
            effect.Parameters["uBaseImage"].SetValue(CursedflameFX.Voronoi);
            effect.Parameters["uFlow"].SetValue(CursedflameFX.SoftGlow);
            effect.Parameters["uGradient"].SetValue(CursedflameFX.Gradient);
            effect.Parameters["uDissolve"].SetValue(CursedflameFX.SoftGlow);

            Main.graphics.GraphicsDevice.BlendState = BlendState.Additive;
            trail.DrawTrail(effect);
            Main.graphics.GraphicsDevice.BlendState = BlendState.AlphaBlend;
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            if (tex == null) {
                return false;
            }
            Vector2 origin = tex.Size() * 0.5f;
            Color light = Lighting.GetColor(Projectile.Center.ToTileCoordinates());
            light.A = 255;

            //速度方向拉丝的残影，越旧越短越淡；同样只走绿系
            for (int i = Projectile.oldPos.Length - 1; i >= 1; i--) {
                if (Projectile.oldPos[i] == Vector2.Zero) {
                    continue;
                }
                float f = 1f - (i / (float)Projectile.oldPos.Length);
                Color ghost = Color.Lerp(CursedflameFX.FlameMoss, CursedflameFX.TrailDeep, i / (float)Projectile.oldPos.Length);
                ghost.A = 0;
                Vector2 pos = Projectile.oldPos[i] + (Projectile.Size * 0.5f) - Main.screenPosition;
                Main.EntitySpriteDraw(tex, pos, null, ghost * (f * f * 0.2f * Projectile.Opacity)
                    , Projectile.rotation, origin, Projectile.scale * (0.55f + (0.45f * f)), SpriteEffects.None, 0);
            }

            //断口的绿焰底光，只做垫层不当主体
            Texture2D glow = CursedflameFX.SoftGlow;
            if (glow != null) {
                Vector2 stump = Projectile.Center - (Projectile.velocity.SafeNormalize(Vector2.UnitX) * 10f * Projectile.scale);
                Color halo = CursedflameFX.FlameGreen with { A = 0 };
                Main.EntitySpriteDraw(glow, stump - Main.screenPosition, null, halo * (0.5f * Projectile.Opacity)
                    , 0f, glow.Size() * 0.5f, 0.34f, SpriteEffects.None, 0);
            }

            Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, null, light * Projectile.Opacity
                , Projectile.rotation, origin, Projectile.scale, SpriteEffects.None, 0);
            return false;
        }
    }
}
