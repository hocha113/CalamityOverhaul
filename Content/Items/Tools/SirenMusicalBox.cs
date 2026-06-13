using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Players;
using CalamityOverhaul.Content.PRTTypes;
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
    internal class SirenMusicalBox : ModItem
    {
        public override string Texture => CWRConstant.Item + "Tools/SirenMusicalBox";

        public static LocalizedText DeathText { get; private set; }

        public override void SetStaticDefaults() {
            DeathText = this.GetLocalization(nameof(DeathText), () => "{0}在未知的袭击下化作腐尸");
        }

        public override void Unload() {
            DeathText = null;
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

            TileObjectData.newTile.CopyFrom(TileObjectData.Style2x2);
            TileObjectData.newTile.Width = Width;
            TileObjectData.newTile.Height = Height;
            TileObjectData.newTile.Origin = new Point16(0, 1);
            TileObjectData.newTile.AnchorBottom = new AnchorData(AnchorType.SolidTile | AnchorType.SolidWithTop | AnchorType.SolidSide, Width, 0);
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
            for (int k = 0; k < 13; k++) {
                Dust.NewDust(new Vector2(i * 16, j * 16), Width * 16, Height * 16, DustID.SilverCoin);
            }
        }

        public override bool PreDraw(int i, int j, SpriteBatch spriteBatch) {
            Tile tile = Main.tile[i, j];
            int frameY = tile.TileFrameY;

            if (VaultUtils.SafeGetTopLeft(i, j, out Point16 topLeft) && SirenMusicalSystem.IsBoxPlaying(topLeft)) {
                frameY += Height * 18;
            }

            Texture2D texture = ModContent.Request<Texture2D>(Texture).Value;
            Vector2 offset = Main.drawToScreen ? Vector2.Zero : new Vector2(Main.offScreenRange);
            Vector2 drawPosition = new Vector2(i * 16 - Main.screenPosition.X, j * 16 - Main.screenPosition.Y) + offset;

            if (!tile.IsHalfBlock && tile.Slope == 0) {
                spriteBatch.Draw(texture, drawPosition, new Rectangle(tile.TileFrameX, frameY, 16, 16), Lighting.GetColor(i, j));
            }
            return false;
        }

        public override void ModifyLight(int i, int j, ref float r, ref float g, ref float b) {
            if (!VaultUtils.SafeGetTopLeft(i, j, out Point16 topLeft) || !SirenMusicalSystem.IsBoxPlaying(topLeft)) {
                return;
            }

            float pulse = MathF.Sin(Main.GlobalTimeWrappedHourly * 3f) * 0.3f + 0.7f;
            r = 0.5f * pulse;
            g = 0f;
            b = 0.5f * pulse;
        }
    }

    internal enum SirenMusicalBoxPacket : byte
    {
        ToggleRequest,
        ForceStopRequest,
        SyncSession,
        SyncPlayerCurse,
    }

    /// <summary>
    /// 海妖八音盒唯一会话。它是玩法状态真源；幽灵视觉由纯客户端 ModSystem 绘制，不参与 Actor 网络同步
    /// </summary>
    internal class SirenMusicalSystem : ModSystem
    {
        private const int ResolveDeathWindow = 15;
        private const string MusicPath = "CalamityOverhaul/Assets/Sounds/Music/SirenMusic";

        private static bool active;
        private static bool resolvingDeath;
        private static Point16 boxPosition;
        private static Vector2 boxCenter;
        private static int musicTimer;
        private static int resolveTimer;
        private static int sirenMusicSlot = -1;

        internal static bool Active => active;
        internal static bool ResolveDeath => active && resolvingDeath;
        internal static int MusicTimer => musicTimer;
        internal static Point16 BoxPosition => boxPosition;
        internal static Vector2 BoxCenter => boxCenter;

        public override void Load() {
            ResetSession(killVisual: false);
        }

        public override void OnWorldUnload() {
            ResetSession(killVisual: true);
        }

        public override void Unload() {
            ResetSession(killVisual: true);
            sirenMusicSlot = -1;
        }

        public override void PostUpdateEverything() {
            if (Main.dedServ) {
                UpdateAuthority();
                return;
            }

            if (active) {
                Main.newMusic = Main.musicBox2 = GetSirenMusicSlot();
                SirenGhostVisual.Update(boxCenter);
            }
            else {
                SirenGhostVisual.Reset();
            }

            UpdateAuthority();
        }

        public override void PostDrawTiles() {
            if (!active || Main.dedServ) {
                return;
            }

            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
            SirenGhostVisual.Draw(Main.spriteBatch);
            Main.spriteBatch.End();
        }

        internal static bool IsBoxPlaying(Point16 position) => active && boxPosition == position;

        internal static bool IsPlayerCursed(Player player) {
            if (player == null || !player.active || player.dead) {
                return false;
            }

            return active || player.GetModPlayer<SirenMusicalBoxPlayer>().HasActiveCurse;
        }

        internal static bool TryGetCurrentSession(out Point16 position, out Vector2 center) {
            position = boxPosition;
            center = boxCenter;
            return active;
        }

        internal static void RequestToggle(Point16 position) {
            if (Main.netMode == NetmodeID.SinglePlayer) {
                HandleToggleRequest(position, Main.LocalPlayer);
                return;
            }

            ModPacket packet = CWRMod.Instance.GetPacket();
            packet.Write((byte)CWRMessageType.SirenMusicalBoxToggle);
            packet.Write((byte)SirenMusicalBoxPacket.ToggleRequest);
            packet.Write(position.X);
            packet.Write(position.Y);
            packet.Send();
        }

        internal static void RequestForceStop() {
            if (Main.netMode != NetmodeID.MultiplayerClient) {
                StopSession(playStopEffects: true, sync: Main.netMode == NetmodeID.Server);
                return;
            }

            ModPacket packet = CWRMod.Instance.GetPacket();
            packet.Write((byte)CWRMessageType.SirenMusicalBoxToggle);
            packet.Write((byte)SirenMusicalBoxPacket.ForceStopRequest);
            packet.Send();
        }

        internal static void HandlePacket(BinaryReader reader, int whoAmI) {
            SirenMusicalBoxPacket action = (SirenMusicalBoxPacket)reader.ReadByte();
            switch (action) {
                case SirenMusicalBoxPacket.ToggleRequest:
                    HandleTogglePacket(reader, whoAmI);
                    break;
                case SirenMusicalBoxPacket.ForceStopRequest:
                    HandleForceStopPacket(whoAmI);
                    break;
                case SirenMusicalBoxPacket.SyncSession:
                    ReceiveSessionSync(reader);
                    break;
                case SirenMusicalBoxPacket.SyncPlayerCurse:
                    SirenMusicalBoxPlayer.ReceiveCurseSync(reader, whoAmI);
                    break;
            }
        }

        private static void HandleTogglePacket(BinaryReader reader, int whoAmI) {
            Point16 position = new(reader.ReadInt16(), reader.ReadInt16());
            if (Main.netMode != NetmodeID.Server) {
                return;
            }

            Player player = Main.player[whoAmI];
            if (player is null || !player.active || player.dead) {
                return;
            }

            HandleToggleRequest(position, player);
        }

        private static void HandleForceStopPacket(int whoAmI) {
            if (Main.netMode != NetmodeID.Server) {
                return;
            }

            Player player = Main.player[whoAmI];
            if (player is null || !player.active || !IsPlayerCursed(player)) {
                return;
            }

            StopSession(playStopEffects: true, sync: true);
        }

        private static void HandleToggleRequest(Point16 position, Player player) {
            if (player == null || player.dead) {
                return;
            }

            if (IsPlayerCursed(player)) {
                return;
            }

            if (!SirenMusicalBoxTP.TryFindMatchingTP(position, out SirenMusicalBoxTP boxTP)) {
                return;
            }

            // 会话激活后所有存活玩家均被视为诅咒，无法通过再次右键关闭；需等待音乐结束或外部解救
            if (active) {
                return;
            }

            StartSession(position, boxTP.Center);
        }

        internal static void BeginResolveDeath() {
            if (!active || resolvingDeath) {
                return;
            }

            resolvingDeath = true;
            resolveTimer = ResolveDeathWindow;
            SyncSession();
        }

        private static void StartSession(Point16 position, Vector2 center) {
            active = true;
            resolvingDeath = false;
            boxPosition = position;
            boxCenter = center;
            musicTimer = 0;
            resolveTimer = 0;

            if (!Main.dedServ) {
                SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.6f, Pitch = -0.3f }, center);
            }

            SyncSession();
        }

        private static void StopSession(bool playStopEffects, bool sync) {
            if (!active) {
                return;
            }

            Vector2 oldCenter = boxCenter;
            ResetSession(killVisual: true);

            if (playStopEffects && !Main.dedServ) {
                SpawnStopEffects(oldCenter);
            }

            if (sync) {
                SyncSession(playStopEffects, oldCenter);
            }
        }

        private static void ResetSession(bool killVisual) {
            active = false;
            resolvingDeath = false;
            boxPosition = Point16.NegativeOne;
            boxCenter = Vector2.Zero;
            musicTimer = 0;
            resolveTimer = 0;
            if (killVisual) {
                SirenGhostVisual.Reset();
            }
        }

        private static void UpdateAuthority() {
            if (!active || Main.netMode == NetmodeID.MultiplayerClient) {
                return;
            }

            if (!SirenMusicalBoxTP.TryFindMatchingTP(boxPosition, out SirenMusicalBoxTP boxTP)) {
                BeginResolveDeath();
            }
            else {
                boxCenter = boxTP.Center;
            }

            if (resolvingDeath) {
                resolveTimer--;
                if (resolveTimer <= 0) {
                    StopSession(playStopEffects: false, sync: true);
                }
                return;
            }

            musicTimer++;
            if (musicTimer >= SirenMusicalBoxPlayer.MusicDuration) {
                musicTimer = SirenMusicalBoxPlayer.MusicDuration;
                BeginResolveDeath();
                return;
            }

            if (musicTimer % 30 == 0) {
                SyncSession();
            }
        }

        private static int GetSirenMusicSlot() {
            if (sirenMusicSlot < 0) {
                sirenMusicSlot = MusicLoader.GetMusicSlot(MusicPath);
            }
            return sirenMusicSlot;
        }

        internal static void ApplySirenMusic() {
            if (!Main.dedServ) {
                Main.newMusic = Main.musicBox2 = GetSirenMusicSlot();
            }
        }

        private static void SyncSession(bool playStopEffects = false, Vector2 stopEffectCenter = default, int toClient = -1, int ignoreClient = -1) {
            if (Main.netMode == NetmodeID.SinglePlayer) {
                return;
            }

            ModPacket packet = CWRMod.Instance.GetPacket();
            packet.Write((byte)CWRMessageType.SirenMusicalBoxToggle);
            packet.Write((byte)SirenMusicalBoxPacket.SyncSession);
            packet.Write(active);
            packet.Write(resolvingDeath);
            packet.Write(boxPosition.X);
            packet.Write(boxPosition.Y);
            packet.Write(boxCenter.X);
            packet.Write(boxCenter.Y);
            packet.Write(musicTimer);
            packet.Write(playStopEffects);
            packet.Write(stopEffectCenter.X);
            packet.Write(stopEffectCenter.Y);
            packet.Send(toClient, ignoreClient);
        }

        private static void ReceiveSessionSync(BinaryReader reader) {
            bool wasActive = active;
            Vector2 previousCenter = boxCenter;

            active = reader.ReadBoolean();
            resolvingDeath = reader.ReadBoolean();
            boxPosition = new Point16(reader.ReadInt16(), reader.ReadInt16());
            boxCenter = new Vector2(reader.ReadSingle(), reader.ReadSingle());
            musicTimer = reader.ReadInt32();
            bool playStopEffects = reader.ReadBoolean();
            Vector2 stopEffectCenter = new(reader.ReadSingle(), reader.ReadSingle());
            resolveTimer = resolvingDeath ? ResolveDeathWindow : 0;

            if (!active) {
                SirenGhostVisual.Reset();
                if (playStopEffects && wasActive) {
                    SpawnStopEffects(stopEffectCenter == default ? previousCenter : stopEffectCenter);
                }
            }
            else if (!wasActive && !Main.dedServ) {
                SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.6f, Pitch = -0.3f }, boxCenter);
            }
        }

        private static void SpawnStopEffects(Vector2 center) {
            for (int i = 0; i < 16; i++) {
                Dust.NewDust(center - new Vector2(16f), 32, 32, DustID.Blood);
            }
        }
    }

    internal class SirenMusicalBoxPlayer : ModPlayer
    {
        public const int MusicDuration = 60 * 23;
        private const int CurseResolveDeathWindow = 15;

        public bool IsCursed;
        public bool HasSirenMusicalBox;

        private int particleTimer;
        private bool deathTriggered;
        private bool curseActive;
        private bool curseResolvingDeath;
        private int curseTimer;
        private int curseResolveTimer;

        private bool syncedCurseActive;
        private bool syncedCurseResolvingDeath;
        private int syncedCurseTimerBucket;

        internal bool HasActiveCurse => curseActive && Player != null && Player.active && !Player.dead;
        internal float CurseTimerRatio => MathHelper.Clamp(curseTimer / (float)MusicDuration, 0f, 1f);

        public override void PreUpdate() {
            AttachWorldCurse();
            IsCursed = SirenMusicalSystem.IsPlayerCursed(Player);
        }

        public override void PostUpdate() {
            if (!IsCursed) {
                particleTimer = 0;
                deathTriggered = false;
                return;
            }

            UpdateMindCurse();

            if (curseResolvingDeath && Player.whoAmI == Main.myPlayer && !deathTriggered) {
                deathTriggered = true;
                ExecuteDeath();
            }

            if (Main.dedServ) {
                return;
            }

            if (Player.whoAmI == Main.myPlayer) {
                SirenMusicalSystem.ApplySirenMusic();
            }

            particleTimer++;
            if (particleTimer % 5 == 0) {
                SirenMusicalBoxEffects.SpawnCurseParticles(Player);
            }
        }

        public override void OnRespawn() {
            IsCursed = false;
            deathTriggered = false;
            ClearCurse();
        }

        public override void OnEnterWorld() {
            if (curseActive && Main.netMode == NetmodeID.MultiplayerClient) {
                SendCurseSync(Player);
            }
        }

        public override void SaveData(TagCompound tag) {
            tag["HasSirenMusicalBox"] = HasSirenMusicalBox;

            if (!curseActive) {
                return;
            }

            tag["SirenMusicalBoxCurseActive"] = curseActive;
            tag["SirenMusicalBoxCurseResolvingDeath"] = curseResolvingDeath;
            tag["SirenMusicalBoxCurseTimer"] = curseTimer;
            tag["SirenMusicalBoxCurseResolveTimer"] = curseResolveTimer;
        }

        public override void LoadData(TagCompound tag) {
            if (tag.TryGet("HasSirenMusicalBox", out bool value)) {
                HasSirenMusicalBox = value;
            }

            curseActive = tag.TryGet("SirenMusicalBoxCurseActive", out bool savedCurseActive) && savedCurseActive;
            curseResolvingDeath = false;
            curseTimer = 0;
            curseResolveTimer = 0;
            deathTriggered = false;

            if (!curseActive) {
                return;
            }

            if (tag.TryGet("SirenMusicalBoxCurseTimer", out int savedCurseTimer)) {
                curseTimer = Math.Clamp(savedCurseTimer, 0, MusicDuration);
            }
            if (tag.TryGet("SirenMusicalBoxCurseResolvingDeath", out bool savedResolvingDeath)) {
                curseResolvingDeath = savedResolvingDeath;
            }
            if (tag.TryGet("SirenMusicalBoxCurseResolveTimer", out int savedResolveTimer)) {
                curseResolveTimer = savedResolveTimer;
            }

            if (curseTimer >= MusicDuration) {
                BeginCurseResolveDeath();
            }
            else if (curseResolvingDeath) {
                curseResolveTimer = Math.Clamp(curseResolveTimer, 1, CurseResolveDeathWindow);
            }
        }

        public override void SyncPlayer(int toWho, int fromWho, bool newPlayer) {
            SendCurseSync(Player, toWho, fromWho);
        }

        public override void SendClientChanges(ModPlayer clientPlayer) {
            SirenMusicalBoxPlayer clone = (SirenMusicalBoxPlayer)clientPlayer;
            int timerBucket = curseTimer / 60;
            if (clone.syncedCurseActive != curseActive
                || clone.syncedCurseResolvingDeath != curseResolvingDeath
                || clone.syncedCurseTimerBucket != timerBucket) {
                SendCurseSync(Player);
            }
        }

        public override void CopyClientState(ModPlayer targetCopy) {
            SirenMusicalBoxPlayer clone = (SirenMusicalBoxPlayer)targetCopy;
            clone.syncedCurseActive = curseActive;
            clone.syncedCurseResolvingDeath = curseResolvingDeath;
            clone.syncedCurseTimerBucket = curseTimer / 60;
        }

        public override void CatchFish(FishingAttempt attempt, ref int itemDrop, ref int npcSpawn,
            ref AdvancedPopupRequest sonar, ref Vector2 sonarPosition) {
            if (attempt.inHoney || attempt.inLava || npcSpawn > 0) {
                return;
            }

            if (HasSirenMusicalBox) {
                if (Main.rand.NextBool(800)) {
                    itemDrop = ModContent.ItemType<SirenMusicalBox>();
                }
                return;
            }

            itemDrop = ModContent.ItemType<SirenMusicalBox>();
            HasSirenMusicalBox = true;
        }

        internal static void StopAllMusicBoxes(Player player = null) {
            SirenMusicalSystem.RequestForceStop();
            Player target = player ?? Main.LocalPlayer;
            if (target != null && target.active && target.TryGetModPlayer(out SirenMusicalBoxPlayer sirenPlayer)) {
                sirenPlayer.ClearCurse(sync: true);
            }
        }

        internal static void ReceiveCurseSync(BinaryReader reader, int whoAmI) {
            int playerIndex = reader.ReadByte();
            bool active = reader.ReadBoolean();
            bool resolvingDeath = reader.ReadBoolean();
            int timer = reader.ReadInt32();
            int resolveTimer = reader.ReadInt32();

            if (Main.netMode == NetmodeID.Server && playerIndex != whoAmI) {
                return;
            }

            if (playerIndex < 0 || playerIndex >= Main.maxPlayers) {
                return;
            }

            Player player = Main.player[playerIndex];
            if (player == null || !player.active) {
                return;
            }

            player.GetModPlayer<SirenMusicalBoxPlayer>().ApplyCurseSync(active, resolvingDeath, timer, resolveTimer);

            if (Main.netMode == NetmodeID.Server) {
                SendCurseSync(player, -1, whoAmI);
            }
        }

        private void AttachWorldCurse() {
            if (Player.dead || !SirenMusicalSystem.Active) {
                return;
            }

            curseActive = true;
            curseTimer = Math.Max(curseTimer, SirenMusicalSystem.MusicTimer);

            if (SirenMusicalSystem.ResolveDeath) {
                BeginCurseResolveDeath();
            }
        }

        private void UpdateMindCurse() {
            if (!curseActive || Player.dead) {
                return;
            }

            if (curseResolvingDeath) {
                curseResolveTimer = Math.Max(curseResolveTimer - 1, 1);
                return;
            }

            curseTimer++;
            if (SirenMusicalSystem.Active) {
                curseTimer = Math.Max(curseTimer, SirenMusicalSystem.MusicTimer);
            }

            if (curseTimer >= MusicDuration) {
                curseTimer = MusicDuration;
                BeginCurseResolveDeath();
            }
        }

        private void BeginCurseResolveDeath() {
            if (!curseActive) {
                curseActive = true;
            }

            curseResolvingDeath = true;
            curseResolveTimer = CurseResolveDeathWindow;
        }

        private void ExecuteDeath() {
            if (Player.dead) {
                return;
            }

            if (Player.TryGetOverride(out PlayerDeath deathOverride)) {
                deathOverride.Doomed = true;
            }

            Player.immune = false;
            Player.immuneTime = 0;
            Player.immuneNoBlink = false;

            SoundEngine.PlaySound(SoundID.NPCDeath59 with { Volume = 0.9f, Pitch = -0.8f }, Player.Center);
            SoundEngine.PlaySound(SoundID.Zombie103 with { Volume = 0.7f, Pitch = -0.6f }, Player.Center);

            if (!Main.dedServ) {
                SirenMusicalBoxEffects.SpawnDeathEffects(Player.Center);
            }

            PlayerDeathReason damageSource = PlayerDeathReason.ByCustomReason(
                SirenMusicalBox.DeathText.ToNetworkText(Player.name)
            );
            Player.KillMe(damageSource, Player.statLifeMax2 * 10, 0, false);
            ClearCurse(sync: true);
        }

        private void ApplyCurseSync(bool active, bool resolvingDeath, int timer, int resolveTimer) {
            if (!active) {
                ClearCurse();
                return;
            }

            int syncedTimer = Math.Clamp(timer, 0, MusicDuration);
            curseTimer = curseActive ? Math.Max(curseTimer, syncedTimer) : syncedTimer;
            curseActive = true;
            curseResolvingDeath = resolvingDeath || curseTimer >= MusicDuration;
            curseResolveTimer = curseResolvingDeath
                ? Math.Clamp(resolveTimer, 1, CurseResolveDeathWindow)
                : 0;
        }

        private void ClearCurse(bool sync = false) {
            curseActive = false;
            curseResolvingDeath = false;
            curseTimer = 0;
            curseResolveTimer = 0;
            particleTimer = 0;

            if (sync) {
                SendCurseSync(Player);
            }
        }

        private static void SendCurseSync(Player player, int toWho = -1, int fromWho = -1) {
            if (Main.netMode == NetmodeID.SinglePlayer || player == null || !player.active) {
                return;
            }

            SirenMusicalBoxPlayer sirenPlayer = player.GetModPlayer<SirenMusicalBoxPlayer>();
            ModPacket packet = CWRMod.Instance.GetPacket();
            packet.Write((byte)CWRMessageType.SirenMusicalBoxToggle);
            packet.Write((byte)SirenMusicalBoxPacket.SyncPlayerCurse);
            packet.Write((byte)player.whoAmI);
            packet.Write(sirenPlayer.curseActive);
            packet.Write(sirenPlayer.curseResolvingDeath);
            packet.Write(sirenPlayer.curseTimer);
            packet.Write(sirenPlayer.curseResolveTimer);
            packet.Send(toWho, fromWho);
        }
    }

    internal static class SirenMusicalBoxEffects
    {
        internal static void SpawnCurseParticles(Player player) {
            if (!player.Alives()) {
                return;
            }

            float timerRatio = player.GetModPlayer<SirenMusicalBoxPlayer>().CurseTimerRatio;

            for (int layer = 0; layer < 2; layer++) {
                float baseAngle = Main.GlobalTimeWrappedHourly * (2f + layer * 0.5f);
                float angle = baseAngle + Main.rand.NextFloat(MathHelper.TwoPi);
                float radius = 60f + layer * 40f + MathF.Sin(Main.GlobalTimeWrappedHourly * 3f + layer) * 15f;
                Vector2 spawnPos = player.Center + angle.ToRotationVector2() * radius;
                Vector2 velocity = (player.Center - spawnPos).SafeNormalize(Vector2.Zero).RotatedBy(MathHelper.PiOver2) * Main.rand.NextFloat(0.5f, 1.5f);
                PRTLoader.NewParticle<PRT_Note>(spawnPos, velocity, RandomNoteColor(), Main.rand.NextFloat(0.3f, 0.6f))
                    .Configure(Main.rand.Next(45, 75), Main.rand.Next(3));
            }

            if (Main.rand.NextBool(2)) {
                Vector2 ghostPos = player.Center + Main.rand.NextVector2CircularEdge(1f, 1f) * Main.rand.Next(100, 200);
                Vector2 ghostVel = (player.Center - ghostPos).SafeNormalize(Vector2.Zero) * Main.rand.NextFloat(2f, 4f);
                PRTLoader.NewParticle<PRT_Note>(ghostPos, ghostVel, Color.DarkViolet * 0.8f, Main.rand.NextFloat(0.5f, 0.75f))
                    .Configure(Main.rand.Next(30, 50), Main.rand.Next(3));
            }

            if (Main.rand.NextBool(3)) {
                Dust dust = Dust.NewDustDirect(player.Center + Main.rand.NextVector2Circular(120f, 120f), 0, 0,
                    DustID.Shadowflame, 0f, 0f, 100, Color.Purple, Main.rand.NextFloat(1.5f, 2.5f));
                dust.noGravity = true;
                dust.velocity = (player.Center - dust.position).SafeNormalize(Vector2.Zero) * Main.rand.NextFloat(0.5f, 1.5f);
            }

            if (timerRatio > 0.78f && Main.rand.NextFloat() < (timerRatio - 0.78f) / 0.22f * 0.4f) {
                Dust warnDust = Dust.NewDustDirect(player.Center + Main.rand.NextVector2Circular(60f, 60f), 0, 0,
                    DustID.Blood, 0f, 0f, 100, Color.Red, Main.rand.NextFloat(2f, 3.5f));
                warnDust.noGravity = true;
                warnDust.velocity = Main.rand.NextVector2Circular(3f, 3f);
            }
        }

        internal static void SpawnDeathEffects(Vector2 center) {
            for (int i = 0; i < 120; i++) {
                Vector2 velocity = Main.rand.NextVector2CircularEdge(12f, 12f) * Main.rand.NextFloat(0.6f, 1.4f);
                Dust dust = Dust.NewDustDirect(center, 0, 0, DustID.Shadowflame, velocity.X, velocity.Y, 100,
                    Main.rand.NextBool() ? Color.DarkMagenta : Color.Purple, Main.rand.NextFloat(2.5f, 4f));
                dust.noGravity = true;
                dust.fadeIn = 1.5f;
            }

            for (int i = 0; i < 48; i++) {
                float angle = MathHelper.TwoPi / 48f * i;
                Vector2 pos = center + angle.ToRotationVector2() * Main.rand.NextFloat(30f, 140f);
                Vector2 vel = angle.ToRotationVector2() * Main.rand.NextFloat(4f, 9f);
                PRTLoader.NewParticle<PRT_Light>(pos, vel, Color.Lerp(Color.Purple, Color.Cyan, Main.rand.NextFloat()) * 0.75f,
                    Main.rand.NextFloat(0.35f, 0.6f)).Configure(Main.rand.Next(30, 70));
            }

            for (int i = 0; i < 6; i++) {
                Vector2 eyePos = center + Main.rand.NextVector2CircularEdge(180f, 180f);
                for (int j = 0; j < 12; j++) {
                    Vector2 pos = eyePos + (MathHelper.TwoPi / 12f * j).ToRotationVector2() * 18f;
                    Dust eyeDust = Dust.NewDustDirect(pos, 0, 0, DustID.DungeonWater, 0f, 0f, 100, Color.Cyan, 1.8f);
                    eyeDust.noGravity = true;
                    eyeDust.velocity = (eyePos - pos).SafeNormalize(Vector2.Zero) * 0.5f;
                }
                Dust.NewDustDirect(eyePos, 0, 0, DustID.Blood, 0f, 0f, 100, Color.DarkRed, 2.5f).noGravity = true;
            }
        }

        internal static void SpawnMusicNoteGore(Vector2 position) {
            int goreType = Main.rand.Next(570, 573);
            float wind = Main.WindForVisuals * 2f;
            if (goreType == 572) {
                position.X -= 8f;
            }
            else if (goreType == 571) {
                position.X -= 4f;
            }

            Vector2 velocity = new(
                wind * (1f + Main.rand.NextFloat(-1.5f, 1.5f)),
                -0.5f * (1f + Main.rand.NextFloat(-0.5f, 0.5f))
            );
            Gore.NewGore(new EntitySource_TileUpdate((int)position.X, (int)position.Y), position, velocity, goreType, 0.8f);
        }

        private static Color RandomNoteColor() {
            return Main.rand.Next(4) switch {
                0 => new Color(186, 85, 211),
                1 => new Color(138, 43, 226),
                2 => new Color(147, 112, 219),
                _ => new Color(255, 0, 255),
            };
        }
    }

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

        internal static void HandleTogglePacket(BinaryReader reader, int whoAmI) => SirenMusicalSystem.HandlePacket(reader, whoAmI);

        public override void OnKill() {
            if (Main.netMode != NetmodeID.MultiplayerClient && SirenMusicalSystem.IsBoxPlaying(Position)) {
                SirenMusicalSystem.BeginResolveDeath();
            }
        }

        public override bool? RightClick(int i, int j, Tile tile, Player player) {
            if (player.whoAmI != Main.myPlayer) {
                return false;
            }

            if (SirenMusicalSystem.IsPlayerCursed(player)) {
                SoundEngine.PlaySound(CWRSound.ButtonZero with { Pitch = -0.62f });
                return false;
            }

            SirenMusicalSystem.RequestToggle(Position);
            return false;
        }
    }

    /// <summary>
    /// 纯客户端幽灵视觉，由会话状态驱动，避免 Actor 网络同步导致重复生成。
    /// </summary>
    internal static class SirenGhostVisual
    {
        private static int timer;
        private static float orbitAngle = -1f;
        private static float glowPulse;
        private static Vector2 ghostCenter;
        private static float ghostRotation;

        internal static void Reset() {
            timer = 0;
            orbitAngle = -1f;
            glowPulse = 0f;
            ghostCenter = Vector2.Zero;
            ghostRotation = 0f;
        }

        internal static void Update(Vector2 boxCenter) {
            if (!SirenMusicalSystem.Active) {
                return;
            }

            if (orbitAngle < 0f) {
                orbitAngle = Main.rand.NextFloat(MathHelper.TwoPi);
            }

            timer++;
            orbitAngle += 0.025f;
            glowPulse = MathF.Sin(timer * 0.08f) * 0.3f + 0.7f;

            float radius = 80f + MathF.Sin(timer * 0.03f) * 30f;
            float verticalBob = MathF.Sin(timer * 0.05f) * 15f;
            ghostCenter = boxCenter + new Vector2(
                MathF.Cos(orbitAngle) * radius,
                MathF.Sin(orbitAngle) * radius * 0.5f + verticalBob - 40f
            );
            ghostRotation = orbitAngle + MathHelper.PiOver2;

            Lighting.AddLight(boxCenter, new Color(139, 0, 139).ToVector3() * glowPulse);

            if (!SirenMusicalSystem.ResolveDeath && timer % 8 == 0) {
                PRTLoader.NewParticle<PRT_Note>(ghostCenter + Main.rand.NextVector2Circular(20f, 20f),
                    Main.rand.NextVector2Circular(1f, 1f), Color.Purple, Main.rand.NextFloat(0.3f, 0.5f))
                    .Configure(Main.rand.Next(30, 60), Main.rand.Next(3));
            }

            if (Main.rand.NextBool(3)) {
                Dust dust = Dust.NewDustDirect(ghostCenter, 0, 0, DustID.Shadowflame, 0f, 0f, 100, Color.Purple, Main.rand.NextFloat(1.5f, 2.5f));
                dust.noGravity = true;
                dust.velocity = Main.rand.NextVector2Circular(0.8f, 0.8f);
            }

            if (!SirenMusicalSystem.ResolveDeath && Main.rand.NextBool(10)) {
                SirenMusicalBoxEffects.SpawnMusicNoteGore(ghostCenter);
            }
        }

        internal static void Draw(SpriteBatch spriteBatch) {
            if (!SirenMusicalSystem.Active || timer <= 0) {
                return;
            }

            Vector2 drawPos = ghostCenter - Main.screenPosition;
            float scale = 0.8f + glowPulse * 0.2f;

            Color glowColor = new Color(139, 0, 139) * glowPulse * 0.5f;
            spriteBatch.Draw(CWRAsset.SoftGlow.Value, drawPos, null,
                glowColor with { A = 0 }, ghostRotation,
                CWRAsset.SoftGlow.Size() / 2, scale * 3f, SpriteEffects.None, 0f);

            Color innerColor = Color.Lerp(Color.Purple, Color.Cyan, MathF.Sin(timer * 0.05f) * 0.5f + 0.5f) * 0.4f;
            spriteBatch.Draw(CWRAsset.SoftGlow.Value, drawPos, null,
                innerColor with { A = 0 }, -ghostRotation * 0.5f,
                CWRAsset.SoftGlow.Size() / 2, scale * 1.5f, SpriteEffects.None, 0f);
        }
    }
}
