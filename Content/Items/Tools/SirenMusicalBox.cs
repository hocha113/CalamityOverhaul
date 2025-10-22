using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.HalibutLegend;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using InnoVault.TileProcessors;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.Enums;
using Terraria.GameContent.ObjectInteractions;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Terraria.ObjectData;

namespace CalamityOverhaul.Content.Items.Tools
{
    /// <summary>
    /// 海妖八音盒物品
    /// </summary>
    internal class SirenMusicalBox : ModItem
    {
        public override string Texture => CWRConstant.Item + "Tools/SirenMusicalBox";
        public static LocalizedText DeathText { get; private set; }
        public override void SetStaticDefaults() {
            DeathText = this.GetLocalization(nameof(DeathText), () => "{0}在未知的袭击下化作腐尸");
        }
        public override void SetDefaults() {
            Item.width = 32;
            Item.height = 32;
            Item.maxStack = 99;
            Item.useTurn = true;
            Item.autoReuse = true;
            Item.useAnimation = 15;
            Item.useTime = 10;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.consumable = true;
            Item.value = Item.buyPrice(0, 5, 0, 0);
            Item.rare = ItemRarityID.Purple;
            Item.createTile = ModContent.TileType<SirenMusicalBoxTile>();
        }
    }

    /// <summary>
    /// 海妖八音盒物块
    /// </summary>
    internal class SirenMusicalBoxTile : ModTile
    {
        public override string Texture => CWRConstant.Item + "Tools/SirenMusicalBoxTile";

        public const int Width = 2;
        public const int Height = 2;

        public override void SetStaticDefaults() {
            Main.tileLighted[Type] = true;
            Main.tileNoAttach[Type] = true;
            Main.tileLavaDeath[Type] = false;
            Main.tileWaterDeath[Type] = false;
            Main.tileFrameImportant[Type] = true;
            AddMapEntry(new Color(139, 0, 139), VaultUtils.GetLocalizedItemName<SirenMusicalBox>());
            AnimationFrameHeight = 36; //2帧，每帧高度18*2=36

            TileObjectData.newTile.CopyFrom(TileObjectData.Style2x2);
            TileObjectData.newTile.Width = Width;
            TileObjectData.newTile.Height = Height;
            TileObjectData.newTile.Origin = new Point16(0, 1);
            TileObjectData.newTile.AnchorBottom = new AnchorData(AnchorType.SolidTile | AnchorType.SolidWithTop | AnchorType.SolidSide, TileObjectData.newTile.Width, 0);
            TileObjectData.newTile.CoordinateHeights = [16, 16];
            TileObjectData.newTile.LavaDeath = false;
            TileObjectData.addTile(Type);
        }

        public override void MouseOver(int i, int j) => Main.LocalPlayer.SetMouseOverByTile<SirenMusicalBox>();

        public override bool CanExplode(int i, int j) => false;

        public override bool HasSmartInteract(int i, int j, SmartInteractScanSettings settings) => true;

        public override void NumDust(int i, int j, bool fail, ref int num) => num = fail ? 1 : 3;

        public override void KillMultiTile(int i, int j, int frameX, int frameY) {
            Item.NewItem(new EntitySource_TileBreak(i, j), i * 16, j * 16, Width * 16, Height * 16, ModContent.ItemType<SirenMusicalBox>());

            for (int z = 0; z < 13; z++) {
                Dust.NewDust(new Vector2(i * 16, j * 16), Width * 16, Height * 16, DustID.SilverCoin);
            }
        }

        public override bool PreDraw(int i, int j, SpriteBatch spriteBatch) {
            Tile t = Main.tile[i, j];
            int frameXPos = t.TileFrameX;
            int frameYPos = t.TileFrameY;

            if (!VaultUtils.SafeGetTopLeft(i, j, out var point)) {
                return false;
            }
            if (!TileProcessorLoader.ByPositionGetTP(point, out SirenMusicalBoxTP module)) {
                return false;
            }

            //根据音乐状态切换帧
            frameYPos += (module.IsMusicPlaying ? 1 : 0) * (Height * 18);

            Texture2D tex = ModContent.Request<Texture2D>(Texture).Value;
            Vector2 offset = Main.drawToScreen ? Vector2.Zero : new Vector2(Main.offScreenRange);
            Vector2 drawOffset = new Vector2(i * 16 - Main.screenPosition.X, j * 16 - Main.screenPosition.Y) + offset;
            Color drawColor = Lighting.GetColor(i, j);

            if (!t.IsHalfBlock && t.Slope == 0) {
                spriteBatch.Draw(tex, drawOffset, new Rectangle(frameXPos, frameYPos, 16, 16), drawColor, 0.0f, Vector2.Zero, 1f, SpriteEffects.None, 0.0f);
            }
            return false;
        }

