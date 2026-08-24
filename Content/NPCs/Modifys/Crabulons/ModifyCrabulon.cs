using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.Modifys.Crabulons.CrabulonUIs;
using InnoVault.GameSystem;
using Microsoft.Xna.Framework.Graphics;
using System.IO;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.NPCs.Modifys.Crabulons
{
    /// <summary>驯养菌生蟹，不依赖残酷模式（直接继承 NPCOverride，不走 BrutalNPCOverride 门控）</summary>
    internal class ModifyCrabulon : NPCOverride, ILocalizedModType
    {
        public override int TargetID => CWRID.NPC_Crabulon;

        public CrabulonPlayer CrabulonPlayer {
            get {
                if (!Owner.Alives()) {
                    return null;
                }
                if (Owner.TryGetOverride<CrabulonPlayer>(out var crabulonPlayer)) {
                    return crabulonPlayer;
                }
                return null;
            }
        }

        public static LocalizedText CrouchText { get; set; }
        public static LocalizedText CrouchAltText { get; set; }
        public static LocalizedText MountHoverText { get; set; }
        public static LocalizedText RideHoverText { get; set; }
        public static LocalizedText ChangeSaddleText { get; set; }
        public static LocalizedText DismountText { get; set; }
        public static LocalizedText DontDismountText { get; set; }
        public static LocalizedText RecallText { get; set; }
        public static LocalizedText SaddleText { get; set; }
        public static LocalizedText ReleaseText { get; set; }
        public static LocalizedText ReleasedText { get; set; }
        public static LocalizedText StatusRestText { get; set; }
        public static LocalizedText StatusMountText { get; set; }
        public static LocalizedText StatusFollowText { get; set; }
        public static LocalizedText UnsaddleText { get; set; }
        public static LocalizedText CommandHintText { get; set; }
        public static LocalizedText ReleaseConfirmText { get; set; }

        public string LocalizationCategory => "NPCModifys";

        public float FeedValue = 0;
        public NPC TargetNPC;
        public Player Owner;
        public bool Crouch;
        public bool Mount;
        public Item SaddleItem = new();
        public bool MountACrabulon;
        public int DontMount;
        public bool hoverNPC;
        internal int DyeItemID;
        internal float dontTurnTo;
        //主人未连入，记名待认领
        internal string pendingOwnerName = string.Empty;

        internal bool rightPressed;
        internal static int mountPlayerHeldProj;
        internal static Vector2 mountPlayerHeldPosOffset;

        //指令环本地视觉，受击闪+上帧生命
        internal int uiOldLife = -1;
        internal float uiDamageFlash;
        //开环占位，防右键取消被读成上马
        internal bool uiCommandOpen;

        public CrabulonPhysics Physics { get; private set; }
        public CrabulonMountSystem MountSystem { get; private set; }
        public CrabulonBehavior Behavior { get; private set; }
        public CrabulonNetworking Networking { get; private set; }
        public CrabulonAnimation Animation { get; private set; }
        public CrabulonRenderer Renderer { get; private set; }

        public override void SetStaticDefaults() {
            CrouchText = this.GetLocalization(nameof(CrouchText), () => "Await");
            CrouchAltText = this.GetLocalization(nameof(CrouchAltText), () => "Follow");
            MountHoverText = this.GetLocalization(nameof(MountHoverText), () => "Right-Click To Mount Saddle");
            RideHoverText = this.GetLocalization(nameof(RideHoverText), () => "Right-Click To Ride");
            ChangeSaddleText = this.GetLocalization(nameof(ChangeSaddleText), () => "Right-Click To Change Saddle");
            DismountText = this.GetLocalization(nameof(DismountText), () => "Right-Click To Dismount");
            DontDismountText = this.GetLocalization(nameof(DontDismountText), () => "The mount feature is temporarily unavailable in multiplayer mode!");
            RecallText = this.GetLocalization(nameof(RecallText), () => "Recall");
            SaddleText = this.GetLocalization(nameof(SaddleText), () => "Saddle");
            ReleaseText = this.GetLocalization(nameof(ReleaseText), () => "Release");
            ReleasedText = this.GetLocalization(nameof(ReleasedText), () => "{0} has been released");
            StatusRestText = this.GetLocalization(nameof(StatusRestText), () => "[REST]");
            StatusMountText = this.GetLocalization(nameof(StatusMountText), () => "[MOUNT]");
            StatusFollowText = this.GetLocalization(nameof(StatusFollowText), () => "[FOLLOW]");
            UnsaddleText = this.GetLocalization(nameof(UnsaddleText), () => "Unsaddle");
            CommandHintText = this.GetLocalization(nameof(CommandHintText), () => "Command");
            ReleaseConfirmText = this.GetLocalization(nameof(ReleaseConfirmText), () => "Click again to release");
        }

        public override void SetProperty() {
            Physics = new CrabulonPhysics(npc, this);
            MountSystem = new CrabulonMountSystem(npc, this);
            Behavior = new CrabulonBehavior(npc, this, Physics);
            Networking = new CrabulonNetworking(this);
            Animation = new CrabulonAnimation(npc, this, Physics);
            Renderer = new CrabulonRenderer(npc, this);
        }

        //投喂入口
        public void ApplyFeed(Player feeder, int dyeItemID) {
            if (FeedValue > 0f) {
                FeedTamed();
            }
            else {
                Feed(feeder, dyeItemID);
            }
        }

        //初驯投喂
        public void Feed(Player feeder, int dyeItemID) {
            DyeItemID = dyeItemID;
            npc.lifeMax = Main.masterMode ? CrabulonConstants.LifeMaxMaster : CrabulonConstants.LifeMaxNormal;
            npc.life = (int)MathHelper.Clamp(npc.life, 0, npc.lifeMax);
            Owner = feeder;
            pendingOwnerName = string.Empty;
            npc.friendly = true;
            npc.npcSlots = 0;
            FeedValue += CrabulonConstants.FeedValuePerFeed;
            ai[8] = CrabulonConstants.DigestTime;
            npc.ai[0] = npc.ai[1] = npc.ai[2] = 0f;
            //驯服复位物理，防穿墙
            npc.noGravity = false;
            npc.noTileCollide = false;
            if (!VaultUtils.isClient) {
                npc.netUpdate = true;
            }
        }

        //已驯投喂
        public void FeedTamed() {
            float maxFeed = CrabulonConstants.MaxFeedValue;
            if (FeedValue >= maxFeed) {
                //饱食满小回血
                if (npc.life < npc.lifeMax) {
                    npc.life = (int)MathHelper.Clamp(npc.life + CrabulonConstants.FeedHealAmount, 0, npc.lifeMax);
                    npc.netUpdate = true;
                }
            }
            else {
                FeedValue += CrabulonConstants.FeedValuePerFeed;
                if (FeedValue > maxFeed) FeedValue = maxFeed;
            }
            ai[8] = CrabulonConstants.DigestTime;
            npc.ai[0] = npc.ai[1] = npc.ai[2] = 0f;
        }

        //解除驯服
        public void ReleaseTame() {
            if (SaddleItem.Alives()) {
                SaddleItem.SpwanItem(npc.FromObjectGetParent(), npc.Hitbox);
                SaddleItem = new Item();
            }
            if (Mount || MountACrabulon) {
                MountSystem.ForceDismount();
            }
            FeedValue = 0f;
            Owner = null;
            pendingOwnerName = string.Empty;
            Crouch = false;
            Mount = false;
            MountACrabulon = false;
            DontMount = 0;
            DyeItemID = 0;
            ApplyStateFields();
            npc.netUpdate = true;
        }

        //网络收后刷新派生字段
        internal void ApplyStateFields() {
            if (FeedValue > 0f) {
                SetFeedState();
                return;
            }
            npc.friendly = false;
            npc.boss = true;
            npc.damage = npc.defDamage;
            npc.npcSlots = 2f;
            if (CrabulonPlayer != null) {
                CrabulonPlayer.IsMount = false;
            }
        }

        public void SetFeedState() {
            npc.timeLeft = 1800;
            //Boss音乐见CrabulonMusicOverride，勿写ModNPC.Music
            npc.BossBar = ModContent.GetInstance<CrabulonFriendBossBar>();
            npc.boss = false;
            npc.friendly = true;
            npc.damage = 0;
            npc.npcSlots = NeedSaving() ? 0f : 2f;
        }

        public override bool? DrawHealthBar(byte hbPosition, ref float scale, ref Vector2 position) {
            if (Mount) {
                return false;
            }
            return null;
        }

        //状态走NPCOverride通道
        public void SendNetWork() {
            if (VaultUtils.isClient) {
                RequestSync(NPCOverrideSyncField.All);
            }
            else if (VaultUtils.isServer) {
                NetOtherWorkSend = true;
            }
        }
        internal static void NetHandle(CWRMessageType type, BinaryReader reader, int whoAmI) => CrabulonNetworking.HandleNetworkMessage(type, reader, whoAmI);

        public override void OtherNetWorkSend(ModPacket netMessage) => Networking.WriteData(netMessage);
        public override void OtherNetWorkReceive(BinaryReader reader) => Networking.ReadData(reader);

        public override void SaveData(TagCompound tag) {
            tag["c"] = npc.lifeMax;
            tag["d"] = FeedValue;
            tag["e"] = Crouch;
            tag["f"] = Mount;
            tag["g"] = MountACrabulon;
            tag["h"] = DontMount;
            tag["i"] = DyeItemID;
            //同名认领有局限
            tag["j"] = Owner.Alives() ? Owner.name : string.Empty;
            tag["k"] = ItemIO.Save(SaddleItem);
        }

        public override void LoadData(TagCompound tag) {
            if (tag.ContainsKey("c")) {
                npc.lifeMax = tag.GetInt("c");
            }
            if (tag.ContainsKey("d")) {
                FeedValue = tag.GetFloat("d");
            }
            if (tag.ContainsKey("e")) {
                Crouch = tag.GetBool("e");
            }
            if (tag.ContainsKey("f")) {
                Mount = tag.GetBool("f");
            }
            if (tag.ContainsKey("g")) {
                MountACrabulon = tag.GetBool("g");
            }
            if (tag.ContainsKey("h")) {
                DontMount = tag.GetInt("h");
            }
            if (tag.ContainsKey("i")) {
                DyeItemID = tag.GetInt("i");
            }
            if (tag.ContainsKey("j")) {
                pendingOwnerName = tag.GetString("j");
                TryResolvePendingOwner();
            }
            SaddleItem = CWRSaveData.LoadItemFromTag(tag, "k", nameof(ModifyCrabulon));

            //骑乘会话态，读档重置
            Mount = false;
            MountACrabulon = false;

            SetFeedState();
        }

        public override bool NeedSaving() => SaddleItem.Alives() || DyeItemID > ItemID.None || FeedValue > 0f;

        //死亡
        public override bool? On_PreKill() {
            if (SaddleItem.Alives()) {
                SaddleItem.SpwanItem(npc.FromObjectGetParent(), npc.Hitbox);
            }

            if (FeedValue > 0f) {
                for (int i = 0; i < ItemLoader.ItemCount; i++) {
                    NPCLoader.blockLoot.Add(i);
                }
            }
            else {
                if (!CWRRef.GetBossRushActive()) {
                    ModifyTruffle.Spawn(npc);
                }
            }

            FeedValue = 0f;
            MountSystem.ForceDismount();

            return null;
        }

        public override void ModifyHitByProjectile(Projectile projectile, ref NPC.HitModifiers modifiers) {
            if (FeedValue > 0f && Crouch) {
                modifiers.FinalDamage /= 2;
            }
        }

        public override bool? CanBeHitByProjectile(Projectile projectile) {
            if (projectile.TryGetGlobalProjectile<CWRProjectile>(out var gProj)
                && gProj.Source != null
                && gProj.Source is EntitySource_Parent entitySource
                && entitySource.Entity is NPC boss
                && boss.type == CWRID.NPC_Crabulon
                && boss.whoAmI == npc.whoAmI) {
                return false;
            }
            return null;
        }

        public override bool FindFrame(int frameHeight) => Animation.UpdateFrame(frameHeight);

        //主人重连按名认领
        internal void TryResolvePendingOwner() {
            if (VaultUtils.isClient || Owner.Alives() || string.IsNullOrEmpty(pendingOwnerName)) {
                return;
            }
            foreach (var p in Main.ActivePlayers) {
                if (p.name != pendingOwnerName) {
                    continue;
                }
                Owner = p;
                pendingOwnerName = string.Empty;
                if (VaultUtils.isServer) {
                    NetOtherWorkSend = true;
                }
                break;
            }
        }

        public override bool AI() {
            if (FeedValue <= 0f) {
                return true;
            }

            SetFeedState();
            TryResolvePendingOwner();

            if (!Owner.Alives()) {
                //主人失效本端下马
                if (Mount || MountACrabulon) {
                    MountSystem.ForceDismount();
                }
                npc.velocity.X *= 0.9f;
                npc.ai[0] = 0f;
                return false;
            }

            rightPressed = Owner.whoAmI == Main.myPlayer && Main.mouseRight && Main.mouseRightRelease;

            Behavior.UpdateBasics();

            if (!MountSystem.ProcessMountAI()) {
                return false;
            }

            return Behavior.ProcessAI();
        }

        public Vector2 GetMountPos() => MountSystem.GetMountPosition();
        public void CloseMount() => MountSystem.Dismount();

        public override bool? CanFallThroughPlatforms() => Physics.ShouldFallThroughPlatforms();

        public override void ModifyHoverBoundingBox(ref Rectangle boundingBox) {
            if (!Mount && !SaddleItem.Alives()) {
                return;
            }
            if (Main.keyState.PressingShift()) {
                return;
            }
            boundingBox = Vector2.Zero.GetRectangle(1);
        }

        public override bool CheckActive() {
            if (FeedValue > 0f) {
                return false;
            }
            return true;
        }

        public override bool? Draw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            Renderer.PreDraw(spriteBatch, screenPos, drawColor);
            return null;
        }

        public override bool PostDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            bool result = Renderer.PostDraw(spriteBatch, screenPos, drawColor);
            CrabulonUIs.CrabulonClusterRenderer.DrawWorldCluster(spriteBatch, this);
            return result;
        }
    }
}