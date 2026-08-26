using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.PRTTypes
{
    /// <summary>
    /// 受益微光脉冲:治疗光环内玩家身上周期性荡开的柔和光晕,跟随宿主。
    /// SoftGlow 黑底加色批,扩张渐隐一拍即收
    /// </summary>
    internal class PRT_DefHealPulse : BasePRT
    {
        public override int InGame_World_MaxCount => 30;
        public override string Texture => CWRConstant.Masking + "SoftGlow";
        public override bool CanPool => true;

        private Color initialColor;
        private int followPlayer = -1;

        public PRT_DefHealPulse Configure(int lifetime, int playerWhoAmI) {
            Lifetime = lifetime;
            initialColor = Color with { A = 255 };
            followPlayer = playerWhoAmI;
            return this;
        }

        public override void Reset() {
            base.Reset();
            initialColor = default;
            followPlayer = -1;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AdditiveBlend;
        }

        public override void AI() {
            //跟随宿主,宿主离场即散
            if (followPlayer >= 0 && followPlayer < Main.maxPlayers) {
                Player player = Main.player[followPlayer];
                if (player.active && !player.dead) {
                    Position = player.MountedCenter;
                }
                else {
                    Kill();
                }
            }
            Velocity = Vector2.Zero;

            float t = LifetimeCompletion;
            Color = initialColor * MathF.Pow(1f - t, 1.4f);
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = PRTLoader.PRT_IDToTexture[ID];
            Vector2 origin = tex.Size() * 0.5f;
            Vector2 pos = Position - Main.screenPosition;
            //扩张的柔环:外圈快张,内芯滞后
            float t = LifetimeCompletion;
            float grow = MathHelper.Lerp(0.35f, 1.05f, 1f - (1f - t) * (1f - t));
            spriteBatch.Draw(tex, pos, null, Color * 0.5f, 0f, origin, Scale * grow, SpriteEffects.None, 0f);
            spriteBatch.Draw(tex, pos, null, Color * 0.35f, 0f, origin, Scale * grow * 0.6f, SpriteEffects.None, 0f);
            return false;
        }
    }
}
