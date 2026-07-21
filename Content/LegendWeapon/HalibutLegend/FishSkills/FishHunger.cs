using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.FishSkills
{
    internal class FishHunger : FishSkill
    {
        public override int UnlockFishID => ItemID.Hungerfish;
        public override int DefaultCooldown => 60 - +HalibutData.GetDomainLayer() * 3;
        public override int ResearchDuration => 60 * 18;
        //活跃恶鬼索引
        private static readonly List<int> ActiveHungries = new();
        internal static int MaxHungries => (1 + HalibutData.GetDomainLayer() / 3); //最多1-4个恶鬼

        public override bool? Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source,
            Vector2 position, Vector2 velocity, int type, int damage, float knockback) {

            if (Cooldown <= 0) {
                SetCooldown();

                //清理失效恶鬼
                CleanupInactiveHungries();

                //如果恶鬼数量未满，生成新恶鬼
                if (ActiveHungries.Count < MaxHungries) {
                    //在玩家附近生成恶鬼
                    Vector2 spawnPos = player.Center + Main.rand.NextVector2Circular(80f, 80f);

                    int hungryProj = Projectile.NewProjectile(
                        source,
                        spawnPos,
                        Vector2.Zero,
                        ModContent.ProjectileType<HungryCompanionProjectile>(),
                        (int)(damage * 1.6f + HalibutData.GetDomainLayer() * 0.4f),
                        knockback * 0.1f,
                        player.whoAmI,
                        ai0: ActiveHungries.Count //传递索引
                    );

                    if (hungryProj >= 0 && hungryProj < Main.maxProjectiles) {
                        ActiveHungries.Add(hungryProj);

                        //显形收束,血珠倒吸+内收暗环
                        FishHungerVFX.SummonConverge(spawnPos);

                        //恶鬼召唤音效:肉响+湿滑挤出
                        SoundEngine.PlaySound(SoundID.NPCHit1 with {
                            Volume = 0.6f,
                            Pitch = -0.3f
                        }, spawnPos);
                        SoundEngine.PlaySound(SoundID.NPCHit13 with {
                            Volume = 0.4f,
                            Pitch = -0.5f
                        }, spawnPos);
                    }
                }
                else {
                    //恶鬼已满，命令现有恶鬼攻击
                    CommandHungriesToAttack(player);
                }
            }

            return null;
        }

        private static void CleanupInactiveHungries() {
            ActiveHungries.RemoveAll(id => id < 0 || id >= Main.maxProjectiles || !Main.projectile[id].active);
        }

        private void CommandHungriesToAttack(Player player) {
            //命令所有恶鬼向鼠标方向发起攻击
            for (int i = 0; i < ActiveHungries.Count; i++) {
                int id = ActiveHungries[i];
                if (id >= 0 && id < Main.maxProjectiles && Main.projectile[id].active) {
                    if (Main.projectile[id].ModProjectile is HungryCompanionProjectile hungry) {
                        hungry.CommandAttack(Main.MouseWorld);
                    }
                }
            }

            //攻击命令音效
            SoundEngine.PlaySound(SoundID.Roar with {
                Volume = 0.5f,
                Pitch = -0.4f
            }, player.Center);
        }
    }

    /// <summary>恶鬼伴随弹幕</summary>
    internal class HungryCompanionProjectile : ModProjectile
    {
        public override string Texture => "Terraria/Images/NPC_" + NPCID.TheHungryII;

        //状态机
        private enum HungryState
        {
            Idle,           //待机，围绕玩家漂浮
            FollowPlayer,   //跟随，跟随玩家移动
            Attacking,      //攻击，冲向目标
            Returning       //返回，攻击后返回玩家附近
        }

        private HungryState State {
            get => (HungryState)Projectile.ai[2];
            set => Projectile.ai[2] = (float)value;
        }

        private ref float HungryIndex => ref Projectile.ai[0];
        private ref float StateTimer => ref Projectile.ai[1];

        //攻击目标
        private Vector2 attackTarget = Vector2.Zero;
        private bool hasAttackTarget = false;

        //漂浮参数
        private float idleAngle = 0f;
        private float idleRadius = 100f;
        private float breathingPhase = 0f;
        private bool orbitInit;

        //动画
        private int currentFrame = 0;
        private int frameCounter = 0;
        private float squashStretch = 1f; //挤压拉伸效果

        //血肉演出状态
        private float facing;           //平滑朝向(嘴的指向)
        private float wrigglePhase;     //触须蠕动相位
        private float hungerT;          //包群饱和度0..1, 驱动饥饿可视化
        private bool wasFull;
        private float materializeT;     //入场成形0..1
        private int bitePause;          //扑咬定帧剩余帧
        private int tugTimer;           //撕扯拉锯剩余帧
        private Vector2 tugAxis;
        private float glintPhase;       //湿光扫掠相位

        private const int MaterializeFrames = 14;
        private const int DissolveWindow = 16;

        public override void SetStaticDefaults() {
            Main.projFrames[Projectile.type] = 6; //6帧动画
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 8;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 26;
            Projectile.height = 26;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 60 * (10 + HalibutData.GetDomainLayer() * 2); //10-30秒生命期
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 20;

            //初始化随机相位
            breathingPhase = Main.rand.NextFloat(MathHelper.TwoPi);
            wrigglePhase = Main.rand.NextFloat(MathHelper.TwoPi);
            glintPhase = Main.rand.NextFloat(MathHelper.TwoPi);
        }

        //attackTarget 只在 owner 端由 CommandAttack 写入, 走 ExtraAI 广播防旁观端看不到扑咬
        public override void SendExtraAI(BinaryWriter writer) {
            writer.Write(hasAttackTarget);
            writer.WriteVector2(attackTarget);
        }

        public override void ReceiveExtraAI(BinaryReader reader) {
            hasAttackTarget = reader.ReadBoolean();
            attackTarget = reader.ReadVector2();
        }

        /// <summary>成形完成前不咬人</summary>
        public override bool? CanDamage() => materializeT < 1f ? false : null;

        public override void AI() {
            Player owner = Main.player[Projectile.owner];

            if (!owner.active || owner.dead) {
                //玩家死亡:塌散演出由 OnKill 统一承担
                Projectile.Kill();
                return;
            }

            if (!FishSkill.GetT<FishHunger>().Active(owner)) {
                Projectile.Kill();
                return;
            }

            //包群饱和度:数量逼近上限时躁动渐强(饥饿可视化)
            int packMax = Math.Max(1, FishHunger.MaxHungries);
            int packNow = owner.ownedProjectileCounts[Projectile.type];
            hungerT = MathHelper.Clamp(packNow / (float)packMax, 0f, 1f);
            bool full = packNow >= packMax;
            if (full && !wasFull && materializeT >= 1f) {
                //攻击就绪的可读脉冲:一次急咬+湿响
                squashStretch = 1.18f;
                SoundEngine.PlaySound(SoundID.NPCHit13 with {
                    Volume = 0.45f,
                    Pitch = -0.15f,
                    MaxInstances = 2
                }, Projectile.Center);
            }
            wasFull = full;

            //首帧按编队位铺开轨道相位(SetDefaults 时 ai[0] 还未写入)
            if (!orbitInit) {
                orbitInit = true;
                idleAngle = MathHelper.TwoPi * HungryIndex / Math.Max(3, packMax);
            }

            //入场成形:血珠已在召唤点倒吸, 本体从小到大撑开
            if (materializeT < 1f) {
                materializeT = Math.Min(1f, materializeT + 1f / MaterializeFrames);
            }

            StateTimer++;

            //蠕动相位:饥饿越深爬得越快, 蓄力期高频颤
            bool inWindup = State == HungryState.Attacking && StateTimer < 20 && bitePause <= 0;
            wrigglePhase += 0.10f + hungerT * 0.11f + (inWindup ? 0.22f : 0f);
            glintPhase += 0.05f + hungerT * 0.03f;

            if (bitePause > 0) {
                //扑咬定帧:速度与咀嚼全部冻结一拍
                bitePause--;
                Projectile.velocity = Vector2.Zero;
                if (bitePause == 0 && State == HungryState.Attacking) {
                    tugTimer = 12; //定帧结束进入撕扯拉锯
                }
            }
            else {
                //更新动画帧
                UpdateAnimation();

                //状态机
                switch (State) {
                    case HungryState.Idle:
                        IdleAI(owner);
                        break;

                    case HungryState.FollowPlayer:
                        FollowPlayerAI(owner);
                        break;

                    case HungryState.Attacking:
                        AttackingAI(owner);
                        break;

                    case HungryState.Returning:
                        ReturningAI(owner);
                        break;
                }

                //撕扯拉锯:沿咬轴往复抽拽, 幅度随时间衰减
                if (tugTimer > 0) {
                    if (State == HungryState.Attacking) {
                        float k = tugTimer / 12f;
                        Projectile.velocity += tugAxis * (MathF.Sin(tugTimer * 1.9f) * 3.1f * k);
                        if (tugTimer % 3 == 0) {
                            FishHungerVFX.TugShed(Projectile.Center + tugAxis * 10f, tugAxis);
                        }
                    }
                    else {
                        tugTimer = 0;
                    }
                    if (tugTimer > 0) {
                        tugTimer--;
                    }
                }
            }

            //生物呼吸效果
            UpdateBreathing();

            //平滑朝向与旧角度缓存
            UpdateFacing();

            //退场先兆:生命尾段渗血变勤(瘪缩在绘制端)
            if (Projectile.timeLeft < DissolveWindow && Main.rand.NextBool(3)) {
                FishHungerVFX.Drool(MouthPos, facing.ToRotationVector2());
            }

            //待机垂涎:饥饿越深滴得越勤
            if (State != HungryState.Attacking && materializeT >= 1f
                && Main.rand.NextBool(Math.Max(9, 26 - (int)(hungerT * 16)))) {
                FishHungerVFX.Drool(MouthPos, facing.ToRotationVector2());
            }

            //照明:湿肉几乎不发光, 只留极淡血色
            Lighting.AddLight(Projectile.Center, 0.16f, 0.04f, 0.04f);

            //定期低吼:饥饿越深叫得越勤
            int growlEvery = 132 - (int)(hungerT * 54);
            if ((int)StateTimer % growlEvery == 0) {
                SoundEngine.PlaySound(SoundID.NPCHit8 with {
                    Volume = 0.3f,
                    Pitch = -0.5f
                }, Projectile.Center);
            }
        }

        private Vector2 MouthPos => Projectile.Center + facing.ToRotationVector2() * 10f * Projectile.scale;

        /// <summary>命令恶鬼攻击目标</summary>
        public void CommandAttack(Vector2 target) {
            attackTarget = target;
            hasAttackTarget = true;
            State = HungryState.Attacking;
            StateTimer = 0;
            tugTimer = 0;
            bitePause = 0;
            Projectile.netUpdate = true;

            //蓄力吸气:血珠被倒吸进嘴
            FishHungerVFX.ChargeSuction(MouthPos);
        }

        /// <summary>待机状态</summary>
        private void IdleAI(Player owner) {
            //计算理想位置:轨道半径带呼吸涨落, 饥饿时越游越急
            idleAngle += 0.02f + hungerT * 0.012f;
            float radiusBreath = idleRadius + MathF.Sin(breathingPhase * 0.8f + HungryIndex * 1.7f) * 9f;
            Vector2 idleOffset = idleAngle.ToRotationVector2() * radiusBreath;
            Vector2 targetPos = owner.Center + idleOffset;

            //平滑移动
            Vector2 toTarget = targetPos - Projectile.Center;
            float distance = toTarget.Length();

            if (distance > 5f) {
                Projectile.velocity = toTarget * 0.08f;
            }
            else {
                Projectile.velocity *= 0.9f;
            }

            //定期切换到跟随状态
            if (distance > 200f) {
                State = HungryState.FollowPlayer;
                StateTimer = 0;
            }
        }

        /// <summary>跟随状态</summary>
        private void FollowPlayerAI(Player owner) {
            Vector2 toOwner = owner.Center - Projectile.Center;
            float distance = toOwner.Length();

            //加速追赶
            Vector2 targetVelocity = toOwner.SafeNormalize(Vector2.Zero) * Math.Min(distance * 0.1f, 15f);
            Projectile.velocity = Vector2.Lerp(Projectile.velocity, targetVelocity, 0.15f);

            //靠近玩家后返回待机
            if (distance < 150f) {
                State = HungryState.Idle;
                StateTimer = 0;
            }

            //急游甩落血珠
            if (Main.rand.NextBool(7) && Projectile.velocity.Length() > 6f) {
                FishHungerVFX.Drool(Projectile.Center - Projectile.velocity * 0.5f, -Projectile.velocity.SafeNormalize(Vector2.UnitY) * 0.5f);
            }
        }

        /// <summary>攻击状态</summary>
        private void AttackingAI(Player owner) {
            if (!hasAttackTarget) {
                State = HungryState.Returning;
                StateTimer = 0;
                return;
            }

            float attackProgress = StateTimer / 60f;

            //前20帧，蓄力
            if (StateTimer < 20) {
                //后退蓄力
                Vector2 toTarget = (attackTarget - Projectile.Center).SafeNormalize(Vector2.Zero);
                Projectile.velocity = -toTarget * (1f - attackProgress * 3f) * 2f;

                //垂涎与充血在绘制端, 这里只滴涎
                if (StateTimer % 4 == 0) {
                    FishHungerVFX.Drool(MouthPos, toTarget);
                }
            }
            //20-40帧
            else if (StateTimer < 40) {
                float rushT = (StateTimer - 20f) / 20f;
                Vector2 toTarget = (attackTarget - Projectile.Center).SafeNormalize(Vector2.Zero);
                float rushSpeed = MathHelper.Lerp(16f, 34f, MathF.Pow(rushT, 1.7f));
                Projectile.velocity = toTarget * rushSpeed;

                //速度拉伸∝当前速度
                squashStretch = 1.22f + rushT * 0.5f;

                //冲刺沿途甩沫
                if ((int)StateTimer % 2 == 0) {
                    FishHungerVFX.Drool(Projectile.Center, -toTarget * 0.6f);
                }

                //起跳一拍:蹬出暗环+后抛血珠+嘶吼
                if (StateTimer == 20) {
                    FishHungerVFX.LungeKick(Projectile.Center, toTarget);
                    SoundEngine.PlaySound(SoundID.NPCHit13 with {
                        Volume = 0.6f,
                        Pitch = 0.2f
                    }, Projectile.Center);
                }
            }
            //40帧后，减速并返回
            else {
                Projectile.velocity *= 0.95f;

                if (StateTimer > 60) {
                    State = HungryState.Returning;
                    StateTimer = 0;
                    hasAttackTarget = false;
                }
            }
        }

        /// <summary>返回状态</summary>
        private void ReturningAI(Player owner) {
            Vector2 returnPos = owner.Center + idleAngle.ToRotationVector2() * idleRadius;
            Vector2 toReturn = returnPos - Projectile.Center;
            float distance = toReturn.Length();

            //平滑返回
            Vector2 targetVelocity = toReturn.SafeNormalize(Vector2.Zero) * Math.Min(distance * 0.12f, 12f);
            Projectile.velocity = Vector2.Lerp(Projectile.velocity, targetVelocity, 0.1f);

            //返回待机位置
            if (distance < 50f) {
                State = HungryState.Idle;
                StateTimer = 0;
            }
        }

        /// <summary>动画帧 tick，饥饿与攻击节奏驱动咀嚼速度</summary>
        private void UpdateAnimation() {
            int speed = State == HungryState.Attacking
                ? (StateTimer < 20 ? 2 : 3)
                : (int)MathHelper.Lerp(6f, 3f, hungerT);
            frameCounter++;
            if (frameCounter >= speed) {
                frameCounter = 0;
                currentFrame++;
                if (currentFrame >= 6) {
                    currentFrame = 0;
                }
            }
            Projectile.frame = currentFrame;
        }

        /// <summary>呼吸缩放 tick</summary>
        private void UpdateBreathing() {
            breathingPhase += 0.05f;

            //呼吸缩放
            float breathScale = (float)Math.Sin(breathingPhase) * 0.05f;
            squashStretch = MathHelper.Lerp(squashStretch, 1f + breathScale, 0.2f);
        }

        /// <summary>朝向平滑</summary>
        private void UpdateFacing() {
            float desired = facing;
            if (State == HungryState.Attacking) {
                if (tugTimer > 0) {
                    desired = tugAxis.ToRotation();
                }
                else if (hasAttackTarget && StateTimer < 20) {
                    desired = (attackTarget - Projectile.Center).ToRotation();
                }
                else if (Projectile.velocity.LengthSquared() > 1f) {
                    desired = Projectile.velocity.ToRotation();
                }
            }
            else if (Projectile.velocity.LengthSquared() > 0.6f) {
                desired = Projectile.velocity.ToRotation();
            }
            facing = facing.AngleLerp(desired, State == HungryState.Attacking ? 0.3f : 0.12f);
            //oldRot 缓存喂给残影链
            Projectile.rotation = facing + MathHelper.Pi;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            Vector2 dir = (target.Center - Projectile.Center).SafeNormalize(facing.ToRotationVector2());
            float ke = MathHelper.Clamp(Projectile.velocity.Length() / 30f, 0.2f, 1f);

            //咬合血沫:血珠锥+筋膜屑+碎肉块
            FishHungerVFX.BiteSpray(Projectile.Center + dir * 12f, dir, ke);

            if (State == HungryState.Attacking) {
                //扑咬定帧:咬死不动一拍, 随后进入撕扯拉锯
                bitePause = 3;
                tugAxis = dir;
                squashStretch = 0.62f; //咬合压缩
            }
            else {
                squashStretch = 0.78f;
            }

            //击中音效:湿咬+深部肉响
            SoundEngine.PlaySound(SoundID.NPCHit13 with {
                Volume = 0.5f,
                Pitch = 0f
            }, Projectile.Center);
            SoundEngine.PlaySound(SoundID.NPCHit1 with {
                Volume = 0.45f,
                Pitch = -0.6f
            }, Projectile.Center);
        }

        public override void OnKill(int timeLeft) {
            //塌散:碎肉与血珠活得比本体久, 一切死亡路径共用
            FishHungerVFX.CollapseBurst(Projectile.Center, Projectile.scale);
            if (!Main.dedServ) {
                SoundEngine.PlaySound(SoundID.NPCDeath1 with {
                    Volume = 0.5f,
                    Pitch = -0.4f
                }, Projectile.Center);
                SoundEngine.PlaySound(SoundID.NPCHit13 with {
                    Volume = 0.4f,
                    Pitch = -0.5f
                }, Projectile.Center);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            Texture2D hungryTex = TextureAssets.Npc[NPCID.TheHungryII].Value;

            int frameHeight = hungryTex.Height / 6;
            Rectangle sourceRect = new Rectangle(0, frameHeight * currentFrame, hungryTex.Width, frameHeight);
            Vector2 origin = sourceRect.Size() / 2f;

            float alpha = (255f - Projectile.alpha) / 255f;

            //入场撑开与退场瘪缩共用的形体因子
            float formScale = MathHelper.Lerp(0.15f, 1f, FishHungerVFX.EaseOutBack(materializeT));
            float dissolveT = Projectile.timeLeft < DissolveWindow ? 1f - Projectile.timeLeft / (float)DissolveWindow : 0f;
            formScale *= 1f - 0.26f * dissolveT;
            alpha *= MathHelper.Lerp(0.35f, 1f, materializeT) * (1f - 0.45f * dissolveT);

            //躁动只改绘制,判定不动
            float windupBoost = State == HungryState.Attacking && StateTimer < 20 && bitePause <= 0
                ? StateTimer / 20f * 2.6f : 0f;
            float amp = hungerT * 1.9f + windupBoost;
            Vector2 jitter = new Vector2(MathF.Sin(wrigglePhase * 3.1f + 1.7f), MathF.Sin(wrigglePhase * 2.6f)) * amp;
            Vector2 drawPos = Projectile.Center - Main.screenPosition + jitter;

            float rot = facing + MathHelper.Pi;
            SpriteEffects fxFlip = MathF.Cos(facing) > 0f ? SpriteEffects.FlipVertically : SpriteEffects.None;
            Vector2 scale = new Vector2(
                Projectile.scale * squashStretch,
                Projectile.scale / squashStretch
            ) * formScale;

            //1 触须垫底:画在本体之下, 根部锚在尾侧轮廓
            DrawTentacles(sb, drawPos, lightColor, alpha, formScale);

            //2 扑咬残影链:旧位置+旧角度, 越旧越暗越小
            if (State == HungryState.Attacking && StateTimer >= 20 && StateTimer < 46 && bitePause <= 0) {
                for (int i = 2; i < Projectile.oldPos.Length; i += 2) {
                    if (Projectile.oldPos[i] == Vector2.Zero) {
                        continue;
                    }
                    float ft = 1f - i / (float)Projectile.oldPos.Length;
                    Vector2 gp = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                    Color gc = FishHungerVFX.Meat(0.35f + ft * 0.4f) * (alpha * 0.42f * ft);
                    sb.Draw(hungryTex, gp, sourceRect, gc, Projectile.oldRot[i], origin
                        , scale * (0.82f + ft * 0.12f), fxFlip, 0);
                }
            }

            //3 暗肉剪影底:向斜下错位半透, 给肉块厚度
            Color under = FishHungerVFX.MeatDark * (alpha * 0.5f);
            sb.Draw(hungryTex, drawPos + new Vector2(2f, 3f), sourceRect, under, rot, origin, scale, fxFlip, 0);

            //4 本体:环境光乘暖肉色;蓄力充血变暗不加亮, 退场失血转深
            Color body = lightColor.MultiplyRGB(new Color(255, 214, 206));
            if (State == HungryState.Attacking && StateTimer < 20 && bitePause <= 0) {
                body = Color.Lerp(body, FishHungerVFX.MeatMid, StateTimer / 20f * 0.35f);
            }
            body = Color.Lerp(body, FishHungerVFX.MeatDark, dissolveT * 0.3f);
            sb.Draw(hungryTex, drawPos, sourceRect, body * alpha, rot, origin, scale, fxFlip, 0);

            //5 湿肉高光:极小面积镜面点沿体表缓扫(加色)
            Texture2D glint = CWRAsset.Extra_98?.Value;
            if (glint != null && materializeT >= 1f && dissolveT < 0.5f) {
                Vector2 gOff = new Vector2(MathF.Cos(glintPhase), MathF.Sin(glintPhase * 1.6f)) * 5f * formScale;
                float gA = (0.20f + 0.10f * MathF.Sin(glintPhase * 2.3f)) * alpha;
                sb.Draw(glint, drawPos + gOff, null, (FishHungerVFX.WetGlint with { A = 0 }) * gA
                    , rot + 0.5f, glint.Size() / 2f, new Vector2(0.09f, 0.045f) * formScale * Projectile.scale, fxFlip, 0);
            }

            return false;
        }

        /// <summary>触须蠕动</summary>
        private void DrawTentacles(SpriteBatch sb, Vector2 drawPos, Color lightColor, float alpha, float formScale) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            Rectangle src = new(0, 0, 1, 1);
            float rearRot = facing + MathHelper.Pi;

            Span<float> angOff = stackalloc float[] { -0.52f, 0.04f, 0.50f };
            Span<float> lens = stackalloc float[] { 30f, 40f, 26f };
            Span<float> widths = stackalloc float[] { 4.4f, 5.2f, 3.6f };

            bool rushing = State == HungryState.Attacking && StateTimer >= 20 && bitePause <= 0;
            bool windup = State == HungryState.Attacking && StateTimer < 20 && bitePause <= 0;
            float ampMul = rushing ? 0.35f : windup ? 0.5f : 1f;
            float freqMul = windup ? 2.3f : 1f;

            for (int n = 0; n < 3; n++) {
                float seed = Projectile.whoAmI * 0.7331f + n * 2.09f;
                Vector2 p = drawPos + rearRot.ToRotationVector2() * (8f * formScale * Projectile.scale);
                float ang = rearRot + angOff[n] * (rushing ? 0.45f : 1f);
                const int segs = 7;
                float step = lens[n] / segs * formScale * Projectile.scale;
                float baseAmp = (0.15f + hungerT * 0.15f) * ampMul;

                for (int k = 0; k < segs; k++) {
                    float tk = k / (segs - 1f);
                    //蠕动:相位沿体节传递, 鞭梢包络放大
                    ang += MathF.Sin(wrigglePhase * freqMul - k * 1.05f + seed) * baseAmp * (0.3f + tk * 1.1f);
                    Vector2 q = p + ang.ToRotationVector2() * step;
                    float w = MathF.Max(widths[n] * (1f - tk * 0.82f) * formScale, 0.9f);
                    Color c = Color.Lerp(FishHungerVFX.MeatMid, FishHungerVFX.MeatDark, 0.25f + tk * 0.68f)
                        .MultiplyRGB(lightColor) * (alpha * (0.95f - tk * 0.25f));
                    Vector2 mid = (p + q) * 0.5f;
                    sb.Draw(pixel, mid, src, c, ang, new Vector2(0.5f), new Vector2(step + 0.8f, w), SpriteEffects.None, 0f);
                    p = q;
                }
            }
        }
    }
}
