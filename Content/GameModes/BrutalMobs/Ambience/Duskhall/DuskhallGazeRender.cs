using InnoVault.RenderHandles;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Ambience.Duskhall
{
    /// <summary>
    /// 幽瞳虚影绘制层：无实体的本机幻象，只画本地玩家自己的凝视演出（状态在 <see cref="DuskhallPlayer"/>）。<br/>
    /// 结构（材质=幽魂目珠，非光球）：眼窝暗蒙（Extra_98 真 alpha，横置窄梭承担暗形）→
    /// 幽蓝辉层+冷青虹膜（SoftGlow 黑底图只进加色批）→ 竖缝瞳（Extra_98 暗形压顶，瞳孔追人）→
    /// 白星点睛（StarTexture_White）。冲刺期整体沿速度拉伸并甩两道残影（运动各向异性），
    /// 消散期只余扩张辉光。挂 EndEntityDraw：幻象要盖在场景与人物之上
    /// </summary>
    internal sealed class DuskhallGazeRender : RenderHandle
    {
        /// <summary>槽位分配权重 1.79</summary>
        public override float Weight => 1.79f;

        //SoftGlow 64² 黑底圆点：只进加色批；Extra_98 72² 真alpha窄梭：唯一能画暗形的载体
        [VaultLoaden(CWRConstant.Masking)]
        private static Asset<Texture2D> SoftGlow = null;
        [VaultLoaden(CWRConstant.Masking)]
        private static Asset<Texture2D> Extra_98 = null;
        //StarTexture_White 326² 真alpha白星：点睛高光
        [VaultLoaden(CWRConstant.Masking)]
        private static Asset<Texture2D> StarTexture_White = null;

        private static readonly Color HaloBlue = new(96, 148, 255);
        private static readonly Color IrisCyan = new(150, 220, 255);
        private static readonly Color SocketDark = new(8, 8, 18);
        private static readonly Color PupilDark = new(4, 4, 12);

        public override void EndEntityDraw(SpriteBatch spriteBatch, Main main
            , GraphicsDevice graphicsDevice, RenderTarget2D screenSwap) {
            if (Main.dedServ || Main.gameMenu) {
                return;
            }
            Player player = Main.LocalPlayer;
            if (player == null || !player.active) {
                return;
            }
            DuskhallPlayer dp = player.GetModPlayer<DuskhallPlayer>();
            if (dp.Phase == DuskhallPlayer.EyePhase.None || dp.EyeAlpha <= 0.01f) {
                return;
            }
            Texture2D glow = SoftGlow?.Value;
            Texture2D spindle = Extra_98?.Value;
            Texture2D star = StarTexture_White?.Value;
            if (glow == null || spindle == null || star == null) {
                return;
            }

            //幽火不稳的明灭（双频拍频，避免匀速呼吸的机械感）
            float t = Main.GlobalTimeWrappedHourly;
            float flicker = 0.86f + 0.14f * MathF.Sin(t * 9.3f) * MathF.Sin(t * 4.1f + 1.3f);
            float a = dp.EyeAlpha * flicker;

            bool rushing = dp.Phase == DuskhallPlayer.EyePhase.Rush;
            bool bursting = dp.Phase == DuskhallPlayer.EyePhase.Burst;
            float speed = dp.EyeVel.Length();
            float rot = rushing && speed > 0.01f ? dp.EyeVel.ToRotation() : 0f;
            //冲刺放大与速度拉伸：移动体必须沿速度各向异性
            float grow = rushing ? 1f + Math.Min(speed / 50f, 0.85f) : 1f;
            float stretch = rushing ? 1f + Math.Min(speed / 34f, 1.1f) : 1f;
            Vector2 screen = dp.EyePos - Main.screenPosition;
            Vector2 glowOrigin = glow.Size() * 0.5f;
            Vector2 spindleOrigin = spindle.Size() * 0.5f;

            //批1：眼窝暗蒙（真 alpha 才能承担暗形，黑底图物理上画不出暗层）
            if (!bursting) {
                spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                    DepthStencilState.None, RasterizerState.CullNone, null,
                    Main.GameViewMatrix.TransformationMatrix);
                float socketRot = rushing ? rot - MathHelper.PiOver2 : MathHelper.PiOver2;
                spriteBatch.Draw(spindle, screen, null, SocketDark * (a * 0.55f), socketRot,
                    spindleOrigin, 2.2f * grow, SpriteEffects.None, 0f);
                spriteBatch.End();
            }

            //批2：辉层（黑底贴图只进加色批）
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null,
                Main.GameViewMatrix.TransformationMatrix);
            if (bursting) {
                //消散余辉：扩张的空洞辉光，虹膜速灭
                float expand = dp.PhaseTimer / 18f;
                spriteBatch.Draw(glow, screen, null, HaloBlue * (a * 0.6f), 0f,
                    glowOrigin, 1.65f * (1f + 1.3f * expand), SpriteEffects.None, 0f);
                spriteBatch.Draw(glow, screen, null, IrisCyan * (a * 0.5f * (1f - expand)), 0f,
                    glowOrigin, 0.62f, SpriteEffects.None, 0f);
            }
            else {
                //冲刺残影：两道错后的虹膜光斑，位置差承载运动感
                if (rushing) {
                    spriteBatch.Draw(glow, screen - dp.EyeVel * 1.6f, null,
                        IrisCyan * (a * 0.4f), rot, glowOrigin,
                        new Vector2(0.55f, 0.45f) * grow, SpriteEffects.None, 0f);
                    spriteBatch.Draw(glow, screen - dp.EyeVel * 3.2f, null,
                        IrisCyan * (a * 0.2f), rot, glowOrigin,
                        new Vector2(0.5f, 0.38f) * grow, SpriteEffects.None, 0f);
                }
                //外辉（冲刺时沿速度拉伸）
                spriteBatch.Draw(glow, screen, null, HaloBlue * (a * 0.55f), rot,
                    glowOrigin, new Vector2(1.65f * stretch, 1.65f) * grow, SpriteEffects.None, 0f);
                //虹膜（潜伏期轻微搏动）
                float irisPulse = rushing ? 1f : 1f + 0.06f * MathF.Sin(t * 6.7f);
                spriteBatch.Draw(glow, screen, null, IrisCyan * (a * 0.85f), rot,
                    glowOrigin, 0.62f * irisPulse * grow, SpriteEffects.None, 0f);
                //白星点睛：偏在瞳孔反侧，像一点湿冷的反光
                Vector2 glintPos = screen - dp.PupilLook * 0.6f + new Vector2(-3f, -4f);
                spriteBatch.Draw(star, glintPos, null, Color.White * (a * 0.75f), 0.3f,
                    star.Size() * 0.5f, 0.06f * grow, SpriteEffects.None, 0f);
            }
            spriteBatch.End();

            //批3：竖缝瞳压顶（真 alpha 暗形；潜伏期瞳孔追人，冲刺期缝线转向运动方向）
            if (!bursting) {
                spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                    DepthStencilState.None, RasterizerState.CullNone, null,
                    Main.GameViewMatrix.TransformationMatrix);
                float pupilRot = rushing ? rot - MathHelper.PiOver2 : 0f;
                spriteBatch.Draw(spindle, screen + dp.PupilLook, null, PupilDark * a, pupilRot,
                    spindleOrigin, 0.55f * grow, SpriteEffects.None, 0f);
                spriteBatch.End();
            }
        }
    }
}
