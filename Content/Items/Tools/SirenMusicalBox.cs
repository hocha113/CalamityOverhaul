using CalamityOverhaul.Content.LegendWeapon.HalibutLegend.Resurrections;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using InnoVault.TileProcessors;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.Enums;
using Terraria.GameContent.ObjectInteractions;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace CalamityOverhaul.Content.Items.Tools
{
    /// <summary>
    /// 海妖八音盒物品
    /// </summary>
    internal class SirenMusicalBox : ModItem
    {
        public override string Texture => CWRConstant.Item + "Tools/SirenMusicalBox";

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

    internal class SirenMusicalSystem : ModSystem
    {
        public override void PostUpdateEverything() {
            if (Main.LocalPlayer.GetModPlayer<SirenMusicalBoxPlayer>().IsMusicPlaying) {
                Main.newMusic = Main.musicBox2 = MusicLoader.GetMusicSlot("CalamityOverhaul/Assets/Sounds/Music/SirenMusic");
            }           
        }
    }

    internal class SirenMusicalBoxPlayer : ModPlayer
    {
        public bool IsMusicPlaying;
        public override bool PreKill(double damage, int hitDirection, bool pvp, ref bool playSound, ref bool genDust, ref PlayerDeathReason damageSource) {
            return base.PreKill(damage, hitDirection, pvp, ref playSound, ref genDust, ref damageSource);
        }
    }

    /// <summary>
    /// 海妖八音盒处理器，控制音乐播放和死亡效果
    /// </summary>
    internal class SirenMusicalBoxTP : TileProcessor, ICWRLoader
    {
        public override int TargetTileID => ModContent.TileType<SirenMusicalBoxTile>();
        public Vector2 Center => PosInWorld + new Vector2(SirenMusicalBoxTile.Width * 18, SirenMusicalBoxTile.Height * 18) / 2;
        Player player;
        //音乐相关
        public bool IsMusicPlaying;
        public int MusicTimer;
        public const int MusicDuration = 60 * 23; //23秒音乐

        //粒子效果
        private int particleTimer;
        private const int ParticleSpawnRate = 5;

        //无敌状态
        private bool wasInvincible;

        public override void SetProperty() => LoadenWorldSendData = false;

        public override void OnKill() {
            if (IsMusicPlaying) {
                StopMusic();
            }
            if (VaultUtils.isClient) {
                SendData();
            }
        }

        public override bool? RightClick(int i, int j, Tile tile, Player player) {//此右键函数会自动同步操作
            if (!IsMusicPlaying) {
                StartMusic();
            }
            else {
                StopMusic();
            }
            return false;
        }

        /// <summary>
        /// 开始播放音乐
        /// </summary>
        private void StartMusic() {
            if (IsMusicPlaying) return;

            IsMusicPlaying = true;
            MusicTimer = 0;
            
            //播放开始音效
            SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.6f, Pitch = -0.3f }, Center);
            
            //查找附近玩家并授予无敌
            if (player != null) {
                MakePlayerInvincible(player, true);
            }
        }

        /// <summary>
        /// 停止播放音乐
        /// </summary>
        private void StopMusic() {
            if (!IsMusicPlaying) return;

            IsMusicPlaying = false;
            MusicTimer = 0;
            
            //移除玩家无敌状态
            if (player != null) {
                MakePlayerInvincible(player, false);
            }
        }

        /// <summary>
        /// 使玩家无敌或移除无敌
        /// </summary>
        private void MakePlayerInvincible(Player player, bool invincible) {
            if (invincible) {
                wasInvincible = player.immune;
                player.immune = true;
                player.immuneTime = 999999;
            } else {
                player.immune = wasInvincible;
                player.immuneTime = 0;
            }
        }

        ///<summary>
        ///生成诡异音符粒子
        ///</summary>
        private void SpawnNoteParticles() {
            if (player == null) return;

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

        ///<summary>
        ///音乐结束时杀死玩家
        ///</summary>
        private void KillPlayer() {
            if (player == null || player.dead) return;

            //移除无敌
            MakePlayerInvincible(player, false);

            //播放恐怖音效
            SoundEngine.PlaySound(SoundID.NPCDeath59 with { Volume = 0.9f, Pitch = -0.8f }, player.Center);
            SoundEngine.PlaySound(SoundID.Zombie103 with { Volume = 0.7f, Pitch = -0.6f }, player.Center);

            //死亡演出特效
            DeathPerformance(player);

            //延迟执行死亡
            player.immune = false;
            player.immuneTime = 0;

            //使用深渊的死亡原因
            PlayerDeathReason damageSource = PlayerDeathReason.ByCustomReason(
                ResurrectionDeath.DeathText.ToNetworkText(player.name)
            );

            //直接杀死玩家
            player.KillMe(damageSource, player.statLifeMax2 * 10, 0, false);
            
            //停止音乐
            StopMusic();
        }

        ///<summary>
        ///死亡演出效果
        ///</summary>
        private void DeathPerformance(Player player) {
            //大量暗影粒子爆发
            for (int i = 0; i < 100; i++) {
                Vector2 velocity = Main.rand.NextVector2CircularEdge(8f, 8f);
                Dust dust = Dust.NewDustDirect(player.Center, 0, 0, DustID.Shadowflame, velocity.X, velocity.Y, 100, Color.DarkMagenta, Main.rand.NextFloat(2f, 3.5f));
                dust.noGravity = true;
            }

            //黑暗能量环
            for (int i = 0; i < 3; i++) {
                for (int j = 0; j < 20; j++) {
                    float angle = MathHelper.TwoPi / 20f * j;
                    float radius = 50f + i * 30f;
                    Vector2 pos = player.Center + angle.ToRotationVector2() * radius;
                    Vector2 vel = (pos - player.Center).SafeNormalize(Vector2.Zero) * Main.rand.NextFloat(3f, 6f);
                    
                    PRT_Light particle = new PRT_Light(pos, vel, Main.rand.NextFloat(1.2f, 1.8f), Color.Purple, Main.rand.Next(20, 40));
                    PRTLoader.AddParticle(particle);
                }
            }
        }

        public override void Update() {
            if (!IsMusicPlaying) return;

            MusicTimer++;
            particleTimer++;

            //生成粒子效果
            if (particleTimer >= ParticleSpawnRate) {
                particleTimer = 0;
                SpawnNoteParticles();
            }

            //持续给予玩家无敌
            if (player != null && !player.dead) {
                player.immune = true;
                player.immuneTime = 999999;
            }

            //音乐快结束时的警告
            if (MusicTimer >= MusicDuration - 120 && MusicTimer % 20 == 0) {
                SoundEngine.PlaySound(SoundID.NPCHit54 with { Volume = 0.4f, Pitch = -0.5f }, Center);
            }

            //音乐结束，执行死亡
            if (MusicTimer >= MusicDuration) {
                KillPlayer();
            }

            //添加光照效果
            Lighting.AddLight(Center, new Color(139, 0, 139).ToVector3() * (MathF.Sin(Main.GlobalTimeWrappedHourly * 3f) * 0.5f + 0.5f));
        }

        public override void SendData(ModPacket data) {
            data.Write(IsMusicPlaying);
            data.Write(MusicTimer);
        }

        public override void ReceiveData(BinaryReader reader, int whoAmI) {
            IsMusicPlaying = reader.ReadBoolean();
            MusicTimer = reader.ReadInt32();
        }
    }
}
