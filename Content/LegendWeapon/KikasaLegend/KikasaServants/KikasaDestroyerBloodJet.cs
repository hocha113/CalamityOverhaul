using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDomains;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaServants
{
    /// <summary>鬼奴模块的着色器域内加载器</summary>
    internal class KikasaServantAssets
    {
        /// <summary>血液喷柱条带</summary>
        [VaultLoaden(CWRConstant.Effects)]
        public static Effect KikasaBloodJet { get; private set; }
    }

    /// <summary>
    /// 毁灭者鬼奴的血液喷柱：一根粗壮的加压血浆柱，不是激光——
    /// 暗色液柱、下缘被重力撕裂、远端颈缩断成滴串、沿途血雨坠落。
    /// 逐帧锚定鬼奴头部（角度由鬼奴的慢跟瞄准锁供给），
    /// 展开→持续→自根部泄压收束；线碰撞比视觉窄、展开期无伤
    /// </summary>
    internal class KikasaDestroyerBloodJet : ModProjectile, IPrimitiveDrawable, IAdditiveDrawable
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        internal const int ExpandFrames = 10;
        internal const int SustainFrames = KikasaDestroyerServant.JetSustainFrames;
        internal const int CollapseFrames = 14;
        internal const int TotalLife = ExpandFrames + SustainFrames + CollapseFrames;

        private const float BeamLength = 1150f;
        private const float MaxWidth = 54f;
        private const float MuzzleOffset = 30f;

        private ref float Timer => ref Projectile.localAI[0];

        private float beamWidth;
        private float drain;

        public override void SetStaticDefaults()
            => ProjectileID.Sets.DrawScreenCheckFluff[Projectile.type] = 1600;

        public override void SetDefaults() {
            Projectile.width = 32;
            Projectile.height = 32;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 10;
            Projectile.timeLeft = TotalLife + 20;
        }

        /// <summary>宿主鬼奴：owner 场上唯一</summary>
        private KikasaDestroyerServant FindHost() {
            int type = ModContent.ProjectileType<KikasaDestroyerServant>();
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile p = Main.projectile[i];
                if (p?.active == true && p.owner == Projectile.owner && p.type == type
                    && p.ModProjectile is KikasaDestroyerServant servant) {
                    return servant;
                }
            }
            return null;
        }

        public override void AI() {
            KikasaDestroyerServant host = FindHost();

            //宿主没了/开始溶解：快进泄压
            if ((host == null || host.IsDismissing) && Timer < ExpandFrames + SustainFrames) {
                Timer = ExpandFrames + SustainFrames;
            }

            if (host != null) {
                Projectile.rotation = host.HeadRot;
                Projectile.Center = host.HeadPos + Projectile.rotation.ToRotationVector2() * MuzzleOffset;
            }

            //宽度与泄压包络
            int collapseStart = ExpandFrames + SustainFrames;
            if (Timer < ExpandFrames) {
                float t = Timer / ExpandFrames;
                beamWidth = MathHelper.Lerp(5f, MaxWidth, 1f - MathF.Pow(1f - t, 3f));
                drain = 0f;
            }
            else if (Timer >= collapseStart) {
                float t = (Timer - collapseStart) / CollapseFrames;
                beamWidth = MaxWidth;
                drain = MathHelper.Clamp(t, 0f, 1f);
            }
            else {
                beamWidth = MaxWidth * (1f + 0.04f * MathF.Sin(Main.GlobalTimeWrappedHourly * 26f));
                drain = 0f;
            }

            Timer++;
            if (Timer >= TotalLife) {
                Projectile.Kill();
                return;
            }

            Vector2 dir = Projectile.rotation.ToRotationVector2();
            for (int i = 0; i < 6; i++) {
                Lighting.AddLight(Projectile.Center + dir * (BeamLength / 6f * i), 0.34f, 0.07f, 0.06f);
            }

            if (Main.dedServ || beamWidth < MaxWidth * 0.3f) {
                return;
            }

            //根口喷溅：出口涡流甩珠
            if (drain < 0.4f && (int)Timer % 2 == 0) {
                Vector2 perp = dir.RotatedBy(MathHelper.PiOver2);
                PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                    Projectile.Center + dir * Main.rand.NextFloat(8f, 30f)
                        + perp * Main.rand.NextFloat(-beamWidth, beamWidth) * 0.4f,
                    dir.RotatedBy(Main.rand.NextFloat(-0.5f, 0.5f)) * Main.rand.NextFloat(2f, 6f),
                    Main.rand.NextBool(3) ? KikasaEyeBloodShot.BloodDeep : KikasaEyeBloodShot.BloodMain,
                    Main.rand.NextFloat(0.4f, 0.7f))?.Configure(Main.rand.Next(12, 22));
            }

            //远端三分之一失压血雨：柱身撕出的血坠回世界
            if ((int)Timer % 2 == 1) {
                float frac = Main.rand.NextFloat(0.62f, 0.98f);
                Vector2 pos = Projectile.Center + dir * BeamLength * frac
                    + dir.RotatedBy(MathHelper.PiOver2) * Main.rand.NextFloat(-beamWidth, beamWidth) * 0.5f;
                PRTLoader.NewParticle<PRT_KikasaBloodGlob>(pos,
                    dir * Main.rand.NextFloat(2f, 7f) + new Vector2(0f, Main.rand.NextFloat(0.5f, 2f)),
                    KikasaEyeBloodShot.BloodMain * 0.7f,
                    Main.rand.NextFloat(0.45f, 0.8f))?.Configure(Main.rand.Next(26, 44));
            }

            //血雨落湖的涟漪余韵（观看域门控）
            Player owner = Main.player[Projectile.owner];
            if ((int)Timer % 6 == 3 && owner?.active == true
                && owner.TryGetModPlayer(out KikasaDomainPlayer domain)
                && domain.AnyActive && domain.RiseT > 0.5f
                && KikasaDomain.Viewed == domain) {
                float frac = Main.rand.NextFloat(0.5f, 1f);
                Vector2 pos = Projectile.Center + dir * BeamLength * frac;
                //柱身在湖面上方不远处才有血雨落湖
                if (pos.Y > domain.LakeWorldY - 320f && pos.Y < domain.LakeWorldY + 40f) {
                    KikasaDomainDeco.RippleAt(new Vector2(pos.X + Main.rand.NextFloat(-30f, 30f),
                        domain.LakeWorldY), Main.rand.NextFloat(0.3f, 0.55f));
                }
            }

            //泄压收束的湿咳一声
            if ((int)Timer == collapseStart + 2) {
                SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 0.6f, Pitch = -0.6f, MaxInstances = 2 }, Projectile.Center);
                SoundEngine.PlaySound(SoundID.Drip with { Volume = 0.5f, Pitch = -0.4f, MaxInstances = 2 }, Projectile.Center);
            }
        }

        /// <summary>展开完成才可伤；泄压过半后血压不足不再切人</summary>
        public override bool? CanDamage()
            => Timer > ExpandFrames && drain < 0.5f ? null : false;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            float _ = 0f;
            //碰撞比视觉窄；泄压期从根部让出已断的一段
            Vector2 dir = Projectile.rotation.ToRotationVector2();
            Vector2 start = Projectile.Center + dir * (BeamLength * drain);
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(),
                start, Projectile.Center + dir * BeamLength, beamWidth * 0.6f, ref _);
        }

        public override bool PreDraw(ref Color lightColor) => false;

        void IPrimitiveDrawable.DrawPrimitives() {
            if (Main.dedServ || beamWidth <= 2f) {
                return;
            }
            Effect fx = KikasaServantAssets.KikasaBloodJet;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (fx == null || noise == null) {
                return;
            }

            Vector2 dir = Projectile.rotation.ToRotationVector2();
            Vector2 perp = dir.RotatedBy(MathHelper.PiOver2);
            //近端 bleed 藏进头雕
            Vector2 origin = Projectile.Center - dir * (beamWidth * 0.35f + 26f);
            Vector2 tip = Projectile.Center + dir * BeamLength;
            //视觉半宽给摆动与撕裂留余量
            float halfW = beamWidth * 1.6f;

            //uv.x 1=根→0=末端；uv.y 跨截面
            VertexPositionColorTexture[] verts = new VertexPositionColorTexture[4];
            verts[0] = new VertexPositionColorTexture((origin + perp * halfW).ToVector3(), Color.White, new Vector2(1f, 0f));
            verts[1] = new VertexPositionColorTexture((origin - perp * halfW).ToVector3(), Color.White, new Vector2(1f, 1f));
            verts[2] = new VertexPositionColorTexture((tip + perp * halfW).ToVector3(), Color.White, new Vector2(0f, 0f));
            verts[3] = new VertexPositionColorTexture((tip - perp * halfW).ToVector3(), Color.White, new Vector2(0f, 1f));

            GraphicsDevice device = Main.graphics.GraphicsDevice;
            BlendState origBlend = device.BlendState;
            RasterizerState origRaster = device.RasterizerState;
            //浓血：预乘 AlphaBlend，暗缘真正压暗背景——绝不用 Additive
            device.BlendState = BlendState.AlphaBlend;
            device.RasterizerState = RasterizerState.CullNone;

            float fade = MathHelper.Clamp(beamWidth / MaxWidth, 0f, 1f)
                * MathHelper.Clamp(1.2f - drain * 0.55f, 0f, 1f);
            //uv.y=0 是 +perp 侧；世界向下撕裂侧换算符号
            float gravSide = perp.Y < 0f ? 1f : -1f;

            fx.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            fx.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            fx.Parameters["uSeed"]?.SetValue(Projectile.identity * 0.7391f % 3.71f);
            fx.Parameters["uFade"]?.SetValue(fade);
            fx.Parameters["uDrain"]?.SetValue(drain);
            fx.Parameters["uGravSide"]?.SetValue(gravSide);
            fx.Parameters["uNoiseTex"]?.SetValue(noise);
            fx.Parameters["uColDark"]?.SetValue(KikasaEyeBloodShot.BloodDark.ToVector3());
            fx.Parameters["uColDeep"]?.SetValue(KikasaEyeBloodShot.BloodDeep.ToVector3());
            fx.Parameters["uColMain"]?.SetValue(KikasaEyeBloodShot.BloodMain.ToVector3());
            fx.Parameters["uColBright"]?.SetValue(KikasaEyeBloodShot.BloodBright.ToVector3());

            foreach (EffectPass pass in fx.CurrentTechnique.Passes) {
                pass.Apply();
                device.DrawUserPrimitives(PrimitiveType.TriangleStrip, verts, 0, 2);
            }

            device.BlendState = origBlend;
            device.RasterizerState = origRaster;
        }

        void IAdditiveDrawable.DrawAdditiveAfterNon(SpriteBatch spriteBatch) {
            //只补一点根口湿光，浓血不做大光晕
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow == null || beamWidth <= 2f || drain > 0.6f) {
                return;
            }
            float opacity = MathHelper.Clamp(beamWidth / MaxWidth, 0f, 1f) * (1f - drain);
            Vector2 mouth = Projectile.Center - Main.screenPosition;
            float flicker = 1f + 0.08f * MathF.Sin(Main.GlobalTimeWrappedHourly * 34f);
            spriteBatch.Draw(glow, mouth, null, KikasaEyeBloodShot.BloodMain * (0.5f * opacity), 0f,
                glow.Size() * 0.5f, beamWidth / MaxWidth * 1.4f * flicker, SpriteEffects.None, 0f);
            spriteBatch.Draw(glow, mouth, null, KikasaEyeBloodShot.BloodBright * (0.3f * opacity), 0f,
                glow.Size() * 0.5f, beamWidth / MaxWidth * 0.7f, SpriteEffects.None, 0f);
        }
    }
}
