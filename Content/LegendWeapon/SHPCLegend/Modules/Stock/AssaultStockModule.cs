using CalamityOverhaul.Common;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
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
    /// 悬浮炮臂：悬停在玩家肩侧的机械炮荚，炮口始终跟随光标。
    /// 侦测到玩家击发主武器的瞬间，左右臂交替射出协战镖弹
    /// </summary>
    internal sealed class SHPCAssaultArmProj : ModProjectile, IAdditiveDrawable
    {
        public override string Texture => CWRConstant.Placeholder;

        private static readonly Color ArmMain = new(255, 160, 80);
        private static readonly Color ArmEdge = new(180, 70, 20);
        private static readonly Color ArmCore = new(255, 235, 200);

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
                || owner.HeldItem == null || owner.HeldItem.type != SHPCOverride.ID) {
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

        public override bool PreDraw(ref Color lightColor) => false;

        void IAdditiveDrawable.DrawAdditiveAfterNon(SpriteBatch spriteBatch) {
            Texture2D white = CWRAsset.Placeholder_White?.Value;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (white == null) return;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Vector2 recoilOffset = aimRotation.ToRotationVector2() * -recoil * 5f;
            drawPos += recoilOffset;

            //荚体辉光
            if (glow != null) {
                spriteBatch.Draw(glow, drawPos, null, ArmEdge * 0.55f, 0f,
                    glow.Size() * 0.5f, 0.62f, SpriteEffects.None, 0f);
            }
            //炮荚本体：旋转 45° 的菱形装甲块
            spriteBatch.Draw(white, drawPos, null, ArmEdge * 0.95f,
                aimRotation + MathHelper.PiOver4, new Vector2(0.5f, 0.5f), new Vector2(15f, 15f), SpriteEffects.None, 0f);
            spriteBatch.Draw(white, drawPos, null, ArmMain,
                aimRotation + MathHelper.PiOver4, new Vector2(0.5f, 0.5f), new Vector2(10f, 10f), SpriteEffects.None, 0f);
            //炮管：指向光标的亮线
            Vector2 barrelDir = aimRotation.ToRotationVector2();
            spriteBatch.Draw(white, drawPos + barrelDir * 10f, null, ArmCore,
                aimRotation, new Vector2(0f, 0.5f), new Vector2(13f, 3f), SpriteEffects.None, 0f);
            //核心指示灯：随充能闪烁
            float blink = 0.7f + 0.3f * MathF.Sin((float)Main.timeForVisualEffects * 0.2f + Side * 2.6f);
            spriteBatch.Draw(white, drawPos, null, ArmCore * blink,
                0f, new Vector2(0.5f, 0.5f), new Vector2(4f, 4f), SpriteEffects.None, 0f);
        }
    }

    /// <summary>
    /// 协战镖弹：炮臂射出的高速能量镖，带短拖尾与微量追踪
    /// </summary>
    internal sealed class SHPCArmBoltProj : ModProjectile, IAdditiveDrawable
    {
        public override string Texture => CWRConstant.Placeholder;

        private static readonly Color BoltMain = new(255, 170, 90);
        private static readonly Color BoltEdge = new(200, 80, 25);

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
            Lighting.AddLight(Projectile.Center, BoltMain.ToVector3() * 0.25f);
            if (Main.netMode != NetmodeID.Server && Main.rand.NextBool(3)) {
                PRTLoader.NewParticle<PRT_CyberSquare>(Projectile.Center, -Projectile.velocity * 0.1f,
                    BoltMain, Main.rand.NextFloat(0.25f, 0.5f)).Configure(BoltEdge, Main.rand.Next(6, 12));
            }
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
            Texture2D white = CWRAsset.Placeholder_White?.Value;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            if (glow != null) {
                spriteBatch.Draw(glow, drawPos, null, BoltEdge * 0.5f, 0f,
                    glow.Size() * 0.5f, 0.35f, SpriteEffects.None, 0f);
            }
            if (white != null) {
                //拉长的镖体
                spriteBatch.Draw(white, drawPos, null, BoltMain,
                    Projectile.rotation, new Vector2(0.5f, 0.5f), new Vector2(14f, 3f), SpriteEffects.None, 0f);
                spriteBatch.Draw(white, drawPos, null, Color.White * 0.9f,
                    Projectile.rotation, new Vector2(0.5f, 0.5f), new Vector2(8f, 1.6f), SpriteEffects.None, 0f);
            }
        }
    }
}
