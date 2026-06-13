using CalamityOverhaul.Common;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces.Teleport
{
    /// <summary>瞬移终点重组弹幕，CyberReform.fx 体素向心归位</summary>
    internal class CyberReformProj : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;

        //生命：与 CyberTeleport.HideDuration(22) 对齐，留出 snap+消散尾
        private const int Lifetime = 32;
        //SNAP 闪光中心帧：玩家在中心实体化（≈22/32=0.69）
        private const float SnapPeakT = 0.65f;
        //演出整体可视半径（像素）
        private const float DisplayRadius = 240f;

        //向心方向 +1 = reform
        private const float Direction = 1f;

        private float seed;

        public override void SetDefaults() {
            Projectile.width = 2;
            Projectile.height = 2;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = Lifetime;
        }

        public override void AI() {
            if (Projectile.localAI[0] == 0f) {
                seed = Main.rand.NextFloat();
                Projectile.localAI[0] = 1f;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Effect shader = EffectLoader.CyberReform?.Value;
            if (shader == null) return false;
            if (CWRAsset.Placeholder_White == null) return false;
            if (CWRAsset.Extra_193?.Value == null) return false;

            Texture2D canvas = CWRAsset.Placeholder_White.Value;
            Texture2D noise = CWRAsset.Extra_193.Value;

            float t = 1f - (float)Projectile.timeLeft / Lifetime;

            //演出参数计算
            //reformProgress：0~SnapPeakT 内格子从外飞向目标
            float reformProgress = MathHelper.Clamp(t / SnapPeakT, 0f, 1f);
            //snap 脉冲：以 SnapPeakT 为顶峰，宽 0.18 的钟形
            float snapWindow = 0.18f;
            float snapDelta = MathF.Abs(t - SnapPeakT);
            float snap = MathF.Max(0f, 1f - snapDelta / snapWindow);
            snap = MathF.Pow(snap, 1.5f);
            //SNAP 后的消散
            float dissipate = t > SnapPeakT
                ? MathHelper.Clamp((t - SnapPeakT) / (1f - SnapPeakT), 0f, 1f)
                : 0f;

            //淡入淡出
            float fadeAlpha;
            if (t < 0.10f) fadeAlpha = MathHelper.SmoothStep(0f, 1f, t / 0.10f);
            else if (t > 0.85f) fadeAlpha = MathHelper.SmoothStep(1f, 0f, (t - 0.85f) / 0.15f);
            else fadeAlpha = 1f;

            //时间基于"主人玩家"的领域状态，避免远端客户端读 Local 造成节奏错位
            CyberspacePlayer ownerCp = Cyberspace.For(Projectile.owner);
            float effectTime = ownerCp != null && ownerCp.Active
                ? ownerCp.EffectTime
                : (float)Main.timeForVisualEffects * 0.04f;
            shader.Parameters["uTime"]?.SetValue(effectTime);
            shader.Parameters["fadeAlpha"]?.SetValue(MathHelper.Clamp(fadeAlpha, 0f, 1f));
            shader.Parameters["reformProgress"]?.SetValue(reformProgress);
            shader.Parameters["snapPulse"]?.SetValue(MathHelper.Clamp(snap, 0f, 1f));
            shader.Parameters["dissipate"]?.SetValue(dissipate);
            shader.Parameters["seed"]?.SetValue(seed);
            shader.Parameters["direction"]?.SetValue(Direction);

            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            float drawDiameter = DisplayRadius * 2f;

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Additive,
                SamplerState.LinearWrap, DepthStencilState.None, RasterizerState.CullNone,
                null, Main.GameViewMatrix.TransformationMatrix);

            Main.graphics.GraphicsDevice.Textures[1] = noise;
            Main.graphics.GraphicsDevice.SamplerStates[1] = SamplerState.LinearWrap;
            shader.CurrentTechnique.Passes[0].Apply();

            Main.spriteBatch.Draw(canvas, drawPos, null, Color.White,
                0f, canvas.Size() * 0.5f, new Vector2(drawDiameter, drawDiameter),
                SpriteEffects.None, 0f);

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                Main.DefaultSamplerState, DepthStencilState.None, RasterizerState.CullNone,
                null, Main.GameViewMatrix.TransformationMatrix);

            return false;
        }

        public override bool ShouldUpdatePosition() => false;
    }
}
