using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.SummonMinions.Projectiles
{
    /// <summary>
    /// 回航血弧：血蝠从猎物身上撕下的一弯血浆凝刃，向主人飞回，沿途割伤挡路者。
    /// 生命周期 = 撕出（速度快、弧体拉长）/ 回航（软寻的主人，血珠滴落）/
    /// 归怀（贴近主人散作血雾，无伤害）。材质：血浆凝成的月牙刃，边缘暗红芯发亮
    /// </summary>
    internal class GsSanguineArcProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        public override string LocalizationCategory => "GodSmithSummonMinionsB";

        private static readonly Color BloodBright = new(255, 92, 92);
        private static readonly Color BloodDeep = new(150, 22, 34);
        private static readonly Color BloodPale = new(255, 176, 168);

        private ref float Life => ref Projectile.localAI[0];

        private float Seed => Projectile.identity * 0.7391f % MathHelper.TwoPi;

        public override void SetDefaults() {
            Projectile.width = 26;
            Projectile.height = 26;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 90;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 18;
        }

        public override void AI() {
            Life++;
            if (Life == 1f && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.NPCHit18 with { Volume = 0.5f, Pitch = -0.25f },
                    Projectile.Center);
            }
            Player owner = Main.player[Projectile.owner];
            if (!owner.active || owner.dead) {
                Projectile.Kill();
                return;
            }
            //回航寻的：目标速度指向主人，前 12 帧保留撕出冲势后逐渐接管
            Vector2 want = (owner.Center - Projectile.Center).SafeNormalize(Vector2.UnitX);
            float speed = MathHelper.Lerp(8.5f, 13f, MathHelper.Clamp(Life / 40f, 0f, 1f));
            float steer = Life < 12f ? 0.05f : 0.14f;
            Projectile.velocity = Vector2.Lerp(Projectile.velocity, want * speed, steer);
            Projectile.rotation = Projectile.velocity.ToRotation();

            //归怀：贴近主人即散作血雾
            if (Life > 8f && Projectile.Center.Distance(owner.Center) < 26f) {
                Projectile.Kill();
                return;
            }

            if (VaultUtils.isServer) {
                return;
            }
            Lighting.AddLight(Projectile.Center, BloodDeep.ToVector3() * 0.24f);
            //回航血珠：低频滴落，受重力下坠
            if (Life % 4f == 0f) {
                PRTLoader.NewParticle<PRT_Spark>(
                    Projectile.Center - Projectile.velocity * 0.4f,
                    new Vector2(Main.rand.NextFloat(-0.3f, 0.3f), 0.6f),
                    BloodDeep, Main.rand.NextFloat(0.18f, 0.3f))?.Configure(true, Main.rand.Next(14, 22));
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (VaultUtils.isServer) {
                return;
            }
            //割伤反馈：顺刃向血滴喷洒
            for (int i = 0; i < 4; i++) {
                PRTLoader.NewParticle<PRT_Spark>(target.Center,
                    Projectile.velocity.SafeNormalize(Vector2.UnitX)
                        .RotatedBy(Main.rand.NextFloat(-0.5f, 0.5f)) * Main.rand.NextFloat(2f, 4f),
                    BloodBright, Main.rand.NextFloat(0.22f, 0.36f))?.Configure(true, Main.rand.Next(12, 18));
            }
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            //归怀血雾：柔散不喧宾
            for (int i = 0; i < 5; i++) {
                PRTLoader.NewParticle<PRT_Light>(
                    Projectile.Center + Main.rand.NextVector2Circular(8f, 8f),
                    Main.rand.NextVector2Circular(1.2f, 1.2f),
                    BloodDeep, Main.rand.NextFloat(0.08f, 0.14f))?.Configure(12, 0.6f);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D soft = CWRAsset.Extra_98?.Value;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (soft == null || glow == null) {
                return false;
            }
            Vector2 pos = Projectile.Center - Main.screenPosition;
            float rot = Projectile.rotation;
            float fadeIn = MathHelper.Clamp(Life / 6f, 0f, 1f);
            //撕出瞬间弧体被速度拉长，回航期回落
            float stretch = 1f + MathHelper.Clamp((Projectile.velocity.Length() - 8f) * 0.1f, 0f, 0.5f);
            float flutter = 0.1f * (float)Math.Sin(Life * 0.5f + Seed);

            //月牙 = 三片错角薄弧叠出弯刃（上下两翼 + 中脊），暗红垫底、亮红作芯
            for (int i = -1; i <= 1; i++) {
                float wing = i * (0.42f + flutter * 0.4f);
                Color c = i == 0 ? BloodBright * 0.9f : BloodDeep * 0.75f;
                float len = (i == 0 ? 42f : 34f) * stretch;
                Main.EntitySpriteDraw(soft, pos, null, c * fadeIn, rot + wing,
                    soft.Size() / 2f,
                    new Vector2(len / soft.Width, (i == 0 ? 7f : 5f) / soft.Height),
                    SpriteEffects.None, 0);
            }
            //刃尖苍白高光（加色）
            Main.EntitySpriteDraw(soft, pos + rot.ToRotationVector2() * 12f * stretch, null,
                (BloodPale with { A = 0 }) * (0.7f * fadeIn), rot,
                soft.Size() / 2f, new Vector2(16f / soft.Width, 3f / soft.Height),
                SpriteEffects.None, 0);
            //血辉底光
            Main.EntitySpriteDraw(glow, pos, null, (BloodDeep with { A = 0 }) * (0.4f * fadeIn),
                0f, glow.Size() / 2f, 0.5f, SpriteEffects.None, 0);
            return false;
        }
    }
}
