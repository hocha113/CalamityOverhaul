using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDomains;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDreams
{
    /// <summary>
    /// 湖镜里的倒影恶犬：原版狼贴图 + <c>KikasaHound.fx</c> 湿墨黑犬材质，
    /// 绕 LakeWorldY 垂直镜像跟随玩家动作（帧逻辑移植原版狼 FindFrame）。
    /// 画在领域镜面（TechUnify）之后——镜面是拷屏合成，画早了会被镜像换掉；
    /// 黑犬体量大于玩家镜像，直接盖住原本的人影。
    /// 纯观看端表现，状态开关随 <see cref="KikasaDomainPlayer.HoundReflection"/> 快照同步
    /// </summary>
    internal static class KikasaHoundReflection
    {
        /// <summary>犬体缩放：要盖得住玩家的镜像，也要压得住场。转场镜面共用同一几何</summary>
        internal const float HoundScale = 1.28f;

        /// <summary>眼睛在帧内的原生 uv（贴图面向左）；素材校准位，游戏内再调</summary>
        internal static readonly Vector2 EyeAnchor = new(0.17f, 0.38f);

        //每个施术者一份的观看端动画态
        private static readonly int[] frames = new int[Main.maxPlayers];
        private static readonly float[] frameCounters = new float[Main.maxPlayers];
        private static readonly float[] gazes = new float[Main.maxPlayers];
        private static readonly float[] appears = new float[Main.maxPlayers];
        private static readonly bool[] growlLatches = new bool[Main.maxPlayers];
        //朝向只认一个极性：走动跟速度，站住锁存；避免一停一动翻面
        private static readonly int[] facings = new int[Main.maxPlayers];

        internal static void Clear() {
            Array.Clear(frames);
            Array.Clear(frameCounters);
            Array.Clear(gazes);
            Array.Clear(appears);
            Array.Clear(growlLatches);
            Array.Clear(facings);
        }

        /// <summary>转场镜面注入犬影时取当前帧，动作连续不跳帧</summary>
        internal static int GetFrame(int who)
            => who >= 0 && who < frames.Length ? frames[who] : 0;

        /// <summary>转场镜面与倒影同一副朝向</summary>
        internal static int GetFacing(int who)
            => who >= 0 && who < facings.Length && facings[who] != 0 ? facings[who] : 1;

        /// <summary>倒影出没渐变，抹除玩家镜像的遮罩跟它同步淡入淡出</summary>
        internal static float GetAppear(int who)
            => who >= 0 && who < appears.Length ? appears[who] : 0f;

        /// <summary>在镜面合成后调用。批次自管，世界坐标经视图矩阵</summary>
        internal static void Draw(SpriteBatch spriteBatch, KikasaDomainPlayer kdp) {
            Player caster = kdp.Player;
            if (caster?.active != true) {
                return;
            }
            int who = caster.whoAmI;

            //出没渐变：倒影醒/睡、玩家潜入水下、梦侧无湖，都走同一条淡出
            bool visible = kdp.HoundReflection && !caster.dead
                && !kdp.DreamWorldVisual
                && caster.Center.Y < kdp.LakeWorldY - 4f;
            float target = visible ? 1f : 0f;
            appears[who] = MathHelper.Lerp(appears[who], target, 0.10f);
            if (appears[who] < 0.02f) {
                appears[who] = target > 0f ? appears[who] : 0f;
                frames[who] = 3;
                frameCounters[who] = 0f;
                gazes[who] = 0f;
                return;
            }

            //湖没涨起来，镜子还没成形
            float riseGate = MathHelper.Clamp((kdp.RiseProgress - 0.55f) / 0.35f, 0f, 1f);
            float alpha = appears[who] * riseGate;
            if (alpha < 0.02f) {
                return;
            }

            UpdateAnimation(who, caster);
            UpdateGaze(who, caster, kdp);
            //走动跟速度；站住保持上一帧，不另读 direction（符号一换就会抽）
            if (MathF.Abs(caster.velocity.X) > 0.3f) {
                facings[who] = caster.velocity.X > 0f ? 1 : -1;
            }
            else if (facings[who] == 0) {
                facings[who] = caster.direction != 0 ? caster.direction : 1;
            }

            Main.instance.LoadNPC(NPCID.Wolf);
            Texture2D tex = TextureAssets.Npc[NPCID.Wolf].Value;
            if (tex == null) {
                return;
            }

            int frameCount = Main.npcFrameCount[NPCID.Wolf];
            int frameH = tex.Height / frameCount;
            //源矩形上下各内缩 1px + shader 帧界钳制，双通道防帧表渗色
            Rectangle source = new(0, frames[who] * frameH + 1, tex.Width, frameH - 2);

            float width = tex.Width * HoundScale;
            float height = source.Height * HoundScale;
            //镜像几何：玩家脚底关于水线的映像是犬爪线，也就是翻转后贴图的顶边
            float quadTopY = 2f * kdp.LakeWorldY - caster.Bottom.Y;
            Vector2 topLeft = new(caster.Center.X - width * 0.5f, quadTopY);

            Effect hound = EffectLoader.KikasaHound?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;

            //垂直倒影仍走 SpriteEffects；水平翻转交给 KikasaHound.fx 的 uFlipH
            //（Immediate + 自定义像素着色器时 SpriteEffects 水平翻转经常不进 TEXCOORD）
            bool faceRight = facings[who] > 0;
            SpriteEffects effects = SpriteEffects.FlipVertically;

            spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend,
                SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone,
                null, Main.GameViewMatrix.TransformationMatrix);

            if (hound != null && noise != null) {
                Main.instance.GraphicsDevice.Textures[1] = noise;
                Main.instance.GraphicsDevice.SamplerStates[1] = SamplerState.LinearWrap;

                //凝视与转场窥犬取更亮者：驻留段那双眼睛必须燃起来
                float eyeGlow = 0.34f + 0.66f * gazes[who];
                eyeGlow = MathF.Max(eyeGlow, kdp.DreamGaze);

                hound.Parameters["uTime"]?.SetValue(kdp.EffectTime);
                hound.Parameters["uSeed"]?.SetValue(who * 0.613f + 0.37f);
                hound.Parameters["uUvRect"]?.SetValue(new Vector4(
                    0f, source.Y / (float)tex.Height,
                    1f, source.Height / (float)tex.Height));
                hound.Parameters["uTexel"]?.SetValue(new Vector2(1f / tex.Width, 1f / tex.Height));
                hound.Parameters["uAspect"]?.SetValue(tex.Width / (float)source.Height);
                hound.Parameters["uFlipH"]?.SetValue(faceRight ? 1f : 0f);
                hound.Parameters["uFlipV"]?.SetValue(1f);
                hound.Parameters["uMode"]?.SetValue(0f);
                //犬背贴水线才有湿缝，沉深了自然没有
                hound.Parameters["uSeamGate"]?.SetValue(
                    MathHelper.Clamp(1f - (quadTopY - kdp.LakeWorldY) / 48f, 0f, 1f));
                hound.Parameters["uWobble"]?.SetValue(
                    0.010f + 0.018f * kdp.FoamBoost + 0.05f * kdp.DreamBoil);
                hound.Parameters["uEyeGlow"]?.SetValue(eyeGlow);
                hound.Parameters["uEyeAnchor"]?.SetValue(EyeAnchor);
                hound.Parameters["uDissolve"]?.SetValue(0f);
                hound.Parameters["uEdgeTint"]?.SetValue(KikasaDomain.CoolTint(
                    new Color(112, 26, 26), new Color(42, 58, 66)).ToVector3());
                hound.CurrentTechnique = hound.Techniques["TechHound"];
                hound.CurrentTechnique.Passes[0].Apply();

                spriteBatch.Draw(tex, topLeft - Main.screenPosition, source,
                    Color.White * alpha, 0f, Vector2.Zero, HoundScale, effects, 0f);
            }
            else {
                //着色器缺失：近黑剪影回退，水平翻转改回 SpriteEffects
                SpriteEffects fallback = effects
                    | (faceRight ? SpriteEffects.FlipHorizontally : SpriteEffects.None);
                spriteBatch.Draw(tex, topLeft - Main.screenPosition, source,
                    new Color(12, 6, 9) * (alpha * 0.9f), 0f, Vector2.Zero, HoundScale, fallback, 0f);
            }

            spriteBatch.End();
        }

        //帧逻辑移植原版狼（NPC.cs FindFrame case 155）：
        //跃起 10、下坠 11、落地过渡 12、跑动循环 3-9。
        //静立不用帧 0：那是转身序列的第一拍，倒过来看会像面朝反了；
        //改停在跑动起手帧 3，和移动同一套朝向，一停一动不会翻面。

        private static void UpdateAnimation(int who, Player caster) {
            float vx = caster.velocity.X;
            float vy = caster.velocity.Y;
            int frame = frames[who];

            if (vy < -0.1f) {
                frame = 10;
                frameCounters[who] = 0f;
            }
            else if (vy > 0.1f) {
                frame = 11;
                frameCounters[who] = 0f;
            }
            else if (MathF.Abs(vx) < 0.1f) {
                frame = 3;
                frameCounters[who] = 0f;
            }
            else {
                frameCounters[who] += MathF.Abs(vx) * 0.4f;
                if (frame == 10 || frame == 11) {
                    //落回水面的过渡拍
                    frame = 12;
                    frameCounters[who] = 0f;
                }
                else if (frameCounters[who] > 8f) {
                    frameCounters[who] -= 8f;
                    frame++;
                    if (frame > 9 || frame < 3) {
                        frame = 3;
                    }
                }
            }
            frames[who] = frame;
        }

        //凝视：人越站得住，水里那双眼睛越亮；亮透时喉底一声低应

        private static void UpdateGaze(int who, Player caster, KikasaDomainPlayer kdp) {
            bool still = MathF.Abs(caster.velocity.X) < 0.4f
                && MathF.Abs(caster.velocity.Y) < 0.2f;
            gazes[who] = MathHelper.Clamp(
                gazes[who] + (still ? 1f / 170f : -0.06f), 0f, 1f);

            if (gazes[who] > 0.92f && !growlLatches[who]) {
                growlLatches[who] = true;
                SoundEngine.PlaySound(SoundID.Roar with {
                    Pitch = -1f,
                    Volume = 0.2f,
                    MaxInstances = 2,
                }, new Vector2(caster.Center.X, kdp.LakeWorldY + 60f));
            }
            else if (gazes[who] < 0.3f) {
                growlLatches[who] = false;
            }
        }
    }
}
