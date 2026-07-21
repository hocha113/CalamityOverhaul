using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.FishSkills
{
    /// <summary>沙蝎技能域内 shader 资源（域内加载器，不经 EffectLoader）</summary>
    internal class FishScorpioAssets
    {
        /// <summary>沙龙卷漏斗，三层异相旋带 + 噪声撕顶 + 哑光颗粒</summary>
        [VaultLoaden(CWRConstant.Effects)]
        public static Effect FishScorpioNado { get; private set; }
    }

    /// <summary>沙蝎 VFX，风载沙盘旋，失能即坠成丘</summary>
    internal static class FishScorpioVFX
    {
        //==== 沙色谱（哑光，最亮不过亮沙，全程无白）====
        /// <summary>亮沙（受光面）</summary>
        public static readonly Color SandLight = new(229, 202, 148);
        /// <summary>主沙</summary>
        public static readonly Color Sand = new(196, 165, 109);
        /// <summary>暗沙（背光/衬底）</summary>
        public static readonly Color SandDark = new(139, 109, 67);
        /// <summary>沙影（最深压底）</summary>
        public static readonly Color SandDeep = new(96, 74, 46);

        public static Color RandGrain() {
            int r = Main.rand.Next(10);
            if (r < 3) {
                return SandLight;
            }
            return r < 8 ? Sand : SandDark;
        }

        //==== 粒子族 ====

        /// <summary>定向沙粒锥，dir 为主喷方向，windLift&gt;0 时沙粒被风托着走一段再落</summary>
        public static void GrainBurst(Vector2 pos, Vector2 dir, int count, float spdMin, float spdMax, float windLift = 0f, float spread = 0.6f) {
            dir = dir.SafeNormalize(Vector2.UnitX);
            for (int i = 0; i < count; i++) {
                Vector2 vel = dir.RotatedByRandom(spread) * Main.rand.NextFloat(spdMin, spdMax);
                PRTLoader.NewParticle<PRT_FishScorpioSand>(pos + Main.rand.NextVector2Circular(4f, 4f), vel
                    , RandGrain(), Main.rand.NextFloat(0.7f, 1.15f))
                    ?.Configure(Main.rand.Next(18, 30), windLift, 0.26f, Main.rand.NextFloat(0.4f, 0.62f));
            }
        }

        /// <summary>沙尘雾团</summary>
        public static void Puff(Vector2 pos, Vector2 vel, float scale, float strength, bool dark = false) {
            PRTLoader.NewParticle<PRT_FishScorpioDust>(pos, vel, dark ? SandDark : Sand, scale)
                ?.Configure(Main.rand.Next(28, 42), strength, 0.004f, 0.012f);
        }

        /// <summary>地面土浪，出入土/龙卷落点用的成组喷发</summary>
        public static void GroundPlume(Vector2 pos, int grains, float scaleMul = 1f) {
            for (int i = 0; i < 3; i++) {
                Vector2 vel = new Vector2(Main.rand.NextFloat(-1.2f, 1.2f), Main.rand.NextFloat(-1.6f, -0.5f));
                Puff(pos + new Vector2(Main.rand.NextFloat(-12f, 12f), 0f), vel
                    , Main.rand.NextFloat(0.22f, 0.34f) * scaleMul, Main.rand.NextFloat(0.24f, 0.36f), i == 0);
            }
            GrainBurst(pos, -Vector2.UnitY, grains, 1.5f, 4.5f, 0.35f, 0.9f);
            //少量原版沙尘做廉价底噪
            for (int i = 0; i < grains / 2; i++) {
                Dust d = Dust.NewDustPerfect(pos + Main.rand.NextVector2Circular(10f, 4f)
                    , DustID.Sand, new Vector2(Main.rand.NextFloat(-1.5f, 1.5f), Main.rand.NextFloat(-3f, -1f))
                    , 120, Sand, Main.rand.NextFloat(0.9f, 1.4f));
                d.noGravity = false;
            }
        }

        /// <summary>短命沙丘 decal，groundPos 应已贴地</summary>
        public static void Mound(Vector2 groundPos, float widthPx, int lifetime = 55) {
            PRTLoader.NewParticle<PRT_FishScorpioMound>(groundPos, Vector2.Zero, Sand, 1f)
                ?.Configure(lifetime, widthPx);
        }

        /// <summary>从 pos 向下找地表，找不到返回 null</summary>
        public static Vector2? FindGroundBelow(Vector2 pos, int maxTiles = 10) {
            Point tile = pos.ToTileCoordinates();
            for (int y = 0; y < maxTiles; y++) {
                if (!WorldGen.InWorld(tile.X, tile.Y + y)) {
                    break;
                }
                if (Main.tile[tile.X, tile.Y + y].HasSolidTile()) {
                    return new Vector2(pos.X, (tile.Y + y) * 16f);
                }
            }
            return null;
        }

        //==== 龙卷漏斗绘制 ====

        /// <summary>
        /// 画一根沙龙卷漏斗（shader quad）
        /// power 风力 0..1（衰减时旋带失能），grow 出生包络 0..1，fade 整体透明度
        /// 内部自带 End/Begin，调用处于常规实体批次时使用；shader 缺失时退化为哑光雾柱
        /// </summary>
        public static void DrawNado(SpriteBatch sb, Vector2 center, float widthPx, float heightPx, float seed, float power, float grow, float fade) {
            if (fade <= 0.01f || grow <= 0.01f) {
                return;
            }

            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            Effect fx = FishScorpioAssets.FishScorpioNado;
            if (fx == null || noise == null) {
                DrawNadoFallback(sb, center, widthPx, heightPx, power, grow, fade);
                return;
            }

            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearWrap
                , DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            fx.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            fx.Parameters["uSeed"]?.SetValue(seed);
            fx.Parameters["uPower"]?.SetValue(power);
            fx.Parameters["uGrow"]?.SetValue(grow);
            fx.Parameters["uFade"]?.SetValue(fade);
            fx.CurrentTechnique.Passes[0].Apply();

            Vector2 topLeft = center - Main.screenPosition - new Vector2(widthPx * 0.5f, heightPx * 0.5f);
            sb.Draw(noise, new Rectangle((int)topLeft.X, (int)topLeft.Y, (int)widthPx, (int)heightPx), Color.White);

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None
                , RasterizerState.CullCounterClockwise, null, Main.GameViewMatrix.TransformationMatrix);
        }

        /// <summary>.fxc 缺失时的 CPU 退化，哑光雾团摞成粗柱，保证技能不隐形</summary>
        private static void DrawNadoFallback(SpriteBatch sb, Vector2 center, float widthPx, float heightPx, float power, float grow, float fade) {
            Texture2D fog = CWRAsset.Fog?.Value;
            if (fog == null) {
                return;
            }
            float t = Main.GlobalTimeWrappedHourly;
            Vector2 origin = fog.Size() * 0.5f;
            for (int i = 0; i < 4; i++) {
                float v = i / 3f;
                float w = MathHelper.Lerp(widthPx * 0.45f, widthPx, v) * grow;
                Vector2 pos = center - Main.screenPosition + new Vector2(MathF.Sin(t * 3f + i * 1.7f) * 4f
                    , MathHelper.Lerp(heightPx * 0.38f, -heightPx * 0.38f, v));
                Color col = Color.Lerp(SandDark, Sand, v) * (fade * 0.38f * (0.5f + 0.5f * power));
                sb.Draw(fog, pos, null, col, t * (i % 2 == 0 ? 0.8f : -0.6f), origin, w / fog.Width, SpriteEffects.None, 0f);
            }
        }

        //==== 声音分层 ====

        /// <summary>出入土的沙层翻涌，双层 Dig 错开音高</summary>
        public static void BurrowSound(Vector2 pos, float pitch = 0f) {
            SoundEngine.PlaySound(SoundID.Dig with { Volume = 0.6f, Pitch = 0.2f + pitch, MaxInstances = 3 }, pos);
            SoundEngine.PlaySound(SoundID.Dig with { Volume = 0.4f, Pitch = -0.45f + pitch, MaxInstances = 3 }, pos);
        }
    }

    /// <summary>
    /// 移动沙龙卷，蝎尾聚旋成形后飞向目标
    /// 出膛过冲减速到巡航、蛇摆航线；末端风力衰减掉沙，死后留沙幕与沙丘
    /// </summary>
    internal class FishScorpioSandnado : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        /// <summary>可视漏斗高度</summary>
        private const float VisualH = 164f;
        /// <summary>可视漏斗宽度</summary>
        private const float VisualW = 102f;
        /// <summary>末端风力衰减帧数</summary>
        private const int WindDownFrames = 30;

        private ref float Time => ref Projectile.localAI[0];
        /// <summary>出膛时的巡航速度下限，ai[0] 由蝎子在生成时写入初速大小</summary>
        private float CruiseSpeed => Projectile.ai[0] * 0.55f;
        private float Seed => Projectile.identity * 0.7331f % 10f;

        public override void SetDefaults() {
            Projectile.width = 46;
            Projectile.height = 92;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 150;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 12;
            Projectile.DamageType = DamageClass.Summon;
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            if (target.IsWormBody()) {
                modifiers.FinalDamage *= 0.5f;
            }
            if (target.type == CWRID.NPC_DevourerofGodsHead || target.type == CWRID.NPC_DevourerofGodsTail) {
                modifiers.FinalDamage *= 2f;
            }
        }

        /// <summary>风力包络，出生斜坡上来，末端衰减到 0</summary>
        private float WindPower() {
            float up = MathHelper.Clamp(Time / 20f, 0f, 1f) * 0.4f + 0.6f;
            float down = MathHelper.Clamp(Projectile.timeLeft / (float)WindDownFrames, 0f, 1f);
            return up * down;
        }

        /// <summary>出生包络，easeOutBack 过冲成形</summary>
        private float GrowEnvelope() {
            float x = MathHelper.Clamp(Time / 10f, 0f, 1f);
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            float xm = x - 1f;
            return 1f + c3 * xm * xm * xm + c1 * xm * xm;
        }

        public override void AI() {
            Time++;

            //蛇摆，航线沿风场缓慢弯曲
            Projectile.velocity = Projectile.velocity.RotatedBy(MathF.Sin(Time * 0.11f + Seed) * 0.01f);

            //出膛过冲衰减到巡航
            float speed = Projectile.velocity.Length();
            if (speed > CruiseSpeed && CruiseSpeed > 0.1f) {
                Projectile.velocity *= 0.968f;
            }
            //末端风力耗尽，整体失速
            if (Projectile.timeLeft < WindDownFrames) {
                Projectile.velocity *= 0.94f;
            }

            if (Main.dedServ) {
                return;
            }

            float power = WindPower();
            Vector2 basePos = Projectile.Center + new Vector2(0f, VisualH * 0.36f);

            //卷入碎屑
            if (Main.rand.NextBool(2)) {
                Vector2 pos = basePos + new Vector2(Main.rand.NextFloat(-VisualW * 0.24f, VisualW * 0.24f), Main.rand.NextFloat(-8f, 4f));
                Vector2 vel = new Vector2(Main.rand.NextFloat(-1.6f, 1.6f), Main.rand.NextFloat(-1.4f, -0.4f)) + Projectile.velocity * 0.3f;
                PRTLoader.NewParticle<PRT_FishScorpioSand>(pos, vel, FishScorpioVFX.RandGrain(), Main.rand.NextFloat(0.65f, 1.05f))
                    ?.Configure(Main.rand.Next(24, 38), 1f * power, 0.26f, 0.75f);
            }
            //底裙尾迹尘
            if (Time % 6 == 0) {
                FishScorpioVFX.Puff(basePos + Main.rand.NextVector2Circular(8f, 3f)
                    , new Vector2(-Projectile.velocity.X * 0.1f, -0.2f), 0.2f, 0.16f, true);
            }
            //末端失能，沙粒失去升力成幕脱落
            if (Projectile.timeLeft < WindDownFrames && Main.rand.NextBool(2)) {
                Vector2 pos = Projectile.Center + new Vector2(Main.rand.NextFloat(-VisualW * 0.3f, VisualW * 0.3f)
                    , Main.rand.NextFloat(-VisualH * 0.4f, VisualH * 0.3f));
                PRTLoader.NewParticle<PRT_FishScorpioSand>(pos, Projectile.velocity * 0.25f, FishScorpioVFX.RandGrain()
                    , Main.rand.NextFloat(0.7f, 1.1f))?.Configure(Main.rand.Next(20, 32), 0f);
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (Main.dedServ) {
                return;
            }
            //命中沙爆
            Vector2 dir = Projectile.velocity.SafeNormalize(Vector2.UnitX);
            FishScorpioVFX.GrainBurst(target.Center - dir * target.width * 0.25f, dir, 9, 2.5f, 7f, 0.3f, 0.5f);
            FishScorpioVFX.Puff(target.Center, dir * 1.2f, 0.24f, 0.28f, true);
            SoundEngine.PlaySound(SoundID.Dig with { Volume = 0.35f, Pitch = 0.4f, MaxInstances = 3 }, target.Center);
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            //沙幕，整根柱体的沙同时失去动力
            for (int i = 0; i < 16; i++) {
                Vector2 pos = Projectile.Center + new Vector2(Main.rand.NextFloat(-VisualW * 0.32f, VisualW * 0.32f)
                    , Main.rand.NextFloat(-VisualH * 0.42f, VisualH * 0.38f));
                Vector2 vel = new Vector2(Main.rand.NextFloat(-0.8f, 0.8f), Main.rand.NextFloat(-0.5f, 1.2f));
                PRTLoader.NewParticle<PRT_FishScorpioSand>(pos, vel, FishScorpioVFX.RandGrain(), Main.rand.NextFloat(0.7f, 1.15f))
                    ?.Configure(Main.rand.Next(22, 34), 0f);
            }
            for (int i = 0; i < 2; i++) {
                FishScorpioVFX.Puff(Projectile.Center + new Vector2(0f, VisualH * 0.25f * i)
                    , new Vector2(0f, 0.4f), 0.3f, 0.22f, i == 0);
            }
            //落沙堆丘，脚下找得到地面才留丘
            Vector2? ground = FishScorpioVFX.FindGroundBelow(Projectile.Center + new Vector2(0f, VisualH * 0.3f));
            if (ground != null) {
                FishScorpioVFX.Mound(ground.Value, 64f, 60);
            }
            SoundEngine.PlaySound(SoundID.Dig with { Volume = 0.4f, Pitch = -0.2f, MaxInstances = 3 }, Projectile.Center);
        }

        public override bool PreDraw(ref Color lightColor) {
            float fade = MathHelper.Clamp(Time / 8f, 0f, 1f);
            //末端随风力衰减变淡，但沙幕 PRT 会接住视觉质量
            fade *= MathHelper.Clamp(Projectile.timeLeft / (WindDownFrames * 0.6f), 0f, 1f);

            //地面接触影
            Texture2D fog = CWRAsset.Fog?.Value;
            Vector2? ground = FishScorpioVFX.FindGroundBelow(Projectile.Center + new Vector2(0f, VisualH * 0.3f), 6);
            if (fog != null && ground != null) {
                Vector2 pos = ground.Value - Main.screenPosition;
                Main.spriteBatch.Draw(fog, pos, null, FishScorpioVFX.SandDeep * (0.3f * fade), 0f
                    , fog.Size() * 0.5f, new Vector2(0.42f, 0.1f), SpriteEffects.None, 0f);
            }

            FishScorpioVFX.DrawNado(Main.spriteBatch, Projectile.Center, VisualW, VisualH
                , Seed, WindPower(), GrowEnvelope(), fade);
            return false;
        }
    }
}
