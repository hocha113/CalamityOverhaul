using CalamityOverhaul.Common;
using CalamityOverhaul.Content.PRTTypes;
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
    internal class FishUnicorn : FishSkill
    {
        public override int UnlockFishID => ItemID.UnicornFish;
        public override int DefaultCooldown => 60 * (10 - HalibutData.GetDomainLayer() / 2);
        public override int ResearchDuration => 60 * 14;

        public override bool? AltFunctionUse(Item item, Player player) => true;

        public override bool? CanUseItem(Item item, Player player) {
            if (player.altFunctionUse == 2) {
                //检查冷却
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
            //设置冷却
            SetCooldown();

            //计算水平冲刺方向（只考虑X轴方向）
            Vector2 mouseDirection = (Main.MouseWorld - player.Center).SafeNormalize(Vector2.UnitX * player.direction);
            float horizontalDirection = mouseDirection.X > 0 ? 1f : -1f;

            ShootState shootState = player.GetShootState();

            //生成独角兽鱼冲刺弹幕：初速为零，蓄势-爆发-硬刹曲线由弹幕自己执行
            Projectile.NewProjectile(
                player.GetSource_ItemUse(item),
                player.Center,
                Vector2.Zero,
                ModContent.ProjectileType<UnicornFishDashProj>(),
                (int)(shootState.WeaponDamage * (2.5f + HalibutData.GetDomainLayer() * 0.75f)),
                shootState.WeaponKnockback * 2.5f,
                player.whoAmI,
                ai0: 0,
                ai1: horizontalDirection
            );

            //预告拍水晶颤音，爆发双层音移至起跳帧
            SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.5f, Pitch = 0.4f }, player.Center);
        }
    }

    /// <summary>
    /// 独角兽鱼冲刺弹幕：蓄势-爆发-硬刹三段水平突刺，全程无敌穿墙。<br/>
    /// 彩虹由玩家残影序列编码：每个残影单色，色相沿队列逐个偏移，串起来才是彩虹；<br/>
    /// 配角尖双螺旋细光轨与受重力缓落的星屑余韵。<br/>
    /// ai[0]=时间轴  ai[1]=水平方向 ±1
    /// </summary>
    internal class UnicornFishDashProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Item_" + ItemID.UnicornFish;

        private ref float DashTimer => ref Projectile.ai[0];
        private ref float DashDirection => ref Projectile.ai[1];

        private Player Owner => Main.player[Projectile.owner];

        //蓄势-爆发-硬刹时间轴（帧）
        private const int WindupFrames = 8;
        private const int BurstFrames = 24;
        private const int BrakeFrames = 6;
        private const int ActionEnd = WindupFrames + BurstFrames + BrakeFrames;
        /// <summary>余辉期：控制权已交还，弹幕滞留陪残影链与星屑走完</summary>
        private const int AfterglowFrames = 34;
        /// <summary>一帧点满的爆发速度，总位移与旧曲线持平（约 2700px）</summary>
        private const float BurstSpeed = 104f;
        private const float PullbackMax = 10f;
        private const int GhostLife = 26;
        /// <summary>残影队列的逐个色相偏移，约 13 帧快照扫过近整圈色环</summary>
        private const float GhostHueStep = 0.068f;
        private const float HelixFreq = 0.045f;
        private const float HelixRadius = 20f;

        private static readonly Color PaleGold = new(255, 238, 200);

        /// <summary>玩家单帧快照残影（客户端视觉），队列按寿命自然尾部先蚀</summary>
        private struct DashGhost
        {
            public Vector2 Position;   //玩家左上角坐标
            public int Direction;
            public Rectangle BodyFrame;
            public Rectangle LegFrame;
            public float Hue;
            public int SpawnTime;
        }

        private readonly List<DashGhost> ghosts = new();
        private Vector2 castCenter;
        private int ghostCounter;
        private float helixPhase;
        private bool launchSoundPlayed;
        private bool brakeKicked;

        /// <summary>最新残影的色相，枪头回声与光照跟随它续接队列</summary>
        private float HeadHue => (0.02f + Math.Max(ghostCounter - 1, 0) * GhostHueStep) % 1f;

        private Vector2 HornTip(float dir) => Projectile.Center + new Vector2(dir * 74f, -4f);

        public override void SetDefaults() {
            Projectile.width = 90;
            Projectile.height = 90;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = ActionEnd + AfterglowFrames;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 8;
        }

        public override bool? CanDamage() => DashTimer > ActionEnd ? false : null;

        public override void AI() {
            if (!Owner.active || Owner.dead) {
                Projectile.Kill();
                return;
            }

            if (DashTimer == 0) {
                castCenter = Owner.Center;
                Projectile.Center = castCenter;
            }
            DashTimer++;

            if (DashTimer <= ActionEnd) {
                ActionUpdate();
            }
            else {
                Projectile.velocity = Vector2.Zero;
            }

            //残影尾部先蚀，湮灭时蜕落星屑
            for (int i = ghosts.Count - 1; i >= 0; i--) {
                if (DashTimer - ghosts[i].SpawnTime >= GhostLife) {
                    ShedStardust(ghosts[i]);
                    ghosts.RemoveAt(i);
                }
            }
        }

        private void ActionUpdate() {
            float dir = DashDirection >= 0 ? 1f : -1f;

            if (DashTimer <= WindupFrames) {
                //蓄势：末端 pow 迟滞的小幅反拉（深呼吸），身体钉在原地
                float t = DashTimer / (float)WindupFrames;
                float pull = MathF.Pow(t, 7f) * PullbackMax;
                Projectile.Center = castCenter - new Vector2(dir * pull, 0f);
                Projectile.velocity = Vector2.Zero;

                if (!VaultUtils.isServer) {
                    WindupTelegraph(dir, t);
                    Lighting.AddLight(castCenter + new Vector2(dir * 58f, 0f), PaleGold.ToVector3() * 0.30f * t);
                }
            }
            else if (DashTimer <= WindupFrames + BurstFrames) {
                int bt = (int)DashTimer - WindupFrames;
                if (bt == 1) {
                    //一帧点满全速
                    Projectile.velocity = new Vector2(dir * BurstSpeed, 0f);
                    if (!VaultUtils.isServer && !launchSoundPlayed) {
                        launchSoundPlayed = true;
                        SoundEngine.PlaySound(SoundID.Item1 with { Volume = 0.9f, Pitch = 0.3f }, Owner.Center);
                        SoundEngine.PlaySound(SoundID.DD2_WyvernDiveDown with { Volume = 0.7f, Pitch = 0.5f }, Owner.Center);
                    }
                }
                else if (bt <= 4) {
                    //起步 3 帧轻微过冲
                    Projectile.velocity.X *= 1.03f;
                }
                else {
                    //中段缓释，速度全程在演化
                    Projectile.velocity.X *= 0.995f;
                }
            }
            else {
                //硬刹
                Projectile.velocity.X *= 0.52f;
                if (!brakeKicked) {
                    brakeKicked = true;
                    if (!VaultUtils.isServer) {
                        RecordGhost();
                        BrakeImpact(dir);
                    }
                }
            }

            //玩家钉在弹幕上（仅动作期）
            Owner.Center = Projectile.Center;
            Owner.direction = (int)dir;
            Projectile.direction = Projectile.spriteDirection = (int)dir;

            //免疫帧（覆盖窗口与旧版一致，刹车尾段靠状态残留）
            if (DashTimer < ActionEnd - 6) {
                Owner.GivePlayerImmuneState(10);
            }

            if (DashTimer > WindupFrames && DashTimer <= WindupFrames + BurstFrames && !VaultUtils.isServer) {
                FlightVisuals(dir);
                Lighting.AddLight(Projectile.Center, Main.hslToRgb(HeadHue, 0.55f, 0.50f).ToVector3() * 0.70f);
            }
        }

        /// <summary>蓄势期：星屑沿收紧的双臂螺旋吸向角尖，72% 后静默（爆发前的屏息）</summary>
        private void WindupTelegraph(float dir, float t) {
            if (t >= 0.72f) {
                return;
            }
            Vector2 tip = castCenter + new Vector2(dir * 58f, -4f);
            float ang = DashTimer * 1.05f;
            float radius = MathHelper.Lerp(60f, 12f, t / 0.72f);
            for (int s = 0; s < 2; s++) {
                Vector2 pos = tip + (ang + s * MathHelper.Pi).ToRotationVector2() * radius;
                Vector2 vel = (tip - pos) * 0.16f;
                PRTLoader.NewParticle<PRT_FishUnicornHelix>(pos, vel, PaleGold * 0.8f, 1f)
                    ?.Configure(vel.ToRotation(), 20f, 10);
            }
        }

        /// <summary>爆发期视觉：残影快照 + 角尖双螺旋光轨 + 稀疏星屑蜕落</summary>
        private void FlightVisuals(float dir) {
            //残影链：每 2 帧一帧快照，色相沿队列逐个偏移
            if ((int)DashTimer % 2 == 0) {
                RecordGhost();
            }

            //双螺旋细光轨：相位随水平位移推进，子采样填补高速帧空隙
            Vector2 horn = HornTip(dir);
            float advance = MathF.Abs(Projectile.velocity.X);
            float segLen = MathHelper.Clamp(advance * 0.45f, 26f, 62f);
            for (int sub = 0; sub < 2; sub++) {
                float backDist = advance * 0.5f * sub;
                Vector2 basePos = horn - new Vector2(dir * backDist, 0f);
                float ph = helixPhase - backDist * HelixFreq;
                for (int strand = 0; strand < 2; strand++) {
                    float p = ph + strand * MathHelper.Pi;
                    float depth = MathF.Cos(p);
                    //前侧亮后侧暗，编码螺线的空间纵深
                    float bright = MathHelper.Lerp(0.45f, 1f, depth * 0.5f + 0.5f);
                    Vector2 pos = basePos + new Vector2(0f, MathF.Sin(p) * HelixRadius);
                    float tangent = MathF.Atan2(HelixRadius * HelixFreq * depth, dir);
                    PRTLoader.NewParticle<PRT_FishUnicornHelix>(pos, Vector2.Zero, PaleGold * bright, 1f)
                        ?.Configure(tangent, segLen, 12);
                }
            }
            helixPhase += advance * HelixFreq;

            //稀疏星屑：从队尾蜕落，缓落成路径余韵
            if ((int)DashTimer % 3 == 0) {
                Vector2 tail = Projectile.Center - new Vector2(dir * Main.rand.NextFloat(30f, 90f), Main.rand.NextFloat(-26f, 26f));
                PRTLoader.NewParticle<PRT_FishUnicornStardust>(tail
                    , new Vector2(dir * Main.rand.NextFloat(-0.8f, 0.4f), Main.rand.NextFloat(-1.1f, -0.3f))
                    , StardustColor(HeadHue), Main.rand.NextFloat(0.40f, 0.65f))
                    ?.Configure(Main.rand.Next(36, 58), 0.045f);
            }
        }

        /// <summary>硬刹拍：星屑扇 + 横压冲击环 + 水晶脆响 + 克制的水平震</summary>
        private void BrakeImpact(float dir) {
            SoundEngine.PlaySound(SoundID.DD2_WitherBeastCrystalImpact with { Volume = 0.65f, Pitch = 0.25f }, Projectile.Center);

            Vector2 horn = HornTip(dir);
            for (int i = 0; i < 12; i++) {
                Vector2 vel = new Vector2(dir, 0f).RotatedBy(Main.rand.NextFloat(-0.55f, 0.55f)) * Main.rand.NextFloat(3f, 9f);
                PRTLoader.NewParticle<PRT_FishUnicornStardust>(horn + Main.rand.NextVector2Circular(10f, 10f)
                    , vel, StardustColor(HeadHue), Main.rand.NextFloat(0.5f, 0.9f))
                    ?.Configure(Main.rand.Next(34, 55), 0.05f);
            }
            PRTLoader.NewParticle<PRT_DWave>(horn, Vector2.Zero, new Color(215, 190, 255), 0.30f)
                ?.Configure(new Vector2(1f, 0.55f), dir > 0 ? 0f : MathHelper.Pi, 1.05f, 14);

            if (Main.myPlayer == Projectile.owner && CWRServerConfig.Instance.ScreenVibration) {
                Main.instance.CameraModifiers.Add(new PunchCameraModifier(Projectile.Center
                    , new Vector2(dir, 0f), 4f, 5f, 9, 1200f, FullName));
            }
        }

        private void RecordGhost() {
            ghosts.Add(new DashGhost {
                Position = Owner.position,
                Direction = Owner.direction,
                BodyFrame = Owner.bodyFrame,
                LegFrame = Owner.legFrame,
                Hue = (0.02f + ghostCounter * GhostHueStep) % 1f,
                SpawnTime = (int)DashTimer
            });
            ghostCounter++;
        }

        /// <summary>残影湮灭时蜕落 2 粒星屑，稀疏缓落</summary>
        private void ShedStardust(in DashGhost ghost) {
            if (Main.dedServ) {
                return;
            }
            Vector2 center = ghost.Position + Owner.Size * 0.5f;
            for (int i = 0; i < 2; i++) {
                PRTLoader.NewParticle<PRT_FishUnicornStardust>(center + Main.rand.NextVector2Circular(14f, 18f)
                    , new Vector2(Main.rand.NextFloat(-0.6f, 0.6f), Main.rand.NextFloat(-1.2f, -0.3f))
                    , StardustColor(ghost.Hue), Main.rand.NextFloat(0.42f, 0.70f))
                    ?.Configure(Main.rand.Next(40, 62), 0.05f);
            }
        }

        /// <summary>星屑配色：低饱和高亮度的粉彩，装饰不抢残影的戏</summary>
        private static Color StardustColor(float hue)
            => Main.hslToRgb((hue + Main.rand.NextFloat(-0.05f, 0.05f) + 1f) % 1f, 0.42f, 0.74f);

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //击退增强
            target.velocity += Projectile.velocity.SafeNormalize(Vector2.Zero) * 15f;

            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.NPCHit4 with { Volume = 1f, Pitch = 0.2f }, target.Center);
            SoundEngine.PlaySound(SoundID.DD2_WitherBeastCrystalImpact with { Volume = 0.35f, Pitch = 0.5f }, target.Center);

            //穿刺点：小簇星屑 + 竖压穿刺环（克制，主角仍是残影链）
            float dir = DashDirection >= 0 ? 1f : -1f;
            for (int i = 0; i < 5; i++) {
                Vector2 vel = new Vector2(dir, 0f).RotatedBy(Main.rand.NextFloat(-0.9f, 0.9f)) * Main.rand.NextFloat(2f, 7f);
                PRTLoader.NewParticle<PRT_FishUnicornStardust>(target.Center + Main.rand.NextVector2Circular(12f, 12f)
                    , vel, StardustColor(HeadHue), Main.rand.NextFloat(0.45f, 0.75f))
                    ?.Configure(Main.rand.Next(30, 48), 0.055f);
            }
            PRTLoader.NewParticle<PRT_DWave>(target.Center, Vector2.Zero, new Color(220, 200, 255), 0.22f)
                ?.Configure(new Vector2(0.55f, 1f), dir > 0 ? 0f : MathHelper.Pi, 0.80f, 12);
        }

        private float FishScale() {
            if (DashTimer <= WindupFrames) {
                //蓄势压缩
                return 0.92f;
            }
            if (DashTimer <= WindupFrames + BurstFrames) {
                float bt = DashTimer - WindupFrames;
                return MathHelper.Lerp(0.92f, 1.5f, VaultUtils.EaseOutCubic(MathHelper.Clamp(bt / 5f, 0f, 1f)));
            }
            if (DashTimer <= ActionEnd) {
                float kt = (DashTimer - WindupFrames - BurstFrames) / (float)BrakeFrames;
                return MathHelper.Lerp(1.5f, 1.1f, kt);
            }
            return 1.1f;
        }

        public override bool PreDraw(ref Color lightColor) {
            float dir = DashDirection >= 0 ? 1f : -1f;

            DrawGhostChain();

            if (!TextureAssets.Item[ItemID.UnicornFish].IsLoaded) {
                Main.instance.LoadItem(ItemID.UnicornFish);
            }
            Texture2D fish = TextureAssets.Item[ItemID.UnicornFish].Value;
            Texture2D streak = FishUnicornAssets.Streak?.Value;
            Vector2 origin = fish.Size() / 2f;
            //独角兽鱼贴图呈 45° 斜放，按方向修正到水平
            float drawRotation = dir > 0 ? MathHelper.PiOver4 : -MathHelper.PiOver4;
            SpriteEffects effects = dir > 0 ? SpriteEffects.None : SpriteEffects.FlipHorizontally;

            if (DashTimer <= WindupFrames && streak != null) {
                //蓄势光锥：两道细光从后方收向角尖，扭角随蓄势收拢（螺旋暗示）
                float t = DashTimer / (float)WindupFrames;
                Vector2 tip = castCenter + new Vector2(dir * 58f, -4f) - Main.screenPosition;
                float len = MathHelper.Lerp(18f, 66f, VaultUtils.EaseOutCubic(t));
                float twist = (1f - t) * 0.6f;
                float baseBack = (dir > 0 ? 0f : MathHelper.Pi) + MathHelper.Pi;
                for (int s = -1; s <= 1; s += 2) {
                    float lineAng = baseBack + s * twist;
                    Vector2 center = tip + lineAng.ToRotationVector2() * (len * 0.5f);
                    Main.EntitySpriteDraw(streak, center, null, (PaleGold with { A = 0 }) * (0.55f * t)
                        , lineAng + MathHelper.PiOver2, streak.Size() / 2f
                        , new Vector2(0.14f, len / streak.Height), SpriteEffects.None, 0);
                }
            }

            float fishAlpha = DashTimer <= ActionEnd ? 1f : MathF.Max(0f, 1f - (DashTimer - ActionEnd) / 8f);
            if (DashTimer <= WindupFrames) {
                //蓄势期渐显，禁 pop-in
                fishAlpha = MathHelper.Lerp(0.35f, 1f, DashTimer / (float)WindupFrames);
            }
            if (fishAlpha > 0f) {
                float scale = FishScale();
                Vector2 fishPos = Projectile.Center + new Vector2(dir * 44f, 0f) - Main.screenPosition;
                float speedFactor = MathHelper.Clamp(MathF.Abs(Projectile.velocity.X) / BurstSpeed, 0f, 1f);

                //枪头底光垫层（占比克制）
                Texture2D glow = FishUnicornAssets.Glow?.Value;
                if (glow != null && DashTimer > WindupFrames) {
                    Main.EntitySpriteDraw(glow, HornTip(dir) - Main.screenPosition, null
                        , (PaleGold with { A = 0 }) * (0.22f * fishAlpha * (0.4f + 0.6f * speedFactor))
                        , 0f, glow.Size() / 2f, 0.5f, SpriteEffects.None, 0);
                }

                //速度回声：紧贴身后两帧，色相续接残影队列，速度归零时自动收拢
                if (DashTimer > WindupFrames && speedFactor > 0.05f) {
                    for (int i = 1; i <= 2; i++) {
                        float echoAlpha = (i == 1 ? 0.34f : 0.16f) * fishAlpha;
                        Color echo = Main.hslToRgb((HeadHue - i * GhostHueStep + 1f) % 1f, 0.75f, 0.66f) * echoAlpha;
                        Main.EntitySpriteDraw(fish, fishPos - new Vector2(dir * 26f * i * speedFactor, 0f), null
                            , echo, drawRotation, origin, scale * (1f - 0.06f * i), effects, 0);
                    }
                }

                //角尖光矛：白金细条沿运动轴拖出，长度随速度
                if (streak != null && DashTimer > WindupFrames && speedFactor > 0.05f) {
                    float lanceLen = MathHelper.Lerp(36f, 110f, speedFactor);
                    float worldAng = dir > 0 ? 0f : MathHelper.Pi;
                    Vector2 lanceCenter = HornTip(dir) - new Vector2(dir * lanceLen * 0.35f, 0f) - Main.screenPosition;
                    Main.EntitySpriteDraw(streak, lanceCenter, null, (PaleGold with { A = 0 }) * (0.60f * fishAlpha)
                        , worldAng + MathHelper.PiOver2, streak.Size() / 2f
                        , new Vector2(0.12f, lanceLen / streak.Height), SpriteEffects.None, 0);
                }

                //鱼形枪头本体
                Main.EntitySpriteDraw(fish, fishPos, null, Color.White * fishAlpha, drawRotation, origin, scale, effects, 0);
            }

            //起跳过曝爆点，只准亮 2 帧
            Texture2D flash = FishUnicornAssets.Flash?.Value;
            int sinceLaunch = (int)DashTimer - WindupFrames;
            if (flash != null && sinceLaunch >= 0 && sinceLaunch <= 1) {
                float k = 1f - sinceLaunch * 0.45f;
                Main.EntitySpriteDraw(flash, HornTip(dir) - Main.screenPosition, null
                    , new Color(255, 248, 232, 0) * (0.85f * k), 0f, flash.Size() / 2f
                    , 0.15f + 0.55f * k, SpriteEffects.None, 0);
            }

            return false;
        }

        /// <summary>
        /// 残影链绘制：切换到玩家渲染批次，逐个以单色实体绘出快照。<br/>
        /// 弹幕层先于玩家层，残影天然垫在本体之下（夹心）
        /// </summary>
        private void DrawGhostChain() {
            if (ghosts.Count == 0 || Main.dedServ) {
                return;
            }
            Player owner = Owner;
            if (owner == null || !owner.active) {
                return;
            }

            SpriteBatch sb = Main.spriteBatch;
            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp
                , null, Main.Rasterizer, null, Main.GameViewMatrix.ZoomMatrix);

            PlayerCloneRenderer.Prepare(owner);
            foreach (DashGhost ghost in ghosts) {
                float age = (DashTimer - ghost.SpawnTime) / (float)GhostLife;
                float alpha = MathF.Pow(1f - MathHelper.Clamp(age, 0f, 1f), 1.25f);
                if (alpha <= 0.02f) {
                    continue;
                }
                //每个残影单色：饱和中亮度色相，A 通道随寿命淡出
                Color tint = Main.hslToRgb(ghost.Hue, 0.88f, 0.60f);
                tint.A = (byte)(235 * alpha);
                PlayerCloneRenderer.DrawPrepared(ghost.Position, tint, ghost.Direction
                    , ghost.BodyFrame, ghost.LegFrame);
            }

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState
                , DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
        }
    }
}
