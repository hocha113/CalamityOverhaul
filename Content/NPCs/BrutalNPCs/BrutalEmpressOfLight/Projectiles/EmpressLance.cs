using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEmpressOfLight.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEmpressOfLight.Rendering;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEmpressOfLight.Projectiles
{
    /// <summary>长枪模式（ai[1]）</summary>
    internal enum EmpressLanceMode : int
    {
        /// <summary>墙：瞄准期弱追踪 0.009×((60-t)/60)^0.4</summary>
        Wall = 0,
        /// <summary>瞄准枪：强追踪 0.075×((60-t)/60)^2.25，发射后 1000px 内补追</summary>
        Aimed = 1,
        /// <summary>终章疾速：瞄准期后退 30px 再射，40f 出手，68px/f 子步</summary>
        Rush = 2,
        /// <summary>终章智能：瞄准期恒定强追踪，发射 100px/f，60f 后预测补追</summary>
        Tracking = 3,
    }

    /// <summary>
    /// 以太长枪：60f 瞄准（追踪率随时间衰减到零=预告即承诺）→ 发射贯穿。
    /// 本体=原版 919 贴图；ai[0]=角度 ai[1]=模式 ai[2]=宿主 whoAmI。高速时子步推进防穿人
    /// </summary>
    internal class EmpressLance : ModProjectile, IEmpressAttack, IPrimitiveDrawable
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.FairyQueenLance;

        public EmpressScorchTier ScorchTier => EmpressScorchTier.Medium;
        public float FeedbackIntensity => 0.8f;

        private const float BaseSpeed = 40f;
        private const float SpearLength = 300f;
        private const float TelegraphLength = 2800f;
        private const int FlyLife = 86;

        private ref float Angle => ref Projectile.ai[0];
        private EmpressLanceMode Mode => (EmpressLanceMode)(int)Projectile.ai[1];
        private NPC Host => ((int)Projectile.ai[2]).TryGetNPC(out NPC n) ? n : null;
        private ref float Timer => ref Projectile.localAI[0];

        private int AimTime => Mode == EmpressLanceMode.Rush ? 40 : 60;
        private bool Launched => Timer >= AimTime;
        private float Hue => (Projectile.identity * 0.157f) % 1f;
        private static float DayBlend => VaultUtils.isServer ? 0f : EmpressDayDrive.Intensity;

        private float Speedup => Mode switch {
            EmpressLanceMode.Rush => 0.7f,
            EmpressLanceMode.Tracking => 1.5f,
            _ => NPC.ShouldEmpressBeEnraged() ? 0.4f : 0.15f,
        };

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 3000;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 24;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 600;
            CooldownSlot = ImmunityCooldownID.Bosses;
        }

        private Player TargetPlayer() {
            NPC host = Host;
            if (host != null && host.target >= 0 && host.target < Main.maxPlayers && Main.player[host.target].Alives()) {
                return Main.player[host.target];
            }
            return null;
        }

        public override void AI() {
            if (Timer == 0f) {
                Projectile.timeLeft = AimTime + FlyLife;
                Projectile.rotation = Angle;
                Projectile.velocity = Vector2.Zero;
            }
            Timer++;

            if (!Launched) {
                AimUpdate();
            }
            else {
                FlyUpdate();
            }

            Projectile.rotation = Angle;
            Lighting.AddLight(Projectile.Center, EmpressMotion.FormColor(Hue, DayBlend, 0.55f).ToVector3() * 0.5f * Projectile.Opacity);
        }

        private void AimUpdate() {
            Projectile.velocity = Vector2.Zero;
            Projectile.Opacity = MathHelper.Clamp(Timer / 14f, 0f, 1f);
            Player target = TargetPlayer();
            float t = MathHelper.Clamp(Timer / AimTime, 0f, 1f);
            bool day = NPC.ShouldEmpressBeEnraged();

            if (target != null) {
                float maxDelta = Mode switch {
                    EmpressLanceMode.Wall => 0.009f * MathF.Pow(1f - t, 0.4f),
                    EmpressLanceMode.Aimed => 0.075f * MathF.Pow(1f - t, 2.25f),
                    EmpressLanceMode.Rush => 0.02f * MathF.Pow(1f - t, 0.4f),
                    _ => 0.12f,
                };
                if (!day) {
                    maxDelta *= 0.5f;
                }
                Vector2 aim = EmpressMotion.Intercept(Projectile.Center, target, BaseSpeed * (1f + Speedup));
                Angle = EmpressMotion.TurnToward(Angle, Projectile.AngleTo(aim), maxDelta);
            }

            //终章疾速：瞄准期沿方向后退再射（反向运动）
            if (Mode == EmpressLanceMode.Rush) {
                Projectile.position -= Angle.ToRotationVector2() * (MathF.Pow(1f - t, 2f) * 1.6f);
            }

            //即将发射前 4f：收束火花，屏息拍
            if (!VaultUtils.isServer && Timer > AimTime - 4 && Main.rand.NextBool(2)) {
                Vector2 gather = Projectile.Center + Main.rand.NextVector2CircularEdge(60f, 60f);
                PRTLoader.NewParticle<PRT_EmpressSpark>(gather, (Projectile.Center - gather) * 0.16f,
                    EmpressMotion.FormColor(Hue, DayBlend, 0.7f), Main.rand.NextFloat(0.6f, 1f))?.Configure(10, Hue, DayBlend);
            }
        }

        private void FlyUpdate() {
            Vector2 dir = Angle.ToRotationVector2();
            float speed = BaseSpeed * (1f + Speedup);
            if (Timer == AimTime) {
                Projectile.velocity = dir * BaseSpeed;
                OnLaunch(dir);
            }

            //发射后补追：1000px 内以 (1000-d)/450 的加速度转向（智能枪 60f 后才开，用预测点）
            Player target = TargetPlayer();
            int homingDelay = Mode == EmpressLanceMode.Tracking ? 60 : (Mode == EmpressLanceMode.Aimed ? 45 : 999);
            if (target != null && Timer - AimTime > homingDelay) {
                float d = Projectile.Distance(target.Center);
                float accel = Math.Max((1000f - d) / 450f, 0f);
                if (accel > 0f) {
                    Vector2 aim = Mode == EmpressLanceMode.Tracking ? EmpressMotion.Intercept(Projectile.Center, target, speed) : target.Center;
                    Vector2 desired = Projectile.DirectionTo(aim) * BaseSpeed;
                    Vector2 diff = desired - Projectile.velocity;
                    Projectile.velocity += diff.SafeNormalize(Vector2.Zero) * Math.Min(diff.Length(), accel);
                    Angle = Projectile.velocity.ToRotation();
                }
            }

            //子步：speedup 部分分多次推进并逐步判定，高速枪不穿人
            float extra = Speedup;
            while (extra > 0f) {
                float step = Math.Min(extra, 1f);
                Projectile.position += Projectile.velocity * step;
                //敌对弹幕的玩家判定在每个客户端对本地玩家结算，子步内逐段判
                Projectile.Damage();
                extra -= step;
            }

            Projectile.Opacity = MathHelper.Clamp(Projectile.timeLeft / 12f, 0f, 1f);

            if (!VaultUtils.isServer && Main.rand.NextBool(3)) {
                PRTLoader.NewParticle<PRT_EmpressSpark>(Projectile.Center - dir * Main.rand.NextFloat(SpearLength * 0.6f),
                    Main.rand.NextVector2Circular(1.2f, 1.2f), EmpressMotion.FormColor(Hue, DayBlend, 0.62f),
                    Main.rand.NextFloat(0.5f, 0.9f))?.Configure(14, Hue, DayBlend);
            }
        }

        private void OnLaunch(Vector2 dir) {
            if (VaultUtils.isServer) {
                return;
            }
            EmpressProjectileSystem.EnqueueShot(Projectile.Center, SoundID.Item162 with { Volume = 0.8f, Pitch = 0.1f });
            PRTLoader.NewParticle<PRT_EmpressRipple>(Projectile.Center, Vector2.Zero, Color.White, 0.62f)?.Configure(14, Hue, DayBlend);
            EmpressMotion.SparkBurst(Projectile.Center, dir, 5, 4f, 11f, DayBlend, 0.35f);
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer || !Launched) {
                return;
            }
            Vector2 dir = Angle.ToRotationVector2();
            //被整场清弹时降载，防同帧粒子风暴
            if (timeLeft > 4) {
                EmpressMotion.SparkBurst(Projectile.Center - dir * SpearLength * 0.3f, dir, 1, 1f, 3f, DayBlend);
                return;
            }
            for (int i = 0; i < 6; i++) {
                float along = i / 6f;
                PRTLoader.NewParticle<PRT_EmpressSpark>(Projectile.Center - dir * SpearLength * along,
                    dir * (3f - along * 2.4f) + Main.rand.NextVector2Circular(1.6f, 1.6f),
                    EmpressMotion.FormColor(Hue + along * 0.12f, DayBlend, 0.62f),
                    Main.rand.NextFloat(0.5f, 0.9f))?.Configure(16, Hue, DayBlend);
            }
        }

        public override bool? CanDamage() => Launched ? null : false;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (!Launched) {
                return false;
            }
            float p = 0f;
            Vector2 dir = Angle.ToRotationVector2();
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(),
                Projectile.Center + dir * 20f, Projectile.Center - dir * SpearLength, 22f, ref p);
        }

        /// <summary>实体批：原版长枪贴图为体，暗边+金晕叠层；飞行时沿杆拖三道同材质残影</summary>
        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Vector2 dir = Angle.ToRotationVector2();
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Vector2 origin = tex.Size() / 2f;
            float day = DayBlend;
            Color body = EmpressMotion.FormColor(Hue, day, 0.66f) * Projectile.Opacity;
            Color dark = EmpressMotion.FormDark(day) * Projectile.Opacity;
            Color rim = EmpressMotion.FormRim(Hue, day, 0.55f) * Projectile.Opacity;
            float scale = Launched ? 1f : 0.7f + 0.3f * MathHelper.Clamp(Timer / AimTime, 0f, 1f);

            if (Launched) {
                //沿杆三道残影，越远越偏向光谱边（色散在拖尾里最显）
                for (int i = 1; i <= 3; i++) {
                    Vector2 ghost = drawPos - dir * (i * 26f);
                    Main.spriteBatch.Draw(tex, ghost, null, Color.Lerp(body, rim, i / 3f) * (0.35f * (1f - i / 4f)), Angle, origin, scale * (1f - i * 0.1f), SpriteEffects.None, 0f);
                }
                Main.spriteBatch.Draw(glow, drawPos, null, (rim with { A = 0 }) * 0.55f, 0f, glow.Size() / 2f, 1.3f, SpriteEffects.None, 0f);
            }
            //暗边（真 alpha 垫底）→ 光谱边 → 白金本体
            Main.spriteBatch.Draw(tex, drawPos, null, dark, Angle, origin, scale * 1.22f, SpriteEffects.None, 0f);
            Main.spriteBatch.Draw(tex, drawPos, null, rim, Angle, origin, scale * 1.1f, SpriteEffects.None, 0f);
            Main.spriteBatch.Draw(tex, drawPos, null, body, Angle, origin, scale, SpriteEffects.None, 0f);
            if (Launched) {
                Main.spriteBatch.Draw(tex, drawPos, null, (Color.White with { A = 0 }) * (0.45f * Projectile.Opacity), Angle, origin, scale * 0.7f, SpriteEffects.None, 0f);
            }
            return false;
        }

        /// <summary>图元层：瞄准期预告线（EmpressLanceBeam TelegraphTech），追踪中的线是活的</summary>
        void IPrimitiveDrawable.DrawPrimitives() {
            if (VaultUtils.isServer || Launched) {
                return;
            }
            Effect effect = EffectLoader.EmpressLanceBeam?.Value;
            if (effect == null) {
                return;
            }
            EffectTechnique tech = effect.Techniques["TelegraphTech"];
            if (tech == null) {
                return;
            }
            GraphicsDevice device = Main.graphics.GraphicsDevice;
            BlendState origBlend = device.BlendState;
            RasterizerState origRaster = device.RasterizerState;
            device.BlendState = BlendState.Additive;
            device.RasterizerState = RasterizerState.CullNone;

            float progress = MathHelper.Clamp(Timer / AimTime, 0f, 1f);
            effect.CurrentTechnique = tech;
            effect.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            //昼把色相钉在金（0.1），夜走本枪色相
            effect.Parameters["uHue"]?.SetValue(MathHelper.Lerp(Hue, 0.1f, DayBlend));
            effect.Parameters["uProgress"]?.SetValue(progress);
            effect.Parameters["uOpacity"]?.SetValue(Projectile.Opacity * 0.85f);

            Vector2 dir = Angle.ToRotationVector2();
            Vector2 perp = dir.RotatedBy(MathHelper.PiOver2);
            Vector2 start = Projectile.Center - dir * 200f;
            Vector2 end = Projectile.Center + dir * TelegraphLength;
            float laneHalf = 26f;
            VertexPositionColorTexture[] verts = new VertexPositionColorTexture[4];
            verts[0] = new VertexPositionColorTexture((start + perp * laneHalf).ToVector3(), Color.White, new Vector2(0f, 0f));
            verts[1] = new VertexPositionColorTexture((start - perp * laneHalf).ToVector3(), Color.White, new Vector2(0f, 1f));
            verts[2] = new VertexPositionColorTexture((end + perp * laneHalf).ToVector3(), Color.White, new Vector2(1f, 0f));
            verts[3] = new VertexPositionColorTexture((end - perp * laneHalf).ToVector3(), Color.White, new Vector2(1f, 1f));
            foreach (EffectPass pass in tech.Passes) {
                pass.Apply();
                device.DrawUserPrimitives(PrimitiveType.TriangleStrip, verts, 0, 2);
            }

            device.BlendState = origBlend;
            device.RasterizerState = origRaster;
        }
    }
}
