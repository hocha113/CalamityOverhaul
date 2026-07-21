using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using InnoVault.Trails;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMechanicalEye.Projectiles
{
    /// <summary>电弧链锁；ai[0]/ai[1]双眼whoAmI；ai[2]持续帧；前30帧无伤预警</summary>
    internal class TwinsTetherArc : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder2;

        [VaultLoaden(CWRConstant.Masking + "ThunderTrail")]
        private static Asset<Texture2D> ThunderTex = null;

        private const int WarmupTime = 30;
        private const int FadeTime = 12;
        private const int ArcPointCount = 14;

        private ref float Timer => ref Projectile.localAI[0];
        private NPC EyeA => ((int)Projectile.ai[0]).TryGetNPC(out NPC n) ? n : null;
        private NPC EyeB => ((int)Projectile.ai[1]).TryGetNPC(out NPC n) ? n : null;
        private int Duration => (int)Projectile.ai[2];

        private ThunderTrail mainTrail;
        private ThunderTrail coreTrail;
        private float power;  //0~1 当前功率

        internal static Color ArcColor => new(140, 215, 255);

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 3200;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 26;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 1200;
            CooldownSlot = ImmunityCooldownID.Bosses;
        }

        public override void AI() {
            NPC eyeA = EyeA;
            NPC eyeB = EyeB;

            //任一眼失效→快速消散
            if (!eyeA.Alives() || !eyeB.Alives()) {
                if (Timer < Duration - FadeTime) {
                    Timer = Duration - FadeTime;
                }
                if (!eyeA.Alives() && !eyeB.Alives()) {
                    Projectile.Kill();
                    return;
                }
            }

            if (Timer == 0 && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item93 with { Volume = 0.9f, Pitch = -0.2f }, Projectile.Center);
            }

            //锚定在两眼中点
            if (eyeA.Alives() && eyeB.Alives()) {
                Projectile.Center = (eyeA.Center + eyeB.Center) / 2f;
            }

            //功率，预警→展开→消散
            if (Timer < WarmupTime) {
                power = Timer / WarmupTime * 0.25f;
            }
            else if (Timer >= Duration - FadeTime) {
                power = MathHelper.Lerp(1f, 0f, (Timer - (Duration - FadeTime)) / FadeTime);
            }
            else {
                float t = MathHelper.Clamp((Timer - WarmupTime) / 12f, 0f, 1f);
                power = MathHelper.Lerp(0.25f, 1f, VaultUtils.EaseOutCubic(t));
            }

            //全功率瞬间的爆鸣
            if ((int)Timer == WarmupTime && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item122 with { Volume = 1.1f }, Projectile.Center);
            }

            Timer++;
            if (Timer >= Duration) {
                Projectile.Kill();
                return;
            }

            if (VaultUtils.isServer || !eyeA.Alives() || !eyeB.Alives()) {
                return;
            }

            //重建电弧路径
            BuildArcPath(eyeA.Center, eyeB.Center);

            //沿线光照与飞溅火花
            for (int i = 0; i < 5; i++) {
                Lighting.AddLight(Vector2.Lerp(eyeA.Center, eyeB.Center, i / 4f), ArcColor.ToVector3() * 0.6f * power);
            }
            if (power > 0.5f && Main.rand.NextBool(3)) {
                Vector2 sparkPos = Vector2.Lerp(eyeA.Center, eyeB.Center, Main.rand.NextFloat());
                PRTLoader.NewParticle<PRT_TwinsSpark>(sparkPos,
                    Main.rand.NextVector2Circular(5f, 5f), Color.White, Main.rand.NextFloat(0.9f, 1.5f))?.Configure(15, 0);
            }
        }

        /// <summary>两眼间采样并扰动电弧路径</summary>
        private void BuildArcPath(Vector2 start, Vector2 end) {
            Vector2[] points = new Vector2[ArcPointCount];
            Vector2 dir = end - start;
            Vector2 perp = dir.SafeNormalize(Vector2.UnitY).RotatedBy(MathHelper.PiOver2);
            float waveSeed = Main.GlobalTimeWrappedHourly * 9f;

            for (int i = 0; i < ArcPointCount; i++) {
                float t = i / (float)(ArcPointCount - 1);
                //两端固定，中段正弦摆动
                float envelope = (float)Math.Sin(t * MathHelper.Pi);
                float wave = (float)Math.Sin(waveSeed + t * 11f) * 16f * envelope * power;
                points[i] = start + dir * t + perp * wave;
            }

            if (mainTrail == null) {
                mainTrail = new ThunderTrail(ThunderTex, GetMainWidth, GetMainColor, GetArcAlpha) {
                    CanDraw = true,
                    UseNonOrAdd = true,
                    PartitionPointCount = 3,
                };
                mainTrail.SetRange((0, 10));
                mainTrail.SetExpandWidth(6);

                coreTrail = new ThunderTrail(ThunderTex, GetCoreWidth, GetCoreColor, GetArcAlpha) {
                    CanDraw = true,
                    UseNonOrAdd = true,
                    PartitionPointCount = 2,
                };
                coreTrail.SetRange((0, 5));
                coreTrail.SetExpandWidth(3);
            }

            mainTrail.BasePositions = points;
            coreTrail.BasePositions = points;
            if ((int)Timer % 3 == 0) {
                mainTrail.RandomThunder();
                coreTrail.RandomThunder();
            }
        }

        private float GetMainWidth(float factor) => (18f + 10f * (float)Math.Sin(factor * MathHelper.Pi)) * power;
        private float GetCoreWidth(float factor) => (7f + 4f * (float)Math.Sin(factor * MathHelper.Pi)) * power;
        private Color GetMainColor(float factor) => ArcColor;
        private Color GetCoreColor(float factor) => Color.White;
        private float GetArcAlpha(float factor) => power;

        //预警期无伤害
        public override bool? CanDamage() => power >= 0.5f ? null : false;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            NPC eyeA = EyeA;
            NPC eyeB = EyeB;
            if (!eyeA.Alives() || !eyeB.Alives()) {
                return false;
            }
            float p = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(),
                eyeA.Center, eyeB.Center, 34f * power, ref p);
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            target.AddBuff(BuffID.Electrified, 120);
        }

        public override bool PreDraw(ref Color lightColor) {
            if (power <= 0.02f) {
                return false;
            }

            mainTrail?.DrawThunder(Main.instance.GraphicsDevice);
            coreTrail?.DrawThunder(Main.instance.GraphicsDevice);

            //两端连接点辉光
            NPC eyeA = EyeA;
            NPC eyeB = EyeB;
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Color glowColor = ArcColor with { A = 0 };
            float pulse = 1f + 0.15f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 26f);
            if (eyeA.Alives()) {
                Main.EntitySpriteDraw(glow, eyeA.Center - Main.screenPosition, null, glowColor * power,
                    0f, glow.Size() / 2f, 0.9f * power * pulse, SpriteEffects.None, 0);
            }
            if (eyeB.Alives()) {
                Main.EntitySpriteDraw(glow, eyeB.Center - Main.screenPosition, null, glowColor * power,
                    0f, glow.Size() / 2f, 0.9f * power * pulse, SpriteEffects.None, 0);
            }

            return false;
        }
    }
}