        public override void ModifyLight(int i, int j, ref float r, ref float g, ref float b) {
            if (VaultUtils.SafeGetTopLeft(i, j, out var point)) {
                if (TileProcessorLoader.ByPositionGetTP(point, out SirenMusicalBoxTP module)) {
                    if (module.IsMusicPlaying) {
                        float pulse = (MathF.Sin(Main.GlobalTimeWrappedHourly * 3f) * 0.3f + 0.7f);
                        r = 0.5f * pulse;
                        g = 0f;
                        b = 0.5f * pulse;
                    }
                }
            }
        }
    }

    /// <summary>
    /// 管理海妖八音盒的音乐播放和全局状态
    /// </summary>
    internal class SirenMusicalSystem : ModSystem
    {
        /// <summary>
        /// 当前活动的八音盒位置（用于音乐范围判定）
        /// </summary>
        public static Point16 ActiveBoxPosition = Point16.Zero;

        /// <summary>
        /// 是否有激活的八音盒
        /// </summary>
        public static bool HasActiveBox => ActiveBoxPosition != Point16.Zero;

        public override void PostUpdateEverything() {
            //检查本地玩家是否应该播放音乐
            Player localPlayer = Main.LocalPlayer;
            if (localPlayer == null || !localPlayer.active) {
                return;
            }

            SirenMusicalBoxPlayer modPlayer = localPlayer.GetModPlayer<SirenMusicalBoxPlayer>();

            if (HasActiveBox) {
                modPlayer.IsMusicPlaying = true;
                Main.newMusic = Main.musicBox2 = MusicLoader.GetMusicSlot("CalamityOverhaul/Assets/Sounds/Music/SirenMusic");
            }
            else {
                modPlayer.IsMusicPlaying = false;
            }
        }

        public override void ClearWorld() {
            ActiveBoxPosition = Point16.Zero;
        }
    }

    /// <summary>
    /// 跟踪玩家与海妖八音盒的交互状态
    /// </summary>
    internal class SirenMusicalBoxPlayer : ModPlayer
    {
        /// <summary>
        /// 玩家是否正在听海妖音乐
        /// </summary>
        public bool IsMusicPlaying;

        /// <summary>
        /// 玩家的八音盒计时器
        /// </summary>
        public int MusicTimer;

        /// <summary>
        /// 是否处于八音盒诅咒状态（音乐结束必死）
        /// </summary>
        public bool IsCursed;

        /// <summary>
        /// 玩家是否曾经通过钓鱼获得过八音盒（首次保底逻辑用）
        /// </summary>
        public bool HasSirenMusicalBox;

        /// <summary>
        /// 玩家所属的八音盒位置
        /// </summary>
        public Point16 BoundBoxPosition;

        /// <summary>
        /// 音乐持续时间（23秒）
        /// </summary>
        public const int MusicDuration = 60 * 23;

        public override void ResetEffects() {
            //如果不在播放音乐，重置状态
            if (!IsMusicPlaying) {
                MusicTimer = 0;
            }
        }

        public override void PostUpdate() {
            //如果正在播放音乐，更新计时器
            if (IsCursed && IsMusicPlaying) {
                MusicTimer++;
                //音乐结束，执行死亡
                if (MusicTimer >= MusicDuration) {
                    ExecuteDeath();
                }

                if (Main.rand.NextBool(13)) {
                    Newphonogram(Player.Center);
                }
            }
        }

        public static void Newphonogram(Vector2 position) {
            int goreType = Main.rand.Next(570, 573);
            float wind = Main.WindForVisuals * 2f;
            float randX = 1f + Main.rand.NextFloat(-1.5f, 1.5f);
            float randY = 1f + Main.rand.NextFloat(-0.5f, 0.5f);

            if (goreType == 572) {
                position.X -= 8f;
            }
            else if (goreType == 571) {
                position.X -= 4f;
            }

            Vector2 velocity = new(wind * randX, -0.5f * randY);

            Gore.NewGore(new EntitySource_TileUpdate((int)position.X, (int)position.Y), position, velocity, goreType, 0.8f);
        }

