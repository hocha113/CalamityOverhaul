using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMoonLord.Core;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMoonLord.Projectiles
{
    /// <summary>
    /// 星图预兆：天空中逐笔连出的星座，为星陨标出弹道。纯演出无判定。
    /// ai[0]=种子，ai[1]=节点数，ai[2]=持续帧。节点/弹道数学静态共享，权威端用同种子生成彗星
    /// </summary>
    internal class MLordConstellationProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder2;

        internal const int RevealFrames = 44;
        internal const int FadeFrames = 20;

        private ref float Timer => ref Projectile.localAI[0];
        private int Seed => (int)Projectile.ai[0];
        private int NodeCount => Math.Clamp((int)Projectile.ai[1], 2, 9);
        private int HoldFrames => (int)Projectile.ai[2];

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 3600;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 8;
            Projectile.hostile = false;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 600;
        }

        #region 确定性星图数学（状态侧生成彗星用同一套）

        /// <summary>[0,1) 确定性散列</summary>
        internal static float Hash01(int seed, int salt) {
            float v = (float)Math.Sin(seed * 12.9898f + salt * 78.233f) * 43758.5453f;
            return v - (float)Math.Floor(v);
        }

        /// <summary>第 i 个星座节点相对锚点的偏移</summary>
        internal static Vector2 GetNodeOffset(int seed, int index, int nodeCount) {
            float spacing = 300f;
            float x = (index - (nodeCount - 1) * 0.5f) * spacing + (Hash01(seed, index * 3 + 1) - 0.5f) * 150f;
            float y = (Hash01(seed, index * 3 + 2) - 0.5f) * 190f;
            return new Vector2(x, y);
        }

        /// <summary>第 i 条弹道的初速（向下偏斜，带横向分量）</summary>
        internal static Vector2 GetLaneVelocity(int seed, int index) {
            float sway = (Hash01(seed, index * 7 + 3) - 0.5f) * 6.4f;
            float speed = 8.5f + Hash01(seed, index * 7 + 4) * 3f;
            return new Vector2(sway, speed);
        }

        /// <summary>第 i 条弹道的弯折加速度</summary>
        internal static float GetLaneCurve(int seed, int index) {
            return (Hash01(seed, index * 7 + 5) - 0.5f) * 0.14f;
        }

        #endregion

        public override void AI() {
            Timer++;
            Projectile.velocity = Vector2.Zero;
            if (Timer >= HoldFrames + FadeFrames) {
                Projectile.Kill();
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            Texture2D star = CWRAsset.StarTexture_White?.Value;
            if (star == null) {
                return false;
            }

            int nodeCount = NodeCount;
            float reveal = MathHelper.Clamp(Timer / RevealFrames, 0f, 1f);
            float fade = MathHelper.Clamp((HoldFrames + FadeFrames - Timer) / FadeFrames, 0f, 1f);
            float alpha = Math.Min(reveal * 1.6f, 1f) * fade;
            //可见段数（逐笔描画）
            float drawnSegments = reveal * (nodeCount - 1);

            for (int i = 0; i < nodeCount; i++) {
                Vector2 node = Projectile.Center + GetNodeOffset(Seed, i, nodeCount);
                Vector2 nodeScreen = node - Main.screenPosition;
                float twinkle = 0.7f + 0.3f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 7f + i * 1.9f);

                //节点星
                float nodeReveal = MathHelper.Clamp(drawnSegments - (i - 1), 0f, 1f);
                if (i == 0) {
                    nodeReveal = MathHelper.Clamp(reveal * nodeCount, 0f, 1f);
                }
                Main.EntitySpriteDraw(star, nodeScreen, null,
                    MLordDirector.MoonWhite with { A = 0 } * (0.85f * alpha * nodeReveal * twinkle),
                    Main.GlobalTimeWrappedHourly * 0.8f + i, star.Size() / 2f, 0.16f, SpriteEffects.None, 0);

                //连线
                if (i < nodeCount - 1) {
                    float segT = MathHelper.Clamp(drawnSegments - i, 0f, 1f);
                    if (segT <= 0f) {
                        continue;
                    }
                    Vector2 next = Projectile.Center + GetNodeOffset(Seed, i + 1, nodeCount);
                    Vector2 dir = next - node;
                    float len = dir.Length() * segT;
                    float rot = dir.ToRotation();
                    Main.EntitySpriteDraw(pixel, nodeScreen, null,
                        MLordDirector.Phantasmal with { A = 0 } * (0.5f * alpha),
                        rot, new Vector2(0f, pixel.Height * 0.5f),
                        new Vector2(len / pixel.Width, 1.6f / pixel.Height), SpriteEffects.None, 0);
                }

                //弹道预示：节点向下的虚线短标
                float laneAlpha = alpha * nodeReveal * 0.45f;
                if (laneAlpha > 0.02f) {
                    Vector2 laneDir = GetLaneVelocity(Seed, i).SafeNormalize(Vector2.UnitY);
                    for (int d = 1; d <= 3; d++) {
                        Vector2 dashPos = nodeScreen + laneDir * (d * 46f);
                        Main.EntitySpriteDraw(pixel, dashPos, null,
                            MLordDirector.DeepViolet with { A = 0 } * (laneAlpha * (1f - d * 0.22f)),
                            laneDir.ToRotation(), new Vector2(0f, pixel.Height * 0.5f),
                            new Vector2(22f / pixel.Width, 2.2f / pixel.Height), SpriteEffects.None, 0);
                    }
                }
            }
            return false;
        }
    }
}
