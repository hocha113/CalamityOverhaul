using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.HalibutLegend.FishSkills;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDomains;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaServants.KikasaEye;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using InnoVault.Trails;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaServants.KikasaEmpress
{
    /// <summary>
    /// 女皇鬼奴的血虹缎带：一条曲线行进的实体宽条带，不是直线光束。
    /// 带头沿大开大合的 S 曲线扫过战场一侧（ai0=基向 ai1=摆向，路径是
    /// 生命帧的确定性函数，各端一致），身后拖出双层缎面，血水条带打底、
    /// 原版虹彩渐变压低透明度做珠光叠层；扫过湖面犁开行进浪线，
    /// 尾声自带尾向头珠化断裂，散作一场珠雨落湖
    /// </summary>
    internal class KikasaEmpressRibbon : ModProjectile, IPrimitiveDrawable
    {
        public override string Texture => CWRConstant.VaultPlaceholder2;

        internal const float LaunchSpeed = 20f;

        /// <summary>行进期长度</summary>
        private const int TravelEnd = 80;

        /// <summary>珠化断裂期长度</summary>
        private const int BeadFrames = 34;

        private const int TotalLife = TravelEnd + BeadFrames;

        /// <summary>条带缓存长度（越长缎带越长）</summary>
        private const int TrailLen = 44;

        /// <summary>挥出基向（方向角）</summary>
        private ref float BaseAngle => ref Projectile.ai[0];

        /// <summary>垂摆起向符号：决定 S 曲线先向哪侧卷</summary>
        private ref float SwaySign => ref Projectile.ai[1];

        private ref float Life => ref Projectile.localAI[0];

        private Trail bloodTrail;
        private Trail sheenTrail;
        private float prevHeadY = float.NaN;
        private int lakeFxTick;

        private static Color BloodDark => KikasaDomain.CoolTint(new(64, 12, 14), new(38, 48, 52));
        private static Color BloodDeep => KikasaDomain.CoolTint(new(140, 32, 30), new(84, 104, 110));
        private static Color BloodMain => KikasaDomain.CoolTint(new(237, 77, 69), new(126, 158, 164));
        private static Color PearlBright => KikasaDomain.CoolTint(new(246, 170, 150), new(180, 204, 208));

        /// <summary>连续量抖动的确定性相位（9.1：绘制路径不掷 Main.rand）</summary>
        private float Seed => Projectile.identity * 0.7391f % 3.71f;

        private float VisualFade => MathHelper.Clamp(Life / 5f, 0f, 1f);

        /// <summary>珠化进度 0~1：条带自尾向头被吃掉的份额</summary>
        private float BeadEat => Life <= TravelEnd ? 0f
            : MathHelper.Clamp((Life - TravelEnd) / (float)BeadFrames, 0f, 1f);

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = TrailLen;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
            ProjectileID.Sets.DrawScreenCheckFluff[Projectile.type] = 1200;
        }

        public override void SetDefaults() {
            Projectile.width = 30;
            Projectile.height = 30;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.penetrate = -1;
            Projectile.timeLeft = TotalLife + 20;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 16;
        }

        /// <summary>珠化期不再伤人：伤害窗与可见的行进条带严格对齐</summary>
        public override bool? CanDamage() => Life > 2f && Life <= TravelEnd ? null : false;

        /// <summary>整条缎带都是判定体：沿旧位隔段线碰撞</summary>
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            Vector2[] oldPos = Projectile.oldPos;
            if (oldPos == null) {
                return false;
            }
            float _ = 0f;
            Vector2 half = Projectile.Size * 0.5f;
            int usable = (int)((1f - BeadEat) * TrailLen) - 1;
            for (int i = 2; i < usable; i += 2) {
                if (oldPos[i] == Vector2.Zero || oldPos[i - 2] == Vector2.Zero) {
                    continue;
                }
                if (Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(),
                    oldPos[i - 2] + half, oldPos[i] + half, 30f, ref _)) {
                    return true;
                }
            }
            return false;
        }

        public override void AI() {
            Life++;

            if (Life <= TravelEnd) {
                //S 曲线行进：方向 = 基向 + 大幅正弦垂摆（幅度缓收），速度复利微增
                //路径是 Life 的确定性函数，各端一致，缎带不是直线光束
                float sway = MathF.Sin(Life * 0.075f + 0.3f) * 0.92f * SwaySign
                    * (1f - Life / 260f);
                float speed = LaunchSpeed * (1f + Life * 0.0045f);
                Projectile.velocity = (BaseAngle + sway).ToRotationVector2() * speed;
                Projectile.rotation = Projectile.velocity.ToRotation();

                //缎面甩珠：带头身后撕下小血珠
                if (!Main.dedServ && Life % 3 == 0) {
                    Vector2 dir = Projectile.velocity.SafeNormalize(Vector2.UnitY);
                    PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                        Projectile.Center - dir * Main.rand.NextFloat(8f, 22f),
                        Projectile.velocity * 0.12f + dir.RotatedBy(MathHelper.PiOver2) * Main.rand.NextFloat(-1f, 1f),
                        (Main.rand.NextBool(3) ? BloodDeep : BloodMain) * 0.55f,
                        Main.rand.NextFloat(0.3f, 0.5f))?.Configure(Main.rand.Next(14, 24));
                }
                //缎面珠光碎星：稀疏虹彩
                if (!Main.dedServ && Main.rand.NextBool(7)) {
                    PRTLoader.NewParticle<PRT_Sparkle>(
                        Projectile.Center - Projectile.velocity * Main.rand.NextFloat(0.4f, 1.6f),
                        Vector2.Zero,
                        KikasaEmpressServant.IridescentTint(Main.rand.NextFloat()) * 0.42f,
                        Main.rand.NextFloat(0.22f, 0.38f))?.Configure(PearlBright * 0.4f, 12, 0f, 0.4f);
                }

                UpdateLakeSweep();
            }
            else {
                //珠化断裂：带头急刹，条带自尾向头散成珠雨
                Projectile.velocity *= 0.86f;
                if (Life == TravelEnd + 1) {
                    SoundEngine.PlaySound(SoundID.Item165 with { Volume = 0.35f, Pitch = -0.3f, MaxInstances = 2 }, Projectile.Center);
                    SoundEngine.PlaySound(SoundID.Drip with { Volume = 0.5f, Pitch = -0.4f, MaxInstances = 2 }, Projectile.Center);
                }
                ShedBeads();
                if (Life >= TotalLife) {
                    Projectile.Kill();
                    return;
                }
            }

            float glow = 0.5f * VisualFade * (1f - BeadEat * 0.7f);
            Lighting.AddLight(Projectile.Center, 0.5f * glow, 0.13f * glow, 0.17f * glow);
        }

        /// <summary>珠化脱落：正被吃掉的尾段化作带重力的血珠，虹彩星屑稀疏点缀</summary>
        private void ShedBeads() {
            if (Main.dedServ || Projectile.oldPos == null) {
                return;
            }
            //本帧被吃掉的旧位区间
            float prevEat = MathHelper.Clamp((Life - 1 - TravelEnd) / (float)BeadFrames, 0f, 1f);
            int from = (int)((1f - BeadEat) * (TrailLen - 1));
            int to = (int)((1f - prevEat) * (TrailLen - 1));
            Vector2 half = Projectile.Size * 0.5f;
            for (int i = from; i <= to && i < TrailLen; i++) {
                if (i < 0 || Projectile.oldPos[i] == Vector2.Zero) {
                    continue;
                }
                Vector2 pos = Projectile.oldPos[i] + half;
                for (int k = 0; k < 2; k++) {
                    PRTLoader.NewParticle<PRT_KikasaBloodGlob>(pos + Main.rand.NextVector2Circular(9f, 9f),
                        new Vector2(Main.rand.NextFloat(-0.8f, 0.8f), Main.rand.NextFloat(0.2f, 1.4f)),
                        (Main.rand.NextBool(3) ? BloodDeep : BloodMain) * Main.rand.NextFloat(0.5f, 0.65f),
                        Main.rand.NextFloat(0.32f, 0.55f))?.Configure(Main.rand.Next(24, 40), 0.26f);
                }
                if (Main.rand.NextBool(3)) {
                    PRTLoader.NewParticle<PRT_Sparkle>(pos, new Vector2(0f, Main.rand.NextFloat(0.2f, 0.7f)),
                        KikasaEmpressServant.IridescentTint(i * 0.06f) * 0.4f,
                        Main.rand.NextFloat(0.2f, 0.34f))?.Configure(PearlBright * 0.35f, 16, 0.02f, 0.4f);
                }
            }
        }

        /// <summary>缎带扫过血湖：带头过水线的瞬间与贴水行进都犁开浪线（观看域门控）</summary>
        private void UpdateLakeSweep() {
            Player owner = Main.player[Projectile.owner];
            if (owner?.active != true
                || !owner.TryGetModPlayer(out KikasaDomainPlayer domain)
                || !domain.AnyActive || domain.RiseT <= 0.5f
                || KikasaDomain.Viewed != domain) {
                prevHeadY = Projectile.Center.Y;
                return;
            }
            float lakeY = domain.LakeWorldY;
            float y = Projectile.Center.Y;

            //过水线拍：上下穿越都溅
            if (!float.IsNaN(prevHeadY) && (prevHeadY < lakeY) != (y < lakeY)) {
                Vector2 hit = new(Projectile.Center.X, lakeY);
                KikasaDomainDeco.SplashAt(hit, 8);
                KikasaDomainDeco.RippleAt(hit, 1.2f);
                SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 0.5f, Pitch = 0f, MaxInstances = 3 }, hit);
            }
            //贴水行进：浪线跟着缎带走
            else if (MathF.Abs(y - lakeY) < 46f && ++lakeFxTick % 3 == 0) {
                KikasaDomainDeco.RippleAt(new Vector2(Projectile.Center.X, lakeY),
                    0.5f + MathF.Abs(Projectile.velocity.X) * 0.014f);
            }
            prevHeadY = y;
        }

        //==================== 谢幕 ====================

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            //残余带段一并散珠，别让条带凭空消失
            Vector2 half = Projectile.Size * 0.5f;
            int usable = (int)((1f - BeadEat) * (TrailLen - 1));
            for (int i = 0; i < usable; i += 3) {
                if (Projectile.oldPos[i] == Vector2.Zero) {
                    continue;
                }
                PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                    Projectile.oldPos[i] + half + Main.rand.NextVector2Circular(6f, 6f),
                    new Vector2(Main.rand.NextFloat(-0.6f, 0.6f), Main.rand.NextFloat(0.3f, 1.6f)),
                    BloodMain * 0.55f, Main.rand.NextFloat(0.3f, 0.5f))?.Configure(Main.rand.Next(20, 34), 0.26f);
            }
        }

        //==================== 绘制 ====================

        public override bool PreDraw(ref Color lightColor) => false;

        /// <summary>血水底带宽度：带头最宽、带尾收细，珠化自尾向头吃掉</summary>
        public float GetBloodWidth(float completionRatio) {
            float eatGate = BeadEat <= 0f ? 1f
                : MathHelper.Clamp(((1f - completionRatio) - BeadEat) * 6f, 0f, 1f);
            return MathHelper.Lerp(24f, 9f, completionRatio) * VisualFade * eatGate;
        }

        /// <summary>珠光叠层宽度：比血底窄一圈，压在带心</summary>
        public float GetSheenWidth(float completionRatio) => GetBloodWidth(completionRatio) * 0.62f;

        public Color GetColorFunc(Vector2 coord) => Color.White;

        /// <summary>珠光叠层顶点色：透明度即珠光强度，血底绝不被盖过</summary>
        public Color GetSheenColor(Vector2 coord)
            => Color.White * (0.30f * VisualFade * (1f - KikasaDomain.ViewedRainBlend * 0.5f));

        void IPrimitiveDrawable.DrawPrimitives() {
            if (Main.dedServ || !Projectile.active || VisualFade <= 0.01f) {
                return;
            }
            Vector2[] oldPos = Projectile.oldPos;
            if (oldPos == null || oldPos.Length == 0) {
                return;
            }
            Vector2[] positions = new Vector2[oldPos.Length];
            for (int i = 0; i < positions.Length; i++) {
                if (oldPos[i] == Vector2.Zero) {
                    oldPos[i] = Projectile.position;
                }
                positions[i] = oldPos[i] + Projectile.Size * 0.5f;
            }

            DrawBloodBand(positions);
            DrawSheenBand(positions);
            DrawHead();
        }

        /// <summary>血水缎底：借灵液液柱条带 shader 换血色板，缎面有流动的水肌理</summary>
        private void DrawBloodBand(Vector2[] positions) {
            Effect fx = FishIchornAssets.FishIchornJet;
            if (fx == null) {
                return;
            }
            fx.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            fx.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            fx.Parameters["uSeed"]?.SetValue(Seed);
            fx.Parameters["uFade"]?.SetValue(VisualFade * 0.92f);
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (noise != null) {
                fx.Parameters["uNoiseTex"]?.SetValue(noise);
            }
            fx.Parameters["uColDark"]?.SetValue(BloodDark.ToVector3());
            fx.Parameters["uColDeep"]?.SetValue(BloodDeep.ToVector3());
            fx.Parameters["uColGold"]?.SetValue(BloodMain.ToVector3());
            fx.Parameters["uColBright"]?.SetValue(PearlBright.ToVector3());

            bloodTrail ??= new Trail(positions, GetBloodWidth, GetColorFunc);
            bloodTrail.TrailPositions = positions;
            Main.graphics.GraphicsDevice.BlendState = BlendState.AlphaBlend;
            bloodTrail.DrawTrail(fx);
        }

        /// <summary>虹彩珠光叠层：原版女皇渐变条 + 流线底图，加色压低透明度浮在血底上</summary>
        private void DrawSheenBand(Vector2[] positions) {
            Effect fx = EffectLoader.GradientTrail?.Value;
            Texture2D gradient = TextureAssets.Extra[156]?.Value;
            Texture2D flowBase = CWRAsset.Airflow?.Value;
            if (fx == null || gradient == null || flowBase == null) {
                return;
            }
            fx.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            fx.Parameters["uTime"]?.SetValue((float)Main.timeForVisualEffects * 0.06f);
            fx.Parameters["uTimeG"]?.SetValue(Main.GlobalTimeWrappedHourly * 0.25f);
            fx.Parameters["udissolveS"]?.SetValue(1f);
            fx.Parameters["uBaseImage"]?.SetValue(flowBase);
            fx.Parameters["uFlow"]?.SetValue(CWRAsset.PerlinNoise?.Value ?? flowBase);
            fx.Parameters["uGradient"]?.SetValue(gradient);
            fx.Parameters["uDissolve"]?.SetValue(CWRAsset.Extra_193?.Value ?? flowBase);

            sheenTrail ??= new Trail(positions, GetSheenWidth, GetSheenColor);
            sheenTrail.TrailPositions = positions;
            GraphicsDevice device = Main.graphics.GraphicsDevice;
            device.BlendState = BlendState.Additive;
            sheenTrail.DrawTrail(fx);
            device.BlendState = BlendState.AlphaBlend;
        }

        /// <summary>带头液团：暗血压边→血红主体→珠光亮芯，行进期才有头</summary>
        private void DrawHead() {
            Texture2D tex = CWRAsset.Extra_98?.Value;
            if (tex == null || BeadEat >= 0.35f) {
                return;
            }
            SpriteBatch sb = Main.spriteBatch;
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            float fade = VisualFade * (1f - BeadEat / 0.35f);
            Vector2 pos = Projectile.Center + Projectile.velocity * 0.4f - Main.screenPosition;
            Vector2 origin = tex.Size() * 0.5f;
            float rot = Projectile.rotation + MathHelper.PiOver2;
            float stretch = MathHelper.Clamp(Projectile.velocity.Length() * 0.03f, 0.2f, 0.85f);

            sb.Draw(tex, pos, null, BloodDark * (0.8f * fade), rot, origin,
                new Vector2(0.5f, 0.56f + stretch * 0.8f), SpriteEffects.None, 0f);
            sb.Draw(tex, pos, null, BloodMain * fade, rot, origin,
                new Vector2(0.38f, 0.46f + stretch * 0.7f), SpriteEffects.None, 0f);
            Color core = PearlBright with { A = 0 };
            sb.Draw(tex, pos, null, core * (0.55f * fade), rot, origin,
                new Vector2(0.13f, 0.24f + stretch * 0.3f), SpriteEffects.None, 0f);

            //带头珠光星：虹彩慢转
            Texture2D star = CWRAsset.StarTexture?.Value;
            if (star != null) {
                Color iri = KikasaEmpressServant.IridescentTint(Main.GlobalTimeWrappedHourly * 0.13f + Seed) with { A = 0 };
                sb.Draw(star, pos, null, iri * (0.4f * fade), Main.GlobalTimeWrappedHourly * 2.4f,
                    star.Size() * 0.5f, 0.16f, SpriteEffects.None, 0f);
            }
            sb.End();
        }
    }
}
