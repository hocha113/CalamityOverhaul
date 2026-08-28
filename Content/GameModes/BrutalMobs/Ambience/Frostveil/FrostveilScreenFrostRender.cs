using InnoVault.RenderHandles;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Ambience.Frostveil
{
    /// <summary>
    /// 雪原镜头层：白毛风暴露度的屏边霜花（渐浓的冰晶星簇+雾凇角雾）、
    /// 满值瞬间的白闪（冰晶迸裂缘）、风雪墙经过时的视野雪幕白化（含镜前斜扫雪丝）。
    /// 挂 EndCaptureDraw 画在一切后效之上（霜结在镜头上），全部真 alpha 亮层，
    /// 布局由确定性哈希生成，零逐帧分配、无状态更新
    /// </summary>
    internal sealed class FrostveilScreenFrostRender : RenderHandle
    {
        /// <summary>批次槽位 1.61（本槽位分配值）</summary>
        public override float Weight => 1.61f;

        //镜前雪丝与冰晶迸裂贴图（Masking alpha 表已核：两者均为真 alpha）
        [VaultLoaden(CWRConstant.Masking)]
        private static Asset<Texture2D> Extra_98 = null;
        [VaultLoaden(CWRConstant.Masking)]
        private static Asset<Texture2D> Flashimpact2 = null;

        /// <summary>每角冰晶星数</summary>
        private const int StarsPerCorner = 10;

        private static readonly Color FrostTint = new(215, 240, 255);
        private static readonly Color FogTint = new(205, 232, 252);

        private static float Hash01(int i) {
            float f = MathF.Sin(i * 12.9898f + 78.233f) * 43758.5453f;
            return f - MathF.Floor(f);
        }

        public override void EndCaptureDraw(SpriteBatch spriteBatch,
            GraphicsDevice graphicsDevice, RenderTarget2D screenSwap) {
            if (Main.dedServ || Main.gameMenu) {
                return;
            }
            Player player = Main.LocalPlayer;
            if (player == null || !player.active) {
                return;
            }
            FrostveilPlayer frost = player.GetModPlayer<FrostveilPlayer>();
            float frostK = MathHelper.Clamp(
                (frost.Exposure - FrostveilPlayer.FrostStage) / (1f - FrostveilPlayer.FrostStage), 0f, 1f);
            float veil = FrostveilAmbience.WhiteoutVeil;
            float flash = frost.ChillFlash;
            if (frostK < 0.01f && veil < 0.01f && flash < 0.01f) {
                return;
            }

            Texture2D white = VaultAsset.placeholder2?.Value;
            Texture2D fog = CWRAsset.Fog?.Value;
            Texture2D star = CWRAsset.StarTexture_White?.Value;
            if (white == null || fog == null || star == null) {
                return;
            }

            int vpW = Main.screenWidth;
            int vpH = Main.screenHeight;
            float time = Main.GlobalTimeWrappedHourly;

            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone);

            //风雪墙雪幕：整幕白化 + 横掠的大雾团 + 镜前斜扫雪丝，视野被雪吃掉
            if (veil > 0.01f) {
                spriteBatch.Draw(white, new Rectangle(0, 0, vpW, vpH),
                    new Color(238, 246, 252) * (0.3f * veil));
                Vector2 fogOrigin = fog.Size() * 0.5f;
                float sweep = MathF.Sign(Main.windSpeedCurrent) >= 0f ? 1f : -1f;
                for (int i = 0; i < 7; i++) {
                    float h = Hash01(i + 90);
                    float x = ((h * (vpW + 700f) + time * sweep * (260f + h * 160f))
                        % (vpW + 700f) + vpW + 700f) % (vpW + 700f) - 350f;
                    float y = vpH * (0.1f + 0.8f * Hash01(i + 130));
                    spriteBatch.Draw(fog, new Vector2(x, y), null,
                        Color.White * (0.2f * veil), h * MathHelper.TwoPi + time * 0.05f,
                        fogOrigin, 3.4f + h * 2.2f, SpriteEffects.None, 0f);
                }
                //镜前斜扫雪丝：快速掠过的拉丝，读出"雪打在镜头上"的速度
                Texture2D spindle = Extra_98?.Value;
                if (spindle != null) {
                    Vector2 spindleOrigin = spindle.Size() * 0.5f;
                    float slant = MathHelper.PiOver2 + sweep * 0.30f;
                    for (int i = 0; i < 5; i++) {
                        float h = Hash01(i + 260);
                        float x = ((h * (vpW + 500f) + time * sweep * (900f + h * 500f))
                            % (vpW + 500f) + vpW + 500f) % (vpW + 500f) - 250f;
                        float y = vpH * (0.06f + 0.88f * Hash01(i + 310));
                        spriteBatch.Draw(spindle, new Vector2(x, y), null,
                            Color.White * (0.16f * veil), slant, spindleOrigin,
                            new Vector2(0.16f, 2.4f + h * 1.8f), SpriteEffects.None, 0f);
                    }
                }
            }

            //屏边霜花：四角雾凇 + 冰晶星簇，暴露度越深爬得越靠屏心
            if (frostK > 0.01f) {
                Vector2 fogOrigin = fog.Size() * 0.5f;
                Vector2 starOrigin = star.Size() * 0.5f;
                float reach = MathHelper.Lerp(80f, 210f, frostK);
                for (int c = 0; c < 4; c++) {
                    Vector2 corner = new(c % 2 == 0 ? 0f : vpW, c < 2 ? 0f : vpH);
                    Vector2 inward = new(c % 2 == 0 ? 1f : -1f, c < 2 ? 1f : -1f);

                    //角雾凇：一大团冷雾压角，读作镜面结霜的底
                    spriteBatch.Draw(fog, corner + inward * reach * 0.35f, null,
                        FogTint * (0.22f * frostK), c * 1.7f, fogOrigin,
                        2.6f + frostK * 1.3f, SpriteEffects.None, 0f);

                    for (int j = 0; j < StarsPerCorner; j++) {
                        int id = c * StarsPerCorner + j;
                        float h1 = Hash01(id);
                        float h2 = Hash01(id + 200);
                        float h3 = Hash01(id + 400);
                        //星簇沿角向内铺开，个体只在自己的暴露度阈值后亮起（渐浓）
                        float gate = MathHelper.Clamp((frostK - h1 * 0.85f) / 0.15f, 0f, 1f);
                        if (gate <= 0f) {
                            continue;
                        }
                        Vector2 pos = corner + inward * new Vector2(
                            h1 * reach * (0.9f + h2), h2 * reach * (0.9f + h3));
                        float twinkle = 0.72f + 0.28f * MathF.Sin(time * 1.3f + id * 2.4f);
                        float alpha = 0.5f * gate * twinkle;
                        float scale = 0.05f + 0.08f * h3;
                        spriteBatch.Draw(star, pos, null, FrostTint * alpha,
                            h2 * MathHelper.TwoPi, starOrigin, scale, SpriteEffects.None, 0f);
                        spriteBatch.Draw(star, pos, null, FrostTint * (alpha * 0.45f),
                            h2 * MathHelper.TwoPi + MathHelper.PiOver4, starOrigin,
                            scale * 0.6f, SpriteEffects.None, 0f);
                    }
                }
            }

            //满值白闪：寒意攥住心脏的一瞬——平白之上四缘迸出冰晶裂纹，随闪衰减
            if (flash > 0.01f) {
                spriteBatch.Draw(white, new Rectangle(0, 0, vpW, vpH),
                    new Color(240, 248, 255) * (0.26f * flash * flash));
                Texture2D crystal = Flashimpact2?.Value;
                if (crystal != null) {
                    Vector2 crystalOrigin = crystal.Size() * 0.5f;
                    float k = flash * flash;
                    float cScale = vpH / 512f * (0.55f + 0.25f * flash);
                    for (int c = 0; c < 4; c++) {
                        Vector2 corner = new(c % 2 == 0 ? 0f : vpW, c < 2 ? 0f : vpH);
                        spriteBatch.Draw(crystal, corner, null,
                            FrostTint * (0.45f * k), c * MathHelper.PiOver2 + Hash01(c + 700) * 0.6f,
                            crystalOrigin, cScale, SpriteEffects.None, 0f);
                    }
                }
            }

            spriteBatch.End();
        }
    }
}
