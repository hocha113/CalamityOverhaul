using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDomains;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaThralls
{
    /// <summary>
    /// 伞奴转化·幕一「化水」：死亡瞬间各端各自捕获尸体裸贴图快照
    /// （HitEffect 时真身尚在场，这是全端都能取样的唯一窗口），
    /// 随后真身消失，本层用快照经 KikasaThrallForm 播 1→0 融化——
    /// 头肩先蚀、躯体拉丝下坠、本色渐浊，熔断前沿逐帧洒污水团，
    /// 脚下污潭随融化涨开、尾段排干（水正流向重组点）。
    /// 画在 KikasaDomainRender.EndEntityDraw：湖面镜面自动倒影。
    /// </summary>
    internal static class KikasaThrallMeltFX
    {
        /// <summary>融化主段：progress 1→0</summary>
        internal const int MeltFrames = 56;

        /// <summary>残潭滞留排干段</summary>
        internal const int TailFrames = 14;

        internal const int TotalFrames = MeltFrames + TailFrames;

        private const int MaxShows = 8;

        private sealed class MeltShow
        {
            public int OwnerIndex;
            public float Seed;
            public int Timer;
            /// <summary>贴图绘制中心（已含原版底边锚差修正）</summary>
            public Vector2 Center;
            /// <summary>尸体脚底：污潭与地面裁切锚</summary>
            public Vector2 Feet;
            /// <summary>尸底贴地：潭铺地、裁切开；空中击杀走坠水</summary>
            public bool Grounded;
            public float GroundY;
            public int NpcType;
            public Rectangle Frame;
            public float Rot;
            public float Scale;
            public SpriteEffects Fx;
            /// <summary>体型系数：粒子密度与音量按它放大</summary>
            public float SplashScale;
            public bool MidBeatDone;
            public bool EndBeatDone;
        }

        private static readonly List<MeltShow> shows = [];

        //==================== 起演（每端在自己的死亡观测帧调用） ====================

        internal static void Start(NPC npc, int ownerIndex) {
            if (Main.dedServ || shows.Count >= MaxShows) {
                return;
            }
            Main.instance.LoadNPC(npc.type);
            if (TextureAssets.Npc[npc.type]?.Value == null) {
                return;
            }

            Vector2 feet = new(npc.Center.X, npc.Bottom.Y);
            bool grounded = TryProbeGround(feet, out float groundY);
            if (grounded) {
                //潭贴实心面顶而不是悬在碰撞箱底
                feet.Y = groundY;
            }

            MeltShow show = new() {
                OwnerIndex = ownerIndex,
                Seed = npc.whoAmI * 0.7391f + (npc.position.X % 97f) * 0.013f,
                Center = npc.Center + new Vector2(0f, VanillaCenterOffY(npc)),
                Feet = feet,
                Grounded = grounded,
                GroundY = grounded ? groundY : float.MaxValue,
                NpcType = npc.type,
                Frame = npc.frame,
                Rot = npc.rotation,
                Scale = npc.scale,
                Fx = npc.spriteDirection > 0
                    ? SpriteEffects.FlipHorizontally : SpriteEffects.None,
                SplashScale = MathHelper.Clamp(
                    MathF.Sqrt(npc.width * (float)npc.height) / 30f, 0.9f, 2.4f),
            };
            shows.Add(show);

            if (IsViewedOwner(ownerIndex)) {
                //起拍：湿闷的一声——雨认下了这具尸体
                SoundEngine.PlaySound(SoundID.SplashWeak with {
                    Volume = 0.5f * show.SplashScale,
                    Pitch = -0.8f,
                    MaxInstances = 3,
                }, show.Center);
                SoundEngine.PlaySound(SoundID.Drip with {
                    Volume = 0.42f,
                    Pitch = -0.55f,
                    MaxInstances = 3,
                }, show.Center);
            }
        }

        //==================== 推进 ====================

        internal static void Update() {
            for (int i = shows.Count - 1; i >= 0; i--) {
                MeltShow show = shows[i];
                show.Timer++;
                if (show.Timer > TotalFrames) {
                    shows.RemoveAt(i);
                    continue;
                }
                AdvanceShow(show);
            }
        }

        /// <summary>融化进度 1→0：pow 曲线先慢后快——结构先撑住，随后加速塌落</summary>
        private static float MeltProgress(MeltShow show)
            => 1f - MathF.Pow(MathHelper.Clamp(show.Timer / (float)MeltFrames, 0f, 1f), 1.35f);

        private static void AdvanceShow(MeltShow show) {
            int t = show.Timer;
            float progress = MeltProgress(show);
            float frameH = show.Frame.Height * show.Scale;
            float frameW = show.Frame.Width * show.Scale;
            Vector2 top = show.Center - new Vector2(0f, frameH * 0.5f);

            //熔断前沿洒污水：前沿自顶向下推进，密度随体型
            int cadence = show.SplashScale > 1.6f ? 1 : 2;
            if (t < MeltFrames && t % cadence == 0) {
                float frontY = frameH * MathHelper.Clamp(1f - progress, 0f, 1f);
                Vector2 from = new(
                    show.Center.X + Main.rand.NextFloat(-0.5f, 0.5f) * frameW * 0.8f,
                    MathHelper.Clamp(top.Y + frontY + Main.rand.NextFloat(-6f, 14f),
                        top.Y, top.Y + frameH - 2f));
                PRTLoader.NewParticle<PRT_SewageGlob>(from,
                    new Vector2(Main.rand.NextFloat(-1.4f, 1.4f), Main.rand.NextFloat(0.4f, 1.8f)),
                    Color.Lerp(KikasaThrall.SewageDeep, KikasaThrall.SewageDark, Main.rand.NextFloat())
                        * Main.rand.NextFloat(0.6f, 0.9f),
                    Main.rand.NextFloat(0.5f, 0.9f) * show.SplashScale)
                    ?.Configure(Main.rand.Next(16, 28));
            }

            //湿雾偶发
            if (t < MeltFrames && Main.rand.NextBool(9)) {
                PRTLoader.NewParticle<PRT_GhostRainMist>(
                    show.Feet + new Vector2(Main.rand.NextFloat(-24f, 24f) * show.SplashScale, -6f),
                    new Vector2(Main.rand.NextFloat(-0.2f, 0.2f), -Main.rand.NextFloat(0.05f, 0.2f)),
                    KikasaThrall.SewageDark * Main.rand.NextFloat(0.5f, 0.8f),
                    Main.rand.NextFloat(0.5f, 0.9f))
                    ?.Configure(Main.rand.Next(50, 90));
            }

            //中段涌拍：整具身体正过熔断腰线
            if (!show.MidBeatDone && t >= (int)(MeltFrames * 0.55f)) {
                show.MidBeatDone = true;
                if (IsViewedOwner(show.OwnerIndex)) {
                    SoundEngine.PlaySound(SoundID.SplashWeak with {
                        Volume = 0.36f * show.SplashScale,
                        Pitch = -0.5f,
                        MaxInstances = 3,
                    }, show.Feet);
                }
            }

            //收拍：残躯塌尽只剩污潭
            if (!show.EndBeatDone && t >= MeltFrames) {
                show.EndBeatDone = true;
                if (IsViewedOwner(show.OwnerIndex)) {
                    SoundEngine.PlaySound(SoundID.Drip with {
                        Volume = 0.4f,
                        Pitch = -0.75f,
                        MaxInstances = 3,
                    }, show.Feet);
                }
            }
        }

        //==================== 绘制（KikasaDomainRender.EndEntityDraw 调用） ====================

        internal static void Draw(SpriteBatch sb) {
            if (shows.Count == 0) {
                return;
            }
            KikasaDomainPlayer viewed = KikasaDomain.Viewed;
            if (viewed == null) {
                return;
            }
            int viewedOwner = viewed.Player.whoAmI;

            bool any = false;
            foreach (MeltShow show in shows) {
                if (show.OwnerIndex == viewedOwner) {
                    any = true;
                    break;
                }
            }
            if (!any) {
                return;
            }

            Effect form = EffectLoader.KikasaThrallForm?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            GraphicsDevice device = Main.instance.GraphicsDevice;
            bool shaderOk = form != null && noise != null && !noise.IsDisposed;

            Texture previousTexture1 = device.Textures[1];
            SamplerState previousSampler1 = device.SamplerStates[1];

            //先普通批画全部污潭——Immediate 批里 Apply 过的像素着色器是粘滞状态，
            //潭若混在身体之间画会被上一份身体的着色器污染
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
                DepthStencilState.None, RasterizerState.CullNone, null,
                Main.GameViewMatrix.TransformationMatrix);
            foreach (MeltShow show in shows) {
                if (show.OwnerIndex != viewedOwner) {
                    continue;
                }
                DrawShowPuddle(sb, show);
            }
            sb.End();

            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.PointClamp,
                DepthStencilState.None, RasterizerState.CullNone, null,
                Main.GameViewMatrix.TransformationMatrix);
            if (shaderOk) {
                device.Textures[1] = noise;
                device.SamplerStates[1] = SamplerState.LinearWrap;
            }
            foreach (MeltShow show in shows) {
                if (show.OwnerIndex != viewedOwner) {
                    continue;
                }
                DrawShowBody(sb, form, shaderOk, show);
            }
            sb.End();

            device.Textures[1] = previousTexture1;
            device.SamplerStates[1] = previousSampler1;
        }

        /// <summary>污潭：融化期涨开、尾段排干（水去了重组点）</summary>
        private static void DrawShowPuddle(SpriteBatch sb, MeltShow show) {
            if (!show.Grounded) {
                return;
            }
            float meltRatio = MathHelper.Clamp(show.Timer / (float)MeltFrames, 0f, 1f);
            float envelope = show.Timer <= MeltFrames
                ? MathF.Sin(MathHelper.Clamp(meltRatio * 1.2f, 0f, 1f) * MathHelper.PiOver2)
                : 1f - MathHelper.Clamp((show.Timer - MeltFrames) / (float)TailFrames, 0f, 1f);
            KikasaThrallRenderer.DrawPuddle(sb, show.Feet, envelope,
                show.SplashScale, show.Seed);
        }

        private static void DrawShowBody(SpriteBatch sb, Effect form, bool shaderOk, MeltShow show) {
            float progress = MeltProgress(show);
            if (progress <= 0.01f) {
                return;
            }

            Main.instance.LoadNPC(show.NpcType);
            Texture2D tex = TextureAssets.Npc[show.NpcType]?.Value;
            if (tex == null) {
                return;
            }

            //夜雨里保轮廓：环境光染向湿墨灰白
            Color light = Lighting.GetColor((show.Feet / 16f).ToPoint());
            light = Color.Lerp(light, KikasaThrall.PaleSheen, 0.30f);

            //液体蠕动微转角，随融化加深
            float wobble = show.Rot + MathF.Sin(Main.GlobalTimeWrappedHourly * 5.3f + show.Seed * 9f)
                * 0.05f * (1f - progress);

            if (shaderOk) {
                KikasaThrallRenderer.SetFormParams(form, tex, show.Frame, progress,
                    show.Scale, wobble, show.Center.Y, show.GroundY, show.Seed);
                form.CurrentTechnique.Passes[0].Apply();
                sb.Draw(tex, show.Center - Main.screenPosition, show.Frame, light,
                    wobble, show.Frame.Size() * 0.5f, show.Scale, show.Fx, 0f);
            }
            else {
                //无着色器回退：浊化淡出
                Color tint = Color.Lerp(light, KikasaThrall.SewageDeep, 1f - progress) * progress;
                sb.Draw(tex, show.Center - Main.screenPosition, show.Frame, tint,
                    show.Rot, show.Frame.Size() * 0.5f, show.Scale, show.Fx, 0f);
            }
        }

        internal static void Clear() => shows.Clear();

        //==================== 小件 ====================

        /// <summary>原版把贴图底边锚在碰撞箱底+4px，中心锚定绘制需补这几像素（同沉溺鬼影）</summary>
        private static float VanillaCenterOffY(NPC npc)
            => npc.Bottom.Y - npc.frame.Height * npc.scale * 0.5f + 4f + npc.gfxOffY
                - npc.Center.Y;

        /// <summary>脚下 3 格内探实心面：贴地则潭铺地、身体带地面裁切</summary>
        private static bool TryProbeGround(Vector2 feet, out float groundY) {
            int tileX = (int)(feet.X / 16f);
            int tileY = (int)(feet.Y / 16f);
            for (int i = 0; i < 4; i++) {
                int y = tileY + i;
                if (!WorldGen.InWorld(tileX, y, 40)) {
                    break;
                }
                Tile tile = Framing.GetTileSafely(tileX, y);
                if (tile.HasTile && Main.tileSolid[tile.TileType]
                    && !Main.tileSolidTop[tile.TileType]) {
                    groundY = y * 16f;
                    return true;
                }
            }
            groundY = 0f;
            return false;
        }

        private static bool IsViewedOwner(int ownerIndex) {
            KikasaDomainPlayer viewed = KikasaDomain.Viewed;
            return viewed != null && viewed.Player.whoAmI == ownerIndex;
        }
    }
}
