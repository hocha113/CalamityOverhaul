using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEaterOfWorlds.Rendering;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Accessories.BrutalRelics.EaterOfWorlds
{
    /// <summary>
    /// 友方吞世幼虫(蚀界之颚击杀产物)：破尸而出→入土潜行→猎物脚下预兆→破土突咬，循环伏击。<br/>
    /// 多节拖行走路径历史重采样，运动为世吞式限转向蛇形寻的(非匀速直线)。<br/>
    /// <b>联机模型</b>：owner端是权威(相位切换/换目标只在owner发生并经netUpdate下发)，
    /// 其余端沿同步的 ai 相位自走表现；判伤天然只在owner端解算。
    /// ai[0]=当前猎物who+1(0无) ai[1]=相位 ai[2]=消散令(满编顶替，服务端可置)
    /// </summary>
    internal class MawWormProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        public override LocalizedText DisplayName
            => this.GetLocalization(nameof(DisplayName), () => "吞世幼虫");

        #region 常量与状态
        private const int PhaseEmerge = 0;
        private const int PhaseDive = 1;
        private const int PhaseBurrow = 2;
        private const int PhaseTelegraph = 3;
        private const int PhaseLunge = 4;
        private const int PhaseExpire = 5;

        /// <summary>体节间距(px)</summary>
        private const float SegSpacing = 22f;
        /// <summary>身节数(不含头尾)</summary>
        private const int BodyCount = 6;
        /// <summary>整虫贴图缩放</summary>
        private const float WormScale = 0.62f;
        /// <summary>路径采样步长(px)</summary>
        private const float PathStep = 8f;
        /// <summary>路径点数上限(覆盖整虫长+余量)</summary>
        private const int MaxPathPoints = 46;
        /// <summary>预兆持续帧</summary>
        private const int TelegraphTime = 16;
        /// <summary>猎物搜索半径</summary>
        private const float HuntRange = 1150f;
        /// <summary>破土溅酸传播半径</summary>
        private const float BreachSplashRange = 150f;

        private int Phase => (int)Projectile.ai[1];
        private int TargetWho => (int)Projectile.ai[0] - 1;
        private bool ExpireOrdered => Projectile.ai[2] == 1f;
        private bool IsAuthority => Projectile.owner == Main.myPlayer;

        /// <summary>相位内计时(各端本地，相位切换时归零)</summary>
        private int phaseTimer;
        /// <summary>上帧相位，远端凭此检测同步来的切换</summary>
        private int prevPhase;
        /// <summary>蛇形摆动相位</summary>
        private float slitherPhase;
        /// <summary>上帧头部是否在实体块内(过土面检测)</summary>
        private bool wasInSolid;
        /// <summary>破土/入土FX最小间隔</summary>
        private int burstFxCooldown;
        /// <summary>owner周期纠漂计时</summary>
        private int netSyncTimer;
        /// <summary>无猎物滞留帧，超时消散</summary>
        private int idleTimer;
        /// <summary>伏击锚点(由猎物推导，各端同分布tile数据下一致)</summary>
        private Vector2 ambushAnchor;
        /// <summary>路径历史：旧在前新在后，等距重采样</summary>
        private readonly List<Vector2> path = new(MaxPathPoints + 4);
        #endregion

        public override void SetStaticDefaults() {
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 600;
        }

        public override void SetDefaults() {
            Projectile.width = 26;
            Projectile.height = 26;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Generic;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = WorldEatersMaw.WormLifetime;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 12;
            Projectile.netImportant = true;
        }

        #region 主AI
        public override void AI() {
            //首帧：破尸酸爆(各端本地演出)+路径播种
            if (Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                path.Add(Projectile.Center);
                prevPhase = Phase;
                wasInSolid = Collision.SolidCollision(Projectile.position, Projectile.width, Projectile.height);
                if (!VaultUtils.isServer) {
                    EowMotionFX.SpawnBreachBlast(Projectile.Center, 1.3f, -Vector2.UnitY);
                    EowMotionFX.PlaySpitCue(Projectile.Center, -0.2f);
                    PRTLoader.NewParticle<PRT_StarPulseRing>(Projectile.Center, Vector2.Zero,
                        EowMotionFX.AcidGreen, 0.1f).Configure(0.1f, 0.9f, 20);
                }
            }

            //远端凭同步的ai[1]检测相位切换，归零本地计时
            if (Phase != prevPhase) {
                prevPhase = Phase;
                phaseTimer = 0;
            }
            phaseTimer++;
            if (burstFxCooldown > 0) {
                burstFxCooldown--;
            }

            //权威端裁决消散：寿命将尽/满编顶替
            if (IsAuthority && Phase != PhaseExpire
                && (ExpireOrdered || Projectile.timeLeft <= 46)) {
                SwitchPhase(PhaseExpire);
            }
            //消散期不再自然减寿(入土动作走完再死)
            if (Phase == PhaseExpire && Projectile.timeLeft < 90) {
                Projectile.timeLeft = 90;
            }

            UpdateTargeting();

            switch (Phase) {
                case PhaseEmerge: UpdateEmerge(); break;
                case PhaseDive: UpdateDive(); break;
                case PhaseBurrow: UpdateBurrow(); break;
                case PhaseTelegraph: UpdateTelegraph(); break;
                case PhaseLunge: UpdateLunge(); break;
                default: UpdateExpire(); break;
            }

            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
            UpdatePath();
            UpdateGroundCrossing();
            UpdateAmbientFX();

            //owner周期纠漂：位置/速度/ai经同步包对齐各端
            if (IsAuthority && ++netSyncTimer >= 15) {
                netSyncTimer = 0;
                Projectile.netUpdate = true;
            }
        }

        /// <summary>权威端切相位并立即下发</summary>
        private void SwitchPhase(int next) {
            Projectile.ai[1] = next;
            prevPhase = next;
            phaseTimer = 0;
            if (IsAuthority) {
                Projectile.netUpdate = true;
            }
        }

        /// <summary>猎物维护：owner端权威改选(写ai[0])，其余端只读</summary>
        private void UpdateTargeting() {
            NPC current = TargetWho >= 0 && TargetWho < Main.maxNPCs ? Main.npc[TargetWho] : null;
            bool valid = current != null && current.active && current.CanBeChasedBy();
            if (!IsAuthority) {
                return;
            }
            //每20帧或失效时改选：近者优先，带酸蚀者按半距计
            if (valid && phaseTimer % 20 != 0) {
                return;
            }
            int best = -1;
            float bestScore = HuntRange;
            foreach (var npc in Main.ActiveNPCs) {
                if (!npc.CanBeChasedBy()) {
                    continue;
                }
                float score = Projectile.Distance(npc.Center);
                if (MawCorrosionNPC.GetStacks(npc) > 0) {
                    score *= 0.45f;
                }
                if (score < bestScore) {
                    bestScore = score;
                    best = npc.whoAmI;
                }
            }
            int nextAi = best + 1;
            if ((int)Projectile.ai[0] != nextAi) {
                Projectile.ai[0] = nextAi;
                Projectile.netUpdate = true;
            }
        }

        private NPC GetTarget() {
            int who = TargetWho;
            if (who < 0 || who >= Main.maxNPCs) {
                return null;
            }
            NPC npc = Main.npc[who];
            return npc.active && npc.CanBeChasedBy() ? npc : null;
        }
        #endregion

        #region 各相位
        /// <summary>破尸抛物弧：先冲后坠，非匀速</summary>
        private void UpdateEmerge() {
            if (phaseTimer > 6) {
                Projectile.velocity.Y += 0.34f;
            }
            Projectile.velocity.X *= 0.995f;
            if (phaseTimer >= 26 && IsAuthority) {
                SwitchPhase(PhaseDive);
            }
        }

        /// <summary>俯冲入土：钻向身下，入土够深后转潜行</summary>
        private void UpdateDive() {
            SteerToward(Projectile.Center + new Vector2(Projectile.velocity.X * 6f, 420f), 20f, 2.8f, 0.10f, 0.25f);
            bool buried = Collision.SolidCollision(Projectile.position, Projectile.width, Projectile.height);
            if (IsAuthority && ((buried && phaseTimer > 10) || phaseTimer > 55)) {
                SwitchPhase(PhaseBurrow);
            }
        }

        /// <summary>地下潜行至猎物脚下伏击位</summary>
        private void UpdateBurrow() {
            NPC target = GetTarget();
            if (target == null) {
                //无猎物：绕所有者脚下地里徘徊，久候不至则散
                idleTimer++;
                Player owner = Main.player[Projectile.owner];
                Vector2 holdPos = EowMotionFX.FindGroundBelow(owner.Center) + new Vector2(0f, 260f);
                SteerToward(holdPos, 11f, 2.2f, 0.07f, 0.6f);
                if (IsAuthority && idleTimer > 150) {
                    SwitchPhase(PhaseExpire);
                }
                return;
            }
            idleTimer = 0;

            //伏击锚：猎物脚下地表(tile数据各端一致，锚点无需额外同步)
            ambushAnchor = EowMotionFX.FindGroundBelow(target.Center);
            Vector2 lurkPos = ambushAnchor + new Vector2(0f, 300f);
            SteerToward(lurkPos, 21f, 3.0f, 0.10f, 0.5f);

            if (IsAuthority && Projectile.Distance(lurkPos) < 80f) {
                SwitchPhase(PhaseTelegraph);
            }
        }

        /// <summary>伏击预兆：地下盘桓蓄势，地表酸圈+汇聚尘(起手拍，给目标读的时间窗)</summary>
        private void UpdateTelegraph() {
            NPC target = GetTarget();
            if (target == null) {
                if (IsAuthority) {
                    SwitchPhase(PhaseBurrow);
                }
                return;
            }
            ambushAnchor = EowMotionFX.FindGroundBelow(target.Center);

            //地下减速盘桓：微幅回拉(蓄势反向预备)
            Projectile.velocity *= 0.86f;
            Projectile.velocity.Y += 0.12f;

            //预兆演出(客户端)：汇聚蚀土屑，末1/4静默收势
            if (!VaultUtils.isServer && EowMotionFX.OnScreen(ambushAnchor, 500f)
                && phaseTimer < TelegraphTime * 3 / 4 && Main.rand.NextBool(2)) {
                Vector2 dustPos = ambushAnchor + new Vector2(Main.rand.NextFloat(-90f, 90f), Main.rand.NextFloat(-6f, 4f));
                Dust dust = Dust.NewDustDirect(dustPos, 4, 4,
                    Main.rand.NextBool(3) ? DustID.CorruptGibs : DustID.Dirt,
                    0, 0, 110, default, Main.rand.NextFloat(0.9f, 1.5f));
                dust.velocity = (ambushAnchor - dustPos).SafeNormalize(Vector2.Zero) * 2.4f
                    - Vector2.UnitY * Main.rand.NextFloat(0.5f, 1.4f);
                dust.noGravity = true;
            }
            Lighting.AddLight(ambushAnchor, EowMotionFX.AcidGreen.ToVector3() * (0.3f + phaseTimer / (float)TelegraphTime * 0.5f));

            if (phaseTimer >= TelegraphTime) {
                //突刺：一帧内定满速(带猎物速度预判)，此后不再转向
                Vector2 predicted = target.Center + target.velocity * 10f;
                Projectile.velocity = (predicted - Projectile.Center).SafeNormalize(-Vector2.UnitY) * 26f;
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.WormDig with { Volume = 1f, Pitch = 0.3f, MaxInstances = 5 }, Projectile.Center);
                }
                if (IsAuthority) {
                    SwitchPhase(PhaseLunge);
                }
                else {
                    //远端同拍自走，等owner包到达再校正
                    prevPhase = PhaseLunge;
                    Projectile.ai[1] = PhaseLunge;
                    phaseTimer = 0;
                }
            }
        }

        /// <summary>破土突咬：前段直冲锁向，后段重力接管拱弧回落</summary>
        private void UpdateLunge() {
            if (phaseTimer > 13) {
                Projectile.velocity.Y += 0.52f;
                Projectile.velocity.X *= 0.99f;
            }
            //突刺途中甩酸
            if (!VaultUtils.isServer && Main.rand.NextBool(3)) {
                PRTLoader.NewParticle<PRT_AcidSplash>(
                    Projectile.Center + Main.rand.NextVector2Circular(8f, 8f),
                    -Projectile.velocity * 0.06f + Main.rand.NextVector2Circular(0.8f, 0.8f),
                    Color.White, Main.rand.NextFloat(0.35f, 0.6f)).Configure(Main.rand.Next(14, 24));
            }
            if (IsAuthority && phaseTimer >= 34) {
                SwitchPhase(PhaseDive);
            }
        }

        /// <summary>消散：扎进地里没入，土屑收尾</summary>
        private void UpdateExpire() {
            SteerToward(Projectile.Center + new Vector2(0f, 400f), 17f, 2.6f, 0.10f, 0.2f);
            bool buried = Collision.SolidCollision(Projectile.position, Projectile.width, Projectile.height);
            if ((buried && phaseTimer > 12) || phaseTimer > 70) {
                if (!VaultUtils.isServer) {
                    EowMotionFX.SpawnDirtBurst(Projectile.Center, 0.7f);
                }
                Projectile.Kill();
            }
        }

        /// <summary>世吞式限转向蛇形寻的：低速灵巧高速迟钝，入弯收油(移植自EowHeadAI.UpdateMovement)</summary>
        private void SteerToward(Vector2 targetPos, float moveSpeed, float turnSpeed, float accelRate, float slither) {
            Vector2 toTarget = targetPos - Projectile.Center;
            float distance = toTarget.Length();
            if (distance < 0.01f) {
                return;
            }
            float desired = toTarget.ToRotation();
            float speed = Projectile.velocity.Length();
            float heading = speed > 0.01f ? Projectile.velocity.ToRotation() : desired;

            float speedFactor = MathHelper.Clamp(speed / 24f, 0f, 1f);
            float maxTurn = turnSpeed / 20f * MathHelper.Lerp(2.0f, 0.75f, speedFactor);
            float newHeading = heading.AngleTowards(desired, maxTurn);

            float err = Math.Abs(MathHelper.WrapAngle(desired - newHeading));
            float throttle = MathHelper.Lerp(1f, 0.62f, MathHelper.Clamp(err / MathHelper.Pi, 0f, 1f));
            speed = MathHelper.Lerp(speed, moveSpeed * throttle, accelRate);

            if (slither > 0.01f) {
                slitherPhase += 0.09f + speed * 0.002f;
                newHeading += (float)Math.Sin(slitherPhase) * 0.3f * slither * MathHelper.Lerp(0.5f, 1f, speedFactor);
            }
            Projectile.velocity = newHeading.ToRotationVector2() * speed;
        }
        #endregion

        #region 路径与过土面
        /// <summary>等距重采样路径：一帧跨多个步长时补插中间点</summary>
        private void UpdatePath() {
            if (path.Count == 0) {
                path.Add(Projectile.Center);
                return;
            }
            Vector2 last = path[^1];
            float move = Vector2.Distance(last, Projectile.Center);
            while (move >= PathStep) {
                Vector2 dir = (Projectile.Center - last) / move;
                last += dir * PathStep;
                path.Add(last);
                move = Vector2.Distance(last, Projectile.Center);
            }
            if (path.Count > MaxPathPoints) {
                path.RemoveRange(0, path.Count - MaxPathPoints);
            }
        }

        /// <summary>沿路径向后取距头distBack处的插值点</summary>
        private Vector2 PositionAlongPath(float distBack) {
            Vector2 cursor = Projectile.Center;
            if (path.Count == 0 || distBack <= 0f) {
                return cursor;
            }
            for (int i = path.Count - 1; i >= 0; i--) {
                Vector2 pt = path[i];
                float step = Vector2.Distance(cursor, pt);
                if (step >= distBack) {
                    return Vector2.Lerp(cursor, pt, distBack / Math.Max(step, 0.001f));
                }
                distBack -= step;
                cursor = pt;
            }
            return cursor; //路径不够长：余下体节堆在末端(初生未展开的自然形态)
        }

        /// <summary>头部过土面检测：出土=破土酸爆+溅酸传播，入土=土屑没入</summary>
        private void UpdateGroundCrossing() {
            bool nowSolid = Collision.SolidCollision(Projectile.position, Projectile.width, Projectile.height);
            if (nowSolid == wasInSolid) {
                return;
            }
            bool exiting = wasInSolid && !nowSolid;
            wasInSolid = nowSolid;
            if (burstFxCooldown > 0) {
                return;
            }
            burstFxCooldown = 8;

            if (exiting) {
                if (!VaultUtils.isServer) {
                    EowMotionFX.SpawnBreachBlast(Projectile.Center, 1.15f,
                        Projectile.velocity.SafeNormalize(-Vector2.UnitY));
                    EowMotionFX.CameraPunch(Projectile.Center, 3f, 9, "MawWormBreach", -Vector2.UnitY);
                }
                //破土溅酸传播：owner端结算，广播随AddStacks走
                if (IsAuthority) {
                    foreach (var npc in Main.ActiveNPCs) {
                        if (npc.CanBeChasedBy() && Projectile.Distance(npc.Center) < BreachSplashRange) {
                            MawCorrosionNPC.AddStacks(npc, 1, Projectile.owner);
                        }
                    }
                }
            }
            else if (!VaultUtils.isServer) {
                EowMotionFX.SpawnDirtBurst(Projectile.Center, 0.9f);
            }
        }

        /// <summary>常态环境FX：地上爬行时侧漏酸沫+微光</summary>
        private void UpdateAmbientFX() {
            float glow = Phase == PhaseLunge ? 0.7f : 0.3f;
            Lighting.AddLight(Projectile.Center, EowMotionFX.AcidGreen.ToVector3() * glow);
            if (VaultUtils.isServer || wasInSolid || !EowMotionFX.OnScreen(Projectile.Center)) {
                return;
            }
            if (Main.rand.NextBool(7)) {
                Vector2 lateral = Projectile.velocity.SafeNormalize(Vector2.Zero).RotatedBy(MathHelper.PiOver2);
                float side = Main.rand.NextBool() ? 1f : -1f;
                PRTLoader.NewParticle<PRT_AcidSplash>(
                    Projectile.Center + Main.rand.NextVector2Circular(8f, 8f),
                    lateral * side * Main.rand.NextFloat(1f, 2.6f), Color.White,
                    Main.rand.NextFloat(0.3f, 0.5f)).Configure(Main.rand.Next(12, 22));
            }
        }
        #endregion

        #region 判定
        /// <summary>整条体节链参与碰撞：头框+各节22px方框</summary>
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (projHitbox.Intersects(targetHitbox)) {
                return true;
            }
            int half = (int)(SegSpacing * 0.5f);
            for (int k = 1; k <= BodyCount + 1; k++) {
                Vector2 seg = PositionAlongPath(k * SegSpacing);
                Rectangle segBox = new((int)seg.X - half, (int)seg.Y - half, half * 2, half * 2);
                if (segBox.Intersects(targetHitbox)) {
                    return true;
                }
            }
            return false;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //撕咬叠层走 WorldEatersMawPlayer.OnHitNPCWithProj 统一入口；此处只做本地咬合演出
            if (VaultUtils.isServer) {
                return;
            }
            EowMotionFX.SpawnAcidBurst(target.Center, 0.8f, Projectile.velocity.SafeNormalize(Vector2.Zero) * 2f);
            SoundEngine.PlaySound(SoundID.NPCDeath13 with { Volume = 0.5f, Pitch = 0.1f, MaxInstances = 4 }, target.Center);
        }
        #endregion

        #region 绘制
        public override bool PreDraw(ref Color lightColor) {
            //伏击预兆盘：复用世吞TechOmen(预告实体化范式)，画在锚点地表
            if (Phase == PhaseTelegraph) {
                DrawAmbushOmen();
            }
            DrawWormChain();
            return false;
        }

        /// <summary>体节链绘制：尾→头叠序，埋土段逐节隐去(伏击的"消失"正是演出)</summary>
        private void DrawWormChain() {
            Main.instance.LoadNPC(NPCID.EaterofWorldsHead);
            Main.instance.LoadNPC(NPCID.EaterofWorldsBody);
            Main.instance.LoadNPC(NPCID.EaterofWorldsTail);
            Texture2D headTex = TextureAssets.Npc[NPCID.EaterofWorldsHead].Value;
            Texture2D bodyTex = TextureAssets.Npc[NPCID.EaterofWorldsBody].Value;
            Texture2D tailTex = TextureAssets.Npc[NPCID.EaterofWorldsTail].Value;

            float lungeGlow = Phase == PhaseLunge ? 1f : Phase == PhaseTelegraph ? phaseTimer / (float)TelegraphTime : 0f;

            //尾→身(远到近)→头
            for (int k = BodyCount + 1; k >= 0; k--) {
                Vector2 segPos = k == 0 ? Projectile.Center : PositionAlongPath(k * SegSpacing);
                //埋进实体块的节不画：破土时逐节钻出，入土时逐节没入
                if (Collision.SolidCollision(segPos - new Vector2(8f, 8f), 16, 16)) {
                    continue;
                }

                Texture2D tex = k == 0 ? headTex : k == BodyCount + 1 ? tailTex : bodyTex;
                float rot;
                if (k == 0) {
                    rot = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
                }
                else {
                    Vector2 front = PositionAlongPath((k - 1) * SegSpacing);
                    rot = (front - segPos).SafeNormalize(-Vector2.UnitY).ToRotation() + MathHelper.PiOver2;
                }

                Vector2 drawPos = segPos - Main.screenPosition;
                Rectangle frame = tex.Bounds;
                Vector2 origin = frame.Size() / 2f;
                Color light = Lighting.GetColor((int)(segPos.X / 16f), (int)(segPos.Y / 16f));

                //友方身份色：亮紫罗兰底别于野生世吞的暗腐化
                Color body = Color.Lerp(light, EowMotionFX.CorruptPurple, 0.42f);
                Main.EntitySpriteDraw(tex, drawPos, frame, body, rot, origin, WormScale, SpriteEffects.None, 0);

                //酸光脉络(加色)：沿链相位错开的呼吸
                float pulse = 0.5f + 0.5f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 6f - k * 0.7f);
                Color vein = EowMotionFX.AcidGreen with { A = 0 } * (0.16f + 0.2f * pulse + 0.25f * lungeGlow);
                Main.EntitySpriteDraw(tex, drawPos, frame, vein, rot, origin, WormScale * 1.04f, SpriteEffects.None, 0);

                //突咬时的头部腭光
                if (k == 0 && lungeGlow > 0.05f) {
                    Texture2D soft = CWRAsset.SoftGlow.Value;
                    Vector2 mawTip = drawPos + (rot - MathHelper.PiOver2).ToRotationVector2() * 14f * WormScale;
                    Main.EntitySpriteDraw(soft, mawTip, null,
                        EowMotionFX.AcidGreen with { A = 0 } * (0.65f * lungeGlow), 0f,
                        soft.Size() / 2f, 0.3f + 0.2f * lungeGlow, SpriteEffects.None, 0);
                }
            }
        }

        /// <summary>地表伏击预兆盘(TechOmen)，无着色器走软光回退</summary>
        private void DrawAmbushOmen() {
            if (ambushAnchor == Vector2.Zero) {
                return;
            }
            float chargeT = MathHelper.Clamp(phaseTimer / (float)TelegraphTime, 0f, 1f);
            Vector2 drawPos = ambushAnchor - Main.screenPosition;
            Effect effect = EffectLoader.EowGeyser?.Value;

            if (effect == null) {
                //回退：压扁软光呼吸
                Texture2D softGlow = CWRAsset.SoftGlow.Value;
                float pulse = 0.6f + 0.4f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * (6f + chargeT * 12f));
                Color warn = EowMotionFX.AcidGreen with { A = 0 } * (chargeT * 0.6f * pulse);
                Main.EntitySpriteDraw(softGlow, drawPos, null, warn, 0f, softGlow.Size() / 2f,
                    new Vector2(110f / softGlow.Width * 2.2f, 0.4f), SpriteEffects.None, 0);
                return;
            }

            const float radius = 110f;
            effect.CurrentTechnique = effect.Techniques["TechOmen"];
            effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            effect.Parameters["uSeed"]?.SetValue(Projectile.whoAmI % 71 * 0.149f);
            effect.Parameters["uProgress"]?.SetValue(chargeT);
            effect.Parameters["uFade"]?.SetValue(0f);
            effect.Parameters["uAspect"]?.SetValue(1f);
            effect.Parameters["uDirtColor"]?.SetValue(EowMotionFX.DirtBrown.ToVector3());
            effect.Parameters["uAcidColor"]?.SetValue(EowMotionFX.AcidGreen.ToVector3());

            SpriteBatch sb = Main.spriteBatch;
            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, RasterizerState.CullNone, effect, Main.GameViewMatrix.TransformationMatrix);
            effect.CurrentTechnique.Passes[0].Apply();

            Texture2D pixel = VaultAsset.placeholder2.Value;
            Vector2 scale = new Vector2(radius * 2f / pixel.Width, radius * 0.62f / pixel.Height);
            sb.Draw(pixel, drawPos, null, Color.White, 0f, pixel.Size() / 2f, scale, SpriteEffects.None, 0f);

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
        }
        #endregion
    }
}
