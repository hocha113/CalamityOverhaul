using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMoonLord.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMoonLord.Rendering;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMoonLord.Projectiles
{
    /// <summary>
    /// 黑闪黑洞弹体：慢起步复合加速直线掷向锚点（预告即承诺，不追踪）。
    /// 飞行期引力井拉拽玩家（公平阀：牵引朝向分速度封顶，留挣脱手段）；
    /// 到锚/寿终→坍缩预兆→黑闪爆点（伤害窗与可见冲击环严格同半径）→余辉。
    /// 暗核真 alpha 遮挡 + 红黑电弧加色缘；引力透镜走 Warp 层
    /// </summary>
    internal class MLordBlackHoleProj : ModProjectile, IWarpDrawable
    {
        public override string Texture => CWRConstant.VaultPlaceholder2;

        //―――― 时间轴（timeLeft 递减制）――――
        internal const int FlightLife = 180;
        internal const int CollapseLife = 16;
        internal const int FlashLife = 14;
        internal const int LingerLife = 34;
        internal const int TotalLife = FlightLife + CollapseLife + FlashLife + LingerLife;

        //―――― 公平阀（发射/拉拽/爆点逻辑真正读取的命名常量）――――
        /// <summary>出手初速（慢起步：给玩家读向时间）</summary>
        internal const float LaunchSpeed = 4.6f;
        /// <summary>残血底牌拍出手初速（更快，锁定承诺与超长预告不变）</summary>
        internal const float DesperateLaunchSpeed = 6.9f;
        /// <summary>复合加速倍率/帧</summary>
        private const float AccelRate = 1.0175f;
        /// <summary>速度上限</summary>
        private const float MaxSpeed = 14.5f;
        /// <summary>残血底牌拍速度上限</summary>
        private const float DesperateMaxSpeed = 17.5f;
        /// <summary>引力井作用半径 px</summary>
        private const float PullRadius = 780f;
        /// <summary>强拉半径（此内拉力最大）</summary>
        private const float HardPullRadius = 260f;
        /// <summary>被拉向洞的分速度封顶：低于它才施力，正常位移速度即可挣脱（逃逸阀）</summary>
        private const float EscapeTowardSpeedCap = 8f;
        /// <summary>出手后引力宽限帧：贴脸掷出不做无预警吸附（接触伤同吃此宽限）</summary>
        private const int GraceFrames = 20;
        /// <summary>黑洞本体接触判定半径（与可见暗核 φ 对齐：判定=视觉）</summary>
        private const float CoreRadius = 48f;
        /// <summary>黑闪爆点最大半径：伤害窗逐帧取当前可见环半径，绝不超出</summary>
        internal const float DetonationRadius = 320f;
        /// <summary>飞行体绘制半径（终局大招的体量：暗核+吸积盘+电弧结构约 2.5 倍此值）</summary>
        private const float FlightBodyRadius = 76f;

        /// <summary>飞行帧计数（本地推进，仅表现与宽限判断用）</summary>
        private ref float FlightTimer => ref Projectile.localAI[0];
        /// <summary>已越过锚点后的累计位移 px（服务端提前引爆判据）</summary>
        private ref float PassedDist => ref Projectile.localAI[1];

        private Vector2 Anchor => new(Projectile.ai[0], Projectile.ai[1]);
        /// <summary>残血底牌拍标记（ai[2]，随生成包同步）：巨体量+更快+爆点大幅震屏与大面积扭曲</summary>
        private bool Desperate => Projectile.ai[2] == 1f;
        /// <summary>残血底牌拍体量倍率：暗核视觉与接触判定同倍放大（视觉=判定的承诺不破）</summary>
        private float BodyScale => Desperate ? 2.5f : 1f;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 1400;
            //同材质拖尾缓存（契约5：飞行弹必须有可读尾迹）
            ProjectileID.Sets.TrailCacheLength[Type] = 10;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = (int)(CoreRadius * 2f);
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = TotalLife;
        }

        //―――― 阶段判定（timeLeft 同步，各端一致）――――
        private bool InFlight => Projectile.timeLeft > CollapseLife + FlashLife + LingerLife;
        private bool InCollapse => !InFlight && Projectile.timeLeft > FlashLife + LingerLife;
        private bool InFlash => !InFlight && !InCollapse && Projectile.timeLeft > LingerLife;
        private bool InLinger => Projectile.timeLeft <= LingerLife;
        /// <summary>坍缩进度 0~1</summary>
        private float CollapseT => InCollapse
            ? 1f - (Projectile.timeLeft - FlashLife - LingerLife) / (float)CollapseLife : (InFlight ? 0f : 1f);
        /// <summary>爆闪进度 0~1</summary>
        private float FlashT => InFlash ? 1f - (Projectile.timeLeft - LingerLife) / (float)FlashLife : (InLinger ? 1f : 0f);
        /// <summary>当前可见冲击环半径（伤害窗逐帧对齐它）</summary>
        private float FlashRingRadius => DetonationRadius * VaultUtils.EaseOutCubic(FlashT);

        public override void AI() {
            FlightTimer++;

            if (InFlight) {
                UpdateFlight();
            }
            else if (InCollapse) {
                //坍缩预兆：停摆 + 收缩（变小再变响）
                Projectile.velocity *= 0.82f;
                if (Projectile.timeLeft == FlashLife + LingerLife + CollapseLife && !VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.MaxMana with { Volume = 0.8f, Pitch = -1f }, Projectile.Center);
                }
            }
            else if (InFlash) {
                Projectile.velocity = Vector2.Zero;
                if (Projectile.timeLeft == FlashLife + LingerLife && !VaultUtils.isServer) {
                    FireFlashPresentation();
                }
            }
            else {
                //余辉：无判定的消散
                Projectile.velocity = Vector2.Zero;
            }

            Lighting.AddLight(Projectile.Center, MLordDirector.BlackFlashRed.ToVector3() * 0.5f * (1f - FlashT * 0.5f));
        }

        /// <summary>飞行：复合加速 + 引力拉拽 + 过锚提前引爆（服务端判据）</summary>
        private void UpdateFlight() {
            //复合加速：慢起步越飞越快（重量感=起步迟，威胁感=后段快）；残血拍全程更快
            float speed = Projectile.velocity.Length();
            if (speed < (Desperate ? DesperateMaxSpeed : MaxSpeed)) {
                Projectile.velocity *= AccelRate;
            }

            //引力井：只拉本地玩家（动作权威在玩家本地），宽限期不吸
            Player local = Main.LocalPlayer;
            if (!VaultUtils.isServer && FlightTimer > GraceFrames && local.active && !local.dead) {
                Vector2 toHole = Projectile.Center - local.Center;
                float dist = toHole.Length();
                if (dist < PullRadius && dist > 30f) {
                    float strength = MathHelper.Lerp(0.07f, 0.36f,
                        MathHelper.Clamp(1f - (dist - HardPullRadius) / (PullRadius - HardPullRadius), 0f, 1f));
                    Vector2 pullDir = toHole.SafeNormalize(Vector2.Zero);
                    //逃逸阀：朝洞分速度低于封顶才施力，位移技/正常横移足以挣脱
                    if (Vector2.Dot(local.velocity, pullDir) < EscapeTowardSpeedCap) {
                        local.velocity += pullDir * strength;
                    }
                }
            }

            //过锚判据（服务端权威）：越过锚点后再飞 140px 即入坍缩，timeLeft 改写随包同步
            if (!VaultUtils.isClient) {
                if (Vector2.Dot(Projectile.velocity, Anchor - Projectile.Center) < 0f) {
                    PassedDist += Projectile.velocity.Length();
                    if (PassedDist > 140f) {
                        Projectile.timeLeft = CollapseLife + FlashLife + LingerLife;
                        Projectile.netUpdate = true;
                    }
                }
            }

            //―――― 客户端飞行表现 ――――
            if (VaultUtils.isServer) {
                return;
            }
            //吸积：周边星尘被拉进洞（红黑材质），取位随体量同倍外扩
            if (Main.rand.NextBool(2)) {
                Vector2 pos = Projectile.Center + Main.rand.NextVector2Unit()
                    * Main.rand.NextFloat(90f, 240f) * BodyScale;
                Vector2 pull = (Projectile.Center - pos) * 0.1f + Projectile.velocity * 0.4f;
                Color c = Color.Lerp(MLordDirector.BlackFlashRed, MLordDirector.VoidBlack, Main.rand.NextFloat(0.6f));
                PRTLoader.NewParticle<PRT_HeavenfallStar>(pos, pull.RotatedBy(0.4f), c,
                    Main.rand.NextFloat(0.3f, 0.6f))?.Configure(false, Main.rand.Next(10, 16));
            }
            //缘弧迸溅
            if (Main.rand.NextBool(7)) {
                PRTLoader.NewParticle<PRT_Spark>(
                    Projectile.Center + Main.rand.NextVector2Unit() * CoreRadius * BodyScale * 1.3f,
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(2f, 5f),
                    MLordDirector.BlackFlashRed, Main.rand.NextFloat(0.8f, 1.2f))
                    ?.Configure(false, Main.rand.Next(8, 14));
            }
        }

        /// <summary>黑闪爆点表现：冲击帧 + 屏效 + 红黑碎星（伤害窗同帧开启）。
        /// 残血底牌拍全面放大：涟漪列全屏波纹（护盾爆碎级）+ 碎星更多更远更大</summary>
        private void FireFlashPresentation() {
            SoundEngine.PlaySound(SoundID.Item122 with { Volume = 1.2f, Pitch = -0.45f }, Projectile.Center);
            SoundEngine.PlaySound(SoundID.Zombie104 with { Volume = 1f, Pitch = 0.1f }, Projectile.Center);
            //残血底牌拍：爆点大幅震撼（冲击帧加重 + 约 1.2 秒长余震），开幕拍保持正常冲击
            MLordScreenFX.Punch(Projectile.Center, Desperate ? 20f : 13f, Desperate ? 26 : 18);
            if (Desperate) {
                Main.LocalPlayer.CWR()?.GetScreenShake(14f);
            }
            //残血大爆：超 1 强度显形尾随涟漪列，58 帧扩散窗把波扫出全屏
            MLordBlackFlashFX.PushFlash(Projectile.Center,
                Desperate ? 2.2f : 1f, Desperate ? 58 : MLordBlackFlashFX.BaseLife);
            //红黑碎星 + 空间裂纹（残血拍：更多、更快、更大）
            int starCount = Desperate ? 42 : 26;
            float velScale = Desperate ? 1.6f : 1f;
            float sizeScale = Desperate ? 1.35f : 1f;
            for (int i = 0; i < starCount; i++) {
                Vector2 vel = Main.rand.NextVector2Unit() * Main.rand.NextFloat(3f, 13f) * velScale;
                Color c = Color.Lerp(MLordDirector.BlackFlashRed, MLordDirector.MoonWhite, Main.rand.NextFloat(0.35f));
                PRTLoader.NewParticle<PRT_HeavenfallStar>(Projectile.Center, vel, c,
                    Main.rand.NextFloat(0.6f, 1.2f) * sizeScale)?.Configure(true, Main.rand.Next(20, 36));
            }
            int fractureCount = Desperate ? 11 : 6;
            for (int i = 0; i < fractureCount; i++) {
                PRTLoader.NewParticle<PRT_SpaceFracture>(Projectile.Center,
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(2f, 6f) * velScale,
                    MLordDirector.BlackFlashRed, Main.rand.NextFloat(0.9f, 1.4f) * sizeScale)
                    ?.Configure(Main.rand.Next(18, 28), Main.rand.NextFloat(-0.05f, 0.05f));
            }
        }

        /// <summary>判定：飞行/坍缩=本体小圆；爆闪=逐帧对齐可见冲击环；余辉无判定。
        /// 出手宽限帧内接触伤随引力一起豁免：贴脸掷出不做无预警判定（契约3）</summary>
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (InLinger) {
                return false;
            }
            if (InFlight && FlightTimer <= GraceFrames) {
                return false;
            }
            //暗核判定随体量同倍放大（可见暗核=判定圆）；爆闪环半径不随体量走
            float radius = InFlash ? FlashRingRadius : CoreRadius * BodyScale;
            Vector2 closest = new(
                MathHelper.Clamp(Projectile.Center.X, targetHitbox.Left, targetHitbox.Right),
                MathHelper.Clamp(Projectile.Center.Y, targetHitbox.Top, targetHitbox.Bottom));
            return Vector2.DistanceSquared(closest, Projectile.Center) <= radius * radius;
        }

        public override void ModifyHitPlayer(Player target, ref Player.HurtModifiers modifiers) {
            //爆闪窗吃满额外爆点伤害（长预告演出级的一锤）
            if (InFlash) {
                modifiers.SourceDamage *= MLordDirector.BlackFlashBurstDamage / (float)MLordDirector.BlackHoleContactDamage;
            }
        }

        #region 扭曲与绘制

        public bool DontUseBlueshiftEffect() => true;
        public bool CanDrawCustom() => false;
        public void DrawCustom(SpriteBatch spriteBatch) { }

        /// <summary>引力透镜：飞行常驻，坍缩收紧，爆闪一记扩张脉冲。
        /// 残血底牌拍：透镜场随体量放大，爆点扩成大面积光线扭曲并在余辉期继续外扩</summary>
        public void Warp() {
            float env;
            float size;
            if (InFlight) {
                env = MathHelper.Clamp(FlightTimer / 20f, 0f, 1f);
                size = 800f * BodyScale;
            }
            else if (InCollapse) {
                env = 1f;
                size = MathHelper.Lerp(800f, 420f, CollapseT) * BodyScale;
            }
            else {
                float fade = InFlash ? 1f : 1f - Projectile.timeLeft / (float)LingerLife;
                env = 1f - fade * 0.85f;
                size = MathHelper.Lerp(420f * BodyScale, Desperate ? 4600f : 1400f,
                    VaultUtils.EaseOutCubic(FlashT));
                if (Desperate && InLinger) {
                    //余辉期扭曲场继续外扩到 ~7200px：大面积光线涟漪扫过战场后随 env 自然消散
                    size += (1f - Projectile.timeLeft / (float)LingerLife) * 2600f;
                }
            }
            if (env <= 0.04f) {
                return;
            }
            //残血拍飞行/坍缩 0.55，爆闪与余辉顶到 0.7——增强聚焦在爆点
            float strength = Desperate ? (InFlight || InCollapse ? 0.55f : 0.7f) : 0.38f;
            NeutronWarpHelper.DrawWarp(Projectile.Center, size, size,
                strength * env, 1f, 0f, "GravitationalLens", 0.42f);
        }

        public override bool PreDraw(ref Color lightColor) {
            Vector2 pos = Projectile.Center - Main.screenPosition;
            //残血底牌拍全程 2.5 倍体量（坍缩/爆闪的收缩终值同倍，节奏曲线不变）
            float bodyR = (InCollapse
                ? MathHelper.Lerp(FlightBodyRadius, 40f, CollapseT) : FlightBodyRadius) * BodyScale;
            float bodyVis = InLinger ? Projectile.timeLeft / (float)LingerLife : 1f;
            if (InFlash) {
                bodyR = MathHelper.Lerp(40f, 18f, FlashT) * BodyScale;
            }

            if (InFlight) {
                DrawTrail(bodyR);
            }
            DrawHoleBody(pos, bodyR, bodyVis * (1f - FlashT));
            if (FlashT > 0f) {
                DrawFlashRing(pos);
            }
            return false;
        }

        /// <summary>
        /// 同材质拖尾（契约5）：黑洞自己的暗核+红缘按 oldPos 重绘（0.55×、衰减 alpha），
        /// 读作洞体撕开空间留下的尾迹而非装饰光带
        /// </summary>
        private void DrawTrail(float bodyR) {
            Texture2D glow = CWRAsset.DiffusionCircle?.Value;
            if (glow == null) {
                return;
            }
            for (int i = Projectile.oldPos.Length - 1; i >= 1; i--) {
                Vector2 oldPos = Projectile.oldPos[i];
                if (oldPos == Vector2.Zero) {
                    continue;
                }
                float k = 1f - i / (float)Projectile.oldPos.Length;
                Vector2 pos = oldPos + Projectile.Size * 0.5f - Main.screenPosition;
                float r = bodyR * 0.55f * (0.45f + 0.55f * k);
                float texScale = r * 2.6f / glow.Width;
                //红缘（加色）压底，暗核（真 alpha）叠上：与本体同层序同材质
                Main.EntitySpriteDraw(glow, pos, null,
                    MLordDirector.BlackFlashRed with { A = 0 } * (0.34f * k),
                    Main.GlobalTimeWrappedHourly * 1.5f + i * 0.3f, glow.Size() / 2f,
                    texScale * 1.25f, SpriteEffects.None, 0);
                Main.EntitySpriteDraw(glow, pos, null, MLordDirector.VoidBlack * (0.55f * k),
                    -Main.GlobalTimeWrappedHourly + i * 0.3f, glow.Size() / 2f,
                    texScale, SpriteEffects.None, 0);
            }
        }

        /// <summary>洞体：shader 量体（暗核+吸积盘+电弧），缺 shader 走 CPU 双层</summary>
        private void DrawHoleBody(Vector2 pos, float radius, float vis) {
            if (vis <= 0.02f) {
                return;
            }
            Effect shader = EffectLoader.MLordBlackFlash?.Value;
            if (shader != null) {
                Texture2D canvas = CWRUtils.GetT2DAsset(CWRConstant.VaultPlaceholder2).Value;
                float scale = radius * 5f / canvas.Width;
                shader.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
                shader.Parameters["uCollapse"]?.SetValue(0.9f + CollapseT * 0.1f);
                shader.Parameters["uArc"]?.SetValue(0.85f + CollapseT * 0.15f);
                shader.Parameters["uAlpha"]?.SetValue(vis);
                shader.Parameters["uSeed"]?.SetValue(Projectile.identity % 89 * 0.211f);

                Main.spriteBatch.End();
                Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, Main.DefaultSamplerState,
                    DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
                GraphicsDevice gd = Main.instance.GraphicsDevice;
                gd.Textures[1] = CWRAsset.PerlinNoise.Value;
                gd.SamplerStates[1] = SamplerState.LinearWrap;
                shader.CurrentTechnique.Passes[0].Apply();
                Main.spriteBatch.Draw(canvas, pos, null, Color.White, 0f,
                    canvas.Size() * 0.5f, scale, SpriteEffects.None, 0f);
                Main.spriteBatch.End();
                Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                    DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
                return;
            }

            //CPU 回退：暗核真 alpha + 红缘 + 斜吸积盘
            Texture2D glow = CWRAsset.DiffusionCircle?.Value;
            if (glow == null) {
                return;
            }
            float texScale = radius * 2.6f / glow.Width;
            Main.EntitySpriteDraw(glow, pos, null, MLordDirector.BlackFlashRed with { A = 0 } * (0.5f * vis),
                Main.GlobalTimeWrappedHourly * 1.5f, glow.Size() / 2f, texScale * 1.25f, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(glow, pos, null, MLordDirector.VoidBlack * (0.96f * vis),
                -Main.GlobalTimeWrappedHourly, glow.Size() / 2f, texScale, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(glow, pos, null, MLordDirector.BlackFlashRed with { A = 0 } * (0.55f * vis),
                Main.GlobalTimeWrappedHourly * 2.4f, glow.Size() / 2f,
                new Vector2(texScale * 1.7f, texScale * 0.4f), SpriteEffects.None, 0);
        }

        /// <summary>爆闪冲击环：可见环半径即伤害半径（视觉=判定，一像素不差的承诺）</summary>
        private void DrawFlashRing(Vector2 pos) {
            Texture2D glow = CWRAsset.DiffusionCircle?.Value;
            Texture2D star = CWRAsset.StarTexture?.Value;
            if (glow == null || star == null) {
                return;
            }
            float ringR = FlashRingRadius;
            float fade = InLinger ? Projectile.timeLeft / (float)LingerLife : 1f;
            float ringScale = ringR * 2f / glow.Width;
            //白芯闪帧（只在爆闪窗内的短脉冲）；残血拍白芯放大一倍——爆心体量而非判定边界，不误读
            if (InFlash) {
                float coreScale = (0.6f + FlashT * 0.5f) * (Desperate ? 2f : 1f);
                Main.EntitySpriteDraw(star, pos, null, MLordDirector.MoonWhite with { A = 0 } * (0.9f * (1f - FlashT)),
                    Main.GlobalTimeWrappedHourly * 3f, star.Size() / 2f, coreScale, SpriteEffects.None, 0);
            }
            //红黑环体：外红缘 + 内暗吞（暗层真 alpha，把爆心咬出一圈黑）
            Main.EntitySpriteDraw(glow, pos, null, MLordDirector.BlackFlashRed with { A = 0 } * (0.85f * fade),
                0f, glow.Size() / 2f, ringScale, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(glow, pos, null, MLordDirector.VoidBlack * (0.7f * fade * (1f - FlashT)),
                0f, glow.Size() / 2f, ringScale * 0.62f, SpriteEffects.None, 0);
        }

        #endregion
    }
}
