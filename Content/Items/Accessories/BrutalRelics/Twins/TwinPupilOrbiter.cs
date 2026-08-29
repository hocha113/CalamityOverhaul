using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMechanicalEye.Core;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Accessories.BrutalRelics.Twins
{
    /// <summary>
    /// 迷你魔眼环绕体。ai[0]=0视界(激光眼)/1焚瞳(火焰眼)；ai[1]=状态；ai[2]=状态计时。
    /// 状态推进全部由计时确定性驱动，唯一必要同步点是接令进蓄力(netUpdate 捎带冲锋目标)。
    /// 本体贴图复用原版迷你双子(朝向约定 rotation=faceDir+π，3帧循环)
    /// </summary>
    internal class TwinPupilOrbiter : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder2;

        internal const int StateOrbit = 0;
        internal const int StateWindup = 1;
        internal const int StateDash = 2;
        internal const int StateRecover = 3;

        private const int WindupTime = 16;
        private const int DashTime = 24;
        private const int CrossTick = 15;
        private const int RecoverTime = 18;

        internal bool IsLaserEye => Projectile.ai[0] == 0f;
        internal int State => (int)Projectile.ai[1];
        private ref float StateTimer => ref Projectile.ai[2];

        /// <summary>交叉冲锋目标，netUpdate 时随 ExtraAI 同步</summary>
        private Vector2 dashTarget;
        //以下全是各端本地推导量，不上网络
        private Vector2 dashStart;
        private Vector2 dashCtrl1;
        private Vector2 dashCtrl2;
        private Vector2 dashExit;
        private Vector2 smoothedMove;
        private Vector2 faceDir = -Vector2.UnitY;
        private long consumedOrder;
        private int attackTimer;
        private float glintProgress;
        //索敌缓存：全表扫5帧一趟，间隔帧只做便宜校验
        private int cachedTargetIdx = -1;
        private int retargetTimer;

        internal Color MainColor => IsLaserEye ? TwinPupilTether.LaserColor : TwinPupilTether.FlameColor;
        internal Color GlowColor => IsLaserEye ? TwinPupilTether.LaserGlow : TwinPupilTether.FlameGlow;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 12;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 30;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 18000;
            Projectile.netImportant = true;//常驻体，迟入玩家也要收到
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 20;
            Projectile.DamageType = DamageClass.Generic;
        }

        public override void SendExtraAI(BinaryWriter writer) => writer.WriteVector2(dashTarget);

        public override void ReceiveExtraAI(BinaryReader reader) => dashTarget = reader.ReadVector2();

        public override void AI() {
            Player owner = Main.player[Projectile.owner];
            if (!owner.active) {
                Projectile.Kill();
                return;
            }
            TwinPupilTetherPlayer mp = owner.GetModPlayer<TwinPupilTetherPlayer>();
            //装备期间逐帧续命，卸下或死亡自然过期
            if (mp.TetherEquipped && !owner.dead) {
                Projectile.timeLeft = 2;
            }

            UpdateFrame();

            //owner 端受理交叉冲锋指令(时刻戳去重，两只眼各自消费)
            if (Projectile.owner == Main.myPlayer && State == StateOrbit
                && mp.DashOrder != 0 && mp.DashOrder != consumedOrder
                && (long)Main.GameUpdateCount - mp.DashOrder < 4) {
                consumedOrder = mp.DashOrder;
                dashTarget = mp.DashTarget;
                Projectile.ai[1] = StateWindup;
                StateTimer = 0f;
                Projectile.netUpdate = true;//唯一必要同步点：状态切换+冲锋目标
            }

            switch (State) {
                case StateWindup:
                    WindupBehavior(owner);
                    break;
                case StateDash:
                    DashBehavior(owner);
                    break;
                case StateRecover:
                    RecoverBehavior(owner);
                    break;
                default:
                    OrbitBehavior(owner);
                    break;
            }

            StateTimer++;

            Projectile.rotation = faceDir.ToRotation() + MathHelper.Pi;//原版迷你双子朝向约定
            Lighting.AddLight(Projectile.Center, MainColor.ToVector3() * 0.4f);

            //owner 端逐帧刷新冲刺撞击伤，吃全增伤
            if (Projectile.owner == Main.myPlayer) {
                Projectile.damage = (int)owner.GetTotalDamage(DamageClass.Generic)
                    .ApplyTo(TwinPupilTether.DashContactDamage);
            }
        }

        private void UpdateFrame() {
            if (++Projectile.frameCounter > 3) {
                Projectile.frameCounter = 0;
                if (++Projectile.frame > 2) {
                    Projectile.frame = 0;
                }
            }
        }

        #region 轨道与攻击

        /// <summary>环绕悬停：移动越快张角越大、半径越远，系绳随之拉长</summary>
        private void OrbitBehavior(Player owner) {
            smoothedMove = Vector2.Lerp(smoothedMove, owner.velocity, 0.085f);
            float speed01 = MathHelper.Clamp(smoothedMove.Length() / 11f, 0f, 1f);

            //基向：静止悬于头顶，移动时倒向运动方向
            Vector2 moveDir = smoothedMove.SafeNormalize(-Vector2.UnitY);
            Vector2 baseDir = Vector2.Lerp(-Vector2.UnitY, moveDir, speed01).SafeNormalize(-Vector2.UnitY);
            float halfAngle = MathHelper.Lerp(0.42f, 1.18f, speed01);
            float radius = MathHelper.Lerp(96f, 152f, speed01);
            float side = IsLaserEye ? 1f : -1f;

            Vector2 anchor = owner.Center + baseDir.RotatedBy(halfAngle * side) * radius
                + TwinsMotion.BreathingOffset(IsLaserEye ? 0f : 2.6f, 9f);

            //超远直接收拢(传送等场景)，其余弹簧跟随
            if (Vector2.DistanceSquared(Projectile.Center, anchor) > 900f * 900f) {
                Projectile.Center = anchor;
                Projectile.velocity = Vector2.Zero;
            }
            Vector2 pull = (anchor - Projectile.Center) * 0.13f;
            Projectile.velocity = Vector2.Lerp(Projectile.velocity, pull, 0.25f);

            if (IsLaserEye) {
                LaserEyeAttack(owner);
            }
            else {
                FlameEyeAttack(owner);
            }
        }

        /// <summary>视界：中距点射高单发，出膛前有充能微光</summary>
        private void LaserEyeAttack(Player owner) {
            NPC target = FindTarget(TwinPupilTether.LaserRange, true);
            glintProgress = 0f;
            if (target == null) {
                SetIdleFacing(owner);
                //无目标时保留大半蓄势，不空转满蓄
                if (attackTimer > TwinPupilTether.LaserFireInterval - 12) {
                    attackTimer = TwinPupilTether.LaserFireInterval - 12;
                }
                return;
            }

            attackTimer++;

            //线性预判提前量
            Vector2 muzzle = Projectile.Center;
            float flightTime = Vector2.Distance(muzzle, target.Center) / 21f;
            Vector2 aimPos = target.Center + target.velocity * flightTime * 0.85f;
            Vector2 aimDir = (aimPos - muzzle).SafeNormalize(Vector2.UnitX);
            faceDir = Vector2.Lerp(faceDir, aimDir, 0.3f).SafeNormalize(aimDir);

            //出膛前的充能微光(驱动瞳孔漩涡)
            int glintStart = TwinPupilTether.LaserFireInterval - 10;
            if (attackTimer > glintStart) {
                glintProgress = (attackTimer - glintStart) / 10f * 0.5f;
            }

            if (attackTimer < TwinPupilTether.LaserFireInterval) {
                return;
            }
            attackTimer = 0;

            //后坐与枪口火花各端自播；弹体只在 owner 端生成
            Projectile.velocity -= aimDir * 3.6f;
            if (!VaultUtils.isServer) {
                for (int i = 0; i < 5; i++) {
                    PRTLoader.NewParticle<PRT_TwinPupilSpark>(muzzle + aimDir * 14f,
                        aimDir.RotatedBy(Main.rand.NextFloat(-0.35f, 0.35f)) * Main.rand.NextFloat(3f, 7f),
                        Color.White, Main.rand.NextFloat(0.8f, 1.3f))?.Configure(14, 0);
                }
            }
            if (Projectile.owner == Main.myPlayer) {
                int damage = (int)owner.GetTotalDamage(DamageClass.Generic).ApplyTo(TwinPupilTether.LaserDamage);
                Projectile.NewProjectile(Projectile.GetSource_FromAI(), muzzle + aimDir * 16f, aimDir * 21f,
                    ModContent.ProjectileType<TwinPupilLaser>(), damage, 3f, Projectile.owner);
            }
        }

        /// <summary>焚瞳：近距持续喷吐咒焰</summary>
        private void FlameEyeAttack(Player owner) {
            NPC target = FindTarget(TwinPupilTether.FlameRange, true);
            glintProgress = 0f;
            if (target == null) {
                SetIdleFacing(owner);
                attackTimer = 0;
                return;
            }

            Vector2 aimDir = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitX);
            faceDir = Vector2.Lerp(faceDir, aimDir, 0.35f).SafeNormalize(aimDir);

            if (++attackTimer >= TwinPupilTether.FlameFireInterval) {
                attackTimer = 0;
                Projectile.velocity -= aimDir * 0.7f;//喷焰轻微后推
                if (Projectile.owner == Main.myPlayer) {
                    int damage = (int)owner.GetTotalDamage(DamageClass.Generic).ApplyTo(TwinPupilTether.FlameDamage);
                    Vector2 vel = aimDir.RotatedBy(Main.rand.NextFloat(-0.13f, 0.13f)) * Main.rand.NextFloat(9.5f, 11.5f);
                    Projectile.NewProjectile(Projectile.GetSource_FromAI(), Projectile.Center + aimDir * 12f, vel,
                        ModContent.ProjectileType<TwinPupilFlameJet>(), damage, 1f, Projectile.owner);
                }
            }

            if (VaultUtils.isServer) {
                return;
            }
            //持续喷焰声(各端自播，按距离衰减)
            if (Main.GameUpdateCount % 18 == 0) {
                SoundEngine.PlaySound(SoundID.Item34 with { Volume = 0.3f, Pitch = 0.15f }, Projectile.Center);
            }
            //嘴边热浪
            if (Main.rand.NextBool(2)) {
                Dust dust = Dust.NewDustDirect(Projectile.Center + aimDir * 10f, 6, 6, DustID.CursedTorch,
                    aimDir.X * 3f, aimDir.Y * 3f, 100, default, 1.4f);
                dust.noGravity = true;
            }
        }

        /// <summary>索敌：全表扫5帧一趟并缓存目标，间隔帧只做 O(1) 校验(视线在节流拍复查)</summary>
        private NPC FindTarget(float range, bool needLineOfSight) {
            if (retargetTimer > 0) {
                retargetTimer--;
                if (cachedTargetIdx >= 0 && cachedTargetIdx < Main.maxNPCs) {
                    NPC cached = Main.npc[cachedTargetIdx];
                    if (cached.active && cached.CanBeChasedBy(Projectile)
                        && Vector2.Distance(cached.Center, Projectile.Center) < range) {
                        return cached;
                    }
                    cachedTargetIdx = -1;
                }
                return null;
            }

            retargetTimer = 5;
            NPC best = null;
            float bestDist = range;
            foreach (NPC npc in Main.ActiveNPCs) {
                if (!npc.CanBeChasedBy(Projectile)) {
                    continue;
                }
                float dist = Vector2.Distance(npc.Center, Projectile.Center);
                if (dist >= bestDist) {
                    continue;
                }
                if (needLineOfSight && !Collision.CanHitLine(Projectile.Center, 1, 1, npc.Center, 1, 1)) {
                    continue;
                }
                best = npc;
                bestDist = dist;
            }
            cachedTargetIdx = best?.whoAmI ?? -1;
            return best;
        }

        private void SetIdleFacing(Player owner) {
            Vector2 desired = smoothedMove.LengthSquared() > 4f
                ? smoothedMove.SafeNormalize(Vector2.UnitX)
                : (Projectile.Center - owner.Center).SafeNormalize(-Vector2.UnitY);
            faceDir = Vector2.Lerp(faceDir, desired, 0.12f).SafeNormalize(desired);
        }

        #endregion

        #region 交叉冲锋编舞

        /// <summary>蓄力：向玩家身侧内收反向蓄势，末段绷紧颤抖，充能涡+内聚火花</summary>
        private void WindupBehavior(Player owner) {
            if (StateTimer == 0f && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item15 with { Pitch = 0.2f, Volume = 0.7f }, Projectile.Center);
            }

            float progress = StateTimer / WindupTime;

            Vector2 pullPoint = owner.Center
                + (Projectile.Center - owner.Center).SafeNormalize(-Vector2.UnitY) * 64f;
            Projectile.velocity = (pullPoint - Projectile.Center) * (0.10f + progress * 0.08f);
            if (progress > 0.7f && !VaultUtils.isServer) {
                Projectile.position += Main.rand.NextVector2Circular(1.6f, 1.6f);
            }

            faceDir = Vector2.Lerp(faceDir, (dashTarget - Projectile.Center).SafeNormalize(Vector2.UnitX), 0.35f);
            glintProgress = progress;

            //能量内聚火花
            if (!VaultUtils.isServer && (int)StateTimer % 2 == 0) {
                Vector2 spawnPos = Projectile.Center + Main.rand.NextVector2CircularEdge(70f, 70f) * (1f - progress * 0.5f);
                PRTLoader.NewParticle<PRT_TwinPupilSpark>(spawnPos, (Projectile.Center - spawnPos) * 0.13f,
                    Color.White, Main.rand.NextFloat(0.9f, 1.5f))?.Configure(14, IsLaserEye ? 0 : 1);
            }

            if (StateTimer >= WindupTime) {
                //各端确定性同拍进入冲刺
                Projectile.ai[1] = StateDash;
                StateTimer = -1f;//帧末自增回0
                dashStart = Projectile.Center;
                BuildDashPath(owner);
                glintProgress = 0f;
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item74 with { Volume = 0.7f, Pitch = 0.3f }, Projectile.Center);
                    PRTLoader.NewParticle<PRT_DWave>(Projectile.Center, faceDir * 1.4f, MainColor, 0.22f)?
                        .Configure(new Vector2(1.45f, 0.55f), faceDir.ToRotation() + MathHelper.PiOver2, 1.0f, 14);
                }
            }
        }

        /// <summary>
        /// 两段贝塞尔拼出包抄路径：入段甩向侧翼加速扑向交点(光标)，
        /// 出段切向连续地穿到对侧过冲减速。两眼镜像取边、同拍在交点交叉
        /// </summary>
        private void BuildDashPath(Player owner) {
            Vector2 axis = (dashTarget - owner.Center).SafeNormalize(-Vector2.UnitY);
            float side = IsLaserEye ? 1f : -1f;
            Vector2 perp = axis.RotatedBy(MathHelper.PiOver2) * side;
            dashCtrl1 = dashTarget + perp * 300f - axis * 230f;
            dashCtrl2 = dashTarget + (dashTarget - dashCtrl1) * 0.62f;//切向连续
            dashExit = dashTarget - perp * 170f + axis * 270f;
        }

        private static Vector2 QuadBezier(Vector2 a, Vector2 c, Vector2 b, float t) {
            float u = 1f - t;
            return u * u * a + 2f * u * t * c + t * t * b;
        }

        private void DashBehavior(Player owner) {
            float crossT = CrossTick / (float)DashTime;
            float u = MathHelper.Clamp(StateTimer / DashTime, 0f, 1f);

            Vector2 pos;
            if (u <= crossT) {
                //入段：前慢后快，撞线前速度拉满
                float a = MathF.Pow(u / crossT, 1.75f);
                pos = QuadBezier(dashStart, dashCtrl1, dashTarget, a);
            }
            else {
                //出段：过冲减速
                float b = (u - crossT) / (1f - crossT);
                b = 1f - (1f - b) * (1f - b);
                pos = QuadBezier(dashTarget, dashCtrl2, dashExit, b);
            }

            Vector2 delta = pos - Projectile.Center;
            Projectile.velocity = delta;//速度承载拉伸/拖尾表现
            Projectile.Center = pos;
            if (delta.LengthSquared() > 4f) {
                faceDir = delta.SafeNormalize(faceDir);
            }

            //残影列车之外的速度线火花
            if (!VaultUtils.isServer && (int)StateTimer % 2 == 0) {
                PRTLoader.NewParticle<PRT_TwinPupilSpark>(
                    Projectile.Center - faceDir * 22f + Main.rand.NextVector2Circular(10f, 10f),
                    -delta * 0.12f, Color.White, Main.rand.NextFloat(1f, 1.6f))?.Configure(14, IsLaserEye ? 0 : 1);
            }

            //交点拍：owner 的视界眼负责引爆，避免双份
            if ((int)StateTimer == CrossTick) {
                if (!VaultUtils.isServer) {
                    TwinsMotion.Shake(dashTarget, 5.5f, 10);
                }
                if (IsLaserEye && Projectile.owner == Main.myPlayer) {
                    int damage = (int)owner.GetTotalDamage(DamageClass.Generic).ApplyTo(TwinPupilTether.BurstDamage);
                    //ai[0]=系绳轴向角，供干涉爆纹定向
                    Projectile.NewProjectile(Projectile.GetSource_FromAI(), dashTarget, Vector2.Zero,
                        ModContent.ProjectileType<TwinPupilCrossBurst>(), damage, 8f, Projectile.owner,
                        (dashTarget - owner.Center).ToRotation());
                }
            }

            if (StateTimer >= DashTime) {
                Projectile.ai[1] = StateRecover;
                StateTimer = -1f;
                Projectile.velocity *= 0.4f;//硬刹
            }
        }

        private void RecoverBehavior(Player owner) {
            Projectile.velocity *= 0.86f;
            SetIdleFacing(owner);
            if (StateTimer >= RecoverTime) {
                Projectile.ai[1] = StateOrbit;
                StateTimer = -1f;
                attackTimer = 0;
            }
        }

        #endregion

        #region 判定

        //只有交叉冲刺途中有撞击伤
        public override bool? CanDamage() => State == StateDash && StateTimer >= 2f ? null : false;

        public override void ModifyDamageHitbox(ref Rectangle hitbox) {
            if (State == StateDash) {
                hitbox.Inflate(10, 10);
            }
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
            => TwinPupilRendNPC.ApplyRendBonus(target, ref modifiers);

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //冲锋撞击也算切割源，撕开创口
            if (target.TryGetGlobalNPC(out TwinPupilRendNPC rend)) {
                rend.ApplyRend();
            }
        }

        #endregion

        #region 绘制

        private static Texture2D GetBodyTexture(bool laser) {
            //原版光学法杖迷你双子贴图(Retanimini/Spazmamini)，天然的"迷你双子"玩家词汇
            int vanillaType = laser ? ProjectileID.Retanimini : ProjectileID.Spazmamini;
            Main.instance.LoadProjectile(vanillaType);
            return TextureAssets.Projectile[vanillaType].Value;
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = GetBodyTexture(IsLaserEye);
            Rectangle frame = tex.Frame(1, 3, 0, Projectile.frame);
            Vector2 origin = frame.Size() / 2f;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            //速度拉伸(贴图X轴即朝向轴)
            float stretch = MathHelper.Clamp(Projectile.velocity.Length() / 26f, 0f, 1f);
            Vector2 scaleVec = new(1.15f + stretch * 0.4f, 1.15f - stretch * 0.15f);

            //残影列车：冲刺时加密加亮并染主题色
            float ghostBase = State == StateDash ? 0.5f : 0.16f;
            Color ghostTint = Color.Lerp(Color.White, MainColor, State == StateDash ? 0.55f : 0.3f);
            for (int i = Projectile.oldPos.Length - 1; i >= 1; i--) {
                if (Projectile.oldPos[i] == Vector2.Zero) {
                    continue;
                }
                float fade = ghostBase * (1f - i / (float)Projectile.oldPos.Length);
                Vector2 gp = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Main.EntitySpriteDraw(tex, gp, frame, ghostTint * fade, Projectile.oldRot[i],
                    origin, scaleVec, SpriteEffects.None, 0);
            }

            //本体
            Main.EntitySpriteDraw(tex, drawPos, frame, lightColor, Projectile.rotation,
                origin, scaleVec, SpriteEffects.None, 0);

            //瞳孔辉光：预乘批里的 A=0 加色技法
            Vector2 pupilPos = drawPos + faceDir * 12f;
            Texture2D glow = CWRAsset.SoftGlow.Value;
            float pulse = 1f + 0.12f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 9f + (IsLaserEye ? 0f : 2f));
            Main.EntitySpriteDraw(glow, pupilPos, null, (MainColor with { A = 0 }) * 0.85f, 0f,
                glow.Size() / 2f, 0.34f * pulse, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(glow, pupilPos, null, (Color.White with { A = 0 }) * 0.7f, 0f,
                glow.Size() / 2f, 0.15f, SpriteEffects.None, 0);

            //充能涡：交叉冲锋蓄力与激光出膛前，复用双子 Boss 的漩涡语汇
            if (glintProgress > 0.05f) {
                DrawChargeVortex(MathHelper.Clamp(glintProgress, 0f, 1f));
            }
            return false;
        }

        /// <summary>迷你版充能汇聚涡，消费方式与 Boss 侧 DrawChargeVortex 同构</summary>
        private void DrawChargeVortex(float progress) {
            Effect shader = EffectLoader.TwinsChargeVortex?.Value;
            if (shader == null) {
                return;
            }
            SpriteBatch sb = Main.spriteBatch;
            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.Additive, SamplerState.LinearWrap,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            shader.Parameters["uColor"]?.SetValue(MainColor.ToVector3());
            shader.Parameters["uSecondaryColor"]?.SetValue(Color.Lerp(MainColor, Color.White, 0.55f).ToVector3());
            shader.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            shader.Parameters["uProgress"]?.SetValue(progress);
            shader.Parameters["uIntensity"]?.SetValue(0.4f + progress * 0.7f);
            shader.Parameters["uOpacity"]?.SetValue(1f);
            shader.Parameters["uImage1"]?.SetValue(CWRAsset.Extra_193.Value);
            shader.CurrentTechnique.Passes[0].Apply();

            Texture2D quad = VaultAsset.placeholder2.Value;
            const float size = 190f;
            sb.Draw(quad, Projectile.Center - Main.screenPosition, null, Color.White, 0f,
                quad.Size() / 2f, new Vector2(size / quad.Width, size / quad.Height), SpriteEffects.None, 0f);

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            for (int i = 0; i < 8; i++) {
                PRTLoader.NewParticle<PRT_TwinPupilSpark>(Projectile.Center, VaultUtils.RandVr(2f, 6f),
                    Color.White, Main.rand.NextFloat(0.8f, 1.3f))?.Configure(16, IsLaserEye ? 0 : 1);
            }
        }

        #endregion
    }
}
