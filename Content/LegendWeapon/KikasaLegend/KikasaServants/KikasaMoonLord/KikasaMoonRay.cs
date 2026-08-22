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

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaServants.KikasaMoonLord
{
    /// <summary>
    /// 幻月血芒：噬月心藏自竖缝瞳芯轰出的贯屏毁灭射线，全场唯一的粗扫荡光束。
    /// ai[0]=起始角 ai[1]=角速度，逐帧锚定心脏、确定性慢弧扫荡；
    /// 宿主没了/开始溶解就快进收束。专属着色器三层束体
    /// （白炽核→血色体→暗色吸光外缘），扫过湖面处水被烧沸：
    /// 沿途蒸汽白雾柱腾起、水位线行波扭曲，收束后整条扫带留一条
    /// 久久不散的余韵蒸汽带
    /// </summary>
    internal class KikasaMoonRay : ModProjectile, IPrimitiveDrawable, IAdditiveDrawable
    {
        public override string Texture => CWRConstant.VaultPlaceholder2;

        internal const int ExpandFrames = 16;
        internal const int SweepFrames = 150;
        internal const int CollapseFrames = 26;
        internal const int TotalLife = ExpandFrames + SweepFrames + CollapseFrames;

        /// <summary>扫荡半弧（弧度）：慢而宽，压过毁灭者中束横扫一头</summary>
        internal const float ArcHalf = 0.62f;

        /// <summary>缝口前伸量</summary>
        internal const float MuzzleOffset = 86f;
        private static float MaxBeamLength => 4200f;
        /// <summary>核宽：毁灭者 88 → 幻月 150，全场最粗</summary>
        private static float MaxWidth => 150f;

        private ref float Timer => ref Projectile.localAI[0];
        private ref float StartAngle => ref Projectile.ai[0];
        private ref float SweepSpeed => ref Projectile.ai[1];

        private float beamWidth;
        private float beamLength;
        private float prevCrossX = float.NaN;
        //本端记录的扫带范围，收束时铺余韵蒸汽带
        private float sweptMinX = float.NaN;
        private float sweptMaxX = float.NaN;
        private bool steamBandDone;

        private static Color ThemeBlood => KikasaDomain.CoolTint(new(220, 40, 26), new(120, 150, 156));
        private static Color ThemeCore => new(255, 244, 230);
        /// <summary>蒸汽白：近白微血，烧滚的血水汽</summary>
        private static Color SteamPale => KikasaDomain.CoolTint(new(236, 224, 220), new(206, 216, 218));

        private float Seed => Projectile.identity * 0.7391f % 1f;

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 4600;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 40;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 12;
            Projectile.timeLeft = TotalLife + 30;
        }

        /// <summary>宿主心脏：owner 场上唯一</summary>
        private KikasaMoonLordServant FindHost() {
            int servantType = ModContent.ProjectileType<KikasaMoonLordServant>();
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile p = Main.projectile[i];
                if (p?.active == true && p.owner == Projectile.owner && p.type == servantType
                    && p.ModProjectile is KikasaMoonLordServant servant) {
                    return servant;
                }
            }
            return null;
        }

        /// <summary>按 owner 找本人的幻月射线，心脏侧跟权威角用</summary>
        internal static Projectile FindFor(int owner) {
            int type = ModContent.ProjectileType<KikasaMoonRay>();
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile p = Main.projectile[i];
                if (p?.active == true && p.owner == owner && p.type == type) {
                    return p;
                }
            }
            return null;
        }

        public override void AI() {
            KikasaMoonLordServant host = FindHost();

            //宿主没了/开始溶解：快进收束
            if ((host == null || host.IsDismissing) && Timer < TotalLife - CollapseFrames) {
                Timer = TotalLife - CollapseFrames;
            }

            //展开定格→慢弧扫荡→收束定格
            float sweepT = MathHelper.Clamp(Timer - ExpandFrames, 0f, SweepFrames);
            float beamAngle = StartAngle + SweepSpeed * sweepT;
            Projectile.rotation = beamAngle;

            if (host != null) {
                Projectile.Center = host.HeartPos + beamAngle.ToRotationVector2() * MuzzleOffset;
            }

            float collapseStart = TotalLife - CollapseFrames;
            if (Timer < ExpandFrames) {
                float t = Timer / ExpandFrames;
                beamWidth = MathHelper.Lerp(6f, MaxWidth, VaultUtils.EaseOutCubic(t));
                beamLength = MathHelper.Lerp(0f, MaxBeamLength, VaultUtils.EaseOutQuad(t));
            }
            else if (Timer >= collapseStart) {
                float t = (Timer - collapseStart) / CollapseFrames;
                beamWidth = MathHelper.Lerp(MaxWidth, 0f, VaultUtils.EaseInQuad(t));
                beamLength = MaxBeamLength;
                //收束一开始就把余韵蒸汽带铺下去
                if (!steamBandDone) {
                    steamBandDone = true;
                    LayResidualSteam();
                }
            }
            else {
                beamWidth = MaxWidth;
                beamLength = MaxBeamLength;
            }
            //束宽随残余心律微搏
            beamWidth *= 1f + 0.04f * MathF.Sin(Main.GlobalTimeWrappedHourly * 22f + Seed * 9f);

            Timer++;
            if (Timer >= TotalLife) {
                Projectile.Kill();
                return;
            }

            Vector2 beamDir = Projectile.rotation.ToRotationVector2();
            for (int i = 0; i < 8; i++) {
                Lighting.AddLight(Projectile.Center + beamDir * (beamLength / 8f * i),
                    ThemeBlood.ToVector3() * 0.85f);
            }

            if (VaultUtils.isServer || beamWidth < MaxWidth * 0.25f) {
                return;
            }

            //低频震屏：同 id 刷新、带距离衰减
            if ((int)Timer % 6 == 0) {
                DestroyerMotionFX.CameraPunch(Projectile.Center, 2.6f, 8, "KikasaMoonRayRumble", beamDir);
            }

            //沿束熔滴：血珠被束压出来甩向两侧
            if (Main.rand.NextBool(2)) {
                float along = Main.rand.NextFloat();
                Vector2 perp = beamDir.RotatedBy(MathHelper.PiOver2);
                Vector2 pos = Projectile.Center + beamDir * beamLength * along
                    + perp * Main.rand.NextFloat(-beamWidth * 0.4f, beamWidth * 0.4f);
                PRTLoader.NewParticle<PRT_KikasaBloodGlob>(pos,
                    perp * Main.rand.NextFloat(-3.5f, 3.5f) + beamDir * Main.rand.NextFloat(1f, 4f),
                    Main.rand.NextBool(3) ? KikasaEyeBloodShot.BloodDeep : KikasaEyeBloodShot.BloodMain,
                    Main.rand.NextFloat(0.4f, 0.7f))?.Configure(Main.rand.Next(14, 24), 0.3f);
            }
            //缝口向心汇聚
            if (Main.rand.NextBool(2)) {
                Vector2 gatherPos = Projectile.Center + Main.rand.NextVector2CircularEdge(80f, 80f);
                PRTLoader.NewParticle<PRT_Spark>(gatherPos,
                    (Projectile.Center - gatherPos) * 0.12f,
                    ThemeBlood, Main.rand.NextFloat(1f, 1.6f))?.Configure(false, 14);
            }

            UpdateLakeBoil(beamDir);
        }

        /// <summary>射线扫过血湖：交点处水被烧沸，蒸汽柱、行波扭曲、飞血与嘶鸣</summary>
        private void UpdateLakeBoil(Vector2 dir) {
            Player owner = Main.player[Projectile.owner];
            if (owner?.active != true
                || !owner.TryGetModPlayer(out KikasaDomainPlayer domain)
                || !domain.AnyActive || domain.RiseT <= 0.5f) {
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
            //记录扫带范围，收束时铺蒸汽带
            sweptMinX = float.IsNaN(sweptMinX) ? cross.X : MathF.Min(sweptMinX, cross.X);
            sweptMaxX = float.IsNaN(sweptMaxX) ? cross.X : MathF.Max(sweptMaxX, cross.X);

            //嘶鸣：水碰上白炽的声音
            if ((int)Timer % 9 == 4) {
                SoundEngine.PlaySound(SoundID.LiquidsWaterLava with { Volume = 0.65f, Pitch = 0.1f, MaxInstances = 3 }, cross);
            }

            if (KikasaDomain.Viewed != domain) {
                return;
            }
            int t = (int)Timer;

            //水位线扭曲：大涟漪行波逐帧跟着交点走
            if (t % 2 == 0) {
                KikasaDomainDeco.RippleAt(cross, MathHelper.Clamp(1.3f + sweep * 0.05f, 1.3f, 2.4f));
            }
            //蒸汽白雾柱：贴水快速腾起（每帧 1 团短命雾，雾池 120 上限是全场共用的）
            PRTLoader.NewParticle<PRT_GhostRainMist>(
                cross + new Vector2(Main.rand.NextFloat(-20f, 20f), -Main.rand.NextFloat(2f, 14f)),
                new Vector2(dir.X * 0.5f + Main.rand.NextFloat(-0.3f, 0.3f), -Main.rand.NextFloat(1.8f, 3.4f)),
                SteamPale * Main.rand.NextFloat(0.5f, 0.75f),
                Main.rand.NextFloat(0.65f, 1.05f))?.Configure(Main.rand.Next(30, 52));
            //沸水飞溅：烧开的血水向上炸
            for (int i = 0; i < 2; i++) {
                PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                    cross + new Vector2(Main.rand.NextFloat(-16f, 16f), -4f),
                    new Vector2(Main.rand.NextFloat(-2.6f, 2.6f) + dir.X * 1.8f,
                        -Main.rand.NextFloat(5f, 11f)),
                    Main.rand.NextBool(3) ? KikasaEyeBloodShot.BloodDeep : KikasaEyeBloodShot.BloodMain,
                    Main.rand.NextFloat(0.45f, 0.8f))?.Configure(Main.rand.Next(22, 38));
            }
            if (t % 6 == 3) {
                KikasaDomainDeco.SplashAt(cross, 7);
            }
            if (t % 14 == 7) {
                PRTLoader.NewParticle<PRT_DWave>(cross, Vector2.Zero,
                    KikasaEyeBloodShot.BloodDeep, 0.08f)
                    ?.Configure(new Vector2(0.5f, 1f), -MathHelper.PiOver2, 0.32f, 10);
            }
        }

        /// <summary>余韵蒸汽带：收束时沿整条扫带铺一排慢升的长命白雾，久久不散</summary>
        private void LayResidualSteam() {
            if (Main.dedServ || float.IsNaN(sweptMinX) || sweptMaxX - sweptMinX < 60f) {
                return;
            }
            Player owner = Main.player[Projectile.owner];
            if (owner?.active != true
                || !owner.TryGetModPlayer(out KikasaDomainPlayer domain)
                || !domain.AnyActive || KikasaDomain.Viewed != domain) {
                return;
            }
            float lakeY = domain.LakeWorldY;
            int count = Math.Min(30, (int)((sweptMaxX - sweptMinX) / 46f) + 6);
            for (int i = 0; i < count; i++) {
                float x = MathHelper.Lerp(sweptMinX, sweptMaxX, (i + Main.rand.NextFloat(0.9f)) / count);
                PRTLoader.NewParticle<PRT_GhostRainMist>(
                    new Vector2(x, lakeY - Main.rand.NextFloat(4f, 30f)),
                    new Vector2(Main.rand.NextFloat(-0.15f, 0.15f), -Main.rand.NextFloat(0.15f, 0.45f)),
                    SteamPale * Main.rand.NextFloat(0.4f, 0.6f),
                    Main.rand.NextFloat(0.8f, 1.3f))?.Configure(Main.rand.Next(200, 330));
                //带里偶发一粒缓落的余沸血珠
                if (Main.rand.NextBool(3)) {
                    PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                        new Vector2(x, lakeY - Main.rand.NextFloat(2f, 12f)),
                        new Vector2(0f, -Main.rand.NextFloat(0.8f, 1.8f)),
                        KikasaEyeBloodShot.BloodMain * 0.5f,
                        Main.rand.NextFloat(0.3f, 0.5f))?.Configure(Main.rand.Next(30, 50));
                }
            }
        }

        //展开完才可伤
        public override bool? CanDamage() => Timer > ExpandFrames ? null : false;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            float p = 0f;
            //碰撞比视觉窄
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(),
                Projectile.Center, Projectile.Center + Projectile.rotation.ToRotationVector2() * beamLength,
                beamWidth * 0.55f, ref p);
        }

        public override bool PreDraw(ref Color lightColor) => false;

        /// <summary>近端 bleed，把硬切边藏进心脏</summary>
        private float MuzzleBackBleed => beamWidth * 0.36f + 48f;

        void IPrimitiveDrawable.DrawPrimitives() {
            if (beamWidth <= 1f || beamLength <= 10f) {
                return;
            }
            float opacity = MathHelper.Clamp(beamWidth / MaxWidth, 0f, 1f);
            Effect effect = EffectLoader.KikasaMoonRay?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (effect != null && noise != null) {
                DrawShaderBeam(effect, noise, opacity);
            }
            else {
                DrawFallbackBeam(opacity);
            }
        }

        void IAdditiveDrawable.DrawAdditiveAfterNon(SpriteBatch spriteBatch) {
            DrawAdditiveDressing(MathHelper.Clamp(beamWidth / MaxWidth, 0f, 1f));
        }

        /// <summary>KikasaMoonRay.fx 三层束体：预乘 AlphaBlend：暗缘要能压暗背景</summary>
        private void DrawShaderBeam(Effect effect, Texture2D noise, float opacity) {
            Vector2 mouth = Projectile.Center;
            Vector2 dir = Projectile.rotation.ToRotationVector2();
            Vector2 perp = dir.RotatedBy(MathHelper.PiOver2);
            Vector2 tip = mouth + dir * beamLength;
            Vector2 origin = mouth - dir * MuzzleBackBleed;
            //视觉半宽含暗缘/沸边余量
            float halfW = beamWidth * 2.2f;

            //uv.x 1缝口→0末端；uv.y 横截面
            VertexPositionColorTexture[] verts = new VertexPositionColorTexture[4];
            verts[0] = new VertexPositionColorTexture((origin + perp * halfW).ToVector3(), Color.White, new Vector2(1f, 0f));
            verts[1] = new VertexPositionColorTexture((origin - perp * halfW).ToVector3(), Color.White, new Vector2(1f, 1f));
            verts[2] = new VertexPositionColorTexture((tip + perp * halfW).ToVector3(), Color.White, new Vector2(0f, 0f));
            verts[3] = new VertexPositionColorTexture((tip - perp * halfW).ToVector3(), Color.White, new Vector2(0f, 1f));

            GraphicsDevice device = Main.graphics.GraphicsDevice;
            BlendState origBlend = device.BlendState;
            RasterizerState origRaster = device.RasterizerState;
            device.BlendState = BlendState.AlphaBlend;
            device.RasterizerState = RasterizerState.CullNone;

            effect.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            effect.Parameters["uFade"]?.SetValue(opacity);
            effect.Parameters["uSeed"]?.SetValue(Seed);
            effect.Parameters["uPulse"]?.SetValue(0.5f + 0.5f * MathF.Sin(Main.GlobalTimeWrappedHourly * 21f + Seed * 6f));
            effect.Parameters["uNoiseTex"]?.SetValue(noise);
            foreach (EffectPass pass in effect.CurrentTechnique.Passes) {
                pass.Apply();
                device.DrawUserPrimitives(PrimitiveType.TriangleStrip, verts, 0, 2);
            }

            device.BlendState = origBlend;
            device.RasterizerState = origRaster;
        }

        /// <summary>无着色器回退：三层拉伸光条拼出核/体/缘</summary>
        private void DrawFallbackBeam(float opacity) {
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow == null) {
                return;
            }
            SpriteBatch sb = Main.spriteBatch;
            sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
            Vector2 dir = Projectile.rotation.ToRotationVector2();
            Vector2 mid = Projectile.Center + dir * beamLength * 0.5f - Main.screenPosition;
            Vector2 gOrigin = glow.Size() * 0.5f;
            sb.Draw(glow, mid, null, ThemeBlood * (0.75f * opacity), Projectile.rotation,
                gOrigin, new Vector2(beamLength / glow.Width, beamWidth * 2.4f / glow.Height), SpriteEffects.None, 0f);
            sb.Draw(glow, mid, null, ThemeCore * (0.9f * opacity), Projectile.rotation,
                gOrigin, new Vector2(beamLength / glow.Width, beamWidth * 0.9f / glow.Height), SpriteEffects.None, 0f);
            sb.End();
        }

        /// <summary>缝口敷层：呼吸光球 + 竖向星芒（自竖缝喷薄）+ 束上推进光球</summary>
        private void DrawAdditiveDressing(float opacity) {
            Texture2D glow = CWRAsset.DiffusionCircle?.Value;
            Texture2D star = CWRAsset.StarTexture?.Value;
            if (glow == null || star == null) {
                return;
            }
            Vector2 dir = Projectile.rotation.ToRotationVector2();
            float flicker = 1f + 0.1f * MathF.Sin(Main.GlobalTimeWrappedHourly * 36f + Seed * 8f);

            Color blood = ThemeBlood;
            Color moon = KikasaMoonLordServant.MoonGlint;
            Color core = ThemeCore;

            //束上推进光球：血浪自缝口涌向远端
            Vector2 screenMouth = Projectile.Center - Main.screenPosition;
            const int pulses = 5;
            for (int i = 0; i < pulses; i++) {
                float along = (Main.GlobalTimeWrappedHourly * 0.8f + i / (float)pulses) % 1f;
                Vector2 pPos = screenMouth + dir * beamLength * along;
                float pScale = beamWidth / MaxWidth * (0.5f + 0.5f * MathF.Sin(along * MathHelper.Pi));
                Main.EntitySpriteDraw(glow, pPos, null, blood * (0.6f * opacity), 0f, glow.Size() / 2f,
                    pScale * 0.36f, SpriteEffects.None, 0);
            }

            //缝口呼吸球三层 + 竖向星芒
            float muzzleScale = beamWidth / MaxWidth;
            Main.EntitySpriteDraw(glow, screenMouth, null, blood * (0.9f * opacity), 0f, glow.Size() / 2f,
                muzzleScale * 2.0f * flicker, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(glow, screenMouth, null, moon * (0.65f * opacity), 0f, glow.Size() / 2f,
                muzzleScale * 1.2f, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(glow, screenMouth, null, core * opacity, 0f, glow.Size() / 2f,
                muzzleScale * 0.7f, SpriteEffects.None, 0);
            //竖缝星芒：竖向拉长，光是从缝里挤出来的
            Main.EntitySpriteDraw(star, screenMouth, null, core * (0.85f * opacity), MathHelper.PiOver2,
                star.Size() / 2f, new Vector2(muzzleScale * 0.4f, muzzleScale * 1.1f) * flicker, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(star, screenMouth, null, moon * (0.5f * opacity), MathHelper.PiOver2,
                star.Size() / 2f, new Vector2(muzzleScale * 0.26f, muzzleScale * 0.7f), SpriteEffects.None, 0);
        }
    }
}