        /// <summary>
        /// 执行玩家死亡
        /// </summary>
        private void ExecuteDeath() {
            if (Player.dead) {
                return;
            }

            //移除无敌
            Player.immune = false;
            Player.immuneTime = 0;
            Player.immuneNoBlink = false;

            //播放恐怖音效
            SoundEngine.PlaySound(SoundID.NPCDeath59 with { Volume = 0.9f, Pitch = -0.8f }, Player.Center);
            SoundEngine.PlaySound(SoundID.Zombie103 with { Volume = 0.7f, Pitch = -0.6f }, Player.Center);

            //死亡演出特效
            SpawnDeathEffects();

            //重置状态
            ResetCurse();

            PlayerDeathReason damageSource = PlayerDeathReason.ByCustomReason(
                SirenMusicalBox.DeathText.ToNetworkText(Player.name)
            );

            //杀死玩家
            Player.KillMe(damageSource, Player.statLifeMax2 * 10, 0, false);
        }

        /// <summary>
        /// 生成死亡特效
        /// </summary>
        private void SpawnDeathEffects() {
            for (int i = 0; i < 150; i++) {
                Vector2 velocity = Main.rand.NextVector2CircularEdge(12f, 12f);
                float speed = Main.rand.NextFloat(0.6f, 1.4f);
                velocity *= speed;

                Dust dust = Dust.NewDustDirect(Player.Center, 0, 0, DustID.Shadowflame, velocity.X, velocity.Y, 100,
                    Main.rand.NextBool() ? Color.DarkMagenta : Color.Purple, Main.rand.NextFloat(2.5f, 4f));
                dust.noGravity = true;
                dust.fadeIn = 1.5f;
            }

            for (int i = 0; i < 60; i++) {
                float angle = MathHelper.TwoPi / 60f * i;
                float radius = Main.rand.NextFloat(20f, 60f);
                Vector2 pos = Player.Center + angle.ToRotationVector2() * radius;
                Vector2 vel = new Vector2(0, -Main.rand.NextFloat(3f, 6f)).RotatedBy(angle * 0.3f);

                PRT_Light soulFragment = new PRT_Light(pos, vel, Main.rand.NextFloat(0.5f, 0.75f),
                    Color.Cyan * 0.8f, Main.rand.Next(40, 70));
                PRTLoader.AddParticle(soulFragment);
            }

            for (int layer = 0; layer < 5; layer++) {
                int particlesPerRing = 24;
                float ringRadius = 40f + layer * 35f;
                for (int j = 0; j < particlesPerRing; j++) {
                    float angle = MathHelper.TwoPi / particlesPerRing * j;
                    Vector2 pos = Player.Center + angle.ToRotationVector2() * ringRadius;
                    Vector2 vel = angle.ToRotationVector2() * Main.rand.NextFloat(4f, 8f);

                    Color ringColor = Color.Lerp(Color.Purple, Color.DarkRed, layer / 5f);
                    PRT_Light particle = new PRT_Light(pos, vel, Main.rand.NextFloat(0.5f, 0.75f),
                        ringColor, Main.rand.Next(30, 60));
                    PRTLoader.AddParticle(particle);
                }
            }

            for (int i = 0; i < 10; i++) {
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                Vector2 tentacleStart = Player.Center + angle.ToRotationVector2() * Main.rand.NextFloat(250f, 350f);

                //绘制触手轨迹
                int segmentCount = Main.rand.Next(20, 30);
                for (int j = 0; j < segmentCount; j++) {
                    float progress = j / (float)segmentCount;
                    Vector2 pos = Vector2.Lerp(tentacleStart, Player.Center, CWRUtils.EaseOutCubic(progress));

                    //添加一些扭曲
                    float waveOffset = MathF.Sin(progress * MathHelper.Pi * 3f + i) * 20f;
                    Vector2 perpendicular = Vector2.Normalize(new Vector2(-(tentacleStart.Y - Player.Center.Y), tentacleStart.X - Player.Center.X));
                    pos += perpendicular * waveOffset;

                    //触手本体
                    Dust tentacle = Dust.NewDustDirect(pos, 0, 0, DustID.DungeonWater, 0f, 0f, 100,
                        Color.Lerp(Color.DarkBlue, Color.Cyan, progress), Main.rand.NextFloat(2f, 3.5f) * (1f - progress * 0.5f));
                    tentacle.noGravity = true;
                    tentacle.velocity = Main.rand.NextVector2Circular(1f, 1f);

                    //触手末端的能量
                    if (j % 3 == 0) {
                        PRT_Light energy = new PRT_Light(pos, Main.rand.NextVector2Circular(2f, 2f),
                            Main.rand.NextFloat(0.31f, 0.5f), Color.Cyan * 0.6f, Main.rand.Next(20, 40));
                        PRTLoader.AddParticle(energy);
                    }
                }

                //触手末端爆炸
                for (int k = 0; k < 8; k++) {
                    Vector2 burstVel = Main.rand.NextVector2CircularEdge(5f, 5f);
                    Dust burst = Dust.NewDustDirect(Player.Center, 0, 0, DustID.Blood, burstVel.X, burstVel.Y, 100,
                        Color.DarkRed, Main.rand.NextFloat(2.5f, 3.5f));
                    burst.noGravity = true;
                }
            }

            int eyeCount = Main.rand.Next(6, 10);
            for (int i = 0; i < eyeCount; i++) {
                float angle = MathHelper.TwoPi / eyeCount * i + Main.rand.NextFloat(-0.2f, 0.2f);
                Vector2 eyePos = Player.Center + angle.ToRotationVector2() * Main.rand.NextFloat(150f, 250f);

                //白色巩膜
                for (int j = 0; j < 16; j++) {
                    float eyeAngle = MathHelper.TwoPi / 16f * j;
                    Vector2 scleraPos = eyePos + eyeAngle.ToRotationVector2() * 20f;

                    Dust sclera = Dust.NewDustDirect(scleraPos, 0, 0, DustID.Ice, 0f, 0f, 100, Color.White, 2f);
                    sclera.noGravity = true;
                    sclera.velocity = (eyePos - scleraPos).SafeNormalize(Vector2.Zero) * Main.rand.NextFloat(0.3f, 0.8f);
                    sclera.fadeIn = 1.2f;
                }

                //深蓝色
                for (int j = 0; j < 12; j++) {
                    float irisAngle = MathHelper.TwoPi / 12f * j;
                    Vector2 irisPos = eyePos + irisAngle.ToRotationVector2() * 12f;

                    Dust iris = Dust.NewDustDirect(irisPos, 0, 0, DustID.DungeonWater, 0f, 0f, 100, Color.Cyan, 1.8f);
                    iris.noGravity = true;
                    iris.velocity = (eyePos - irisPos).SafeNormalize(Vector2.Zero) * 0.5f;
                }

                //血红色
                for (int j = 0; j < 8; j++) {
                    Vector2 pupilOffset = Main.rand.NextVector2Circular(5f, 5f);
                    Dust pupil = Dust.NewDustDirect(eyePos + pupilOffset, 0, 0, DustID.Blood, 0f, 0f, 100, Color.DarkRed, 2.5f);
                    pupil.noGravity = true;
                    pupil.velocity = -pupilOffset * 0.1f;
                }

                //眼睛凝视光线
                Vector2 gazeDirection = (Player.Center - eyePos).SafeNormalize(Vector2.Zero);
                for (int j = 0; j < 30; j++) {
                    Vector2 gazePos = eyePos + gazeDirection * (j * 8f);
                    PRT_Light gaze = new PRT_Light(gazePos, gazeDirection * 0.5f,
                        Main.rand.NextFloat(0.6f, 0.83f), Color.Red * 0.6f, Main.rand.Next(15, 30));
                    PRTLoader.AddParticle(gaze);
                }
            }

            for (int i = 0; i < 200; i++) {
                Vector2 mistVel = Main.rand.NextVector2Unit() * Main.rand.NextFloat(3f, 15f);
                Dust mist = Dust.NewDustDirect(Player.Center, 0, 0, DustID.Blood, mistVel.X, mistVel.Y, 100,
                    Color.DarkRed, Main.rand.NextFloat(1.5f, 3f));
                mist.noGravity = true;
                mist.fadeIn = Main.rand.NextFloat(0.8f, 1.5f);
            }

            for (int i = 0; i < 100; i++) {
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                float radius = Main.rand.NextFloat(5f, 100f);
                Vector2 pos = Player.Center + angle.ToRotationVector2() * radius;
                Vector2 vel = (Player.Center - pos).SafeNormalize(Vector2.Zero) * Main.rand.NextFloat(8f, 16f);

                Dust vortex = Dust.NewDustDirect(pos, 0, 0, DustID.Shadowflame, vel.X, vel.Y, 100,
                    Color.Black, Main.rand.NextFloat(2f, 3.5f));
                vortex.noGravity = true;
                vortex.fadeIn = 1.5f;
            }

            for (int i = 0; i < 80; i++) {
                Vector2 pos = Player.Center + Main.rand.NextVector2Circular(40f, 40f);
                Vector2 vel = new Vector2(Main.rand.NextFloat(-2f, 2f), -Main.rand.NextFloat(6f, 12f));

                Color soulColor = Main.rand.Next(3) switch {
                    0 => Color.White,
                    1 => Color.Cyan,
                    _ => Color.Purple
                };

                PRT_Light soul = new PRT_Light(pos, vel, Main.rand.NextFloat(0.35f, 0.55f),
                    soulColor * 0.7f, Main.rand.Next(60, 100));
                PRTLoader.AddParticle(soul);
            }

            for (int i = 0; i < 50; i++) {
                float angle = MathHelper.TwoPi / 50f * i;
                Vector2 pos = Player.Center + angle.ToRotationVector2() * Main.rand.NextFloat(80f, 150f);

                for (int j = 0; j < 5; j++) {
                    Vector2 crackPos = pos + Main.rand.NextVector2Circular(10f, 10f);
                    Dust crack = Dust.NewDustDirect(crackPos, 0, 0, DustID.Shadowflame, 0f, 0f, 100,
                        Color.Lerp(Color.Purple, Color.Black, j / 5f), Main.rand.NextFloat(1.5f, 2.5f));
                    crack.noGravity = true;
                    crack.velocity = angle.ToRotationVector2().RotatedBy(MathHelper.PiOver2) * (j + 1);
                }
            }
        }

