using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Scenarios.Kiyume.Fog;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Kiyume.Ambience
{
    /// <summary>
    /// 鬼梦鸦群掠雾（KIY-P5-E · E6）：几只鸦影贴着雾面掠过，翅膀几乎碰到雾。
    /// 两条入场：B 级自走周期的巡航过屏（松散队形横穿），与导演
    /// <see cref="KiyumeDirector.NotifyCrowOmen"/> 的前置信号（从 origin 附近地面惊起，
    /// 训练「鸦飞=有事」的语法）。原版乌鸦帧 + <c>KikasaHound.fx</c> 实体态群影材质，
    /// 槽位/探地/回退全套镜像 <see cref="KiyumeHoundShade"/>。<br/>
    /// 绘制由 <see cref="KiyumeFogSystem.PostDrawTiles"/> 在犬影之后、近带雾海之前调用，
    /// 与犬影同层「雾墙后」；Update 自持本 ModSystem，不依赖雾系统驱动。<br/>
    /// 权威端+同步字段：无。纯客户端表现，不是 NPC、无判定；本类 static 只是
    /// 本地演出进度，非 per-player 游戏状态，netcode 静态禁令不适用。
    /// </summary>
    internal class KiyumeCrowFlight : ModSystem
    {
        private sealed class Crow
        {
            internal bool Active;
            internal bool Omen;         //惊起鸦（近处显形有下限）还是巡航鸦（全吃雾门）
            internal Vector2 Pos;
            internal float Vx;
            internal float FogGap;      //贴雾高差（px），个体各异
            internal float YScatter;    //队形纵向错落
            internal float BobPhase;
            internal float Scale;
            internal float Seed;
            internal int Frame;         //0=蹲踞 1..4=扑翼（原版乌鸦帧表，运行时钳制）
            internal float FrameCounter;
            internal float FlapRate;
            internal float Alpha;
            internal int PerchHold;     //惊起前的蹲踞帧（逐只错开）
            internal int Life;          //>0 倒数（惊起鸦）；-1 巡航（过屏转倒数）
            internal float GroundY;     //探地缓存（每 8f 刷新）
            internal int ProbeTimer;
        }

        //槽位帽 6（计划书 CrowCount 上限），一次只飞一群
        private static readonly Crow[] crows = [new(), new(), new(), new(), new(), new()];
        private static int cruiseTimer = -1;    //-1=首个周期未掷

        internal static void Clear() {
            foreach (Crow c in crows) {
                c.Active = false;
                c.Alpha = 0f;
                c.Life = 0;
            }
            cruiseTimer = -1;
        }

        public override void OnWorldLoad() => Clear();
        public override void ClearWorld() => Clear();
        public override void Unload() => Clear();

        //==================== 驱动 ====================

        public override void PostUpdateEverything() {
            if (Main.dedServ || Main.gameMenu) {
                return;
            }
            //跟雾走而不跟场景走：主世界 ForceEnable 看样时与犬影同进退
            float presence = KiyumeFogSystem.Presence;
            if (presence < 0.01f) {
                return;
            }
            Player player = Main.LocalPlayer;
            if (player?.active != true) {
                return;
            }

            bool anyActive = false;
            foreach (Crow c in crows) {
                if (c.Active) {
                    anyActive = true;
                    Advance(c, player, presence);
                }
            }
            UpdateCruiseClock(player, presence, anyActive);
        }

        //B 级自走周期：松散队形从屏缘外入画，贴雾横穿
        private static void UpdateCruiseClock(Player player, float presence, bool anyActive) {
            if (anyActive) {
                return;    //一次只飞一群，飞完再走钟
            }
            if (cruiseTimer < 0) {
                //初相错开：进梦后首群不与其它点缀同刻
                cruiseTimer = (int)(NextCruisePeriod() * Main.rand.NextFloat(0.3f, 0.8f));
                return;
            }
            if (--cruiseTimer > 0) {
                return;
            }
            //投放门：雾要够浓才有可贴的雾面（枯林以东雾衰减，投了也看不见），短重试
            float fogLine = KiyumeFogTide.SurfaceAt(player.Center.X);
            if (presence < KiyumeScore.CrowPresenceGate
                || KiyumeFogSim.DensityAt(new Vector2(player.Center.X, fogLine + 16f)) < KiyumeScore.CrowCruiseFogGate) {
                cruiseTimer = 600;
                return;
            }
            StartCruise(player);
            cruiseTimer = NextCruisePeriod();
        }

        private static int NextCruisePeriod() {
            int period = Main.rand.Next(KiyumeScore.CrowPeriodMin, KiyumeScore.CrowPeriodMax + 1);
            //犬让位期 B 级周期 ×2：真威胁在场，点缀退后（导演门 7 的 B 级条款）
            if (KiyumeDirector.HoundYieldActive) {
                period *= 2;
            }
            return period;
        }

        private static void StartCruise(Player player) {
            int count = Main.rand.Next(KiyumeScore.CrowCountMin, KiyumeScore.CrowCountMax + 1);
            float dir = Main.rand.NextBool() ? 1f : -1f;
            //从行进方向的后侧屏缘外起步，队尾拖在更外侧
            float headX = player.Center.X - dir * (Main.screenWidth * 0.5f + KiyumeScore.CrowEdgeMarginPx);
            for (int i = 0; i < count && i < crows.Length; i++) {
                Crow c = crows[i];
                float x = headX - dir * i * Main.rand.NextFloat(40f, 90f);
                SeedCrow(c, omen: false, x, dir);
                c.Pos.Y = FlightTargetY(c, x, 0f);
                c.PerchHold = 0;
                c.Life = -1;    //巡航：过屏即转倒数
            }
        }

        /// <summary>
        /// 导演前置信号接收口：从 origin 附近地面惊起一群（ArmScare(CrowOmen) 同入口）。
        /// 转发发生在档期过门帧，惊起演出全长 180~300f 即「提前量」——
        /// 鸦群先于事件高潮在场，慢事件（灭灯波/月轮）开演时鸦还在天上。
        /// </summary>
        internal static void StartleFrom(Vector2 origin) {
            if (Main.dedServ || Main.gameMenu) {
                return;
            }
            //惊起优先但不瞬移：天上的巡航鸦转入溶解让位，惊起群先占空槽；
            //槽全被占的极端场合才顶替（顶替 Alpha 从 0 重升，无一帧闪没）
            foreach (Crow c in crows) {
                if (c.Active && c.Life != 0) {
                    c.Life = c.Life < 0 ? 30 : Math.Min(c.Life, 30);
                }
            }
            int count = Main.rand.Next(KiyumeScore.CrowCountMin, KiyumeScore.CrowCountMax + 1);
            float dir = Main.rand.NextBool() ? 1f : -1f;
            int seeded = 0;
            for (int pass = 0; pass < 2 && seeded < count; pass++) {
                for (int i = 0; i < crows.Length && seeded < count; i++) {
                    Crow c = crows[i];
                    if (pass == 0 && c.Active) {
                        continue;    //第一轮只取空槽
                    }
                    if (pass == 1 && c.Active && c.Omen) {
                        continue;    //顶替只顶巡航鸦，不顶同批惊起鸦
                    }
                    SeedOmenCrow(c, origin, dir, seeded);
                    seeded++;
                }
            }
            //起飞一声夜鸟惊起（裁决 22：零新音频，Owl 变调替声）
            SoundEngine.PlaySound(SoundID.Owl with {
                Volume = KiyumeScore.CapAccent(KiyumeScore.CrowOwlVol),
                Pitch = KiyumeScore.CrowOwlPitch,
                MaxInstances = 2
            }, origin);
        }

        private static void SeedOmenCrow(Crow c, Vector2 origin, float dir, int order) {
            float x = origin.X + Main.rand.NextFloat(-160f, 160f);
            SeedCrow(c, omen: true, x, dir);
            //探到地面就蹲踞后起飞（逐只错开），探不到就直接在雾线上显形（宁高勿钻地）
            if (TryFindGround(x, origin.Y - 240f, out float ground)) {
                c.Pos.Y = ground;
                c.PerchHold = 6 + order * Main.rand.Next(4, 11);
            }
            else {
                c.Pos.Y = FlightTargetY(c, x, 0f);
                c.PerchHold = 0;
            }
            c.Life = Main.rand.Next(KiyumeScore.CrowOmenLeadMin, KiyumeScore.CrowOmenLeadMax + 1)
                + c.PerchHold;
        }

        private static void SeedCrow(Crow c, bool omen, float x, float dir) {
            c.Active = true;
            c.Omen = omen;
            c.Pos = new Vector2(x, 0f);
            c.Vx = dir * Main.rand.NextFloat(KiyumeScore.CrowSpeedMin, KiyumeScore.CrowSpeedMax);
            c.FogGap = Main.rand.NextFloat(KiyumeScore.CrowFogGapMin, KiyumeScore.CrowFogGapMax);
            c.YScatter = Main.rand.NextFloat(-KiyumeScore.CrowScatterPx, KiyumeScore.CrowScatterPx);
            c.BobPhase = Main.rand.NextFloat(MathHelper.TwoPi);
            c.Scale = Main.rand.NextFloat(0.9f, 1.1f);
            c.Seed = Main.rand.NextFloat(10f);
            c.Frame = 1;
            c.FrameCounter = 0f;
            c.FlapRate = Main.rand.NextFloat(0.9f, 1.15f);
            c.Alpha = 0f;
            c.GroundY = float.MaxValue;
            c.ProbeTimer = 0;
        }

        private static void Advance(Crow c, Player player, float presence) {
            //蹲踞拍：原地帧 0，等自己的起飞点
            if (c.PerchHold > 0) {
                c.PerchHold--;
                c.Frame = 0;
                c.Alpha = MathHelper.Lerp(c.Alpha, AlphaTarget(c, presence), 0.15f);
                if (c.Life > 0) {
                    c.Life--;
                }
                return;
            }

            c.Pos.X += c.Vx;
            //探地缓存每 8f 刷新：飞行带不许低过地表（低潮时雾线沉进村地面之下）
            if (--c.ProbeTimer <= 0) {
                c.ProbeTimer = 8;
                c.GroundY = TryFindGround(c.Pos.X, c.Pos.Y - 420f, out float ground)
                    ? ground : float.MaxValue;
            }
            float bob = MathF.Sin(Main.GlobalTimeWrappedHourly * 2f + c.BobPhase) * 4f;
            c.Pos.Y = MathHelper.Lerp(c.Pos.Y, FlightTargetY(c, c.Pos.X, bob), 0.06f);

            //扑翼循环：飞行帧 1..4，原版每 5f 进一帧的节奏（个体略有快慢）
            c.FrameCounter += c.FlapRate;
            if (c.FrameCounter >= 5f) {
                c.FrameCounter -= 5f;
                c.Frame++;
                if (c.Frame > 4 || c.Frame < 1) {
                    c.Frame = 1;
                }
            }

            //巡航鸦飞过对侧屏缘转入倒数；惊起鸦寿命自然走完
            if (c.Life < 0) {
                float cullDist = Main.screenWidth * 0.5f + KiyumeScore.CrowEdgeMarginPx + 120f;
                float off = c.Pos.X - player.Center.X;
                if ((c.Vx > 0f && off > cullDist) || (c.Vx < 0f && off < -cullDist)) {
                    c.Life = 45;
                }
            }
            else if (c.Life > 0) {
                c.Life--;
            }

            c.Alpha = MathHelper.Lerp(c.Alpha, AlphaTarget(c, presence), 0.08f);
            if (c.Life == 0 && c.Alpha < 0.02f) {
                c.Active = false;
            }
        }

        //显形目标：雾在下面才有影可读；惊起鸦给下限（惊起本身就是显形拍），
        //巡航鸦全吃雾门（飞过无雾段自然隐去）；尾段 45f 化进雾里
        private static float AlphaTarget(Crow c, float presence) {
            float fogLine = KiyumeFogTide.SurfaceAt(c.Pos.X);
            float fogBelow = KiyumeFogSim.DensityAt(new Vector2(c.Pos.X, fogLine + 20f));
            float fogK = MathHelper.Clamp((fogBelow - 0.12f) / 0.28f, 0f, 1f);
            if (c.Omen) {
                fogK = 0.35f + 0.65f * fogK;
            }
            float lifeK = c.Life < 0 ? 1f : MathHelper.Clamp(c.Life / 45f, 0f, 1f);
            return presence * fogK * lifeK;
        }

        //飞行高度：贴着雾面（SurfaceAt − 个体高差 + 错落 + 微浮沉），但不低过地表净空
        private static float FlightTargetY(Crow c, float x, float bob) {
            float y = KiyumeFogTide.SurfaceAt(x) - c.FogGap + c.YScatter + bob;
            if (c.GroundY < float.MaxValue) {
                y = MathF.Min(y, c.GroundY - KiyumeScore.CrowGroundClearPx);
            }
            return y;
        }

        //==================== 绘制（KiyumeFogSystem.PostDrawTiles 犬影后调用）====================

        internal static void Draw(SpriteBatch sb) {
            if (Main.dedServ || Main.gameMenu) {
                return;
            }
            bool any = false;
            foreach (Crow c in crows) {
                any |= c.Active && c.Alpha > 0.02f;
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
            foreach (Crow c in crows) {
                if (c.Active && c.Alpha > 0.02f) {
                    DrawOne(sb, c, tex, hound);
                }
            }
            sb.End();
            gd.Textures[1] = null;
        }

        private static void DrawOne(SpriteBatch sb, Crow c, Texture2D tex, Effect hound) {
            //帧区间运行时判：乌鸦帧表以 Main.npcFrameCount 现值为准，帧号钳在表内
            int frameCount = Math.Max(Main.npcFrameCount[NPCID.Raven], 1);
            int frame = Math.Clamp(c.Frame, 0, frameCount - 1);
            int frameH = tex.Height / frameCount;
            //源矩形上下各内缩 1px + shader 帧界钳制，双通道防帧表渗色（犬影同款纪律）
            var source = new Rectangle(0, frame * frameH + 1, tex.Width, frameH - 2);
            var origin = new Vector2(source.Width * 0.5f, source.Height * 0.5f);
            //乌鸦飞行帧原生朝右（对 FindFrame case 301 核实），与狼相反：向左飞才翻转
            bool faceLeft = c.Vx < 0f;
            //压着雾面的轻微俯仰（原版 velocity.X*0.1 的收敛版）
            float bank = MathHelper.Clamp(c.Vx * 0.05f, -0.25f, 0.25f);

            if (hound == null) {
                //着色器缺失：近黑剪影回退（犬影同款回退链）
                SpriteEffects fb = faceLeft ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
                sb.Draw(tex, c.Pos - Main.screenPosition, source,
                    new Color(10, 5, 8) * (c.Alpha * 0.9f), bank, origin, c.Scale, fb, 0f);
                return;
            }

            //淡出走 uDissolve：它该是化进雾里，不是被调低了透明度
            float dissolve = MathHelper.Clamp(1f - c.Alpha, 0f, 1f) * 0.75f;

            hound.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            hound.Parameters["uSeed"]?.SetValue(c.Seed);
            hound.Parameters["uUvRect"]?.SetValue(new Vector4(
                0f, source.Y / (float)tex.Height, 1f, source.Height / (float)tex.Height));
            hound.Parameters["uTexel"]?.SetValue(new Vector2(1f / tex.Width, 1f / tex.Height));
            hound.Parameters["uAspect"]?.SetValue(tex.Width / (float)source.Height);
            hound.Parameters["uFlipH"]?.SetValue(faceLeft ? 1f : 0f);
            hound.Parameters["uFlipV"]?.SetValue(0f);
            hound.Parameters["uMode"]?.SetValue(1f);
            hound.Parameters["uSeamGate"]?.SetValue(0f);
            hound.Parameters["uWobble"]?.SetValue(0.008f);
            //鸦不点睛：余烬双目是犬影的身份记号，群鸦只是黑
            hound.Parameters["uEyeGlow"]?.SetValue(0f);
            hound.Parameters["uEyeAnchor"]?.SetValue(new Vector2(0.5f, 0.4f));
            hound.Parameters["uDissolve"]?.SetValue(dissolve);
            hound.Parameters["uEdgeTint"]?.SetValue(new Color(112, 26, 26).ToVector3());
            hound.CurrentTechnique = hound.Techniques["TechHound"];
            hound.CurrentTechnique.Passes[0].Apply();

            sb.Draw(tex, c.Pos - Main.screenPosition, source,
                Color.White * MathHelper.Clamp(c.Alpha * 1.25f, 0f, 1f),
                bank, origin, c.Scale, SpriteEffects.None, 0f);
        }

        //从起始高度向下探地表（犬影同款）
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

        /// <summary>一行状态摘要（TestItem 验收用）</summary>
        internal static string StatusLine() {
            int active = 0;
            int omen = 0;
            foreach (Crow c in crows) {
                if (c.Active) {
                    active++;
                    if (c.Omen) {
                        omen++;
                    }
                }
            }
            return $"[鸦群] 巡航钟{cruiseTimer} 在天{active}只(惊起{omen})";
        }
    }
}
