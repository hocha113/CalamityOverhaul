using CalamityOverhaul.Common;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using InnoVault.Trails;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.Graphics.Shaders;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Rogue
{
    internal class CosmicCalamityProjectile : ModProjectile, IPrimitiveDrawable
    {
        public static SoundStyle BelCanto = new("CalamityOverhaul/Assets/Sounds/BelCanto") { Volume = 2.5f };
        public override string Texture => CWRConstant.Item + "Rogue/CosmicCalamity";
        private Trail Trail;
        public int Time = 0;
        public int TimeUnderground = 0;
        public Vector2 NPCDestination;
        public Player Owner => Main.player[Projectile.owner];
        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 26;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 1;
        }

        public override void SetDefaults() {
            Projectile.width = 86;
            Projectile.height = 86;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.DamageType = CWRLoad.RogueDamageClass;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 20;
            Projectile.penetrate = 1;
            Projectile.MaxUpdates = 3;
            Projectile.timeLeft = 300;
        }

        public override void AI() {
            //更新弹幕旋转方向
            Projectile.rotation = Projectile.velocity.ToRotation();

            //如果不是服务器模式，创建粒子效果
            if (!VaultUtils.isServer) {
                CreateSparkEffect();
            }

            //如果时间超过某个值，执行相关行为
            if (Time > (Projectile.GetProjStealthStrike() ? 0 : 60)) {
                TrackNearestNPC();
                UpdateUndergroundBehavior();
            }

            //增加计时
            Time++;
        }

        private void CreateSparkEffect() {
            Color color = Color.Lerp(Color.Cyan, Color.White, Main.rand.NextFloat(0.3f, 0.64f));
            BasePRT spark = new PRT_Spark(Projectile.Center - Projectile.velocity * 8f
                , -Projectile.velocity * 0.1f, false, 9, 2.3f, color * 0.1f);
            PRTLoader.AddParticle(spark);
        }

        private void TrackNearestNPC() {
            for (int i = 0; i < Main.maxNPCs; i++) {
                if (Main.npc[i].CanBeChasedBy(Projectile.GetSource_FromThis(), false)) {
                    NPCDestination = Main.npc[i].Center + Main.npc[i].velocity * 5f;
                }
            }
        }

        private void UpdateUndergroundBehavior() {
            TimeUnderground++;

            //添加光照效果
            Vector3 DustLight = new Vector3(0.171f, 0.124f, 0.086f);
            Lighting.AddLight(Projectile.Center + Projectile.velocity, DustLight * 14);

            //播放地下声音
            if (Time % 15 == 0 && TimeUnderground < 120) {
                SoundEngine.PlaySound(SoundID.WormDig with { Volume = 0.7f, Pitch = 0.2f }, Projectile.Center);
            }

            //处理弹幕速度调整
            AdjustProjectileVelocity();
        }

        private void AdjustProjectileVelocity() {
            float returnSpeed = 10;
            float acceleration = 0.2f;

            //计算NPC的距离和方向
            float xDist = NPCDestination.X - Projectile.Center.X;
            float yDist = NPCDestination.Y - Projectile.Center.Y;
            float dist = (float)Math.Sqrt(xDist * xDist + yDist * yDist);
            dist = returnSpeed / dist;

            xDist *= dist;
            yDist *= dist;

            //如果NPC在距离内且弹幕在地下停留了一段时间
            if (Vector2.Distance(NPCDestination, Projectile.Center) < 1800 && TimeUnderground > 25) {
                AdjustVelocityTowardsTarget(xDist, yDist, acceleration);
            }
        }

        private void AdjustVelocityTowardsTarget(float xDist, float yDist, float acceleration) {
            if (Projectile.velocity.X < xDist) {
                Projectile.velocity.X = AdjustVelocity(Projectile.velocity.X, xDist, acceleration);
            }
            else if (Projectile.velocity.X > xDist) {
                Projectile.velocity.X = AdjustVelocity(Projectile.velocity.X, xDist, -acceleration);
            }

            if (Projectile.velocity.Y < yDist) {
                Projectile.velocity.Y = AdjustVelocity(Projectile.velocity.Y, yDist, acceleration);
            }
            else if (Projectile.velocity.Y > yDist) {
                Projectile.velocity.Y = AdjustVelocity(Projectile.velocity.Y, yDist, -acceleration);
            }
        }

        private float AdjustVelocity(float velocity, float target, float adjustment) {
            velocity += adjustment;
            if (velocity < 0f && target > 0f) {
                velocity += adjustment;
            }
            else if (velocity > 0f && target < 0f) {
                velocity -= adjustment;
            }
            return velocity;
        }

        public static void SpanDimensionalWave(Vector2 spanPos, Vector2 vr, Color color1, Color color2, float starS, float endS, float starS2, float endS2) {
            BasePRT pulse = new PRT_DWave(spanPos - vr * 0.52f, vr / 1.5f, color1, new Vector2(1f, 2f), vr.ToRotation(), starS, endS, 60);
            PRTLoader.AddParticle(pulse);
            BasePRT pulse2 = new PRT_DWave(spanPos - vr * 0.40f, vr / 1.5f * 0.9f, color2, new Vector2(0.8f, 1.5f), vr.ToRotation(), starS2, starS2, 50);
            PRTLoader.AddParticle(pulse2);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (Projectile.numHits == 0) {
                if (Projectile.GetProjStealthStrike()) {
                    Projectile.NewProjectile(Projectile.FromObjectGetParent(), Projectile.Center, MathHelper.PiOver4.ToRotationVector2() * 13
                        , ModContent.ProjectileType<CosmicCalamityRay>(), Projectile.damage / 2, 0, Projectile.owner);
                    Projectile.NewProjectile(Projectile.FromObjectGetParent(), Projectile.Center
                        , (MathHelper.PiOver2 + MathHelper.PiOver4).ToRotationVector2() * 13
                        , ModContent.ProjectileType<CosmicCalamityRay>(), Projectile.damage / 2, 0, Projectile.owner);
                    for (int i = 0; i < 4; i++) {
                        float rot = MathHelper.PiOver2 * i;
                        Vector2 vr = rot.ToRotationVector2() * 10;
                        for (int j = 0; j < 16; j++) {
                            float slp = j / 16f;
                            float slp2 = 16f / j;
                            Vector2 spanPos = Projectile.Center + rot.ToRotationVector2() * 64 * j;
                            BasePRT pulse = new PRT_DWave(spanPos - vr * 0.52f, vr / 1.5f
                                , Color.Gold, new Vector2(1f, 2f), vr.ToRotation(), 0.82f * slp, 0.32f, 60);
                            PRTLoader.AddParticle(pulse);
                            BasePRT pulse2 = new PRT_DWave(spanPos - vr * 0.40f, vr / 1.5f * 0.9f
                                , Color.OrangeRed, new Vector2(0.8f, 1.5f), vr.ToRotation(), 0.58f * slp, 0.28f, 50);
                            PRTLoader.AddParticle(pulse2);
                        }
                    }
                    SoundEngine.PlaySound(BelCanto, Projectile.Center);
                    target.AddBuff(CWRID.Buff_GodSlayerInferno, 1300);
                    Projectile.Explode(190, "CalamityMod/Sounds/NPCKilled/DevourerDeathImpact".GetSound() with { Volume = 0.8f });
                    Projectile.Kill();
                }
                else {
                    Projectile.Explode(90, "CalamityMod/Sounds/NPCKilled/DevourerDeath".GetSound() with { Volume = 0.8f });
                    target.AddBuff(CWRID.Buff_GodSlayerInferno, 300);
                }
            }

            if (!Projectile.GetProjStealthStrike()) {
                BasePRT pulse = new PRT_DWave(Projectile.Center - Projectile.velocity * 0.52f
                    , Projectile.velocity / 1.5f, Color.Fuchsia, new Vector2(1f, 2f), Projectile.velocity.ToRotation(), 0.82f, 0.32f, 60);
                PRTLoader.AddParticle(pulse);
                BasePRT pulse2 = new PRT_DWave(Projectile.Center - Projectile.velocity * 0.40f
                    , Projectile.velocity / 1.5f * 0.9f, Color.Aqua, new Vector2(0.8f, 1.5f), Projectile.velocity.ToRotation(), 0.58f, 0.28f, 50);
                PRTLoader.AddParticle(pulse2);
            }

            for (int j = 0; j < 4; j++) {
                Vector2 dustVel = new Vector2(6, 6).RotatedByRandom(100) * Main.rand.NextFloat(0.5f, 1.2f);

                Dust dust = Dust.NewDustPerfect(Projectile.Center + dustVel * 2, DustID.WitherLightning, dustVel, 0, default, 1f);
                dust.shader = GameShaders.Armor.GetSecondaryShader(Owner.cShield, Owner);

                Dust dust2 = Dust.NewDustPerfect(Projectile.Center + dustVel * 2, DustID.Electric, dustVel, 0, default, 1f);
                dust2.shader = GameShaders.Armor.GetSecondaryShader(Owner.cShield, Owner);
            }
            if (Projectile.numHits > 6) {
                Projectile.Explode(90, spanSound: false);
                Projectile.Kill();
            }
        }

        public override void OnKill(int timeLeft) {
            for (int j = 0; j < 14; j++) {
                Vector2 dustVel = new Vector2(6, 6).RotatedByRandom(100) * Main.rand.NextFloat(0.5f, 1.2f);

                Dust dust = Dust.NewDustPerfect(Projectile.Center + dustVel * 2, DustID.WitherLightning, dustVel, 0, default, 1f);
                dust.shader = GameShaders.Armor.GetSecondaryShader(Owner.cShield, Owner);

                Dust dust2 = Dust.NewDustPerfect(Projectile.Center + dustVel * 2, DustID.Electric, dustVel, 0, default, 1f);
                dust.shader = GameShaders.Armor.GetSecondaryShader(Owner.cShield, Owner);
            }
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            Projectile.velocity = -oldVelocity * 0.75f;
            return false;
        }

        internal Color GetColorFunc(Vector2 completionRatio) {
            float amount = MathHelper.Lerp(0.65f, 1f, (float)Math.Cos((0f - Main.GlobalTimeWrappedHourly) * 3f) * 0.5f + 0.5f);
            float num = Utils.GetLerpValue(1f, 0.64f, completionRatio.X, clamped: true) * Projectile.Opacity;
            Color value = Color.Lerp(Main.hslToRgb(1, 1f, 0.8f), Color.PaleTurquoise
                , (float)Math.Sin(completionRatio.X * MathF.PI * 1.6f - Main.GlobalTimeWrappedHourly * 4f) * 0.5f + 0.5f);
            return Color.Lerp(Color.White, value, amount) * num;
        }

        internal float GetWidthFunc(float completionRatio) {
            float amount = (float)Math.Pow(1f - completionRatio, 3.0);
            return MathHelper.Lerp(0f, 22f * Projectile.scale * Projectile.Opacity, amount);
        }

        void IPrimitiveDrawable.DrawPrimitives() {
            if (Projectile.oldPos == null || Projectile.oldPos.Length == 0) {
                return;
            }

            //准备轨迹点
            Vector2[] positions = new Vector2[Projectile.oldPos.Length];
            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero) {
                    Projectile.oldPos[i] = Projectile.Center;
                }
                positions[i] = Projectile.oldPos[i] + Projectile.Size * 0.5f;
            }

            //创建或更新 Trail
            Trail ??= new Trail(positions, GetWidthFunc, GetColorFunc);
            Trail.TrailPositions = positions;

            //使用 InnoVault 的绘制方法
            Effect effect = EffectLoader.GradientTrail.Value;
            effect.Parameters["transformMatrix"].SetValue(VaultUtils.GetTransfromMatrix());
            effect.Parameters["uTime"].SetValue((float)Main.timeForVisualEffects * 0.08f);
            effect.Parameters["uTimeG"].SetValue(Main.GlobalTimeWrappedHourly * 0.2f);
            effect.Parameters["udissolveS"].SetValue(1f);
            effect.Parameters["uBaseImage"].SetValue(CWRUtils.GetT2DValue("CalamityMod/ExtraTextures/Trails/ScarletDevilStreak"));
            effect.Parameters["uFlow"].SetValue(CWRAsset.Airflow.Value);
            effect.Parameters["uGradient"].SetValue(CWRUtils.GetT2DValue(CWRConstant.ColorBar + "DragonRage_Bar"));
            effect.Parameters["uDissolve"].SetValue(CWRAsset.Extra_193.Value);

            Main.graphics.GraphicsDevice.BlendState = BlendState.Additive;
            Trail?.DrawTrail(effect);
            Main.graphics.GraphicsDevice.BlendState = BlendState.AlphaBlend;
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D value = TextureAssets.Projectile[Type].Value;
            Main.EntitySpriteDraw(value, Projectile.Center - Main.screenPosition, null, lightColor
                , Projectile.rotation + MathHelper.PiOver4, value.Size() / 2, Projectile.scale, SpriteEffects.None, 0);
            return false;
        }
    }
}
