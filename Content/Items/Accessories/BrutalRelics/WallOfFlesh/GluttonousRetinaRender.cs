using CalamityOverhaul.Common;
using InnoVault.RenderHandles;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace CalamityOverhaul.Content.Items.Accessories.BrutalRelics.WallOfFlesh
{
    /// <summary>
    /// 视网膜锁定绘制层(权重槽 1.78)：
    /// 1) 处刑窗口内被标记敌人头上的低强度虹膜准星(各端可见，状态由各端本地推得)；
    /// 2) 处刑命中瞬间的一次性红光锁定扫描闪(仅命中结算端=拥有者本地，观感反馈)。
    /// 两者共用 BRelicRetinaLock 着色器，uMode 区分常驻环与爆闪
    /// </summary>
    internal sealed class GluttonousRetinaRender : RenderHandle
    {
        public override float Weight => 1.78f;

        /// <summary>爆闪时长(tick)</summary>
        private const int FlashLife = 16;
        /// <summary>常驻准星面片边长 px</summary>
        private const float MarkSize = 130f;
        /// <summary>爆闪面片边长 px</summary>
        private const float FlashSize = 340f;

        //一次性爆闪请求：视图本地演出状态(仅本端命中结算写入)，非逐玩家游戏状态
        private static int flashNpc = -1;
        private static Vector2 flashPos;
        private static long flashStart = -FlashLife;

        /// <summary>请求一次处刑爆闪(拥有者命中结算端调用)</summary>
        internal static void RequestFlash(int npcIndex, Vector2 worldPos) {
            flashNpc = npcIndex;
            flashPos = worldPos;
            flashStart = (long)Main.GameUpdateCount;
        }

        public override void EndEntityDraw(SpriteBatch spriteBatch, Main main) {
            if (Main.dedServ || Main.gameMenu) {
                return;
            }
            Effect effect = EffectLoader.BRelicRetinaLock?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (effect == null || noise == null) {
                return;
            }

            //常驻标记准星：慢环呼吸 + 旋转刻度
            foreach (NPC npc in Main.ActiveNPCs) {
                GluttonousThroatGlobalNPC mark = npc.GetGlobalNPC<GluttonousThroatGlobalNPC>();
                if (!mark.MarkVisible || !OnScreen(npc.Center, MarkSize)) {
                    continue;
                }
                float remain = MathHelper.Clamp(
                    (mark.MarkUntil - (long)Main.GameUpdateCount) / (float)GluttonousThroatPlayer.MarkWindow, 0f, 1f);
                DrawLockQuad(spriteBatch, effect, noise, npc.Center,
                    MarkSize * (0.9f + npc.width / 120f), mode: 0f,
                    progress: 1f - remain, intensity: 0.7f, seed: npc.whoAmI * 0.173f % 1f);
            }

            //一次性处刑爆闪：跟随目标，目标没了停在原地收尾
            long flashAge = (long)Main.GameUpdateCount - flashStart;
            if (flashAge >= 0 && flashAge < FlashLife) {
                if (flashNpc >= 0 && flashNpc < Main.maxNPCs && Main.npc[flashNpc].active) {
                    flashPos = Main.npc[flashNpc].Center;
                }
                if (OnScreen(flashPos, FlashSize)) {
                    DrawLockQuad(spriteBatch, effect, noise, flashPos, FlashSize, mode: 1f,
                        progress: flashAge / (float)FlashLife, intensity: 1f,
                        seed: flashStart % 97 / 97f);
                }
            }
        }

        private static bool OnScreen(Vector2 worldPos, float margin) {
            return worldPos.X > Main.screenPosition.X - margin
                && worldPos.X < Main.screenPosition.X + Main.screenWidth + margin
                && worldPos.Y > Main.screenPosition.Y - margin
                && worldPos.Y < Main.screenPosition.Y + Main.screenHeight + margin;
        }

        /// <summary>Immediate 批画一片着色器方形面片(入场时无活动批，自开自收)</summary>
        private static void DrawLockQuad(SpriteBatch sb, Effect effect, Texture2D noise,
            Vector2 worldCenter, float size, float mode, float progress, float intensity, float seed) {
            effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            effect.Parameters["uMode"]?.SetValue(mode);
            effect.Parameters["uProgress"]?.SetValue(MathHelper.Clamp(progress, 0f, 1f));
            effect.Parameters["uIntensity"]?.SetValue(intensity);
            effect.Parameters["seed"]?.SetValue(seed);

            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearWrap,
                DepthStencilState.None, RasterizerState.CullNone, effect, Main.GameViewMatrix.TransformationMatrix);
            GraphicsDevice gd = Main.instance.GraphicsDevice;
            gd.Textures[1] = noise;
            gd.SamplerStates[1] = SamplerState.LinearWrap;
            Texture2D quad = VaultAsset.placeholder2.Value;
            sb.Draw(quad, worldCenter - Main.screenPosition, null, Color.White, 0f,
                quad.Size() / 2f, size / quad.Width, SpriteEffects.None, 0f);
            sb.End();
        }
    }
}
