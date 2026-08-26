using CalamityOverhaul.Common;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee.DestroyersBladeEXs
{
    /// <summary>
    /// 红白能量光束:白热芯 + 血红鞘,复用 DestroyerBeam.fx 的炽白色板。
    /// ai[0]=初始化闩 ai[1]=歼灭协议(1 时获得追踪与穿透强化)
    /// </summary>
    internal class DestroyersBeamEX : ModProjectile, IPrimitiveDrawable
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private bool Empowered => Projectile.ai[1] > 0f;
        private ref float Init => ref Projectile.ai[0];

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 26;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = 2;
            Projectile.width = 26;
            Projectile.height = 26;
            Projectile.timeLeft = 300;
            Projectile.friendly = true;
            Projectile.ignoreWater = true;
            Projectile.extraUpdates = 2;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override void AI() {
            if (Init == 0) {
                //发射不响,声音留给挥砍与命中
                Init = 1;
                if (Empowered) {
                    Projectile.penetrate = 4;
                    Projectile.scale = 1.15f;
                }
            }

            //歼灭协议:光束追踪最近猎物
            if (Empowered) {
                int target = FindHomingTarget(700f);
                if (target >= 0) {
                    Vector2 want = (Main.npc[target].Center - Projectile.Center)
                        .SafeNormalize(Vector2.Zero) * Projectile.velocity.Length();
                    Projectile.velocity = Vector2.Lerp(Projectile.velocity, want, 0.045f);
                }
            }

            Projectile.rotation = Projectile.velocity.ToRotation();

            //沿途电火花
            if (!VaultUtils.isServer && Main.rand.NextBool(8)) {
                Dust d = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(6f, 6f)
                    , DustID.RedTorch, -Projectile.velocity * 0.1f, 100, default, 1.1f);
                d.noGravity = true;
            }

            Lighting.AddLight(Projectile.Center, new Vector3(1f, 0.35f, 0.25f) * 1.1f * Main.essScale);
        }

        private int FindHomingTarget(float range) {
            int best = -1;
            float bestDist = range;
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (npc?.active != true || !npc.CanBeChasedBy()) {
                    continue;
                }
                float dist = Vector2.Distance(npc.Center, Projectile.Center);
                if (dist < bestDist) {
                    bestDist = dist;
                    best = i;
                }
            }
            return best;
        }

        public override void OnKill(int timeLeft) {
            //爆响压量限流,齐射连爆不叠成噪墙
            Projectile.Explode(Empowered ? 150 : 110, SoundID.Item14 with { Volume = 0.38f, Pitch = 0.1f, MaxInstances = 3, SoundLimitBehavior = SoundLimitBehavior.IgnoreNew });
            if (Main.dedServ) {
                return;
            }
            Color warm = new Color(255, 120, 80);
            PRTLoader.NewParticle<PRT_MechExplosion>(Projectile.Center, Main.rand.NextVector2Circular(1f, 1f)
                , warm, Empowered ? 0.75f : 0.5f).Configure(Main.rand.Next(16, 26), warm);
            for (int i = 0; i < 9; i++) {
                PRTLoader.NewParticle<PRT_SparkAlpha>(Projectile.Center, Main.rand.NextVector2Circular(7f, 7f)
                    , Main.rand.NextBool(3) ? Color.White : new Color(255, 70, 50)
                    , Main.rand.NextFloat(1f, 2f)).Configure(false, Main.rand.Next(10, 16));
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            //弹头:白热核 + 红晕 + 十字耀斑
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Color outer = new Color(255, 60, 40);
            outer.A = 0;
            Main.EntitySpriteDraw(glow, drawPos, null, outer, 0f, glow.Size() / 2f
                , (Empowered ? 1.15f : 0.85f) * Projectile.scale, SpriteEffects.None, 0);
            Color core = new Color(255, 235, 225);
            core.A = 0;
            Main.EntitySpriteDraw(glow, drawPos, null, core, 0f, glow.Size() / 2f
                , 0.4f * Projectile.scale, SpriteEffects.None, 0);

            Texture2D star = CWRAsset.StarTexture.Value;
            Color starColor = new Color(255, 210, 190);
            starColor.A = 0;
            Main.EntitySpriteDraw(star, drawPos, null, starColor * 0.85f, Projectile.rotation
                , star.Size() / 2f, 0.17f * Projectile.scale, SpriteEffects.None, 0);
            return false;
        }

        void IPrimitiveDrawable.DrawPrimitives() {
            Effect effect = EffectLoader.DestroyerBeam?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (effect == null || noise == null || Projectile.oldPos == null) {
                return;
            }

            int valid = 0;
            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero) {
                    break;
                }
                valid++;
            }
            if (valid < 3) {
                return;
            }

            float halfWidth = (Empowered ? 26f : 19f) * Projectile.scale;
            var bars = new VertexPositionColorTexture[valid * 2];
            for (int i = 0; i < valid; i++) {
                Vector2 pos = Projectile.oldPos[i] + Projectile.Size / 2f;
                Vector2 next = i == 0
                    ? Projectile.Center + Projectile.velocity
                    : Projectile.oldPos[i - 1] + Projectile.Size / 2f;
                Vector2 dir = (next - pos).SafeNormalize(Projectile.rotation.ToRotationVector2());
                Vector2 perp = dir.RotatedBy(MathHelper.PiOver2);

                float factor = 1f - i / (float)valid; //1=弹头 0=尾部
                float width = halfWidth * (0.35f + 0.65f * factor);
                bars[i * 2] = new VertexPositionColorTexture((pos + perp * width).ToVector3()
                    , Color.White, new Vector2(factor, 0f));
                bars[i * 2 + 1] = new VertexPositionColorTexture((pos - perp * width).ToVector3()
                    , Color.White, new Vector2(factor, 1f));
            }

            GraphicsDevice device = Main.graphics.GraphicsDevice;
            BlendState origBlend = device.BlendState;
            RasterizerState origRaster = device.RasterizerState;
            device.BlendState = BlendState.Additive;
            device.RasterizerState = RasterizerState.CullNone;

            //炽白色板:exMode=1 是本武器系的红白等离子档
            effect.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            effect.Parameters["fadeAlpha"]?.SetValue(1f);
            effect.Parameters["exMode"]?.SetValue(1f);
            effect.Parameters["seed"]?.SetValue(Projectile.whoAmI * 0.137f % 1f);
            effect.Parameters["uNoiseTex"]?.SetValue(noise);
            foreach (EffectPass pass in effect.CurrentTechnique.Passes) {
                pass.Apply();
                device.DrawUserPrimitives(PrimitiveType.TriangleStrip, bars, 0, bars.Length - 2);
            }

            device.BlendState = origBlend;
            device.RasterizerState = origRaster;
        }

        public override void PostDraw(Color lightColor)
            => Lighting.AddLight(Projectile.Center, new Vector3(1f, 0.5f, 0.4f) * 1.6f * Main.essScale);
    }
}
