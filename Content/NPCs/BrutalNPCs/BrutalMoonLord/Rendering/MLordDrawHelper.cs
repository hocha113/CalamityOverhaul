using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMoonLord.Core;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMoonLord.Rendering
{
    /// <summary>手/头眼窝姿态（部件 AI 每帧本地推导，绘制消费）</summary>
    internal struct MLordEyePose
    {
        /// <summary>瞳孔朝向角</summary>
        public float PupilAngle;
        /// <summary>瞳孔离心度 0~1</summary>
        public float PupilOut;
        /// <summary>已破坏（画蠕动残口）</summary>
        public bool Broken;
        /// <summary>残口蠕动计时</summary>
        public float WriggleTimer;
        /// <summary>发光强度 0~1（攻击窗提亮）</summary>
        public float Glow;
    }

    /// <summary>
    /// 月总拼装绘制：复刻原版 Extra 贴图组装（披风/胸甲/骨臂 IK/眼窝/瞳孔/眼睑/口须），
    /// 叠加天体辉光层。骨臂超出 IK 全长时切幻影拉伸绘制
    /// </summary>
    internal static class MLordDrawHelper
    {
        /// <summary>原版月总的受光色：局部光照与白光 3:7 混合</summary>
        public static Color CommonLight(NPC npc) {
            Point tile = npc.Center.ToTileCoordinates();
            return npc.GetAlpha(Color.Lerp(Lighting.GetColor(tile.X, tile.Y), Color.White, 0.3f));
        }

        #region 核心拼装

        /// <summary>核心整体：披风双翼→肩臂 IK→胸甲→心脏帧 + 心脏辉光</summary>
        public static void DrawCoreAssembly(SpriteBatch spriteBatch, NPC core, Vector2 screenPos, MLordContext context) {
            Texture2D heartTex = TextureAssets.Npc[NPCID.MoonLordCore].Value;
            Texture2D chestTex = TextureAssets.Extra[16].Value;
            Texture2D upperArmTex = TextureAssets.Extra[14].Value;
            Texture2D mantleTex = TextureAssets.Extra[13].Value;

            Color light = CommonLight(core);
            Vector2 center = core.Center;
            MLordPartsStatus parts = MLordFacts.ScanParts(core);

            //肩→手骨臂（每侧）
            for (int side = 0; side < 2; side++) {
                int handIndex = side == 0 ? parts.LeftHand : parts.RightHand;
                if (handIndex < 0 || !Main.npc[handIndex].active) {
                    continue;
                }
                DrawUpperArm(spriteBatch, upperArmTex, core, Main.npc[handIndex], side, light, screenPos);
            }

            //披风双翼（镜像两半）
            Vector2 mantleOriginL = new(mantleTex.Width, 278f);
            Vector2 mantleOriginR = new(0f, 278f);
            spriteBatch.Draw(mantleTex, center - screenPos, null, light, core.rotation, mantleOriginL, 1f, SpriteEffects.None, 0f);
            spriteBatch.Draw(mantleTex, center - screenPos, null, light, core.rotation, mantleOriginR, 1f, SpriteEffects.FlipHorizontally, 0f);

            //胸甲
            spriteBatch.Draw(chestTex, center - screenPos, null, light, core.rotation, new Vector2(112f, 101f), 1f, SpriteEffects.None, 0f);

            //心脏（帧由 FindFrame 驱动）
            spriteBatch.Draw(heartTex, center - screenPos, core.frame, light, core.rotation,
                core.frame.Size() / 2f, 1f, SpriteEffects.None, 0f);

            //心脏辉光：裸露度驱动的搏动光晕
            float exposure = context?.HeartExposure ?? 0f;
            if (exposure > 0.05f) {
                Texture2D glow = CWRAsset.DiffusionCircle?.Value;
                if (glow != null) {
                    float pulse = 0.75f + 0.25f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 6.4f);
                    Main.EntitySpriteDraw(glow, center - screenPos, null,
                        MLordDirector.Phantasmal with { A = 0 } * (0.75f * exposure * pulse), 0f,
                        glow.Size() / 2f, 0.62f * exposure * pulse, SpriteEffects.None, 0);
                    Main.EntitySpriteDraw(glow, center - screenPos, null,
                        MLordDirector.MoonWhite with { A = 0 } * (0.4f * exposure * pulse), 0f,
                        glow.Size() / 2f, 0.3f * exposure, SpriteEffects.None, 0);
                }
            }

            //蓄力星环观感
            if (context != null && context.IsCharging && context.ChargeProgress > 0.02f) {
                Texture2D star = CWRAsset.StarTexture?.Value;
                if (star != null) {
                    float progress = context.ChargeProgress;
                    Main.EntitySpriteDraw(star, center - screenPos, null,
                        MLordDirector.Phantasmal with { A = 0 } * (0.55f * progress),
                        Main.GlobalTimeWrappedHourly * 2.2f, star.Size() / 2f,
                        0.7f + progress * 0.7f, SpriteEffects.None, 0);
                }
            }
        }

        /// <summary>核心侧上臂：肩锚 acos IK 指向手，超程收直；肩锚随核心倾斜旋转防披风错位</summary>
        private static void DrawUpperArm(SpriteBatch spriteBatch, Texture2D tex, NPC core, NPC hand,
            int side, Color light, Vector2 screenPos) {
            float dir = side == 0 ? -1f : 1f;
            Vector2 shoulder = core.Center + new Vector2(MLordDirector.ShoulderOffset.X * dir, MLordDirector.ShoulderOffset.Y)
                .RotatedBy(core.rotation);
            Vector2 handAnchor = hand.Center + new Vector2(0f, 76f);
            Vector2 half = (handAnchor - shoulder) * 0.5f;

            float ratio = half.Length() / MLordDirector.ArmIKLength;
            float bend = ratio >= 1f ? 0f : (float)Math.Acos(ratio) * -dir;
            SpriteEffects effects = side == 0 ? SpriteEffects.None : SpriteEffects.FlipHorizontally;
            Vector2 origin = new(76f, 66f);
            if (side != 0) {
                origin.X = tex.Width - origin.X;
            }

            float alpha = ratio >= 1f ? MathHelper.Clamp(2.2f - ratio, 0.25f, 1f) : 1f;
            spriteBatch.Draw(tex, shoulder - screenPos, null, light * alpha,
                half.ToRotation() - bend - MathHelper.PiOver2, origin, 1f, effects, 0f);

            //超程幻影补桥：肩到手的星尘光带
            if (ratio > 1f) {
                DrawSpectralBridge(shoulder, handAnchor, MathHelper.Clamp(ratio - 1f, 0f, 1f));
            }
        }

        /// <summary>骨臂超程时的幻影星桥</summary>
        private static void DrawSpectralBridge(Vector2 from, Vector2 to, float strength) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            Vector2 delta = to - from;
            float len = delta.Length();
            float rot = delta.ToRotation();
            Vector2 screenFrom = from - Main.screenPosition;
            float pulse = 0.6f + 0.4f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 12f);
            Main.EntitySpriteDraw(pixel, screenFrom, null,
                MLordDirector.DeepViolet with { A = 0 } * (0.4f * strength * pulse), rot,
                new Vector2(0f, pixel.Height * 0.5f), new Vector2(len / pixel.Width, 10f / pixel.Height), SpriteEffects.None, 0);
            Main.EntitySpriteDraw(pixel, screenFrom, null,
                MLordDirector.Phantasmal with { A = 0 } * (0.65f * strength * pulse), rot,
                new Vector2(0f, pixel.Height * 0.5f), new Vector2(len / pixel.Width, 3.2f / pixel.Height), SpriteEffects.None, 0);
        }

        #endregion

        #region 手部拼装

        /// <summary>手：小臂 IK→眼窝（或残口）→瞳孔→手壳 + 辉光</summary>
        public static void DrawHandAssembly(SpriteBatch spriteBatch, NPC hand, Vector2 screenPos,
            in MLordEyePose pose, int gripFrame) {
            NPC core = MLordFacts.GetCore(hand);
            Texture2D handTex = TextureAssets.Npc[NPCID.MoonLordHand].Value;
            Texture2D forearmTex = TextureAssets.Extra[15].Value;
            Texture2D socketTex = TextureAssets.Extra[17].Value;
            Texture2D pupilTex = TextureAssets.Extra[19].Value;
            Texture2D brokenTex = TextureAssets.Extra[26].Value;

            Color light = CommonLight(hand);
            bool isLeft = (int)hand.ai[MLordAiSlots.HandSide] == 0;
            float dir = isLeft ? -1f : 1f;
            SpriteEffects effects = isLeft ? SpriteEffects.None : SpriteEffects.FlipHorizontally;

            //小臂 IK（核心在场才画；肩锚随核心倾斜，与上臂同锚）
            if (core != null) {
                Vector2 shoulder = core.Center + new Vector2(MLordDirector.ShoulderOffset.X * dir, MLordDirector.ShoulderOffset.Y)
                    .RotatedBy(core.rotation);
                Vector2 handAnchor = hand.Center + new Vector2(0f, 76f);
                Vector2 half = (shoulder - handAnchor) * 0.5f;
                float ratio = half.Length() / MLordDirector.ArmIKLength;
                float bend = ratio >= 1f ? 0f : (float)Math.Acos(ratio) * -dir;
                Vector2 origin = new(60f, 30f);
                if (!isLeft) {
                    origin.X = forearmTex.Width - origin.X;
                }
                float alpha = ratio >= 1f ? MathHelper.Clamp(2.2f - ratio, 0.25f, 1f) : 1f;
                spriteBatch.Draw(forearmTex, handAnchor - screenPos, null, light * alpha,
                    half.ToRotation() + bend - MathHelper.PiOver2, origin, 1f, effects, 0f);
            }

            Vector2 socketOrigin = new(26f, 42f);
            if (!isLeft) {
                socketOrigin.X = socketTex.Width - socketOrigin.X;
            }

            if (pose.Broken) {
                //蠕动残口（4 帧）
                Rectangle frame = brokenTex.Frame(1, 4, 0, (int)(pose.WriggleTimer / 8f) % 4);
                spriteBatch.Draw(brokenTex, hand.Center - screenPos, frame, light, 0f,
                    socketOrigin - new Vector2(4f, 4f), 1f, effects, 0f);
                DrawVoidLeak(hand.Center, 0.5f);
            }
            else {
                //眼窝 + 瞳孔（椭圆轨道）
                spriteBatch.Draw(socketTex, hand.Center - screenPos, null, light, 0f, socketOrigin, 1f, effects, 0f);
                Vector2 pupilOffset = Utils.Vector2FromElipse(pose.PupilAngle.ToRotationVector2(),
                    new Vector2(30f, 66f) * pose.PupilOut);
                Vector2 jitter = new(1f * -dir, 3f);
                spriteBatch.Draw(pupilTex, hand.Center - screenPos + pupilOffset + jitter, null, light, 0f,
                    pupilTex.Size() / 2f, 1f, SpriteEffects.None, 0f);
                DrawEyeGlow(hand.Center + pupilOffset + jitter, pose.Glow);
            }

            //手壳（抓握帧）
            Rectangle handFrame = handTex.Frame(1, 4, 0, Math.Clamp(gripFrame, 0, 3));
            Vector2 handOrigin = new(120f, 180f);
            if (!isLeft) {
                handOrigin.X = handTex.Width - handOrigin.X;
            }
            spriteBatch.Draw(handTex, hand.Center - screenPos, handFrame, light, 0f, handOrigin, 1f, effects, 0f);

            //高速冲线的星尘残影
            float speed = hand.velocity.Length();
            if (speed > 16f) {
                Texture2D glowTex = CWRAsset.DiffusionCircle?.Value;
                if (glowTex != null) {
                    float heat = MathHelper.Clamp((speed - 16f) / 30f, 0f, 1f);
                    Vector2 back = -hand.velocity.SafeNormalize(Vector2.Zero);
                    for (int i = 1; i <= 3; i++) {
                        Main.EntitySpriteDraw(glowTex, hand.Center - screenPos + back * (i * 26f), null,
                            MLordDirector.Phantasmal with { A = 0 } * (heat * (0.5f - i * 0.13f)), 0f,
                            glowTex.Size() / 2f, 0.6f - i * 0.12f, SpriteEffects.None, 0);
                    }
                }
            }
        }

        #endregion

        #region 头部拼装

        /// <summary>头：眼窝（或残口）→瞳孔→颅骨→眼睑帧→口须帧 + 辉光</summary>
        public static void DrawHeadAssembly(SpriteBatch spriteBatch, NPC head, Vector2 screenPos,
            in MLordEyePose pose, int eyelidFrame, int mouthFrame) {
            Texture2D skullTex = TextureAssets.Npc[NPCID.MoonLordHead].Value;
            Texture2D socketTex = TextureAssets.Extra[18].Value;
            Texture2D pupilTex = TextureAssets.Extra[19].Value;
            Texture2D brokenTex = TextureAssets.Extra[26].Value;
            Texture2D eyelidTex = TextureAssets.Extra[29].Value;
            Texture2D mouthTex = TextureAssets.Extra[25].Value;

            Color light = CommonLight(head);
            Vector2 socketOrigin = new(19f, 34f);

            if (pose.Broken) {
                Rectangle frame = brokenTex.Frame(1, 4, 0, (int)(pose.WriggleTimer / 8f) % 4);
                spriteBatch.Draw(brokenTex, head.Center - screenPos, frame, light, head.rotation,
                    socketOrigin + new Vector2(4f, 4f), 1f, SpriteEffects.None, 0f);
                DrawVoidLeak(head.Center, 0.6f);
            }
            else {
                spriteBatch.Draw(socketTex, head.Center - screenPos, null, light, head.rotation, socketOrigin, 1f, SpriteEffects.None, 0f);
                Vector2 pupilOffset = Utils.Vector2FromElipse(pose.PupilAngle.ToRotationVector2(),
                    new Vector2(27f, 59f) * pose.PupilOut);
                spriteBatch.Draw(pupilTex, head.Center - screenPos + pupilOffset, null, light, head.rotation,
                    pupilTex.Size() / 2f, 1f, SpriteEffects.None, 0f);
                DrawEyeGlow(head.Center + pupilOffset, pose.Glow);
            }

            //颅骨
            spriteBatch.Draw(skullTex, head.Center - screenPos, skullTex.Frame(), light, head.rotation,
                new Vector2(191f, 130f), 1f, SpriteEffects.None, 0f);

            //眼睑（4 帧，闭合=无敌可读信号）
            Rectangle eyelidRect = eyelidTex.Frame(1, 4, 0, Math.Clamp(eyelidFrame, 0, 3));
            Vector2 eyelidPos = (head.Center - screenPos + new Vector2(0f, 4f).RotatedBy(head.rotation)).Floor();
            spriteBatch.Draw(eyelidTex, eyelidPos, eyelidRect, light, head.rotation,
                eyelidRect.Size() / 2f, 1f, SpriteEffects.None, 0f);

            //口须（3 帧）
            Rectangle mouthRect = mouthTex.Frame(1, 3, 0, Math.Clamp(mouthFrame, 0, 2));
            Vector2 mouthPos = (head.Center - screenPos + new Vector2(0f, 214f).RotatedBy(head.rotation)).Floor();
            spriteBatch.Draw(mouthTex, mouthPos, mouthRect, light, head.rotation,
                mouthRect.Size() / 2f, 1f, SpriteEffects.None, 0f);
        }

        #endregion

        #region 真眼拼装

        /// <summary>真眼：本体帧 + 瞳孔 + 相位辉光</summary>
        public static void DrawFreeEyeAssembly(SpriteBatch spriteBatch, NPC eye, Vector2 screenPos,
            in MLordEyePose pose, int bodyFrame, float scalePulse) {
            Texture2D bodyTex = TextureAssets.Npc[NPCID.MoonLordFreeEye].Value;
            Texture2D pupilTex = TextureAssets.Extra[19].Value;
            Color light = CommonLight(eye);

            Rectangle frame = bodyTex.Frame(1, 4, 0, Math.Clamp(bodyFrame, 0, 3));
            spriteBatch.Draw(bodyTex, eye.Center - screenPos, frame, light, eye.rotation,
                new Vector2(40f, 40f), 1f, SpriteEffects.None, 0f);

            Vector2 pupilOffset = Utils.Vector2FromElipse(pose.PupilAngle.ToRotationVector2(),
                new Vector2(30f, 30f) * pose.PupilOut);
            spriteBatch.Draw(pupilTex, eye.Center - screenPos + pupilOffset, null, light, eye.rotation,
                pupilTex.Size() / 2f, scalePulse, SpriteEffects.None, 0f);
            DrawEyeGlow(eye.Center + pupilOffset, pose.Glow);
        }

        #endregion

        #region 辉光小件

        /// <summary>瞳孔辉光：攻击窗提亮的相位光斑（强度=0 不画）</summary>
        private static void DrawEyeGlow(Vector2 worldPos, float glow) {
            if (glow <= 0.03f) {
                return;
            }
            Texture2D tex = CWRAsset.DiffusionCircle?.Value;
            if (tex == null) {
                return;
            }
            float pulse = 0.8f + 0.2f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 11f + worldPos.X * 0.01f);
            Main.EntitySpriteDraw(tex, worldPos - Main.screenPosition, null,
                MLordDirector.Phantasmal with { A = 0 } * (0.8f * glow * pulse), 0f,
                tex.Size() / 2f, 0.24f * glow * pulse, SpriteEffects.None, 0);
        }

        /// <summary>破坏残口的虚空渗光</summary>
        private static void DrawVoidLeak(Vector2 worldPos, float strength) {
            Texture2D tex = CWRAsset.DiffusionCircle?.Value;
            if (tex == null) {
                return;
            }
            float pulse = 0.6f + 0.4f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 5f + worldPos.Y * 0.008f);
            Main.EntitySpriteDraw(tex, worldPos - Main.screenPosition, null,
                MLordDirector.DeepViolet with { A = 0 } * (0.55f * strength * pulse), 0f,
                tex.Size() / 2f, 0.4f * strength * pulse, SpriteEffects.None, 0);
        }

        #endregion
    }
}