        /// <summary>
        /// 绑定到八音盒并开始诅咒
        /// </summary>
        public void BindToBox(Point16 boxPosition) {
            IsCursed = true;
            BoundBoxPosition = boxPosition;
            MusicTimer = 0;
        }

        /// <summary>
        /// 重置诅咒状态
        /// </summary>
        public void ResetCurse() {
            IsCursed = false;
            IsMusicPlaying = false;
            MusicTimer = 0;
            BoundBoxPosition = Point16.Zero;
        }

        public override void SaveData(TagCompound tag) {
            tag["IsCursed"] = IsCursed;
            tag["MusicTimer"] = MusicTimer;
            tag["BoundBoxX"] = BoundBoxPosition.X;
            tag["BoundBoxY"] = BoundBoxPosition.Y;
            //保存是否曾钓到八音盒
            tag["HasSirenMusicalBox"] = HasSirenMusicalBox;
        }

        public override void LoadData(TagCompound tag) {
            try {
                IsCursed = tag.GetBool("IsCursed");
                MusicTimer = tag.GetInt("MusicTimer");
                if (tag.TryGet("BoundBoxX", out short x) && tag.TryGet("BoundBoxY", out short y)) {
                    BoundBoxPosition = new Point16(x, y);
                }

                //读取是否曾钓到八音盒
                if (tag.TryGet("HasSirenMusicalBox", out bool hasSirenMusicalBox)) {
                    HasSirenMusicalBox = hasSirenMusicalBox;
                }
            } catch { }
        }

