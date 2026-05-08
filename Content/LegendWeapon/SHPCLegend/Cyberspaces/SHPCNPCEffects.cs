using CalamityOverhaul.Content.HackTimes;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces
{
    /// <summary>
    /// SHPC 武器专属 NPC 附加效果容器
    /// <br/>数据侵蚀（归零枪管）：持续 tick 伤害 + 绿色腐蚀滤镜
    /// <br/>时相减速（时相握把）：强制降速 + 蓝紫粒子视觉
    /// </summary>
    internal class SHPCNPCEffects : GlobalNPC
    {
        public override bool InstancePerEntity => true;

        /// <summary>数据侵蚀剩余帧数</summary>
        public int DataErosionTime;
        /// <summary>每次 tick 伤害量</summary>
        public int DataErosionTickDmg;
        /// <summary>时相减速剩余帧数</summary>
        public int ChronalSlowTime;

        private static bool _shaderActive;

        /// <summary>施加数据侵蚀效果，新时长仅在大于当前剩余时才刷新</summary>
        public void ApplyDataErosion(int duration, int tickDmg) {
            DataErosionTime = Math.Max(DataErosionTime, duration);
            DataErosionTickDmg = Math.Max(DataErosionTickDmg, tickDmg);
        }

        /// <summary>施加时相减速效果，新时长仅在大于当前剩余时才刷新</summary>
        public void ApplyChronalSlow(int duration) {
            ChronalSlowTime = Math.Max(ChronalSlowTime, duration);
        }

        public override bool PreAI(NPC npc) {
            if (ChronalSlowTime > 0) {
                ChronalSlowTime--;
                npc.velocity *= 0.5f;
                if (Main.netMode != NetmodeID.Server && Main.rand.NextBool(8)) {
                    Vector2 pos = npc.Center + Main.rand.NextVector2Circular(npc.width * 0.4f, npc.height * 0.4f);
                    Vector2 vel = Main.rand.NextVector2CircularEdge(1.5f, 1.5f);
                    PRTLoader.AddParticle(new PRT_CyberSquare(
                        pos, vel,
                        new Color(120, 80, 255), new Color(60, 30, 180),
                        Main.rand.NextFloat(0.5f, 1.2f), Main.rand.Next(10, 20)));
                }
            }

            if (DataErosionTime > 0) {
                DataErosionTime--;
                int elapsed = (int)(Main.GameUpdateCount);
                if (elapsed % 30 == 0 && DataErosionTickDmg > 0) {
                    npc.SimpleStrikeNPC(DataErosionTickDmg, 0, false, 0f, null, false, 0f, true);
                }
            }
            else {
                DataErosionTickDmg = 0;
            }
            return true;
        }

        public override bool PreDraw(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            if (DataErosionTime <= 0) return true;

            Effect shader = HackEffectAssets.HackContagion;
            if (shader == null) return true;

            Texture2D tex = TextureAssets.Npc[npc.type].Value;
            //progress 0→1 随侵蚀剩余时间的消耗推进，用 saturate 夹住
            float totalTime = 240f;
            float progress = Math.Clamp(1f - DataErosionTime / totalTime, 0f, 1f);

            shader.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            shader.Parameters["progress"]?.SetValue(progress);
            shader.Parameters["intensity"]?.SetValue(1f);
            shader.Parameters["texelSize"]?.SetValue(new Vector2(1f / tex.Width, 1f / tex.Height));

            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend,
                Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer,
                null, Main.GameViewMatrix.TransformationMatrix);
            shader.CurrentTechnique.Passes[0].Apply();

            _shaderActive = true;
            return true;
        }

        public override void PostDraw(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            if (!_shaderActive) return;
            _shaderActive = false;

            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer,
                null, Main.GameViewMatrix.TransformationMatrix);
        }
    }
}
