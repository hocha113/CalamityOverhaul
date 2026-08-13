using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalKingSlime.Core;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalKingSlime.Rendering
{
    /// <summary>本体绘制：体内忍者→皇家凝胶身体(RoyalAura shader)→头顶扣冠→挤压拉伸弹簧形变</summary>
    internal static class KingSlimeRenderer
    {
        /// <summary>
        /// 原版对照：王冠不在本体贴图里，Main.DrawNPCDirect 对 type 50 用 TextureAssets.Extra[39]
        /// 独立补画一层(Main.cs:24942-24969)，锚 Center.Y-(70-帧偏移)*scale。
        /// 本管线接管本体绘制后在此复刻该层：贴图用同款王冠 Gore(与离体弹幕无缝衔接)，
        /// 位置改锚形变后的头顶，弹簧滞后做次级运动
        /// </summary>
        private const int CrownGoreID = Terraria.ID.GoreID.KingSlimeCrown;

        /// <summary>每帧推进压扁弹簧、摇晃衰减与扣冠滞后弹簧(各端本地)</summary>
        public static void UpdateSpring(KingSlimeStateContext ctx) {
            //弹簧回中
            ctx.SquashVelocity += (1f - ctx.VisualSquash) * 0.16f;
            ctx.SquashVelocity *= 0.8f;
            ctx.VisualSquash += ctx.SquashVelocity;
            ctx.VisualSquash = MathHelper.Clamp(ctx.VisualSquash, 0.28f, 1.9f);

            //摇晃衰减
            ctx.WobblePhase += 0.32f;
            ctx.WobbleAmp *= 0.93f;
            if (ctx.WobbleAmp < 0.004f) {
                ctx.WobbleAmp = 0f;
            }

            //扣冠滞后弹簧：起跳(vy<0)冠慢半拍下沉、下坠(vy>0)冠上浮，落地时砸沉再回弹
            NPC npc = ctx.Npc;
            float lagTarget = 0f;
            if (npc != null) {
                lagTarget = MathHelper.Clamp(-npc.velocity.Y * 0.9f, -14f, 10f);
                if (ctx.JustLanded) {
                    ctx.CrownLagVel += MathHelper.Clamp(ctx.LandingPower * 0.55f, 1.5f, 11f);
                }
            }
            ctx.CrownLagVel += (lagTarget - ctx.CrownLag) * 0.24f;
            ctx.CrownLagVel *= 0.72f;
            ctx.CrownLag = MathHelper.Clamp(ctx.CrownLag + ctx.CrownLagVel, -22f, 26f);
        }

        /// <summary>
        /// 扣冠锚点(世界系)：形变后头顶中心。服务端 frame 未必有效，回退 122px 帧高估算
        /// </summary>
        public static Vector2 CrownAnchorWorld(NPC npc, KingSlimeStateContext ctx) {
            float frameH = npc.frame.Height > 0 ? npc.frame.Height : 122f;
            float scaleY = npc.scale * MathHelper.Clamp(ctx.VisualSquash, 0.28f, 1.9f);
            float bottomY = npc.position.Y + npc.height + 4f;
            //冠心略沉入头顶(对照原版 Center.Y-70*scale ≈ 贴图顶下 6px)
            return new Vector2(npc.Center.X, bottomY - frameH * scaleY + 9f * scaleY);
        }

        /// <summary>身体绘制入口，返回false=已接管</summary>
        public static void DrawBody(SpriteBatch spriteBatch, NPC npc, KingSlimeStateContext ctx, Vector2 screenPos, Color drawColor) {
            if (ctx.HideBodySprite || ctx.BodyOpacity <= 0.01f) {
                return;
            }

            Texture2D bodyTex = TextureAssets.Npc[npc.type].Value;
            int frameCount = Main.npcFrameCount[npc.type];
            Rectangle frameRec = npc.frame;
            if (frameRec.Height <= 0) {
                frameRec = bodyTex.GetRectangle(0, frameCount);
            }
            //邻帧渗线防护：原版帧表零间距(帧高=贴图高/帧数，NPC.FindFrame NPC.cs:60035)，
            //非整数缩放下线性过滤在帧界会混入相邻帧边缘像素行——源矩形上下各内缩 1px
            if (frameRec.Height > 4) {
                frameRec.Y += 1;
                frameRec.Height -= 2;
            }

            //形变：压扁变宽、拉伸变窄，近似体积守恒
            float squash = ctx.VisualSquash;
            float wobble = ctx.WobbleAmp;
            float wobbleX = 1f + (float)Math.Sin(ctx.WobblePhase) * wobble;
            float wobbleY = 1f - (float)Math.Sin(ctx.WobblePhase + 1.1f) * wobble * 0.8f;
            float scaleY = npc.scale * squash * wobbleY;
            float scaleX = npc.scale * (1f + (1f - squash) * 0.85f) * wobbleX;

            //锚定底部：压扁时贴地不悬空
            Vector2 bottom = new Vector2(npc.Center.X, npc.position.Y + npc.height) - screenPos + new Vector2(0f, npc.gfxOffY + 4f);
            Vector2 origin = new Vector2(frameRec.Width * 0.5f, frameRec.Height);
            //立塔倾倒角，绕底部中心
            float lean = ctx.BodyLean;

            float opacity = ctx.BodyOpacity;
            Color bodyColor = drawColor * opacity;

            //---------------- 体内忍者 ----------------
            if (!ctx.NinjaGone) {
                DrawNinja(spriteBatch, npc, ctx, screenPos, drawColor, opacity, scaleX, scaleY, lean);
            }

            //---------------- 皇家凝胶身体 ----------------
            Effect aura = EffectLoader.KingSlimeRoyalAura?.Value;
            bool shaderOn = aura != null && opacity > 0.05f;
            if (shaderOn) {
                aura.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
                aura.Parameters["intensity"]?.SetValue(MathHelper.Clamp(0.55f + ctx.AuraProgress * 0.45f, 0f, 1f) * opacity);
                aura.Parameters["mode"]?.SetValue((float)ctx.AuraMode);
                aura.Parameters["progress"]?.SetValue(ctx.AuraProgress);
                aura.Parameters["texelSize"]?.SetValue(new Vector2(1f / bodyTex.Width, 1f / bodyTex.Height));
                //帧界 uv 范围：描边邻域采样越过帧界会把相邻帧实体像素当作轮廓画出横线
                aura.Parameters["uvFrame"]?.SetValue(new Vector4(
                    frameRec.X / (float)bodyTex.Width, frameRec.Y / (float)bodyTex.Height,
                    (frameRec.X + frameRec.Width) / (float)bodyTex.Width,
                    (frameRec.Y + frameRec.Height) / (float)bodyTex.Height));
                aura.Parameters["seed"]?.SetValue(npc.whoAmI * 0.173f % 1f);
                aura.Parameters["royalCore"]?.SetValue(KingSlimeGelFX.CrownGold.ToVector3());
                aura.Parameters["royalEdge"]?.SetValue(new Vector3(0.42f, 0.5f, 1f));

                spriteBatch.End();
                spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, Main.DefaultSamplerState,
                    DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
                aura.CurrentTechnique.Passes[0].Apply();
            }

            SpriteEffects flip = npc.spriteDirection > 0 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
            spriteBatch.Draw(bodyTex, bottom, frameRec, bodyColor, lean,
                origin, new Vector2(scaleX, scaleY), flip, 0f);

            if (shaderOn) {
                spriteBatch.End();
                spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                    DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
            }

            //蓄力/狂暴时体表加一圈微弱加色轮廓光
            if (ctx.AuraProgress > 0.35f) {
                Color glow = KingSlimeGelFX.GelFoam with { A = 0 };
                spriteBatch.Draw(bodyTex, bottom, frameRec, glow * ((ctx.AuraProgress - 0.35f) * 0.3f * opacity), lean,
                    origin, new Vector2(scaleX * 1.03f, scaleY * 1.03f), flip, 0f);
            }

            //---------------- 头顶扣冠(复刻原版独立王冠层) ----------------
            DrawMountedCrown(spriteBatch, npc, ctx, screenPos, drawColor, opacity, scaleY, lean);

            //入场王冠天降(纯演出层)
            DrawIntroCrownDrop(spriteBatch, npc, ctx, screenPos);
        }

        /// <summary>
        /// 默认态扣冠：稳固扣在形变后的头顶，随压扁下沉/拉伸抬升，弹簧滞后给重量感。<br/>
        /// 王冠离体期(存在 BKSCrownProj)与状态声明隐藏时不绘制
        /// </summary>
        private static void DrawMountedCrown(SpriteBatch spriteBatch, NPC npc, KingSlimeStateContext ctx,
            Vector2 screenPos, Color drawColor, float opacity, float scaleY, float lean) {
            if (ctx.HideCrown || ctx.FindCrown() != null) {
                return;
            }

            Main.instance.LoadGore(CrownGoreID);
            Texture2D crown = TextureAssets.Gore[CrownGoreID].Value;

            //锚形变后头顶：与身体同一 bottom/scaleY 推导，压扁自动下沉、拉伸自动抬升
            float frameH = npc.frame.Height > 0 ? npc.frame.Height : crown.Height * 4f;
            Vector2 bottom = new Vector2(npc.Center.X, npc.position.Y + npc.height) - screenPos + new Vector2(0f, npc.gfxOffY + 4f);
            Vector2 offset = new Vector2(0f, -frameH * scaleY + 9f * scaleY + ctx.CrownLag);
            if (lean != 0f) {
                offset = offset.RotatedBy(lean);
            }
            Vector2 pos = bottom + offset;

            //原版扣冠不随 npc.scale 缩放(Main.cs:24969 恒 1f)；分裂收核期跟随 ScaleMul 缩小
            float scale = MathHelper.Clamp(ctx.ScaleMul, 0.55f, 1f);
            //倾角：立塔倾倒+横移小晃
            float rot = lean + npc.velocity.X * 0.02f;

            Color color = drawColor * opacity;
            Vector2 origin = crown.Size() * 0.5f;
            spriteBatch.Draw(crown, pos, null, color, rot, origin, scale, SpriteEffects.None, 0f);
            //金属泽光(与离体弹幕同款，交接不跳变)
            spriteBatch.Draw(crown, pos, null, KingSlimeGelFX.CrownGold with { A = 0 } * (0.35f * opacity),
                rot, origin, scale * 1.03f, SpriteEffects.None, 0f);
        }

        /// <summary>入场演出：王冠从天而降扣上头顶，加速下落+金色残影</summary>
        private static void DrawIntroCrownDrop(SpriteBatch spriteBatch, NPC npc, KingSlimeStateContext ctx, Vector2 screenPos) {
            float t = ctx.IntroCrownDrop;
            if (t <= 0f || t > 1f) {
                return;
            }
            Main.instance.LoadGore(CrownGoreID);
            Texture2D crown = TextureAssets.Gore[CrownGoreID].Value;
            //加速坠落：ease-in二次；终点=扣冠锚点，命中帧与常驻扣冠层无缝交接
            float fall = t * t;
            Vector2 dest = CrownAnchorWorld(npc, ctx);
            Vector2 pos = dest - new Vector2(0f, (1f - fall) * 620f) - screenPos;
            Vector2 origin = crown.Size() * 0.5f;

            //金色坠落残影
            for (int i = 1; i <= 3; i++) {
                Vector2 ghost = pos - new Vector2(0f, i * 26f * t);
                spriteBatch.Draw(crown, ghost, null, KingSlimeGelFX.CrownGold with { A = 0 } * (0.3f - i * 0.08f),
                    0f, origin, 1f, SpriteEffects.None, 0f);
            }
            spriteBatch.Draw(crown, pos, null, Color.White, 0f, origin, 1f, SpriteEffects.None, 0f);
            spriteBatch.Draw(crown, pos, null, KingSlimeGelFX.CrownGold with { A = 0 } * 0.4f, 0f, origin, 1.04f, SpriteEffects.None, 0f);
        }

        /// <summary>体内忍者：速度滞后漂移，影袭前摇发亮</summary>
        private static void DrawNinja(SpriteBatch spriteBatch, NPC npc, KingSlimeStateContext ctx, Vector2 screenPos,
            Color drawColor, float opacity, float scaleX, float scaleY, float lean) {
            Texture2D ninja = TextureAssets.Ninja.Value;
            //滞后：身体动、忍者拖
            Vector2 lag = new Vector2(-npc.velocity.X * 2f, -npc.velocity.Y);
            //压扁时忍者被压低
            float squashDrop = (1f - MathHelper.Clamp(ctx.VisualSquash, 0.3f, 1f)) * npc.height * 0.3f;
            lag.Y += squashDrop;
            //限制在体内
            float maxLag = 24f * npc.scale;
            if (lag.Length() > maxLag) {
                lag = lag.SafeNormalize(Vector2.Zero) * maxLag;
            }

            Vector2 pos = npc.Center - screenPos + lag + new Vector2(0f, npc.gfxOffY);
            //随倾倒角绕底部中心旋转
            if (lean != 0f) {
                Vector2 pivot = new Vector2(npc.Center.X, npc.position.Y + npc.height) - screenPos;
                pos = pivot + (pos - pivot).RotatedBy(lean);
            }
            float rot = npc.velocity.X * 0.05f + lean;
            Rectangle rec = new Rectangle(0, 0, ninja.Width, ninja.Height);
            Vector2 origin = rec.Size() * 0.5f;

            spriteBatch.Draw(ninja, pos, rec, drawColor * (opacity * 0.9f), rot, origin, 1f, SpriteEffects.None, 0f);

            //影袭前摇：忍者剪影亮起冷白
            if (ctx.NinjaGlow > 0.01f) {
                Color glow = new Color(200, 226, 255, 0) * ctx.NinjaGlow * opacity;
                spriteBatch.Draw(ninja, pos, rec, glow, rot, origin, 1.04f, SpriteEffects.None, 0f);
                float flicker = 0.5f + 0.5f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 26f);
                spriteBatch.Draw(ninja, pos, rec, glow * (0.5f * flicker), rot, origin, 1.12f, SpriteEffects.None, 0f);
            }
        }
    }
}
