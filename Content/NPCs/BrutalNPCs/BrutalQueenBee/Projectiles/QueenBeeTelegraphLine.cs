using CalamityOverhaul.Common;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalQueenBee.Projectiles
{
    /// <summary>
    /// 琥珀预警线：蜂舞信号房格行进；ai[0]=锚NPC(-1定点) ai[1]=追玩家(-1不追) ai[2]=PackParams(模式,时长)<br/>
    /// 模式0定线 1旋转追踪
    /// </summary>
    internal class QueenBeeTelegraphLine : ModProjectile
    {
        public override string Texture => CWRConstant.Masking + "MaskLaserLine";

        /// <summary>末段停追+白闪帧数</summary>
        internal const int LockTime = 14;

        /// <summary>模式+时长打进 ai[2]，随生成同步</summary>
        internal static float PackParams(int mode, int duration) => mode + duration * 4f;

        private int AnchorNpc => (int)Projectile.ai[0];
        private int TrackPlayer => (int)Projectile.ai[1];
        private int Mode => (int)Projectile.ai[2] % 4;
        private int Duration => (int)Projectile.ai[2] / 4;
        private bool Locked => Projectile.timeLeft <= LockTime;

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 4200;

        public override void SetDefaults() {
            Projectile.width = 10;
            Projectile.height = 10;
            Projectile.hostile = false;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 46;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            //首帧套打包时长
            if (Projectile.localAI[0] == 0f) {
                if (Duration > 0) {
                    Projectile.timeLeft = Duration;
                }
                Projectile.localAI[0] = Projectile.timeLeft;
                Projectile.rotation = Projectile.velocity.ToRotation();
            }

            NPC anchor = AnchorNpc.TryGetNPC(out NPC a) ? a : null;
            if (anchor.Alives()) {
                Projectile.Center = anchor.Center;
            }

            Player player = TrackPlayer.TryGetPlayer(out Player p) ? p : null;
            if (!Locked && player.Alives() && Mode == 1) {
                float targetRot = (player.Center - Projectile.Center).ToRotation();
                Projectile.rotation = Projectile.rotation.AngleLerp(targetRot, 0.1f);
            }

            Projectile.velocity = Projectile.rotation.ToRotationVector2();
            Lighting.AddLight(Projectile.Center, new Vector3(0.55f, 0.4f, 0.1f));
        }

        public override bool PreDraw(ref Color lightColor) {
            float total = Math.Max(Projectile.localAI[0], 1f);
            float lifeT = 1f - Projectile.timeLeft / total;
            float fadeIn = MathHelper.Clamp(lifeT * 4f, 0f, 1f);
            float lockT = Locked ? 1f - Projectile.timeLeft / (float)LockTime : 0f;

            if (EffectLoader.QueenBeeTelegraph?.Value != null) {
                DrawShaderLine(EffectLoader.QueenBeeTelegraph.Value, fadeIn, lockT);
                return false;
            }

            DrawSpriteFallback(fadeIn, lockT);
            return false;
        }

        /// <summary>着色器蜂舞信号线</summary>
        private void DrawShaderLine(Effect effect, float fadeIn, float lockT) {
            const float LineLength = 4200f;
            float width = 104f + lockT * 54f;

            effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            effect.Parameters["uIntensity"]?.SetValue(fadeIn * (0.7f + lockT * 0.4f));
            effect.Parameters["uLockProgress"]?.SetValue(lockT);
            effect.Parameters["uAspect"]?.SetValue(LineLength / width);
            effect.Parameters["uColor"]?.SetValue(new Vector3(1f, 0.72f, 0.22f));

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

        /// <summary>sprite回退</summary>
        private void DrawSpriteFallback(float fadeIn, float lockT) {
            Texture2D tex = Terraria.GameContent.TextureAssets.Projectile[Type].Value;
            float pulse = 0.65f + 0.35f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 12f);
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Vector2 origin = new Vector2(0, tex.Height / 2f);

            if (!Locked) {
                Color warn = new Color(255, 175, 60, 0) * (0.42f * fadeIn * pulse);
                Main.EntitySpriteDraw(tex, drawPos, null, warn, Projectile.rotation,
                    origin, new Vector2(1050f, 0.42f + 0.22f * pulse), SpriteEffects.None, 0);
                Main.EntitySpriteDraw(tex, drawPos, null, warn * 0.7f, Projectile.rotation,
                    origin, new Vector2(1050f, 1.05f), SpriteEffects.None, 0);
            }
            else {
                float flash = 0.7f + 0.3f * (float)Math.Sin(lockT * MathHelper.Pi * 6f);
                Color core = new Color(255, 240, 200, 0) * (0.9f * flash);
                Color glow = new Color(255, 165, 55, 0) * (0.72f * flash);
                Main.EntitySpriteDraw(tex, drawPos, null, glow, Projectile.rotation,
                    origin, new Vector2(1050f, 2f), SpriteEffects.None, 0);
                Main.EntitySpriteDraw(tex, drawPos, null, core, Projectile.rotation,
                    origin, new Vector2(1050f, 0.75f), SpriteEffects.None, 0);
            }
        }

        public override void DrawBehind(int index, List<int> behindNPCsAndTiles, List<int> behindNPCs,
            List<int> behindProjectiles, List<int> overPlayers, List<int> overWiresUI) => behindNPCsAndTiles.Add(index);
    }
}
