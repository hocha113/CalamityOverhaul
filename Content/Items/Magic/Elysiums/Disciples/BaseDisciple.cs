using CalamityOverhaul.Common;
using CalamityOverhaul.Content.PRTTypes;
using CalamityOverhaul.Content.UIs.UIEffect;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Magic.Elysiums.Disciples
{
    /// <summary>
    /// 门徒基类：圣像幽灵环绕主人(伪3D轨道)，冷却好时施展席位能力，
    /// 殉道时通体燃金化光尘归于主人。
    /// ai[1]=状态通道(0常态 1殉道)，主人端写入随弹幕同步
    /// </summary>
    internal abstract class BaseDisciple : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        /// <summary>席位索引0~11，即身份</summary>
        public abstract int Seat { get; }

        protected DiscipleDef Def => DiscipleCatalog.Get(Seat);
        protected Player Owner => Main.player[Projectile.owner];

        private ref float StateFlag => ref Projectile.ai[1];

        private const int EmergeTime = 26;
        private const int MartyrTime = 32;

        //轨道
        private float orbitAngle;
        private float breathePhase;
        private Vector2 velocitySmooth;
        private float currentScale = 1f;
        private float pseudoDepth;

        //演出
        private int emergeTimer;
        private int martyrTimer = -1;
        protected float haloFlare;
        protected int abilityCooldown;
        private int timer;

        /// <summary>正在殉道化光</summary>
        public bool IsMartyring => martyrTimer >= 0;

        private float EmergeProgress => Math.Min(emergeTimer / (float)EmergeTime, 1f);
        private float DissolveProgress => martyrTimer < 0 ? 0f : Math.Min(martyrTimer / (float)MartyrTime, 1f);

        public override void SetDefaults() {
            Projectile.width = 40;
            Projectile.height = 40;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 18000;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.netImportant = true;
            abilityCooldown = 60;
        }

        public override bool? CanDamage() => false;

        public override void AI() {
            if (!Owner.active || Owner.dead) {
                Projectile.Kill();
                return;
            }

            if (!Owner.TryGetModPlayer(out ElysiumPlayer ep)) {
                Projectile.Kill();
                return;
            }

            timer++;
            breathePhase += 0.03f;
            Projectile.timeLeft = 120;
            haloFlare = Math.Max(0f, haloFlare - 0.05f);

            //殉道状态经ai[1]同步：远端看到标记即进入演出
            if (StateFlag == 1f && martyrTimer < 0) {
                martyrTimer = 0;
                SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.9f, Pitch = -0.25f }, Projectile.Center);
                SoundEngine.PlaySound(SoundID.Item4 with { Volume = 0.7f, Pitch = 0.1f }, Projectile.Center);
            }

            if (martyrTimer >= 0) {
                MartyrBehavior();
                return;
            }

            ep.RegisterSeat(Seat, Projectile.whoAmI);

            //主人失去武器：门徒静默化光(不产生殉道之力)
            if (Projectile.IsOwnedByLocalPlayer() && !ep.HasElysiumInInventory()) {
                Projectile.Kill();
                return;
            }

            if (emergeTimer < EmergeTime) {
                emergeTimer++;
                if (emergeTimer == 1) {
                    SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.6f, Pitch = 0.3f }, Projectile.Center);
                }
            }

            UpdateOrbit(ep);

            //能力节拍：出场完成后计冷却，子类判定可否施放
            if (emergeTimer >= EmergeTime) {
                if (abilityCooldown > 0) {
                    abilityCooldown--;
                }
                else if (TryCast()) {
                    ExecuteAbility();
                    abilityCooldown = Def.AbilityCooldown;
                    haloFlare = 1f;
                }
                PassiveTick();
            }

            //身份色微光
            Color bodyColor = Def.BodyColor;
            float lightMul = 0.4f * EmergeProgress;
            Lighting.AddLight(Projectile.Center, bodyColor.R / 255f * lightMul
                , bodyColor.G / 255f * lightMul, bodyColor.B / 255f * lightMul);
        }

        #region 行为钩子
        /// <summary>能力是否可施放(目标校验等，各端一致的纯查询)</summary>
        protected virtual bool TryCast() => false;

        /// <summary>施展席位能力(内部自行做主人端门控)</summary>
        protected virtual void ExecuteAbility() { }

        /// <summary>每帧被动(出场完成后)</summary>
        protected virtual void PassiveTick() { }

        /// <summary>殉道化光启动(主人端调用，状态经ai同步)</summary>
        public void BeginMartyrdom() {
            if (martyrTimer >= 0) {
                return;
            }
            StateFlag = 1f;
            Projectile.netUpdate = true;
        }
        #endregion

        #region 殉道演出
        private void MartyrBehavior() {
            martyrTimer++;
            haloFlare = 1f;
            Projectile.velocity *= 0.9f;

            //化作光尘流向主人
            if (!Main.dedServ && martyrTimer % 2 == 0) {
                Vector2 spawnPos = Projectile.Center + Main.rand.NextVector2Circular(24f, 34f);
                Vector2 vel = (Owner.Center - spawnPos).SafeNormalize(Vector2.UnitY) * Main.rand.NextFloat(4f, 8f);
                PRTLoader.NewParticle<PRT_Light>(spawnPos, vel, Def.AccentColor, Main.rand.NextFloat(0.24f, 0.42f))
                    ?.Configure(Main.rand.Next(20, 32), 0.95f, _entity: Owner, _followingRateRatio: 0.6f);
            }

            float glow = 0.8f * (1f - DissolveProgress);
            Lighting.AddLight(Projectile.Center, glow, glow * 0.9f, glow * 0.6f);

            if (martyrTimer >= MartyrTime) {
                if (!Main.dedServ) {
                    for (int i = 0; i < 8; i++) {
                        Vector2 vel = (Owner.Center - Projectile.Center).SafeNormalize(Vector2.UnitY)
                            .RotatedBy(Main.rand.NextFloat(-0.5f, 0.5f)) * Main.rand.NextFloat(5f, 9f);
                        PRTLoader.NewParticle<PRT_HeavenfallStar>(Projectile.Center, vel
                            , Def.AccentColor, Main.rand.NextFloat(0.6f, 1f))?.Configure(false, Main.rand.Next(14, 22));
                    }
                }
                Projectile.Kill();
            }
        }
        #endregion

        #region 轨道
        /// <summary>伪3D环绕：倾斜椭圆轨道，深度驱动缩放，同伴间避让</summary>
        private void UpdateOrbit(ElysiumPlayer ep) {
            int aliveCount = Math.Max(1, ep.AliveDiscipleCount);
            int order = ep.GetSeatOrder(Seat);

            float goldenAngle = MathHelper.Pi * (3f - MathF.Sqrt(5f));
            float baseOffset = goldenAngle * order;
            orbitAngle += 0.028f * Def.OrbitSpeedMul;
            float currentAngle = orbitAngle + baseOffset + Seat * 0.37f;

            float baseRadius = 108f + aliveCount * 9f;

            //3D坐标：倾斜椭圆 + 高度层
            float tilt = Seat * 0.14f + MathF.Sin(breathePhase * 0.3f) * 0.05f;
            float tiltDir = Seat * MathHelper.TwoPi / DiscipleCatalog.SeatCount;
            float x3 = MathF.Cos(currentAngle) * baseRadius;
            float y3 = MathF.Sin(currentAngle) * baseRadius;
            float z3 = MathF.Sin(currentAngle + tiltDir) * baseRadius * MathF.Sin(tilt)
                + (Seat % 3 - 1) * 22f
                + MathF.Sin(breathePhase * 0.6f + Seat) * 7f;

            Vector2 projected = Owner.Center + new Vector2(x3, y3 * 0.5f - z3 * 0.35f) + new Vector2(0f, -46f);

            //深度只改缩放不改透明度
            pseudoDepth = z3 / baseRadius;
            float targetScale = MathHelper.Clamp(0.68f + pseudoDepth * 0.32f, 0.55f, 1.12f);
            currentScale = MathHelper.Lerp(currentScale, targetScale, 0.12f);

            //呼吸半径微差
            Vector2 breathe = currentAngle.ToRotationVector2() * MathF.Sin(breathePhase + Seat * 0.5f) * 6f;

            //同伴避让
            Vector2 avoidance = Vector2.Zero;
            for (int i = 0; i < ElysiumPlayer.SeatCount; i++) {
                if (i == Seat || !ep.TryGetDisciple(i, out BaseDisciple other)) {
                    continue;
                }
                float dist = Vector2.Distance(Projectile.Center, other.Projectile.Center);
                if (dist is < 42f and > 1f) {
                    avoidance += (Projectile.Center - other.Projectile.Center).SafeNormalize(Vector2.UnitX)
                        * (42f - dist) * 0.5f;
                }
            }

            Vector2 targetPos = projected + breathe + avoidance;
            Vector2 toTarget = targetPos - Projectile.Center + Owner.velocity * 0.2f;
            velocitySmooth = Vector2.Lerp(velocitySmooth, toTarget * 0.4f, 0.18f);
            Projectile.Center += velocitySmooth;

            //离目标过远直接拉回(传送/坠落追身)
            if (Vector2.DistanceSquared(Projectile.Center, targetPos) > 900f * 900f) {
                Projectile.Center = targetPos;
            }
        }
        #endregion

        #region 绘制
        public override bool PreDraw(ref Color lightColor) {
            Effect effect = EffectLoader.DiscipleForm?.Value;
            Texture2D canvas = VaultAsset.placeholder2?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (effect == null || canvas == null || noise == null) {
                return false;
            }

            float quadSize = 150f * Def.SizeMul * currentScale;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            //袍摆拖曳量取平滑速度(UV空间,限幅)
            Vector2 motion = new(MathHelper.Clamp(-velocitySmooth.X * 0.02f, -0.08f, 0.08f), 0f);

            effect.CurrentTechnique = effect.Techniques["DiscipleForm"];
            effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            effect.Parameters["fadeAlpha"]?.SetValue(1f);
            effect.Parameters["uSeed"]?.SetValue(Seat * 2.399f);
            effect.Parameters["bodyColor"]?.SetValue(Def.BodyColor.ToVector3());
            effect.Parameters["accentColor"]?.SetValue(Def.AccentColor.ToVector3());
            effect.Parameters["uHaloFlare"]?.SetValue(haloFlare);
            effect.Parameters["uMotion"]?.SetValue(motion);
            effect.Parameters["uDissolve"]?.SetValue(DissolveProgress);
            effect.Parameters["uEmerge"]?.SetValue(EmergeProgress);

            Main.graphics.GraphicsDevice.Textures[1] = noise;
            Main.graphics.GraphicsDevice.SamplerStates[1] = SamplerState.LinearWrap;

            SpriteBatch sb = Main.spriteBatch;
            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend,
                SamplerState.LinearClamp, DepthStencilState.None,
                RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
            effect.CurrentTechnique.Passes[0].Apply();
            sb.Draw(canvas, drawPos, null, Color.White, 0f, canvas.Size() * 0.5f, quadSize, SpriteEffects.None, 0f);
            sb.End();

            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                Main.DefaultSamplerState, DepthStencilState.None,
                Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);

            DrawEmblem(sb, drawPos, quadSize);
            ExtraDraw(sb, drawPos);
            return false;
        }

        /// <summary>胸前圣徽：SVG线稿，施放时燃亮</summary>
        private void DrawEmblem(SpriteBatch sb, Vector2 drawPos, float quadSize) {
            SvgPath path = SvgPathPen.Path(Def.EmblemPath);
            if (path == null) {
                return;
            }
            float visibility = EmergeProgress * (1f - DissolveProgress);
            if (visibility < 0.05f) {
                return;
            }
            //胸位在画布UV(0.5,0.485)
            Vector2 chestPos = drawPos + new Vector2(0f, quadSize * -0.015f);
            float glyphScale = quadSize * 0.052f;
            float alpha = (0.55f + haloFlare * 0.45f) * visibility;
            Color glow = Color.Lerp(Def.AccentColor, Color.White, haloFlare * 0.5f);
            SvgPathPen.Stroke(sb, path, chestPos, glyphScale, 0f, glow with { A = 0 } * alpha, 1.3f, alpha
                , core: Color.White with { A = 0 } * (alpha * 0.6f));
        }

        /// <summary>子类附加绘制(圣盾环等)，此时处于常规AlphaBlend批</summary>
        protected virtual void ExtraDraw(SpriteBatch sb, Vector2 drawPos) { }
        #endregion
    }
}
