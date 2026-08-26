using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Scenarios.Dungeonworld.Fog;
using CalamityOverhaul.Content.Scenarios.Dungeonworld.NPCs;
using InnoVault.PRT;
using InnoVault.RenderHandles;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Dungeonworld.Gen.BossRooms
{
    /// <summary>
    /// 泄洪堂房内氛围复合（B2 频段 1.650–1.659，取 1.651；FloodGalleryAmbience.fx 消费端）。
    /// 三层：水上湿气+拱带漏光（TechGalleryHaze）/ 水下焦散+水面亮线+深水浊暗
    /// （TechGalleryCaustics）/ 格栅闲时气泡 PRT_FloodBubble + 立管滴水音。
    /// 纯客户端表现层：房间定位靠扫场上携带 roomOrigin 的蛰伏体/不溺者（SendExtraAI 已过线，
    /// 看守注册表是权威端私有，客户端不读）；水面行直接采本地 tile 快照，
    /// 永远与玩家看到的水一致，不依赖任何额外同步。
    /// 与全局雾系统不打架：presence 期间按帧续订 FogSuppression.RequestRect（消费者模式，
    /// 消失自动过期），房内让自家焦散接管氛围，房外全局雾原样。
    /// 与 A2 的 UndrownedWaterRender(1.621) 分工：那边管 Boss 战斗事件演出（幕/柱/泄洪流），
    /// 这边管房间本身的常驻空气与水质，互不重画。着色器缺编时静默不画（纯氛围层，无判定承诺）。
    /// </summary>
    internal sealed class FloodGalleryRender : RenderHandle
    {
        /// <summary>与不溺者内容树同门禁：整树未验收时渲染层也不注册</summary>
        public override bool CanLoad() => UndrownedGate.Enabled;

        public override float Weight => 1.651f;

        //==================== 氛围状态（纯本地）====================

        /// <summary>当前锚定的房间原点（无房=null，淡出后清）</summary>
        private static Point? anchor;
        private static float presence;
        private static float agitate = 1f;
        private static float surfaceWorldY;
        private static float lastSurfaceY;
        /// <summary>涨水扰动余拍：水面上移时点燃，焦散跟着水位爬升一起沸</summary>
        private static int riseBoost;
        private static int bubbleTimer;
        private static int dripTimer = 90;

        private static readonly Vector3 TintShallow = new(0.42f, 0.58f, 0.40f);
        private static readonly Vector3 TintDeep = new(0.05f, 0.12f, 0.09f);

        //==================== 逻辑更新 ====================

        public override void UpdateBySystem(int index) {
            if (Main.dedServ || Main.gameMenu) {
                //回菜单/换世界即清态，防旧世界坐标残影带进新世界
                anchor = null;
                presence = 0f;
                riseBoost = 0;
                return;
            }
            if (Main.gamePaused) {
                return;
            }

            //房间定位：场上最近的携房 NPC（蛰伏体或不溺者）
            Point? found = FindRoomOrigin(out bool bossUp);
            if (found.HasValue) {
                anchor = found;
            }

            if (anchor is not Point origin) {
                presence = 0f;
                return;
            }

            //presence：玩家进房渐显（外扩 200px 羽化），离场/清剿（NPC 消失）渐隐
            Rectangle roomPx = new(origin.X * 16, origin.Y * 16,
                FloodGalleryRoom.Width * 16, FloodGalleryRoom.Height * 16);
            Rectangle nearPx = roomPx;
            nearPx.Inflate(200, 200);
            float target = found.HasValue && nearPx.Contains(Main.LocalPlayer.Center.ToPoint()) ? 1f : 0f;
            presence = MathHelper.Lerp(presence, target, 0.05f);
            if (presence < 0.01f) {
                if (!found.HasValue) {
                    anchor = null;   //淡出完才解锚，防止死亡演出瞬间氛围硬切
                }
                return;
            }

            //水面：采本地 tile 快照（含格顶部分格），涨水时点燃扰动余拍
            lastSurfaceY = surfaceWorldY;
            surfaceWorldY = SampleSurfaceWorldY(origin);
            if (lastSurfaceY - surfaceWorldY > 0.4f && lastSurfaceY > 0f) {
                riseBoost = 90;
            }
            if (riseBoost > 0) {
                riseBoost--;
            }
            float agitateTarget = (bossUp ? 1.7f : 1f) + (riseBoost > 0 ? 0.9f : 0f);
            agitate = MathHelper.Lerp(agitate, agitateTarget, 0.04f);

            //压雾：房内自家氛围接管，全局雾靠边（短 TTL 按帧续订，离场自动回雾）
            FogSuppression.RequestRect(roomPx, 12, 240f);

            UpdateBubbles(origin);
            UpdateDrips(origin);
        }

        /// <summary>扫场上携 roomOrigin 的蛰伏体/不溺者，取离本地玩家最近者</summary>
        private static Point? FindRoomOrigin(out bool bossUp) {
            bossUp = false;
            int throneType = ModContent.NPCType<UndrownedThrone>();
            int bossType = ModContent.NPCType<Undrowned>();
            Point? best = null;
            float bestDist = float.MaxValue;
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (!npc.active) {
                    continue;
                }
                Point origin;
                bool isBoss;
                if (npc.type == throneType && npc.ModNPC is UndrownedThrone throne && throne.HasRoom) {
                    origin = throne.RoomOrigin;
                    isBoss = false;
                }
                else if (npc.type == bossType && npc.ModNPC is Undrowned boss && boss.HasRoom) {
                    origin = boss.RoomOrigin;
                    isBoss = true;
                }
                else {
                    continue;
                }
                float dist = Vector2.Distance(npc.Center, Main.LocalPlayer.Center);
                if (dist < bestDist) {
                    bestDist = dist;
                    best = origin;
                    bossUp = isBoss;
                }
            }
            return best;
        }

        /// <summary>沿房内一根通底开列（rel col 25）找水面世界 Y；干房返回地板顶</summary>
        private static float SampleSurfaceWorldY(Point origin) {
            int col = origin.X + 25;
            for (int ry = FloodGalleryRoom.InteriorTop; ry < FloodGalleryRoom.FloorRel; ry++) {
                int y = origin.Y + ry;
                if (!WorldGen.InWorld(col, y, 5)) {
                    continue;
                }
                Tile t = Main.tile[col, y];
                if (t.HasTile && Main.tileSolid[t.TileType]) {
                    continue;
                }
                if (t.LiquidAmount > 0) {
                    //部分格：液体从格底往上填
                    return y * 16f + (255 - t.LiquidAmount) * (16f / 255f);
                }
            }
            return (origin.Y + FloodGalleryRoom.FloorRel) * 16f;
        }

        /// <summary>格栅闲时气泡：淹水期从栅缝匀速冒小泡，链堆处偶发一串（活水感）</summary>
        private static void UpdateBubbles(Point origin) {
            float floorY = (origin.Y + FloodGalleryRoom.FloorRel) * 16f;
            if (surfaceWorldY >= floorY - 4f || presence < 0.3f) {
                return;
            }
            if (--bubbleTimer > 0) {
                return;
            }
            bubbleTimer = Main.rand.Next(6, 14);
            //主通道：格栅横带
            Vector2 grate = FloodGalleryRoom.GrateWorldPos(origin);
            Vector2 at = new(grate.X + Main.rand.NextFloat(-60f, 60f), grate.Y - 6f);
            PRTLoader.NewParticle<PRT_FloodBubble>(at,
                new Vector2(Main.rand.NextFloat(-0.2f, 0.2f), -Main.rand.NextFloat(0.7f, 1.3f)),
                new Color(190, 225, 200), Main.rand.NextFloat(0.5f, 1.0f))
                ?.Configure(Main.rand.Next(70, 130), surfaceWorldY);
            //副通道：沉链堆低频冒泡（水下有东西在锈）
            if (Main.rand.NextBool(4)) {
                float chainX = (origin.X + Main.rand.Next(24, 62)) * 16f;
                PRTLoader.NewParticle<PRT_FloodBubble>(
                    new Vector2(chainX, floorY - 10f),
                    new Vector2(0f, -Main.rand.NextFloat(0.5f, 0.9f)),
                    new Color(170, 205, 180), Main.rand.NextFloat(0.35f, 0.6f))
                    ?.Configure(Main.rand.Next(60, 100), surfaceWorldY);
            }
        }

        /// <summary>立管滴水音：低频随机，音源钉在两根立管口（空间声像=房间在漏）</summary>
        private static void UpdateDrips(Point origin) {
            if (--dripTimer > 0) {
                return;
            }
            dripTimer = Main.rand.Next(70, 180);
            int col = Main.rand.NextBool()
                ? FloodGalleryRoom.PipeLeftCol : FloodGalleryRoom.PipeRightCol;
            Vector2 mouth = new((origin.X + col + 1f) * 16f,
                (origin.Y + FloodGalleryRoom.PipeBottomRel) * 16f);
            SoundEngine.PlaySound(SoundID.Drip with { Volume = 0.5f * presence }, mouth);
        }

        //==================== 绘制（实体层后：湿气与水光盖在人身上，读作空气）====================

        public override void EndEntityDraw(SpriteBatch spriteBatch, Main main) {
            if (Main.dedServ || Main.gameMenu || presence < 0.02f || anchor is not Point origin) {
                return;
            }
            Effect fx = EffectLoader.FloodGalleryAmbience?.Value;
            Texture2D px = VaultAsset.placeholder2?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (fx == null || px == null || noise == null || px.IsDisposed) {
                return;   //纯氛围层无判定承诺，缺编静默
            }

            Vector2 worldTL = new(origin.X * 16f, origin.Y * 16f);
            Vector2 worldSize = new(FloodGalleryRoom.Width * 16f, FloodGalleryRoom.Height * 16f);
            float floorY = (origin.Y + FloodGalleryRoom.FloorRel) * 16f;

            spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend,
                SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone,
                null, Main.GameViewMatrix.TransformationMatrix);
            GraphicsDevice device = Main.instance.GraphicsDevice;
            device.Textures[1] = noise;
            device.SamplerStates[1] = SamplerState.LinearWrap;

            //共享参数化 shader：每帧全参数重设（uniform 是设备全局状态）
            fx.Parameters["uWorldTL"]?.SetValue(worldTL);
            fx.Parameters["uWorldSize"]?.SetValue(worldSize);
            fx.Parameters["uSurfaceY"]?.SetValue(surfaceWorldY);
            fx.Parameters["uFloorY"]?.SetValue(floorY);
            fx.Parameters["uTime"]?.SetValue((float)Main.timeForVisualEffects / 60f);
            fx.Parameters["uPresence"]?.SetValue(presence);
            fx.Parameters["uAgitate"]?.SetValue(agitate);
            fx.Parameters["uTintShallow"]?.SetValue(TintShallow);
            fx.Parameters["uTintDeep"]?.SetValue(TintDeep);

            Vector2 drawPos = worldTL - Main.screenPosition;
            Vector2 quadScale = new(worldSize.X / px.Width, worldSize.Y / px.Height);

            fx.CurrentTechnique = fx.Techniques["TechGalleryHaze"];
            fx.CurrentTechnique.Passes[0].Apply();
            spriteBatch.Draw(px, drawPos, null, Color.White, 0f, Vector2.Zero,
                quadScale, SpriteEffects.None, 0f);

            //焦散只在有水时画（干房省一整层 ps）
            if (surfaceWorldY < floorY - 2f) {
                fx.CurrentTechnique = fx.Techniques["TechGalleryCaustics"];
                fx.CurrentTechnique.Passes[0].Apply();
                spriteBatch.Draw(px, drawPos, null, Color.White, 0f, Vector2.Zero,
                    quadScale, SpriteEffects.None, 0f);
            }

            spriteBatch.End();
            device.Textures[1] = null;   //槽位归还（帧内自守，Janitor 只兜跨帧）
        }
    }

    /// <summary>
    /// 泄洪堂淹水气泡：格栅缝里升起的小气泡，浮力加速 + 横向摇摆，
    /// 到水面即破（触面瞬间放大淡出）。SoftGlow 双层：暗环底 + 高光芯偏左上（球面读法）
    /// </summary>
    internal class PRT_FloodBubble : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "SoftGlow";
        public override bool CanPool => true;
        public override int InGame_World_MaxCount => 200;

        private float surfaceY;
        private float wobbleSeed;
        private bool popped;

        public PRT_FloodBubble Configure(int lifetime, float surfaceWorldY) {
            Lifetime = lifetime;
            surfaceY = surfaceWorldY;
            wobbleSeed = Main.rand.NextFloat(MathHelper.TwoPi);
            return this;
        }

        public override void Reset() {
            base.Reset();
            surfaceY = 0f;
            wobbleSeed = 0f;
            popped = false;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AlphaBlend;
            if (Lifetime <= 0) {
                Lifetime = 90;
            }
        }

        public override void AI() {
            if (popped) {
                //破泡收尾：胀一口气熄掉
                Scale *= 1.08f;
                Opacity *= 0.72f;
                Velocity *= 0.5f;
                if (Opacity < 0.05f) {
                    active = false;
                }
                return;
            }
            //浮力缓升 + 横向摇摆（大泡摆得更狠）
            Velocity.Y = MathF.Max(Velocity.Y - 0.015f, -1.8f);
            Velocity.X = MathF.Sin(Time * 0.11f + wobbleSeed) * 0.30f * Scale;
            Opacity = MathHelper.Clamp(Time / 10f, 0f, 0.8f);
            //触水面即破
            if (Position.Y <= surfaceY + 3f) {
                popped = true;
            }
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = PRTLoader.PRT_IDToTexture[ID];
            Vector2 origin = tex.Size() * 0.5f;
            Vector2 pos = Position - Main.screenPosition;
            float s = Scale * 8f / tex.Width;   //基准直径 ~8px

            //暗环底（比水色深半档，撑出圆界）
            spriteBatch.Draw(tex, pos, null, new Color(24, 48, 38) * (Opacity * 0.9f),
                0f, origin, s * 1.25f, SpriteEffects.None, 0f);
            //泡体
            spriteBatch.Draw(tex, pos, null, Color * (Opacity * 0.55f),
                0f, origin, s, SpriteEffects.None, 0f);
            //高光芯偏左上（球面反光，A=0 预乘补光）
            spriteBatch.Draw(tex, pos - new Vector2(1.5f, 1.5f) * Scale, null,
                (Color.White with { A = 0 }) * (Opacity * 0.5f),
                0f, origin, s * 0.35f, SpriteEffects.None, 0f);
            return false;
        }
    }
}
