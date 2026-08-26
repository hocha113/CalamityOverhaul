using CalamityOverhaul.Common;
using InnoVault.RenderHandles;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Accessories.BrutalRelics.QueenBee
{
    /// <summary>
    /// 蜜蜡甲体表结晶层：实体绘制收尾后逐玩家叠一层六角蜡室护膜。<br/>
    /// 甲存在性看 <see cref="WaxWardBuff"/>(跨端同步真相)，充能细节取各端本地镜像；
    /// 远端镜像偏薄时给下限，护膜可见性不吃观察者本地值(netcode §7.1)
    /// </summary>
    internal sealed class SwarmVortexRender : RenderHandle
    {
        /// <summary>残酷遗物认领槽位</summary>
        public override float Weight => 1.75f;

        public override void EndEntityDraw(SpriteBatch spriteBatch, Main main) {
            if (Main.gameMenu) {
                return;
            }

            //先收集有甲玩家，无人则不开批
            bool anyShell = false;
            for (int i = 0; i < Main.maxPlayers; i++) {
                if (ShellVisible(Main.player[i], out _, out _)) {
                    anyShell = true;
                    break;
                }
            }
            if (!anyShell) {
                return;
            }

            Effect fx = EffectLoader.BRelicWaxShell?.Value;
            if (fx != null) {
                DrawShellShader(spriteBatch, fx);
            }
            else {
                DrawShellFallback(spriteBatch);
            }
        }

        /// <summary>甲是否可见；growOut=结晶进度(远端镜像给0.35下限)，mpOut=玩家侧状态</summary>
        private static bool ShellVisible(Player player, out float growOut, out SwarmVortexPlayer mpOut) {
            growOut = 0f;
            mpOut = null;
            if (player == null || !player.active || player.dead) {
                return false;
            }
            if (!player.TryGetModPlayer(out SwarmVortexPlayer mp)) {
                return false;
            }
            bool hasBuff = player.HasBuff<WaxWardBuff>();
            if (!hasBuff && mp.WaxCharge < 0.5f && mp.CrackFlash <= 0.02f) {
                return false;
            }
            float grow = mp.WaxCharge / SwarmVortexBeacon.WaxMax;
            //远端镜像可能偏薄：buff在=甲在，可见性给下限
            if (hasBuff && player.whoAmI != Main.myPlayer) {
                grow = MathHelper.Max(grow, 0.35f);
            }
            growOut = MathHelper.Clamp(grow, 0f, 1f);
            mpOut = mp;
            return growOut > 0.02f || mp.CrackFlash > 0.02f;
        }

        private static void DrawShellShader(SpriteBatch spriteBatch, Effect fx) {
            spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend,
                SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone,
                fx, Main.GameViewMatrix.TransformationMatrix);

            fx.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            fx.Parameters["uColWax"]?.SetValue(new Vector3(0.95f, 0.85f, 0.55f));
            fx.Parameters["uColAmber"]?.SetValue(new Vector3(0.7f, 0.44f, 0.1f));

            Texture2D pixel = VaultAsset.placeholder2.Value;
            for (int i = 0; i < Main.maxPlayers; i++) {
                Player player = Main.player[i];
                if (!ShellVisible(player, out float grow, out SwarmVortexPlayer mp)) {
                    continue;
                }
                //逐玩家上参后Apply再画(Immediate契约)
                fx.Parameters["uGrow"]?.SetValue(grow);
                fx.Parameters["uCrack"]?.SetValue(mp.CrackFlash);
                fx.Parameters["uFormPulse"]?.SetValue(mp.FormFlash);
                fx.Parameters["uSeed"]?.SetValue(player.whoAmI * 0.173f);
                fx.CurrentTechnique.Passes[0].Apply();

                float side = player.height + 84f;
                Vector2 scale = new(side / pixel.Width, side / pixel.Height);
                spriteBatch.Draw(pixel, player.MountedCenter - Main.screenPosition, null, Color.White,
                    0f, pixel.Size() * 0.5f, scale, SpriteEffects.None, 0f);
            }
            spriteBatch.End();
        }

        /// <summary>无着色器回退：真alpha软纺锤两层裹身，可读出"有一层蜡"即可</summary>
        private static void DrawShellFallback(SpriteBatch spriteBatch) {
            Texture2D tex = CWRAsset.Extra_98?.Value;
            if (tex == null) {
                return;
            }
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone,
                null, Main.GameViewMatrix.TransformationMatrix);
            for (int i = 0; i < Main.maxPlayers; i++) {
                Player player = Main.player[i];
                if (!ShellVisible(player, out float grow, out SwarmVortexPlayer mp)) {
                    continue;
                }
                Vector2 pos = player.MountedCenter - Main.screenPosition;
                Color body = new Color(232, 196, 110) * (0.3f * grow + 0.3f * mp.CrackFlash);
                Vector2 scale = new((player.width + 46f) / (tex.Width * 0.55f),
                    (player.height + 42f) / (tex.Height * 0.72f));
                spriteBatch.Draw(tex, pos, null, body, 0f, tex.Size() * 0.5f, scale, SpriteEffects.None, 0f);
                spriteBatch.Draw(tex, pos, null, body * 0.6f, MathHelper.PiOver2, tex.Size() * 0.5f,
                    scale * 0.82f, SpriteEffects.None, 0f);
            }
            spriteBatch.End();
        }
    }
}