        public override bool PreKill(double damage, int hitDirection, bool pvp, ref bool playSound, ref bool genDust, ref PlayerDeathReason damageSource) {
            if (IsCursed) {
                if (Player.TryGetOverride<HalibutPlayer>(out var halibutPlayer) && halibutPlayer.ResurrectionSystem.Ratio == 1f) {
                    return true;//厉鬼复苏的死亡无法阻挡
                }
                return false;
            }
            return base.PreKill(damage, hitDirection, pvp, ref playSound, ref genDust, ref damageSource);
        }

        public override void OnRespawn() {
            ResetCurse();//不管如何，重生后都重置一次诅咒状态
        }

        public override void CatchFish(FishingAttempt attempt, ref int itemDrop, ref int npcSpawn, ref AdvancedPopupRequest sonar, ref Vector2 sonarPosition) {
            if (!CWRServerConfig.Instance.WeaponOverhaul || attempt.inHoney || attempt.inLava) {
                return;
            }

            if (HasSirenMusicalBox) {
                if (Main.rand.NextBool(800)) {
                    itemDrop = ModContent.ItemType<SirenMusicalBox>();
                }
            }
            else {//必出
                itemDrop = ModContent.ItemType<SirenMusicalBox>();
                HasSirenMusicalBox = true;
            }
        }
    }

