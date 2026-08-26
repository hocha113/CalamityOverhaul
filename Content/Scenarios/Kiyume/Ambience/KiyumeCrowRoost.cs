using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Scenarios.Kiyume.Fog;
using CalamityOverhaul.Content.Scenarios.Kiyume.Gen;
using CalamityOverhaul.Content.Scenarios.Kiyume.NPCs;
using CalamityOverhaul.Content.Scenarios.Kiyume.Stealth;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Kiyume.Ambience
{
    /// <summary>
    /// 檐上鸦阵（R2-C · P4 点子 9 栖息层，飞行层=<see cref="KiyumeCrowFlight"/>）：
    /// 村落屋脊蹲着几撮鸦，跑动或枪声会惊起它们炸窝——本身无害，但炸窝向噪声场
    /// <see cref="KiyumeStealthSense.ReportNoise"/> 上报一次大噪声（潜行压力放大器：
    /// 吵醒鸦阵的人，恶犬也听得见）。<br/>
    /// 混合层架构：栖息鸦本体是纯客户端视觉（镜像 KiyumeCrowFlight 槽位模式，非 NPC 无判定）；
    /// 「惊起」在权威端（服务器/单人）20t 巡检判定，经单字节版本号让同进程表现层接演出
    /// （进程内握手，零网络包）；炸窝散拍后移交 <see cref="KiyumeCrowFlight.StartleFrom"/> 接飞。<br/>
    /// 联机口径（如实降级）：服务器从 DoorwayPoints 建表、判定全员扰动并上报噪声（玩法量全效）；
    /// 客户端结构表恒空，改为本地自扫瓦顶做纯视觉栖鸦、只对本端玩家的扰动本地炸窝——
    /// 「A 惊起的鸦 B 看不见」按独自遇鬼口径接受，不新增网络包。<br/>
    /// 本类 static 全部为世界级会话状态（栖息点表/冷却/版本号，非 per-player，
    /// 镜像噪声环形缓冲先例）或本地演出进度，netcode 静态禁令不适用。<br/>
    /// 与守田人旱田静默区（裁决 16：ScarecrowPlot 外扩 500px）天然无交集：
    /// 旱田在滩涂 [516,558] 列，本层只在村落带（列 ≥620）采样，间隔 ≥60 列 ≈ 990px。
    /// 绘制由 <see cref="KiyumeFogSystem.PostDrawTiles"/> 在飞行鸦群之后调用（同层「雾墙后」）。
    /// </summary>
    internal class KiyumeCrowRoost : ModSystem
    {
        //蹲踞鸦：帧 0（对源 NPC.FindFrame case 301：velocity 归零即 frame.Y=0），炸窝拍转扑翼帧 1..4
        private sealed class PerchCrow
        {
            internal bool Active;
            internal Vector2 Pos;
            internal Vector2 Home;      //窝位（炸窝飞散后冷却尽了从这里重新显形）
            internal Vector2 Vel;       //炸窝上抛用，栖息时归零
            internal bool FaceLeft;
            internal float Scale;
            internal float Seed;
            internal float SwayPhase;   //栖息微摆相位
            internal int Frame;
            internal float FrameCounter;
            internal float Alpha;
        }

        private sealed class Roost
        {
            internal Point RidgeTile;   //屋脊瓦 tile（鸦蹲其顶面）
            internal int Cooldown;      //惊起冷却（判定侧递减，冷却期窝空着）
            internal int BurstTicks;    //炸窝演出剩余帧（表现侧）
            internal readonly PerchCrow[] Perch = [new(), new(), new(), new()];
        }

        //栖息点表：权威端（服务器/单人）从门洞表建；联机客户端由本地自扫填充（纯视觉）
        private static readonly List<Roost> roosts = [];
        private static bool authorityBuilt;
        private static int patrolTimer;
        //克制律：全村同刻至多一处炸窝演出，占闸期间其余点按兵不动（不烧冷却）
        private static int showHold;
        //权威→表现的惊起握手：判定侧拨号，表现层对沿接演出（进程内，单人两层同进程故生效；
        //联机客户端由本地判定路径拨同一枚号，服务器的号不过线）
        private static byte startleVersion;
        private static byte visualVersion;
        private static Vector2 startlePos;
        private static int clientScanTimer;

        internal static void Clear() {
            roosts.Clear();
            authorityBuilt = false;
            patrolTimer = 0;
            showHold = 0;
            startleVersion = 0;
            visualVersion = 0;
            startlePos = Vector2.Zero;
            clientScanTimer = 0;
        }

        public override void OnWorldLoad() => Clear();
        public override void OnWorldUnload() => Clear();
        public override void ClearWorld() => Clear();
        public override void Unload() => Clear();

        //==================== 驱动 ====================

        public override void PostUpdateEverything() {
            if (Main.gameMenu || !KiyumeWorld.Active) {
                return;
            }
            if (VaultUtils.isClient) {
                //联机客户端：权威表不可得（DoorwayPoints 恒空），走降级路径
                ClientScanTick();
                ClientLocalPatrol();
            }
            else {
                AuthorityTick();
            }
            if (!Main.dedServ) {
                VisualTick();
            }
        }

        //==================== 权威侧（服务器/单人）====================

        private static void AuthorityTick() {
            if (!authorityBuilt) {
                //惰性首帧建表：生成先于 OnWorldLoad 完成，此刻门洞表已就绪
                authorityBuilt = true;
                BuildFromDoorways();
            }
            if (++patrolTimer < KiyumeScore.RoostCheckTicks) {
                return;
            }
            patrolTimer = 0;
            if (showHold > 0) {
                showHold -= KiyumeScore.RoostCheckTicks;
            }
            foreach (Roost r in roosts) {
                if (r.Cooldown > 0) {
                    r.Cooldown -= KiyumeScore.RoostCheckTicks;
                    continue;
                }
                if (showHold > 0) {
                    continue;
                }
                foreach (Player player in Main.ActivePlayers) {
                    if (player.dead || player.ghost) {
                        continue;
                    }
                    if (Disturbs(player, r.RidgeTile)) {
                        Startle(r);
                        break;
                    }
                }
            }
        }

        //权威建表：门洞表 x 有序（建村自西向东流式登记），逐门上探瓦顶爬到脊，
        //按最小点距收候选，超帽匀取——8 个点摊满全村，不挤西半村
        private static void BuildFromDoorways() {
            List<Point> picks = [];
            int lastX = int.MinValue;
            foreach (Point door in KiyumeStructures.DoorwayPoints) {
                if (door.X - lastX < KiyumeScore.RoostSpacingCols) {
                    continue;
                }
                if (TryFindRidge(door.X, door.Y, out Point ridge)) {
                    picks.Add(ridge);
                    lastX = door.X;
                }
            }
            int count = Math.Min(picks.Count, KiyumeScore.RoostPointMax);
            for (int i = 0; i < count; i++) {
                AddRoost(picks[(int)((i + 0.5f) * picks.Count / count)]);
            }
        }

        //扰动判定：奔跑近距 / 开火中距（鸦对枪声更远敏）。
        //FirePulse 寿命 20t 与巡检节拍同长，单次开火恰好被采到一次
        private static bool Disturbs(Player player, Point ridge) {
            float dist = Vector2.Distance(player.Center, RidgeWorldPos(ridge));
            if (dist < KiyumeScore.RoostRunDistPx
                && player.velocity.Length() >= KiyumeHoundMetrics.RunSpeedGate) {
                return true;
            }
            return dist < KiyumeScore.RoostFireDistPx
                && player.GetModPlayer<KiyumeStealthPlayer>().FirePulse > 0.01f;
        }

        //惊起（判定侧）：盖点冷却、占演出闸、上报大噪声、拨版本号。
        //ReportNoise 在联机客户端为无害空转，本地降级路径直接复用本函数
        private static void Startle(Roost r) {
            r.Cooldown = KiyumeScore.RoostCooldownTicks;
            showHold = KiyumeScore.RoostShowHoldTicks;
            startlePos = RidgeWorldPos(r.RidgeTile);
            KiyumeStealthSense.ReportNoise(startlePos, KiyumeScore.RoostNoiseAmount);
            unchecked { startleVersion++; }
        }

        //==================== 联机客户端降级路径 ====================

        //本地自扫：60t 节拍在玩家近旁窗口找瓦顶凑纯视觉栖鸦点（与服务器判定表无对应，如实接受）
        private static void ClientScanTick() {
            if (--clientScanTimer > 0) {
                return;
            }
            clientScanTimer = KiyumeScore.RoostClientScanTicks;
            if (roosts.Count >= KiyumeScore.RoostPointMax) {
                return;
            }
            Player player = Main.LocalPlayer;
            if (player?.active != true) {
                return;
            }
            int pcol = (int)(player.Center.X / 16f);
            if (pcol < KiyumeMetrics.VillageLeft || pcol >= KiyumeMetrics.GroveLeft) {
                return;
            }
            int left = Math.Max(pcol - 64, KiyumeMetrics.VillageLeft + 2);
            int right = Math.Min(pcol + 64, KiyumeMetrics.GroveLeft - 2);
            //从玩家脚下略低处向上探（村内地表高差有限，探程见 RoostProbeUpRows）
            int fromRow = (int)(player.Center.Y / 16f) + 4;
            for (int col = left; col <= right; col += 4) {
                if (TooCloseToExisting(col)) {
                    continue;
                }
                if (TryFindRidge(col, fromRow, out Point ridge) && !TooCloseToExisting(ridge.X)) {
                    AddRoost(ridge);
                    if (roosts.Count >= KiyumeScore.RoostPointMax) {
                        return;
                    }
                }
            }
        }

        //本地巡检：只看本端玩家（他端扰动由服务器记噪声，视觉各端自理——独自遇鬼口径）
        private static void ClientLocalPatrol() {
            if (++patrolTimer < KiyumeScore.RoostCheckTicks) {
                return;
            }
            patrolTimer = 0;
            if (showHold > 0) {
                showHold -= KiyumeScore.RoostCheckTicks;
            }
            Player player = Main.LocalPlayer;
            bool playerOk = player?.active == true && !player.dead && !player.ghost;
            foreach (Roost r in roosts) {
                if (r.Cooldown > 0) {
                    r.Cooldown -= KiyumeScore.RoostCheckTicks;
                    continue;
                }
                if (showHold > 0 || !playerOk) {
                    continue;
                }
                if (Disturbs(player, r.RidgeTile)) {
                    Startle(r);
                    break;
                }
            }
        }

        private static bool TooCloseToExisting(int col) {
            foreach (Roost r in roosts) {
                if (Math.Abs(col - r.RidgeTile.X) < KiyumeScore.RoostSpacingCols) {
                    return true;
                }
            }
            return false;
        }

        //==================== 屋脊探测（两路共用）====================

        //从起始列上探瓦顶，再向高处爬坡到局部脊头；脊上要 2 格净空给鸦身
        private static bool TryFindRidge(int col, int fromRow, out Point ridge) {
            ridge = default;
            if (!TryRoofTopAt(col, fromRow, out int top)) {
                return false;
            }
            //爬坡：邻列瓦顶更高（行号更小）就挪过去，坡脊单调，12 步封顶
            for (int step = 0; step < 12; step++) {
                bool moved = false;
                for (int dir = -1; dir <= 1; dir += 2) {
                    if (TryRoofTopAt(col + dir, fromRow, out int t2) && t2 < top) {
                        col += dir;
                        top = t2;
                        moved = true;
                        break;
                    }
                }
                if (!moved) {
                    break;
                }
            }
            if (Framing.GetTileSafely(col, top - 1).HasTile
                || Framing.GetTileSafely(col, top - 2).HasTile) {
                return false;
            }
            ridge = new Point(col, top);
            return true;
        }

        //列内瓦顶：探程内从上往下找第一格实心，恰是瓦（与 KiyumeVillage.RoofTile 同源
        //RedDynastyShingles）才认屋顶——树冠/望楼身/别的结构先挡住即弃列
        private static bool TryRoofTopAt(int col, int fromRow, out int topRow) {
            topRow = 0;
            int start = Math.Max(fromRow - KiyumeScore.RoostProbeUpRows, 1);
            for (int y = start; y < fromRow - 1; y++) {
                Tile t = Framing.GetTileSafely(col, y);
                if (!t.HasTile) {
                    continue;
                }
                if (t.TileType != TileID.RedDynastyShingles) {
                    return false;
                }
                topRow = y;
                return true;
            }
            return false;
        }

        //落窝：2~4 只逐只在脊头两侧错落落座，各自探自己列的瓦顶，顺坡面高低排开
        private static void AddRoost(Point ridge) {
            var r = new Roost { RidgeTile = ridge };
            //只数钳在槽位帽内：调音抬 RoostCrowMax 不越界
            int count = Math.Min(
                Main.rand.Next(KiyumeScore.RoostCrowMin, KiyumeScore.RoostCrowMax + 1), r.Perch.Length);
            for (int i = 0; i < count; i++) {
                PerchCrow c = r.Perch[i];
                int col = ridge.X + (i == 0 ? 0 : Main.rand.Next(-3, 4));
                if (!TryRoofTopAt(col, ridge.Y + KiyumeScore.RoostProbeUpRows, out int top)) {
                    col = ridge.X;
                    top = ridge.Y;
                }
                c.Active = true;
                //底心锚：爪贴瓦顶，+2px 嵌进瓦缝
                c.Home = new Vector2(col * 16f + 8f + Main.rand.NextFloat(-3f, 3f), top * 16f + 2f);
                c.Pos = c.Home;
                c.Vel = Vector2.Zero;
                c.FaceLeft = Main.rand.NextBool();
                c.Scale = Main.rand.NextFloat(0.85f, 1.05f);
                c.Seed = Main.rand.NextFloat(10f);
                c.SwayPhase = Main.rand.NextFloat(MathHelper.TwoPi);
                c.Frame = 0;
                c.FrameCounter = 0f;
                c.Alpha = 0f;
            }
            roosts.Add(r);
        }

        private static Vector2 RidgeWorldPos(Point ridge) => new(ridge.X * 16f + 8f, ridge.Y * 16f);

        //==================== 表现层（客户端/单人）====================

        private static void VisualTick() {
            //版本沿：权威（单人同进程）或本地判定（联机客户端）拨一格，就接一场炸窝
            if (visualVersion != startleVersion) {
                visualVersion = startleVersion;
                BeginBurst();
            }
            float presence = KiyumeFogSystem.Presence;
            foreach (Roost r in roosts) {
                if (r.BurstTicks > 0) {
                    AdvanceBurst(r);
                }
                else {
                    AdvancePerch(r, presence);
                }
            }
        }

        //炸窝起拍：蹲鸦转扑翼上抛，先叫一声，移交飞行层从同点接飞
        //（StartleFrom 自带一声 Owl，加散拍途中的第二声 Bird 共 2~3 声变调鸣叫）
        private static void BeginBurst() {
            Roost hit = null;
            float best = float.MaxValue;
            foreach (Roost r in roosts) {
                float d = Vector2.DistanceSquared(RidgeWorldPos(r.RidgeTile), startlePos);
                if (d < best) {
                    best = d;
                    hit = r;
                }
            }
            if (hit == null) {
                return;
            }
            hit.BurstTicks = KiyumeScore.RoostBurstTicks;
            foreach (PerchCrow c in hit.Perch) {
                if (!c.Active) {
                    continue;
                }
                c.Vel = new Vector2(Main.rand.NextFloat(-1.6f, 1.6f), -Main.rand.NextFloat(1.8f, 3.0f));
                c.Frame = 1;
                c.FrameCounter = Main.rand.NextFloat(3f);
            }
            SoundEngine.PlaySound(SoundID.Bird with {
                Volume = KiyumeScore.CapAccent(KiyumeScore.RoostCryVol),
                Pitch = KiyumeScore.RoostCryPitch,
                MaxInstances = 2
            }, startlePos);
            KiyumeCrowFlight.StartleFrom(startlePos);
        }

        //炸窝散拍：上抛加速 + 应激扑翼（3f 一帧，原版 5f 的惊飞版），散拍走完交给飞行层
        private static void AdvanceBurst(Roost r) {
            r.BurstTicks--;
            if (r.BurstTicks == KiyumeScore.RoostBurstTicks - KiyumeScore.RoostCryGapTicks) {
                SoundEngine.PlaySound(SoundID.Bird with {
                    Volume = KiyumeScore.CapAccent(KiyumeScore.RoostCryVol * 0.8f),
                    Pitch = KiyumeScore.RoostCryPitch + 0.18f,
                    MaxInstances = 2
                }, RidgeWorldPos(r.RidgeTile));
            }
            foreach (PerchCrow c in r.Perch) {
                if (!c.Active) {
                    continue;
                }
                c.Pos += c.Vel;
                c.Vel.Y -= 0.08f;
                c.Vel.X *= 1.01f;
                c.FaceLeft = c.Vel.X < 0f;
                c.FrameCounter += 1f;
                if (c.FrameCounter >= 3f) {
                    c.FrameCounter = 0f;
                    if (++c.Frame > 4) {
                        c.Frame = 1;
                    }
                }
                c.Alpha = MathF.Max(0f, c.Alpha - 1f / KiyumeScore.RoostBurstTicks);
            }
        }

        //栖息拍：冷却期窝空着，冷却尽了悄悄回窝显形（无声，回来这件事不该被注意到）
        private static void AdvancePerch(Roost r, float presence) {
            bool hidden = r.Cooldown > 0;
            float target = hidden ? 0f : presence * KiyumeScore.RoostBodyAlpha;
            foreach (PerchCrow c in r.Perch) {
                if (!c.Active) {
                    continue;
                }
                c.Alpha = MathHelper.Lerp(c.Alpha, target, 0.04f);
                c.Vel = Vector2.Zero;
                c.Frame = 0;    //蹲踞帧（对源 case 301：静止 frame.Y=0）
                if (hidden && c.Alpha < 0.02f && c.Pos != c.Home) {
                    c.Pos = c.Home;
                    c.FaceLeft = Main.rand.NextBool();
                }
            }
        }

        //==================== 绘制（KiyumeFogSystem.PostDrawTiles 飞行鸦群后调用）====================

        internal static void Draw(SpriteBatch sb) {
            if (Main.dedServ || Main.gameMenu) {
                return;
            }
            bool any = false;
            foreach (Roost r in roosts) {
                foreach (PerchCrow c in r.Perch) {
                    any |= c.Active && c.Alpha > 0.02f;
                }
            }
            if (!any) {
                return;
            }

            Main.instance.LoadNPC(NPCID.Raven);
            Texture2D tex = TextureAssets.Npc[NPCID.Raven].Value;
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
            foreach (Roost r in roosts) {
                foreach (PerchCrow c in r.Perch) {
                    if (c.Active && c.Alpha > 0.02f) {
                        DrawOne(sb, c, tex, hound);
                    }
                }
            }
            sb.End();
            gd.Textures[1] = null;
        }

        private static void DrawOne(SpriteBatch sb, PerchCrow c, Texture2D tex, Effect hound) {
            //帧区间运行时判 + 源矩形内缩 1px + shader 帧界钳制，双通道防帧表渗色（飞行层同款纪律）
            int frameCount = Math.Max(Main.npcFrameCount[NPCID.Raven], 1);
            int frame = Math.Clamp(c.Frame, 0, frameCount - 1);
            int frameH = tex.Height / frameCount;
            var source = new Rectangle(0, frame * frameH + 1, tex.Width, frameH - 2);
            //底心锚：爪贴瓦顶
            var origin = new Vector2(source.Width * 0.5f, source.Height);
            //栖息微摆：几乎读不出的重心倒换，证明鸦是活的
            float sway = MathF.Sin(Main.GlobalTimeWrappedHourly * 1.3f + c.SwayPhase) * 0.03f;

            if (hound == null) {
                //着色器缺失：近黑剪影回退（犬影/飞行层同款回退链）
                SpriteEffects fb = c.FaceLeft ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
                sb.Draw(tex, c.Pos - Main.screenPosition, source,
                    new Color(10, 5, 8) * (c.Alpha * 0.9f), sway, origin, c.Scale, fb, 0f);
                return;
            }

            //淡出走 uDissolve：化进雾里，不是被调低了透明度
            float dissolve = MathHelper.Clamp(1f - c.Alpha, 0f, 1f) * 0.75f;

            hound.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            hound.Parameters["uSeed"]?.SetValue(c.Seed);
            hound.Parameters["uUvRect"]?.SetValue(new Vector4(
                0f, source.Y / (float)tex.Height, 1f, source.Height / (float)tex.Height));
            hound.Parameters["uTexel"]?.SetValue(new Vector2(1f / tex.Width, 1f / tex.Height));
            hound.Parameters["uAspect"]?.SetValue(tex.Width / (float)source.Height);
            hound.Parameters["uFlipH"]?.SetValue(c.FaceLeft ? 1f : 0f);
            hound.Parameters["uFlipV"]?.SetValue(0f);
            hound.Parameters["uMode"]?.SetValue(1f);
            hound.Parameters["uSeamGate"]?.SetValue(0f);
            //蹲着比飞着更稳：噪声扰动压到飞行层的四分之三
            hound.Parameters["uWobble"]?.SetValue(0.006f);
            //鸦不点睛（飞行层同款：余烬双目是犬影的身份记号）
            hound.Parameters["uEyeGlow"]?.SetValue(0f);
            hound.Parameters["uEyeAnchor"]?.SetValue(new Vector2(0.5f, 0.4f));
            hound.Parameters["uDissolve"]?.SetValue(dissolve);
            hound.Parameters["uEdgeTint"]?.SetValue(new Color(112, 26, 26).ToVector3());
            hound.CurrentTechnique = hound.Techniques["TechHound"];
            hound.CurrentTechnique.Passes[0].Apply();

            sb.Draw(tex, c.Pos - Main.screenPosition, source,
                Color.White * MathHelper.Clamp(c.Alpha * 1.25f, 0f, 1f),
                sway, origin, c.Scale, SpriteEffects.None, 0f);
        }

        //==================== 验收口 ====================

        /// <summary>一行状态摘要（TestItem 验收用）</summary>
        internal static string StatusLine() {
            int cooling = 0;
            foreach (Roost r in roosts) {
                if (r.Cooldown > 0) {
                    cooling++;
                }
            }
            return $"[檐鸦] 栖息点{roosts.Count}(冷却中{cooling}) 演出闸{showHold} 版本{startleVersion}";
        }

        /// <summary>调试口：惊起距 pos 最近且不在冷却的栖息点（TestItem 验收用）</summary>
        internal static bool DebugStartleNearest(Vector2 pos) {
            Roost hit = null;
            float best = float.MaxValue;
            foreach (Roost r in roosts) {
                if (r.Cooldown > 0) {
                    continue;
                }
                float d = Vector2.DistanceSquared(RidgeWorldPos(r.RidgeTile), pos);
                if (d < best) {
                    best = d;
                    hit = r;
                }
            }
            if (hit == null) {
                return false;
            }
            Startle(hit);
            return true;
        }
    }
}
