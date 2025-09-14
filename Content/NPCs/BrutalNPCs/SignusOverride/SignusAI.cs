using CalamityMod;
using CalamityMod.Dusts;
using CalamityMod.Events;
using CalamityMod.Items.Accessories;
using CalamityMod.NPCs;
using CalamityMod.NPCs.Signus;
using CalamityMod.Projectiles.Boss;
using CalamityMod.World;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.Graphics.Shaders;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.SignusOverride
{
    internal class SignusAI : CWRNPCOverride
    {
        public override int TargetID => ModContent.NPCType<Signus>();

        //CWR的AI重写通常都使用枚举状态机来管理AI，这更加清晰易读
        private enum AIState
        {
            SpawnAnimation,     //生成动画，淡入
            Idle,               //在攻击之间盘旋并选择下一个攻击模式
            TeleportAndScythe,  //瞬移到玩家背后并发射镰刀弹幕
            ScytheSweep,        //移动到屏幕角落，释放扫荡全屏的镰刀波
            Minefield,          //召唤宇宙地雷阵，限制玩家走位
            PhantomDash,        //蓄力并进行一次毁灭性的冲刺
            PhaseTransition     //阶段转换时的演出
        }

        //使用属性来简化对NPC.ai数组的访问，并使其更具可读性
        private AIState CurrentState {
            get => (AIState)npc.ai[0];
            set => npc.ai[0] = (float)value;
        }

        private ref float StateTimer => ref npc.ai[1];
        private ref float AttackCounter => ref npc.ai[2];
        private ref float Phase => ref npc.ai[3];

        //一些用于存储动态值的变量
        private int lifeToAlpha = 0;
        private int stealthTimer = 0;

        //保留原有的材质引用
        public static Asset<Texture2D> AltTexture => Signus.AltTexture;
        public static Asset<Texture2D> AltTexture2 => Signus.AltTexture2;
        public static Asset<Texture2D> Texture_Glow => Signus.Texture_Glow;
        public static Asset<Texture2D> AltTexture_Glow => Signus.AltTexture_Glow;
        public static Asset<Texture2D> AltTexture2_Glow => Signus.AltTexture2_Glow;

        public override bool AI() {
            CalamityGlobalNPC.signus = npc.whoAmI;

            //获取目标玩家
            Player player = Main.player[npc.target];
            if (!ValidateTarget(player)) {
                //如果目标无效，执行默认的离场逻辑
                DefaultDespawn();
                return false;
            }
            npc.timeLeft = 1800; //保持Boss存活

            //难度和模式相关的变量
            bool bossRush = BossRushEvent.BossRushActive;
            bool death = CalamityWorld.death || bossRush;
            bool revenge = CalamityWorld.revenge || bossRush;
            bool expertMode = Main.expertMode || bossRush;
            double lifeRatio = npc.life / (double)npc.lifeMax;

            //根据生命值调整透明度，这是一个很好的视觉反馈
            lifeToAlpha = (int)((Main.getGoodWorld ? 200D : 100D) * (1D - lifeRatio));
            npc.alpha = Math.Max(npc.alpha, lifeToAlpha);

            //阶段转换逻辑
            if (Phase == 0 && lifeRatio < 0.75) {
                Phase = 1;
                CurrentState = AIState.PhaseTransition;
                StateTimer = 0;
                npc.netUpdate = true;
            }
            if (Phase == 1 && lifeRatio < 0.4) {
                Phase = 2;
                CurrentState = AIState.PhaseTransition;
                StateTimer = 0;
                npc.netUpdate = true;
            }

            //天顶世界潜行机制
            HandleStealth(npc, player);
            //CurrentState.Domp();
            //主AI状态机
            switch (CurrentState) {
                case AIState.SpawnAnimation:
                    DoSpawnAnimation(npc);
                    break;
                case AIState.Idle:
                    DoIdle(npc, player, death);
                    break;
                case AIState.TeleportAndScythe:
                    DoTeleportAndScythe(npc, player, death, revenge);
                    break;
                case AIState.ScytheSweep:
                    DoScytheSweep(npc, player, death, revenge);
                    break;
                case AIState.Minefield:
                    DoMinefield(npc, player, expertMode);
                    break;
                case AIState.PhantomDash:
                    DoPhantomDash(npc, player, death, bossRush);
                    break;
                case AIState.PhaseTransition:
                    DoPhaseTransition(npc);
                    break;
            }

            //通用旋转和朝向
            if (CurrentState != AIState.PhantomDash && CurrentState != AIState.ScytheSweep) {
                Vector2 toPlayer = npc.To(player.Center);
                float dirVelocity = npc.velocity.X;
                if (npc.velocity.X == 0) {
                    dirVelocity = toPlayer.UnitVector().X;
                }
                npc.rotation = dirVelocity * 0.05f;
                npc.spriteDirection = npc.direction = Math.Sign(dirVelocity);
            }
            else if (CurrentState == AIState.ScytheSweep) {
                Vector2 toPlayer = npc.To(player.Center);
                float dirVelocity = toPlayer.UnitVector().X;
                npc.rotation = toPlayer.ToRotation();
                npc.spriteDirection = npc.direction = Math.Sign(dirVelocity);
                if (npc.spriteDirection == -1) {
                    npc.rotation += MathHelper.Pi;
                }
            }

            FindFrame();

            return false; //阻止原版AI运行
        }

        //验证目标是否有效
        private bool ValidateTarget(Player player) {
            if (player.dead || !player.active || Vector2.Distance(player.Center, npc.Center) > 6400f) {
                npc.TargetClosest(false);
                player = Main.player[npc.target];
                return !player.dead && player.active && Vector2.Distance(player.Center, npc.Center) < 6400f;
            }
            return true;
        }

        //默认的离场逻辑
        private void DefaultDespawn() {
            npc.velocity.Y -= 0.15f;
            if (npc.velocity.Y < -12f)
                npc.velocity.Y = -12f;
            if (npc.timeLeft > 60)
                npc.timeLeft = 60;
        }

        //处理天顶世界的潜行机制
        private void HandleStealth(NPC NPC, Player player) {
            if (!Main.zenithWorld) return;

            int maxStealth = 360;
            int stealthSoundGate = 300;

            if (stealthTimer < maxStealth) {
                stealthTimer++;
            }
            if (stealthTimer == stealthSoundGate) {
                SoundEngine.PlaySound(CalamityMod.CalPlayer.CalamityPlayer.RogueStealthSound, NPC.Center);
            }
        }

        //是否触发潜行强化攻击
        private bool IsStealthed() {
            if (Main.zenithWorld && stealthTimer >= 360) {
                stealthTimer = 0;
                SoundEngine.PlaySound(RaidersTalisman.StealthHitSound, npc.Center);
                return true;
            }
            return false;
        }

        //选择下一个攻击模式
        private void SelectNextAttack(NPC NPC) {
            //根据不同阶段，攻击模式有所区别
            int[] attackPool = Phase < 1 ? [1, 2, 3] : [1, 2, 3, 4];
            int nextAttack;
            do {
                nextAttack = Main.rand.Next(attackPool);
            } while (nextAttack == (int)AttackCounter); //避免连续使用相同的攻击

            switch (nextAttack) {
                case 1:
                    CurrentState = AIState.TeleportAndScythe;
                    break;
                case 2:
                    CurrentState = AIState.ScytheSweep;
                    break;
                case 3:
                    CurrentState = AIState.Minefield;
                    break;
                case 4:
                    CurrentState = AIState.PhantomDash;
                    break;
            }

            AttackCounter = nextAttack; //记录本次攻击，以便下次不重复
            StateTimer = 0;
            NPC.netUpdate = true;
        }

        //生成动画状态
        private void DoSpawnAnimation(NPC NPC) {
            NPC.damage = 0;
            NPC.alpha -= 5;
            if (NPC.alpha < lifeToAlpha) {
                NPC.alpha = lifeToAlpha;
                CurrentState = AIState.Idle;
                StateTimer = 0;
                NPC.netUpdate = true;
            }
        }

        //待机状态
        private void DoIdle(NPC NPC, Player player, bool death) {
            NPC.damage = 0;
            //缓慢飘向玩家上方的一个位置
            Vector2 targetPos = player.Center + new Vector2(0, -300);
            float speed = death ? 14f : 10f;
            float inertia = 20f;
            NPC.velocity = (NPC.velocity * (inertia - 1) + (targetPos - NPC.Center).SafeNormalize(Vector2.Zero) * speed) / inertia;

            StateTimer++;
            //等待一段时间后选择下一次攻击
            float waitTime = Phase > 0 ? 60f : 90f;
            if (StateTimer > waitTime) {
                SelectNextAttack(NPC);
            }
        }

        //瞬移并射击镰刀
        private void DoTeleportAndScythe(NPC NPC, Player player, bool death, bool revenge) {
            NPC.damage = 0;
            StateTimer++;

            float teleportDelay = 45f;
            if (StateTimer < teleportDelay) {
                //预警阶段，在即将瞬移的位置产生特效
                if (StateTimer == 1f) {
                    Vector2 teleportPos = player.Center + player.velocity.SafeNormalize(Vector2.Zero) * 200f;
                    //将目标位置存储在 localAI 中
                    NPC.localAI[0] = teleportPos.X;
                    NPC.localAI[1] = teleportPos.Y;
                    NPC.netUpdate = true;
                }

                //在目标位置创建预警特效
                Vector2 futurePos = new Vector2(NPC.localAI[0], NPC.localAI[1]);
                Dust.NewDust(futurePos, 30, 30, (int)CalamityDusts.PurpleCosmilite, 0, 0, 100, default, 2f);

                //自身逐渐消失
                NPC.alpha += 6;
            }
            else if (StateTimer == teleportDelay) {
                //执行瞬移
                SoundEngine.PlaySound(SoundID.Item8, NPC.Center);
                NPC.Center = new Vector2(NPC.localAI[0], NPC.localAI[1]);
                NPC.velocity = Vector2.Zero;
                NPC.alpha = lifeToAlpha;
                NPC.netUpdate = true;

                //潜行强化
                bool stealthed = IsStealthed();

                //发射镰刀
                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    int scytheCount = revenge ? 5 : 3;
                    if (stealthed) scytheCount += 2; //强化

                    Vector2 direction = (player.Center - NPC.Center).SafeNormalize(Vector2.Zero);
                    float spread = MathHelper.ToRadians(stealthed ? 45f : 30f); //强化：更宽的散射范围

                    for (int i = 0; i < scytheCount; i++) {
                        Vector2 perturbedSpeed = direction.RotatedBy(MathHelper.Lerp(-spread, spread, i / (float)(scytheCount - 1))) * (death ? 14f : 12f);
                        int type = ModContent.ProjectileType<SignusScythe>();
                        int damage = NPC.GetProjectileDamage(type);
                        Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, perturbedSpeed, type, damage, 0, Main.myPlayer);
                    }
                }
            }

            //攻击后的硬直
            if (StateTimer > teleportDelay + 30) {
                CurrentState = AIState.Idle;
                StateTimer = 0;
            }
        }

        //镰刀横扫
        private void DoScytheSweep(NPC NPC, Player player, bool death, bool revenge) {
            NPC.damage = 0;
            StateTimer++;

            if (StateTimer == 1f) {
                //移动到屏幕一角
                Vector2 corner = player.Center + new Vector2(Math.Sign(player.Center.X - NPC.Center.X) * 900, -400);
                NPC.localAI[0] = corner.X;
                NPC.localAI[1] = corner.Y;
                NPC.netUpdate = true;
            }

            //飞向目标角落
            Vector2 targetPos = new Vector2(NPC.localAI[0], NPC.localAI[1]);
            NPC.velocity = (targetPos - NPC.Center) * 0.1f;
            NPC.rotation = (player.Center - NPC.Center).ToRotation();
            if (NPC.Center.X < player.Center.X) NPC.rotation += MathHelper.Pi;


            //到达位置后开始攻击
            if (Vector2.Distance(NPC.Center, targetPos) < 50f || StateTimer > 120f) {
                NPC.velocity *= 0.9f;
                //每隔一段时间发射一道镰刀波
                if (StateTimer % (death ? 20 : 30) == 0 && StateTimer > 60) {
                    if (Main.netMode != NetmodeID.MultiplayerClient) {
                        SoundEngine.PlaySound(SoundID.Item71, NPC.Center);
                        Vector2 direction = (player.Center - NPC.Center).SafeNormalize(Vector2.Zero);
                        float speed = revenge ? 11f : 9f;

                        bool stealthed = IsStealthed(); //潜行会影响此次攻击
                        if (stealthed) speed *= 1.5f; //强化：速度更快

                        int type = ModContent.ProjectileType<SignusScythe>();
                        int damage = NPC.GetProjectileDamage(type);
                        Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, direction * speed, type, damage, 0, Main.myPlayer);
                    }
                }
            }

            //结束攻击
            int attackDuration = death ? 240 : 300;
            if (StateTimer > attackDuration) {
                CurrentState = AIState.Idle;
                StateTimer = 0;
            }
        }

        //地雷阵
        private void DoMinefield(NPC NPC, Player player, bool expertMode) {
            StateTimer++;

            //移动到玩家上方并保持不动
            Vector2 hoverPos = player.Center + new Vector2(0, -350);
            NPC.velocity = (hoverPos - NPC.Center) * 0.05f;
            NPC.damage = 0;

            //在计时器特定时刻生成地雷
            if (Main.netMode != NetmodeID.MultiplayerClient && StateTimer > 60 && StateTimer < 180 && StateTimer % 30 == 0) {
                SoundEngine.PlaySound(SoundID.Item122, NPC.Center);

                bool stealthed = IsStealthed(); //潜行会生成更多地雷

                int mineCount = expertMode ? 4 : 3;
                if (stealthed) mineCount += 2; //强化

                for (int i = 0; i < mineCount; i++) {
                    //在玩家周围环形生成
                    float radius = 400f + (stealthed ? 100f : 0f);
                    Vector2 spawnPos = player.Center + Main.rand.NextVector2Circular(radius, radius);
                    int npcType = ModContent.NPCType<CosmicMine>();
                    NPC.NewNPC(NPC.GetSource_FromAI(), (int)spawnPos.X, (int)spawnPos.Y, npcType);
                }
            }

            //结束状态
            if (StateTimer > 240) {
                CurrentState = AIState.Idle;
                StateTimer = 0;
            }
        }

        //幻影冲刺
        private void DoPhantomDash(NPC npc, Player player, bool death, bool bossRush) {
            StateTimer++;
            float chargeVelocity = bossRush ? 28f : death ? 24f : 20f;

            //这个攻击分为三个子阶段：0=准备, 1=冲刺, 2=结束
            float subState = ai[0];

            if (subState == 0) //准备阶段
            {
                npc.damage = 0;
                npc.velocity *= 0.9f;
                if (StateTimer == 1f) {
                    //选择一个屏幕外的点作为冲刺起点
                    Vector2 chargeStartPos = player.Center + new Vector2(1000 * (Main.rand.NextBool() ? 1 : -1), Main.rand.Next(-200, 201));
                    npc.Center = chargeStartPos;

                    //计算冲向玩家的方向
                    Vector2 chargeDir = (player.Center - npc.Center).SafeNormalize(Vector2.Zero);
                    ai[1] = chargeDir.X;
                    ai[2] = chargeDir.Y;
                    npc.netUpdate = true;
                }

                //画出攻击预警线（需要绘制代码支持，这里只做逻辑）
                //在计时器达到60时，进行冲刺
                if (StateTimer > 60) {
                    SoundEngine.PlaySound(SoundID.ForceRoar, npc.Center);
                    Vector2 chargeVel = new Vector2(ai[1], ai[2]) * chargeVelocity;
                    npc.velocity = chargeVel;
                    ai[0] = 1; //切换到冲刺子阶段
                    StateTimer = 0;
                    npc.netUpdate = true;

                    if (IsStealthed()) //潜行强化
                    {
                        npc.velocity *= 1.5f; //强化：冲刺速度极快
                    }
                }
            }
            else if (subState == 1) //冲刺阶段
            {
                npc.damage = npc.defDamage;
                //保持冲刺方向和速度
                npc.rotation = npc.velocity.ToRotation();
                if (npc.velocity.X < 0) npc.rotation += MathHelper.Pi;
                npc.spriteDirection = npc.velocity.X > 0 ? 1 : -1;

                //冲刺60帧后或撞墙后结束
                if (StateTimer > 90 || Collision.SolidCollision(npc.position, npc.width, npc.height)) {
                    ai[0] = 2; //切换到结束子阶段
                    StateTimer = 0;
                    npc.netUpdate = true;
                }
            }
            else if (subState == 2) //结束阶段
            {
                npc.damage = 0;
                npc.velocity *= 0.95f; //缓慢停下
                if (StateTimer > 30) {
                    //重置并返回待机
                    ai[0] = 0;
                    CurrentState = AIState.Idle;
                    StateTimer = 0;
                }
            }
        }

        //阶段转换
        private void DoPhaseTransition(NPC NPC) {
            StateTimer++;
            NPC.damage = 0;
            NPC.velocity *= 0.9f;

            //产生特效和声音
            if (StateTimer == 1) {
                SoundEngine.PlaySound(SoundID.Roar, NPC.Center);
                //无敌
                NPC.dontTakeDamage = true;
            }

            //抖动和粒子效果
            if (Main.rand.NextBool(3)) {
                Dust.NewDust(NPC.position, NPC.width, NPC.height, (int)CalamityDusts.PurpleCosmilite, Main.rand.NextFloat(-5, 5), Main.rand.NextFloat(-5, 5), 100, default, 2.5f);
            }

            if (StateTimer > 90) {
                NPC.dontTakeDamage = false;
                CurrentState = AIState.Idle;
                StateTimer = 0;
                NPC.netUpdate = true;
            }
        }

        private double frameCounter;
        private int frame;
        public void FindFrame() {
           //根据不同的AI状态，我们应用不同的动画逻辑
            frameCounter++;

           //获取当前状态所使用的纹理的总帧数 (原版灾厄Signus所有纹理都是6帧)
            const int totalFrames = 6;
            int frameHeight;
            int frameTicks;//每帧持续的tick数，数值越小动画越快

            switch ((AIState)npc.ai[0]) {
               //幻影冲刺状态: 动画速度最快，体现速度感
                case AIState.PhantomDash:
                    frameTicks = 5;
                    frameHeight = AltTexture2.Value.Height / totalFrames;
                    break;

               //镰刀横扫状态: 动画速度较快
                case AIState.ScytheSweep:
                    frameTicks = 7;
                    frameHeight = AltTexture.Value.Height / totalFrames;
                    break;

               //其他所有状态: 使用标准动画速度
                default:
                    frameTicks = 8;
                    frameHeight = TextureAssets.Npc[npc.type].Value.Height / totalFrames;
                    break;
            }

           //计算当前应该在哪一帧
            frame = (int)(frameCounter / frameTicks) % totalFrames;
        }

        public override bool? Draw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
           //--- 1. 参数准备: 根据当前状态选择纹理、残影数量等 ---
            Texture2D npcTexture;
            Texture2D glowMaskTexture;
            int afterimageAmt;

            SpriteEffects spriteEffects = npc.spriteDirection == 1 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;

            switch ((AIState)npc.ai[0]) {
                case AIState.PhantomDash://冲刺状态
                    npcTexture = AltTexture2.Value;
                    glowMaskTexture = AltTexture2_Glow.Value;
                    afterimageAmt = 10;//最多残影，凸显速度
                    break;
                case AIState.ScytheSweep://横扫状态
                    npcTexture = AltTexture.Value;
                    glowMaskTexture = AltTexture_Glow.Value;
                    afterimageAmt = 7;//较多残影
                    break;
                default://其他状态
                    npcTexture = TextureAssets.Npc[npc.type].Value;
                    glowMaskTexture = Texture_Glow.Value;
                    afterimageAmt = 5;//普通残影
                    break;
            }

           //--- 2. 计算通用绘制变量 ---
            Rectangle frameRect = npcTexture.GetRectangle(frame, 6);
           //纹理中心点，用于旋转和定位
            Vector2 origin = frameRect.Size() / 2f;
            float scale = npc.scale;
            float rotation = npc.rotation;

           //天顶世界潜行透明度计算
            float transparency = 1f;
            if (stealthTimer >= 300) {
                transparency = (float)Math.Sin(Math.PI * (stealthTimer - 300) / 60.0) * 0.5f + 0.5f;
                transparency = MathHelper.Clamp(1f - (stealthTimer - 300) / 60f, 0f, 1f);
            }

           //--- 3. 绘制残影 ---
            if (CalamityConfig.Instance.Afterimages) {
               //绘制身体残影
                for (int i = 1; i < afterimageAmt; i += 2) {
                    Color afterimageColor = npc.GetAlpha(drawColor) * ((afterimageAmt - i) / (float)afterimageAmt) * 0.5f;
                    Vector2 afterimagePos = npc.oldPos[i] + npc.Size / 2f - screenPos;
                    spriteBatch.Draw(npcTexture, afterimagePos, frameRect, afterimageColor * transparency, rotation, origin, scale, spriteEffects, 0f);
                }
               //绘制发光残影
                for (int j = 1; j < afterimageAmt; j++) {
                    Color eyeAfterimageColor = Color.Lerp(Color.White, Color.Fuchsia, 0.5f) * ((afterimageAmt - j) / (float)afterimageAmt) * 0.5f;
                    if (Main.zenithWorld) eyeAfterimageColor = Color.Lerp(Color.White, Color.MediumBlue, 0.5f) * ((afterimageAmt - j) / (float)afterimageAmt) * 0.5f;
                    Vector2 eyeAfterimagePos = npc.oldPos[j] + npc.Size / 2f - screenPos;
                    spriteBatch.Draw(glowMaskTexture, eyeAfterimagePos, frameRect, eyeAfterimageColor * transparency, rotation, origin, scale, spriteEffects, 0f);
                }
            }

           //--- 4. 绘制本体 ---
            Vector2 drawLocation = npc.Center - screenPos;
           //绘制身体
            spriteBatch.Draw(npcTexture, drawLocation, frameRect, npc.GetAlpha(drawColor) * transparency, rotation, origin, scale, spriteEffects, 0f);

           //--- 5. 绘制发光和特效 ---
            Color eyeGlowColor = Color.Lerp(Color.White, Color.Fuchsia, 0.5f);
            if (Main.zenithWorld) {
                eyeGlowColor = Color.MediumBlue;
            }

           //天顶世界下的特殊描边效果
            if (Main.zenithWorld) {
                CalamityUtils.EnterShaderRegion(spriteBatch);
                Color outlineColor = Color.Lerp(Color.Blue, Color.White, 0.4f);
                Vector3 outlineHSL = Main.rgbToHsl(outlineColor);
                float outlineThickness = 1.5f; //加粗描边让它更明显

                GameShaders.Misc["CalamityMod:BasicTint"].UseOpacity(1f);
                GameShaders.Misc["CalamityMod:BasicTint"].UseColor(Main.hslToRgb(1 - outlineHSL.X, outlineHSL.Y, outlineHSL.Z));
                GameShaders.Misc["CalamityMod:BasicTint"].Apply();

                for (float i = 0; i < MathHelper.TwoPi; i += MathHelper.PiOver4) {
                    spriteBatch.Draw(glowMaskTexture, drawLocation + i.ToRotationVector2() * outlineThickness, frameRect, outlineColor * transparency, rotation, origin, scale, spriteEffects, 0f);
                }
                CalamityUtils.ExitShaderRegion(spriteBatch);
            }

           //绘制眼睛和发光部分
            spriteBatch.Draw(glowMaskTexture, drawLocation, frameRect, eyeGlowColor * transparency, rotation, origin, scale, spriteEffects, 0f);

            return false; //阻止原版绘制
        }
    }
}