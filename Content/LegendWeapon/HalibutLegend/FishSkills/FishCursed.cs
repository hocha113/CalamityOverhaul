using CalamityOverhaul.Common;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.FishSkills
{
    /// <summary>诅咒火鱼技能，周期喷射诅咒火焰</summary>
    internal class FishCursed : FishSkill
    {
        public override int UnlockFishID => ItemID.Cursedfish;
        public override int DefaultCooldown => 180 - HalibutData.GetDomainLayer() * 12;
        public override int ResearchDuration => 60 * 16;
        //火焰喷射计数器
        private int flameCounter = 0;
        private static int FlameInterval => 18 - HalibutData.GetDomainLayer();

        public override bool? Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source,
            Vector2 position, Vector2 velocity, int type, int damage, float knockback) {

            flameCounter++;

            //周期性释放诅咒火焰
            if (flameCounter >= FlameInterval && Cooldown <= 0) {
                flameCounter = 0;
                SetCooldown();

                //发射诅咒火焰
                Vector2 shootDir = velocity.SafeNormalize(Vector2.Zero);
                float spreadBase = 0.25f;

                //根据领域层数增加火焰数量和扩散
                int flameCount = 2 + HalibutData.GetDomainLayer() / 2;

                for (int i = 0; i < flameCount; i++) {
                    float spreadAngle = MathHelper.Lerp(-spreadBase, spreadBase, i / (float)Math.Max(1, flameCount - 1));
                    Vector2 flameVelocity = shootDir.RotatedBy(spreadAngle) * Main.rand.NextFloat(16f, 22f);

                    Projectile.NewProjectile(
                        source,
                        position,
                        flameVelocity,
                        ModContent.ProjectileType<CursedFlameStream>(),
                        (int)(damage * (2.8f + HalibutData.GetDomainLayer() * 0.7f)),
                        knockback * 2f,
                        player.whoAmI
                    );
                }

                //火焰喷射音效
                SoundEngine.PlaySound(SoundID.Item34 with {
                    Volume = 0.7f,
                    Pitch = -0.4f
                }, position);

                SoundEngine.PlaySound(SoundID.DD2_BetsyFlameBreath with {
                    Volume = 0.5f,
                    Pitch = -0.3f
                }, position);

                //喷射口火焰爆发效果
                SpawnMuzzleFlare(position, shootDir);
            }

            return null;
        }

        //喷射口演出
        private static void SpawnMuzzleFlare(Vector2 position, Vector2 direction) {
            FishCursedVFX.Punch(position, direction, 3f, 9f, 8, 600f);
            if (Main.dedServ) {
                return;
            }
            //炬闪
            for (int i = 0; i < 2; i++) {
                Vector2 vel = direction.RotatedByRandom(0.16f) * Main.rand.NextFloat(5.5f, 8f);
                Color col = Color.Lerp(FishCursedVFX.GreenCore, FishCursedVFX.GreenMid, Main.rand.NextFloat(0.5f));
                PRTLoader.NewParticle<PRT_FishCursedTongue>(position + direction * 10f, vel, col, Main.rand.NextFloat(0.3f, 0.42f))
                    ?.Configure(Main.rand.Next(9, 13), -1.2f, 0.25f);
            }
            //火星锥
            for (int i = 0; i < 5; i++) {
                Vector2 vel = direction.RotatedByRandom(0.42f) * Main.rand.NextFloat(3.5f, 9f);
                Color col = Color.Lerp(FishCursedVFX.GreenMid, FishCursedVFX.GreenDeep, Main.rand.NextFloat(0.7f));
                PRTLoader.NewParticle<PRT_Spark>(position, vel, col, Main.rand.NextFloat(0.4f, 0.7f))?.Configure(false, 12);
            }
            //原版诅咒尘填充（量收紧）
            for (int i = 0; i < 5; i++) {
                Dust flame = Dust.NewDustPerfect(position, DustID.CursedTorch
                    , direction.RotatedByRandom(0.5f) * Main.rand.NextFloat(4f, 9f), 100, default, Main.rand.NextFloat(1.2f, 1.9f));
                flame.noGravity = true;
                flame.fadeIn = 1.2f;
            }
            //墨绿烟压底（AlphaBlend 真遮挡）
            for (int i = 0; i < 2; i++) {
                PRTLoader.NewParticle<PRT_FishCursedSmog>(position, direction.RotatedByRandom(0.6f) * Main.rand.NextFloat(1.5f, 3f)
                    , FishCursedVFX.SmokeDark, Main.rand.NextFloat(0.24f, 0.32f))?.Configure(24, 0.4f, 0.02f);
            }
        }
    }

    /// <summary>全局钩子，为 Halibut 攻击附加诅咒火 debuff</summary>
    internal class FishCursedGlobalProj : GlobalProjectile
    {
        public override void OnHitNPC(Projectile projectile, NPC target, NPC.HitInfo hit, int damageDone) {
            if (projectile.owner.TryGetPlayer(out var player)
                && FishSkill.GetT<FishCursed>().Active(player)) {
                //在这个技能下攻击会附加诅咒火焰效果
                int buffDuration = 240 + HalibutData.GetDomainLayer() * 30;
                target.AddBuff(BuffID.CursedInferno, buffDuration);

                //诅咒火焰感染粒子效果
                SpawnCursedInfectionEffect(target);
            }
        }

        //诅咒感染
        private static void SpawnCursedInfectionEffect(NPC target) {
            if (Main.dedServ) {
                return;
            }
            Vector2 pos = target.Center + Main.rand.NextVector2Circular(target.width * 0.4f, target.height * 0.4f);
            PRTLoader.NewParticle<PRT_FishCursedTongue>(pos, new Vector2(0f, -1f)
                , Color.Lerp(FishCursedVFX.GreenMid, FishCursedVFX.GreenDeep, Main.rand.NextFloat(0.5f))
                , Main.rand.NextFloat(0.22f, 0.32f))?.Configure(Main.rand.Next(16, 24), -1.5f, 0.5f);
            for (int i = 0; i < 3; i++) {
                PRTLoader.NewParticle<PRT_FishCursedEmber>(pos + Main.rand.NextVector2Circular(10f, 10f)
                    , Main.rand.NextVector2Circular(1.2f, 1.2f)
                    , Color.Lerp(FishCursedVFX.GreenMid, FishCursedVFX.GreenDeep, Main.rand.NextFloat())
                    , Main.rand.NextFloat(0.32f, 0.5f))?.Configure(Main.rand.Next(18, 28));
            }
            for (int i = 0; i < 2; i++) {
                Dust d = Dust.NewDustPerfect(target.Center + Main.rand.NextVector2Circular(16f, 16f), DustID.CursedTorch
                    , Main.rand.NextVector2Circular(2.5f, 2.5f), 120, default, Main.rand.NextFloat(1f, 1.5f));
                d.noGravity = true;
            }
        }
    }

    /// <summary>诅咒火焰流弹幕</summary>
    internal class CursedFlameStream : ModProjectile, IPrimitiveDrawable
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        //火焰状态
        private enum FlameState
        {
            Launch,     //射出减速
            Hover,      //悬停颤动
            Rise,       //加速上浮
            Fading      //消散
        }

        private FlameState State {
            get => (FlameState)Projectile.ai[0];
            set => Projectile.ai[0] = (float)value;
        }

        private ref float StateTime => ref Projectile.ai[1];
        //视觉种子
        private ref float VisualSeed => ref Projectile.localAI[0];

        private float visualRot;    //平滑体朝向，低速时立正向上
        private float riseAccel;    //上浮加速度，复利滚增
        private float driftX;       //上浮期残余前向动量
        private bool impacted;      //已撞击/末次咬肉，燃尽演出不再重复
        private int age;            //总帧龄，燃烧噼啪循环用

        private const float LaunchDrag = 0.965f;        //射出段阻尼/帧（16~22 初速约 45~52 帧滑到悬停，前程约 450px）
        private const float LaunchEndSpeed = 3.2f;      //滑到该速度进入悬停
        private const int LaunchMaxFrames = 52;
        private const int HoverFrames = 14;             //悬停颤动时长（点燃呼吸拍）
        private const float RiseAccelBase = 0.16f;      //上浮初始加速度
        private const float RiseAccelGrowth = 1.045f;   //加速度每帧复利
        private const float RiseAccelMax = 0.55f;
        private const float RiseMaxSpeed = 9.6f;        //上浮速度上限
        private const int RiseFrames = 72;              //上浮时长，此后燃尽

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 16;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 40;
            Projectile.height = 40;
            Projectile.friendly = true;
            Projectile.penetrate = 4;
            Projectile.timeLeft = 200;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = false;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 12;
        }

        public override void AI() {
            if (VisualSeed == 0f) {
                VisualSeed = 1f + Projectile.identity % 100 * 0.917f;
                visualRot = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
            }
            StateTime++;

            switch (State) {
                case FlameState.Launch:
                    LaunchAI();
                    break;
                case FlameState.Hover:
                    HoverAI();
                    break;
                case FlameState.Rise:
                    RiseAI();
                    break;
                default:
                    FadingAI();
                    break;
            }

            //可视朝向
            float speed = Projectile.velocity.Length();
            float targetRot = speed > 1.6f ? Projectile.velocity.ToRotation() + MathHelper.PiOver2 : 0f;
            visualRot = Utils.AngleLerp(visualRot, targetRot, 0.16f);

            if (!Main.dedServ && State != FlameState.Fading) {
                ShedParticles(speed);
            }

            //持续燃烧噼啪（whoAmI 错相防齐射同帧齐响）
            age++;
            if (State != FlameState.Fading && (age + Projectile.whoAmI * 7) % 25 == 0) {
                SoundEngine.PlaySound(SoundID.Item74 with { Volume = 0.24f, Pitch = -0.5f, MaxInstances = 4 }, Projectile.Center);
            }

            //暗绿照明，随消散衰减
            float env = 1f - Projectile.alpha / 255f;
            Lighting.AddLight(Projectile.Center, 0.10f * env, 0.32f * env, 0.12f * env);
        }

        //射出减速
        private void LaunchAI() {
            Projectile.velocity *= LaunchDrag;
            Projectile.velocity += new Vector2(
                MathF.Sin(StateTime * 0.7f + VisualSeed * 3.1f),
                MathF.Cos(StateTime * 0.9f + VisualSeed * 1.7f)) * 0.05f;

            if (Projectile.velocity.Length() <= LaunchEndSpeed || StateTime >= LaunchMaxFrames) {
                State = FlameState.Hover;
                StateTime = 0;
                //残余前向动量在此定格，供上浮期斜飘
                driftX = MathHelper.Clamp(Projectile.velocity.X, -3.2f, 3.2f);
                //点燃拍
                SoundEngine.PlaySound(SoundID.Item20 with { Volume = 0.3f, Pitch = 0.25f, MaxInstances = 3 }, Projectile.Center);
            }
        }

        //悬停颤动
        private void HoverAI() {
            Projectile.velocity *= 0.8f;
            Projectile.velocity += new Vector2(
                MathF.Sin(StateTime * 1.3f + VisualSeed * 5.7f) * 0.09f,
                MathF.Sin(StateTime * 0.55f + VisualSeed * 2.3f) * 0.13f);

            if (StateTime >= HoverFrames) {
                State = FlameState.Rise;
                StateTime = 0;
                riseAccel = RiseAccelBase;
                //吐息，上浮启动的软噗
                SoundEngine.PlaySound(SoundID.DD2_FlameburstTowerShot with { Volume = 0.24f, Pitch = 0.45f, MaxInstances = 2 }, Projectile.Center);
            }
        }

        //复利上浮
        private void RiseAI() {
            riseAccel = MathF.Min(riseAccel * RiseAccelGrowth, RiseAccelMax);
            Projectile.velocity.Y = MathF.Max(Projectile.velocity.Y - riseAccel, -RiseMaxSpeed);
            //残余前向动量缓慢衰减，摇曳叠加其上
            driftX *= 0.988f;
            Projectile.velocity.X = MathHelper.Lerp(Projectile.velocity.X
                , driftX + MathF.Sin(StateTime * 0.11f + VisualSeed * 5.3f) * 1.4f, 0.06f);

            if (StateTime >= RiseFrames) {
                EnterFade();
            }
        }

        //消散 tick
        private void FadingAI() {
            Projectile.alpha += 14;
            if (Projectile.alpha >= 255) {
                Projectile.Kill();
            }
            Projectile.velocity *= 0.9f;
        }

        private void EnterFade() {
            if (State == FlameState.Fading) {
                return;
            }
            State = FlameState.Fading;
            StateTime = 0;
            Projectile.timeLeft = 40;
            //撞击已放过 ImpactBurst，燃尽演出只属于寿终正寝
            if (!impacted) {
                GutterOut();
            }
        }

        //空中燃尽
        private void GutterOut() {
            if (Main.dedServ) {
                return;
            }
            FishCursedVFX.TongueBurst(Projectile.Center, 3, 0.32f, 1.8f);
            for (int i = 0; i < 4; i++) {
                PRTLoader.NewParticle<PRT_FishCursedEmber>(Projectile.Center + Main.rand.NextVector2Circular(10f, 10f)
                    , new Vector2(Main.rand.NextFloat(-0.8f, 0.8f), -Main.rand.NextFloat(0.5f, 1.4f))
                    , Color.Lerp(FishCursedVFX.GreenMid, FishCursedVFX.GreenDeep, Main.rand.NextFloat())
                    , Main.rand.NextFloat(0.34f, 0.52f))?.Configure(Main.rand.Next(26, 40));
            }
            for (int i = 0; i < 2; i++) {
                PRTLoader.NewParticle<PRT_FishCursedSmog>(Projectile.Center, new Vector2(0f, -Main.rand.NextFloat(0.4f, 1f))
                    , FishCursedVFX.SmokeDark, Main.rand.NextFloat(0.22f, 0.3f))?.Configure(30, 0.4f, 0.015f);
            }
        }

        //焰体剥落
        private void ShedParticles(float speed) {
            int phase = (int)StateTime + Projectile.whoAmI * 3;

            int tongueCadence = State switch {
                FlameState.Hover => 3,
                FlameState.Rise => (int)MathHelper.Clamp(5f - speed * 0.35f, 2f, 5f), //甩出率∝速度
                _ => 5,
            };
            if (phase % tongueCadence == 0) {
                Vector2 vel = Projectile.velocity * 0.4f + new Vector2(Main.rand.NextFloat(-0.6f, 0.6f), -Main.rand.NextFloat(0.3f, 0.9f));
                Color col = Color.Lerp(FishCursedVFX.GreenCore, FishCursedVFX.GreenMid, Main.rand.NextFloat(0.3f, 0.9f));
                PRTLoader.NewParticle<PRT_FishCursedTongue>(Projectile.Center + Main.rand.NextVector2Circular(9f, 9f)
                    , vel, col, Main.rand.NextFloat(0.24f, 0.38f))
                    ?.Configure(Main.rand.Next(20, 30), -Main.rand.NextFloat(1.3f, 2f), Main.rand.NextFloat(0.4f, 0.8f));
            }
            if (phase % 7 == 0) {
                PRTLoader.NewParticle<PRT_FishCursedEmber>(Projectile.Center + Main.rand.NextVector2Circular(10f, 10f)
                    , Projectile.velocity * 0.2f + Main.rand.NextVector2Circular(0.8f, 0.8f)
                    , Color.Lerp(FishCursedVFX.GreenMid, FishCursedVFX.GreenDeep, Main.rand.NextFloat())
                    , Main.rand.NextFloat(0.36f, 0.55f))?.Configure(Main.rand.Next(22, 34));
            }
            //暗烟压底
            if ((State == FlameState.Hover || State == FlameState.Rise) && phase % 9 == 0) {
                PRTLoader.NewParticle<PRT_FishCursedSmog>(Projectile.Center + Main.rand.NextVector2Circular(6f, 6f)
                    , new Vector2(0f, -Main.rand.NextFloat(0.5f, 1.2f)), FishCursedVFX.SmokeDark, 0.2f)
                    ?.Configure(26, 0.34f, 0.012f);
            }
            //原版诅咒尘，低频粒状填充
            if (phase % 6 == 0) {
                Dust d = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(10f, 10f)
                    , DustID.CursedTorch, -Projectile.velocity * 0.1f + new Vector2(0f, -0.6f), 130, default, Main.rand.NextFloat(0.9f, 1.4f));
                d.noGravity = true;
            }
        }

        //碰撞地形
        public override bool OnTileCollide(Vector2 oldVelocity) {
            if (State == FlameState.Fading) {
                return false;
            }
            impacted = true;
            SoundEngine.PlaySound(SoundID.Item74 with { Volume = 0.45f, Pitch = 0.1f, MaxInstances = 3 }, Projectile.Center);
            SoundEngine.PlaySound(SoundID.DD2_ExplosiveTrapExplode with { Volume = 0.28f, Pitch = -0.2f, MaxInstances = 3 }, Projectile.Center);
            FishCursedVFX.Punch(Projectile.Center, oldVelocity, 1.6f, 9f, 6, 500f);
            FishCursedVFX.ImpactBurst(Projectile.Center, oldVelocity, 1f);
            EnterFade();
            Projectile.velocity = Vector2.Zero;
            return false;
        }

        //击中NPC
        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //附加诅咒火焰效果
            target.AddBuff(BuffID.CursedInferno, 300 + HalibutData.GetDomainLayer() * 40);

            SoundEngine.PlaySound(SoundID.NPCHit3 with { Volume = 0.4f, Pitch = -0.3f, MaxInstances = 4 }, Projectile.Center);
            SoundEngine.PlaySound(SoundID.Item74 with { Volume = 0.3f, Pitch = 0.2f, MaxInstances = 3 }, Projectile.Center);
            FishCursedVFX.ImpactBurst(Projectile.Center, Projectile.velocity, 0.8f, target);
            //咬肉微掉速
            Projectile.velocity *= 0.85f;
            //末次贯穿即终点，燃尽演出不再叠加
            if (Projectile.penetrate <= 1) {
                impacted = true;
            }
        }

        public override void OnKill(int timeLeft) {
            //燃尽演出已在 EnterFade 拍点放过；这里只兜住意外死亡（穿透耗尽等）
            if (impacted || State == FlameState.Fading) {
                return;
            }
            GutterOut();
        }


        private float BodyScale => State switch {
            FlameState.Hover => 0.68f,
            FlameState.Rise => 0.62f,
            _ => 0.58f,
        };

        /// <summary>焰尾条带</summary>
        void IPrimitiveDrawable.DrawPrimitives() {
            if (Main.dedServ || !Projectile.active) {
                return;
            }
            Effect fx = FishCursedAssets.FishCursedFlame;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (fx == null || noise == null) {
                return;
            }
            float fade = 1f - Projectile.alpha / 255f;
            if (fade <= 0.03f) {
                return;
            }

            //采样点
            Vector2 half = Projectile.Size / 2f;
            Span<Vector2> pts = stackalloc Vector2[1 + Projectile.oldPos.Length];
            int count = 0;
            pts[count++] = Projectile.Center;
            for (int k = 0; k < Projectile.oldPos.Length; k++) {
                if (Projectile.oldPos[k] == Vector2.Zero) {
                    break;
                }
                Vector2 p = Projectile.oldPos[k] + half;
                if (Vector2.DistanceSquared(p, pts[count - 1]) < 4f) {
                    continue;
                }
                pts[count++] = p;
            }
            if (count < 3) {
                return; //悬停期尾迹自然收没
            }

            //头宽∝速度，滑行细带、上浮宽焰
            float maxWidth = MathHelper.Clamp(6f + Projectile.velocity.Length() * 1.05f, 7f, 19f);
            var verts = new VertexPositionColorTexture[count * 2];
            for (int i = 0; i < count; i++) {
                float t = i / (float)(count - 1);
                Vector2 tangent = i < count - 1
                    ? (pts[i] - pts[i + 1]).SafeNormalize(Vector2.UnitX)
                    : (pts[i - 1] - pts[i]).SafeNormalize(Vector2.UnitX);
                Vector2 normal = new(-tangent.Y, tangent.X);
                float width = maxWidth * (0.5f + 0.5f * MathHelper.Clamp(t / 0.14f, 0f, 1f))
                    * MathF.Pow(1f - t, 0.7f);
                verts[i * 2] = new VertexPositionColorTexture((pts[i] + normal * width).ToVector3()
                    , Color.White, new Vector2(t, 0f));
                verts[i * 2 + 1] = new VertexPositionColorTexture((pts[i] - normal * width).ToVector3()
                    , Color.White, new Vector2(t, 1f));
            }

            GraphicsDevice device = Main.instance.GraphicsDevice;
            BlendState prevBlend = device.BlendState;
            RasterizerState prevRaster = device.RasterizerState;
            device.BlendState = BlendState.AlphaBlend;
            device.RasterizerState = RasterizerState.CullNone;

            FishCursedVFX.ApplyFlameTrail(fx, Projectile.whoAmI * 0.61f, fade);
            foreach (EffectPass pass in fx.CurrentTechnique.Passes) {
                pass.Apply();
                device.DrawUserPrimitives(PrimitiveType.TriangleStrip, verts, 0, verts.Length - 2);
            }

            device.BlendState = prevBlend;
            device.RasterizerState = prevRaster;
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D fire = CWRAsset.Fire?.Value;
            if (fire == null) {
                return false;
            }
            float fade = 1f - Projectile.alpha / 255f;
            if (fade <= 0.02f) {
                return false;
            }

            int frameIdx = (int)(Main.GameUpdateCount / 4 + VisualSeed * 7f);
            Rectangle frame = FishCursedVFX.FireFrame(fire, frameIdx);
            Rectangle frameAlt = FishCursedVFX.FireFrame(fire, frameIdx + 5);
            Vector2 origin = frame.Size() * 0.5f;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            float speed = Projectile.velocity.Length();
            //速度拉伸，沿焰轴伸长、横向收窄
            float stretchAmt = MathHelper.Clamp(speed * 0.05f, 0f, 0.85f);
            //悬停呼吸胀缩
            float breath = State == FlameState.Hover
                ? 1f + 0.13f * MathF.Sin(StateTime * 0.5f + VisualSeed)
                : 1f + 0.05f * MathF.Sin(StateTime * 0.22f + VisualSeed);
            float body = BodyScale * breath;
            Vector2 scale = new(body * (1f - stretchAmt * 0.28f), body * (1f + stretchAmt));

            //墨绿雾底
            Texture2D fog = CWRAsset.Fog?.Value;
            if (fog != null) {
                Main.EntitySpriteDraw(fog, drawPos, null, FishCursedVFX.SmokeDark * (0.42f * fade)
                    , visualRot * 0.3f, fog.Size() * 0.5f, 0.36f * body, SpriteEffects.None, 0);
            }

            //速度残影
            if (speed > 4f) {
                for (int i = 3; i <= 6; i += 3) {
                    if (i >= Projectile.oldPos.Length || Projectile.oldPos[i] == Vector2.Zero) {
                        continue;
                    }
                    Vector2 ghostPos = Projectile.oldPos[i] + Projectile.Size * 0.5f - Main.screenPosition;
                    float ghostFade = (1f - i / 8f) * 0.3f * MathHelper.Clamp(speed / 14f, 0f, 1f) * fade;
                    Main.EntitySpriteDraw(fire, ghostPos, frame, FishCursedVFX.GreenDeep with { A = 0 } * ghostFade
                        , visualRot, origin, scale * 0.9f, SpriteEffects.None, 0);
                }
            }

            //暗绿外鞘（压底异质层）
            Main.EntitySpriteDraw(fire, drawPos, frame, FishCursedVFX.GreenDeep with { A = 0 } * (0.5f * fade)
                , visualRot, origin, scale * 1.45f, SpriteEffects.None, 0);
            //饱和中绿主体（错帧去相关）
            Main.EntitySpriteDraw(fire, drawPos, frameAlt, FishCursedVFX.GreenMid with { A = 0 } * (0.8f * fade)
                , visualRot, origin, scale, SpriteEffects.None, 0);

            //黄绿焰心
            Texture2D shot = CWRAsset.LightShot?.Value;
            if (shot != null) {
                Vector2 coreScale = new Vector2(0.13f + 0.10f * stretchAmt, 0.052f) * (body / 0.6f);
                Main.EntitySpriteDraw(shot, drawPos, null, FishCursedVFX.GreenCore with { A = 0 } * (0.8f * fade)
                    , visualRot - MathHelper.PiOver2, shot.Size() * 0.5f, coreScale, SpriteEffects.None, 0);
            }
            return false;
        }
    }
}
