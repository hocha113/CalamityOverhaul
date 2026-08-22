using CalamityOverhaul.Content.PRTTypes;
using CalamityOverhaul.Content.Scenarios.Shenyo;
using InnoVault.Cinematics;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.Scenarios.OniRainWorlds
{
    /// <summary>身处鬼雨世界的嵌套深度（0=真实世界，最深 <see cref="OniRainWorldState.MaxDepth"/>），按玩家独立存档</summary>
    internal sealed class OniRainWorldPlayer : ModPlayer
    {
        public int Depth;

        /// <summary>近期被鬼奴击中的剩余帧数（运行期量），死亡拦截据此判源</summary>
        public int OniHitFrames { get; private set; }

        /// <summary>抵达深层后的静默计时（运行期量），沈幽初遇等它走完再开口</summary>
        public int DeepArrivalCalm { get; internal set; }

        /// <summary>鬼奴命中登记：致死打击与 PreKill 同帧结算，窗口给短即可</summary>
        internal void NoteOniHit() => OniHitFrames = 12;

        public override void ResetEffects() {
            if (OniHitFrames > 0) {
                OniHitFrames--;
            }
            //静默拍只在演出全收后走表
            if (DeepArrivalCalm > 0 && !OniRainWorldTransition.Active
                && !OniRainDescentTransition.Active) {
                DeepArrivalCalm--;
            }
        }

        public override void SaveData(TagCompound tag) {
            if (Depth > 0) {
                tag["oniRainDepth"] = Depth;
            }
        }

        public override void LoadData(TagCompound tag) {
            //旧档只有 bool 键，视作第一层
            int depth = tag.TryGet("oniRainDepth", out int saved) ? saved
                : tag.ContainsKey("inOniRainWorld") ? 1 : 0;
            Depth = Math.Clamp(depth, 0, OniRainWorldState.MaxDepth);
        }

        public override void OnEnterWorld() {
            //带档直接醒在深层：给一拍静默，沈幽再开口
            if (Depth >= 2) {
                DeepArrivalCalm = 90;
            }
        }

        /// <summary>
        /// 死亡拖入：初遇未完成时在第一层被鬼奴杀死不真死——
        /// 拦下死亡，醒在更深一层的雨里（复用深潜演出，溺亡起手拍）。
        /// 仅本地玩家生效；初遇完成后鬼奴致死回归真死。
        /// </summary>
        public override bool PreKill(double damage, int hitDirection, bool pvp,
            ref bool playSound, ref bool genGore, ref PlayerDeathReason damageSource) {
            if (Main.dedServ || Player.whoAmI != Main.myPlayer) {
                return true;
            }
            if (Depth != 1 || OniHitFrames <= 0) {
                return true;
            }
            if (ShenyoStorySync.PostFirstMetIsComplete) {
                return true;
            }
            if (OniRainWorldTransition.Active || OniRainDescentTransition.Active) {
                return true;
            }

            playSound = false;
            genGore = false;
            OniHitFrames = 0;
            //醒过来还有几分力气；演出全程另有逐帧无敌
            Player.statLife = Math.Max(1, (int)(Player.statLifeMax2 * 0.4f));
            Player.GivePlayerImmuneState(90);
            ShenyoStorySync.ArrivedByDeath = true;

            //运镜失败不致命，演出照走
            OniRainDescentTransition.BeginFromDrown(Player, Player.Bottom);
            CutsceneDirector.Play<OniRainDescentCutscene>(Player);
            return false;
        }
    }

    /// <summary>
    /// 鬼雨世界的常驻状态：氛围目标喂给 <see cref="Content.Wraiths.Abilities.GhostRains.GhostRainAmbience"/>
    /// （压顶/天幕/滤镜全套复用），并自带满幕雨帘、潮雾与远雷的本地表现。<br/>
    /// 世界可多层嵌套（雨下还有雨）：层数越深雨越密、雷越频、脸痕越常见，
    /// 分层强度经 <see cref="DepthGrade"/> 喂给天空与调色。
    /// </summary>
    internal static class OniRainWorldState
    {
        /// <summary>嵌套深度上限</summary>
        public const int MaxDepth = 3;

        //沿用鬼雨既定湿墨色板：灰白尸雨/尸斑青/潮雾沉青
        private static readonly Color RainPale = new(170, 185, 190);
        private static readonly Color RainCorpse = new(140, 170, 165);
        private static readonly Color MistDamp = new(58, 66, 70);

        //深度分级表，索引 = 深度-1：雨密度乘数/脸痕稀有度(NextBool 分母)/雷间隔
        private static readonly float[] rainMultByDepth = [1f, 1.15f, 1.3f];
        private static readonly int[] faceStreakDenByDepth = [70, 40, 22];
        private static readonly int[] thunderMinByDepth = [480, 360, 260];
        private static readonly int[] thunderMaxByDepth = [960, 700, 520];

        private static float dropCarry;
        private static int thunderTimer;
        //雷声相对闪光的延迟帧数，光先于声的距离感
        private static int thunderSoundDelay;

        /// <summary>本地玩家所处的嵌套深度，0=真实世界</summary>
        public static int LocalDepth {
            get {
                if (Main.dedServ || Main.gameMenu) {
                    return 0;
                }
                Player player = Main.LocalPlayer;
                if (player?.active != true
                    || !player.TryGetModPlayer(out OniRainWorldPlayer orp)) {
                    return 0;
                }
                return orp.Depth;
            }
        }

        /// <summary>本地玩家是否身处鬼雨世界（任意深度）</summary>
        public static bool LocalIn => LocalDepth > 0;

        /// <summary>深度归一化 0~1：第一层 0、最深层 1，喂天空分层与日光附加压暗</summary>
        public static float DepthGrade => LocalDepth <= 1 ? 0f
            : (LocalDepth - 1) / (float)(MaxDepth - 1);

        /// <summary>给鬼雨氛围控制器的目标强度：在雨世界恒满，演出结算前给预压顶</summary>
        public static float GlobalAmbientTarget
            => LocalIn ? 1f : OniRainWorldTransition.AmbientPreGloom;

        /// <summary>下潜一层（入雨与深潜共用的结算入口），仅本地玩家生效</summary>
        internal static void DescendLocal(Player player) {
            if (player == null || player.whoAmI != Main.myPlayer) {
                return;
            }
            OniRainWorldPlayer orp = player.GetModPlayer<OniRainWorldPlayer>();
            orp.Depth = Math.Min(orp.Depth + 1, MaxDepth);
            thunderTimer = 300;
            //抵达深层后的静默拍：演出收尾后再计，沈幽等它走完才开口
            if (orp.Depth >= 2) {
                orp.DeepArrivalCalm = 75;
            }
        }

        /// <summary>上浮一层，浮出第一层即回到真实世界</summary>
        internal static void AscendLocal(Player player) {
            if (player == null || player.whoAmI != Main.myPlayer) {
                return;
            }
            OniRainWorldPlayer orp = player.GetModPlayer<OniRainWorldPlayer>();
            orp.Depth = Math.Max(orp.Depth - 1, 0);
        }

        /// <summary>被送出鬼雨世界：任意深度直接归零，仅本地玩家生效（沈幽送客的结算口）</summary>
        internal static void ExitToSurfaceLocal(Player player) {
            if (player == null || player.whoAmI != Main.myPlayer) {
                return;
            }
            player.GetModPlayer<OniRainWorldPlayer>().Depth = 0;
        }

        /// <summary>调试出口：D3 上浮一层，氛围沿控制器包络自行排干</summary>
        internal static void DebugExit() {
            Player player = Main.LocalPlayer;
            if (player?.active != true || !LocalIn) {
                return;
            }
            AscendLocal(player);
            SoundEngine.PlaySound(SoundID.SplashWeak with {
                Pitch = -0.4f,
                Volume = 0.7f,
                MaxInstances = 3,
            }, player.Center);
        }

        /// <summary>
        /// 常驻表现：满幕雨帘 + 贴地潮雾 + 稀有脸痕 + 远雷，密度吃氛围强度并按深度分级。<br/>
        /// 演出结算前也承担前兆稀雨——两个世界开始互相渗透的零星雨丝；
        /// 深潜演出的骤雨增压（<see cref="OniRainDescentTransition.RainSurge"/>）也加在这里。
        /// </summary>
        internal static void UpdateFx() {
            if (Main.dedServ || Main.gameMenu) {
                return;
            }
            bool inWorld = LocalIn;
            float preRain = OniRainWorldTransition.PreRainDensity;
            if (!inWorld && preRain <= 0f) {
                return;
            }

            Player player = Main.LocalPlayer;
            int depthIndex = Math.Clamp(LocalDepth, 1, MaxDepth) - 1;
            float density = inWorld
                ? Content.Wraiths.Abilities.GhostRains.GhostRainAmbience.Intensity
                    * rainMultByDepth[depthIndex] + OniRainDescentTransition.RainSurge
                    + OniRainExitTransition.RainSurge
                : preRain;
            if (density < 0.02f) {
                return;
            }

            SpawnRainBand(density);

            if (density > 0.2f && Main.GameUpdateCount % 4 == 0) {
                SpawnMist(player);
                if (density > 0.55f) {
                    SpawnMist(player);
                }
                //深层潮雾更浓
                if (DepthGrade > 0.6f) {
                    SpawnMist(player);
                }
            }

            if (density > 0.8f && Main.rand.NextBool(faceStreakDenByDepth[depthIndex])) {
                SpawnFaceStreak();
            }

            //远雷，稳态下的低频心跳：天幕云底先闪惨白，雷声隔十几到四十帧才到；
            //前兆雨阶段不抢演出节拍的雷声，越深雷越频、越沉
            if (inWorld && --thunderTimer <= 0) {
                thunderTimer = Main.rand.Next(
                    thunderMinByDepth[depthIndex], thunderMaxByDepth[depthIndex]);
                OniRainWorldSky.NotifyThunder();
                thunderSoundDelay = Main.rand.Next(15, 40);
            }
            if (inWorld && thunderSoundDelay > 0 && --thunderSoundDelay == 0) {
                SoundEngine.PlaySound(SoundID.Thunder with {
                    Pitch = Main.rand.NextFloat(-1f, -0.75f),
                    Volume = Main.rand.NextFloat(0.22f, 0.4f) * (1f + DepthGrade * 0.3f),
                    MaxInstances = 3,
                }, player.Center + new Vector2(Main.rand.NextFloat(-900f, 900f), -400f));
            }
        }

        internal static void ResetLocal() {
            dropCarry = 0f;
            thunderTimer = 0;
            thunderSoundDelay = 0;
        }

        /// <summary>满幕雨帘：约 0.02 滴/像素宽/帧 @密度1，风向按世界种子定相</summary>
        private static void SpawnRainBand(float density) {
            float left = Main.screenPosition.X - 160f;
            float right = Main.screenPosition.X + Main.screenWidth + 160f;

            dropCarry += density * 0.02f * (right - left);
            int count = Math.Min((int)dropCarry, 72);
            dropCarry -= count;
            //进量超帧上限时截断积欠，防深层+骤雨叠加下无限攒债
            dropCarry = Math.Min(dropCarry, 30f);
            if (count <= 0) {
                return;
            }

            float wind = MathF.Sin(Main.worldID % 255 * 0.37f) * 2.2f * density;
            for (int i = 0; i < count; i++) {
                Vector2 pos = new(Main.rand.NextFloat(left, right),
                    Main.screenPosition.Y - Main.rand.NextFloat(10f, 220f));
                Vector2 vel = new(wind + Main.rand.NextFloat(-0.35f, 0.35f),
                    Main.rand.NextFloat(11f, 17f));
                Color color = (Main.rand.NextBool(7) ? RainCorpse : RainPale)
                    * Main.rand.NextFloat(0.42f, 0.65f);
                PRTLoader.NewParticle<PRT_GhostRainDrop>(pos, vel, color,
                    Main.rand.NextFloat(0.8f, 1.25f))
                    ?.Configure(Main.rand.Next(70, 110), vel.X);
            }
        }

        /// <summary>贴地潮雾，探不到地面不生</summary>
        private static void SpawnMist(Player player) {
            float x = player.Center.X + Main.rand.NextFloat(
                -Main.screenWidth * 0.55f - 200f, Main.screenWidth * 0.55f + 200f);
            if (!TryFindGround(x, player.Center.Y - 60f, out float groundY)) {
                return;
            }
            Vector2 pos = new(x, groundY - Main.rand.NextFloat(6f, 40f));
            Vector2 vel = new(Main.rand.NextFloat(-0.35f, 0.35f), Main.rand.NextFloat(-0.08f, 0f));
            PRTLoader.NewParticle<PRT_GhostRainMist>(pos, vel,
                MistDamp * Main.rand.NextFloat(0.75f, 1f),
                Main.rand.NextFloat(0.7f, 1.25f))
                ?.Configure(Main.rand.Next(90, 160));
        }

        /// <summary>雨幕稀有的脸痕竖丝</summary>
        private static void SpawnFaceStreak() {
            Vector2 pos = new(
                Main.rand.NextFloat(Main.screenPosition.X + 60f,
                    Main.screenPosition.X + Main.screenWidth - 60f),
                Main.screenPosition.Y + Main.rand.NextFloat(60f, 280f));
            PRTLoader.NewParticle<PRT_GhostRainFaceStreak>(pos,
                new Vector2(0f, Main.rand.NextFloat(1.6f, 2.4f)),
                RainPale * 0.5f, Main.rand.NextFloat(0.85f, 1.15f))
                ?.Configure(Main.rand.Next(50, 74));
        }

        /// <summary>从起始高度向下探地表</summary>
        private static bool TryFindGround(float x, float fromY, out float groundY) {
            int tileX = (int)(x / 16f);
            int tileY = (int)(fromY / 16f);
            for (int i = 0; i < 46; i++) {
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
    }

    /// <summary>演出与常驻表现的驱动泵，兼本地化载体</summary>
    internal class OniRainWorldSystem : ModSystem, ILocalizedModType
    {
        public string LocalizationCategory => "OniRainWorld";

        public static LocalizedText InteractHint { get; private set; }
        public static LocalizedText GrabHint { get; private set; }
        public static LocalizedText OniDeathReason { get; private set; }

        public override void SetStaticDefaults() {
            InteractHint = this.GetLocalization(nameof(InteractHint), () => "[右键] 撑伞入雨");
            GrabHint = this.GetLocalization(nameof(GrabHint), () => "[右键] 夺伞");
            OniDeathReason = this.GetLocalization(nameof(OniDeathReason), () => "{0}被伞下的东西拖进了雨里");
        }

        //送出演出的起演静默拍计数
        private static int sendOffArmDelay;

        public override void PostUpdateEverything() {
            if (Main.dedServ) {
                return;
            }
            OniRainWorldTransition.Update();
            OniRainDescentTransition.Update();
            OniRainExitTransition.Update();
            OniRainWorldState.UpdateFx();
            KasaOnis.KasaOniDirector.Update();
            UpdateShenyoSendOff();
        }

        /// <summary>
        /// 沈幽送客的编排与自愈：初遇播完且伞未交付时，静默一拍后起送出演出；
        /// 掉线/崩溃打断也会在下次条件齐备时重新送一遍；
        /// 已在真实世界的残局（送出成立但交付被打断）直接补发。
        /// </summary>
        private static void UpdateShenyoSendOff() {
            if (Main.gameMenu) {
                sendOffArmDelay = 0;
                return;
            }
            Player player = Main.LocalPlayer;
            if (player?.active != true || !player.Alives()) {
                sendOffArmDelay = 0;
                return;
            }
            if (!Shenyo.ShenyoStorySync.PostFirstMetIsComplete
                || Shenyo.ShenyoStorySync.KikasaGranted) {
                sendOffArmDelay = 0;
                return;
            }
            if (Narrative.NarrativeTriggerGate.IsBusy || CutsceneDirector.IsPlaying) {
                sendOffArmDelay = 0;
                return;
            }
            if (OniRainWorldTransition.Active || OniRainDescentTransition.Active
                || OniRainExitTransition.Active) {
                return;
            }

            //已在真实世界：交付被打断的残局，直接补发
            if (!OniRainWorldState.LocalIn) {
                OniRainExitTransition.GrantKikasa(player);
                return;
            }

            //沈幽话音落下，雨合拢前留一口气
            if (++sendOffArmDelay < 45) {
                return;
            }
            sendOffArmDelay = 0;
            //运镜失败不致命，演出照走
            OniRainExitTransition.Begin(player);
            CutsceneDirector.Play<OniRainExitCutscene>(player);
        }

        public override void ClearWorld() {
            if (!Main.dedServ) {
                OniRainWorldTransition.HardReset();
                OniRainDescentTransition.HardReset();
                OniRainExitTransition.HardReset();
                OniRainWorldState.ResetLocal();
                KasaOnis.KasaOniDirector.ResetLocal();
                sendOffArmDelay = 0;
            }
        }
    }
}
