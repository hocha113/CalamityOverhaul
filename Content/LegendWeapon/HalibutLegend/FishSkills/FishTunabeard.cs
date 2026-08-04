using CalamityOverhaul.Common;
using CalamityOverhaul.Content.PRTTypes;
using CalamityOverhaul.Content.TimeFreezes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.Graphics.CameraModifiers;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.FishSkills
{
    internal class FishTunabeard : FishSkill
    {
        public override int UnlockFishID => ItemID.CapnTunabeard;
        public override int DefaultCooldown => 60 * (24 - HalibutData.GetDomainLayer());//冷却时间随领域层数减少
        public override int ResearchDuration => 60 * 15;

        public override bool? AltFunctionUse(Item item, Player player) => true;

        public override bool? CanUseItem(Item item, Player player) {
            if (player.altFunctionUse == 2) {
                if (Cooldown > 0) {
                    return false;
                }

                item.UseSound = null;
                Use(item, player);
                return false;
            }
            return null;
        }

        public override void Use(Item item, Player player) {
            SetCooldown();

            //计算冲刺方向（朝向光标）
            Vector2 dashDirection = (Main.MouseWorld - player.Center).SafeNormalize(Vector2.UnitX * player.direction);
            float swingDir = dashDirection.X > 0 ? 1f : -1f;

            ShootState shootState = player.GetShootState();

            //生成冲刺弹幕（速度只作方向载体，动力学由弹幕分相接管）
            Projectile.NewProjectile(
                player.GetSource_ItemUse(item),
                player.Center,
                dashDirection * 45f,
                ModContent.ProjectileType<TunabeardDashProj>(),
                (int)(shootState.WeaponDamage * (5f + HalibutData.GetDomainLayer() * 1.25f)),//强力伤害倍率
                shootState.WeaponKnockback * 2f,
                player.whoAmI,
                ai0: 0,
                ai1: swingDir
            );

            //蓄势起手音，水的吸气
            SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 0.55f, Pitch = 0.35f }, player.Center);
        }
    }

    /// <summary>
    /// 金枪鱼须船长突刺，水刃居合<br/>
    /// 动力学三拍，反向蓄势后拉 4 帧（pow(t,8)，最后一帧才猛缩）→ 一帧内全速 →
    /// 两帧硬刹 + 一帧反坐；位移期玩家随行、全程无敌可穿墙（机制与旧版一致）<br/>
    /// 演出四阶段，出手=起点小水花+定向震屏；突进=水绸带刀光（FishTunaRibbon 三股条带）
    /// +水珠沿路甩落+鱼刃残影链+玩家拖影；刹停=终点大水花；余韵=条带从尾端化雾消散、
    /// 悬浮水雾沿路径缓慢下落（活得比突刺久）<br/>
    /// ai[0]=计时 ai[1]=挥向（决定鱼刃翻转）
    /// </summary>
    internal class TunabeardDashProj : ModProjectile, IPrimitiveDrawable, IAdditiveDrawable, IOverlayDrawable
    {
        public override string Texture => "Terraria/Images/Item_" + ItemID.CapnTunabeard;

        private ref float DashTimer => ref Projectile.ai[0];
        private ref float SwingDirection => ref Projectile.ai[1];

        private Player Owner => Main.player[Projectile.owner];

        //==== 动力学常量 ====
        private const float PullbackDist = 26f;   //蓄势后拉总距离(px)
        private const float FullSpeed = 118f;     //全速(px/帧)，一帧内直接给满
        private const int WindupFrames = 4;       //蓄势帧
        private const int LaunchFrame = WindupFrames + 1;          //全速帧
        private const int FullFrames = 19;        //全速持续帧
        private const int BrakeStart = LaunchFrame + FullFrames;   //硬刹起始帧
        private const int BrakeFrames = 3;        //硬刹，两帧骤减+一帧反坐
        private const int ControlEnd = BrakeStart + BrakeFrames - 1;//位移与判定结束帧
        //==== 余韵常量 ====
        private const int RetractDelay = 6;       //刹停到条带开始消散
        private const int RetractFrames = 26;     //消散时长
        private const int LifeFrames = ControlEnd + RetractDelay + RetractFrames + 6;

        private bool initialized;
        private Vector2 dashDir;
        private float swell = 1f;                 //突进期条带/鱼刃的幅度膨胀
        private readonly List<Vector2> pathPos = new(40);
        private readonly List<float> pathBirth = new(40);
        private readonly List<Vector2> drawPath = new(96);

        /// <summary>消散进度 0..1（刹停+延迟前恒 0）</summary>
        private float RetractT => MathHelper.Clamp(
            (DashTimer - (ControlEnd + RetractDelay)) / RetractFrames, 0f, 1f);

        public override void SetDefaults() {
            Projectile.width = 80;
            Projectile.height = 80;
            Projectile.friendly = false;          //判定窗随突进相开关
            Projectile.hostile = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = LifeFrames;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 10;
        }

        public override void AI() {
            DashTimer++;
            int t = (int)DashTimer;

            if (!Owner.active || Owner.dead) {
                Projectile.Kill();
                return;
            }

            if (!initialized) {
                initialized = true;
                dashDir = Projectile.velocity.SafeNormalize(Vector2.UnitX * (SwingDirection >= 0 ? 1 : -1));
                Projectile.velocity = Vector2.Zero;
                Projectile.spriteDirection = Projectile.direction = SwingDirection > 0 ? 1 : -1;
            }

            //判定窗，只在突进
            Projectile.friendly = t >= LaunchFrame && t <= ControlEnd;

            if (t <= ControlEnd) {
                ControlFrame(t);
            }
            else if (t == ControlEnd + 1) {
                //交还操控，残余一点前向动量
                Projectile.velocity = Vector2.Zero;
                Owner.velocity = dashDir * 6f;
            }
            else if (t == ControlEnd + 8 && !Main.dedServ) {
                //鱼刃淡出完毕，化回几粒水珠
                for (int i = 0; i < 4; i++) {
                    FishTunabeardVFX.GatherDrop(FishPos());
                }
            }

            //余韵，消散前沿剥落碎水
            float rt = RetractT;
            if (!Main.dedServ && rt > 0f && rt < 1f && pathPos.Count >= 2 && t % 2 == 0) {
                float frontU = MathHelper.Clamp((rt * 2.3f - 0.5f) / 1.15f, 0f, 1f);
                BuildDrawPath();
                FishTunabeardVFX.FrontWisp(FishTunaRibbonRenderer.PointAlong(drawPath, frontU));
            }

            //路径头/中段水光
            if (pathPos.Count >= 2) {
                float env = 1f - rt;
                Lighting.AddLight(pathPos[^1], new Vector3(0.20f, 0.46f, 0.62f) * env);
                Lighting.AddLight(FishTunaRibbonRenderer.PointAlong(pathPos, 0.5f), new Vector3(0.08f, 0.20f, 0.30f) * env);
            }
        }

        /// <summary>位移期逐帧，蓄势后拉 → 一帧全速 → 硬刹反坐；玩家随行+无敌</summary>
        private void ControlFrame(int t) {
            if (t <= WindupFrames) {
                //反向蓄势，pow(t,8) 曲线
                float t0 = MathF.Pow((t - 1) / (float)WindupFrames, 8f);
                float t1 = MathF.Pow(t / (float)WindupFrames, 8f);
                Vector2 step = -dashDir * PullbackDist * (t1 - t0);
                //后拉不挤进墙里
                Projectile.velocity = Collision.SolidCollision(Owner.position + step, Owner.width, Owner.height)
                    ? Vector2.Zero : step;
                if (!Main.dedServ) {
                    FishTunabeardVFX.GatherDrop(Owner.Center);
                }
            }
            else if (t == LaunchFrame) {
                //一帧内全速
                Projectile.velocity = dashDir * FullSpeed;
                LaunchBurst();
            }
            else if (t >= BrakeStart) {
                int bt = t - BrakeStart + 1;   //1..BrakeFrames
                if (bt == 1) {
                    Projectile.velocity *= 0.34f;
                }
                else if (bt == 2) {
                    Projectile.velocity *= 0.28f;
                }
                else {
                    //反坐一帧钉住硬刹 + 终点大水花
                    Projectile.velocity = -dashDir * 5f;
                    StopBurst();
                }
            }

            //突进期条带/鱼刃膨胀
            swell = 1f + 0.25f * MathHelper.Clamp((t - LaunchFrame) / 10f, 0f, 1f);

            //玩家随行
            Owner.Center = Projectile.Center;
            Owner.velocity = Vector2.Zero;
            Owner.fallStart = (int)(Owner.position.Y / 16f);
            Owner.GivePlayerImmuneState(8);
            if (MathF.Abs(dashDir.X) >= 0.05f) {
                Owner.direction = dashDir.X > 0f ? 1 : -1;
            }

            //突进中的速度语言，拖影 + 沿路甩水
            float speed = Projectile.velocity.Length();
            if (t >= LaunchFrame && speed > 20f) {
                Owner.armorEffectDrawShadow = true;
                if (!Main.dedServ) {
                    SpawnDashSpray(speed);
                }
            }

            RecordPath(t);
        }

        /// <summary>路径记录，从全速帧起（蓄势后拉的"上膛位"即条带尾端），刹停帧强制收口</summary>
        private void RecordPath(int t) {
            if (t < LaunchFrame) {
                return;
            }
            Vector2 head = Projectile.Center;
            if (pathPos.Count == 0) {
                pathPos.Add(head);
                pathBirth.Add(DashTimer);
                return;
            }
            if (Vector2.DistanceSquared(pathPos[^1], head) > 144f || t == ControlEnd) {
                pathPos.Add(head);
                pathBirth.Add(DashTimer);
            }
        }

        /// <summary>重力下垂量(px)，路径点越老垂得越多，水绸松弛下坠的液体语言</summary>
        private float Sag(float age) => MathF.Min(age * age * 0.009f, 34f);

        /// <summary>把下垂叠进路径副本（绘制/播雾/前沿定位共用）</summary>
        private void BuildDrawPath() {
            drawPath.Clear();
            for (int i = 0; i < pathPos.Count; i++) {
                drawPath.Add(pathPos[i] + Vector2.UnitY * Sag(DashTimer - pathBirth[i]));
            }
        }

        /// <summary>出手，起点小水花（向后踢水）+ 爆发音 + 极短定向震屏</summary>
        private void LaunchBurst() {
            SoundEngine.PlaySound(SoundID.Item1 with { Volume = 0.8f, Pitch = -0.3f }, Projectile.Center);
            SoundEngine.PlaySound(SoundID.DD2_WyvernDiveDown with { Volume = 0.6f, Pitch = 0.2f }, Projectile.Center);
            SoundEngine.PlaySound(SoundID.Splash with { Volume = 0.5f, Pitch = 0.3f }, Projectile.Center);
            if (Main.dedServ) {
                return;
            }
            FishTunabeardVFX.SplashBurst(Projectile.Center, -dashDir, 0.55f);
            if (CWRServerConfig.Instance.ScreenVibration) {
                Main.instance.CameraModifiers.Add(new PunchCameraModifier(Projectile.Center
                    , dashDir, 5f, 9f, 10, 1000f, FullName));
            }
        }

        /// <summary>刹停，终点大水花 + 沿整条路径播种悬浮水雾（余韵活得比突刺久）</summary>
        private void StopBurst() {
            SoundEngine.PlaySound(SoundID.Splash with { Volume = 0.75f, Pitch = -0.1f }, Projectile.Center);
            SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.35f, Pitch = -0.55f }, Projectile.Center);
            if (Main.dedServ) {
                return;
            }
            FishTunabeardVFX.SplashBurst(Projectile.Center, dashDir, 1f);
            BuildDrawPath();
            FishTunabeardVFX.SeedPathMist(drawPath);
            Owner.CWR().GetScreenShake(3f);
        }

        /// <summary>突进沿路甩水，每帧 2 粒重力水珠 + 稀疏水尘底噪，量随速度走</summary>
        private void SpawnDashSpray(float speed) {
            Vector2 prev = Projectile.Center - Projectile.velocity;
            Vector2 perp = new(-dashDir.Y, dashDir.X);
            for (int i = 0; i < 2; i++) {
                Vector2 pos = Vector2.Lerp(prev, Projectile.Center, Main.rand.NextFloat())
                    + perp * Main.rand.NextFloat(-26f, 26f) * swell;
                float side = Main.rand.NextBool() ? 1f : -1f;
                Vector2 vel = perp * side * Main.rand.NextFloat(1.2f, 3.2f)
                    - dashDir * Main.rand.NextFloat(0.4f, 1.6f)
                    - Vector2.UnitY * Main.rand.NextFloat(0.8f);
                Color col = Color.Lerp(FishTunabeardVFX.Mid, FishTunabeardVFX.Deep, Main.rand.NextFloat(0.55f));
                PRTLoader.NewParticle<PRT_HeartcarverDroplet>(pos, vel, col
                    , Main.rand.NextFloat(0.65f, 1.05f))
                    ?.Configure(Main.rand.Next(24, 38), 0.28f, 0.982f);
            }
            //廉价底噪
            if (Main.rand.NextFloat() < MathHelper.Clamp(speed / FullSpeed, 0.3f, 1f) * 0.55f) {
                Dust d = Dust.NewDustPerfect(Vector2.Lerp(prev, Projectile.Center, Main.rand.NextFloat())
                    , DustID.Water, -dashDir * Main.rand.NextFloat(0.5f, 1.5f), 120
                    , FishTunabeardVFX.Bright, Main.rand.NextFloat(0.8f, 1.3f));
                d.noGravity = true;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //击中强力音效 + 水花层
            SoundEngine.PlaySound(SoundID.NPCHit4 with { Volume = 0.9f, Pitch = -0.4f }, target.Center);
            SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 0.65f, Pitch = -0.25f }, target.Center);

            //击退增强（旧版机制保留）
            target.velocity += Projectile.velocity.SafeNormalize(Vector2.Zero) * 12f;

            //穿身微滞，世界不停，只有被穿者顿一拍
            TimeFreezeSystem.RefreshNPC<FishTunabeard>(target, 3);

            if (!Main.dedServ) {
                FishTunabeardVFX.TearSpray(target.Center, dashDir);
                Owner.CWR().GetScreenShake(2f);
            }
        }

        //==================== 绘制 ====================
        //夹心

        private Vector2 FishPos() {
            int t = (int)DashTimer;
            float lead = 14f + 26f * MathHelper.Clamp((t - WindupFrames) / 4f, 0f, 1f);
            return Projectile.Center + dashDir * lead;
        }

        /// <summary>夹心底层，头段身下的深水底幕（真 alpha 烟片 AlphaBlend 染深蓝）</summary>
        public override bool PreDraw(ref Color lightColor) {
            if (Main.dedServ || pathPos.Count < 2) {
                return false;
            }
            Texture2D smoke = CWRAsset.SmokeSheet01?.Value;
            if (smoke == null) {
                return false;
            }
            int t = (int)DashTimer;
            float env = t <= ControlEnd ? 1f : 1f - MathHelper.Clamp((t - ControlEnd) / 12f, 0f, 1f);
            if (env <= 0.02f) {
                return false;
            }
            float rot = dashDir.ToRotation();
            int frameSize = smoke.Width / 2;
            for (int k = 0; k < 3; k++) {
                int idx = pathPos.Count - 1 - k * 2;
                if (idx < 0) {
                    break;
                }
                Rectangle frame = new(k % 2 * frameSize, k / 2 % 2 * frameSize, frameSize, frameSize);
                Vector2 pos = pathPos[idx] + Vector2.UnitY * Sag(DashTimer - pathBirth[idx]) - Main.screenPosition;
                Vector2 scale = new Vector2(1.9f - k * 0.25f, 0.62f - k * 0.06f) * 0.5f * swell;
                Main.EntitySpriteDraw(smoke, pos, frame, FishTunabeardVFX.Deep * ((0.30f - k * 0.05f) * env)
                    , rot, frame.Size() * 0.5f, scale, SpriteEffects.None, 0);
            }
            return false;
        }

        /// <summary>三股水绸子带，白沫窄脊 + 主水绸 + 下侧慢流细带（层间视差）</summary>
        void IPrimitiveDrawable.DrawPrimitives() {
            if (Main.dedServ || pathPos.Count < 2) {
                return;
            }
            GraphicsDevice device = Main.instance.GraphicsDevice;
            if (!FishTunaRibbonRenderer.BeginDraw(device, out Effect fx, out var pb, out var pr, out var pd)) {
                return;
            }

            BuildDrawPath();

            int t = (int)DashTimer;
            float retract = RetractT;
            float flash = t >= LaunchFrame ? MathF.Pow(0.5f, t - LaunchFrame) : 0f;
            //消散完毕后的兜底淡出
            float opacity = 1f - MathHelper.Clamp((t - (ControlEnd + RetractDelay + RetractFrames)) / 6f, 0f, 1f);
            float seed = Projectile.identity * 0.6180339887f % 1f;

            Span<FishTunaRibbonRenderer.RibbonDef> defs = [
                //白沫窄脊
                new() { HalfWidth = 13f * swell, PerpOffset = 0f, Seed = seed + 0.71f,
                    FlowMul = 1.70f, TearAmp = 0.25f, HeadBoost = 1.45f, OpacityMul = 0.70f },
                //主水绸
                new() { HalfWidth = 46f * swell, PerpOffset = 0f, Seed = seed,
                    FlowMul = 1.00f, TearAmp = 0.95f, HeadBoost = 0.40f, OpacityMul = 0.95f },
                //下侧细带
                new() { HalfWidth = 21f * swell, PerpOffset = -32f * swell, Seed = seed + 0.43f,
                    FlowMul = 0.65f, TearAmp = 1.30f, HeadBoost = 0.15f, OpacityMul = 0.72f },
            ];
            for (int i = 0; i < defs.Length; i++) {
                FishTunaRibbonRenderer.DrawRibbon(device, fx, drawPath, in defs[i], retract, flash, opacity);
            }

            FishTunaRibbonRenderer.EndDraw(device, pb, pr, pd);
        }

        /// <summary>加色水光，出手/刹停爆点（≤5帧）+ 突进期头端引导水线</summary>
        void IAdditiveDrawable.DrawAdditiveAfterNon(SpriteBatch spriteBatch) {
            if (Main.dedServ || pathPos.Count == 0) {
                return;
            }
            int t = (int)DashTimer;

            //出手爆点，小星芒一拍
            if (t - LaunchFrame is >= 0 and < 4 && CWRAsset.StarFlare02?.Value is Texture2D launchFlare) {
                float k = (t - LaunchFrame) / 4f;
                float a = MathF.Pow(1f - k, 1.6f) * 0.75f;
                spriteBatch.Draw(launchFlare, pathPos[0] - Main.screenPosition, null
                    , FishTunabeardVFX.Foam * a, dashDir.ToRotation(), launchFlare.Size() * 0.5f
                    , 0.5f + k * 0.2f, SpriteEffects.None, 0);
            }

            //突进期头端引导水线
            if (t >= LaunchFrame && t < BrakeStart && CWRAsset.LightShot?.Value is Texture2D streak) {
                spriteBatch.Draw(streak, FishPos() - Main.screenPosition, null
                    , FishTunabeardVFX.Bright * 0.40f, dashDir.ToRotation(), new Vector2(streak.Width * 0.72f, streak.Height * 0.5f)
                    , new Vector2(0.55f, 0.42f) * swell, SpriteEffects.None, 0);
            }

            //刹停爆点，终点星芒，比出手大一号
            if (t - ControlEnd is >= 0 and < 5 && CWRAsset.StarFlare02?.Value is Texture2D stopFlare) {
                float k = (t - ControlEnd) / 5f;
                float a = MathF.Pow(1f - k, 1.6f) * 0.85f;
                spriteBatch.Draw(stopFlare, pathPos[^1] - Main.screenPosition, null
                    , FishTunabeardVFX.Foam * a, dashDir.ToRotation() * 0.5f, stopFlare.Size() * 0.5f
                    , (0.85f + k * 0.35f) * swell, SpriteEffects.None, 0);
            }
        }

        /// <summary>遮挡层，鱼刃本体 + 残影链，稳定盖在水绸之上（刃在水面上走）</summary>
        void IOverlayDrawable.DrawOverlay(SpriteBatch spriteBatch) {
            if (Main.dedServ) {
                return;
            }
            int t = (int)DashTimer;
            float bodyEnv = t <= ControlEnd ? 1f : 1f - MathHelper.Clamp((t - ControlEnd) / 8f, 0f, 1f);
            if (bodyEnv <= 0.02f) {
                return;
            }

            Main.instance.LoadItem(ItemID.CapnTunabeard);
            Texture2D tex = TextureAssets.Item[ItemID.CapnTunabeard].Value;
            Vector2 origin = tex.Size() / 2f;
            SpriteEffects effects = SwingDirection > 0 ? SpriteEffects.None : SpriteEffects.FlipVertically;

            //姿态
            float baseRot = dashDir.ToRotation() + MathHelper.PiOver4 * Projectile.spriteDirection;
            float rot = baseRot;
            if (t <= WindupFrames) {
                rot -= SwingDirection * 0.9f * MathF.Pow(t / (float)WindupFrames, 3f);
            }
            else if (t - LaunchFrame < 3) {
                rot += SwingDirection * 0.28f * (1f - (t - LaunchFrame) / 3f);
            }

            //幅度，突进膨胀，硬刹三帧内回落
            float fishScale = swell + 0.03f;
            if (t >= BrakeStart) {
                fishScale = MathHelper.Lerp(1.28f, 1.02f, MathHelper.Clamp((t - BrakeStart) / 3f, 0f, 1f));
            }

            Color lightC = Lighting.GetColor(Projectile.Center.ToTileCoordinates());
            Color bodyC = Color.Lerp(lightC, Color.White, 0.35f) * bodyEnv;
            Vector2 fishPos = FishPos();

            //残影链
            float speed = Projectile.velocity.Length();
            if (speed > 24f) {
                float gap = MathHelper.Clamp(speed * 0.34f, 10f, 46f);
                for (int k = 3; k >= 1; k--) {
                    float ga = (0.42f - k * 0.11f) * bodyEnv;
                    Color gc = Color.Lerp(bodyC, FishTunabeardVFX.Mid, 0.55f) * ga;
                    spriteBatch.Draw(tex, fishPos - dashDir * gap * k - Main.screenPosition, null
                        , gc, rot, origin, fishScale * (1f - k * 0.06f), effects, 0);
                }
            }

            //鱼刃本体
            spriteBatch.Draw(tex, fishPos - Main.screenPosition, null, bodyC, rot, origin, fishScale, effects, 0);
        }
    }
}
