using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDomains;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaWisps
{
    /// <summary>
    /// 鬼火灼身重绘：被鬼火点燃的 NPC 经 KikasaWispFire.TechBurnBody 后处理
    /// 热浪扭曲 + 斑驳焦痕（越烧越花）+ 轮廓边缘火 + 金脉动；外加金光照明。
    /// 批次做法镜像 SHPCThermalHeatNPC：PreDraw 切 Immediate 套 shader、PostDraw 还原，
    /// 顶部自愈防上一实体的 PostDraw 被吞掉后状态泄漏
    /// </summary>
    internal sealed class KikasaWispBurnNPC : GlobalNPC
    {
        public override bool InstancePerEntity => true;

        /// <summary>灼烧强度包络 0~1，点燃淡入熄灭淡出</summary>
        private float fade;

        /// <summary>焦痕蔓延 0~1：越烧越花，熄灭后慢慢褪</summary>
        private float charT;

        //PreDraw 置位、PostDraw 消费，单线程批次切换
        private static bool shaderActive;

        public override void PostAI(NPC npc) {
            bool lit = npc.CWR()?.KikasaWispFire == true;
            fade = lit ? MathF.Min(fade + 1f / 22f, 1f) : MathF.Max(fade - 1f / 26f, 0f);
            if (lit) {
                charT = MathF.Min(charT + 1f / 240f, 1f);
            }
            else if (fade <= 0f) {
                charT = MathF.Max(charT - 1f / 90f, 0f);
            }
        }

        public override void DrawEffects(NPC npc, ref Color drawColor) {
            if (Main.dedServ || fade <= 0.01f) {
                return;
            }
            Lighting.AddLight(npc.Center, 0.86f * fade, 0.60f * fade, 0.22f * fade);
        }

        public override bool PreDraw(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            //上一实体断批自愈：GlobalNPC 的 PostDraw 会被 ModNPC.PreDraw=false 吞掉，先还原再谈自己
            if (shaderActive) {
                shaderActive = false;
                spriteBatch.End();
                spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                    Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer,
                    null, Main.GameViewMatrix.TransformationMatrix);
            }
            if (fade <= 0.02f || npc.IsABestiaryIconDummy) {
                return true;
            }
            Effect fx = EffectLoader.KikasaWispFire?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (fx == null || noise == null) {
                return true;
            }

            Texture2D tex = TextureAssets.Npc[npc.type].Value;
            Rectangle frame = npc.frame;
            if (frame.Width <= 0 || frame.Height <= 0) {
                frame = tex.Bounds;
            }

            fx.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            fx.Parameters["uRain"]?.SetValue(KikasaDomain.ViewedRainBlend);
            fx.Parameters["uTexelSize"]?.SetValue(new Vector2(1f / tex.Width, 1f / tex.Height));
            //帧界半像素内缩：扭曲与邻域采样全数钳回帧内，防精灵表渗色横线
            fx.Parameters["uUvRect"]?.SetValue(new Vector4(
                (frame.X + 0.5f) / tex.Width, (frame.Y + 0.5f) / tex.Height,
                (frame.X + frame.Width - 0.5f) / tex.Width, (frame.Y + frame.Height - 0.5f) / tex.Height));
            fx.Parameters["uBurnT"]?.SetValue(fade);
            fx.Parameters["uCharT"]?.SetValue(charT);
            fx.Parameters["uSeed"]?.SetValue(npc.whoAmI * 0.618f % 1f * 8f);

            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend,
                Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer,
                null, Main.GameViewMatrix.TransformationMatrix);
            Main.instance.GraphicsDevice.Textures[1] = noise;
            Main.instance.GraphicsDevice.SamplerStates[1] = SamplerState.LinearWrap;
            fx.CurrentTechnique = fx.Techniques["TechBurnBody"];
            fx.CurrentTechnique.Passes[0].Apply();
            shaderActive = true;
            return true;
        }

        public override void PostDraw(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            if (!shaderActive) {
                return;
            }
            shaderActive = false;
            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer,
                null, Main.GameViewMatrix.TransformationMatrix);
        }
    }
}
