using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDomains;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaServants.KikasaEye;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDestroyer.Rendering;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaServants.KikasaDestroyer
{
    /// <summary>
    /// 毁灭者鬼奴的口吐光柱：本体颚束（DestroyerMawBeamProj）的友方复刻
    /// 同一套 DestroyerBeam 着色器与加色装饰，展开→横扫→收束；
    /// ai[0]=起始角 ai[1]=角速度，逐帧锚定鬼奴头部，宿主没了快进收束。
    /// 额外保留血湖交互：光柱扫过湖面掀起行进浪线与飞血
    /// </summary>
    internal class KikasaDestroyerBloodJet : ModProjectile, IPrimitiveDrawable, IAdditiveDrawable
    {
        public override string Texture => CWRConstant.VaultPlaceholder2;

        internal const int ExpandFrames = 18;
        internal const int SweepFrames = 90;
        internal const int CollapseFrames = 16;
        internal const int TotalLife = ExpandFrames + SweepFrames + CollapseFrames;

        /// <summary>口器前伸量</summary>
        internal const float MuzzleOffset = 44f;
        private static float MaxBeamLength => 3600f;
        /// <summary>核宽随 0.7 缩放对齐本体观感（本体 126）</summary>
        private static float MaxWidth => 88f;

        private ref float Timer => ref Projectile.localAI[0];
        private ref float StartAngle => ref Projectile.ai[0];
        private ref float SweepSpeed => ref Projectile.ai[1];

        private float beamWidth;
        private float beamLength;
        private float prevCrossX = float.NaN;

        private static Color ThemeBlood => new(255, 50, 24);
        private static Color ThemeGlow => new(255, 150, 70);

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 3200;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 32;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 10;
            Projectile.timeLeft = TotalLife + 30;
        }

        /// <summary>宿主鬼奴：owner 场上唯一</summary>
        private KikasaDestroyerServant FindHost() {
            int servantType = ModContent.ProjectileType<KikasaDestroyerServant>();
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile p = Main.projectile[i];
                if (p?.active == true && p.owner == Projectile.owner && p.type == servantType
                    && p.ModProjectile is KikasaDestroyerServant servant) {
                    return servant;
                }
            }
            return null;
        }

        /// <summary>按 owner 找本人的光柱，鬼奴侧跟权威角用</summary>
        internal static Projectile FindFor(int owner) {
            int type = ModContent.ProjectileType<KikasaDestroyerBloodJet>();
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile p = Main.projectile[i];
                if (p?.active == true && p.owner == owner && p.type == type) {
                    return p;
                }
            }
            return null;
        }

        public override void AI() {
            KikasaDestroyerServant host = FindHost();

            //宿主没了/开始溶解：快进收束
            if ((host == null || host.IsDismissing) && Timer < TotalLife - CollapseFrames) {
                Timer = TotalLife - CollapseFrames;
            }

            if (Timer == 0 && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Zombie104 with { Volume = 0.9f, Pitch = -0.5f, MaxInstances = 3 }, Projectile.Center);
            }

            //展开定格→横扫→收束定格
            float sweepT = MathHelper.Clamp(Timer - ExpandFrames, 0f, SweepFrames);
            float beamAngle = StartAngle + SweepSpeed * sweepT;
            Projectile.rotation = beamAngle;

            if (host != null) {
                Projectile.Center = host.HeadPos + beamAngle.ToRotationVector2() * MuzzleOffset;
            }

            //宽长缓动（本体同款包络）
            float collapseStart = TotalLife - CollapseFrames;
            if (Timer < ExpandFrames) {
                float t = Timer / ExpandFrames;
                beamWidth = MathHelper.Lerp(4f, MaxWidth, VaultUtils.EaseOutCubic(t));
                beamLength = MathHelper.Lerp(0f, MaxBeamLength, VaultUtils.EaseOutQuad(t));
            }
            else if (Timer >= collapseStart) {
                float t = (Timer - collapseStart) / CollapseFrames;
                beamWidth = MathHelper.Lerp(MaxWidth, 0f, VaultUtils.EaseInQuad(t));
                beamLength = MaxBeamLength;
            }
            else {
                beamWidth = MaxWidth;
                beamLength = MaxBeamLength;
            }
            beamWidth *= 1f + 0.05f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 30f);

            Timer++;
            if (Timer >= TotalLife) {
                Projectile.Kill();
                return;
            }

            Vector2 beamDir = Projectile.rotation.ToRotationVector2();
            for (int i = 0; i < 7; i++) {
                Lighting.AddLight(Projectile.Center + beamDir * (beamLength / 7f * i), ThemeBlood.ToVector3() * 0.7f);
            }

            if (VaultUtils.isServer || beamWidth < MaxWidth * 0.3f) {
                return;
            }

            //低频震屏，同id刷新
            if ((int)Timer % 6 == 0) {
                DestroyerMotionFX.CameraPunch(Projectile.Center, 2.2f, 8, "KikasaMawBeamRumble", beamDir);
            }

            //沿束熔滴+余烬（本体同款）
            if (Main.rand.NextBool(2)) {
                float along = Main.rand.NextFloat();
                Vector2 sparkPos = Projectile.Center + beamDir * beamLength * along
                    + beamDir.RotatedBy(MathHelper.PiOver2) * Main.rand.NextFloat(-beamWidth * 0.45f, beamWidth * 0.45f);
                PRTLoader.NewParticle<PRT_Spark>(sparkPos,
                    beamDir.RotatedBy(Main.rand.NextFloat(-0.4f, 0.4f)) * Main.rand.NextFloat(3f, 9f),
                    Color.Lerp(ThemeGlow, Color.White, Main.rand.NextFloat()), Main.rand.NextFloat(0.9f, 1.5f))
                    ?.Configure(true, Main.rand.Next(14, 22));
            }
            if (Main.rand.NextBool(3)) {
                float along = Main.rand.NextFloat();
                Vector2 emberPos = Projectile.Center + beamDir * beamLength * along;
                PRTLoader.NewParticle<PRT_LavaFire>(emberPos,
                    new Vector2(Main.rand.NextFloat(-2f, 2f), Main.rand.NextFloat(0f, 2.5f)),
                    Color.White, Main.rand.NextFloat(0.7f, 1.2f))?.SetLifetime(20, 40);
            }

            //口器向心聚能
            if (Main.rand.NextBool(2)) {
                Vector2 gatherPos = Projectile.Center + Main.rand.NextVector2CircularEdge(70f, 70f);
                PRTLoader.NewParticle<PRT_Spark>(gatherPos,
                    (Projectile.Center - gatherPos) * 0.12f,
                    ThemeBlood, Main.rand.NextFloat(1f, 1.6f))?.Configure(false, 14);
            }

            UpdateLakeSweep(beamDir);
        }

        /// <summary>光柱扫过血湖：交点处掀起行进浪线与飞血（观看域门控）</summary>
        private void UpdateLakeSweep(Vector2 dir) {
            Player owner = Main.player[Projectile.owner];
            if (owner?.active != true
                || !owner.TryGetModPlayer(out KikasaDomainPlayer domain)
                || !domain.AnyActive || domain.RiseT <= 0.5f
                || KikasaDomain.Viewed != domain) {
                prevCrossX = float.NaN;
                return;
            }
            float lakeY = domain.LakeWorldY;

            float crossT = MathF.Abs(dir.Y) > 0.02f ? (lakeY - Projectile.Center.Y) / dir.Y : -1f;
            if (crossT < 40f || crossT > beamLength) {
                prevCrossX = float.NaN;
                return;
            }

            Vector2 cross = new(Projectile.Center.X + dir.X * crossT, lakeY);
            float sweep = float.IsNaN(prevCrossX) ? 0f : MathF.Abs(cross.X - prevCrossX);
            prevCrossX = cross.X;
            int t = (int)Timer;

            //行进浪线：大涟漪随扫速加码
            if (t % 2 == 0) {
                KikasaDomainDeco.RippleAt(cross, MathHelper.Clamp(1.1f + sweep * 0.05f, 1.1f, 2.0f));
            }
            //飞起的血水与蒸腾血雾：激光把湖面犁开
            for (int i = 0; i < 2; i++) {
                PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                    cross + new Vector2(Main.rand.NextFloat(-18f, 18f), -4f),
                    new Vector2(Main.rand.NextFloat(-2.4f, 2.4f) + dir.X * 1.5f,
                        -Main.rand.NextFloat(4.5f, 9.5f)),
                    Main.rand.NextBool(3) ? KikasaEyeBloodShot.BloodDeep : KikasaEyeBloodShot.BloodMain,
                    Main.rand.NextFloat(0.45f, 0.8f))?.Configure(Main.rand.Next(22, 38));
            }
            if (t % 6 == 3) {
                KikasaDomainDeco.SplashAt(cross, 6);
                PRTLoader.NewParticle<PRT_GhostRainMist>(cross + new Vector2(0f, -8f),
                    new Vector2(dir.X * 0.4f, -0.6f),
                    KikasaDomain.CoolTint(new(58, 18, 20), new(52, 62, 66)) * 0.85f,
                    Main.rand.NextFloat(0.7f, 1f))?.Configure(Main.rand.Next(40, 70));
                SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 0.45f, Pitch = 0.05f, MaxInstances = 3 }, cross);
            }
            if (t % 14 == 7) {
                PRTLoader.NewParticle<PRT_DWave>(cross, Vector2.Zero,
                    KikasaEyeBloodShot.BloodDeep, 0.08f)
                    ?.Configure(new Vector2(0.5f, 1f), -MathHelper.PiOver2, 0.3f, 10);
            }
        }

        //展开完才可伤
        public override bool? CanDamage() => Timer > ExpandFrames ? null : false;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            float p = 0f;
            //碰撞比视觉窄
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(),
                Projectile.Center, Projectile.Center + Projectile.rotation.ToRotationVector2() * beamLength,
                beamWidth * 0.6f, ref p);
        }

        public override bool PreDraw(ref Color lightColor) => false;

        /// <summary>近端 bleed，藏硬切边进头雕</summary>
        private float MuzzleBackBleed => beamWidth * 0.38f + 42f;

        void IPrimitiveDrawable.DrawPrimitives() {
            if (beamWidth <= 1f || beamLength <= 10f) {
                return;
            }
            float opacity = MathHelper.Clamp(beamWidth / MaxWidth, 0f, 1f);
            Effect effect = EffectLoader.DestroyerBeam?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (effect != null && noise != null) {
                DrawShaderBeam(effect, noise, opacity);
            }
        }

        void IAdditiveDrawable.DrawAdditiveAfterNon(SpriteBatch spriteBatch) {
            DrawAdditiveDressing(MathHelper.Clamp(beamWidth / MaxWidth, 0f, 1f));
        }

        /// <summary>DestroyerBeam.fx 主轴+电弧+脉冲（本体同款）</summary>
        private void DrawShaderBeam(Effect effect, Texture2D noise, float opacity) {
            Vector2 mouth = Projectile.Center;
            Vector2 dir = Projectile.rotation.ToRotationVector2();
            Vector2 perp = dir.RotatedBy(MathHelper.PiOver2);
            Vector2 tip = mouth + dir * beamLength;
            float backBleed = MuzzleBackBleed;
            Vector2 origin = mouth - dir * backBleed;
            //视觉半宽含电弧/halo 余量
            float halfW = beamWidth * 3.0f;

            //uv.x 1口器→0末端；uv.y 横截面
            VertexPositionColorTexture[] verts = new VertexPositionColorTexture[4];
            verts[0] = new VertexPositionColorTexture((origin + perp * halfW).ToVector3(), Color.White, new Vector2(1f, 0f));
            verts[1] = new VertexPositionColorTexture((origin - perp * halfW).ToVector3(), Color.White, new Vector2(1f, 1f));
            verts[2] = new VertexPositionColorTexture((tip + perp * halfW).ToVector3(), Color.White, new Vector2(0f, 0f));
            verts[3] = new VertexPositionColorTexture((tip - perp * halfW).ToVector3(), Color.White, new Vector2(0f, 1f));

            GraphicsDevice device = Main.graphics.GraphicsDevice;
            BlendState origBlend = device.BlendState;
            RasterizerState origRaster = device.RasterizerState;
            device.BlendState = BlendState.Additive;
            device.RasterizerState = RasterizerState.CullNone;

            effect.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            effect.Parameters["fadeAlpha"]?.SetValue(opacity);
            effect.Parameters["exMode"]?.SetValue(0f);
            effect.Parameters["seed"]?.SetValue(Projectile.whoAmI * 0.137f % 1f);
            effect.Parameters["uNoiseTex"]?.SetValue(noise);
            foreach (EffectPass pass in effect.CurrentTechnique.Passes) {
                pass.Apply();
                device.DrawUserPrimitives(PrimitiveType.TriangleStrip, verts, 0, 2);
            }

            device.BlendState = origBlend;
            device.RasterizerState = origRaster;
        }

        /// <summary>口器光球/星闪/头桥接，圆点无硬切（本体同款）</summary>
        private void DrawAdditiveDressing(float opacity) {
            Texture2D glow = CWRAsset.DiffusionCircle.Value;
            Texture2D star = CWRAsset.StarTexture.Value;
            Vector2 dir = Projectile.rotation.ToRotationVector2();
            float flicker = 1f + 0.1f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 40f);

            Color blood = ThemeBlood;
            Color amber = ThemeGlow;
            Color core = Color.White;

            //口器→末端推进光球
            Vector2 screenMouth = Projectile.Center - Main.screenPosition;
            const int pulses = 4;
            for (int i = 0; i < pulses; i++) {
                float along = (Main.GlobalTimeWrappedHourly * 0.9f + i / (float)pulses) % 1f;
                Vector2 pPos = screenMouth + dir * beamLength * along;
                float pScale = beamWidth / MaxWidth * (0.5f + 0.5f * (float)Math.Sin(along * MathHelper.Pi));
                Main.EntitySpriteDraw(glow, pPos, null, amber * (0.7f * opacity), 0f, glow.Size() / 2f,
                    pScale * 1.1f * 0.3f, SpriteEffects.None, 0);
            }

            //口器呼吸球+星闪
            float muzzleScale = beamWidth / MaxWidth;
            Main.EntitySpriteDraw(glow, screenMouth, null, blood * (0.95f * opacity), 0f, glow.Size() / 2f,
                muzzleScale * 1.8f * flicker, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(glow, screenMouth, null, amber * opacity, 0f, glow.Size() / 2f,
                muzzleScale * 1.1f, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(glow, screenMouth, null, core * opacity, 0f, glow.Size() / 2f,
                muzzleScale * 0.65f, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(star, screenMouth, null, amber * (0.9f * opacity), Main.GlobalTimeWrappedHourly * 3.2f,
                star.Size() / 2f, muzzleScale * 0.6f * flicker, SpriteEffects.None, 0);

            //头心桥接，吃近端硬边
            KikasaDestroyerServant host = FindHost();
            if (host != null) {
                Vector2 headPos = host.HeadPos - Main.screenPosition;
                float bridge = muzzleScale * 1.6f;
                Main.EntitySpriteDraw(glow, headPos, null, blood * (0.55f * opacity), 0f, glow.Size() / 2f,
                    bridge, SpriteEffects.None, 0);
                Main.EntitySpriteDraw(glow, headPos, null, core * (0.35f * opacity), 0f, glow.Size() / 2f,
                    bridge * 0.45f, SpriteEffects.None, 0);
            }
        }
    }
}
