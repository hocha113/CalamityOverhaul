using CalamityOverhaul.Common;
using InnoVault.RenderHandles;
using InnoVault.TileProcessors;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Industrials.ElectricPowers.TeleportStations
{
    /// <summary>
    /// 工业玩家服务的事件光柱池:传送出发/到达柱与日晷天讯柱共用一支笔
    /// (<see cref="EffectLoader.SvcTeleport"/> TechColumn,换色板复用)。
    /// 纯客户端表现,机器侧凭已同步的事件本地 <see cref="Push"/>,零网络
    /// </summary>
    internal static class SvcColumnFX
    {
        internal struct Column
        {
            /// <summary>柱根世界坐标(锚在台面/机顶,收口契约的"源头"端)</summary>
            public Vector2 Base;
            /// <summary>柱体名义高度(px)</summary>
            public float Height;
            /// <summary>可见满宽(px);quad 宽按 0.60 折算,见 <see cref="Draw"/></summary>
            public float Width;
            public Vector3 Bright;
            public Vector3 Main;
            public Vector3 Deep;
            public int Life;
            public int Timer;
            /// <summary>0=出发(向上生长后收束吞没) 1=到达(自上而下吐出后排空)</summary>
            public float Dir;
            public float Seed;
        }

        private const int MaxColumns = 12;
        private static readonly List<Column> columns = [];

        //传送青色板(rgb 可超 1,过曝进亮芯;与沙盒作业 svcE_column_* 同源)
        internal static readonly Vector3 CyanBright = new(0.95f, 1.35f, 1.30f);
        internal static readonly Vector3 CyanMain = new(0.30f, 0.95f, 0.86f);
        internal static readonly Vector3 CyanDeep = new(0.06f, 0.34f, 0.32f);
        //晨曦金色板(日晷天讯)
        internal static readonly Vector3 GoldBright = new(1.40f, 1.20f, 0.70f);
        internal static readonly Vector3 GoldMain = new(1.05f, 0.72f, 0.28f);
        internal static readonly Vector3 GoldDeep = new(0.38f, 0.22f, 0.05f);

        internal static bool Any => columns.Count > 0;

        /// <summary>压入一根事件柱;服务端与超编静默丢弃</summary>
        internal static void Push(Vector2 basePos, float height, float width,
            Vector3 bright, Vector3 main, Vector3 deep, int life, float dir) {
            if (Main.dedServ || columns.Count >= MaxColumns) {
                return;
            }
            columns.Add(new Column {
                Base = basePos,
                Height = height,
                Width = width,
                Bright = bright,
                Main = main,
                Deep = deep,
                Life = Math.Max(life, 8),
                Timer = 0,
                Dir = dir,
                Seed = Terraria.Main.rand.NextFloat()
            });
        }

        internal static void Update() {
            for (int i = columns.Count - 1; i >= 0; i--) {
                Column c = columns[i];
                c.Timer++;
                if (c.Timer >= c.Life) {
                    columns.RemoveAt(i);
                    continue;
                }
                columns[i] = c;
            }
        }

        internal static void Clear() => columns.Clear();

        /// <summary>无活动批次的环境下调用(EndEntityDraw),自开自还</summary>
        internal static void Draw(SpriteBatch sb) {
            if (columns.Count == 0) {
                return;
            }

            Effect shader = EffectLoader.SvcTeleport?.Value;
            Texture2D canvas = VaultAsset.placeholder2?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (canvas == null) {
                return;
            }
            if (shader == null || noise == null) {
                DrawFallback(sb);
                return;
            }

            sb.Begin(SpriteSortMode.Immediate, BlendState.Additive, SamplerState.LinearWrap,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
            GraphicsDevice gd = Main.instance.GraphicsDevice;
            gd.Textures[1] = noise;
            gd.SamplerStates[1] = SamplerState.LinearWrap;
            shader.CurrentTechnique = shader.Techniques["TechColumn"];

            foreach (Column c in columns) {
                //可见满宽≈quad 宽的 0.60(shader 柱体最大半宽 0.60 半量程),折算写死成具名关系
                float quadW = c.Width / 0.60f;
                float quadH = c.Height * 1.08f; //顶部缕散留 8% 画布
                Rectangle dest = new(
                    (int)MathF.Round(c.Base.X - quadW * 0.5f - Main.screenPosition.X),
                    (int)MathF.Round(c.Base.Y - quadH - Main.screenPosition.Y),
                    (int)MathF.Ceiling(quadW), (int)MathF.Ceiling(quadH));
                //屏外剔除(余量 400px 容缩放)
                if (dest.Right < -400 || dest.X > Main.screenWidth + 400
                    || dest.Bottom < -400 || dest.Y > Main.screenHeight + 400) {
                    continue;
                }

                //共享 shader 的 uniform 是设备全局状态:每个调用点全参数重设
                shader.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly + c.Seed * 9f);
                shader.Parameters["uSeed"]?.SetValue(c.Seed);
                shader.Parameters["uColBright"]?.SetValue(c.Bright);
                shader.Parameters["uColMain"]?.SetValue(c.Main);
                shader.Parameters["uColDeep"]?.SetValue(c.Deep);
                shader.Parameters["uPower"]?.SetValue(1f);
                shader.Parameters["uPulse"]?.SetValue(0f);
                shader.Parameters["uSquish"]?.SetValue(0.34f);
                shader.Parameters["uQuadHalf"]?.SetValue(quadW * 0.5f);
                shader.Parameters["uProgress"]?.SetValue(c.Timer / (float)c.Life);
                shader.Parameters["uDir"]?.SetValue(c.Dir);
                shader.Parameters["uAspect"]?.SetValue(quadH / MathF.Max(quadW, 1f));
                shader.CurrentTechnique.Passes[0].Apply();

                sb.Draw(canvas, dest, Color.White);
            }

            sb.End();
        }

        /// <summary>着色器缺失回退:沿柱堆速度拉伸的软光段,宽度仍随生命收束,不落矩形</summary>
        private static void DrawFallback(SpriteBatch sb) {
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow == null) {
                return;
            }
            sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
            Vector2 origin = glow.Size() * 0.5f;
            foreach (Column c in columns) {
                float p = c.Timer / (float)c.Life;
                float wLife = 1f - MathHelper.SmoothStep(0f, 1f, MathHelper.Clamp((p - 0.55f) / 0.41f, 0f, 1f));
                //真 Additive 批,A 随强度走(A=0 在此批=隐形)
                Color tint = new Color(MathHelper.Clamp(c.Main.X * 0.7f, 0f, 1f),
                    MathHelper.Clamp(c.Main.Y * 0.7f, 0f, 1f), MathHelper.Clamp(c.Main.Z * 0.7f, 0f, 1f));
                int steps = 9;
                for (int i = 0; i < steps; i++) {
                    float along = i / (float)(steps - 1);
                    Vector2 pos = c.Base - new Vector2(0f, along * c.Height) - Main.screenPosition;
                    float w = c.Width / glow.Width * (1.1f - along * 0.4f) * MathF.Max(wLife, 0.05f);
                    sb.Draw(glow, pos, null, tint * (1f - along * 0.6f), 0f, origin,
                        new Vector2(w, w * 2.2f), SpriteEffects.None, 0f);
                }
            }
            sb.End();
        }
    }

    /// <summary>事件光柱绘制手柄:画在全部实体之上,传送柱要吞没玩家</summary>
    internal sealed class SvcColumnRender : RenderHandle
    {
        public override float Weight => 1.27f;

        public override void UpdateBySystem(int index) {
            if (Main.gameMenu) {
                SvcColumnFX.Clear();
                return;
            }
            SvcColumnFX.Update();
        }

        public override void EndEntityDraw(SpriteBatch spriteBatch, Main main
            , GraphicsDevice graphicsDevice, RenderTarget2D screenSwap) {
            if (Main.dedServ || !SvcColumnFX.Any) {
                return;
            }
            SvcColumnFX.Draw(spriteBatch);
        }
    }

    /// <summary>
    /// 传送站待机门户环合批:<see cref="EffectLoader.SvcTeleport"/> TechPortal。
    /// PreDrawEverything 位于 PostDrawTiles 层(物块之上、实体之下),
    /// 玩家站上台面即"站进门户里";断电站不画,门户熄灭即状态可读
    /// </summary>
    internal class TeleportPortalDraw : GlobalTileProcessor
    {
        public override bool PreDrawEverything(SpriteBatch spriteBatch) {
            if (Main.dedServ) {
                return true;
            }
            Texture2D canvas = VaultAsset.placeholder2?.Value;
            if (canvas == null) {
                return true;
            }
            Effect shader = EffectLoader.SvcTeleport?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            bool useShader = shader != null && noise != null;

            bool begun = false;
            foreach (var tp in TileProcessorLoader.TP_InWorld) {
                if (tp is not TeleportStationTP station || !station.Active) {
                    continue;
                }
                if (station.PortalPower <= 0.03f) {
                    continue;
                }
                if (!VaultUtils.IsPointOnScreen(station.PosInWorld - Main.screenPosition, station.DrawExtendMode)) {
                    continue;
                }

                if (!begun) {
                    begun = true;
                    spriteBatch.Begin(useShader ? SpriteSortMode.Immediate : SpriteSortMode.Deferred,
                        BlendState.Additive, SamplerState.LinearWrap,
                        DepthStencilState.None, RasterizerState.CullNone,
                        null, Main.GameViewMatrix.TransformationMatrix);
                    if (useShader) {
                        GraphicsDevice gd = Main.instance.GraphicsDevice;
                        gd.Textures[1] = noise;
                        gd.SamplerStates[1] = SamplerState.LinearWrap;
                        shader.CurrentTechnique = shader.Techniques["TechPortal"];
                    }
                }

                //门户环悬在拱门内部中央
                Vector2 center = new(station.CenterInWorld.X, station.PosInWorld.Y + station.Height - 40f);
                float pulse = station.Afterglow / (float)TeleportStationTP.AfterglowFrames;

                if (useShader) {
                    //环基准半径占归一化 0.68,反推 quad 半宽
                    const float RadiusPx = 27f;
                    float quadHalf = RadiusPx / 0.68f;
                    float seed = (station.Position.X * 11 + station.Position.Y * 7) * 0.137f;

                    //共享 shader 全参数重设,包括本技法不吃的柱参数
                    shader.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly + seed);
                    shader.Parameters["uSeed"]?.SetValue(seed - MathF.Floor(seed));
                    shader.Parameters["uColBright"]?.SetValue(SvcColumnFX.CyanBright);
                    shader.Parameters["uColMain"]?.SetValue(SvcColumnFX.CyanMain);
                    shader.Parameters["uColDeep"]?.SetValue(SvcColumnFX.CyanDeep);
                    shader.Parameters["uPower"]?.SetValue(station.PortalPower);
                    shader.Parameters["uPulse"]?.SetValue(pulse);
                    shader.Parameters["uSquish"]?.SetValue(0.34f);
                    shader.Parameters["uQuadHalf"]?.SetValue(quadHalf);
                    shader.Parameters["uProgress"]?.SetValue(0f);
                    shader.Parameters["uDir"]?.SetValue(0f);
                    shader.Parameters["uAspect"]?.SetValue(1f);
                    shader.CurrentTechnique.Passes[0].Apply();

                    float size = quadHalf * 2f;
                    spriteBatch.Draw(canvas, center - Main.screenPosition, null, Color.White, 0f,
                        canvas.Size() * 0.5f, new Vector2(size / canvas.Width, size / canvas.Height),
                        SpriteEffects.None, 0f);
                }
                else {
                    //回退:薄锐缘环贴图压扁成椭圆,亮度仍分档;
                    //本批是真 Additive(源因子=SourceAlpha),A 必须随强度走,禁 A=0
                    Texture2D ring = CWRUtils.GetT2DAsset(CWRConstant.Masking + "DiffusionCircle4")?.Value;
                    if (ring != null) {
                        float scale = 27f / (ring.Width * 0.5f * 0.95f);
                        Color tint = new Color(110, 235, 215) * (station.PortalPower * (0.6f + pulse * 0.5f));
                        spriteBatch.Draw(ring, center - Main.screenPosition, null, tint, 0f,
                            ring.Size() * 0.5f, new Vector2(scale, scale * 0.34f), SpriteEffects.None, 0f);
                    }
                }
            }

            if (begun) {
                spriteBatch.End();
            }
            return true;
        }
    }

    /// <summary>
    /// 传送事件观察者:钩住 <see cref="Player.Teleport"/>,任何一次真实落在
    /// 本模组传送站落点上的原版传送(交互端本地调用与远端 TeleportEntity 回放走的
    /// 是同一条路径)都在本机触发双端演出——演出接的是传送事件本身,不是右键
    /// </summary>
    internal class TeleportWatcher : ModSystem
    {
        public override void Load() {
            if (Main.dedServ) {
                return; //服务端无表现,不挂钩
            }
            On_Player.Teleport += HookTeleport;
        }

        //On_ 钩子由 tML 随模组卸载自动摘除

        private static void HookTeleport(On_Player.orig_Teleport orig, Player self,
            Vector2 newPos, int style, int extraInfo) {
            Vector2 oldCenter = self.Center;
            orig(self, newPos, style, extraInfo);

            if (Main.gameMenu || style != TeleportationStyleID.TeleportationPylon) {
                return;
            }

            //到达端:落点精确匹配某站台面(浮点同源,容差 8px 只防路径差异)
            TeleportStationTP arrive = null;
            foreach (var tp in TileProcessorLoader.TP_InWorld) {
                if (tp is not TeleportStationTP station || !station.Active) {
                    continue;
                }
                Vector2 slot = station.ArrivalPositionFor(self);
                if (MathF.Abs(newPos.X - slot.X) < 8f && MathF.Abs(newPos.Y - slot.Y) < 8f) {
                    arrive = station;
                    break;
                }
            }
            if (arrive == null) {
                return; //不是站间传送(晶塔/魔杖等),不演
            }

            //出发端:出发点 240px 内最近的另一站(UI 交互半径是 200px)
            TeleportStationTP depart = null;
            float best = 240f * 240f;
            foreach (var tp in TileProcessorLoader.TP_InWorld) {
                if (tp is not TeleportStationTP station || !station.Active || station == arrive) {
                    continue;
                }
                float distSQ = station.CenterInWorld.DistanceSQ(oldCenter);
                if (distSQ < best) {
                    best = distSQ;
                    depart = station;
                }
            }

            arrive.PlayArriveFX(self);
            depart?.PlayDepartFX(self);
        }
    }
}
