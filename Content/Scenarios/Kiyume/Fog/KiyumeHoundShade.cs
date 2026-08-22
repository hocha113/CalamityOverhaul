using CalamityOverhaul.Common;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.Scenarios.Kiyume.Fog
{
    /// <summary>
    /// 雾墙后的犬影：远处雾里走过一条黑犬，你往那边看，它化了。<br/>
    /// 复用鬼伞的 <c>KikasaHound.fx</c> 实体态材质与原版狼帧，只是这里没有湖镜
    /// 它不是倒影，是雾里真有个东西。<br/>
    /// 由 <see cref="KiyumeFogSystem.PostDrawTiles"/> 在背景雾层<b>之前</b>调用绘制，
    /// 顺序就是"在雾墙后面"这句话的全部实现。纯客户端表现，不是 NPC，不参与任何判定
    /// </summary>
    internal static class KiyumeHoundShade
    {
        //够远才有藏身的雾，够近就该化掉
        private const float SpawnMinDist = 520f;
        private const float SpawnMaxDist = 900f;
        private const float FadeNear = 190f;
        private const float FadeFull = 400f;
        private const float HoundScale = 1.18f;
        //眼睛在帧内的原生 uv（贴图面向左），与鬼伞倒影犬同一校准位
        private static readonly Vector2 EyeAnchor = new(0.17f, 0.38f);

        private sealed class Shade
        {
            internal bool Active;
            internal Vector2 Pos;
            internal float Vx;
            internal int Life;
            internal int Frame;
            internal float FrameCounter;
            internal float Alpha;
            internal float Seed;
        }

        private static readonly Shade[] shades = [new Shade(), new Shade()];
        private static int cooldown;

        internal static void Clear() {
            foreach (Shade s in shades) {
                s.Active = false;
                s.Alpha = 0f;
                s.Life = 0;
            }
            cooldown = 0;
        }

        internal static void Update() {
            if (Main.dedServ || Main.gameMenu) {
                return;
            }
            Player player = Main.LocalPlayer;
            if (player?.active != true) {
                return;
            }
            float presence = KiyumeFogSystem.Presence;
            if (cooldown > 0) {
                cooldown--;
            }
            foreach (Shade s in shades) {
                if (s.Active) {
                    Advance(s, player, presence);
                }
                else {
                    TrySpawn(s, player, presence);
                }
            }
        }

        private static void TrySpawn(Shade s, Player player, float presence) {
            if (cooldown > 0 || presence < 0.55f) {
                return;
            }
            int side = Main.rand.NextBool() ? 1 : -1;
            float x = player.Center.X + side * Main.rand.NextFloat(SpawnMinDist, SpawnMaxDist);
            if (!TryFindGround(x, player.Center.Y - 240f, out float groundY)) {
                return;
            }
            var pos = new Vector2(x, groundY);
            //雾不够浓就没有藏身处，这时候放犬只会显得是贴上去的
            if (KiyumeFogSim.DensityAt(pos - new Vector2(0f, 24f)) < 0.42f) {
                return;
            }

            s.Active = true;
            s.Pos = pos;
            //一半朝你走一半走开：朝你走的那条会在近处化掉，走开的那条没入雾里
            s.Vx = (Main.rand.NextBool() ? -side : side) * Main.rand.NextFloat(0.9f, 1.9f);
            s.Life = Main.rand.Next(260, 520);
            s.Alpha = 0f;
            s.Seed = Main.rand.NextFloat(10f);
            s.Frame = 3;
            s.FrameCounter = 0f;
            cooldown = Main.rand.Next(300, 720);
        }

        private static void Advance(Shade s, Player player, float presence) {
            s.Pos.X += s.Vx;
            //贴地走：探不到地面就保持原高度，宁可飘一下也不要瞬移
            if (TryFindGround(s.Pos.X, s.Pos.Y - 48f, out float ground)) {
                s.Pos.Y = MathHelper.Lerp(s.Pos.Y, ground, 0.25f);
            }
            s.Life--;

            float dist = MathF.Abs(s.Pos.X - player.Center.X);
            //走近就没了，而且不会再走回来
            if (dist < FadeNear || dist > 1700f) {
                s.Life = 0;
            }

            float near = MathHelper.Clamp((dist - FadeNear) / (FadeFull - FadeNear), 0f, 1f);
            float fog = MathHelper.Clamp(
                (KiyumeFogSim.DensityAt(s.Pos - new Vector2(0f, 24f)) - 0.28f) / 0.26f, 0f, 1f);
            float target = s.Life > 0 ? near * fog * presence : 0f;
            s.Alpha = MathHelper.Lerp(s.Alpha, target, 0.07f);

            //跑动循环（原版狼帧 3-9）
            s.FrameCounter += MathF.Abs(s.Vx) * 0.5f;
            if (s.FrameCounter > 8f) {
                s.FrameCounter -= 8f;
                s.Frame++;
                if (s.Frame > 9 || s.Frame < 3) {
                    s.Frame = 3;
                }
            }

            if (s.Life <= 0 && s.Alpha < 0.02f) {
                s.Active = false;
            }
        }

        internal static void Draw(SpriteBatch sb) {
            if (Main.dedServ || Main.gameMenu) {
                return;
            }
            bool any = false;
            foreach (Shade s in shades) {
                any |= s.Active && s.Alpha > 0.02f;
            }
            if (!any) {
                return;
            }

            Main.instance.LoadNPC(NPCID.Wolf);
            Texture2D tex = TextureAssets.Npc[NPCID.Wolf].Value;
            if (tex == null) {
                return;
            }
            Effect hound = EffectLoader.KikasaHound?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            GraphicsDevice gd = Main.instance.GraphicsDevice;

            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null,
                Main.GameViewMatrix.TransformationMatrix);
            if (hound != null && noise != null) {
                gd.Textures[1] = noise;
                gd.SamplerStates[1] = SamplerState.LinearWrap;
            }
            foreach (Shade s in shades) {
                if (s.Active && s.Alpha > 0.02f) {
                    DrawOne(sb, s, tex, hound);
                }
            }
            sb.End();
            gd.Textures[1] = null;
        }

        private static void DrawOne(SpriteBatch sb, Shade s, Texture2D tex, Effect hound) {
            int frameCount = Main.npcFrameCount[NPCID.Wolf];
            int frameH = tex.Height / frameCount;
            //源矩形上下各内缩 1px + shader 帧界钳制，双通道防帧表渗色
            var source = new Rectangle(0, s.Frame * frameH + 1, tex.Width, frameH - 2);
            float width = tex.Width * HoundScale;
            float height = source.Height * HoundScale;
            var topLeft = new Vector2(s.Pos.X - width * 0.5f, s.Pos.Y - height);
            bool faceRight = s.Vx > 0f;

            if (hound == null) {
                //着色器缺失：近黑剪影回退
                SpriteEffects fb = faceRight ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
                sb.Draw(tex, topLeft - Main.screenPosition, source,
                    new Color(10, 5, 8) * (s.Alpha * 0.9f), 0f, Vector2.Zero, HoundScale, fb, 0f);
                return;
            }

            //淡出走 uDissolve 而不是纯降 alpha：它该是化进雾里，不是被调低了透明度
            float dissolve = MathHelper.Clamp(1f - s.Alpha, 0f, 1f) * 0.75f;
            float eye = 0.20f + 0.12f * MathF.Sin(Main.GlobalTimeWrappedHourly * 1.7f + s.Seed * 4f);

            hound.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            hound.Parameters["uSeed"]?.SetValue(s.Seed);
            hound.Parameters["uUvRect"]?.SetValue(new Vector4(
                0f, source.Y / (float)tex.Height, 1f, source.Height / (float)tex.Height));
            hound.Parameters["uTexel"]?.SetValue(new Vector2(1f / tex.Width, 1f / tex.Height));
            hound.Parameters["uAspect"]?.SetValue(tex.Width / (float)source.Height);
            hound.Parameters["uFlipH"]?.SetValue(faceRight ? 1f : 0f);
            hound.Parameters["uFlipV"]?.SetValue(0f);
            hound.Parameters["uMode"]?.SetValue(1f);
            hound.Parameters["uSeamGate"]?.SetValue(0f);
            hound.Parameters["uWobble"]?.SetValue(0.010f);
            hound.Parameters["uEyeGlow"]?.SetValue(eye);
            hound.Parameters["uEyeAnchor"]?.SetValue(EyeAnchor);
            hound.Parameters["uDissolve"]?.SetValue(dissolve);
            hound.Parameters["uEdgeTint"]?.SetValue(new Color(112, 26, 26).ToVector3());
            hound.CurrentTechnique = hound.Techniques["TechHound"];
            hound.CurrentTechnique.Passes[0].Apply();

            sb.Draw(tex, topLeft - Main.screenPosition, source,
                Color.White * MathHelper.Clamp(s.Alpha * 1.25f, 0f, 1f),
                0f, Vector2.Zero, HoundScale, SpriteEffects.None, 0f);
        }

        //从起始高度向下探地表
        private static bool TryFindGround(float x, float fromY, out float groundY) {
            int tileX = (int)(x / 16f);
            int tileY = (int)(fromY / 16f);
            for (int i = 0; i < 60; i++) {
                int y = tileY + i;
                if (!WorldGen.InWorld(tileX, y, 20)) {
                    break;
                }
                Tile tile = Framing.GetTileSafely(tileX, y);
                if (tile.HasTile && Main.tileSolid[tile.TileType] && !Main.tileSolidTop[tile.TileType]) {
                    groundY = y * 16f;
                    return true;
                }
            }
            groundY = 0f;
            return false;
        }
    }
}
