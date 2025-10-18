using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.FishSkills
{
    internal class FishBloodyManowar : FishSkill
    {
        public override int UnlockFishID => ItemID.BloodyManowar;
        public override int DefaultCooldown => 180 - HalibutData.GetDomainLayer() * 12;
        public override int ResearchDuration => 60 * 18;

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

            Vector2 targetDirection = (Main.MouseWorld - player.Center).SafeNormalize(Vector2.UnitX * player.direction);
            ShootState shootState = player.GetShootState();

            //生成斧头控制器
            Projectile.NewProjectile(
                player.GetSource_ItemUse(item),
                player.Center,
                targetDirection,
                ModContent.ProjectileType<BloodyAxeController>(),
                (int)(shootState.WeaponDamage * (2.8f + HalibutData.GetDomainLayer() * 0.6f)),
                shootState.WeaponKnockback * 2.5f,
                player.whoAmI
            );

            //音效
            SoundEngine.PlaySound(SoundID.NPCDeath19 with { Volume = 0.7f, Pitch = -0.3f }, player.Center);
            SoundEngine.PlaySound(SoundID.Splash with { Volume = 0.6f, Pitch = -0.2f }, player.Center);
        }
    }

    /// <summary>
    /// 血腥斧头控制器，管理水母聚集和劈砍动作
    /// </summary>
    internal class BloodyAxeController : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;

        private enum AxePhase
        {
            Gathering,//聚集阶段
            Raising,//抬起阶段
            Striking,//劈砍阶段
            Dissipating//消散阶段
        }

        private AxePhase Phase {
            get => (AxePhase)Projectile.ai[0];
            set => Projectile.ai[0] = (float)value;
        }

        private ref float PhaseTimer => ref Projectile.ai[1];
        private Player Owner => Main.player[Projectile.owner];

        private List<int> jellyfishList = new List<int>();
        private Vector2 axeDirection;
        public Vector2 axeCenter;//公开给水母访问

        private const int GatherDuration = 30;//聚集时长
        private const int RaiseDuration = 18;//抬起时长
        private const int StrikeDuration = 22;//劈砍时长
        private const int DissipateDuration = 35;//消散时长

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 200;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = GatherDuration + RaiseDuration + StrikeDuration + DissipateDuration + 10;
        }

        public override void AI() {
            if (!Owner.active || Owner.dead) {
                Projectile.Kill();
                return;
            }

            PhaseTimer++;
            axeDirection = Projectile.velocity.SafeNormalize(Vector2.UnitX);

            switch (Phase) {
                case AxePhase.Gathering:
                    GatheringPhaseAI();
                    break;
                case AxePhase.Raising:
                    RaisingPhaseAI();
                    break;
                case AxePhase.Striking:
                    StrikingPhaseAI();
                    break;
                case AxePhase.Dissipating:
                    DissipatingPhaseAI();
                    break;
            }

            //更新弹幕中心位置供网络同步
            Projectile.Center = axeCenter;
        }

        private void GatheringPhaseAI() {
            if (PhaseTimer == 1) {
                //生成水母组成斧头形状
                int jellyfishCount = 35 + HalibutData.GetDomainLayer() * 5;
                Vector2 spawnCenter = Owner.Center + axeDirection * 150f;

                //斧头形状的水母分布
                for (int i = 0; i < jellyfishCount; i++) {
                    //计算该水母在斧头上的相对偏移
                    Vector2 offset = CalculateAxeShapeOffset(i, jellyfishCount);

                    int proj = Projectile.NewProjectile(
                        Projectile.GetSource_FromThis(),
                        Owner.Center,//初始在玩家位置
                        Vector2.Zero,
                        ModContent.ProjectileType<BloodyJellyfishUnit>(),
                        Projectile.damage,
                        Projectile.knockBack,
                        Projectile.owner,
                        Projectile.identity,//控制器ID
                        i//水母索引
                    );

                    if (proj >= 0) {
                        jellyfishList.Add(proj);
                        //设置水母的目标偏移
                        if (Main.projectile[proj].ModProjectile is BloodyJellyfishUnit unit) {
                            unit.localOffset = offset;
                        }
                    }
                }

                //聚集特效
                for (int i = 0; i < 30; i++) {
                    Vector2 velocity = Main.rand.NextVector2Circular(6f, 6f);
                    Dust gather = Dust.NewDustPerfect(
                        spawnCenter,
                        DustID.Blood,
                        velocity,
                        100,
                        Color.DarkRed,
                        Main.rand.NextFloat(1.2f, 2f)
                    );
                    gather.noGravity = true;
                }
            }

            //聚集阶段保持在玩家前方
            axeCenter = Owner.Center + axeDirection * 150f;

            if (PhaseTimer >= GatherDuration) {
                Phase = AxePhase.Raising;
                PhaseTimer = 0;
                SoundEngine.PlaySound(SoundID.Roar with { Volume = 0.5f, Pitch = -0.4f }, axeCenter);
            }
        }

        private void RaisingPhaseAI() {
            //抬起斧头到玩家上方
            float progress = PhaseTimer / RaiseDuration;
            float easeProgress = MathF.Pow(progress, 0.6f);

            Vector2 startPos = Owner.Center + axeDirection * 150f;
            Vector2 endPos = Owner.Center + new Vector2(axeDirection.X * 60f, -200f);
            axeCenter = Vector2.Lerp(startPos, endPos, easeProgress);

            //抬起粒子
            if (PhaseTimer % 3 == 0) {
                Vector2 dustPos = axeCenter + Main.rand.NextVector2Circular(80f, 80f);
                Dust raise = Dust.NewDustPerfect(
                    dustPos,
                    DustID.Blood,
                    new Vector2(0, -Main.rand.NextFloat(2f, 4f)),
                    100,
                    Color.Red,
                    Main.rand.NextFloat(1f, 1.5f)
                );
                raise.noGravity = true;
            }

            if (PhaseTimer >= RaiseDuration) {
                Phase = AxePhase.Striking;
                PhaseTimer = 0;
                SoundEngine.PlaySound(SoundID.Item1 with { Volume = 0.9f, Pitch = -0.5f }, axeCenter);
            }
        }

        private void StrikingPhaseAI() {
            //劈砍动作
            float progress = PhaseTimer / StrikeDuration;
            float strikeProgress = 1f - MathF.Pow(1f - progress, 3f);//加速曲线

            Vector2 startPos = Owner.Center + new Vector2(axeDirection.X * 60f, -200f);
            Vector2 endPos = Owner.Center + new Vector2(axeDirection.X * 100f, 140f);
            axeCenter = Vector2.Lerp(startPos, endPos, strikeProgress);

            //劈砍音效
            if (PhaseTimer == 5) {
                SoundEngine.PlaySound(SoundID.DD2_MonkStaffSwing with { Volume = 0.8f, Pitch = -0.3f }, axeCenter);
            }

            //冲击波效果
            if (PhaseTimer == 12) {
                CreateStrikeImpact();
            }

            //劈砍粒子
            if (PhaseTimer % 2 == 0) {
                SpawnStrikeParticles();
            }

            if (PhaseTimer >= StrikeDuration) {
                Phase = AxePhase.Dissipating;
                PhaseTimer = 0;
            }
        }

        private void DissipatingPhaseAI() {
            //保持在最终位置
            axeCenter = Owner.Center + new Vector2(axeDirection.X * 100f, 140f);

            //消散所有水母
            if (PhaseTimer == 1) {
                foreach (int projIndex in jellyfishList) {
                    if (Main.projectile.IndexInRange(projIndex) && Main.projectile[projIndex].active) {
                        Main.projectile[projIndex].ai[2] = 1f;//触发消散
                    }
                }
            }

            if (PhaseTimer >= DissipateDuration) {
                Projectile.Kill();
            }
        }

        private Vector2 CalculateAxeShapeOffset(int index, int total) {
            //斧头形状：上窄下宽的三角形
            float t = index / (float)total;

            //分成三部分：刃部、身部、柄部
            if (t < 0.35f) {//刃部(35%)
                float bladeT = t / 0.35f;
                float width = MathHelper.Lerp(10f, 100f, bladeT);//从10宽到100宽
                float x = (bladeT - 0.5f) * width * 2f;
                float y = -bladeT * 140f;//向上延伸
                return new Vector2(x, y);
            }
            else if (t < 0.65f) {//身部(30%)
                float bodyT = (t - 0.35f) / 0.3f;
                float width = MathHelper.Lerp(100f, 50f, bodyT);//从100宽到50宽
                float x = (bodyT - 0.5f) * width * 2f;
                float y = -140f + bodyT * 100f;
                return new Vector2(x, y);
            }
            else {//柄部(35%)
                float handleT = (t - 0.65f) / 0.35f;
                float x = (handleT - 0.5f) * 30f;//柄部较窄
                float y = -40f + handleT * 80f;
                return new Vector2(x, y);
            }
        }

        private void CreateStrikeImpact() {
            Vector2 impactPos = Owner.Center + new Vector2(axeDirection.X * 100f, 140f);

            //生成冲击波弹幕
            Projectile.NewProjectile(
                Projectile.GetSource_FromThis(),
                impactPos,
                Vector2.Zero,
                ModContent.ProjectileType<BloodyStrikeWave>(),
                Projectile.damage * 2,
                Projectile.knockBack * 2f,
                Projectile.owner
            );

            //地面冲击音效
            SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.8f, Pitch = -0.3f }, impactPos);
            SoundEngine.PlaySound(SoundID.DD2_ExplosiveTrapExplode with { Volume = 0.6f, Pitch = -0.4f }, impactPos);

            //冲击粒子
            for (int i = 0; i < 40; i++) {
                float angle = MathHelper.Lerp(-MathHelper.PiOver2, MathHelper.PiOver2, i / 40f);
                Vector2 velocity = new Vector2(axeDirection.X, 0).RotatedBy(angle) * Main.rand.NextFloat(8f, 16f);

                Dust impact = Dust.NewDustPerfect(
                    impactPos,
                    DustID.Blood,
                    velocity,
                    100,
                    Color.DarkRed,
                    Main.rand.NextFloat(1.5f, 2.5f)
                );
                impact.noGravity = Main.rand.NextBool();
            }

            //地面裂纹效果
            for (int i = 0; i < 20; i++) {
                Vector2 crackVel = new Vector2(axeDirection.X * Main.rand.NextFloat(6f, 12f), Main.rand.NextFloat(-2f, 1f));
                Dust crack = Dust.NewDustPerfect(
                    impactPos + new Vector2(Main.rand.NextFloat(-40f, 40f), 0),
                    DustID.Stone,
                    crackVel,
                    100,
                    Color.Gray,
                    Main.rand.NextFloat(1.2f, 1.8f)
                );
                crack.noGravity = false;
            }
        }

        private void SpawnStrikeParticles() {
            //劈砍轨迹粒子
            Vector2 particlePos = axeCenter + Main.rand.NextVector2Circular(80f, 80f);

            Dust trail = Dust.NewDustPerfect(
                particlePos,
                DustID.Blood,
                Main.rand.NextVector2Circular(4f, 4f),
                100,
                Color.Red,
                Main.rand.NextFloat(1.2f, 2f)
            );
            trail.noGravity = true;
        }

        public override void OnKill(int timeLeft) {
            //清理所有水母
            foreach (int projIndex in jellyfishList) {
                if (Main.projectile.IndexInRange(projIndex) && Main.projectile[projIndex].active) {
                    Main.projectile[projIndex].Kill();
                }
            }
        }
    }

    /// <summary>
    /// 单个血腥水母单元
    /// </summary>
    internal class BloodyJellyfishUnit : ModProjectile
    {
        public override string Texture => "Terraria/Images/Item_" + ItemID.BloodyManowar;

        private ref float ControllerID => ref Projectile.ai[0];
        private ref float UnitIndex => ref Projectile.ai[1];
        private ref float IsDissipating => ref Projectile.ai[2];

        public Vector2 localOffset;//水母在斧头上的相对位置偏移
        private float rotation;
        private float pulsePhase;
        private float dissipateAlpha = 1f;

        public override void SetDefaults() {
            Projectile.width = 30;
            Projectile.height = 30;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 200;
        }

        public override void AI() {
            if (!ControllerID.TryGetProjectile(out var controller)) {
                Projectile.Kill();
                return;
            }

            //重置生命时间
            Projectile.timeLeft = 200;

            pulsePhase += 0.12f;
            float pulse = MathF.Sin(pulsePhase + UnitIndex * 0.3f);

            if (controller.ModProjectile is BloodyAxeController axeCtrl) {
                if (IsDissipating == 0) {
                    //根据控制器的axeDirection旋转localOffset
                    Vector2 axeDir = controller.velocity.SafeNormalize(Vector2.UnitX);
                    float axeRotation = axeDir.ToRotation() - MathHelper.PiOver2;//斧头朝向

                    //将局部偏移旋转到世界坐标
                    Vector2 rotatedOffset = localOffset.RotatedBy(axeRotation);

                    //目标位置 = 控制器中心 + 旋转后的偏移
                    Vector2 targetPos = axeCtrl.axeCenter + rotatedOffset;

                    //平滑移动到目标位置
                    Projectile.Center = Vector2.Lerp(Projectile.Center, targetPos, 0.25f);

                    //水母朝向斧头的前方
                    Projectile.rotation = axeRotation + MathHelper.PiOver4;
                }
                else {
                    //消散状态：向外飞散
                    dissipateAlpha -= 0.035f;
                    Projectile.velocity = Main.rand.NextVector2Circular(4f, 4f);
                    Projectile.rotation += 0.15f;

                    if (dissipateAlpha <= 0) {
                        Projectile.Kill();
                    }
                }
            }

            //旋转动画
            rotation += 0.06f * (UnitIndex % 2 == 0 ? 1 : -1);

            //脉动效果
            Projectile.scale = 0.85f + pulse * 0.12f;

            //周期性粒子
            if (Main.rand.NextBool(10) && IsDissipating == 0) {
                Dust d = Dust.NewDustPerfect(
                    Projectile.Center + Main.rand.NextVector2Circular(8f, 8f),
                    DustID.Blood,
                    Vector2.Zero,
                    100,
                    Color.DarkRed,
                    Main.rand.NextFloat(0.5f, 0.8f)
                );
                d.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D texture = TextureAssets.Item[ItemID.BloodyManowar].Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Vector2 origin = texture.Size() / 2f;

            Color drawColor = Color.Lerp(Color.DarkRed, Color.Red, 0.5f) * dissipateAlpha;

            //绘制阴影
            for (int i = 0; i < 2; i++) {
                Vector2 shadowOffset = new Vector2(i * 1.5f, i * 1.5f);
                Main.EntitySpriteDraw(
                    texture,
                    drawPos + shadowOffset,
                    null,
                    Color.Black * 0.25f * dissipateAlpha,
                    Projectile.rotation + rotation,
                    origin,
                    Projectile.scale * 0.95f,
                    SpriteEffects.None,
                    0
                );
            }

            //绘制主体
            Main.EntitySpriteDraw(
                texture,
                drawPos,
                null,
                drawColor,
                Projectile.rotation + rotation,
                origin,
                Projectile.scale,
                SpriteEffects.None,
                0
            );

            //发光层
            if (IsDissipating == 0) {
                Color glowColor = Color.Red * 0.35f * dissipateAlpha;
                glowColor.A = 0;
                Main.EntitySpriteDraw(
                    texture,
                    drawPos,
                    null,
                    glowColor,
                    Projectile.rotation + rotation,
                    origin,
                    Projectile.scale * 1.12f,
                    SpriteEffects.None,
                    0
                );
            }

            return false;
        }
    }

    /// <summary>
    /// 劈砍冲击波
    /// </summary>
    internal class BloodyStrikeWave : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 320;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 30;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override void AI() {
            Projectile.scale += 0.15f;
            Projectile.alpha += 10;

            //扩散血雾
            if (Main.rand.NextBool(3)) {
                Vector2 pos = Projectile.Center + Main.rand.NextVector2Circular(Projectile.width / 2, Projectile.height / 2);
                Dust mist = Dust.NewDustPerfect(
                    pos,
                    DustID.Blood,
                    Main.rand.NextVector2Circular(2f, 2f),
                    100,
                    Color.DarkRed,
                    Main.rand.NextFloat(1.5f, 2.5f)
                );
                mist.noGravity = true;
            }

            if (Projectile.alpha >= 255) {
                Projectile.Kill();
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //血腥减速效果
            target.velocity *= 0.5f;

            //血液飞溅
            for (int i = 0; i < 8; i++) {
                Dust blood = Dust.NewDustDirect(
                    target.position,
                    target.width,
                    target.height,
                    DustID.Blood,
                    Main.rand.NextFloat(-4f, 4f),
                    Main.rand.NextFloat(-4f, 4f),
                    100,
                    default,
                    Main.rand.NextFloat(1.2f, 2f)
                );
                blood.noGravity = false;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D warpTex = CWRUtils.GetT2DValue(CWRConstant.Masking + "DiffusionCircle");
            Color warpColor = new Color(80, 0, 0) * (1f - Projectile.alpha / 255f);
            warpColor.A = 0;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Vector2 origin = warpTex.Size() / 2f;

            for (int i = 0; i < 3; i++) {
                Main.spriteBatch.Draw(
                    warpTex,
                    drawPos,
                    null,
                    warpColor * 0.8f,
                    Projectile.rotation + i * MathHelper.TwoPi / 3f,
                    origin,
                    Projectile.scale * (1f + i * 0.1f),
                    SpriteEffects.None,
                    0f
                );
            }

            return false;
        }
    }
}