    /// <summary>
    /// 海妖八音盒处理器，控制物块状态和粒子效果
    /// </summary>
    internal class SirenMusicalBoxTP : TileProcessor, ICWRLoader
    {
        public override int TargetTileID => ModContent.TileType<SirenMusicalBoxTile>();
        public Vector2 Center => PosInWorld + new Vector2(SirenMusicalBoxTile.Width * 8, SirenMusicalBoxTile.Height * 8);

        //音乐相关
        public bool IsMusicPlaying;

        //粒子效果
        private int particleTimer;
        private const int ParticleSpawnRate = 5;

        //活跃的玩家列表
        private readonly List<int> activePlayers = new();

        public override void SetProperty() => LoadenWorldSendData = false;

        public override void OnKill() {
            if (IsMusicPlaying) {
                StopMusic();
            }
            if (VaultUtils.isClient) {
                SendData();
            }
        }

        public override bool? RightClick(int i, int j, Tile tile, Player player) {
            if (player.TryGetModPlayer<SirenMusicalBoxPlayer>(out var sirenMusicalBoxPlayer)
                && sirenMusicalBoxPlayer.IsCursed) {
                SoundEngine.PlaySound(CWRSound.ButtonZero with { Pitch = -0.62f });
                return false;//被诅咒的玩家无法关上八音盒
            }
            if (!IsMusicPlaying) {
                StartMusic(player);
            }
            else {
                StopMusic();
            }
            return false;
        }

        /// <summary>
        /// 开始播放音乐
        /// </summary>
        private void StartMusic(Player triggerPlayer) {
            if (IsMusicPlaying) {
                return;
            }

            IsMusicPlaying = true;

            //播放开始音效
            SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.6f, Pitch = -0.3f }, Center);

            //设置全局活动八音盒
            SirenMusicalSystem.ActiveBoxPosition = Position;

            //绑定范围内的所有玩家
            BindNearbyPlayers();

