using CalamityOverhaul.Common;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Ranged.Starships
{
    //从玩家身后发射的行星弹幕，共八种形态（按 ai[0] 区分），使用 CelestialStar 着色器渲染天体
    internal class StarshipPlanet : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;

        //行星数据：核心色、表面色、日冕色、尾迹色、应用的 debuff（取 CWRID）
        private struct PlanetData
        {
            public Vector3 Core;
            public Vector3 Surface;
            public Vector3 Corona;
            public Vector3 Trail;
            public float Radius;
            public int DebuffId;
            public int DebuffDuration;
        }

        private static PlanetData GetData(int kind) => kind switch {
            0 => new PlanetData { //火星：双足翼龙诅咒
                Core = new Vector3(1f, 0.75f, 0.5f), Surface = new Vector3(0.95f, 0.45f, 0.25f),
                Corona = new Vector3(1f, 0.35f, 0.15f), Trail = new Vector3(1f, 0.3f, 0.1f),
                Radius = 0.23f, DebuffId = CWRID.Buff_PearlAura, DebuffDuration = 300
            },
            1 => new PlanetData { //海王星：富营养化 + 冰河时代
                Core = new Vector3(0.75f, 0.9f, 1f), Surface = new Vector3(0.3f, 0.6f, 0.95f),
                Corona = new Vector3(0.5f, 0.85f, 1f), Trail = new Vector3(0.2f, 0.5f, 1f),
                Radius = 0.25f, DebuffId = CWRID.Buff_GlacialState, DebuffDuration = 300
            },
            2 => new PlanetData { //木星：粉碎
                Core = new Vector3(1f, 0.85f, 0.55f), Surface = new Vector3(0.85f, 0.6f, 0.4f),
                Corona = new Vector3(0.9f, 0.7f, 0.4f), Trail = new Vector3(0.8f, 0.5f, 0.3f),
                Radius = 0.28f, DebuffId = CWRID.Buff_CrushDepth, DebuffDuration = 300
            },
            3 => new PlanetData { //冥王星：死亡低语
                Core = new Vector3(0.75f, 0.7f, 0.85f), Surface = new Vector3(0.5f, 0.4f, 0.65f),
                Corona = new Vector3(0.6f, 0.45f, 0.75f), Trail = new Vector3(0.35f, 0.25f, 0.55f),
                Radius = 0.2f, DebuffId = CWRID.Buff_WhisperingDeath, DebuffDuration = 300
            },
            4 => new PlanetData { //水星：莫名的悲伤（Nightwither）
                Core = new Vector3(0.9f, 0.9f, 0.95f), Surface = new Vector3(0.6f, 0.55f, 0.6f),
                Corona = new Vector3(0.55f, 0.5f, 0.7f), Trail = new Vector3(0.35f, 0.3f, 0.5f),
                Radius = 0.19f, DebuffId = CWRID.Buff_Nightwither, DebuffDuration = 300
            },
            5 => new PlanetData { //金星：放逐之火
                Core = new Vector3(1f, 0.9f, 0.6f), Surface = new Vector3(1f, 0.7f, 0.35f),
                Corona = new Vector3(1f, 0.55f, 0.2f), Trail = new Vector3(0.9f, 0.4f, 0.1f),
                Radius = 0.22f, DebuffId = CWRID.Buff_BanishingFire, DebuffDuration = 300
            },
            6 => new PlanetData { //土星：碎甲
                Core = new Vector3(1f, 0.92f, 0.7f), Surface = new Vector3(0.9f, 0.75f, 0.5f),
                Corona = new Vector3(0.95f, 0.85f, 0.5f), Trail = new Vector3(0.8f, 0.7f, 0.35f),
                Radius = 0.24f, DebuffId = CWRID.Buff_ArmorCrunch, DebuffDuration = 300
            },
            _ => new PlanetData {
                Core = new Vector3(0.8f, 0.8f, 1f), Surface = new Vector3(0.5f, 0.6f, 0.9f),
                Corona = new Vector3(0.6f, 0.7f, 1f), Trail = new Vector3(0.4f, 0.5f, 0.9f),
                Radius = 0.2f, DebuffId = BuffID.Confused, DebuffDuration = 240
            },
        };

        private const float BaseShaderSize = 130f;
        private ref float Kind => ref Projectile.ai[0];

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 20;
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 1400;
        }

        public override void SetDefaults() {
            Projectile.width = 48;
            Projectile.height = 48;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 300;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.extraUpdates = 1;
            Projectile.usesIDStaticNPCImmunity = true;
            Projectile.idStaticNPCHitCooldown = 24;
        }

        public override void AI() {
            Projectile.rotation = Projectile.velocity.ToRotation();

            //轻微自引导
            NPC target = FindNearestEnemy(900f);
            if (target != null) {
                Vector2 desired = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitX) * Projectile.velocity.Length();
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, desired, 0.035f);
            }

            PlanetData data = GetData((int)Kind);
            Lighting.AddLight(Projectile.Center, data.Corona * 0.9f);

            if (Main.rand.NextBool()) {
                Vector2 dustVel = Main.rand.NextVector2Circular(1.5f, 1.5f);
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.PinkStarfish, dustVel, 120, new Color(data.Trail), 1.2f);
                d.noGravity = true;
            }
        }

        private NPC FindNearestEnemy(float range) {
            NPC best = null;
            float minDist = range * range;
            foreach (NPC n in Main.npc) {
                if (!n.CanBeChasedBy() || Vector2.DistanceSquared(n.Center, Projectile.Center) > minDist) {
                    continue;
                }
                if (Collision.CanHit(Projectile.Center, 1, 1, n.Center, 1, 1) == false) {
                    continue;
                }
                minDist = Vector2.DistanceSquared(n.Center, Projectile.Center);
                best = n;
            }
            return best;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            PlanetData data = GetData((int)Kind);
            if (data.DebuffId > 0) {
                target.AddBuff(data.DebuffId, data.DebuffDuration);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            PlanetData data = GetData((int)Kind);
            float time = Main.GlobalTimeWrappedHourly;

            //拖尾：使用 SoftGlow 堆叠营造行星光芒带
            Vector2 forward = Projectile.velocity.SafeNormalize(Vector2.UnitX);
            Vector2 backward = -forward;
            for (int i = Projectile.oldPos.Length - 1; i >= 0; i--) {
                if (Projectile.oldPos[i] == Vector2.Zero) {
                    continue;
                }
                float f = (Projectile.oldPos.Length - i) / (float)Projectile.oldPos.Length;
                Vector2 p = Projectile.oldPos[i] + Projectile.Size * 0.5f - Main.screenPosition;
                Color c = new Color(data.Trail) * f * 0.6f;
                c.A = 0;
                sb.Draw(glow, p, null, c, Projectile.rotation, glow.Size() * 0.5f
                    , new Vector2(0.6f + f * 1.8f, 0.55f + f * 0.3f) * 1.6f, SpriteEffects.None, 0);
            }

            //使用 CelestialStar 着色器绘制天体本体
            DrawCelestialBody(sb, drawPos, time, data);

            //前向白炽亮点
            sb.Draw(glow, drawPos + forward * 4f, null, (new Color(data.Core) with { A = 0 }) * 0.5f, 0f
                , glow.Size() * 0.5f, 0.7f, SpriteEffects.None, 0);
            return false;
        }

        private void DrawCelestialBody(SpriteBatch sb, Vector2 drawPos, float time, PlanetData data) {
            Effect shader = EffectLoader.CelestialStar?.Value;
            Texture2D canvas = CWRAsset.Placeholder_White?.Value;
            Texture2D noise = CWRAsset.Extra_193?.Value;
            if (shader == null || canvas == null || noise == null) {
                return;
            }

            shader.CurrentTechnique = shader.Techniques["CelestialBody"];
            shader.Parameters["uTime"]?.SetValue(time + Projectile.whoAmI * 0.37f);
            shader.Parameters["fadeAlpha"]?.SetValue(Math.Min(1f, Projectile.timeLeft / 60f));
            shader.Parameters["fallSpeed"]?.SetValue(Projectile.velocity.Length());
            shader.Parameters["coreColor"]?.SetValue(data.Core);
            shader.Parameters["surfaceColor"]?.SetValue(data.Surface);
            shader.Parameters["coronaColor"]?.SetValue(data.Corona);
            shader.Parameters["trailColor"]?.SetValue(data.Trail);
            shader.Parameters["sphereRadius"]?.SetValue(data.Radius);
            shader.Parameters["coronaWidth"]?.SetValue(0.1f);
            shader.Parameters["intensity"]?.SetValue(1.4f);
            shader.Parameters["impactProgress"]?.SetValue(0f);
            shader.Parameters["impactRadius"]?.SetValue(0f);
            shader.Parameters["uNoiseTex"]?.SetValue(noise);

            Main.graphics.GraphicsDevice.Textures[1] = noise;
            Main.graphics.GraphicsDevice.SamplerStates[1] = SamplerState.LinearWrap;

            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.Additive, SamplerState.LinearWrap
                , DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            shader.CurrentTechnique.Passes[0].Apply();
            sb.Draw(canvas, drawPos, null, Color.White, Projectile.rotation
                , canvas.Size() * 0.5f, BaseShaderSize, SpriteEffects.None, 0);

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState
                , DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
        }
    }
}
