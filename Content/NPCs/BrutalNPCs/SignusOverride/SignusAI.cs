using CalamityMod;
using CalamityMod.Dusts;
using CalamityMod.Items.Accessories;
using CalamityMod.NPCs;
using CalamityMod.NPCs.Signus;
using CalamityMod.Projectiles.Boss;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
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
        //为了更具压迫感，我们重新设计了所有攻击模式
        private enum AIState
        {
            SpawnAnimation,         //生成动画，淡入
            Idle,                   //在攻击之间盘旋并选择下一个攻击模式
            ScytheFlurry,           //连续瞬移并释放镰刀弹幕的刺杀模式
            CosmicCrossSweep,       //召唤分身在对角进行交叉镰刀扫射
            MinefieldWeave,         //快速移动并沿途布下宇宙地雷阵
            AssassinsFlurry,        //召唤多个幻影进行连续的毁灭性冲刺
            PhaseTransitionAttack   //阶段转换时释放毁灭性的全屏攻击
        }

        //目标玩家
        private Player Target { get; set; }

        //使用属性来简化对NPC.ai数组的访问，并使其更具可读性
        private AIState CurrentState {
            get => (AIState)npc.ai[0];
            set => npc.ai[0] = (float)value;
        }

        //关键控制属性
        private ref float StateTimer => ref npc.ai[1];
        private ref float AttackCounter => ref npc.ai[2];
        private ref float Phase => ref npc.ai[3];

        //难度和模式相关的变量
        private double LifeRatio => npc.life / (double)npc.lifeMax;

        //一些用于存储动态值的变量
        private int lifeToAlpha = 0;
        private int stealthTimer = 0;

        //动画帧
        private int frame;

        //保留原有的材质引用
        public static Asset<Texture2D> AltTexture => Signus.AltTexture;
        public static Asset<Texture2D> AltTexture2 => Signus.AltTexture2;
        public static Asset<Texture2D> Texture_Glow => Signus.Texture_Glow;
        public static Asset<Texture2D> AltTexture_Glow => Signus.AltTexture_Glow;
        public static Asset<Texture2D> AltTexture2_Glow => Signus.AltTexture2_Glow;

        public override bool AI() {
            CalamityGlobalNPC.signus = npc.whoAmI;

            //获取目标玩家
            Target = Main.player[npc.target];
            if (!ValidateTarget()) {
                //如果目标无效，执行默认的离场逻辑
                DefaultDespawn();
                return false;
            }
            npc.timeLeft = 1800; //保持Boss存活

            //根据生命值调整透明度，这是一个很好的视觉反馈
            lifeToAlpha = (int)((Main.getGoodWorld ? 200D : 100D) * (1D - LifeRatio));
            npc.alpha = Math.Max(npc.alpha, lifeToAlpha);

            //阶段转换逻辑
            if (Phase == 0 && LifeRatio < 0.75) {
                Phase = 1;
                CurrentState = AIState.PhaseTransitionAttack;
                StateTimer = 0;
                AttackCounter = 0;
                npc.netUpdate = true;
            }
            if (Phase == 1 && LifeRatio < 0.4) {
                Phase = 2;
                CurrentState = AIState.PhaseTransitionAttack;
                StateTimer = 0;
                AttackCounter = 0;
                npc.netUpdate = true;
            }

            //天顶世界潜行机制
            HandleStealth();

            //主AI状态机
            switch (CurrentState) {
                case AIState.SpawnAnimation:
                    DoSpawnAnimation();
                    break;
                case AIState.Idle:
                    DoIdle();
                    break;
                case AIState.ScytheFlurry:
                    DoScytheFlurry();
                    break;
                case AIState.CosmicCrossSweep:
                    DoCosmicCrossSweep();
                    break;
                case AIState.MinefieldWeave:
                    DoMinefieldWeave();
                    break;
                case AIState.AssassinsFlurry:
                    DoAssassinsFlurry();
                    break;
                case AIState.PhaseTransitionAttack:
                    DoPhaseTransitionAttack();
                    break;
            }

            UpdateDirection();

            return false; //阻止原版AI运行
        }

        private void UpdateDirection() {
            //通用旋转和朝向
            if (CurrentState != AIState.AssassinsFlurry && CurrentState != AIState.CosmicCrossSweep) {
                Vector2 toPlayer = npc.To(Target.Center);
                float dirVelocity = npc.velocity.X;
                if (npc.velocity.X == 0) {
                    dirVelocity = toPlayer.UnitVector().X;
                }
                npc.rotation = dirVelocity * 0.05f;
                npc.spriteDirection = npc.direction = Math.Sign(dirVelocity);
            }
            else if (CurrentState == AIState.CosmicCrossSweep) {
                //在交叉扫射时，始终朝向玩家
                Vector2 toPlayer = npc.To(Target.Center);
                float dirVelocity = toPlayer.UnitVector().X;
                npc.rotation = toPlayer.ToRotation();
                npc.spriteDirection = npc.direction = Math.Sign(dirVelocity);
                if (npc.spriteDirection == -1) {
                    npc.rotation += MathHelper.Pi;
                }
            }
        }

        //验证目标是否有效
        private bool ValidateTarget() {
            if (Target.dead || !Target.active || Vector2.Distance(Target.Center, npc.Center) > 6400f) {
                npc.TargetClosest(false);
                Target = Main.player[npc.target];
                return !Target.dead && Target.active && Vector2.Distance(Target.Center, npc.Center) < 6400f;
            }
            return true;
        }

        //默认的离场逻辑
        private void DefaultDespawn() {
            npc.velocity.Y -= 0.15f;
            if (npc.velocity.Y < -12f) {
                npc.velocity.Y = -12f;
            }
            if (npc.timeLeft > 60) {
                npc.timeLeft = 60;
            }
        }

        //处理天顶世界的潜行机制
        private void HandleStealth() {
            if (!Main.zenithWorld) {
                return;
            }

            int maxStealth = 360;
            int stealthSoundGate = 300;

            if (stealthTimer < maxStealth) {
                stealthTimer++;
            }
            if (stealthTimer == stealthSoundGate) {
                SoundEngine.PlaySound(CalamityMod.CalPlayer.CalamityPlayer.RogueStealthSound, npc.Center);
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
        private void SelectNextAttack() {
            //根据不同阶段，攻击模式有所区别
            int[] attackPool = Phase < 1 ? [1, 2, 3] : [1, 2, 3, 4];
            int nextAttack;
            do {
                nextAttack = Main.rand.Next(attackPool);
            } while (nextAttack == (int)AttackCounter); //避免连续使用相同的攻击

            switch (nextAttack) {
                case 1:
                    CurrentState = AIState.ScytheFlurry;
                    break;
                case 2:
                    CurrentState = AIState.CosmicCrossSweep;
                    break;
                case 3:
                    CurrentState = AIState.MinefieldWeave;
                    break;
                case 4:
                    CurrentState = AIState.AssassinsFlurry;
                    break;
            }

            AttackCounter = nextAttack; //记录本次攻击，以便下次不重复
            StateTimer = 0;
            npc.netUpdate = true;
        }

        //生成动画状态
        private void DoSpawnAnimation() {
            npc.damage = 0;
            npc.alpha -= 5;
            if (npc.alpha < lifeToAlpha) {
                npc.alpha = lifeToAlpha;
                CurrentState = AIState.Idle;
                StateTimer = 0;
                npc.netUpdate = true;
            }
        }

        //待机状态
        private void DoIdle() {
            npc.damage = 0;
            //更快速地飘向玩家上方的一个位置
            Vector2 targetPos = Target.Center + new Vector2(0, -350);
            float speed = CWRWorld.Death ? 18f : 14f;
            float inertia = 15f;
            npc.velocity = (npc.velocity * (inertia - 1) + (targetPos - npc.Center).SafeNormalize(Vector2.Zero) * speed) / inertia;

            StateTimer++;
            //大幅缩短等待时间，增加压迫感
            float waitTime = Phase > 0 ? 30f : 60f;
            if (StateTimer > waitTime) {
                SelectNextAttack();
            }
        }

        //镰刀乱舞
        private void DoScytheFlurry() {
            npc.damage = 0;
            StateTimer++;

            int teleportCount = Phase > 1 ? 5 : 3; //瞬移次数随阶段增加
            int timePerTeleport = CWRWorld.Death ? 35 : 45; //每次瞬移的间隔

            //在每次瞬移前都逐渐消失
            npc.alpha += 8;
            if (npc.alpha > 250) {
                npc.alpha = 250;
            }

            if (StateTimer % timePerTeleport == 0) {
                //执行瞬移
                SoundEngine.PlaySound(SoundID.Item8, npc.Center);
                //瞬移到玩家周围一个随机的、更近的位置
                Vector2 teleportPos = Target.Center + VaultUtils.RandVr(MathHelper.TwoPi) * Main.rand.Next(600, 800);
                npc.Center = teleportPos;
                npc.velocity = Vector2.Zero;
                npc.alpha = lifeToAlpha;
                npc.netUpdate = true;

                //潜行强化
                bool stealthed = IsStealthed();

                //发射更密集的镰刀
                if (!VaultUtils.isClient) {
                    int scytheCount = CWRWorld.Revenge ? 8 : 6;
                    if (stealthed) {
                        scytheCount += 4; //强化：数量剧增
                    }

                    Vector2 direction = (Target.Center - npc.Center).SafeNormalize(Vector2.Zero);
                    float spread = MathHelper.ToRadians(stealthed ? 60f : 45f); //强化：更宽的散射范围

                    for (int i = 0; i < scytheCount; i++) {
                        Vector2 perturbedSpeed = direction.RotatedBy(MathHelper.Lerp(-spread, spread, i / (float)(scytheCount - 1))) * (CWRWorld.Death ? 15f : 13f);
                        int type = ModContent.ProjectileType<SignusScythe>();
                        int damage = npc.GetProjectileDamage(type);
                        Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, perturbedSpeed, type, damage, 0, Main.myPlayer);
                    }
                }
            }

            //完成所有瞬移攻击后结束
            if (StateTimer > teleportCount * timePerTeleport) {
                CurrentState = AIState.Idle;
                StateTimer = 0;
            }
        }

        //宇宙交叉扫射
        private void DoCosmicCrossSweep() {
            npc.damage = 0;
            StateTimer++;

            if (StateTimer == 1f) {
                //移动到屏幕左上角或右上角
                int direction = Main.rand.NextBool() ? 1 : -1;
                Vector2 corner = Target.Center + new Vector2(direction * 900, -450);
                npc.localAI[0] = corner.X;
                npc.localAI[1] = corner.Y;
                //记录对角位置用于生成幻影
                npc.localAI[2] = Target.Center.X - direction * 900;
                npc.localAI[3] = Target.Center.Y + 450;
                npc.netUpdate = true;
            }

            //飞向目标角落
            Vector2 targetPos = new Vector2(npc.localAI[0], npc.localAI[1]);
            npc.velocity = (targetPos - npc.Center) * 0.1f;

            //到达位置后开始攻击
            if (Vector2.Distance(npc.Center, targetPos) < 100f || StateTimer > 100f) {
                npc.velocity *= 0.95f;
                //更高频率地发射镰刀波
                int fireRate = CWRWorld.Death ? 12 : 18;
                if (Phase > 1) {
                    fireRate -= 4;
                }
                if (StateTimer % fireRate == 0 && StateTimer > 60) {
                    if (!VaultUtils.isClient) {
                        SoundEngine.PlaySound(SoundID.Item71, npc.Center);

                        //本体朝玩家发射
                        Vector2 direction = (Target.Center - npc.Center).SafeNormalize(Vector2.Zero);
                        float speed = CWRWorld.Revenge ? 13f : 11f;

                        bool stealthed = IsStealthed(); //潜行会影响此次攻击
                        if (stealthed) {
                            speed *= 1.5f; //强化：速度更快
                        }

                        int type = ModContent.ProjectileType<SignusScythe>();
                        int damage = npc.GetProjectileDamage(type);
                        Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, direction * speed, type, damage, 0, Main.myPlayer);

                        //幻影在对角同时发射，形成交叉火力
                        Vector2 phantomPos = new Vector2(npc.localAI[2], npc.localAI[3]);
                        Vector2 phantomDirection = (Target.Center - phantomPos).SafeNormalize(Vector2.Zero);
                        Projectile.NewProjectile(npc.GetSource_FromAI(), phantomPos, phantomDirection * speed, type, damage, 0, Main.myPlayer);
                        //在幻影位置产生特效
                        for (int i = 0; i < 10; i++) {
                            Dust.NewDust(phantomPos, 30, 30, (int)CalamityDusts.PurpleCosmilite, 0, 0, 100, default, 1.5f);
                        }
                    }
                }
            }

            //结束攻击
            int attackDuration = CWRWorld.Death ? 200 : 260;
            if (StateTimer > attackDuration) {
                CurrentState = AIState.Idle;
                StateTimer = 0;
            }
        }

        //雷区编织
        private void DoMinefieldWeave() {
            StateTimer++;
            npc.damage = 0;

            //Signus不再静止，而是围绕玩家高速移动，沿途布雷
            float orbitSpeed = 0.05f + (Phase * 0.01f);
            float orbitRadius = 450f;
            Vector2 targetPos = Target.Center + new Vector2(0, -orbitRadius).RotatedBy(StateTimer * orbitSpeed);
            npc.velocity = (targetPos - npc.Center) * 0.1f;


            //在移动过程中持续生成地雷
            if (!VaultUtils.isClient && StateTimer > 30 && StateTimer < 210) {
                int spawnRate = CWRWorld.Death ? 15 : 20;
                if (StateTimer % spawnRate == 0) {
                    SoundEngine.PlaySound(SoundID.Item122, npc.Center);
                    bool stealthed = IsStealthed(); //潜行会生成更危险的地雷

                    //一次生成两颗，位置更随机
                    int mineCount = 2;
                    if (stealthed) {
                        mineCount++; //强化
                    }

                    for (int i = 0; i < mineCount; i++) {
                        //在本体附近生成
                        Vector2 spawnPos = npc.Center + Main.rand.NextVector2Circular(50f, 50f);
                        int npcType = ModContent.NPCType<CosmicMine>();
                        int newNpc = NPC.NewNPC(npc.GetSource_FromAI(), (int)spawnPos.X, (int)spawnPos.Y, npcType);
                        if (stealthed && Main.npc[newNpc].active) {
                            //潜行强化的地雷爆炸范围更大或有二次爆炸 (此处为设想，实际效果需修改地雷AI)
                            Main.npc[newNpc].scale = 1.5f;
                        }
                    }
                }
            }

            //结束状态
            if (StateTimer > 240) {
                CurrentState = AIState.Idle;
                StateTimer = 0;
            }
        }

        //刺客乱舞
        private void DoAssassinsFlurry() {
            StateTimer++;
            float chargeVelocity = CWRWorld.BossRush ? 108f : CWRWorld.Death ? 92f : 80f;
            int dashCount = Phase > 1 ? 8 : 5; //冲刺次数
            int timePerDash = 70; //每次冲刺的准备+执行时间

            //获取当前是第几次冲刺
            int currentDash = (int)(StateTimer / timePerDash);
            float timerInDash = StateTimer % timePerDash;
            bool shootProj = false;

            //这个攻击分为两个子阶段：0-50为准备, 51-70为冲刺
            if (currentDash < dashCount) {
                if (timerInDash == 1f) //准备阶段开始
                {
                    npc.damage = 0;
                    //瞬移到屏幕外的一个随机点
                    Vector2 chargeStartPos = Target.Center + new Vector2(1000 * (Main.rand.NextBool() ? 1 : -1), Main.rand.Next(-300, 301));
                    npc.Center = chargeStartPos;
                    npc.velocity = Vector2.Zero;

                    //计算并存储冲向玩家的方向
                    Vector2 chargeDir = (Target.Center - npc.Center).SafeNormalize(Vector2.Zero);
                    npc.localAI[0] = chargeDir.X;
                    npc.localAI[1] = chargeDir.Y;
                    npc.netUpdate = true;
                }
                else if (timerInDash <= 50f) //持续准备
                {
                    npc.damage = 0;
                    //画出攻击预警线（通过尘埃实现）
                    Vector2 chargeDir = new Vector2(npc.localAI[0], npc.localAI[1]);
                    for (int i = 0; i < 14; i++) {
                        Vector2 dustPos = npc.Center + chargeDir * i * 200f;
                        for (int j = 0; j < 2; j++) {
                            PRT_Light particle = new(dustPos + VaultUtils.RandVr(20), chargeDir.UnitVector() * Main.rand.NextFloat(2f, 15f), Main.rand.NextFloat(0.2f, 1f), Color.Purple, 12);
                            particle.ShouldKillWhenOffScreen = false;
                            PRTLoader.AddParticle(particle);
                        }
                    }
                }
                else if (timerInDash == 51f) //执行冲刺
                {
                    SoundEngine.PlaySound(SoundID.ForceRoar, npc.Center);
                    Vector2 chargeVel = new Vector2(npc.localAI[0], npc.localAI[1]) * chargeVelocity;

                    if (IsStealthed()) //潜行强化
                    {
                        chargeVel *= 1.5f; //强化：冲刺速度极快
                    }

                    npc.velocity = chargeVel;
                    npc.damage = npc.defDamage;
                    npc.netUpdate = true;
                }
                else //冲刺中
                {
                    //写在这里提醒自己，不要被预判线的长度骗了，冲刺距离其实是 (72 - 51) * speed ，并没有多长
                    shootProj = true;
                    //保持冲刺方向和速度
                    npc.rotation = npc.velocity.ToRotation();
                    if (npc.velocity.X < 0) {
                        npc.rotation += MathHelper.Pi;
                    }
                    npc.spriteDirection = npc.velocity.X > 0 ? 1 : -1;
                }
            }
            else //所有冲刺结束
            {
                npc.damage = 0;
                npc.velocity *= 0.95f; //缓慢停下

                if (timerInDash > 30) //停稳后返回待机
                {
                    CurrentState = AIState.Idle;
                    StateTimer = 0;
                }
            }

            if (shootProj && Main.GameUpdateCount % 2 == 0) {
                int type = ModContent.ProjectileType<SignusScythe>();
                int damage = npc.GetProjectileDamage(type);
                Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, npc.velocity.GetNormalVector() * 6, type, damage, 0, Main.myPlayer);
                Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, npc.velocity.GetNormalVector() * -6, type, damage, 0, Main.myPlayer);
            }
        }

        //阶段转换攻击
        private void DoPhaseTransitionAttack() {
            StateTimer++;
            npc.damage = 0;
            npc.velocity *= 0.9f;

            if (StateTimer == 1) {
                SoundEngine.PlaySound(SoundID.Roar, npc.Center);
                npc.dontTakeDamage = true; //无敌
                npc.Center = Target.Center + new Vector2(0, -300); //移动到玩家正上方
                npc.netUpdate = true;
            }

            //蓄力特效
            if (StateTimer > 1 && StateTimer < 120) {
                if (Main.rand.NextBool(3)) {
                    Dust.NewDust(npc.position, npc.width, npc.height, (int)CalamityDusts.PurpleCosmilite, Main.rand.NextFloat(-8, 8), Main.rand.NextFloat(-8, 8), 100, default, 3f);
                }
            }

            //释放毁灭性弹幕环
            if (StateTimer == 120) {
                SoundEngine.PlaySound(SoundID.Item92, npc.Center);
                if (!VaultUtils.isClient) {
                    int scytheCount = CWRWorld.Revenge ? 48 : 36; //巨量弹幕
                    float speed = 10f;
                    for (int i = 0; i < scytheCount; i++) {
                        Vector2 perturbedSpeed = new Vector2(speed, 0).RotatedBy(MathHelper.TwoPi * i / scytheCount);
                        int type = ModContent.ProjectileType<SignusScythe>();
                        int damage = npc.GetProjectileDamage(type);
                        Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, perturbedSpeed, type, damage, 0, Main.myPlayer);
                    }
                    //第二波，更慢，填充空隙
                    for (int i = 0; i < scytheCount; i++) {
                        Vector2 perturbedSpeed = new Vector2(speed * 0.7f, 0).RotatedBy(MathHelper.TwoPi * (i + 0.5f) / scytheCount);
                        int type = ModContent.ProjectileType<SignusScythe>();
                        int damage = npc.GetProjectileDamage(type);
                        Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, perturbedSpeed, type, damage, 0, Main.myPlayer);
                    }
                }
            }


            if (StateTimer > 180) {
                npc.dontTakeDamage = false;
                CurrentState = AIState.Idle;
                StateTimer = 0;
                npc.netUpdate = true;
            }
        }

        public override bool FindFrame(int frameHeight) {
            //根据不同的AI状态，我们应用不同的动画逻辑
            npc.frameCounter++;

            //获取当前状态所使用的纹理的总帧数 (原版灾厄Signus所有纹理都是6帧)
            const int totalFrames = 6;
            int frameTicks;//每帧持续的tick数，数值越小动画越快

            switch ((AIState)npc.ai[0]) {
                //刺客乱舞状态: 动画速度最快，体现极致的速度与力量
                case AIState.AssassinsFlurry:
                    frameTicks = 4;
                    break;

                //交叉扫射状态: 动画速度较快
                case AIState.CosmicCrossSweep:
                    frameTicks = 6;
                    break;

                //其他所有状态: 使用标准动画速度
                default:
                    frameTicks = 8;
                    break;
            }

            //计算当前应该在哪一帧
            frame = (int)(npc.frameCounter / frameTicks) % totalFrames;
            return false;
        }

        public override bool? Draw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            //--- 1. 参数准备: 根据当前状态选择纹理、残影数量等 ---
            Texture2D npcTexture;
            Texture2D glowMaskTexture;
            int afterimageAmt;

            SpriteEffects spriteEffects = npc.spriteDirection == 1 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;

            switch ((AIState)npc.ai[0]) {
                case AIState.AssassinsFlurry://冲刺状态
                    npcTexture = AltTexture2.Value;
                    glowMaskTexture = AltTexture2_Glow.Value;
                    afterimageAmt = 10;//最多残影，凸显极致速度
                    break;
                case AIState.CosmicCrossSweep://横扫状态
                    npcTexture = AltTexture.Value;
                    glowMaskTexture = AltTexture_Glow.Value;
                    afterimageAmt = 8;//较多残影
                    break;
                default://其他状态
                    npcTexture = TextureAssets.Npc[npc.type].Value;
                    glowMaskTexture = Texture_Glow.Value;
                    afterimageAmt = 6;//普通残影
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
                    if (Main.zenithWorld) {
                        eyeAfterimageColor = Color.Lerp(Color.White, Color.MediumBlue, 0.5f) * ((afterimageAmt - j) / (float)afterimageAmt) * 0.5f;
                    }
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