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

namespace CalamityOverhaul.Content.Items.Magic.Elysiums.Revelations
{
    /// <summary>
    /// 天启骑士：启示录期间随行的幽影骑影，被动威能接线在 <see cref="ElysiumPlayer"/>。
    /// 各骑士轨道形态不同：瘟疫漩涡收放、战争冲刺扩张、饥荒沉缓低回、死亡八字巡游。
    /// ai[0]=骑士索引0~3
    /// </summary>
    internal class ApocalypseHorseman : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        public int HorsemanIndex => (int)Projectile.ai[0];
        private HorsemanDef Def => HorsemanCatalog.Get(HorsemanIndex);
        private Player Owner => Main.player[Projectile.owner];

        private const int EmergeTime = 34;

        private int timer;
        private float emergeProgress;
        private float gallopPhase;
        private float orbitAngle;
        private int facing = 1;
        private Vector2 velocitySmooth;

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 400;

        public override void SetDefaults() {
            Projectile.width = 60;
            Projectile.height = 44;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 120;
            Projectile.netImportant = true;
        }

        public override bool? CanDamage() => false;

        public override void AI() {
            if (!Owner.active || Owner.dead) {
                Projectile.Kill();
                return;
            }
            Projectile.timeLeft = 120;
            timer++;

            if (timer == 1) {
                //降临拍：天光落柱昭告
                SoundEngine.PlaySound(SoundID.Item117 with { Volume = 1.1f, Pitch = -0.22f + HorsemanIndex * 0.12f }, Projectile.Center);
                SoundEngine.PlaySound(SoundID.Item122 with { Volume = 0.75f, Pitch = -0.45f + HorsemanIndex * 0.08f }, Projectile.Center);
                if (!Main.dedServ) {
                    PRTLoader.NewParticle<PRT_SkyBolt>(Projectile.Center, Vector2.Zero, Def.AccentColor, 0.9f)
                        ?.Configure(Projectile.Center - new Vector2(0f, 560f), Projectile.Center, 24);
                    for (int i = 0; i < 10; i++) {
                        float angle = MathHelper.TwoPi * i / 10f;
                        PRTLoader.NewParticle<PRT_HeavenfallStar>(Projectile.Center
                            , angle.ToRotationVector2() * Main.rand.NextFloat(3f, 6f)
                            , Def.AccentColor, Main.rand.NextFloat(0.6f, 1f))?.Configure(false, Main.rand.Next(14, 20));
                    }
                }
            }

            emergeProgress = Math.Min(timer / (float)EmergeTime, 1f);

            //主人端：启示录终止即散
            if (Projectile.IsOwnedByLocalPlayer()
                && (!Owner.TryGetModPlayer(out ElysiumPlayer ep) || !ep.IsRevelationActive)) {
                Projectile.Kill();
                return;
            }

            UpdateOrbit();

            //奔驰相位随实际速度推进(静止缓踏)
            float speed = velocitySmooth.Length();
            gallopPhase += 0.08f + Math.Min(speed * 0.02f, 0.28f);
            if (Math.Abs(velocitySmooth.X) > 0.6f) {
                facing = velocitySmooth.X > 0f ? 1 : -1;
            }

            //身份色微光与识别尘
            Color light = Def.BodyColor;
            Lighting.AddLight(Projectile.Center, light.R / 255f * 0.4f, light.G / 255f * 0.4f, light.B / 255f * 0.4f);
            if (!Main.dedServ && Main.rand.NextBool(10)) {
                PRTLoader.NewParticle<PRT_Light>(Projectile.Center + Main.rand.NextVector2Circular(26f, 18f)
                    , new Vector2(-facing * Main.rand.NextFloat(0.5f, 1.5f), -Main.rand.NextFloat(0.3f, 1f))
                    , Def.BodyColor, Main.rand.NextFloat(0.18f, 0.3f))?.Configure(Main.rand.Next(14, 24), 0.75f);
            }
        }

