using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEmpressOfLight.Core;
using InnoVault.RenderHandles;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEmpressOfLight.Rendering
{
    /// <summary>光之女皇全屏后效：色散脉冲/环境描边/命中链/竞技场压暗/终章停顿提示，screenTarget ping-pong</summary>
    internal class EmpressScreenRender : RenderHandle
    {
        /// <summary>权重 1.088，避开 Prime(1.08) 与并行Boss扎堆的 1.09</summary>
        public override float Weight => 1.088f;

        public override void EndCaptureDraw(SpriteBatch sb, GraphicsDevice gd, RenderTarget2D screenSwap) {
            EmpressScreenFX.Update();

            bool arena = EmpressArena.TryGet(out _, out Vector2 arenaCenter, out float arenaRadius);
            if (!EmpressScreenFX.HasAny && !arena) {
                return;
            }
            if (screenSwap == null || Main.screenTarget == null) {
                return;
            }
            Effect shader = EffectLoader.EmpressScreenPrism?.Value;
            if (shader == null) {
                return;
            }

            float pulseP = EmpressScreenFX.PulseActive
                ? MathHelper.Clamp(EmpressScreenFX.PulseAge / (float)EmpressScreenFX.PulseLife, 0f, 1f)
                : 1f;
            float pulseI = EmpressScreenFX.PulseActive ? EmpressScreenFX.PulseIntensity : 0f;

            Vector2 centerUV = WorldToScreenUV(EmpressScreenFX.PulseWorldCenter);
            //脉冲中心离屏过远则只保留其余通道
            if (centerUV.X < -0.6f || centerUV.X > 1.6f || centerUV.Y < -0.6f || centerUV.Y > 1.6f) {
                pulseI = 0f;
            }

            float aspect = Main.screenWidth / (float)Main.screenHeight;
            //竞技场：半径换成宽高比修正后的 uv 单位（以屏高为 1）
            float zoom = Main.GameViewMatrix.Zoom.Y <= 0f ? 1f : Main.GameViewMatrix.Zoom.Y;
            float arenaUV = arena ? arenaRadius * zoom / Main.screenHeight : 0f;
            Vector2 arenaCenterUV = arena ? WorldToScreenUV(arenaCenter) : Vector2.Zero;

            shader.Parameters["uTime"]?.SetValue((float)Main.timeForVisualEffects * 0.016f);
            shader.Parameters["uProgress"]?.SetValue(pulseP);
            shader.Parameters["uIntensity"]?.SetValue(pulseI);
            shader.Parameters["uAmbient"]?.SetValue(EmpressScreenFX.AmbientGrade);
            shader.Parameters["uCenter"]?.SetValue(centerUV);
            shader.Parameters["uAspect"]?.SetValue(aspect);
            shader.Parameters["uHitDark"]?.SetValue(EmpressScreenFX.HitDark);
            shader.Parameters["uFlashDir"]?.SetValue(EmpressScreenFX.FlashDir);
            shader.Parameters["uArenaCenter"]?.SetValue(arenaCenterUV);
            shader.Parameters["uArenaRadius"]?.SetValue(arenaUV);
            shader.Parameters["uArenaSoft"]?.SetValue(0.35f);
            shader.Parameters["uArenaPull"]?.SetValue(EmpressScreenFX.ArenaPull);
            shader.Parameters["uArenaFlash"]?.SetValue(EmpressScreenFX.ArenaFlash);
            shader.Parameters["uPhaseGlow"]?.SetValue(EmpressScreenFX.PhaseGlow);
            shader.Parameters["uPhaseFlash"]?.SetValue(EmpressScreenFX.PhaseFlash);

            //月影：白光中心与最多三枚月屑；同屏单位与竞技场一致（以屏高为 1）
            shader.Parameters["uWhiteout"]?.SetValue(EmpressScreenFX.Whiteout);
            shader.Parameters["uSunPos"]?.SetValue(WorldToScreenUV(EmpressScreenFX.SunWorld));
            Vector2[] shardUV = new Vector2[3];
            Vector3 shardRadius = Vector3.Zero;
            int filled = 0;
            foreach (Terraria.Projectile shard in Projectiles.EmpressMoonShard.Active) {
                if (filled >= 3 || !shard.active) {
                    continue;
                }
                shardUV[filled] = WorldToScreenUV(shard.Center);
                float r = Projectiles.EmpressMoonShard.Radius * shard.scale * zoom / Main.screenHeight;
                if (filled == 0) shardRadius.X = r;
                else if (filled == 1) shardRadius.Y = r;
                else shardRadius.Z = r;
                filled++;
            }
            shader.Parameters["uShard0"]?.SetValue(shardUV[0]);
            shader.Parameters["uShard1"]?.SetValue(shardUV[1]);
            shader.Parameters["uShard2"]?.SetValue(shardUV[2]);
            shader.Parameters["uShardRadius"]?.SetValue(shardRadius);
            shader.Parameters["uShadowLength"]?.SetValue(Projectiles.EmpressMoonShadow.Length * zoom / Main.screenHeight);

            PingPong(sb, gd, screenSwap, shader);
        }

        /// <summary>拷屏再 shader 回写</summary>
        private static void PingPong(SpriteBatch sb, GraphicsDevice gd, RenderTarget2D screenSwap, Effect shader) {
            gd.SetRenderTarget(screenSwap);
            gd.Clear(Color.Transparent);
            sb.Begin(SpriteSortMode.Deferred, BlendState.Opaque);
            sb.Draw(Main.screenTarget, Vector2.Zero, Color.White);
            sb.End();

            gd.SetRenderTarget(Main.screenTarget);
            gd.Clear(Color.Transparent);
            sb.Begin(SpriteSortMode.Immediate, BlendState.Opaque);
            shader.CurrentTechnique.Passes[0].Apply();
            sb.Draw(screenSwap, Vector2.Zero, Color.White);
            sb.End();
        }

        /// <summary>世界→归一化uv(含Zoom)</summary>
        internal static Vector2 WorldToScreenUV(Vector2 worldPos) {
            float screenW = Main.screenWidth;
            float screenH = Main.screenHeight;
            Vector2 zoom = Main.GameViewMatrix.Zoom;
            if (zoom.X <= 0f) {
                zoom.X = 1f;
            }
            if (zoom.Y <= 0f) {
                zoom.Y = 1f;
            }
            Vector2 screenCenterPx = new(screenW * 0.5f, screenH * 0.5f);
            Vector2 viewWorldCenter = Main.screenPosition + screenCenterPx;
            Vector2 screenPx = screenCenterPx + (worldPos - viewWorldCenter) * zoom;
            return new Vector2(screenPx.X / screenW, screenPx.Y / screenH);
        }
    }
}
