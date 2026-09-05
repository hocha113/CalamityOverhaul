using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Modifys.RTerraBlades
{
    /// <summary>
    /// 真永夜之刃之魂：泰拉之光的夜魂命中后，一柄夜化的真永夜之刃在命中处高速回旋，缓缓追着目标，连斩数次后散去<br/>
    /// 旋转靠旋转残影表达（不是贴图原地转），本体夜紫真 alpha 剪影带一层紫光镀<br/>
    /// ai[0]=目标 NPC 序号（缓追）
    /// </summary>
    internal class TerraSoulSpinProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        /// <summary>每次命中占泰拉之光伤害比例</summary>
        internal const float DamageMul = 0.24f;
        private const int Lifetime = 42;
        private const float SpinRate = 0.42f;
        private const float DriftSpeed = 1.8f;
        private const float DriftRange = 420f;

        private int TargetIndex => (int)Projectile.ai[0];
        private int Age => Lifetime - Projectile.timeLeft;
        private float SpinDir => Projectile.identity % 2 == 0 ? 1f : -1f;

        /// <summary>出生 4 帧撑开并小幅过冲，末 8 帧收拢</summary>
        private float ScaleEnvelope {
            get {
                float grow = MathHelper.Clamp(Age / 4f, 0f, 1f);
                float pop = grow < 0.7f ? 1.1f * (grow / 0.7f) : MathHelper.Lerp(1.1f, 1f, (grow - 0.7f) / 0.3f);
                float shrink = MathHelper.Clamp(Projectile.timeLeft / 8f, 0f, 1f);
                return pop * shrink;
            }
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 76;
            Projectile.aiStyle = -1;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.usesLocalNPCImmunity = true;
            //同一目标最多吃三段
            Projectile.localNPCHitCooldown = 12;
            Projectile.timeLeft = Lifetime;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
        }

        public override void AI() {
            if (Age == 0 && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item8 with { Volume = 0.5f, Pitch = -0.5f }, Projectile.Center);
                SoundEngine.PlaySound(SoundID.Item71 with { Volume = 0.4f, Pitch = -0.4f }, Projectile.Center);
                PRTLoader.NewParticle<PRT_StarPulseRing>(Projectile.Center, Vector2.Zero, RTerraBlade.NightViolet, 0f)
                    ?.Configure(0.04f, 0.5f, 11);
            }

            Projectile.rotation += SpinDir * SpinRate;

            //缓追目标：夜刃像活物一样贴上去，目标没了就原地滑停
            if (TargetIndex >= 0 && TargetIndex < Main.maxNPCs && Main.npc[TargetIndex].active
                && Projectile.Distance(Main.npc[TargetIndex].Center) < DriftRange) {
                Vector2 to = (Main.npc[TargetIndex].Center - Projectile.Center).SafeNormalize(Vector2.Zero);
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, to * DriftSpeed, 0.12f);
            }
            else {
                Projectile.velocity *= 0.9f;
            }

            if (VaultUtils.isServer) {
                return;
            }
            Lighting.AddLight(Projectile.Center, RTerraBlade.NightViolet.ToVector3() * 0.9f);
            if (Main.rand.NextBool(2)) {
                //刃尖沿切向甩夜屑
                float edgeAng = Projectile.rotation - MathHelper.PiOver4 + (Main.rand.NextBool() ? 0f : MathHelper.Pi);
                Vector2 tip = Projectile.Center + edgeAng.ToRotationVector2() * (30f * ScaleEnvelope);
                Vector2 tangent = edgeAng.ToRotationVector2().RotatedBy(SpinDir * MathHelper.PiOver2);
                PRTLoader.NewParticle<PRT_Spark>(tip, tangent * Main.rand.NextFloat(2f, 5f)
                    , Main.rand.NextBool(3) ? RTerraBlade.SoulBright(0f) : RTerraBlade.NightViolet
                    , Main.rand.NextFloat(0.6f, 1f))?.Configure(false, Main.rand.Next(8, 14));
            }
        }

        /// <summary>圆形判定跟着缩放走，收拢期不再咬人</summary>
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            float env = ScaleEnvelope;
            if (env < 0.5f) {
                return false;
            }
            return targetHitbox.Distance(Projectile.Center) <= 40f * env;
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            int dir = Math.Sign(target.Center.X - Projectile.Center.X);
            modifiers.HitDirectionOverride = dir == 0 ? 1 : dir;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (VaultUtils.isServer) {
                return;
            }
            for (int i = 0; i < 4; i++) {
                PRTLoader.NewParticle<PRT_SparkAlpha>(target.Center, Main.rand.NextVector2Circular(5f, 5f)
                    , Main.rand.NextBool(3) ? RTerraBlade.SoulBright(0f) : RTerraBlade.NightViolet
                    , Main.rand.NextFloat(0.7f, 1.1f))?.Configure(false, Main.rand.Next(10, 16));
            }
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            for (int i = 0; i < 6; i++) {
                PRTLoader.NewParticle<PRT_Light>(Projectile.Center + Main.rand.NextVector2Circular(16f, 16f)
                    , Main.rand.NextVector2Circular(1.2f, 1.2f), RTerraBlade.NightViolet
                    , Main.rand.NextFloat(0.12f, 0.18f))?.Configure(Main.rand.Next(12, 18), 0.9f);
            }
        }

        /// <summary>夜化真永夜之刃：紫光底垫 + 四道旋转残影 + 夜紫本体 + 紫光镀层</summary>
        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = TerraBladeFX.ItemTex(ItemID.TrueNightsEdge);
            Texture2D glow = TerraBladeFX.SoftGlow;
            Vector2 origin = tex.Size() / 2f;
            Vector2 pos = Projectile.Center - Main.screenPosition;
            float env = ScaleEnvelope;
            if (env <= 0.01f) {
                return false;
            }
            float scale = 1.35f * env;

            if (glow != null) {
                Color under = RTerraBlade.NightViolet * (0.35f * env);
                under.A = 0;
                Main.EntitySpriteDraw(glow, pos, null, under, 0f, glow.Size() / 2f, 1.1f * env, SpriteEffects.None, 0);
            }

            //旋转残影：真 alpha 夜紫剪影按角距回溯，越旧越淡
            for (int k = 4; k >= 1; k--) {
                float ghostRot = Projectile.rotation - SpinDir * SpinRate * 0.75f * k;
                Color gc = RTerraBlade.NightDeep * (0.55f - 0.11f * k) * env;
                Main.EntitySpriteDraw(tex, pos, null, gc, ghostRot, origin, scale, SpriteEffects.None, 0);
            }

            Color body = new Color(190, 150, 255) * env;
            Main.EntitySpriteDraw(tex, pos, null, body, Projectile.rotation, origin, scale, SpriteEffects.None, 0);
            Color sheath = RTerraBlade.NightViolet * (0.45f * env);
            sheath.A = 0;
            Main.EntitySpriteDraw(tex, pos, null, sheath, Projectile.rotation, origin, scale * 1.07f, SpriteEffects.None, 0);
            return false;
        }
    }
}