        /// <summary>四骑士各异的轨道运动学</summary>
        private void UpdateOrbit() {
            orbitAngle += Def.OrbitSpeed;
            float t = timer * 0.016f;
            Vector2 offset;
            switch (HorsemanIndex) {
                case 0: {
                    //瘟疫：漩涡收放，半径呼吸
                    float radius = Def.OrbitRadius * (0.7f + 0.3f * MathF.Sin(t * 1.2f));
                    offset = orbitAngle.ToRotationVector2() * radius;
                    offset.Y *= 0.55f;
                    break;
                }
                case 1: {
                    //战争：冲刺扩张，角速度阵发提速
                    float surge = MathF.Pow(MathF.Max(MathF.Sin(t * 1.6f), 0f), 3f);
                    orbitAngle += surge * 0.05f;
                    float radius = Def.OrbitRadius * (0.85f + surge * 0.4f);
                    offset = orbitAngle.ToRotationVector2() * radius;
                    offset.Y *= 0.5f;
                    break;
                }
                case 2: {
                    //饥荒：沉缓低回，贴着下弧慢行
                    float radius = Def.OrbitRadius;
                    offset = orbitAngle.ToRotationVector2() * radius;
                    offset.Y = offset.Y * 0.35f + 60f + MathF.Sin(t * 0.8f) * 22f;
                    break;
                }
                default: {
                    //死亡：八字巡游(利萨如)
                    float radius = Def.OrbitRadius;
                    offset = new Vector2(MathF.Sin(orbitAngle) * radius, MathF.Sin(orbitAngle * 2f) * radius * 0.32f - 40f);
                    break;
                }
            }

            Vector2 targetPos = Owner.Center + offset;
            Vector2 toTarget = targetPos - Projectile.Center + Owner.velocity * 0.2f;
            velocitySmooth = Vector2.Lerp(velocitySmooth, toTarget * 0.35f, 0.16f);
            Projectile.Center += velocitySmooth;

            if (Vector2.DistanceSquared(Projectile.Center, targetPos) > 1100f * 1100f) {
                Projectile.Center = targetPos;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Effect effect = EffectLoader.HorsemanForm?.Value;
            Texture2D canvas = VaultAsset.placeholder2?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (effect == null || canvas == null || noise == null) {
                return false;
            }

            float quadSize = 150f * Def.SizeMul;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            float motion = MathHelper.Clamp(velocitySmooth.Length() * 0.06f, 0.15f, 1f);

            effect.CurrentTechnique = effect.Techniques["HorsemanForm"];
            effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            effect.Parameters["fadeAlpha"]?.SetValue(1f);
            effect.Parameters["uSeed"]?.SetValue(HorsemanIndex * 1.73f);
            effect.Parameters["bodyColor"]?.SetValue(Def.BodyColor.ToVector3());
            effect.Parameters["accentColor"]?.SetValue(Def.AccentColor.ToVector3());
            effect.Parameters["uGallop"]?.SetValue(gallopPhase);
            effect.Parameters["uMotion"]?.SetValue(motion);
            effect.Parameters["uEmerge"]?.SetValue(emergeProgress);

            Main.graphics.GraphicsDevice.Textures[1] = noise;
            Main.graphics.GraphicsDevice.SamplerStates[1] = SamplerState.LinearWrap;

            //贴图空间骑影朝+X，朝左时水平翻转
            SpriteEffects flip = facing > 0 ? SpriteEffects.None : SpriteEffects.FlipHorizontally;

            SpriteBatch sb = Main.spriteBatch;
            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend,
                SamplerState.LinearClamp, DepthStencilState.None,
                RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
            effect.CurrentTechnique.Passes[0].Apply();
            sb.Draw(canvas, drawPos, null, Color.White, 0f, canvas.Size() * 0.5f, quadSize, flip, 0f);
            sb.End();

            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                Main.DefaultSamplerState, DepthStencilState.None,
                Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);

            //头顶悬浮圣徽
            SvgPath sigil = SvgPathPen.Path(Def.SigilPath);
            if (sigil != null && emergeProgress > 0.5f) {
                float sigilAlpha = (emergeProgress - 0.5f) * 2f * (0.6f + 0.4f * MathF.Sin(Main.GlobalTimeWrappedHourly * 2.4f + HorsemanIndex));
                Vector2 sigilPos = drawPos + new Vector2(0f, -quadSize * 0.34f);
                SvgPathPen.Stroke(sb, sigil, sigilPos, 11f, 0f, Def.AccentColor with { A = 0 } * sigilAlpha
                    , 1.3f, sigilAlpha, core: Color.White with { A = 0 } * (0.5f * sigilAlpha));
            }
            return false;
        }

        public override void OnKill(int timeLeft) {
            SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.7f, Pitch = -0.3f }, Projectile.Center);
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < 12; i++) {
                PRTLoader.NewParticle<PRT_Light>(Projectile.Center + Main.rand.NextVector2Circular(30f, 22f)
                    , VaultUtils.RandVr(1.5f, 4f), Def.BodyColor, Main.rand.NextFloat(0.24f, 0.4f))
                    ?.Configure(Main.rand.Next(18, 30), 0.85f);
            }
        }
    }
}
