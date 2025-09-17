using CalamityMod.NPCs.Crabulon;
using CalamityMod.Systems;
using CalamityOverhaul.Content.Items.Tools;
using InnoVault.GameSystem;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using System.Collections.Generic;
using System.Reflection;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.Graphics;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.Modifys
{
    internal class ModifyCrabulon : NPCOverride//驯养菌生蟹，不依赖生物大修
    {
        public override int TargetID => ModContent.NPCType<Crabulon>();
        public CrabulonPlayer CrabulonPlayer => Owner.GetOverride<CrabulonPlayer>();
        public float FeedValue = 0;
        public NPC TargetNPC;
        public Player Owner;
        public bool Crouch;
        public bool Mount;
        public bool Saddle;
        public bool MountACrabulon;
        public bool OnJump;
        public int DontMount;
        public bool hoverNPC;
        private float jumpHeightUpdate;
        private float jumpHeightUpdate2;
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

        public override bool? DrawHealthBar(byte hbPosition, ref float scale, ref Vector2 position) {
            if (Mount) {
                return false;
            }
            return null;
        }

        public void Feed(int i) {
            npc.lifeMax = Main.masterMode ? 8000 : 6000;
            npc.life = (int)MathHelper.Clamp(npc.life, 0, npc.lifeMax);
            Owner = Main.player[i];
            npc.friendly = true;
            npc.npcSlots = 0;
            //每次喂食增加500点驯服值
            FeedValue += 500;
        }

        public override bool? On_PreKill() {//死亡后生成沉睡蘑菇人
            FeedValue = 0f;
            CrabulonPlayer.MountCrabulonIndex = -1;
            CrabulonPlayer.IsMount = false;
            CrabulonPlayer.MountDraw = false;

            if (Main.hardMode || VaultUtils.isClient || FeedValue > 0f) {
                return null;
            }

            if (NPC.AnyNPCs(NPCID.Truffle) || NPC.AnyNPCs(ModContent.NPCType<SleepTruffle>())) {
                return null;
            }

            NPC truffle = NPC.NewNPCDirect(npc.FromObjectGetParent(), npc.Center, ModContent.NPCType<SleepTruffle>());
            truffle.velocity = new Vector2(Main.rand.NextFloat(-2, 2), -4);

            return null;
        }

        public override bool FindFrame(int frameHeight) {
            if (FeedValue > 0f && Mount) {
                //空中：跳跃 / 下落帧
                if (!npc.collideY) {
                    if (npc.velocity.Y < 0) {
                        ai[11] = MathHelper.Lerp(ai[11], 1, 0.1f);
                    }
                    else {
                        ai[11] = MathHelper.Lerp(ai[11], 4, 0.2f);
                    }
                    npc.frame.Y = frameHeight * (int)ai[11];//第5帧是下落
                    npc.frameCounter = 0;//避免和跑动动画冲突
                }
                else {
                    if (Math.Abs(npc.velocity.X) > 0.1f)//跑动
                    {
                        npc.frameCounter += Math.Abs(npc.velocity.X) * 0.04;
                        if (npc.frameCounter >= Main.npcFrameCount[npc.type])
                            npc.frameCounter = 0;

                        int frame = (int)npc.frameCounter % 4;//0~3帧是跑动动画
                        npc.frame.Y = frame * frameHeight;
                    }
                    else//Idle
                    {
                        npc.frameCounter += 0.05;
                        if (npc.frameCounter >= Main.npcFrameCount[npc.type])
                            npc.frameCounter = 0;

                        int frame = (int)npc.frameCounter % 2;//0~1帧是Idle动画
                        npc.frame.Y = frame * frameHeight;
                    }
                }
            }
            return true;
        }

        public Vector2 GetMountPos() => npc.Top + new Vector2(0, npc.gfxOffY);

        public bool MountAI() {
            if (DontMount > 0) {
                DontMount--;
            }

            if (!Mount) {
                //按下交互键骑乘
                if (!MountACrabulon && DontMount <= 0 && hoverNPC && UIHandleLoader.keyRightPressState == KeyPressState.Pressed) {
                    MountACrabulon = true;
                }
                if (MountACrabulon) {
                    Owner.CWR().IsRotatingDuringDash = true;
                    Owner.velocity = Owner.Center.To(GetMountPos()).UnitVector() * 8;
                    Owner.CWR().RotationDirection = Math.Sign(Owner.velocity.X);
                    Owner.CWR().PendingDashRotSpeedMode = 0.06f;
                    Owner.CWR().PendingDashVelocity = Owner.velocity;
                    //Owner.Center = Vector2.Lerp(Owner.Center, GetMountPos(), 0.02f);
                    if (Owner.Center.To(GetMountPos()).Length() < Owner.width) {
                        Mount = true;
                        MountACrabulon = false;
                        CrabulonPlayer.MountCrabulonIndex = npc.whoAmI;
                    }

                    if (++ai[5] > 60) {
                        if (ai[5] == 60) {
                            CrabulonPlayer.CloseDuringDash(Owner);
                        }
                        ai[5] = 0;
                        MountACrabulon = false;//防止某些极端情况下的超时
                    }
                }
            }
            else {
                CrabulonPlayer.CloseDuringDash(Owner);
                CrabulonPlayer.MountCrabulonIndex = npc.whoAmI;
                CrabulonPlayer.IsMount = true;
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

                if (jumpHeightUpdate2 > 0) {
                    npc.ai[0] = 1f;
                }

                //--- 玩家位置同步 ---
                Owner.Center = GetMountPos();
                Owner.velocity = Vector2.Zero; //禁用玩家自身移动

                if (hoverNPC && UIHandleLoader.keyRightPressState == KeyPressState.Pressed) {
                    Mount = false;
                    DontMount = 30;
                    MountACrabulon = false;

                    Owner.fullRotation = 0;
                    Owner.velocity.Y -= 5;
                }

                //根据移动方向设置NPC的图像朝向
                npc.spriteDirection = npc.direction = Math.Sign(npc.velocity.X);
                return false; //阻止默认AI
            }

            return true;
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
            if (Mount) {
                if (npc.velocity.Y < 0 || Owner.holdDownCardinalTimer[0] > 2) {
                    return true;
                }
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

            hoverNPC = npc.Hitbox.Intersects(Main.MouseWorld.GetRectangle(1));
            //首先，默认尝试在地面移动，受重力影响
            npc.noGravity = false;
            npc.noTileCollide = false;

            if (!Collision.CanHitLine(Owner.Center, 10, 10, npc.Center, 10, 10)) {
                npc.noTileCollide = true;
            }

            if (jumpHeightUpdate > 0) {
                jumpHeightUpdate2 = 60;
                jumpHeightUpdate -= 14;
                npc.position.Y -= 14;
                npc.noGravity = true;
            }

            if (jumpHeightUpdate2 > 0) {
                jumpHeightUpdate2--;
            }

            CrabulonPlayer.IsMount = false;
            if (!MountAI()) {
                return false;
            }

            if (Crouch) {
                npc.velocity.X /= 2;
                npc.velocity.Y /= 2;
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

            //根据移动方向设置NPC的图像朝向
            npc.spriteDirection = npc.direction;

            if (npc.collideY && targetPos.Y < npc.Bottom.Y - 400 && npc.velocity.Y > -20) {
                npc.velocity.Y = -20;
            }

            return false;
        }

        public void HoverDraw() {
            if (!hoverNPC) {
                return;
            }
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
            return null;
        }

        public override bool PostDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            if (Saddle) {
                spriteBatch.Draw(MushroomSaddle.MushroomSaddlePlace.Value, npc.Top + new Vector2(0, 16) - Main.screenPosition, null, drawColor
                , npc.rotation, MushroomSaddle.MushroomSaddlePlace.Size() / 2, 1f, SpriteEffects.None, 0);
            }
            HoverDraw();
            return true;
        }
    }

    internal class CrouchBotton : UIHandle
    {
        private bool isCrouch;
        private float sengs;
        private NPC crabulon;
        private ModifyCrabulon modify;
        internal bool Opne {
            get {
                crabulon = player.GetOverride<CrabulonPlayer>().LatelyCrabulon;
                if (crabulon == null) {
                    return false;
                }
                modify = crabulon.GetOverride<ModifyCrabulon>();
                return crabulon.DistanceSQ(player.Center) < 90000 && !modify.Mount;
            }
        }
        public override bool Active => Opne || sengs > 0f;
        public override void Update() {
            Vector2 size = new Vector2(100, 40);
            DrawPosition = new Vector2(Main.screenWidth / 2, Main.screenHeight / 12 * 11) - size / 2;
            UIHitBox = DrawPosition.GetRectangle(size);

            if (!Opne) {
                if (sengs > 0f) {
                    sengs -= 0.1f;
                }
                return;
            }
            else {
                if (sengs < 1f) {
                    sengs += 0.1f;
                }
            }

            hoverInMainPage = UIHitBox.Intersects(MouseHitBox);

            isCrouch = modify.Crouch;

            if (hoverInMainPage && keyLeftPressState == KeyPressState.Pressed) {
                modify.Crouch = !modify.Crouch;
            }

            if (modify.hoverNPC) {
                Item itme = player.GetItem();
                if (itme.type == ModContent.ItemType<MushroomSaddle>() && keyLeftPressState == KeyPressState.Pressed) {
                    modify.Saddle = true;
                }
            }
        }

        public override void Draw(SpriteBatch spriteBatch) {
            VaultUtils.DrawBorderedRectangle(spriteBatch, CWRAsset.UI_JAR.Value, 10, UIHitBox, Color.AliceBlue * sengs, Color.Wheat * sengs);
            string content = isCrouch ? "跟随" : "等待";
            float textScale = 1.2f;
            Vector2 textSize = FontAssets.MouseText.Value.MeasureString(content) * textScale;
            Vector2 drawPos = DrawPosition + UIHitBox.Size() / 2 - textSize / 2;
            Utils.DrawBorderStringFourWay(Main.spriteBatch, FontAssets.MouseText.Value, content
                        , drawPos.X, drawPos.Y, Color.White * sengs, Color.Black * sengs, Vector2.Zero, textScale);

            if (Opne && modify.hoverNPC) {
                Item itme = player.GetItem();
                if (itme.type == ModContent.ItemType<MushroomSaddle>()) {
                    VaultUtils.SimpleDrawItem(spriteBatch, itme.type, MousePosition + new Vector2(0, 32), 32, 1f, 0, Color.White);
                }
            }
        }
    }

    internal class CrabulonMountBar : UIHandle
    {
        public bool Open => player.GetOverride<CrabulonPlayer>().MountCrabulonIndex != -1;
        private float sengs;
        public override bool Active => Open || sengs > 0f;
        public static readonly List<CrabulonLife> crabulonLives = [];
        public NPC npc;
        public const int liveMargin = 4;
        public const int crabulonLiveCount = 20;
        public const int crabulonLiveColumn = 2;
        public const int crabulonLiveLine = crabulonLiveCount / crabulonLiveColumn;
        public override void OnEnterWorld() {
            crabulonLives.Clear();
            for (int i = 0; i < crabulonLiveCount; i++) {
                crabulonLives.Add(new CrabulonLife() { index = i} );
            }
        }
        public override void Update() {
            if (!Open) {
                if (sengs > 0f) {
                    sengs -= 0.1f;
                }
                return;
            }
            else {
                if (sengs < 1f) {
                    sengs += 0.1f;
                }
            }

            if (!player.GetOverride<CrabulonPlayer>().MountCrabulonIndex.TryGetNPC(out npc)) {
                return;
            }

            Vector2 lifeSize = CrabulonLife.Life.Size();

            Vector2 uiSize = new Vector2((lifeSize.X + liveMargin) * crabulonLiveLine, (lifeSize.Y + liveMargin) * crabulonLiveColumn);

            DrawPosition = new Vector2(((int)(Main.screenWidth / 2 - uiSize.X / 2)), ((int)(Main.screenHeight / 2 + uiSize.X / 2 + Main.screenHeight / 10 * 1)));

            UIHitBox = DrawPosition.GetRectangle(uiSize);

            hoverInMainPage = UIHitBox.Intersects(MouseHitBox);

            DrawPosition.Y = MathHelper.Clamp(DrawPosition.Y, 0, UIHitBox.Y);

            for (int i = 0; i < crabulonLiveCount; i++) {
                var crabulonLive = crabulonLives[i];
                crabulonLive.DrawPosition = DrawPosition + CrabulonLife.Life.Size() / 2;
                crabulonLive.npc = npc;
                crabulonLive.DrawPosition.X += (i % crabulonLiveLine) * (lifeSize.X + liveMargin);
                crabulonLive.DrawPosition.Y += (i / crabulonLiveLine) * (lifeSize.Y + liveMargin);
                crabulonLive.sengs = sengs;
                crabulonLive.Update();
            }
        }

        public override void Draw(SpriteBatch spriteBatch) {
            if (!npc.Alives()) {
                return;
            }

            foreach (var crabulonLive in crabulonLives) {
                crabulonLive.Draw(spriteBatch);
            }

            if (hoverInMainPage) {
                string content = $"{npc.life}/{npc.lifeMax}";
                float textScale = 1f;
                Vector2 textSize = FontAssets.MouseText.Value.MeasureString(content) * textScale;
                Vector2 drawPos = MousePosition + new Vector2(0, 36);
                Utils.DrawBorderStringFourWay(Main.spriteBatch, FontAssets.MouseText.Value, content
                            , drawPos.X, drawPos.Y, Color.White, Color.Black, new Vector2(0.3f), textScale);
            }
        }
    }

    internal class CrabulonLife : UIHandle
    {
        [VaultLoaden(CWRConstant.UI + "CrabulonLife")]
        public static Asset<Texture2D> Life;
        public override LayersModeEnum LayersMode => LayersModeEnum.None;

        public int lifeValue; //存储此生命单元当前拥有的生命值
        public int index;     //此生命单元的索引
        public NPC npc;       //关联的NPC

        internal float sengs;

        //用于实现动态效果的私有字段
        private float shakeTime;        //抖动效果的持续时间计时器
        private float dynamicScale = 1f;    //用于“濒危”状态的动态缩放
        private float dynamicRotation;  //用于抖动的动态旋转
        private Vector2 shakeOffset = Vector2.Zero; //用于抖动的动态位置偏移

        public override void Update() {
            //确保我们有一个有效的NPC实例
            if (npc == null || !npc.active) {
                return;
            }

            //计算每个生命单元能代表的最大生命值
            int maxLifePerUnit = npc.lifeMax / CrabulonMountBar.crabulonLiveCount;
            if (maxLifePerUnit <= 0) { //避免除以零的错误
                return;
            }

            //计算当前帧此单元“应该”拥有的生命值
            int newLifeValue = (int)MathHelper.Clamp(npc.life - index * maxLifePerUnit, 0, maxLifePerUnit);

            //--- 1. 实现掉血抖动效果 ---
            //如果新计算的生命值比上一帧的要低，说明掉血了
            if (newLifeValue < lifeValue) {
                shakeTime = 20f; //启动一个持续20帧的抖动效果
            }

            //如果抖动计时器正在生效
            if (shakeTime > 0) {
                shakeTime--;
                float intensity = shakeTime / 20f; //抖动强度随时间衰减
                                                   //生成随机的位置偏移和旋转
                shakeOffset = Main.rand.NextVector2Circular(intensity * 4f, intensity * 4f);
                dynamicRotation = Main.rand.NextFloat(-0.2f, 0.2f) * intensity;
            }
            else {
                //效果结束后，恢复默认值
                shakeOffset = Vector2.Zero;
                dynamicRotation = 0f;
            }

            //更新当前生命值，以便下一帧进行比较
            lifeValue = newLifeValue;

            //--- 2. 实现濒危颤抖效果 ---
            float lifePercent = (float)lifeValue / maxLifePerUnit;

            //如果生命值在0%到35%之间，则触发效果
            if (lifePercent > 0 && lifePercent < 0.35f) {
                float pulseSpeed = 12f; //颤抖速度
                float pulseIntensity = 0.12f; //颤抖幅度
                                              //使用正弦函数制造平滑的、循环的缩放动画
                dynamicScale = 1f + (float)Math.Sin(Main.GameUpdateCount * (pulseSpeed / 60f)) * pulseIntensity;
            }
            else {
                dynamicScale = 1f; //不在危险区域时，恢复默认大小
            }
        }

        public override void Draw(SpriteBatch spriteBatch) {
            if (npc == null || !npc.active) {
                return;
            }

            int maxLifePerUnit = npc.lifeMax / CrabulonMountBar.crabulonLiveCount;
            if (maxLifePerUnit <= 0) {
                return;
            }

            //计算此单元的填充比例，用于决定基础大小和颜色
            float fillRatio = (float)lifeValue / maxLifePerUnit;

            //如果生命完全耗尽，则不绘制
            if (fillRatio <= 0) {
                return;
            }

            //颜色会随着生命值降低而变暗
            Color drawColor = Color.White * fillRatio;
            drawColor.A = (byte)(255 * (0.2f + fillRatio * 0.8f));

            //最终的绘制大小 = 基础大小 * 动态缩放
            float finalScale = 0.5f + fillRatio * dynamicScale * 0.5f;

            //最终的绘制位置 = 基础位置 + 抖动偏移
            Vector2 finalDrawPosition = DrawPosition + shakeOffset;

            //使用所有动态参数进行绘制
            spriteBatch.Draw(Life.Value, finalDrawPosition, null, drawColor * sengs, dynamicRotation, Life.Size() / 2, finalScale, SpriteEffects.None, 0);
        }
    }

    internal class CrabulonFriendBar : ModBossBar
    {
        public override bool PreDraw(SpriteBatch spriteBatch, NPC npc, ref BossBarDrawParams drawParams) {
            if (npc.TryGetOverride<ModifyCrabulon>(out var modifyCrabulon)) {
                if (modifyCrabulon.FeedValue > 0f) {
                    return false;
                }
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

        public override void AI() {
            if (((int)Projectile.ai[0]).TryGetNPC(out var npc)) {
                Projectile.Center = npc.Center;
            }
        }

        public override bool ShouldUpdatePosition() => false;
    }
}
