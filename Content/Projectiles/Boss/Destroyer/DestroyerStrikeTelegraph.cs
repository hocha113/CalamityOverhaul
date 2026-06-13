using CalamityOverhaul.Common;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Boss.Destroyer
{
    /// <summary>
    /// 通用预警线：锁定前脉冲红线追踪目标，锁定后定格白闪
    /// 无伤害纯演出，服务端生成保证多人可见
    /// <br/>ai[0]: 锚定NPC索引（-1=固定在生成点）
    /// <br/>ai[1]: 追踪的玩家索引（-1=不追踪）
    /// <br/>ai[2]: 打包参数 = 模式 + 时长*4（用 <see cref="PackParams"/> 生成；
    /// 时长走 ai 槽(timeLeft 不参与生成同步包)；模式 0 方向固定 1 旋追踪 2 垂线跟玩家 X）
    /// <br/>velocity = 单位方向
    /// </summary>
    internal class DestroyerStrikeTelegraph : ModProjectile
    {
        public override string Texture => CWRConstant.Masking + "MaskLaserLine";

        /// <summary>锁定窗口：到期前这段时间停止追踪并白闪</summary>
        internal const int LockTime = 16;

        /// <summary>把模式与持续时长打包进 ai[2]（随生成包同步到所有端）</summary>
        internal static float PackParams(int mode, int duration) => mode + duration * 4f;

        private int AnchorNpc => (int)Projectile.ai[0];
        private int TrackPlayer => (int)Projectile.ai[1];
        private int Mode => (int)Projectile.ai[2] % 4;
        private int Duration => (int)Projectile.ai[2] / 4;
        private bool Locked => Projectile.timeLeft <= LockTime;

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 4800;

        public override void SetDefaults() {
            Projectile.width = 10;
            Projectile.height = 10;
            Projectile.hostile = false;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 50;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            //首帧应用打包时长并记录总时长用于淡入计算
            if (Projectile.localAI[0] == 0f) {
                if (Duration > 0) {
                    Projectile.timeLeft = Duration;
                }
                Projectile.localAI[0] = Projectile.timeLeft;
                Projectile.rotation = Projectile.velocity.ToRotation();
            }

            //锚定到NPC
            NPC anchor = AnchorNpc >= 0 ? CWRUtils.GetNPCInstance(AnchorNpc) : null;
            if (anchor.Alives()) {
                Projectile.Center = anchor.Center;
            }

            //追踪目标
            Player player = TrackPlayer >= 0 ? CWRUtils.GetPlayerInstance(TrackPlayer) : null;
            if (!Locked && player.Alives()) {
                if (Mode == 1) {
                    float targetRot = (player.Center - Projectile.Center).ToRotation();
                    Projectile.rotation = Projectile.rotation.AngleLerp(targetRot, 0.12f);
                }
                else if (Mode == 2) {
                    Projectile.Center = new Vector2(
                        MathHelper.Lerp(Projectile.Center.X, player.Center.X, 0.18f),
                        Projectile.Center.Y);
                }
            }

            Projectile.velocity = Projectile.rotation.ToRotationVector2();
            Lighting.AddLight(Projectile.Center, new Vector3(0.6f, 0.12f, 0.06f));
        }

        public override bool PreDraw(ref Color lightColor) {
            float total = Math.Max(Projectile.localAI[0], 1f);
            float lifeT = 1f - Projectile.timeLeft / total;
            //淡入
            float fadeIn = MathHelper.Clamp(lifeT * 4f, 0f, 1f);
            float lockT = Locked ? 1f - Projectile.timeLeft / (float)LockTime : 0f;

            //优先走专属能量流着色器；着色器资产缺失时回退到sprite绘制
            if (EffectLoader.DestroyerTelegraph?.Value != null) {
                DrawShaderLine(EffectLoader.DestroyerTelegraph.Value, fadeIn, lockT);
                return false;
            }

            DrawSpriteFallback(fadeIn, lockT);
            return false;
        }

        /// <summary>
        /// 着色器绘制：白色占位quad整幅拉伸，噪声能量流与锁定白闪全部在着色器内生成
        /// </summary>
        private void DrawShaderLine(Effect effect, float fadeIn, float lockT) {
            const float LineLength = 4800f;
            float width = 120f + lockT * 70f;

            effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            effect.Parameters["uIntensity"]?.SetValue(fadeIn * (0.72f + lockT * 0.38f));
            effect.Parameters["uLockProgress"]?.SetValue(lockT);
            effect.Parameters["uAspect"]?.SetValue(LineLength / width);
            effect.Parameters["uColor"]?.SetValue(new Vector3(1f, 0.22f, 0.13f));

            SpriteBatch sb = Main.spriteBatch;
            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.Additive, Main.DefaultSamplerState,
                DepthStencilState.None, RasterizerState.CullNone, effect, Main.GameViewMatrix.TransformationMatrix);
            effect.CurrentTechnique.Passes[0].Apply();

            Texture2D pixel = VaultAsset.placeholder2.Value;
            Vector2 scale = new Vector2(LineLength / pixel.Width, width / pixel.Height);
            sb.Draw(pixel, Projectile.Center - Main.screenPosition, null, Color.White,
                Projectile.rotation, new Vector2(0, pixel.Height / 2f), scale, SpriteEffects.None, 0f);

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
        }

        /// <summary>
        /// sprite回退路径（DestroyerTelegraph.fxc 缺失时）
        /// </summary>
        private void DrawSpriteFallback(float fadeIn, float lockT) {
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            float pulse = 0.65f + 0.35f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 14f);
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Vector2 origin = new Vector2(0, tex.Height / 2f);

            if (!Locked) {
                //追踪期：暗红主线 + 呼吸脉冲
                Color warn = new Color(255, 50, 30, 0) * (0.45f * fadeIn * pulse);
                Main.EntitySpriteDraw(tex, drawPos, null, warn, Projectile.rotation,
                    origin, new Vector2(1200f, 0.45f + 0.25f * pulse), SpriteEffects.None, 0);
                Main.EntitySpriteDraw(tex, drawPos, null, warn * 0.7f, Projectile.rotation,
                    origin, new Vector2(1200f, 1.1f), SpriteEffects.None, 0);
            }
            else {
                //锁定白闪：高亮核心 + 宽外晕，强提示即将打击
                float flash = 0.7f + 0.3f * (float)Math.Sin(lockT * MathHelper.Pi * 6f);
                Color core = new Color(255, 235, 210, 0) * (0.9f * flash);
                Color glow = new Color(255, 90, 40, 0) * (0.75f * flash);
                Main.EntitySpriteDraw(tex, drawPos, null, glow, Projectile.rotation,
                    origin, new Vector2(1200f, 2.2f), SpriteEffects.None, 0);
                Main.EntitySpriteDraw(tex, drawPos, null, core, Projectile.rotation,
                    origin, new Vector2(1200f, 0.8f), SpriteEffects.None, 0);
            }
        }

        public override void DrawBehind(int index, List<int> behindNPCsAndTiles, List<int> behindNPCs,
            List<int> behindProjectiles, List<int> overPlayers, List<int> overWiresUI) => behindNPCsAndTiles.Add(index);
    }
}