            //在服务器上同步
            if (Main.netMode == NetmodeID.Server) {
                SendData();
            }
        }

        /// <summary>
        /// 停止播放音乐
        /// </summary>
        internal void StopMusic() {
            if (!IsMusicPlaying) {
                return;
            }

            IsMusicPlaying = false;

            //清除全局活动八音盒
            if (SirenMusicalSystem.ActiveBoxPosition == Position) {
                SirenMusicalSystem.ActiveBoxPosition = Point16.Zero;
            }

            //解除所有玩家的绑定
            UnbindAllPlayers();

            //在服务器上同步
            if (Main.netMode == NetmodeID.Server) {
                SendData();
            }
        }

        /// <summary>
        /// 绑定范围内的所有玩家
        /// </summary>
        private void BindNearbyPlayers() {
            activePlayers.Clear();

            foreach (Player player in Main.player) {
                if (!player.active || player.dead) {
                    continue;
                }

                float distance = Vector2.Distance(player.Center, Center);
                SirenMusicalBoxPlayer modPlayer = player.GetModPlayer<SirenMusicalBoxPlayer>();
                modPlayer.BindToBox(Position);
                activePlayers.Add(player.whoAmI);
            }
        }

        /// <summary>
        /// 解除所有玩家的绑定
        /// </summary>
        private void UnbindAllPlayers() {
            foreach (int playerIndex in activePlayers) {
                if (playerIndex < 0 || playerIndex >= Main.maxPlayers) {
                    continue;
                }

                Player player = Main.player[playerIndex];
                if (player != null && player.active) {
                    SirenMusicalBoxPlayer modPlayer = player.GetModPlayer<SirenMusicalBoxPlayer>();
                    modPlayer.ResetCurse();
                }
            }
            activePlayers.Clear();
        }

        /// <summary>
        /// 生成诡异音符粒子
        /// </summary>
        private void SpawnNoteParticles() {
            //为每个活跃玩家生成粒子
            foreach (int playerIndex in activePlayers) {
                if (playerIndex < 0 || playerIndex >= Main.maxPlayers) {
                    continue;
                }

                Player player = Main.player[playerIndex];
                if (player == null || !player.active || player.dead) {
                    continue;
                }

                //音符环绕效果 - 多层螺旋
                for (int layer = 0; layer < 2; layer++) {
                    float baseAngle = Main.GlobalTimeWrappedHourly * (2f + layer * 0.5f);
                    float angle = baseAngle + Main.rand.NextFloat(MathHelper.TwoPi);
                    float radius = 60f + layer * 40f + MathF.Sin(Main.GlobalTimeWrappedHourly * 3f + layer) * 15f;

                    Vector2 spawnPos = player.Center + angle.ToRotationVector2() * radius;
                    Vector2 velocity = (player.Center - spawnPos).SafeNormalize(Vector2.Zero).RotatedBy(MathHelper.PiOver2) * Main.rand.NextFloat(0.5f, 1.5f);

                    //音符粒子 - 使用更鲜艳的颜色
                    Color particleColor = Main.rand.Next(4) switch {
                        0 => new Color(186, 85, 211), //中紫罗兰色
                        1 => new Color(138, 43, 226), //蓝紫色
                        2 => new Color(147, 112, 219), //中紫色
                        _ => new Color(255, 0, 255)    //品红色
                    };

                    PRT_Light noteParticle = new PRT_Light(
                        spawnPos,
                        velocity,
                        Main.rand.NextFloat(0.2f, 0.8f),
                        particleColor,
                        Main.rand.Next(45, 75)
                    );
                    PRTLoader.AddParticle(noteParticle);
                }

                //追踪玩家的幽灵音符
                if (Main.rand.NextBool(2)) {
                    float offsetAngle = Main.rand.NextFloat(MathHelper.TwoPi);
                    Vector2 ghostPos = player.Center + offsetAngle.ToRotationVector2() * Main.rand.Next(100, 200);
                    Vector2 ghostVel = (player.Center - ghostPos).SafeNormalize(Vector2.Zero) * Main.rand.NextFloat(2f, 4f);

                    PRT_Light ghostNote = new PRT_Light(
                        ghostPos,
                        ghostVel,
                        Main.rand.NextFloat(0.5f, 0.75f),
                        Color.DarkViolet * 0.8f,
                        Main.rand.Next(30, 50)
                    );
                    PRTLoader.AddParticle(ghostNote);
                }

                //深色暗影尘埃 - 增加恐怖氛围
                if (Main.rand.NextBool(3)) {
                    for (int i = 0; i < 2; i++) {
                        float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                        float radius = Main.rand.NextFloat(40f, 120f);
                        Vector2 dustPos = player.Center + angle.ToRotationVector2() * radius;

                        Dust dust = Dust.NewDustDirect(dustPos, 0, 0, DustID.Shadowflame, 0f, 0f, 100, Color.Purple, Main.rand.NextFloat(1.5f, 2.5f));
                        dust.noGravity = true;
                        dust.velocity = (player.Center - dustPos).SafeNormalize(Vector2.Zero) * Main.rand.NextFloat(0.5f, 1.5f);
                        dust.fadeIn = Main.rand.NextFloat(0.8f, 1.4f);
                    }
                }

                //血红色警告粒子（音乐快结束时）
                SirenMusicalBoxPlayer modPlayer = player.GetModPlayer<SirenMusicalBoxPlayer>();
                if (modPlayer.MusicTimer >= SirenMusicalBoxPlayer.MusicDuration - 300) {
                    float dangerIntensity = (modPlayer.MusicTimer - (SirenMusicalBoxPlayer.MusicDuration - 300)) / 300f;

                    if (Main.rand.NextFloat() < dangerIntensity * 0.3f) {
                        Vector2 warnPos = player.Center + Main.rand.NextVector2Circular(60f, 60f);
                        Dust warnDust = Dust.NewDustDirect(warnPos, 0, 0, DustID.Blood, 0f, 0f, 100, Color.Red, Main.rand.NextFloat(2f, 3.5f));
                        warnDust.noGravity = true;
                        warnDust.velocity = Main.rand.NextVector2Circular(3f, 3f);
                    }
                }

                //海妖之眼效果 - 偶尔出现诡异的"眼睛"
                if (Main.rand.NextBool(120)) {
                    Vector2 eyePos = player.Center + Main.rand.NextVector2Circular(150f, 150f);

                    //眼睛外圈
                    for (int i = 0; i < 12; i++) {
                        float eyeAngle = MathHelper.TwoPi / 12f * i;
                        Vector2 pos = eyePos + eyeAngle.ToRotationVector2() * 15f;

                        Dust eyeDust = Dust.NewDustDirect(pos, 0, 0, DustID.DungeonWater, 0f, 0f, 100, Color.Cyan, 1.8f);
                        eyeDust.noGravity = true;
                        eyeDust.velocity = (eyePos - pos).SafeNormalize(Vector2.Zero) * 0.5f;
                    }

                    //眼睛瞳孔
                    Dust pupil = Dust.NewDustDirect(eyePos, 0, 0, DustID.Shadowflame, 0f, 0f, 100, Color.Red, 2.5f);
                    pupil.noGravity = true;
                }

                //扭曲的音波效果
                if (Main.rand.NextBool(10)) {
                    int waveCount = 20;
                    float waveRadius = Main.rand.NextFloat(80f, 150f);

                    for (int i = 0; i < waveCount; i++) {
                        float waveAngle = MathHelper.TwoPi / waveCount * i;
                        Vector2 wavePos = player.Center + waveAngle.ToRotationVector2() * waveRadius;
                        Vector2 waveVel = waveAngle.ToRotationVector2() * Main.rand.NextFloat(1f, 3f);

                        Color waveColor = Color.Lerp(Color.Purple, Color.Cyan, Main.rand.NextFloat());
                        PRT_Light wave = new PRT_Light(wavePos, waveVel, 0.52f, waveColor * 0.6f, Main.rand.Next(20, 40));
                        PRTLoader.AddParticle(wave);
                    }
                }

                //玩家脚下的深渊涟漪
                if (Main.rand.NextBool(15)) {
                    Vector2 groundPos = new Vector2(player.Center.X, player.Bottom.Y);

                    for (int i = 0; i < 3; i++) {
                        int rippleCount = 16;
                        float rippleRadius = 30f + i * 25f;

                        for (int j = 0; j < rippleCount; j++) {
                            float rippleAngle = MathHelper.TwoPi / rippleCount * j;
                            Vector2 ripplePos = groundPos + rippleAngle.ToRotationVector2() * rippleRadius;

                            Dust ripple = Dust.NewDustDirect(ripplePos, 0, 0, DustID.DungeonWater, 0f, -1f, 100,
                                Color.DarkBlue * (1f - i * 0.3f), 1.5f - i * 0.3f);
                            ripple.noGravity = true;
                            ripple.fadeIn = 0.8f;
                        }
                    }
                }
            }
        }

        public override void Update() {
            if (!IsMusicPlaying) {
                return;
            }

            particleTimer++;

            //生成粒子效果
            if (particleTimer >= ParticleSpawnRate) {
                if (Main.rand.NextBool(4)) {
                    SirenMusicalBoxPlayer.Newphonogram(Center);
                }

                particleTimer = 0;
                SpawnNoteParticles();
            }

            //检查并更新活跃玩家列表
            for (int i = activePlayers.Count - 1; i >= 0; i--) {
                int playerIndex = activePlayers[i];
                if (playerIndex < 0 || playerIndex >= Main.maxPlayers) {
                    activePlayers.RemoveAt(i);
                    continue;
                }

                Player player = Main.player[playerIndex];
                if (player == null || !player.active || player.dead) {
                    activePlayers.RemoveAt(i);
                    continue;
                }
            }

            //如果没有活跃玩家，停止音乐
            if (activePlayers.Count == 0) {
                StopMusic();
            }

            //添加光照效果
            Lighting.AddLight(Center, new Color(139, 0, 139).ToVector3() * (MathF.Sin(Main.GlobalTimeWrappedHourly * 3f) * 0.5f + 0.5f));
        }

        public override void SendData(ModPacket data) {
            data.Write(IsMusicPlaying);
            data.Write(activePlayers.Count);
            foreach (int playerIndex in activePlayers) {
                data.Write((byte)playerIndex);
            }
        }

        public override void ReceiveData(BinaryReader reader, int whoAmI) {
            IsMusicPlaying = reader.ReadBoolean();

            activePlayers.Clear();
            int count = reader.ReadInt32();
            for (int i = 0; i < count; i++) {
                activePlayers.Add(reader.ReadByte());
            }

            //更新全局状态
            if (IsMusicPlaying) {
                SirenMusicalSystem.ActiveBoxPosition = Position;
            }
            else if (SirenMusicalSystem.ActiveBoxPosition == Position) {
                SirenMusicalSystem.ActiveBoxPosition = Point16.Zero;
            }
        }
    }
}
