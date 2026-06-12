using CalamityOverhaul.Common;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Boss.SkeletronPrime
{
    /// <summary>
    /// 机械骷髅王通用预警弹幕：线 / 扇形 / 圆环 三模式，纯视觉不可伤害。
    /// <br/>ai[0] = 模式 + 扇形半角编码（mode = 整数部分 0线/1扇/2环；小数部分×10 = 扇形半角弧度）
    /// <br/>ai[1] = 旋转（线/扇，弧度）或 半径（环，像素）
    /// <br/>ai[2] = 总时长（帧）；充能进度由 timeLeft 推导，各端确定性动画无需额外同步
    /// </summary>
    internal class PrimeTelegraphLine : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder2;

        internal static float LineLength => 1150f;
        internal static float LineWidth => 64f;
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
        /// <summary>0→1 充能进度（由 timeLeft 推导，两端一致）</summary>
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
        }

        public override bool PreDraw(ref Color lightColor) {
            Effect shader = EffectLoader.PrimeTelegraph?.Value;
            if (shader == null) {
                DrawFallback();
                return false;
            }

            SpriteBatch sb = Main.spriteBatch;
            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.Additive, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            shader.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            shader.Parameters["uProgress"]?.SetValue(Progress);
            shader.Parameters["uIntensity"]?.SetValue(1f);
            shader.Parameters["uMode"]?.SetValue((float)Mode);
            shader.Parameters["uFanAngle"]?.SetValue(FanHalfAngle);
            shader.CurrentTechnique.Passes[0].Apply();

            Texture2D quad = CWRAsset.Placeholder_White.Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            switch (Mode) {
                case 0: //线：origin 在左端中点，沿 ai[1] 方向延伸
                    sb.Draw(quad, drawPos, null, Color.White, Projectile.ai[1],
                        new Vector2(0f, quad.Height / 2f),
                        new Vector2(LineLength / quad.Width, LineWidth / quad.Height),
                        SpriteEffects.None, 0f);
                    break;
                case 1: //扇：顶点在左端中点；quad 高度=2×长度保证着色器角度等比
                    sb.Draw(quad, drawPos, null, Color.White, Projectile.ai[1],
                        new Vector2(0f, quad.Height / 2f),
                        new Vector2(FanRadius / quad.Width, FanRadius * 2f / quad.Height),
                        SpriteEffects.None, 0f);
                    break;
                default: //环：中心对齐，边长=直径×1.05
                    float size = Projectile.ai[1] * 2.1f;
                    sb.Draw(quad, drawPos, null, Color.White, 0f,
                        quad.Size() / 2f,
                        new Vector2(size / quad.Width, size / quad.Height),
                        SpriteEffects.None, 0f);
                    break;
            }

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
            return false;
        }

        /// <summary>着色器缺失时的贴图兜底：拉伸激光线 / 扩散圆环</summary>
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

            Texture2D line = CWRUtils.GetT2DValue(CWRConstant.Masking + "MaskLaserLine");
            Vector2 origin = new(0f, line.Height / 2f);
            float length = Mode == 0 ? LineLength : FanRadius;
            if (Mode == 1) {
                //扇形兜底：画两条边界线
                Main.EntitySpriteDraw(line, Projectile.Center - Main.screenPosition, null, color,
                    Projectile.ai[1] - FanHalfAngle, origin, new Vector2(length / line.Width, 0.16f), SpriteEffects.None, 0);
                Main.EntitySpriteDraw(line, Projectile.Center - Main.screenPosition, null, color,
                    Projectile.ai[1] + FanHalfAngle, origin, new Vector2(length / line.Width, 0.16f), SpriteEffects.None, 0);
                return;
            }
            Main.EntitySpriteDraw(line, Projectile.Center - Main.screenPosition, null, color,
                Projectile.ai[1], origin, new Vector2(length / line.Width, 0.3f), SpriteEffects.None, 0);
        }

        public override void DrawBehind(int index, List<int> behindNPCsAndTiles, List<int> behindNPCs,
            List<int> behindProjectiles, List<int> overPlayers, List<int> overWiresUI) => behindNPCsAndTiles.Add(index);

        #region 生成助手（服务端裁决，netImportant 同步到客户端）

        /// <summary>生成方向线预警（自 center 沿 rotation 延伸）</summary>
        internal static void SpawnLine(NPC owner, Vector2 center, float rotation, int duration) {
            if (VaultUtils.isClient) {
                return;
            }
            int id = Projectile.NewProjectile(owner.GetSource_FromAI(), center, Vector2.Zero,
                ModContent.ProjectileType<PrimeTelegraphLine>(), 0, 0f, Main.myPlayer,
                0f, rotation, duration);
            ApplyDuration(id, duration);
        }

        /// <summary>生成扇形预警（顶点在 center，朝向 rotation，半角 halfAngle）</summary>
        internal static void SpawnFan(NPC owner, Vector2 center, float rotation, float halfAngle, int duration) {
            if (VaultUtils.isClient) {
                return;
            }
            float encoded = 1f + MathHelper.Clamp(halfAngle, 0.05f, 1.4f) / 10f;
            int id = Projectile.NewProjectile(owner.GetSource_FromAI(), center, Vector2.Zero,
                ModContent.ProjectileType<PrimeTelegraphLine>(), 0, 0f, Main.myPlayer,
                encoded, rotation, duration);
            ApplyDuration(id, duration);
        }

        /// <summary>生成圆环预警（中心 center，半径 radiusPx）</summary>
        internal static void SpawnRing(NPC owner, Vector2 center, float radiusPx, int duration) {
            if (VaultUtils.isClient) {
                return;
            }
            int id = Projectile.NewProjectile(owner.GetSource_FromAI(), center, Vector2.Zero,
                ModContent.ProjectileType<PrimeTelegraphLine>(), 0, 0f, Main.myPlayer,
                2f, radiusPx, duration);
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
