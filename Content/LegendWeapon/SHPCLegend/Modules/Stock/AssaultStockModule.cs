using CalamityOverhaul.Common;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Stock
{
    /// <summary>
    /// 突击枪托：托内嵌入双联协战模块。持握 SHPC 时在玩家两肩展开一对
    /// 「悬浮炮臂」，与玩家的每次射击同步交替速射协战镖弹（50% 武器伤害）
    /// （机械骷髅王礼物 —— 多臂火力平台的余响）
    /// </summary>
    internal sealed class AssaultStockModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Stock;
        //突击装甲橙
        public override Color TintColor => new(255, 150, 70);

        public override void Apply(ref ShootContext ctx) {
            ctx.AttackSpeedMul += 0.06f;
            ctx.ManaCostMul += 0.25f;
        }

        public override void OnPlayerUpdate(Player player) {
            if (player.whoAmI != Main.myPlayer) return;
            if (player.HeldItem == null || player.HeldItem.type != SHPCOverride.ID) return;
            int armType = ModContent.ProjectileType<SHPCAssaultArmProj>();
            if (player.ownedProjectileCounts[armType] >= 2) return;
            //左右双臂：ai0 记录侧别
            for (int side = -1; side <= 1; side += 2) {
                Projectile.NewProjectile(player.GetSource_FromThis(),
                    player.Center, Vector2.Zero, armType, 0, 0f, player.whoAmI,
                    ai0: side);
            }
        }
    }

    /// <summary>
    /// 悬浮炮臂：悬停在玩家肩侧的微缩 SHPC 复制体，炮口始终跟随光标。
    /// 侦测到玩家击发主武器的瞬间，左右臂交替射出协战光弹。
    /// 改件被卸下时立即自毁
    /// </summary>
    internal sealed class SHPCAssaultArmProj : ModProjectile, IAdditiveDrawable
    {
        public override string Texture => CWRConstant.Placeholder;

        private static readonly Color ArmMain = new(255, 160, 80);
        private static readonly Color ArmEdge = new(180, 70, 20);
        private static readonly Color ArmCore = new(255, 235, 200);
        //本体贴图缩放：SHPC 原图 152x70，缩小为肩侧炮荚尺寸
        private const float BodyScale = 0.42f;

        private int Side => (int)Projectile.ai[0];
        private float aimRotation;
        private float recoil;
        private int prevItemAnimation;
        /// <summary>本臂观测到的击发事件计数：双臂各自计数同一串事件，按奇偶分工交替开火</summary>
        private int observedShots;

        public override void SetDefaults() {
            Projectile.width = 20;
            Projectile.height = 20;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 30;
        }

        public override void AI() {
            Player owner = Main.player[Projectile.owner];
            if (owner == null || !owner.active || owner.dead
                || owner.HeldItem == null || owner.HeldItem.type != SHPCOverride.ID
                || !SHPCModificationSystem.HasModule<AssaultStockModule>(owner)) {
                Projectile.Kill();
                return;
            }
            Projectile.timeLeft = 30;

            //肩侧悬停：上下轻微浮动 + 平滑跟随
            float bob = MathF.Sin((float)Main.timeForVisualEffects * 0.07f + Side * 1.9f) * 4f;
            Vector2 anchor = owner.Center + new Vector2(-owner.direction * 26f * Side, -38f + bob);
            Projectile.Center = Vector2.Lerp(Projectile.Center, anchor, 0.25f);

            Vector2 toMouse = Main.MouseWorld - Projectile.Center;
            aimRotation = aimRotation.AngleLerp(toMouse.ToRotation(), 0.3f);
            recoil = MathF.Max(recoil - 0.12f, 0f);

            //击发侦测：主武器动画从满值跳变的那一帧即为开火帧（仅左键攻击）
            if (Projectile.owner == Main.myPlayer
                && owner.ItemAnimationActive
                && owner.altFunctionUse != 2
                && owner.itemAnimation > prevItemAnimation) {
                observedShots++;
                //左右臂按奇偶交替开火
                if ((observedShots & 1) == (Side > 0 ? 0 : 1)) {
                    FireBolt(owner);
                }
            }
            prevItemAnimation = owner.itemAnimation;
        }

        private void FireBolt(Player owner) {
            int dmg = Math.Max((int)(owner.GetWeaponDamage(owner.HeldItem) * 0.5f), 1);
            Vector2 dir = (Main.MouseWorld - Projectile.Center).SafeNormalize(Vector2.UnitX)
                .RotatedBy(Main.rand.NextFloat(-0.05f, 0.05f));
            Projectile.NewProjectile(Projectile.GetSource_FromThis(),
                Projectile.Center + dir * 16f, dir * 17f,
                ModContent.ProjectileType<SHPCArmBoltProj>(),
                dmg, 1.5f, Projectile.owner);
            recoil = 1f;
            if (Main.netMode != NetmodeID.Server) {
                SoundEngine.PlaySound(SoundID.Item12 with { Volume = 0.3f, Pitch = 0.6f }, Projectile.Center);
                for (int i = 0; i < 3; i++) {
                    PRTLoader.NewParticle<PRT_Spark>(Projectile.Center + dir * 18f,
                        dir.RotatedBy(Main.rand.NextFloat(-0.4f, 0.4f)) * Main.rand.NextFloat(2f, 5f),
                        ArmMain, Main.rand.NextFloat(0.4f, 0.8f)).Configure(true, Main.rand.Next(6, 12));
                }
            }
        }

        public override void OnKill(int timeLeft) {
            if (Main.netMode == NetmodeID.Server) return;
            //回收时的解体闪光，避免凭空消失
            for (int i = 0; i < 8; i++) {
                PRTLoader.NewParticle<PRT_Spark>(Projectile.Center,
                    Main.rand.NextVector2Circular(3f, 3f),
                    ArmMain, Main.rand.NextFloat(0.4f, 0.8f)).Configure(true, Main.rand.Next(8, 14));
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            //本体直接复用 SHPC 武器贴图的微缩版，避免像素拼合的潦草感
            Texture2D body = TextureAssets.Item[SHPCOverride.ID].Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition
                + aimRotation.ToRotationVector2() * -recoil * 5f;
            //武器贴图默认朝右，瞄向左侧时垂直翻转避免倒持
            SpriteEffects flip = MathF.Cos(aimRotation) < 0f
                ? SpriteEffects.FlipVertically : SpriteEffects.None;
            Color bodyColor = Color.Lerp(lightColor, Color.White, 0.45f);
            Main.EntitySpriteDraw(body, drawPos, null, bodyColor, aimRotation,
                body.Size() * 0.5f, BodyScale, flip);
            return false;
        }

        void IAdditiveDrawable.DrawAdditiveAfterNon(SpriteBatch spriteBatch) {
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            Texture2D star = CWRAsset.StarTexture_White?.Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition
                + aimRotation.ToRotationVector2() * -recoil * 5f;

            //机身环境辉光
            if (glow != null) {
                spriteBatch.Draw(glow, drawPos, null, ArmEdge * 0.45f, 0f,
                    glow.Size() * 0.5f, 0.8f, SpriteEffects.None, 0f);
            }
            //炮口充能指示：随相位闪烁的小型十字耀斑
            float blink = 0.55f + 0.45f * MathF.Sin((float)Main.timeForVisualEffects * 0.2f + Side * 2.6f);
            Vector2 muzzlePos = drawPos + aimRotation.ToRotationVector2() * (76f * BodyScale);
            if (star != null) {
                spriteBatch.Draw(star, muzzlePos, null, ArmCore * (blink * 0.85f),
                    aimRotation, star.Size() * 0.5f, 0.07f + recoil * 0.05f, SpriteEffects.None, 0f);
            }
            if (glow != null) {
                spriteBatch.Draw(glow, muzzlePos, null, ArmMain * blink, 0f,
                    glow.Size() * 0.5f, 0.3f + recoil * 0.25f, SpriteEffects.None, 0f);
                //尾部悬浮推进器光点
                Vector2 thrusterPos = drawPos - aimRotation.ToRotationVector2() * (70f * BodyScale);
                spriteBatch.Draw(glow, thrusterPos, null, ArmEdge * (0.5f + 0.2f * blink), 0f,
                    glow.Size() * 0.5f, 0.35f, SpriteEffects.None, 0f);
            }
        }
    }

    /// <summary>
    /// 协战光弹：炮臂射出的高速光弹，彗尾光锥 + 残影拖尾，带微量追踪
    /// </summary>
    internal sealed class SHPCArmBoltProj : ModProjectile, IAdditiveDrawable
    {
        public override string Texture => CWRConstant.Placeholder;

        private static readonly Color BoltMain = new(255, 170, 90);
        private static readonly Color BoltEdge = new(200, 80, 25);

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 10;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 10;
            Projectile.height = 10;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 90;
            Projectile.extraUpdates = 1;
            Projectile.DamageType = DamageClass.Magic;
        }

        public override void AI() {
            //微量追踪：让镖弹更可靠地参与协战
            NPC target = Projectile.Center.FindClosestNPC(360f, false, true);
            if (target != null) {
                Vector2 desired = (target.Center - Projectile.Center).SafeNormalize(Vector2.Zero) * 17f;
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, desired, 0.035f);
            }
            Projectile.rotation = Projectile.velocity.ToRotation();
            Lighting.AddLight(Projectile.Center, BoltMain.ToVector3() * 0.4f);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (Main.netMode == NetmodeID.Server) return;
            SoundEngine.PlaySound(SoundID.NPCHit4 with { Volume = 0.25f, Pitch = 0.5f }, target.Center);
            for (int i = 0; i < 4; i++) {
                PRTLoader.NewParticle<PRT_Spark>(target.Center,
                    Main.rand.NextVector2CircularEdge(3f, 3f),
                    BoltMain, Main.rand.NextFloat(0.4f, 0.8f)).Configure(true, Main.rand.Next(8, 14));
            }
        }

        public override bool PreDraw(ref Color lightColor) => false;

        void IAdditiveDrawable.DrawAdditiveAfterNon(SpriteBatch spriteBatch) {
            Texture2D shot = CWRAsset.LightShot?.Value;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            Texture2D star = CWRAsset.StarTexture_White?.Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            //残影链：沿历史位置布置渐隐光点，形成连续能量尾
            if (glow != null) {
                for (int i = 1; i < Projectile.oldPos.Length; i++) {
                    if (Projectile.oldPos[i] == Vector2.Zero) break;
                    float fade = 1f - i / (float)Projectile.oldPos.Length;
                    Vector2 trailPos = Projectile.oldPos[i] + Projectile.Size * 0.5f - Main.screenPosition;
                    spriteBatch.Draw(glow, trailPos, null,
                        Color.Lerp(BoltEdge, BoltMain, fade) * (fade * 0.45f), 0f,
                        glow.Size() * 0.5f, 0.22f * fade + 0.05f, SpriteEffects.None, 0f);
                }
            }
            //彗尾光锥：箭头端锚定在弹头，尾迹向后发散
            if (shot != null) {
                Vector2 tipOrigin = new(shot.Width, shot.Height * 0.5f);
                spriteBatch.Draw(shot, drawPos, null, BoltMain * 0.9f,
                    Projectile.rotation, tipOrigin, new Vector2(0.42f, 0.13f), SpriteEffects.None, 0f);
                spriteBatch.Draw(shot, drawPos, null, Color.White * 0.75f,
                    Projectile.rotation, tipOrigin, new Vector2(0.26f, 0.07f), SpriteEffects.None, 0f);
            }
            //弹头：光晕 + 十字星芒高光
            if (glow != null) {
                spriteBatch.Draw(glow, drawPos, null, BoltMain * 0.85f, 0f,
                    glow.Size() * 0.5f, 0.32f, SpriteEffects.None, 0f);
            }
            if (star != null) {
                float twinkle = 0.8f + 0.2f * MathF.Sin((float)Main.timeForVisualEffects * 0.35f + Projectile.whoAmI);
                spriteBatch.Draw(star, drawPos, null, Color.White * (0.85f * twinkle),
                    Projectile.rotation, star.Size() * 0.5f, 0.05f, SpriteEffects.None, 0f);
            }
        }
    }
}
