using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMoonLord.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMoonLord.Rendering;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
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
        internal const int FlashLife = 8;
        internal const int LingerLife = 34;
        internal const int TotalLife = FlightLife + CollapseLife + FlashLife + LingerLife;

        //―――― 公平阀（发射/拉拽/爆点逻辑真正读取的命名常量）――――
        /// <summary>出手初速（慢起步：给玩家读向时间）</summary>
        internal const float LaunchSpeed = 4.6f;
        /// <summary>复合加速倍率/帧</summary>
        private const float AccelRate = 1.0175f;
        /// <summary>速度上限</summary>
        private const float MaxSpeed = 14.5f;
        /// <summary>引力井作用半径 px</summary>
        private const float PullRadius = 780f;
        /// <summary>强拉半径（此内拉力最大）</summary>
        private const float HardPullRadius = 260f;
        /// <summary>被拉向洞的分速度封顶：低于它才施力，正常位移速度即可挣脱（逃逸阀）</summary>
        private const float EscapeTowardSpeedCap = 8f;
        /// <summary>出手后引力宽限帧：贴脸掷出不做无预警吸附</summary>
        private const int GraceFrames = 20;
        /// <summary>黑洞本体接触判定半径</summary>
        private const float CoreRadius = 36f;
        /// <summary>黑闪爆点最大半径：伤害窗逐帧取当前可见环半径，绝不超出</summary>
        internal const float DetonationRadius = 210f;

        /// <summary>飞行帧计数（本地推进，仅表现与宽限判断用）</summary>
        private ref float FlightTimer => ref Projectile.localAI[0];
        /// <summary>已越过锚点后的累计位移 px（服务端提前引爆判据）</summary>
        private ref float PassedDist => ref Projectile.localAI[1];

        private Vector2 Anchor => new(Projectile.ai[0], Projectile.ai[1]);

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 1400;

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
            //复合加速：慢起步越飞越快（重量感=起步迟，威胁感=后段快）
            float speed = Projectile.velocity.Length();
            if (speed < MaxSpeed) {
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
            //吸积：周边星尘被拉进洞（红黑材质）
            if (Main.rand.NextBool(2)) {
                Vector2 pos = Projectile.Center + Main.rand.NextVector2Unit() * Main.rand.NextFloat(90f, 240f);
                Vector2 pull = (Projectile.Center - pos) * 0.1f + Projectile.velocity * 0.4f;
                Color c = Color.Lerp(MLordDirector.BlackFlashRed, MLordDirector.VoidBlack, Main.rand.NextFloat(0.6f));
                PRTLoader.NewParticle<PRT_HeavenfallStar>(pos, pull.RotatedBy(0.4f), c,
                    Main.rand.NextFloat(0.3f, 0.6f))?.Configure(false, Main.rand.Next(10, 16));
            }
            //缘弧迸溅
            if (Main.rand.NextBool(7)) {
                PRTLoader.NewParticle<PRT_Spark>(
                    Projectile.Center + Main.rand.NextVector2Unit() * CoreRadius * 1.3f,
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(2f, 5f),
                    MLordDirector.BlackFlashRed, Main.rand.NextFloat(0.8f, 1.2f))
                    ?.Configure(false, Main.rand.Next(8, 14));
            }
        }

        /// <summary>黑闪爆点表现：冲击帧 + 屏效 + 红黑碎星（伤害窗同帧开启）</summary>
        private void FireFlashPresentation() {
            SoundEngine.PlaySound(SoundID.Item122 with { Volume = 1.2f, Pitch = -0.45f }, Projectile.Center);
            SoundEngine.PlaySound(SoundID.Zombie104 with { Volume = 1f, Pitch = 0.1f }, Projectile.Center);
            MLordScreenFX.Punch(Projectile.Center, 13f, 18);
            MLordBlackFlashFX.PushFlash(Projectile.Center);
            //红黑碎星 + 空间裂纹
            for (int i = 0; i < 26; i++) {
                Vector2 vel = Main.rand.NextVector2Unit() * Main.rand.NextFloat(3f, 13f);
                Color c = Color.Lerp(MLordDirector.BlackFlashRed, MLordDirector.MoonWhite, Main.rand.NextFloat(0.35f));
                PRTLoader.NewParticle<PRT_HeavenfallStar>(Projectile.Center, vel, c,
                    Main.rand.NextFloat(0.6f, 1.2f))?.Configure(true, Main.rand.Next(20, 36));
            }
            for (int i = 0; i < 6; i++) {
                PRTLoader.NewParticle<PRT_SpaceFracture>(Projectile.Center,
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(2f, 6f),
                    MLordDirector.BlackFlashRed, Main.rand.NextFloat(0.9f, 1.4f))
                    ?.Configure(Main.rand.Next(18, 28), Main.rand.NextFloat(-0.05f, 0.05f));
            }
        }

        /// <summary>判定：飞行/坍缩=本体小圆；爆闪=逐帧对齐可见冲击环；余辉无判定</summary>
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (InLinger) {
                return false;
            }
            float radius = InFlash ? FlashRingRadius : CoreRadius;
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

        /// <summary>引力透镜：飞行常驻，坍缩收紧，爆闪一记扩张脉冲</summary>
        public void Warp() {
            float env;
            float size;
            if (InFlight) {
                env = MathHelper.Clamp(FlightTimer / 20f, 0f, 1f);
                size = 620f;
            }
            else if (InCollapse) {
                env = 1f;
                size = MathHelper.Lerp(620f, 340f, CollapseT);
            }
            else {
                float fade = InFlash ? 1f : 1f - Projectile.timeLeft / (float)LingerLife;
                env = 1f - fade * 0.85f;
                size = MathHelper.Lerp(340f, 1150f, VaultUtils.EaseOutCubic(FlashT));
            }
            if (env <= 0.04f) {
                return;
            }
            NeutronWarpHelper.DrawWarp(Projectile.Center, size, size, 0.38f * env, 1f, 0f, "GravitationalLens", 0.42f);
        }

        public override bool PreDraw(ref Color lightColor) {
            Vector2 pos = Projectile.Center - Main.screenPosition;
            float bodyR = InCollapse ? MathHelper.Lerp(46f, 26f, CollapseT) : 46f;
            float bodyVis = InLinger ? Projectile.timeLeft / (float)LingerLife : 1f;
            if (InFlash) {
                bodyR = MathHelper.Lerp(26f, 12f, FlashT);
            }

            DrawHoleBody(pos, bodyR, bodyVis * (1f - FlashT));
            if (FlashT > 0f) {
                DrawFlashRing(pos);
            }
            return false;
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
            //白芯闪帧（只在爆闪窗内的短脉冲）
            if (InFlash) {
                Main.EntitySpriteDraw(star, pos, null, MLordDirector.MoonWhite with { A = 0 } * (0.9f * (1f - FlashT)),
                    Main.GlobalTimeWrappedHourly * 3f, star.Size() / 2f, 0.6f + FlashT * 0.5f, SpriteEffects.None, 0);
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
