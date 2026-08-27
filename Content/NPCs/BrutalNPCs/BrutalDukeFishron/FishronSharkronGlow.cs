using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDukeFishron.Rendering;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDukeFishron
{
    /// <summary>
    /// 鲨鱼龙(Sharkron)夜战可读性层：不动原版 AI，
    /// 本体下垫一圈海绿描边光并随身点光，黑夜俯冲航道上也能一眼认出鲨影
    /// </summary>
    internal class FishronSharkronGlow : BrutalNPCOverride
    {
        public override int TargetID => NPCID.Sharkron;

        public override bool? CanBrutalOverride() {
            return null;
        }

        public override void PostAI() {
            //随身点光：身体靠光照读细节，描边只负责勾轮廓
            Lighting.AddLight(npc.Center, FishronMotionFX.SeaGreen.ToVector3() * 0.5f);
            //末相电眼另给一撮头前点光，雨夜里眼睛先于身体被看见
            if (DukeFishronAI.AnyPhaseThreeActive()) {
                Vector2 ahead = npc.velocity.SafeNormalize(npc.rotation.ToRotationVector2());
                Lighting.AddLight(npc.Center + ahead * (npc.width * 0.34f),
                    FishronMotionFX.StormBolt.ToVector3() * 0.55f);
            }
        }

        /// <summary>本体绘制前垫描边：同帧同变换的加色剪影八向错位，只露出轮廓一圈</summary>
        public override bool? Draw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            if (npc.IsABestiaryIconDummy) {
                return null;
            }
            Main.instance.LoadNPC(npc.type);
            Texture2D tex = TextureAssets.Npc[npc.type].Value;
            int frameCount = Math.Max(Main.npcFrameCount[npc.type], 1);
            Rectangle frame = npc.frame;
            if (frame.Width <= 0 || frame.Height <= 0) {
                frame = tex.Frame(1, frameCount, 0, 0);
            }
            //原版对鲨鱼龙(371/372)走居中特判分支,不吃通用的 Bottom 底锚:
            //帧心直接锚在 Center + gfxOffY(addY/addHeight 对这两类均为 0),origin 取整除的帧半宽高
            Vector2 origin = new Vector2(tex.Width / 2, tex.Height / frameCount / 2);
            Vector2 drawPos = npc.Center + new Vector2(0f, npc.gfxOffY) - screenPos;
            SpriteEffects effects = npc.spriteDirection == 1 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;

            //微呼吸避免死板常亮
            float breathe = 0.85f + 0.15f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 4.6f + npc.whoAmI * 1.3f);
            float fade = npc.Opacity * breathe;
            Color halo = new Color(FishronMotionFX.SeaGreen.R, FishronMotionFX.SeaGreen.G,
                FishronMotionFX.SeaGreen.B, 0) * (0.16f * fade);
            Color rimBase = Color.Lerp(FishronMotionFX.SeaGreen, FishronMotionFX.FoamWhite, 0.45f);
            Color rim = new Color(rimBase.R, rimBase.G, rimBase.B, 0) * (0.32f * fade);

            for (int i = 0; i < 8; i++) {
                Vector2 off = (MathHelper.TwoPi * i / 8f).ToRotationVector2();
                //外圈软晕
                spriteBatch.Draw(tex, drawPos + off * 4.5f * npc.scale, frame, halo,
                    npc.rotation, origin, npc.scale, effects, 0f);
                //内圈亮边
                spriteBatch.Draw(tex, drawPos + off * 2f * npc.scale, frame, rim,
                    npc.rotation, origin, npc.scale, effects, 0f);
            }

            //末相电眼：眼位垫风暴色辉点+白热瞳芯（黑底 SoftGlow 走 A=0 加光），
            //沿速度向前找头位，脉动错相免得群鲨同闪成灯串
            if (DukeFishronAI.AnyPhaseThreeActive() && CWRAsset.SoftGlow?.Value != null) {
                Texture2D glow = CWRAsset.SoftGlow.Value;
                Vector2 ahead = npc.velocity.SafeNormalize(npc.rotation.ToRotationVector2());
                Vector2 eyePos = drawPos + ahead * (npc.width * 0.34f * npc.scale);
                float pulse = 0.8f + 0.2f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 9f + npc.whoAmI * 2.1f);
                Color eyeHalo = new Color(FishronMotionFX.StormBolt.R, FishronMotionFX.StormBolt.G,
                    FishronMotionFX.StormBolt.B, 0) * (0.6f * fade * pulse);
                spriteBatch.Draw(glow, eyePos, null, eyeHalo, 0f, glow.Size() / 2f,
                    0.55f * npc.scale, SpriteEffects.None, 0f);
                spriteBatch.Draw(glow, eyePos, null, new Color(255, 255, 255, 0) * (0.8f * fade * pulse),
                    0f, glow.Size() / 2f, 0.24f * npc.scale, SpriteEffects.None, 0f);
            }
            return null;
        }
    }

    /// <summary>龙卷甩出的大鲨鱼龙(Sharkron2)，同款描边</summary>
    internal class FishronSharkronGlow2 : FishronSharkronGlow
    {
        public override int TargetID => NPCID.Sharkron2;
    }
}
