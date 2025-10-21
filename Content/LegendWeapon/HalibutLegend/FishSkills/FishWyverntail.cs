using InnoVault.GameContent.BaseEntity;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.FishSkills
{
    internal class FishWyverntail : FishSkill
    {
        public override int UnlockFishID => ItemID.Wyverntail;
        public override int DefaultCooldown => 60 * 25 - HalibutData.GetDomainLayer() * 8; //冷却较长
        public override int ResearchDuration => 60 * 35;

        //每个玩家仅允许一个控制器存在
        private static bool PlayerHasController(Player player) {
            int type = ModContent.ProjectileType<WhiteWyvernTailController>();
            for (int i = 0; i < Main.maxProjectiles; i++) {
                if (Main.projectile[i].active && Main.projectile[i].owner == player.whoAmI && Main.projectile[i].type == type) {
                    return true;
                }
            }
            return false;
        }

        public override bool? Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source,
            Vector2 position, Vector2 velocity, int type, int damage, float knockback) {

            //如果已经存在控制器则不重复生成，允许提前使用观察冷却
            if (Cooldown > 0) {
                return null;
            }

            if (!PlayerHasController(player)) {
                //生成控制器：周期性发射小白龙
                int proj = Projectile.NewProjectile(source, player.Center, Vector2.Zero,
                    ModContent.ProjectileType<WhiteWyvernTailController>(), 0, 0f, player.whoAmI,
                    ai0: damage, ai1: knockback);
                if (proj >= 0) {
                    //召唤特效 & 声音
                    SpawnSummonEffect(player.Center);
                }
                SetCooldown();
            }
            return null;
        }

        private static void SpawnSummonEffect(Vector2 position) {
            for (int i = 0; i < 22; i++) {
                float angle = MathHelper.TwoPi * i / 22f;
                Vector2 vel = angle.ToRotationVector2() * Main.rand.NextFloat(3f, 7f);
                Dust d = Dust.NewDustPerfect(position, DustID.WhiteTorch, vel, 100, new Color(200, 230, 255), Main.rand.NextFloat(1.2f, 1.9f));
                d.noGravity = true;
                d.fadeIn = 1.2f;
            }
            for (int i = 0; i < 12; i++) {
                Dust shard = Dust.NewDustDirect(position - new Vector2(12), 24, 24, DustID.SilverFlame, Scale: Main.rand.NextFloat(1f, 1.6f));
                shard.velocity = Main.rand.NextVector2Circular(4f, 4f);
                shard.noGravity = true;
            }
        }
    }

    /// <summary>
    /// 控制器弹幕，周期性发射小白龙，持续一定时间
    /// </summary>
    internal class WhiteWyvernTailController : BaseHeldProj
    {
        public override string Texture => CWRConstant.Placeholder;

        private const int BaseLifeTime = 60 * 20;//控制器寿命 20秒
        private ref float BaseDamage => ref Projectile.ai[0];
        private ref float BaseKnockback => ref Projectile.ai[1];
        private ref float Timer => ref Projectile.ai[2];

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 12;
            Projectile.tileCollide = false;
            Projectile.timeLeft = BaseLifeTime;
            Projectile.penetrate = -1;
            Projectile.ignoreWater = true;
            Projectile.friendly = false;
        }

        public override void AI() {
            if (!Owner.active || Owner.dead || !FishSkill.GetT<FishWyverntail>().Active(Owner)) {
                Projectile.Kill();
                return;
            }

            //跟随玩家
            Projectile.Center = Owner.Center + new Vector2(0, -40);

            Timer++;
            int layer = HalibutData.GetDomainLayer(Owner);
            int interval = Math.Clamp(120 - layer * 8, 35, 120); //发射间隔随层数减少
            int batch = 1 + layer / 4; //层数提升后一次发射多条

            if (Timer % interval == 0) {
                //寻找目标 NPC
                NPC target = Owner.Center.FindClosestNPC(1400f);
                Vector2 firePos = Projectile.Center + Main.rand.NextVector2Circular(24f, 24f);
                for (int i = 0; i < batch; i++) {
                    Vector2 dir;
                    if (target != null && target.active && target.CanBeChasedBy()) {
                        Vector2 predictive = target.Center + target.velocity * 18f; //简单预判
                        dir = (predictive - firePos).SafeNormalize(Vector2.UnitY);
                    }
                    else {
                        dir = Main.rand.NextVector2Unit();
                    }
                    float speed = Main.rand.NextFloat(12f, 16f) + layer * 0.8f;
                    if (Projectile.IsOwnedByLocalPlayer()) {
                        int proj = Projectile.NewProjectile(Projectile.GetSource_FromAI(), firePos, dir * speed,
                        ModContent.ProjectileType<MiniWhiteWyvern>(),
                        (int)(BaseDamage * (1.4f + layer * 0.25f)), BaseKnockback * 0.35f, Owner.whoAmI,
                        ai0: target?.whoAmI ?? -1);
                        if (proj >= 0) {
                            Main.projectile[proj].scale = 0.9f + layer * 0.03f;
                        }
                    }
                }

                //发射音效与粒子
                SoundEngine.PlaySound(SoundID.DD2_OgreRoar with { Volume = 0.6f, Pitch = -0.2f }, firePos);
                for (int k = 0; k < 18; k++) {
                    Vector2 v = Main.rand.NextVector2Circular(5f, 5f);
                    Dust d = Dust.NewDustPerfect(firePos, DustID.WhiteTorch, v, 120, new Color(220, 240, 255), Main.rand.NextFloat(0.9f, 1.4f));
                    d.noGravity = true;
                }
            }

            //微弱视觉提示（光照）
            Lighting.AddLight(Projectile.Center, 0.25f, 0.35f, 0.5f);
        }

        public override bool? CanDamage() => false;
        public override bool PreDraw(ref Color lightColor) => false; //不绘制
    }

    /// <summary>
    /// 追踪小白龙，使用原版 WyvernHead 纹理
    /// </summary>
    internal class MiniWhiteWyvern : ModProjectile
    {
        public override string Texture => "Terraria/Images/NPC_" + NPCID.WyvernHead;

        private ref float TargetIndex => ref Projectile.ai[0];
        private ref float StateTimer => ref Projectile.ai[1];
        private ref float SerpentinePhase => ref Projectile.ai[2];

        private NPC target;
        private float desiredRot;
        private float serpAmplitude;
        private float serpFrequency;

        public override void SetStaticDefaults() {
            Main.projFrames[Projectile.type] = Main.npcFrameCount[NPCID.WyvernHead];
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 50;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 54; //取头部大致尺寸
            Projectile.height = 54;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 60 * 8;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 20;
        }

        public override bool? CanDamage() => true;

        public override void AI() {
            StateTimer++;
            SerpentinePhase += 0.15f;

            //获取目标
            if (TargetIndex >= 0 && TargetIndex < Main.maxNPCs && Main.npc[(int)TargetIndex].active && Main.npc[(int)TargetIndex].CanBeChasedBy()) {
                target = Main.npc[(int)TargetIndex];
            }
            else {
                target = Projectile.Center.FindClosestNPC(1000f);
                if (target != null) TargetIndex = target.whoAmI;
            }

            //初始 serpentine 参数
            if (StateTimer == 1) {
                serpAmplitude = Main.rand.NextFloat(12f, 24f);
                serpFrequency = Main.rand.NextFloat(1.2f, 2f);
            }

            //若存在目标则追踪
            if (target != null) {
                Vector2 toTarget = target.Center - Projectile.Center;
                float dist = toTarget.Length();
                Vector2 dir = toTarget.SafeNormalize(Vector2.UnitX);

                //蛇形偏移：垂直于朝向的法线
                Vector2 normal = dir.RotatedBy(MathHelper.PiOver2);
                float wave = (float)Math.Sin(SerpentinePhase * serpFrequency) * serpAmplitude * MathHelper.Clamp(dist / 400f, 0.2f, 1f);
                Vector2 desiredVel = dir * MathHelper.Clamp(18f + dist * 0.01f, 12f, 32f) + normal * wave;
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, desiredVel, 0.15f);
                desiredRot = Projectile.velocity.ToRotation();
                if (dist < 40f) {
                    serpAmplitude *= 0.85f; //靠近时减幅
                }
            }
            else {
                //无目标缓慢游动
                Projectile.velocity *= 0.97f;
                desiredRot = Projectile.velocity.ToRotation();
            }

            //平滑旋转
            Projectile.rotation = desiredRot;

            //帧动画：根据速度与状态摆动
            int frameCount = Main.projFrames[Projectile.type];
            float animSpeed = MathHelper.Clamp(Projectile.velocity.Length() / 16f, 0.3f, 1.4f);
            if (++Projectile.frameCounter >= 6 / animSpeed) {
                Projectile.frameCounter = 0;
                Projectile.frame++;
                if (Projectile.frame >= frameCount) Projectile.frame = 0;
            }

            //粒子效果
            if (Main.rand.NextBool(5)) {
                Dust d = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.WhiteTorch,
                    -Projectile.velocity.X * 0.2f, -Projectile.velocity.Y * 0.2f, 120, new Color(220, 240, 255), Main.rand.NextFloat(0.8f, 1.2f));
                d.noGravity = true;
            }

            //寿命末尾淡出
            if (Projectile.timeLeft < 40) {
                Projectile.alpha = (int)MathHelper.Lerp(0, 255, 1f - Projectile.timeLeft / 40f);
            }

            Lighting.AddLight(Projectile.Center, 0.35f, 0.45f, 0.65f);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //撞击后爆开少量粒子
            for (int i = 0; i < 14; i++) {
                Vector2 v = Main.rand.NextVector2Circular(6f, 6f);
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.WhiteTorch, v, 100, new Color(255, 255, 255), Main.rand.NextFloat(1f, 1.6f));
                d.noGravity = true;
            }
            SoundEngine.PlaySound(SoundID.NPCHit13 with { Volume = 0.6f, Pitch = 0.2f }, Projectile.Center);
        }

        public override bool PreDraw(ref Color lightColor) {
            //加载原版白龙头纹理
            Main.instance.LoadNPC(NPCID.WyvernHead);
            Texture2D vaule = TextureAssets.Npc[NPCID.WyvernHead].Value;
            int frameHeight = vaule.Height / Main.npcFrameCount[NPCID.WyvernHead];
            Rectangle source = new Rectangle(0, Projectile.frame * frameHeight, vaule.Width, frameHeight);
            Vector2 origin = source.Size() / 2f;
            float fade = 1f - Projectile.alpha / 255f;
            float drawOffset = MathHelper.PiOver2;

            //绘制拖尾
            for (int i = 1; i < Projectile.oldPos.Length; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;
                float progress = 1f - i / (float)Projectile.oldPos.Length;
                Color trailColor = new Color(180, 210, 240) * progress * 0.5f * fade;
                Vector2 drawPos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                float rot = Projectile.oldRot[i] + drawOffset;
                Main.EntitySpriteDraw(vaule, drawPos, source, trailColor, rot, origin, Projectile.scale * (0.9f + progress * 0.1f), SpriteEffects.None, 0);
            }

            //主体
            SpriteEffects spriteEffect = Projectile.velocity.X < 0 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
            Main.EntitySpriteDraw(vaule, Projectile.Center - Main.screenPosition, source, lightColor * fade, Projectile.rotation + drawOffset, origin, Projectile.scale, spriteEffect, 0);

            //轻微辉光
            Color glow = new Color(255, 255, 255, 0) * 0.35f * fade;
            Main.EntitySpriteDraw(vaule, Projectile.Center - Main.screenPosition, source, glow, Projectile.rotation + drawOffset, origin, Projectile.scale * 1.05f, spriteEffect, 0);
            return false;
        }

        public override Color? GetAlpha(Color lightColor) => new Color(255, 255, 255, 200) * (1f - Projectile.alpha / 255f);
    }
}
