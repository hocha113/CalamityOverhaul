using CalamityOverhaul.Content.UIs;
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
    //驯养菌生蟹，不依赖生物大修
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

        //本地化文本
        public static LocalizedText CrouchText { get; set; }
        public static LocalizedText CrouchAltText { get; set; }
        public static LocalizedText MountHoverText { get; set; }
        public static LocalizedText RideHoverText { get; set; }
        public static LocalizedText ChangeSaddleText { get; set; }
        public static LocalizedText DismountText { get; set; }
        public static LocalizedText DontDismountText { get; set; }

        public string LocalizationCategory => "NPCModifys";

        //核心状态
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

        //内部状态
        internal bool rightPressed;
        internal static int mountPlayerHeldProj;
        internal static Vector2 mountPlayerHeldPosOffset;

        //子系统
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
        }

        public override void SetProperty() {
            Physics = new CrabulonPhysics(npc, this);
            MountSystem = new CrabulonMountSystem(npc, this, Physics);
            Behavior = new CrabulonBehavior(npc, this, Physics);
            Networking = new CrabulonNetworking(this);
            Animation = new CrabulonAnimation(npc, this, Physics);
            Renderer = new CrabulonRenderer(npc, this);
        }

        //投喂逻辑
        public void Feed(Projectile projectile) {
            DyeItemID = projectile.CWR().DyeItemID;
            npc.lifeMax = Main.masterMode ? CrabulonConstants.LifeMaxMaster : CrabulonConstants.LifeMaxNormal;
            npc.life = (int)MathHelper.Clamp(npc.life, 0, npc.lifeMax);
            Owner = Main.player[projectile.owner];
            npc.friendly = true;
            npc.npcSlots = 0;
            FeedValue += CrabulonConstants.FeedValuePerFeed;
            ai[8] = CrabulonConstants.DigestTime;
            npc.ai[0] = npc.ai[1] = npc.ai[2] = 0f;
        }

        //设置驯服状态
        public void SetFeedState() {
            npc.timeLeft = 1800;
            npc.ModNPC.Music = -1;
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

        //网络同步方法
        public void SendFeedPacket(int projIdentity) => Networking.SendFeedPacket(projIdentity);
        public void SendNetWork() => Networking.SendNetworkPacket();
        public static void ReceiveFeedPacket(BinaryReader reader, int whoAmI) => CrabulonNetworking.ReceiveFeedPacket(reader, whoAmI);
        public static void ReceiveNetWork(BinaryReader reader, int whoAmI) => CrabulonNetworking.ReceiveNetworkData(reader, whoAmI);
        internal static void NetHandle(CWRMessageType type, BinaryReader reader, int whoAmI) => CrabulonNetworking.HandleNetworkMessage(type, reader, whoAmI);

        public override void OtherNetWorkSend(ModPacket netMessage) => Networking.WriteData(netMessage);
        public override void OtherNetWorkReceive(BinaryReader reader) => Networking.ReadData(reader);

        //数据保存与加载
        public override void SaveData(TagCompound tag) {
            tag["a"] = npc.position;
            tag["b"] = npc.life;
            tag["c"] = npc.lifeMax;
            tag["d"] = FeedValue;
            tag["e"] = Crouch;
            tag["f"] = Mount;
            tag["g"] = MountACrabulon;
            tag["h"] = DontMount;
            tag["i"] = DyeItemID;
            tag["j"] = Owner.Alives() ? Owner.name : string.Empty;
            tag["k"] = ItemIO.Save(SaddleItem);
        }

        public override void LoadData(TagCompound tag) {
            if (tag.ContainsKey("a")) {
                npc.position = tag.Get<Vector2>("a");
            }
            if (tag.ContainsKey("b")) {
                npc.life = tag.GetInt("b");
            }
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
                string playerName = tag.GetString("j");
                foreach (var p in Main.player) {
                    if (p.name == playerName) {
                        Owner = p;
                        break;
                    }
                }
            }
            if (tag.ContainsKey("k")) {
                SaddleItem = ItemIO.Load(tag.Get<TagCompound>("k"));
            }

            SetFeedState();
        }

        public override bool NeedSaving() => SaddleItem.Alives() || DyeItemID > ItemID.None || FeedValue > 0f;

        //死亡处理
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
            if (CrabulonPlayer != null) {
                CrabulonPlayer.IsMount = false;
            }

            return null;
        }

        //伤害修改
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

        //帧动画
        public override bool FindFrame(int frameHeight) => Animation.UpdateFrame(frameHeight);

        //主AI逻辑
        public override bool AI() {
            if (FeedValue <= 0f) {
                return true;
            }

            SetFeedState();

            if (!Owner.Alives()) {
                npc.velocity.X *= 0.9f;
                npc.ai[0] = 0f;
                return false;
            }

            rightPressed = Owner.whoAmI == Main.myPlayer && Main.mouseRight && Main.mouseRightRelease;

            //首先执行基础更新，这些逻辑在任何状态下都会执行
            Behavior.UpdateBasics();

            //然后处理骑乘AI，如果在骑乘状态则跳过后续AI
            if (!MountSystem.ProcessMountAI()) {
                return false;
            }

            //最后处理行为AI
            return Behavior.ProcessAI();
        }

        //辅助方法
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

        //绘制
        public override bool? Draw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            Renderer.PreDraw(spriteBatch, screenPos, drawColor);
            return null;
        }

        public override bool PostDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            return Renderer.PostDraw(spriteBatch, screenPos, drawColor);
        }
    }
}