using CalamityOverhaul.Content.LegendWeapon.HalibutLegend.Resurrections;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using InnoVault.TileProcessors;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
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
            Item.value = Terraria.Item.buyPrice(0, 5, 0, 0);
            Item.rare = ItemRarityID.Red;
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

                //给予无敌状态
                if (!Player.dead) {
                    Player.immune = true;
                    Player.immuneTime = 999999;
                    Player.immuneNoBlink = true;
                }

                //音乐结束，执行死亡
                if (MusicTimer >= MusicDuration) {
                    ExecuteDeath();
                }
            }
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
            //大量暗影粒子爆发
            for (int i = 0; i < 100; i++) {
                Vector2 velocity = Main.rand.NextVector2CircularEdge(8f, 8f);
                Dust dust = Dust.NewDustDirect(Player.Center, 0, 0, DustID.Shadowflame, velocity.X, velocity.Y, 100, Color.DarkMagenta, Main.rand.NextFloat(2f, 3.5f));
                dust.noGravity = true;
            }

            //黑暗能量环
            for (int i = 0; i < 3; i++) {
                for (int j = 0; j < 20; j++) {
                    float angle = MathHelper.TwoPi / 20f * j;
                    float radius = 50f + i * 30f;
                    Vector2 pos = Player.Center + angle.ToRotationVector2() * radius;
                    Vector2 vel = (pos - Player.Center).SafeNormalize(Vector2.Zero) * Main.rand.NextFloat(3f, 6f);

                    PRT_Light particle = new PRT_Light(pos, vel, Main.rand.NextFloat(1.2f, 1.8f), Color.Purple, Main.rand.Next(20, 40));
                    PRTLoader.AddParticle(particle);
                }
            }

            //海妖触手效果（参考深渊死亡系统）
            for (int i = 0; i < 6; i++) {
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                Vector2 tentaclePos = Player.Center + angle.ToRotationVector2() * 200f;

                for (int j = 0; j < 15; j++) {
                    Vector2 pos = Vector2.Lerp(tentaclePos, Player.Center, j / 15f);
                    Dust dust = Dust.NewDustDirect(pos, 0, 0, DustID.DungeonWater, 0f, 0f, 100, Color.DarkBlue, 2f);
                    dust.velocity = Main.rand.NextVector2Circular(2f, 2f);
                    dust.noGravity = true;
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
        }

        public override void LoadData(TagCompound tag) {
            IsCursed = tag.GetBool("IsCursed");
            MusicTimer = tag.GetInt("MusicTimer");
            if (tag.TryGet("BoundBoxX", out short x) && tag.TryGet("BoundBoxY", out short y)) {
                BoundBoxPosition = new Point16(x, y);
            }
        }

        public override bool PreKill(double damage, int hitDirection, bool pvp, ref bool playSound, ref bool genDust, ref PlayerDeathReason damageSource) {
            if (IsCursed) {
                return false;
            }
            return base.PreKill(damage, hitDirection, pvp, ref playSound, ref genDust, ref damageSource);
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
        private void StopMusic() {
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

                //在玩家周围生成环绕的音符
                float angle = Main.GlobalTimeWrappedHourly * 2f + Main.rand.NextFloat(MathHelper.TwoPi);
                float radius = 80f + MathF.Sin(Main.GlobalTimeWrappedHourly * 3f) * 20f;

                Vector2 spawnPos = player.Center + angle.ToRotationVector2() * radius;
                Vector2 velocity = new Vector2(0, -Main.rand.NextFloat(1f, 2f)).RotatedByRandom(0.5f);

                //创建音符粒子
                PRT_Light noteParticle = new PRT_Light(
                    spawnPos,
                    velocity,
                    Main.rand.NextFloat(0.8f, 1.2f),
                    Main.rand.NextBool() ? Color.Purple : Color.DarkMagenta,
                    Main.rand.Next(30, 60)
                );
                PRTLoader.AddParticle(noteParticle);

                //额外的诡异粒子
                if (Main.rand.NextBool(3)) {
                    Dust dust = Dust.NewDustDirect(spawnPos, 0, 0, DustID.Shadowflame, 0f, 0f, 100, Color.Purple, Main.rand.NextFloat(1f, 1.5f));
                    dust.noGravity = true;
                    dust.velocity = velocity * 0.5f;
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
