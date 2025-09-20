using CalamityMod.NPCs.Crabulon;
using CalamityMod.Systems;
using CalamityOverhaul.Content.Items.Tools;
using CalamityOverhaul.Content.PRTTypes;
using CalamityOverhaul.Content.UIs;
using InnoVault.GameSystem;
using InnoVault.PRT;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Reflection;
using Terraria;
using Terraria.Audio;
using Terraria.Graphics;
using Terraria.Graphics.Shaders;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.NPCs.Modifys
{
    internal class ModifyCrabulon : NPCOverride, ILocalizedModType//驯养菌生蟹，不依赖生物大修
    {
        public override int TargetID => ModContent.NPCType<Crabulon>();
        public CrabulonPlayer CrabulonPlayer => Owner.GetOverride<CrabulonPlayer>();
        public static LocalizedText CrouchText { get; set; }
        public static LocalizedText CrouchAltText { get; set; }
        public string LocalizationCategory => "NPCModifys";
        public float FeedValue = 0;
        public NPC TargetNPC;
        public Player Owner;
        public bool Crouch;
        public bool Mount;
        public Item SaddleItem = new();
        public bool MountACrabulon;
        public bool OnJump;
        public int DontMount;
        public bool hoverNPC;
        private float jumpHeightUpdate;
        private float jumpHeightSetFrame;
        private float dontTurnTo;
        internal int DyeItemID;
        private delegate bool On_Player_Delegate(CrabulonMusicScene crabulonMusicScene, Player player);
        
        public override void Load() {
            MethodInfo methodInfo = typeof(CrabulonMusicScene).GetMethod("IsSceneEffectActive", BindingFlags.Instance | BindingFlags.Public);
            VaultHook.Add(methodInfo, OnCrabulonMusicSceneIsSceneEffectActive);
        }

        //这是一个笨办法，并不优雅，但有效
        private static bool OnCrabulonMusicSceneIsSceneEffectActive(On_Player_Delegate orig, CrabulonMusicScene crabulonMusicScene, Player player) {
            bool reset = false;
            foreach (var npc in Main.ActiveNPCs) {
                if (!npc.boss || npc.type != ModContent.NPCType<Crabulon>()) {
                    continue;
                }
                reset = true;
            }

            if (reset) {
                return orig.Invoke(crabulonMusicScene, player);
            }
            
            return false;
        }

        public override void SetStaticDefaults() {
            CrouchText = this.GetLocalization(nameof(CrouchText), () => "Await");
            CrouchAltText = this.GetLocalization(nameof(CrouchAltText), () => "Follow");
        }

        public override bool? DrawHealthBar(byte hbPosition, ref float scale, ref Vector2 position) {
            if (Mount) {
                return false;
            }
            return null;
        }

        public void Feed(Projectile projectile) {
            DyeItemID = projectile.CWR().DyeItemID;
            npc.lifeMax = Main.masterMode ? 8000 : 6000;
            npc.life = (int)MathHelper.Clamp(npc.life, 0, npc.lifeMax);
            Owner = Main.player[projectile.owner];
            npc.friendly = true;
            npc.npcSlots = 0;
            //每次喂食增加500点驯服值
            FeedValue += 500;
            ai[8] = 120;
            npc.netUpdate = true;
            NetAISend();
        }

        public void SaveData(TagCompound tag) {
            tag["root"] = "root";
            if (FeedValue <= 0f) {
                return;
            }
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

        public void LoadData(TagCompound tag) {
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
                Player player = null;
                foreach (var p in Main.player) {
                    if (!p.active) {
                        continue;
                    }
                    if (p.name != playerName) {
                        continue;
                    }
                    player = p;
                }
                Owner = player;
            }
            if (tag.ContainsKey("k")) {
                SaddleItem = ItemIO.Load(tag.Get<TagCompound>("k"));
            }
        }

        public override bool? On_PreKill() {//死亡后生成沉睡蘑菇人
            FeedValue = 0f;
            CrabulonPlayer.MountCrabulonIndex = -1;
            CrabulonPlayer.IsMount = false;
            CrabulonPlayer.MountDraw = false;

            if (VaultUtils.isClient || FeedValue > 0f) {
                return null;
            }

            if (NPC.AnyNPCs(NPCID.Truffle)) {
                return null;
            }

            ModifyTruffle.GlobalSleepState = true;
            NPC truffle = NPC.NewNPCDirect(npc.FromObjectGetParent(), npc.Center, NPCID.Truffle);
            truffle.velocity = new Vector2(Main.rand.NextFloat(-2, 2), -4);

            return null;
        }

        public override bool FindFrame(int frameHeight) {
            if (FeedValue <= 0f) {
                return true;
            }

            //空中：跳跃 / 下落帧
            if (!npc.collideY) {
                if (npc.velocity.Y < 0 || groundClearance > 100) {
                    ai[11] = MathHelper.Lerp(ai[11], 1, 0.1f);
                }
                else {
                    dontTurnTo = 10;
                    ai[11] = MathHelper.Lerp(ai[11], 4, 0.2f);
                }
                npc.frame.Y = frameHeight * (int)ai[11];//第5帧是下落
                npc.frameCounter = 0;//避免和跑动动画冲突
            }
            else {
                if (Math.Abs(npc.velocity.X) > 0.1f)//跑动
                {
                    npc.frameCounter += Math.Abs(npc.velocity.X) * 0.04;
                    npc.frameCounter %= Main.npcFrameCount[npc.type];
                    int frame = (int)npc.frameCounter;
                    npc.frame.Y = frame * frameHeight;
                }
                else//Idle
                {                  
                    if (ai[9] > 0) {
                        npc.frameCounter += 0.1;
                        npc.frameCounter %= 2;
                    }
                    else {
                        npc.frameCounter += 0.15;
                        npc.frameCounter %= Main.npcFrameCount[npc.type];
                    }

                    int frame = (int)npc.frameCounter;
                    npc.frame.Y = frame * frameHeight;
                }
            }

            return false;
        }

        public override bool AI() {
            //当FeedValue大于0时，进入驯服状态
            if (FeedValue <= 0f) {
                //如果不在驯服状态，则执行原版AI
                return true;
            }

            npc.timeLeft = 1800;
            npc.ModNPC.Music = -1;
            npc.BossBar = ModContent.GetInstance<CrabulonFriendBar>();
            //取消Boss状态，设置为对玩家友好
            npc.boss = false;
            npc.friendly = true;
            npc.damage = 0;

            //如果没有找到可跟随的玩家，则原地减速并进入站立动画
            if (!Owner.Alives()) {
                npc.velocity.X *= 0.9f;
                //使用站立动画
                npc.ai[0] = 0f;
                //返回false以阻止原版AI运行
                return false;
            }

            if (ai[7] > 0) {
                ai[7]--;
            }
            if (ai[8] > 0) {
                ai[8]--;
            }

            if (dontTurnTo > 0) {
                dontTurnTo--;
            }

            GetDistanceToGround();

            hoverNPC = npc.Hitbox.Intersects(Main.MouseWorld.GetRectangle(1));
            if (hoverNPC) {
                Item item = Owner.GetItem();
                if (item.type == ModContent.ItemType<MushroomSaddle>() && item.ModItem is MushroomSaddle saddle) {
                    saddle.ModifyCrabulon = this;
                }
            }

            //首先，默认尝试在地面移动，受重力影响
            npc.noGravity = false;
            npc.noTileCollide = false;

            if (jumpHeightUpdate > 0) {
                jumpHeightSetFrame = 60;
                jumpHeightUpdate -= 14;
                npc.position.Y -= 14;
                npc.noGravity = true;
            }

            if (jumpHeightSetFrame > 0) {
                jumpHeightSetFrame--;
            }

            CrabulonPlayer.IsMount = false;
            if (!MountAI()) {
                return false;
            }

            if (ai[8] > 0) {//刚刚投喂后的等待消化时间
                if (!VaultUtils.isServer) {
                    if (ai[8] == 90) {
                        for (int i = 0; i < 16; i++) {
                            Vector2 spawnPos = npc.position + new Vector2(Main.rand.Next(npc.width), Main.rand.Next(npc.height));
                            PRTLoader.NewParticle<PRT_Nutritional>(spawnPos, Vector2.Zero);
                        }
                    }
                    if (ai[8] == 30) {
                        for (int i = 0; i < 66; i++) {
                            Vector2 spawnPos = npc.position + new Vector2(Main.rand.Next(npc.width), Main.rand.Next(npc.height));
                            PRTLoader.NewParticle<PRT_Nutritional>(spawnPos, Vector2.Zero);
                        }
                    }
                }
                npc.velocity.X /= 2;
                if (npc.collideY) {
                    npc.velocity.Y /= 2;
                }
                npc.ai[0] = 0f;
                return false;
            }

            if (Crouch) {
                if (ai[9] < 60) {
                    ai[9]+=2;
                }
                npc.velocity.X /= 2;
                if (npc.collideY) {
                    npc.velocity.Y /= 2;
                }
                npc.ai[0] = 0f;
                return false;
            }
            else if (ai[9] > 0) {
                ai[9]--;
                npc.ai[0] = 0f;
                return false;
            }

            if (Owner.Distance(npc.Center) > 2600) {
                if (++ai[6] > 160) {
                    ai[6] = 0;
                    npc.Center = Owner.Center + new Vector2(0, -200);//远离后瞬移过来
                    SoundEngine.PlaySound(SoundID.Item8, npc.Center);//魔法瞬移声
                    for (int i = 0; i < 132; i++) {
                        Vector2 dustPos = npc.Bottom + new Vector2(Main.rand.NextFloat(-npc.width, npc.width), 0);
                        int dust = Dust.NewDust(dustPos, 4, 4, DustID.BlueFairy, 0f, -2f, 100, default, 1.5f);
                        Main.dust[dust].velocity *= 0.5f;
                        Main.dust[dust].velocity.Y *= 300f / Main.rand.NextFloat(160, 230);
                        Main.dust[dust].shader = GameShaders.Armor.GetShaderFromItemId(DyeItemID);
                    }
                    return false;
                }
            }

            //定义AI所需的参数
            float moveSpeed = 4f; //移动速度
            float inertia = 15f; //惯性，数值越大，转向和加减速越平滑
            float followDistance = 150f; //开始跟随的水平距离

            //计算从NPC指向玩家的向量
            Vector2 targetPos = Owner.Center;

            TargetNPC = npc.Center.FindClosestNPC(1000, false);
            if (TargetNPC != null) {
                targetPos = TargetNPC.Center;
                followDistance = 100;
                moveSpeed = 8;
            }

            Vector2 toDis = targetPos - npc.Center;

            if (!Collision.CanHitLine(targetPos, 10, 10, npc.Center, 10, 10)) {
                npc.noTileCollide = true;
            }

            //水平移动逻辑
            if (Math.Abs(toDis.X) > followDistance && npc.velocity.Y <= 0) {
                //如果玩家在右边，向右移动
                if (toDis.X > 0) {
                    npc.velocity.X = (npc.velocity.X * inertia + moveSpeed) / (inertia + 1f);
                    npc.direction = 1;
                }
                //如果玩家在左边，向左移动
                else {
                    npc.velocity.X = (npc.velocity.X * inertia - moveSpeed) / (inertia + 1f);
                    npc.direction = -1;
                }
                //使用行走动画，这会触发Crabulon原代码中的FindFrame逻辑来播放对应动画
                npc.ai[0] = 1f;
            }
            else {
                //当足够近时，水平速度逐渐减慢
                npc.velocity.X *= 0.9f;
                //使用站立动画
                npc.ai[0] = 0f;
                if (TargetNPC != null) {//说明锁定了敌人
                    npc.ai[0] = 3f;
                    if (npc.velocity.Y == 0) {
                        npc.velocity.Y -= 12;
                    }
                    else {
                        npc.velocity.Y += 0.2f;
                    }
                    JumpFloorEffect(60, 6f);
                }
            }

            AutoStepClimbing();

            if (dontTurnTo <= 0f) {
                //根据移动方向设置NPC的图像朝向
                npc.spriteDirection = npc.direction;
            }

            if (npc.collideY && targetPos.Y < npc.Bottom.Y - 400 && npc.velocity.Y > -20) {
                npc.velocity.Y = -20;
            }

            if (targetPos.Y < npc.Bottom.Y) {
                ai[7] = 110;
            }

            return false;
        }

        public Vector2 GetMountPos() => npc.Top + new Vector2(0, ai[9] > 0 ? ai[9] : npc.gfxOffY);

        public bool MountAI() {
            if (DontMount > 0) {
                DontMount--;
            }

            if (Mount) {
                CrabulonPlayer.CloseDuringDash(Owner);
                CrabulonPlayer.MountCrabulonIndex = npc.whoAmI;
                CrabulonPlayer.IsMount = true;

                //--- 玩家位置同步 ---
                Owner.Center = GetMountPos();
                Owner.velocity = Vector2.Zero; //禁用玩家自身移动
                if (ai[9] > 0) {
                    ai[9]--;
                    npc.ai[0] = 0f;
                    return false;
                }

                //--- 移动控制 ---
                float accel = 0.5f;     //加速度
                float maxSpeed = 6f;   //最大速度
                float friction = 0.85f; //摩擦系数

                Vector2 input = Vector2.Zero;

                //横向输入
                if (Owner.holdDownCardinalTimer[2] > 2) { //→ 右
                    input.X += 1f;
                }
                if (Owner.holdDownCardinalTimer[3] > 2) { //← 左
                    input.X -= 1f;
                }

                if (Owner.holdDownCardinalTimer[0] == 2 && !Collision.SolidCollision(npc.position, npc.width, npc.height + 20)) {//下平台
                    npc.velocity.Y += 0.2f;
                    if (npc.velocity.Y < 12) {
                        npc.velocity.Y = 12;
                    }
                }

                //跳跃（只在接触地面时生效，防止无限连跳）
                if (Owner.justJumped && npc.collideY) {
                    npc.velocity.Y = -maxSpeed * 4f;
                    OnJump = true;
                }

                JumpFloorEffect();

                //横向速度控制：加速度 + 限速
                if (input.X != 0f) {
                    npc.velocity.X = MathHelper.Clamp(npc.velocity.X + input.X * accel, -maxSpeed, maxSpeed);
                }
                else {
                    //没有输入时逐渐减速
                    npc.velocity.X *= friction;
                    if (Math.Abs(npc.velocity.X) < 0.1f) {
                        npc.velocity.X = 0f;
                    }
                }

                AutoStepClimbing();

                //--- 状态标记（供AI或动画用） ---
                npc.ai[0] = (Math.Abs(npc.velocity.X) > 0.1f) ? 1f : 0f;
                if (Math.Abs(npc.velocity.Y) > 1f) {
                    npc.ai[0] = 3f;
                }

                if (jumpHeightSetFrame > 0) {
                    npc.ai[0] = 1f;
                }

                if (hoverNPC && UIHandleLoader.keyRightPressState == KeyPressState.Pressed) {
                    Mount = false;
                    DontMount = 30;
                    MountACrabulon = false;

                    Owner.fullRotation = 0;
                    Owner.velocity.Y -= 5;
                }

                if (dontTurnTo <= 0f) {
                    //根据移动方向设置NPC的图像朝向
                    npc.spriteDirection = npc.direction = Math.Sign(npc.velocity.X);
                }

                return false; //阻止默认AI
            }
            else {
                //按下交互键骑乘
                if (SaddleItem.Alives() && !MountACrabulon && DontMount <= 0 && hoverNPC && UIHandleLoader.keyRightPressState == KeyPressState.Pressed) {
                    MountACrabulon = true;
                }
                if (MountACrabulon) {
                    Owner.velocity = Owner.Center.To(GetMountPos()).UnitVector() * 8;
                    Owner.CWR().IsRotatingDuringDash = true;
                    Owner.CWR().RotationDirection = Math.Sign(Owner.velocity.X);
                    Owner.CWR().PendingDashRotSpeedMode = 0.06f;
                    Owner.CWR().PendingDashVelocity = Owner.velocity;

                    if (++ai[5] > 60f || Owner.Center.To(GetMountPos()).Length() < Owner.width) {//ai[5]防止某些极端情况下超时
                        ai[5] = 0f;
                        Mount = true;
                        MountACrabulon = false;
                        CrabulonPlayer.MountCrabulonIndex = npc.whoAmI;
                    }
                }
            }

            return true;
        }

        int groundClearance;
        public void GetDistanceToGround() {
            groundClearance = 0;
            Vector2 startPos = npc.Bottom;
            while (true) {
                Vector2 pos = startPos + new Vector2(0, groundClearance);
                Tile tile = Framing.GetTileSafely(pos.ToTileCoordinates16());
                bool hitTile;
                if (CanFallThroughPlatforms() == true) {
                    hitTile = tile.HasSolidTile();
                }
                else {
                    hitTile = tile.HasTile;
                }
                if (hitTile) {
                    break;
                }
                if (groundClearance > 1000) {
                    break;
                }
                groundClearance += 16;
            }
        }

        public void JumpFloorEffect(int checkDis = 300, float slp = 1f) {
            //如果不在地面，累积下落距离
            if (!npc.collideY) {
                ai[3] += Math.Abs(npc.velocity.Y); //累积下落的“速度距离”
                if (npc.velocity.Y < 0) {
                    ai[3] = 0;
                    ai[4] = 0;
                }
                if (ai[3] > ai[4] && npc.velocity.Y > 0) {
                    ai[4] = ai[3]; //记录最大下落强度
                }
            }
            else {
                //落地瞬间检测：上一帧在下落，这一帧接触地面
                if (npc.oldVelocity.Y > 2f && ai[4] > checkDis) {
                    float impactStrength = ai[4] * slp;

                    //播放音效：强度越高，音效音量/音调可变
                    SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.5f + Math.Min(impactStrength / 600f, 0.5f) }, npc.Center);

                    //制造尘土效果
                    int dustCount = (int)MathHelper.Clamp(impactStrength / 30f, 5, 40);
                    for (int i = 0; i < dustCount; i++) {
                        Vector2 dustPos = npc.Bottom + new Vector2(Main.rand.NextFloat(-npc.width, npc.width), 0);
                        int dust = Dust.NewDust(dustPos, 4, 4, DustID.BlueFairy, 0f, -2f, 100, default, 1.5f);
                        Main.dust[dust].velocity *= 0.5f;
                        Main.dust[dust].velocity.Y *= impactStrength / Main.rand.NextFloat(160, 230);
                        Main.dust[dust].shader = GameShaders.Armor.GetShaderFromItemId(DyeItemID);
                    }

                    if (!VaultUtils.isClient) {
                        float multiplicative = Owner.GetDamage(DamageClass.Generic).Multiplicative;
                        int baseDmg = 120 + (int)(impactStrength / 60f);
                        baseDmg = (int)(baseDmg * multiplicative);
                        Projectile.NewProjectile(npc.FromObjectGetParent(), npc.Center, Vector2.Zero
                            , ModContent.ProjectileType<CrabulonFriendHitbox>()
                            , baseDmg, 2, Owner.whoAmI, npc.whoAmI);
                    }
                }

                //落地后清空
                ai[3] = 0;
                ai[4] = 0;
            }
        }

        public void AutoStepClimbing() {
            if (npc.noTileCollide) {
                return;
            }

            if (!npc.collideX || npc.velocity.Y != 0) {
                return;
            }

            int stepHeight = 116;//最大可爬高度（16像素 = 1格）
            bool canStepUp = false;
            int jumpCount = 0;
            //检测前方是否有障碍，但头顶留空
            for (int i = 1; i <= stepHeight / 8; i++) { //逐级检测（8像素一层）
                Vector2 newPos = npc.position - new Vector2(0, i * 8);
                if (!Collision.SolidCollision(newPos, npc.width, npc.height)) {
                    if (i > jumpCount) {
                        jumpCount = i;
                    }
                }
            }

            if (jumpCount > 0) {
                jumpHeightUpdate = jumpCount;
                canStepUp = true;
            }

            if (canStepUp) {
                //避免卡进方块
                npc.velocity.Y /= 2;
            }
        }

        public override bool? CanFallThroughPlatforms() {
            if (Mount && Owner.holdDownCardinalTimer[0] > 2) {
                return true;
            }
            if (ai[7] > 0) {
                return false;
            }
            return null;
        }

        public override void ModifyHoverBoundingBox(ref Rectangle boundingBox) {
            if (Mount) {
                boundingBox = Vector2.Zero.GetRectangle(1);//修改为一个在世界零点位置的非常小的矩形，这样基本不可能摸到
            }
        }

        public override bool CheckActive() {
            return false;
        }

        public void MountDrawPlayer() {
            if (!CrabulonPlayer.IsMount) {
                return;
            }

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointWrap
                , null, Main.Rasterizer, null, Main.GameViewMatrix.ZoomMatrix);
            CrabulonPlayer.MountDraw = true;
            var mountPlayer = Main.player[Owner.whoAmI];
            float originalRotation = mountPlayer.fullRotation;
            mountPlayer.fullRotation = npc.rotation + MathHelper.PiOver2;
            Vector2 oldRotOrigin = mountPlayer.fullRotationOrigin;
            mountPlayer.fullRotationOrigin = mountPlayer.Size / 2f;
            mountPlayer.Center = GetMountPos();
            Main.PlayerRenderer.DrawPlayer(Main.Camera, mountPlayer, mountPlayer.position, mountPlayer.bodyRotation, mountPlayer.fullRotationOrigin);
            mountPlayer.fullRotation = originalRotation;
            mountPlayer.fullRotationOrigin = oldRotOrigin;
            CrabulonPlayer.MountDraw = false;
            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointWrap
                , null, Main.Rasterizer, null, Main.GameViewMatrix.ZoomMatrix);
        }

        public override bool? Draw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            if (Mount && Owner != null) {
                MountDrawPlayer();
            }
            if (DyeItemID > 0) {
                CWRItem.AddByDyeEffectByWorld(npc, DyeItemID);
            }
            if (ai[9] > 0) {
                npc.gfxOffY = ai[9];
            }
            return null;
        }

        public override bool PostDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            if (DyeItemID > 0) {
                CWRItem.CloseByDyeEffectByWorld();//结束关于Draw开启的着色效果
            }

            if (SaddleItem.Alives()) {
                CWRItem.AddByDyeEffectByWorld(npc, SaddleItem.CWR().DyeItemID);
                spriteBatch.Draw(MushroomSaddle.MushroomSaddlePlace.Value, npc.Top + new Vector2(0, 16 + npc.gfxOffY) - Main.screenPosition, null, drawColor
                , npc.rotation, MushroomSaddle.MushroomSaddlePlace.Size() / 2, 1f, SpriteEffects.None, 0);
                CWRItem.CloseByDyeEffectByWorld();
            }
            return true;
        }
    }

    internal class CrabulonPlayer : PlayerOverride
    {
        ///<summary>
        ///骑乘的菌生蟹索引
        ///</summary>
        public int MountCrabulonIndex;
        private int oldMountCrabulonIndex;
        public bool IsMount;
        public bool MountDraw;
        public NPC LatelyCrabulon;
        public override void ResetEffects() {
            MountCrabulonIndex = -1;
            LatelyCrabulon = null;
        }
        public static void CloseDuringDash(Player player) {
            CWRPlayer modPlayer = player.CWR();
            player.fullRotation = 0;
            modPlayer.IsRotatingDuringDash = false;
            modPlayer.RotationResetCounter = 15;
            modPlayer.RotationDirection = player.direction;
            modPlayer.DashCooldownCounter = 95;
            modPlayer.CustomCooldownCounter = 90;
        }
        public override void PostUpdate() {
            if (MountCrabulonIndex == -1) {
                IsMount = false;
                MountDraw = false;
                if (oldMountCrabulonIndex != -1) {
                    CloseDuringDash(Player);
                }
            }

            foreach (var npc in Main.ActiveNPCs) {
                if (npc.boss || npc.type != ModContent.NPCType<Crabulon>()) {
                    continue;
                }
                LatelyCrabulon = npc;
            }

            oldMountCrabulonIndex = MountCrabulonIndex;
        }
        public override bool PreDrawPlayers(Camera camera, IEnumerable<Player> players) {
            if (!IsMount) {
                return true;
            }

            foreach (Player player in players) {
                if (player.whoAmI != Player.whoAmI) {
                    continue;
                }
                return false;
            }

            return true;
        }
    }

    internal class CrabulonFriendHitbox : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;
        public override void SetDefaults() {
            Projectile.width = 296;
            Projectile.height = 196;
            Projectile.hostile = false;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Generic;
            Projectile.timeLeft = 2;
            Projectile.penetrate = -1;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            if (((int)Projectile.ai[0]).TryGetNPC(out var npc)) {
                Projectile.Center = npc.Center;
            }
        }
    }
}
