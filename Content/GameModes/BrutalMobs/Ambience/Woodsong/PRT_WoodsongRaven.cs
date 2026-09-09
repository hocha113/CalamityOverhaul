using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Ambience.Woodsong
{
    /// <summary>
    /// 惊鸦演员：原版渡鸦贴图，三态一线。
    /// 栖息（第 0 帧蹲在树冠或地面，偶尔换向小跳）→ 骚动（换向、小跳、一声鸦鸣，约 18 帧）→ 惊飞（扑翼爬升，飞出屏幕才销毁）。
    /// 栖息群由 <see cref="WoodsongAmbience"/> 落在屏外，玩家走近时背离玩家惊飞；
    /// 屏外威胁锁定玩家时全群骚动后背离来敌惊飞，鸟群逃离的反向即敌来向。
    /// 全程不在屏内凭空出现或消失：屏外落位、屏外销毁，屏内只有起飞与远去。
    /// 纯氛围本地演出，AlphaBlend 世界光照绘制
    /// </summary>
    internal class PRT_WoodsongRaven : BasePRT
    {
        public override string Texture => CWRConstant.VaultPlaceholder;
        public override bool CanPool => true;
        public override int InGame_World_MaxCount => 16;

        internal const int ModeBird = 0;
        internal const int ModePerch = 1;
        internal const int ModeAgitated = 2;

        /// <summary>栖息鸟被本地玩家惊飞的距离（像素）</summary>
        internal const float FlushRadius = 210f;
        /// <summary>栖息态离本地玩家多远才静默回收（远超屏幕半对角，玩家看不见）</summary>
        private const float PerchCullRange = 2600f;
        /// <summary>飞行态离本地玩家多远必定回收</summary>
        private const float FlightCullRange = 3000f;
        /// <summary>骚动到起飞的拍长（帧）</summary>
        private const int AgitateFrames = 18;
        /// <summary>飞行硬上限（帧）：正常情况下早已飞出屏幕，这里只是安全网</summary>
        private const int FlightCap = 720;
        /// <summary>飞出屏缘多少像素后销毁</summary>
        private const int OffScreenMargin = 240;
        /// <summary>远去感拉满所需帧数</summary>
        private const float RecedeFrames = 240f;

        /// <summary>羽色：近黑偏冷，乘世界光照后白日是剪影、夜里沉进天色</summary>
        private static readonly Color Plumage = new(22, 24, 32);

        //==== 栖息群登记与指令（屏幕级演出量，非逐玩家）====
        private static uint stampFrame;
        private static uint perchStamp;
        /// <summary>本帧已盖戳的栖息鸟数</summary>
        internal static int PerchedCount { get; private set; }
        /// <summary>栖息群位置（最后盖戳的那只，群内偏差 ±30px）</summary>
        internal static Vector2 PerchAnchor { get; private set; }
        /// <summary>场上是否有栖息群（近两帧内有鸟盖戳）</summary>
        internal static bool HasPerchedFlock => PerchedCount > 0 && Main.GameUpdateCount - perchStamp <= 2;

        private static uint commandStamp;
        private static float commandDir;
        private static bool commandAgitate;
        private static uint cawStamp;
        private static uint rustleStamp;

        private int mode;
        private float phase;
        private float baseScale;
        private int faceDir = -1;
        private int idleTimer;
        private bool hopping;
        private float restY;
        private int roostLife;
        private bool onTree;
        private int agitateTimer;
        private int stagger;
        private Vector2 launchVel;
        private int flightTime;
        private bool enteredScreen;
        private uint ackStamp;

        /// <summary>命令全部栖息鸟惊飞：agitate=先骚动一拍再起飞（报敌/雾夜），否则立刻起飞</summary>
        internal static void FlushFlock(float dirX, bool agitate) {
            commandStamp = Main.GameUpdateCount;
            commandDir = dirX;
            commandAgitate = agitate;
        }

        internal static void ResetRegistry() {
            stampFrame = 0;
            perchStamp = 0;
            PerchedCount = 0;
            PerchAnchor = default;
            commandStamp = 0;
            cawStamp = 0;
            rustleStamp = 0;
        }

        /// <summary>落一只栖息鸟：脚点在 footPos（地表或树冠面），roostFrames 后自行离场</summary>
        public PRT_WoodsongRaven ConfigurePerch(Vector2 footPos, int face, int roostFrames, bool treetop) {
            mode = ModePerch;
            faceDir = face == 0 ? (Main.rand.NextBool() ? 1 : -1) : Math.Sign(face);
            roostLife = roostFrames;
            onTree = treetop;
            Position = footPos - new Vector2(0f, FrameHeight() * 0.5f * Scale);
            restY = Position.Y;
            Velocity = Vector2.Zero;
            idleTimer = Main.rand.Next(40, 160);
            return this;
        }

        /// <summary>直接以飞行态放出（屏外横穿入场用）：生成时给的速度即巡航速度，至少错一帧后经 TakeOff 动身</summary>
        public PRT_WoodsongRaven ConfigureFlight(int staggerFrames) {
            mode = ModeBird;
            launchVel = Velocity;
            stagger = Math.Max(staggerFrames, 1);
            Velocity = Vector2.Zero;
            return this;
        }

        public override void Reset() {
            base.Reset();
            mode = ModeBird;
            phase = 0f;
            baseScale = 1f;
            faceDir = -1;
            idleTimer = 0;
            hopping = false;
            restY = 0f;
            roostLife = 0;
            onTree = false;
            agitateTimer = 0;
            stagger = 0;
            launchVel = default;
            flightTime = 0;
            enteredScreen = false;
            ackStamp = 0;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AlphaBlend;
            //屏外落位与屏外销毁都由本类自管，不吃框架的离屏回收
            ShouldKillWhenOffScreen = false;
            Lifetime = -1;
            Opacity = 1f;
            phase = Main.rand.NextFloat(100f);
            baseScale = Scale;
            launchVel = Velocity;
        }

        public override void AI() {
            switch (mode) {
                case ModePerch:
                    PerchAI();
                    break;
                case ModeAgitated:
                    AgitatedAI();
                    break;
                default:
                    FlightAI();
                    break;
            }
        }

        //==================== 栖息 ====================

        private void PerchAI() {
            StampPerched();
            UpdateHop();

            Player lp = Main.LocalPlayer;
            if (lp == null || !lp.active) {
                return;
            }
            Vector2 toBird = Position - lp.Center;
            if (toBird.LengthSquared() > PerchCullRange * PerchCullRange) {
                //离得太远：屏外静默回收
                active = false;
                return;
            }

            //玩家走近：背离玩家立刻惊飞（原版鸟类的惯常反应）
            if (!lp.dead && Math.Abs(toBird.X) < FlushRadius && Math.Abs(toBird.Y) < FlushRadius) {
                float dir = toBird.X != 0f ? Math.Sign(toBird.X) : (Main.rand.NextBool() ? 1f : -1f);
                Flush(dir, Main.rand.Next(0, 9));
                return;
            }

            //外部指令（报敌/雾夜）：每条指令只响应一次
            if (commandStamp != 0 && commandStamp != ackStamp && Main.GameUpdateCount - commandStamp <= 2) {
                ackStamp = commandStamp;
                if (commandAgitate) {
                    BeginAgitate();
                }
                else {
                    Flush(commandDir, Main.rand.Next(0, 11));
                }
                return;
            }

            //栖息超时：不在屏内就静默回收，在屏内则自行飞走，绝不在玩家眼前消失
            if (roostLife > 0 && Time > roostLife) {
                if (!OnScreen(0)) {
                    active = false;
                }
                else {
                    Flush(Main.rand.NextBool() ? 1f : -1f, Main.rand.Next(0, 13));
                }
                return;
            }

            //空闲：多数时候换个方向看，少数时候原地小跳
            if (!hopping && --idleTimer <= 0) {
                idleTimer = Main.rand.Next(60, 180);
                if (Main.rand.Next(10) < 7) {
                    faceDir = -faceDir;
                }
                else {
                    StartHop(1.4f);
                }
            }
        }

        //==================== 骚动 ====================

        private void BeginAgitate() {
            mode = ModeAgitated;
            agitateTimer = 0;
            stagger = Main.rand.Next(0, 9);
            //全群只叫一声：同帧第一只负责
            if (cawStamp != Main.GameUpdateCount) {
                cawStamp = Main.GameUpdateCount;
                SoundEngine.PlaySound(SoundID.Roar with {
                    Volume = 0.16f,
                    Pitch = Main.rand.NextFloat(0.82f, 0.95f),
                    MaxInstances = 2
                }, Position);
            }
        }

        private void AgitatedAI() {
            StampPerched();
            UpdateHop();
            agitateTimer++;
            //第一拍：齐齐转头看向来处，随后一次小跳
            if (agitateTimer == 1) {
                faceDir = commandDir >= 0f ? -1 : 1;
            }
            if (agitateTimer == 6 && !hopping) {
                StartHop(1.6f);
            }
            if (agitateTimer >= AgitateFrames + stagger) {
                Flush(commandDir, 0);
            }
        }

        //==================== 惊飞 ====================

        private void Flush(float dirX, int staggerFrames) {
            mode = ModeBird;
            flightTime = 0;
            stagger = staggerFrames;
            hopping = false;
            float dir = dirX >= 0f ? 1f : -1f;
            launchVel = new Vector2(dir * Main.rand.NextFloat(1.0f, 2.2f), -Main.rand.NextFloat(1.6f, 2.6f));
            Velocity = Vector2.Zero;
            enteredScreen = OnScreen(0);
        }

        private void FlightAI() {
            if (stagger > 0) {
                //错拍：原地等自己的起飞帧，一群鸟不齐步
                stagger--;
                Velocity = Vector2.Zero;
                if (stagger == 0) {
                    TakeOff();
                }
                return;
            }
            if (flightTime == 0 && Velocity == Vector2.Zero) {
                TakeOff();
            }
            flightTime++;

            //横向逐步加速逃离，纵向扑翼起伏爬升
            Velocity.X = MathHelper.Clamp(Velocity.X * 1.012f, -4.6f, 4.6f);
            Velocity.Y = -(1.15f + MathF.Sin((Time + phase) * 0.5f) * 0.75f);

            //远去感：缩小并变淡到下限，不归零，屏内永不凭空消失
            float recede = Math.Min(flightTime / RecedeFrames, 1f);
            Scale = baseScale * MathHelper.Lerp(1f, 0.75f, recede);
            Opacity = MathHelper.Lerp(1f, 0.6f, recede);

            bool onScreen = OnScreen(0);
            if (onScreen) {
                enteredScreen = true;
            }
            //销毁只在屏外：进过屏的飞出屏缘一段后回收，没进过屏的靠寿命上限与距离兜底
            if (enteredScreen && !OnScreen(OffScreenMargin)) {
                active = false;
                return;
            }
            if (flightTime > FlightCap) {
                active = false;
                return;
            }
            Player lp = Main.LocalPlayer;
            if (lp != null && lp.active
                && Vector2.DistanceSquared(Position, lp.Center) > FlightCullRange * FlightCullRange) {
                active = false;
            }
        }

        /// <summary>真正动身：套上起飞速度，一声扑翼；一次惊飞事件只带一声枝叶响与一阵落叶（30 帧窗）</summary>
        private void TakeOff() {
            Velocity = launchVel;
            SoundEngine.PlaySound(SoundID.Item32 with {
                Volume = 0.24f,
                Pitch = 0.2f + Main.rand.NextFloat(0.3f),
                MaxInstances = 4
            }, Position);
            if (rustleStamp != 0 && Main.GameUpdateCount - rustleStamp < 30) {
                return;
            }
            rustleStamp = Main.GameUpdateCount;
            SoundEngine.PlaySound(SoundID.Grass with {
                Volume = 0.36f,
                Pitch = -0.08f,
                MaxInstances = 3
            }, Position);
            if (!onTree) {
                return;
            }
            int shed = Main.rand.Next(5, 9);
            for (int i = 0; i < shed; i++) {
                PRTLoader.NewParticle<PRT_WoodsongLeaf>(
                    Position + Main.rand.NextVector2Circular(34f, 18f),
                    new Vector2(Main.rand.NextFloat(-1.8f, 1.8f), Main.rand.NextFloat(-0.5f, 0.8f)),
                    Color.White, Main.rand.NextFloat(0.8f, 1.1f))
                    ?.Configure(Main.windSpeedCurrent * 1.5f, Main.rand.Next(110, 170));
            }
        }

        //==================== 公用 ====================

        private void StampPerched() {
            uint now = Main.GameUpdateCount;
            if (stampFrame != now) {
                stampFrame = now;
                PerchedCount = 0;
            }
            PerchedCount++;
            perchStamp = now;
            PerchAnchor = Position;
        }

        private void StartHop(float power) {
            hopping = true;
            Velocity.Y = -power;
        }

        /// <summary>小跳的落回：重力拉回脚点即停，位置由框架先加速度再进 AI</summary>
        private void UpdateHop() {
            if (!hopping) {
                Velocity = Vector2.Zero;
                return;
            }
            Velocity.Y += 0.22f;
            if (Velocity.Y > 0f && Position.Y >= restY) {
                Position.Y = restY;
                Velocity = Vector2.Zero;
                hopping = false;
            }
        }

        private bool OnScreen(int extend)
            => VaultUtils.IsPointOnScreen(Position - Main.screenPosition, extend);

        private static float FrameHeight() {
            Main.instance.LoadNPC(NPCID.Raven);
            Texture2D tex = TextureAssets.Npc[NPCID.Raven].Value;
            int frames = Math.Max(Main.npcFrameCount[NPCID.Raven], 1);
            return tex == null ? 30f : tex.Height / (float)frames;
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            if (Opacity <= 0.01f || !OnScreen(64)) {
                return false;
            }
            Main.instance.LoadNPC(NPCID.Raven);
            Texture2D tex = TextureAssets.Npc[NPCID.Raven].Value;
            if (tex == null) {
                return false;
            }
            int frames = Math.Max(Main.npcFrameCount[NPCID.Raven], 1);
            //原版 FindFrame：静止只用第 0 帧，飞行循环 1~4 帧每 4 帧一换
            bool flying = mode == ModeBird && stagger <= 0 && Velocity != Vector2.Zero;
            int frame = flying && frames > 2 ? 1 + (Time / 4) % (frames - 1) : 0;
            Rectangle src = tex.Frame(1, frames, 0, frame);

            bool faceRight = flying ? Velocity.X > 0f : faceDir > 0;
            SpriteEffects flip = faceRight ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
            float rotation = flying ? Velocity.X * 0.1f : 0f;

            //世界光照：黑羽在暗处沉没，月下与白日读作剪影（保 0.3 底）
            Color light = Lighting.GetColor((int)(Position.X / 16f), (int)(Position.Y / 16f));
            float lightK = 0.3f + 0.7f * ((light.R + light.G + light.B) / 765f);
            Color col = Plumage * (0.92f * lightK * Opacity);

            spriteBatch.Draw(tex, Position - Main.screenPosition, src, col,
                rotation, src.Size() * 0.5f, Scale, flip, 0f);
            return false;
        }
    }
}
