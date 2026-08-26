using CalamityOverhaul.Common;
using InnoVault.RenderHandles;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.Items.Accessories.BrutalRelics.Golem
{
    /// <summary>
    /// 石卫姿态绘制层：为每名成形中的玩家画玄武岩护壳（脉络随蓄能点亮）
    /// 与满层灼热光环。姿态与包络各端本地推进，层数经 SolarCoreFistNet 到位
    /// </summary>
    internal sealed class SolarCoreFistRender : RenderHandle
    {
        /// <summary>残酷遗物认领槽 1.84</summary>
        public override float Weight => 1.84f;

        //石壳画布尺寸（px），uAspect 与 shader 画布契约同源
        private const float ShellW = 110f;
        private const float ShellH = 136f;
        //光环画布：判定半径 170px，环位于画布 0.77 半径处 → 半宽=170/0.77
        private const float AuraHalf = SolarCoreFistPlayer.AuraRadius / 0.77f;

        public override void EndEntityDraw(SpriteBatch spriteBatch, Main main) {
            if (Main.gameMenu) {
                return;
            }

            bool batchOpen = false;
            Effect shader = EffectLoader.BRelicStoneGuard?.Value;

            //光环批（Additive，强度在 A）
            for (int i = 0; i < Main.maxPlayers; i++) {
                Player player = Main.player[i];
                if (player == null || !player.active
                    || !player.TryGetModPlayer(out SolarCoreFistPlayer mp) || mp.AuraGlow <= 0.02f) {
                    continue;
                }
                if (OffScreen(player.Center, AuraHalf + 60f)) {
                    continue;
                }

                if (shader == null) {
                    continue; //光环无兜底：判定仍在，缺编宁缺毋滥
                }

                if (!batchOpen) {
                    batchOpen = true;
                    spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Additive, SamplerState.LinearWrap,
                        DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
                    BindNoise();
                }

                Texture2D quad = VaultAsset.placeholder2.Value;
                shader.CurrentTechnique = shader.Techniques["TechAura"];
                ApplyCommonParams(shader, mp, player, 1f);
                shader.CurrentTechnique.Passes[0].Apply();
                float side = AuraHalf * 2f;
                spriteBatch.Draw(quad, player.Center - Main.screenPosition, null, Color.White, 0f,
                    quad.Size() / 2f, new Vector2(side / quad.Width, side / quad.Height), SpriteEffects.None, 0f);
            }
            if (batchOpen) {
                spriteBatch.End();
                batchOpen = false;
            }

            //石壳批（预乘 AlphaBlend，暗石真遮挡）
            for (int i = 0; i < Main.maxPlayers; i++) {
                Player player = Main.player[i];
                if (player == null || !player.active
                    || !player.TryGetModPlayer(out SolarCoreFistPlayer mp) || mp.ShellForm <= 0.02f) {
                    continue;
                }
                if (OffScreen(player.Center, ShellH)) {
                    continue;
                }

                if (!batchOpen) {
                    batchOpen = true;
                    spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearWrap,
                        DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
                    BindNoise();
                }

                if (shader != null) {
                    Texture2D quad = VaultAsset.placeholder2.Value;
                    shader.CurrentTechnique = shader.Techniques["TechShell"];
                    ApplyCommonParams(shader, mp, player, ShellW / ShellH);
                    shader.CurrentTechnique.Passes[0].Apply();
                    //倒吊时上下翻转，成形方向仍自脚底
                    SpriteEffects fx = player.gravDir < 0f ? SpriteEffects.FlipVertically : SpriteEffects.None;
                    spriteBatch.Draw(quad, player.MountedCenter - Main.screenPosition, null, Color.White, 0f,
                        quad.Size() / 2f, new Vector2(ShellW / quad.Width, ShellH / quad.Height), fx, 0f);
                }
                else {
                    DrawShellFallback(spriteBatch, player, mp);
                }
            }
            if (batchOpen) {
                spriteBatch.End();
            }
        }

        private static void BindNoise() {
            //噪声固定 s1：Immediate 批 Apply 前显式绑定（合同同 ShockRingDraw）
            GraphicsDevice gd = Main.instance.GraphicsDevice;
            gd.Textures[1] = CWRAsset.PerlinNoise.Value;
            gd.SamplerStates[1] = SamplerState.LinearWrap;
        }

        /// <summary>共享 uniform 全参数重设（uniform 是设备全局态，跨玩家必须逐个重传）</summary>
        private static void ApplyCommonParams(Effect shader, SolarCoreFistPlayer mp, Player player, float aspect) {
            shader.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            shader.Parameters["uForm"]?.SetValue(mp.ShellForm);
            shader.Parameters["uCharge"]?.SetValue(Math.Min(mp.ChargeStacks / (float)SolarCoreFistPlayer.AuraStacks, 1f));
            shader.Parameters["uFlare"]?.SetValue(mp.Flare);
            shader.Parameters["uAura"]?.SetValue(mp.AuraGlow);
            shader.Parameters["uSeed"]?.SetValue(player.whoAmI * 0.613f);
            shader.Parameters["uAspect"]?.SetValue(aspect);
        }

        /// <summary>着色器缺编兜底：暖光呼吸 + 蓄能亮度（预乘批 A=0 即加色），不许无形</summary>
        private static void DrawShellFallback(SpriteBatch sb, Player player, SolarCoreFistPlayer mp) {
            Texture2D soft = CWRAsset.SoftGlow.Value;
            Vector2 pos = player.MountedCenter - Main.screenPosition;
            float charge = Math.Min(mp.ChargeStacks / (float)SolarCoreFistPlayer.AuraStacks, 1f);
            float pulse = 0.85f + 0.15f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 3.2f + player.whoAmI);
            sb.Draw(soft, pos, null, new Color(210, 190, 170, 0) * (0.35f * mp.ShellForm * pulse),
                0f, soft.Size() / 2f, 1.9f, SpriteEffects.None, 0f);
            sb.Draw(soft, pos, null, new Color(255, 150, 50, 0) * ((0.25f + 0.55f * charge) * mp.ShellForm),
                0f, soft.Size() / 2f, 1.2f, SpriteEffects.None, 0f);
        }

        private static bool OffScreen(Vector2 worldPos, float pad) {
            Vector2 screen = Main.screenPosition;
            return worldPos.X + pad < screen.X || worldPos.X - pad > screen.X + Main.screenWidth
                || worldPos.Y + pad < screen.Y || worldPos.Y - pad > screen.Y + Main.screenHeight;
        }
    }
}
