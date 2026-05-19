using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.HalibutLegend;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.Actors;
using InnoVault.GameSystem;
using InnoVault.PRT;
using InnoVault.TileProcessors;
using Microsoft.Xna.Framework.Graphics;
using System;
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
    #region 物品与物块

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
            AnimationFrameHeight = 36;

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

        public override bool CreateDust(int i, int j, ref int type) {
            type = Main.rand.NextBool(2) ? DustID.Water : DustID.SilverCoin;
            return true;
        }

        public override void NumDust(int i, int j, bool fail, ref int num) => num = fail ? 1 : 3;

        public override void KillMultiTile(int i, int j, int frameX, int frameY) {
            for (int z = 0; z < 13; z++) {
                Dust.NewDust(new Vector2(i * 16, j * 16), Width * 16, Height * 16, DustID.SilverCoin);
            }
        }

        public override bool PreDraw(int i, int j, SpriteBatch spriteBatch) {
            Tile t = Main.tile[i, j];
            int frameXPos = t.TileFrameX;
            int frameYPos = t.TileFrameY;

            bool isPlaying = false;
            if (VaultUtils.SafeGetTopLeft(i, j, out var point)) {
                isPlaying = SirenGhostActor.TryFindByBoxPosition(point, out _);
            }

            frameYPos += (isPlaying ? 1 : 0) * (Height * 18);

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
                if (SirenGhostActor.TryFindByBoxPosition(point, out _)) {
                    float pulse = MathF.Sin(Main.GlobalTimeWrappedHourly * 3f) * 0.3f + 0.7f;
                    r = 0.5f * pulse;
                    g = 0f;
                    b = 0.5f * pulse;
                }
            }
        }
    }

    #endregion

    #region 系统与玩家

    /// <summary>
    /// 负责全局音乐覆盖，不再持有可变全局状态
    /// </summary>
    internal class SirenMusicalSystem : ModSystem
    {
        public override void PostUpdateEverything() {
            if (Main.dedServ) {
                return;
            }

            if (SirenGhostActor.TryGetActiveSession(out _)) {
                Main.newMusic = Main.musicBox2 = MusicLoader.GetMusicSlot("CalamityOverhaul/Assets/Sounds/Music/SirenMusic");
            }
        }
    }

    /// <summary>
    /// 反射级别的死亡拦截，优先级在所有ModPlayer之前。
    /// 使用每实例标记代替静态字段，多人模式下每个玩家拥有独立状态
    /// </summary>
    internal class SirenMusicalBoxPlayerDeath : PlayerOverride
    {
        /// <summary>
        /// 当音乐终结执行死亡时置为 true，同帧内使 On_PreKill 放行
        /// </summary>
        public bool MusicHasEnded;

        public override void ResetEffects() {
            MusicHasEnded = false;
        }

        public override bool? On_PreKill(double damage, int hitDirection, bool pvp,
            ref bool playSound, ref bool genDust, ref PlayerDeathReason damageSource) {
            //音乐终结触发的死亡，强制放行
            if (MusicHasEnded) {
                return true;
            }
            if (Player.GetModPlayer<SirenMusicalBoxPlayer>().IsCursed) {
                //厉鬼复苏的死亡无法阻挡
                if (Player.TryGetOverride<HalibutPlayer>(out var halibutPlayer)
                    && halibutPlayer.ResurrectionSystem.Ratio == 1f) {
                    return true;
                }
                Player.statLife = (int)MathHelper.Clamp(Player.statLife, 1, Player.statLifeMax2);
                return false;
            }
            return null;
        }
    }

    /// <summary>
    /// 跟踪玩家与海妖八音盒的交互状态。
    /// IsCursed 每帧从 TileProcessor 状态派生，无需独立网络同步
    /// </summary>
    internal class SirenMusicalBoxPlayer : ModPlayer
    {
        /// <summary>
        /// 是否处于八音盒诅咒状态（由 PreUpdate 从 TP 状态派生）
        /// </summary>
        public bool IsCursed;

        /// <summary>
        /// 玩家是否曾经通过钓鱼获得过八音盒（首次保底逻辑用）
        /// </summary>
        public bool HasSirenMusicalBox;

        /// <summary>
        /// 当前正在播放的海妖会话 Actor（仅当前帧有效）
        /// </summary>
        private SirenGhostActor activeActor;

        /// <summary>
        /// 音乐持续时间（23秒）
        /// </summary>
        public const int MusicDuration = 60 * 23;

        private int particleTimer;

        /// <summary>
        /// 根据当前帧的 TP 状态查找正在播放的八音盒
        /// </summary>
        public override void PreUpdate() {
            SirenGhostActor.TryGetActiveSession(out activeActor);
            IsCursed = activeActor != null;
        }

        public override void PostUpdate() {
            if (!IsCursed || activeActor == null) {
                particleTimer = 0;
                return;
            }

            //服务端广播终结状态后，仅本地玩家执行死亡
            if (activeActor.ResolveDeath && Player.whoAmI == Main.myPlayer) {
                ExecuteDeath(false);
            }

            //客户端粒子效果
            if (Main.dedServ) {
                return;
            }

            particleTimer++;
            if (particleTimer % 5 == 0) {
                SpawnCurseParticles();
            }
        }

        /// <summary>
        /// 在玩家周围生成诅咒视觉效果
        /// </summary>
        private void SpawnCurseParticles() {
            if (!Player.Alives() || activeActor == null) {
                return;
            }

            float timerRatio = activeActor.MusicTimer / (float)MusicDuration;

            //音符环绕效果
            for (int layer = 0; layer < 2; layer++) {
                float baseAngle = Main.GlobalTimeWrappedHourly * (2f + layer * 0.5f);
                float angle = baseAngle + Main.rand.NextFloat(MathHelper.TwoPi);
                float radius = 60f + layer * 40f + MathF.Sin(Main.GlobalTimeWrappedHourly * 3f + layer) * 15f;

                Vector2 spawnPos = Player.Center + angle.ToRotationVector2() * radius;
                Vector2 velocity = (Player.Center - spawnPos).SafeNormalize(Vector2.Zero).RotatedBy(MathHelper.PiOver2) * Main.rand.NextFloat(0.5f, 1.5f);

                Color particleColor = Main.rand.Next(4) switch {
                    0 => new Color(186, 85, 211),
                    1 => new Color(138, 43, 226),
                    2 => new Color(147, 112, 219),
                    _ => new Color(255, 0, 255)
                };

                PRTLoader.NewParticle<PRT_Note>(spawnPos, velocity, particleColor, Main.rand.NextFloat(0.3f, 0.6f)).Configure(Main.rand.Next(45, 75), Main.rand.Next(3));
            }

            //追踪玩家的幽灵音符
            if (Main.rand.NextBool(2)) {
                float offsetAngle = Main.rand.NextFloat(MathHelper.TwoPi);
                Vector2 ghostPos = Player.Center + offsetAngle.ToRotationVector2() * Main.rand.Next(100, 200);
                Vector2 ghostVel = (Player.Center - ghostPos).SafeNormalize(Vector2.Zero) * Main.rand.NextFloat(2f, 4f);

                PRTLoader.NewParticle<PRT_Note>(ghostPos, ghostVel, Color.DarkViolet * 0.8f, Main.rand.NextFloat(0.5f, 0.75f)).Configure(Main.rand.Next(30, 50), Main.rand.Next(3));
            }

            //暗影尘埃
            if (Main.rand.NextBool(3)) {
                for (int i = 0; i < 2; i++) {
                    float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                    float radius = Main.rand.NextFloat(40f, 120f);
                    Vector2 dustPos = Player.Center + angle.ToRotationVector2() * radius;

                    Dust dust = Dust.NewDustDirect(dustPos, 0, 0, DustID.Shadowflame, 0f, 0f, 100, Color.Purple, Main.rand.NextFloat(1.5f, 2.5f));
                    dust.noGravity = true;
                    dust.velocity = (Player.Center - dustPos).SafeNormalize(Vector2.Zero) * Main.rand.NextFloat(0.5f, 1.5f);
                    dust.fadeIn = Main.rand.NextFloat(0.8f, 1.4f);
                }
            }

            //血红色警告粒子（最后5秒）
            if (timerRatio > 0.78f) {
                float dangerIntensity = (timerRatio - 0.78f) / 0.22f;

                if (Main.rand.NextFloat() < dangerIntensity * 0.3f) {
                    Vector2 warnPos = Player.Center + Main.rand.NextVector2Circular(60f, 60f);
                    Dust warnDust = Dust.NewDustDirect(warnPos, 0, 0, DustID.Blood, 0f, 0f, 100, Color.Red, Main.rand.NextFloat(2f, 3.5f));
                    warnDust.noGravity = true;
                    warnDust.velocity = Main.rand.NextVector2Circular(3f, 3f);
                }

                if (Main.rand.NextBool(2)) {
                    float panicAngle = Main.rand.NextFloat(MathHelper.TwoPi);
                    Vector2 panicPos = Player.Center + panicAngle.ToRotationVector2() * Main.rand.NextFloat(40f, 80f);

                    PRTLoader.NewParticle<PRT_Note>(panicPos, Main.rand.NextVector2Circular(2f, 2f), Color.Lerp(Color.Red, Color.DarkMagenta, Main.rand.NextFloat()), Main.rand.NextFloat(0.4f, 0.7f)).Configure(Main.rand.Next(20, 40), Main.rand.Next(3));
                }
            }

            //音符雨
            if (Main.rand.NextBool(8)) {
                Vector2 fallPos = Player.Center + new Vector2(Main.rand.NextFloat(-200f, 200f), -Main.rand.NextFloat(150f, 250f));
                Color fallColor = Main.rand.Next(3) switch {
                    0 => Color.Purple,
                    1 => Color.Violet,
                    _ => Color.Magenta
                };
                PRTLoader.NewParticle<PRT_Note>(fallPos, new Vector2(Main.rand.NextFloat(-1f, 1f), Main.rand.NextFloat(1f, 3f)), fallColor * 0.8f, Main.rand.NextFloat(0.4f, 0.8f)).Configure(Main.rand.Next(60, 90), Main.rand.Next(3));
            }
        }

        /// <summary>
        /// 执行玩家死亡（仅在本地玩家上调用）
        /// </summary>
        internal void ExecuteDeath(bool stopMusicBoxes) {
            if (Player.dead) {
                return;
            }

            //设置死亡放行标记（同帧内 On_PreKill 检查此标记）
            if (Player.TryGetOverride<SirenMusicalBoxPlayerDeath>(out var deathOverride)) {
                deathOverride.MusicHasEnded = true;
            }

            Player.immune = false;
            Player.immuneTime = 0;
            Player.immuneNoBlink = false;

            SoundEngine.PlaySound(SoundID.NPCDeath59 with { Volume = 0.9f, Pitch = -0.8f }, Player.Center);
            SoundEngine.PlaySound(SoundID.Zombie103 with { Volume = 0.7f, Pitch = -0.6f }, Player.Center);

            if (!Main.dedServ) {
                SpawnDeathEffects();
            }

            PlayerDeathReason damageSource = PlayerDeathReason.ByCustomReason(
                SirenMusicalBox.DeathText.ToNetworkText(Player.name)
            );

            Player.KillMe(damageSource, Player.statLifeMax2 * 10, 0, false);

            if (stopMusicBoxes) {
                StopAllMusicBoxes();
            }
        }

        /// <summary>
        /// 停止所有正在播放的八音盒
        /// </summary>
        internal static void StopAllMusicBoxes() {
            if (Main.netMode == NetmodeID.MultiplayerClient) {
                if (SirenGhostActor.TryGetActiveSession(out var actor)) {
                    SirenMusicalBoxTP.RequestToggle(actor.BoxPosition);
                }
                return;
            }

            var actors = ActorLoader.GetActiveActors<SirenGhostActor>();
            foreach (var actor in actors) {
                actor.StopSession();
            }
        }

        /// <summary>
        /// 生成死亡特效
        /// </summary>
        private void SpawnDeathEffects() {
            //暗影爆发
            for (int i = 0; i < 150; i++) {
                Vector2 velocity = Main.rand.NextVector2CircularEdge(12f, 12f) * Main.rand.NextFloat(0.6f, 1.4f);
                Dust dust = Dust.NewDustDirect(Player.Center, 0, 0, DustID.Shadowflame, velocity.X, velocity.Y, 100,
                    Main.rand.NextBool() ? Color.DarkMagenta : Color.Purple, Main.rand.NextFloat(2.5f, 4f));
                dust.noGravity = true;
                dust.fadeIn = 1.5f;
            }

            //灵魂碎片
            for (int i = 0; i < 60; i++) {
                float angle = MathHelper.TwoPi / 60f * i;
                float radius = Main.rand.NextFloat(20f, 60f);
                Vector2 pos = Player.Center + angle.ToRotationVector2() * radius;
                Vector2 vel = new Vector2(0, -Main.rand.NextFloat(3f, 6f)).RotatedBy(angle * 0.3f);
                PRTLoader.NewParticle<PRT_Light>(pos, vel, Color.Cyan * 0.8f, Main.rand.NextFloat(0.5f, 0.75f)).Configure(Main.rand.Next(40, 70));
            }

            //多层光环
            for (int layer = 0; layer < 5; layer++) {
                int particlesPerRing = 24;
                float ringRadius = 40f + layer * 35f;
                for (int j = 0; j < particlesPerRing; j++) {
                    float angle = MathHelper.TwoPi / particlesPerRing * j;
                    Vector2 pos = Player.Center + angle.ToRotationVector2() * ringRadius;
                    Vector2 vel = angle.ToRotationVector2() * Main.rand.NextFloat(4f, 8f);
                    Color ringColor = Color.Lerp(Color.Purple, Color.DarkRed, layer / 5f);
                    PRTLoader.NewParticle<PRT_Light>(pos, vel, ringColor, Main.rand.NextFloat(0.5f, 0.75f)).Configure(Main.rand.Next(30, 60));
                }
            }

            //触手轨迹
            for (int i = 0; i < 10; i++) {
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                Vector2 tentacleStart = Player.Center + angle.ToRotationVector2() * Main.rand.NextFloat(250f, 350f);

                int segmentCount = Main.rand.Next(20, 30);
                for (int j = 0; j < segmentCount; j++) {
                    float progress = j / (float)segmentCount;
                    Vector2 pos = Vector2.Lerp(tentacleStart, Player.Center, CWRUtils.EaseOutCubic(progress));
                    float waveOffset = MathF.Sin(progress * MathHelper.Pi * 3f + i) * 20f;
                    Vector2 perpendicular = Vector2.Normalize(new Vector2(-(tentacleStart.Y - Player.Center.Y), tentacleStart.X - Player.Center.X));
                    pos += perpendicular * waveOffset;

                    Dust tentacle = Dust.NewDustDirect(pos, 0, 0, DustID.DungeonWater, 0f, 0f, 100,
                        Color.Lerp(Color.DarkBlue, Color.Cyan, progress), Main.rand.NextFloat(2.5f, 4f) * (1f - progress * 0.5f));
                    tentacle.noGravity = true;
                    tentacle.velocity = Main.rand.NextVector2Circular(1f, 1f);

                    if (j % 3 == 0) {
                        PRTLoader.NewParticle<PRT_Light>(pos, Main.rand.NextVector2Circular(2f, 2f),
                            Color.Cyan * 0.6f, Main.rand.NextFloat(0.31f, 0.5f)).Configure(Main.rand.Next(20, 40));
                    }
                }

                for (int k = 0; k < 8; k++) {
                    Vector2 burstVel = Main.rand.NextVector2CircularEdge(5f, 5f);
                    Dust burst = Dust.NewDustDirect(Player.Center, 0, 0, DustID.Blood, burstVel.X, burstVel.Y, 100,
                        Color.DarkRed, Main.rand.NextFloat(2.5f, 3.5f));
                    burst.noGravity = true;
                }
            }

            //海妖之眼
            int eyeCount = Main.rand.Next(6, 10);
            for (int i = 0; i < eyeCount; i++) {
                float angle = MathHelper.TwoPi / eyeCount * i + Main.rand.NextFloat(-0.2f, 0.2f);
                Vector2 eyePos = Player.Center + angle.ToRotationVector2() * Main.rand.NextFloat(150f, 250f);

                for (int j = 0; j < 16; j++) {
                    float eyeAngle = MathHelper.TwoPi / 16f * j;
                    Vector2 scleraPos = eyePos + eyeAngle.ToRotationVector2() * 20f;
                    Dust sclera = Dust.NewDustDirect(scleraPos, 0, 0, DustID.Ice, 0f, 0f, 100, Color.White, 2f);
                    sclera.noGravity = true;
                    sclera.velocity = (eyePos - scleraPos).SafeNormalize(Vector2.Zero) * Main.rand.NextFloat(0.3f, 0.8f);
                    sclera.fadeIn = 1.2f;
                }
                for (int j = 0; j < 12; j++) {
                    float irisAngle = MathHelper.TwoPi / 12f * j;
                    Vector2 irisPos = eyePos + irisAngle.ToRotationVector2() * 12f;
                    Dust iris = Dust.NewDustDirect(irisPos, 0, 0, DustID.DungeonWater, 0f, 0f, 100, Color.Cyan, 1.8f);
                    iris.noGravity = true;
                    iris.velocity = (eyePos - irisPos).SafeNormalize(Vector2.Zero) * 0.5f;
                }
                for (int j = 0; j < 8; j++) {
                    Vector2 pupilOffset = Main.rand.NextVector2Circular(5f, 5f);
                    Dust pupil = Dust.NewDustDirect(eyePos + pupilOffset, 0, 0, DustID.Blood, 0f, 0f, 100, Color.DarkRed, 2.5f);
                    pupil.noGravity = true;
                    pupil.velocity = -pupilOffset * 0.1f;
                }

                Vector2 gazeDirection = (Player.Center - eyePos).SafeNormalize(Vector2.Zero);
                for (int j = 0; j < 30; j++) {
                    Vector2 gazePos = eyePos + gazeDirection * (j * 8f);
                    PRTLoader.NewParticle<PRT_Light>(gazePos, gazeDirection * 0.5f,
                        Color.Red * 0.6f, Main.rand.NextFloat(0.6f, 0.83f)).Configure(Main.rand.Next(15, 30));
                }
            }

            //血雾
            for (int i = 0; i < 200; i++) {
                Vector2 mistVel = Main.rand.NextVector2Unit() * Main.rand.NextFloat(3f, 15f);
                Dust mist = Dust.NewDustDirect(Player.Center, 0, 0, DustID.Blood, mistVel.X, mistVel.Y, 100,
                    Color.DarkRed, Main.rand.NextFloat(1.5f, 3f));
                mist.noGravity = true;
                mist.fadeIn = Main.rand.NextFloat(0.8f, 1.5f);
            }

            //暗影漩涡
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

            //灵魂上升
            for (int i = 0; i < 80; i++) {
                Vector2 pos = Player.Center + Main.rand.NextVector2Circular(40f, 40f);
                Vector2 vel = new Vector2(Main.rand.NextFloat(-2f, 2f), -Main.rand.NextFloat(6f, 12f));
                Color soulColor = Main.rand.Next(3) switch {
                    0 => Color.White,
                    1 => Color.Cyan,
                    _ => Color.Purple
                };
                PRTLoader.NewParticle<PRT_Light>(pos, vel, soulColor * 0.7f, Main.rand.NextFloat(0.35f, 0.55f)).Configure(Main.rand.Next(60, 100));
            }

            //空间裂纹
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

        public override void OnRespawn() {
            IsCursed = false;
        }

        public override void SaveData(TagCompound tag) {
            tag["HasSirenMusicalBox"] = HasSirenMusicalBox;
        }

        public override void LoadData(TagCompound tag) {
            if (tag.TryGet("HasSirenMusicalBox", out bool value)) {
                HasSirenMusicalBox = value;
            }
        }

        public override void CatchFish(FishingAttempt attempt, ref int itemDrop, ref int npcSpawn, ref AdvancedPopupRequest sonar, ref Vector2 sonarPosition) {
            if (attempt.inHoney || attempt.inLava) {
                return;
            }

            if (HasSirenMusicalBox) {
                if (Main.rand.NextBool(800)) {
                    itemDrop = ModContent.ItemType<SirenMusicalBox>();
                }
            }
            else {
                itemDrop = ModContent.ItemType<SirenMusicalBox>();
                HasSirenMusicalBox = true;
            }
        }

        /// <summary>
        /// 生成音符Gore
        /// </summary>
        public static void SpawnMusicNoteGore(Vector2 position) {
            int goreType = Main.rand.Next(570, 573);
            float wind = Main.WindForVisuals * 2f;
            if (goreType == 572) position.X -= 8f;
            else if (goreType == 571) position.X -= 4f;
            Vector2 velocity = new(
                wind * (1f + Main.rand.NextFloat(-1.5f, 1.5f)),
                -0.5f * (1f + Main.rand.NextFloat(-0.5f, 0.5f))
            );
            Gore.NewGore(new EntitySource_TileUpdate((int)position.X, (int)position.Y), position, velocity, goreType, 0.8f);
        }
    }

    #endregion

    #region TileProcessor

    /// <summary>
    /// 海妖八音盒的核心状态管理器。
    /// 计时器在此处维护并通过网络同步，确保所有客户端状态一致
    /// </summary>
    internal class SirenMusicalBoxTP : TileProcessor, ICWRLoader
    {
        public override int TargetTileID => ModContent.TileType<SirenMusicalBoxTile>();
        public Vector2 Center => PosInWorld + new Vector2(SirenMusicalBoxTile.Width * 8, SirenMusicalBoxTile.Height * 8);

        internal static bool TryFindMatchingTP(Point16 position, out SirenMusicalBoxTP boxTP) {
            if (TileProcessorLoader.ByPositionGetTP(position, out SirenMusicalBoxTP targetTP) && targetTP.Active) {
                boxTP = targetTP;
                return true;
            }

            boxTP = null;
            return false;
        }

        internal static void RequestToggle(Point16 position) {
            if (Main.netMode == NetmodeID.SinglePlayer) {
                if (TryFindMatchingTP(position, out var singlePlayerTP)) {
                    singlePlayerTP.HandleToggleRequest(Main.LocalPlayer);
                }
                return;
            }

            ModPacket packet = CWRMod.Instance.GetPacket();
            packet.Write((byte)CWRMessageType.SirenMusicalBoxToggle);
            packet.Write(position.X);
            packet.Write(position.Y);
            packet.Send();
        }

        internal static void HandleTogglePacket(BinaryReader reader, int whoAmI) {
            Point16 position = new(reader.ReadInt16(), reader.ReadInt16());
            if (!TryFindMatchingTP(position, out var boxTP)) {
                return;
            }

            Player player = Main.player[whoAmI];
            if (player is null || !player.active || player.dead) {
                return;
            }

            boxTP.HandleToggleRequest(player);
        }

        private void HandleToggleRequest(Player player) {
            if (player.GetModPlayer<SirenMusicalBoxPlayer>().IsCursed) {
                return;
            }

            if (SirenGhostActor.TryFindByBoxPosition(Position, out var actor)) {
                actor.StopSession();
                return;
            }

            if (SirenGhostActor.TryGetActiveSession(out _)) {
                return;
            }

            StartMusic();
        }

        public override void OnKill() {
            if (Main.netMode == NetmodeID.MultiplayerClient) {
                return;
            }

            if (SirenGhostActor.TryFindByBoxPosition(Position, out var actor)) {
                actor.BeginResolveDeath();
            }
        }

        public override bool? RightClick(int i, int j, Tile tile, Player player) {
            if (player.whoAmI != Main.myPlayer) {
                return false;
            }
            //被诅咒的玩家无法关闭八音盒
            if (player.GetModPlayer<SirenMusicalBoxPlayer>().IsCursed) {
                SoundEngine.PlaySound(CWRSound.ButtonZero with { Pitch = -0.62f });
                return false;
            }

            RequestToggle(Position);
            return false;
        }

        /// <summary>
        /// 开始播放音乐
        /// </summary>
        private void StartMusic() {
            if (Main.netMode == NetmodeID.MultiplayerClient) {
                return;
            }

            if (SirenGhostActor.TryGetActiveSession(out _)) {
                return;
            }

            SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.6f, Pitch = -0.3f }, Center);

            int actorIndex = ActorLoader.NewActor<SirenGhostActor>(Center, Vector2.Zero);
            if (actorIndex >= 0 && ActorLoader.Actors[actorIndex] is SirenGhostActor ghostActor) {
                ghostActor.BindToBox(Position, Center);
                ghostActor.NetUpdate = true;
            }
        }

        /// <summary>
        /// 停止播放音乐
        /// </summary>
        internal void StopMusic() {
            if (Main.netMode == NetmodeID.MultiplayerClient) {
                return;
            }

            var ghosts = ActorLoader.GetActiveActors<SirenGhostActor>();
            foreach (var ghost in ghosts) {
                if (ghost.BoxPosition == Position) {
                    ghost.StopSession();
                }
            }
        }
    }

    #endregion

    #region Actor

    /// <summary>
    /// 海妖幽灵实体，在八音盒播放期间绕其轨道运行并生成视觉效果
    /// 位置通过 [SyncVar] 自动在多人模式下同步
    /// </summary>
    internal class SirenGhostActor : Actor
    {
        private const int ResolveDeathSyncWindow = 15;

        /// <summary>
        /// 八音盒左上角图格坐标
        /// </summary>
        [SyncVar]
        public Point16 BoxPosition;

        /// <summary>
        /// 八音盒中心的世界坐标
        /// </summary>
        [SyncVar]
        public Vector2 BoxCenter;

        [SyncVar]
        public int MusicTimer;

        [SyncVar]
        public bool ResolveDeath;

        private int timer;
        private float orbitAngle;
        private float glowPulse;
        private int resolveDeathCooldown;

        public static bool TryGetActiveSession(out SirenGhostActor actor) {
            var actors = ActorLoader.GetActiveActors<SirenGhostActor>();
            foreach (var sessionActor in actors) {
                if (sessionActor.Active) {
                    actor = sessionActor;
                    return true;
                }
            }

            actor = null;
            return false;
        }

        public static bool TryFindByBoxPosition(Point16 boxPosition, out SirenGhostActor actor) {
            var actors = ActorLoader.GetActiveActors<SirenGhostActor>();
            foreach (var sessionActor in actors) {
                if (sessionActor.Active && sessionActor.BoxPosition == boxPosition) {
                    actor = sessionActor;
                    return true;
                }
            }

            actor = null;
            return false;
        }

        public override void OnSpawn(params object[] args) {
            Width = 32;
            Height = 32;
            DrawLayer = ActorDrawLayer.AfterTiles;
            DrawExtendMode = 400;
            orbitAngle = Main.rand.NextFloat(MathHelper.TwoPi);
        }

        public void BindToBox(Point16 boxPosition, Vector2 boxCenter) {
            BoxPosition = boxPosition;
            BoxCenter = boxCenter;
            Position = boxCenter;
            MusicTimer = 0;
            ResolveDeath = false;
            resolveDeathCooldown = 0;
        }

        public void BeginResolveDeath() {
            if (ResolveDeath) {
                return;
            }

            ResolveDeath = true;
            resolveDeathCooldown = ResolveDeathSyncWindow;
            NetUpdate = true;
        }

        public void StopSession() {
            if (!Main.dedServ) {
                for (int i = 0; i < 16; i++) {
                    Dust.NewDust(BoxCenter - new Vector2(16f), 32, 32, DustID.Blood);
                }
            }

            ActorLoader.KillActor(WhoAmI);
        }

        public override void AI() {
            bool isAuthority = Main.netMode != NetmodeID.MultiplayerClient;

            if (isAuthority) {
                if (!SirenMusicalBoxTP.TryFindMatchingTP(BoxPosition, out var boxTP)) {
                    BeginResolveDeath();
                }
                else {
                    BoxCenter = boxTP.Center;
                }

                if (!ResolveDeath) {
                    MusicTimer++;
                    if (MusicTimer >= SirenMusicalBoxPlayer.MusicDuration) {
                        MusicTimer = SirenMusicalBoxPlayer.MusicDuration;
                        BeginResolveDeath();
                    }
                }
                else {
                    resolveDeathCooldown--;
                    if (resolveDeathCooldown <= 0) {
                        StopSession();
                        return;
                    }
                }

                if (MusicTimer % 30 == 0 || ResolveDeath) {
                    NetUpdate = true;
                }
            }

            timer++;
            orbitAngle += 0.025f;
            glowPulse = MathF.Sin(timer * 0.08f) * 0.3f + 0.7f;

            //椭圆轨道 + 垂直浮动
            float radius = 80f + MathF.Sin(timer * 0.03f) * 30f;
            float verticalBob = MathF.Sin(timer * 0.05f) * 15f;
            Position = BoxCenter + new Vector2(
                MathF.Cos(orbitAngle) * radius,
                MathF.Sin(orbitAngle) * radius * 0.5f + verticalBob - 40f
            );

            Rotation = orbitAngle + MathHelper.PiOver2;

            if (Main.dedServ) {
                return;
            }

            Lighting.AddLight(BoxCenter, new Color(139, 0, 139).ToVector3() * (MathF.Sin(Main.GlobalTimeWrappedHourly * 3f) * 0.5f + 0.5f));

            //八音盒周围的音符粒子
            if (!ResolveDeath && timer % 8 == 0) {
                Color noteColor = Main.rand.Next(4) switch {
                    0 => new Color(186, 85, 211),
                    1 => new Color(138, 43, 226),
                    2 => new Color(147, 112, 219),
                    _ => new Color(255, 0, 255)
                };
                PRTLoader.NewParticle<PRT_Note>(Center + Main.rand.NextVector2Circular(20f, 20f), Main.rand.NextVector2Circular(1f, 1f), noteColor, Main.rand.NextFloat(0.3f, 0.5f)).Configure(Main.rand.Next(30, 60), Main.rand.Next(3));
            }

            //幽灵尾迹尘埃
            if (Main.rand.NextBool(3)) {
                Dust dust = Dust.NewDustDirect(Center, 0, 0, DustID.Shadowflame, 0f, 0f, 100, Color.Purple, Main.rand.NextFloat(1.5f, 2.5f));
                dust.noGravity = true;
                dust.velocity = Main.rand.NextVector2Circular(0.8f, 0.8f);
            }

            //音符Gore
            if (!ResolveDeath && Main.rand.NextBool(10)) {
                SirenMusicalBoxPlayer.SpawnMusicNoteGore(Center);
            }

            //扭曲音波（低概率）
            if (!ResolveDeath && Main.rand.NextBool(30)) {
                int waveCount = 16;
                float waveRadius = Main.rand.NextFloat(60f, 120f);
                for (int i = 0; i < waveCount; i++) {
                    float waveAngle = MathHelper.TwoPi / waveCount * i;
                    Vector2 wavePos = BoxCenter + waveAngle.ToRotationVector2() * waveRadius;
                    Color waveColor = Color.Lerp(Color.Purple, Color.Cyan, Main.rand.NextFloat());
                    PRTLoader.NewParticle<PRT_Note>(wavePos, waveAngle.ToRotationVector2() * Main.rand.NextFloat(1f, 2.5f), waveColor * 0.6f, Main.rand.NextFloat(0.3f, 0.5f)).Configure(Main.rand.Next(20, 40), Main.rand.Next(3));
                }
            }

            //海妖之眼闪现（稀有）
            if (!ResolveDeath && Main.rand.NextBool(180)) {
                Vector2 eyePos = BoxCenter + Main.rand.NextVector2Circular(150f, 150f);
                for (int i = 0; i < 12; i++) {
                    float eyeAngle = MathHelper.TwoPi / 12f * i;
                    Vector2 pos = eyePos + eyeAngle.ToRotationVector2() * 15f;
                    Dust eyeDust = Dust.NewDustDirect(pos, 0, 0, DustID.DungeonWater, 0f, 0f, 100, Color.Cyan, 1.8f);
                    eyeDust.noGravity = true;
                    eyeDust.velocity = (eyePos - pos).SafeNormalize(Vector2.Zero) * 0.5f;
                }
                Dust pupil = Dust.NewDustDirect(eyePos, 0, 0, DustID.Shadowflame, 0f, 0f, 100, Color.Red, 2.5f);
                pupil.noGravity = true;
            }

            //深渊涟漪（从八音盒脚下扩散）
            if (!ResolveDeath && Main.rand.NextBool(20)) {
                Vector2 groundPos = BoxCenter + new Vector2(0, SirenMusicalBoxTile.Height * 8);
                int rippleCount = 12;
                float rippleRadius = 30f + Main.rand.NextFloat(0, 50f);
                for (int j = 0; j < rippleCount; j++) {
                    float rippleAngle = MathHelper.TwoPi / rippleCount * j;
                    Vector2 ripplePos = groundPos + rippleAngle.ToRotationVector2() * rippleRadius;
                    Dust ripple = Dust.NewDustDirect(ripplePos, 0, 0, DustID.DungeonWater, 0f, -1f, 100,
                        Color.DarkBlue * 0.7f, 1.2f);
                    ripple.noGravity = true;
                    ripple.fadeIn = 0.8f;
                }
            }
        }

        public override bool PreDraw(SpriteBatch spriteBatch, ref Color drawColor) {
            Vector2 drawPos = Center - Main.screenPosition;
            float scale = 0.8f + glowPulse * 0.2f;

            //外层光晕
            Color glowColor = new Color(139, 0, 139) * glowPulse * 0.5f;
            spriteBatch.Draw(CWRAsset.SoftGlow.Value, drawPos, null,
                glowColor with { A = 0 }, Rotation,
                CWRAsset.SoftGlow.Size() / 2, scale * 3f, SpriteEffects.None, 0f);

            //内层光晕
            Color innerColor = Color.Lerp(Color.Purple, Color.Cyan, MathF.Sin(timer * 0.05f) * 0.5f + 0.5f) * 0.4f;
            spriteBatch.Draw(CWRAsset.SoftGlow.Value, drawPos, null,
                innerColor with { A = 0 }, -Rotation * 0.5f,
                CWRAsset.SoftGlow.Size() / 2, scale * 1.5f, SpriteEffects.None, 0f);

            return false;
        }
    }

    #endregion
}
