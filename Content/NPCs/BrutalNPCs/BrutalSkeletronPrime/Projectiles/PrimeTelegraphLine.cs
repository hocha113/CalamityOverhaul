using CalamityOverhaul.Common;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime.Projectiles
{
    /// <summary>预警线/扇/环；ai[0]模式+扇半角，ai[1]转角或半径，ai[2]时长；扇环分technique</summary>
    internal class PrimeTelegraphLine : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder2;

        internal static float LineLength => 1150f;
        internal static float FanRadius => 880f;
        internal static float DefaultFanHalfAngle => 0.45f;

        private int Mode => (int)Projectile.ai[0];
        private float FanHalfAngle {
            get {
                float frac = Projectile.ai[0] - Mode;
                return frac > 0.001f ? frac * 10f : DefaultFanHalfAngle;
            }
        }
        private int Duration => (int)System.Math.Max(Projectile.ai[2], 1f);
        /// <summary>充能进度，timeLeft推导</summary>
        private float Progress => MathHelper.Clamp(1f - Projectile.timeLeft / (float)Duration, 0f, 1f);

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 2400;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 2;
            Projectile.hostile = false;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 60;
            Projectile.netImportant = true;
        }

        public override bool? CanDamage() => false;

        public override void AI() {
            Lighting.AddLight(Projectile.Center, new Vector3(0.5f, 0.12f, 0.04f) * (0.4f + 0.6f * Progress));
            if (Projectile.localAI[0] >= 0 && Projectile.localAI[0].TryGetNPC(out var npc) && npc.type == Projectile.localAI[1]) {
                Projectile.Center = npc.Center;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            switch (Mode) {
                case 0:
                    DrawDashTelegraph();
                    break;
                case 1:
                    DrawShaderQuad("FanTech", FanQuad());
                    break;
                default:
                    DrawShaderQuad("RingTech", RingQuad());
                    break;
            }
            return false;
        }

        #region 线模式冲刺预判

        /// <summary>冲刺预判线，末20%白热</summary>
        private void DrawDashTelegraph() {
            Texture2D line = CWRAsset.MaskLaserLine.Value;
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Vector2 origin = new(0f, line.Height / 2f);
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            float rot = Projectile.ai[1];
            float progress = Progress;
            float pulse = 0.85f + 0.15f * (float)System.Math.Sin(Main.GlobalTimeWrappedHourly * 9f + Projectile.whoAmI * 0.7f);

            //末20%白热
            float flash = MathHelper.Clamp((progress - 0.8f) / 0.2f, 0f, 1f);
            Color baseCol = Color.Lerp(new Color(255, 40, 15), new Color(255, 200, 110), flash) with { A = 0 };
            Color hotCol = Color.Lerp(new Color(255, 150, 40), Color.White, flash) with { A = 0 };

            float lenScale = LineLength / line.Width;

            //层1基线
            Main.EntitySpriteDraw(line, drawPos, null, baseCol * (0.5f * pulse),
                rot, origin, new Vector2(lenScale, 0.22f), SpriteEffects.None, 0);

            //层2充能段
            if (progress > 0.02f) {
                Rectangle chargeSrc = new(0, 0, (int)(line.Width * progress), line.Height);
                Main.EntitySpriteDraw(line, drawPos, chargeSrc, hotCol * 0.9f,
                    rot, origin, new Vector2(lenScale, 0.42f * pulse), SpriteEffects.None, 0);
            }

            //层3前端光点
            Vector2 tip = drawPos + rot.ToRotationVector2() * LineLength * progress;
            Main.EntitySpriteDraw(glow, tip, null, hotCol * 0.9f,
                0f, glow.Size() / 2f, 0.55f + 0.3f * flash, SpriteEffects.None, 0);

            //层4根部辉光
            Main.EntitySpriteDraw(glow, drawPos, null, baseCol * 0.8f,
                0f, glow.Size() / 2f, 0.7f * pulse, SpriteEffects.None, 0);
        }

        #endregion

        #region 扇环technique面片

        private readonly record struct QuadInfo(Vector2 Origin, Vector2 Scale, float Rotation);

        private QuadInfo FanQuad() {
            Texture2D quad = VaultAsset.placeholder2.Value;
            //quad高=2×长
            return new QuadInfo(new Vector2(0f, quad.Height / 2f),
                new Vector2(FanRadius / quad.Width, FanRadius * 2f / quad.Height), Projectile.ai[1]);
        }

        private QuadInfo RingQuad() {
            Texture2D quad = VaultAsset.placeholder2.Value;
            //边长=半径×2.6，主环r=0.77
            float size = Projectile.ai[1] * 2.6f;
            return new QuadInfo(quad.Size() / 2f,
                new Vector2(size / quad.Width, size / quad.Height), 0f);
        }

        private void DrawShaderQuad(string technique, QuadInfo info) {
            Effect shader = EffectLoader.PrimeTelegraph?.Value;
            if (shader == null) {
                DrawFallback();
                return;
            }

            SpriteBatch sb = Main.spriteBatch;
            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.Additive, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            shader.CurrentTechnique = shader.Techniques[technique];
            shader.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            shader.Parameters["uProgress"]?.SetValue(Progress);
            shader.Parameters["uIntensity"]?.SetValue(1f);
            shader.Parameters["uFanAngle"]?.SetValue(FanHalfAngle);
            shader.CurrentTechnique.Passes[0].Apply();

            Texture2D quad = VaultAsset.placeholder2.Value;
            sb.Draw(quad, Projectile.Center - Main.screenPosition, null, Color.White,
                info.Rotation, info.Origin, info.Scale, SpriteEffects.None, 0f);

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
        }

        /// <summary>扇/环贴图兜底</summary>
        private void DrawFallback() {
            float alpha = 0.3f + 0.5f * Progress;
            Color color = new Color(255, 60, 20, 0) * alpha;

            if (Mode == 2) {
                Texture2D circle = CWRAsset.DiffusionCircle.Value;
                float scale = Projectile.ai[1] * 2f / circle.Width;
                Main.EntitySpriteDraw(circle, Projectile.Center - Main.screenPosition, null, color,
                    0f, circle.Size() / 2f, scale, SpriteEffects.None, 0);
                return;
            }

            Texture2D line = CWRAsset.MaskLaserLine.Value;
            Vector2 origin = new(0f, line.Height / 2f);
            //扇形兜底边界线
            Main.EntitySpriteDraw(line, Projectile.Center - Main.screenPosition, null, color,
                Projectile.ai[1] - FanHalfAngle, origin, new Vector2(FanRadius / line.Width, 0.16f), SpriteEffects.None, 0);
            Main.EntitySpriteDraw(line, Projectile.Center - Main.screenPosition, null, color,
                Projectile.ai[1] + FanHalfAngle, origin, new Vector2(FanRadius / line.Width, 0.16f), SpriteEffects.None, 0);
        }

        #endregion

        public override void DrawBehind(int index, List<int> behindNPCsAndTiles, List<int> behindNPCs,
            List<int> behindProjectiles, List<int> overPlayers, List<int> overWiresUI) => behindNPCsAndTiles.Add(index);

        #region 生成助手（服务端裁决，netImportant 同步到客户端）

        /// <summary>方向线预警</summary>
        internal static void SpawnLine(NPC owner, Vector2 center, float rotation, int duration, bool fower = false) {
            if (VaultUtils.isClient) {
                return;
            }
            int id = Projectile.NewProjectile(owner.GetSource_FromAI(), center, Vector2.Zero,
                ModContent.ProjectileType<PrimeTelegraphLine>(), 0, 0f, Main.myPlayer,
                0f, rotation, duration);
            if (fower && id.TryGetProjectile(out var proj)) {
                proj.localAI[0] = owner.whoAmI;
                proj.localAI[1] = owner.type;
            }
            ApplyDuration(id, duration);
        }

        /// <summary>扇形预警</summary>
        internal static void SpawnFan(NPC owner, Vector2 center, float rotation, float halfAngle, int duration, bool fower = false) {
            if (VaultUtils.isClient) {
                return;
            }
            float encoded = 1f + MathHelper.Clamp(halfAngle, 0.05f, 1.4f) / 10f;
            int id = Projectile.NewProjectile(owner.GetSource_FromAI(), center, Vector2.Zero,
                ModContent.ProjectileType<PrimeTelegraphLine>(), 0, 0f, Main.myPlayer,
                encoded, rotation, duration);
            if (fower && id.TryGetProjectile(out var proj)) {
                proj.localAI[0] = owner.whoAmI;
                proj.localAI[1] = owner.type;
            }
            ApplyDuration(id, duration);
        }

        /// <summary>圆环预警</summary>
        internal static void SpawnRing(NPC owner, Vector2 center, float radiusPx, int duration, bool fower = false) {
            if (VaultUtils.isClient) {
                return;
            }
            int id = Projectile.NewProjectile(owner.GetSource_FromAI(), center, Vector2.Zero,
                ModContent.ProjectileType<PrimeTelegraphLine>(), 0, 0f, Main.myPlayer,
                2f, radiusPx, duration);
            if (fower && id.TryGetProjectile(out var proj)) {
                proj.localAI[0] = owner.whoAmI;
                proj.localAI[1] = owner.type;
            }
            ApplyDuration(id, duration);
        }

        private static void ApplyDuration(int id, int duration) {
            if (id >= 0 && id < Main.maxProjectiles) {
                Main.projectile[id].timeLeft = duration;
                Main.projectile[id].netUpdate = true;
            }
        }

        #endregion
    }
}
