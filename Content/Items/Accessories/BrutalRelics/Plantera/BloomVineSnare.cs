using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalPlantera.Rendering;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Accessories.BrutalRelics.Plantera
{
    /// <summary>
    /// 荆棘藤网反缠：受击反制弹幕。根在玩家、梢头飞向攻击者，
    /// 藤体走 PlanteraVine 着色器的生长前沿逐节啃出(非贴图拉伸)；命中即缠(挂减益)钉在目标身上。
    /// ai[0]=目标NPC索引+1(0=无锁定) ai[1]=出手时生长值 ai[2]=相位(0飞行1缠住2回收)。
    /// 相位只由 owner 端裁决并 netUpdate，远端按同步 ai 渲染，目标失效时本地只淡出不 Kill
    /// </summary>
    internal class BloomVineSnare : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder2;

        private const int MaxFlightTime = 26;
        private const float MaxRange = 980f;
        /// <summary>缠住持续(帧)，减益比这久，藤体只是演出前段</summary>
        private const int LatchTime = 150;
        private const int RetractTime = 12;
        /// <summary>缠住后玩家拉开这个距离藤断</summary>
        private const float BreakRange = 1600f;

        private float Growth => Projectile.ai[1];
        private int TargetIndex => (int)Projectile.ai[0] - 1;
        private int Phase { get => (int)Projectile.ai[2]; set => Projectile.ai[2] = value; }

        private float age;
        private int latchTimer;
        private int retractTimer;
        private float seed;

        private Player Owner => Main.player[Projectile.owner];

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 1800;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 26;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Generic;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 600;
            Projectile.netImportant = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        /// <summary>只有飞行段有撞击伤害，缠住后的持续伤走减益</summary>
        public override bool? CanDamage() => Phase == 0 ? null : false;

        public override void AI() {
            if (seed == 0f) {
                seed = 0.13f + Projectile.identity * 0.057f % 0.74f;
            }
            if (!Owner.active || Owner.dead) {
                if (Projectile.owner == Main.myPlayer) {
                    Projectile.Kill();
                }
                return;
            }

            age++;
            //防原版超时截断，收尾由回收相位负责
            if (Projectile.timeLeft < 90) {
                Projectile.timeLeft = 90;
            }

            switch (Phase) {
                case 0:
                    UpdateFlight();
                    break;
                case 1:
                    UpdateLatched();
                    break;
                default:
                    UpdateRetract();
                    break;
            }
        }

        private void UpdateFlight() {
            //追踪锁定目标；无锁定直线甩出
            if (TargetIndex >= 0 && TargetIndex < Main.maxNPCs) {
                NPC target = Main.npc[TargetIndex];
                if (target.active && target.CanBeChasedBy()) {
                    Vector2 want = Projectile.Center.To(target.Center).SafeNormalize(Vector2.UnitX) * 30f;
                    Projectile.velocity = Vector2.Lerp(Projectile.velocity, want, 0.25f);
                }
            }
            Projectile.rotation = Projectile.velocity.ToRotation();

            //梢头飞行叶屑
            if (!VaultUtils.isServer && Main.rand.NextBool(2)) {
                Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height,
                    DustID.JungleGrass, 0f, 0f, 100, default, Main.rand.NextFloat(0.8f, 1.2f));
                dust.velocity = -Projectile.velocity * 0.08f;
                dust.noGravity = true;
            }

            //空挥止损：超时或超程回收(owner裁决)
            if (Projectile.owner == Main.myPlayer
                && (age > MaxFlightTime || Projectile.Distance(Owner.Center) > MaxRange)) {
                BeginRetract();
            }
        }

        private void UpdateLatched() {
            NPC target = TargetIndex >= 0 && TargetIndex < Main.maxNPCs ? Main.npc[TargetIndex] : null;
            bool valid = target != null && target.active && !target.friendly;
            if (!valid) {
                //远端只冻结等 owner 包，owner 直接回收
                if (Projectile.owner == Main.myPlayer) {
                    BeginRetract();
                }
                Projectile.velocity = Vector2.Zero;
                return;
            }

            Projectile.velocity = Vector2.Zero;
            Projectile.Center = target.Center;
            Projectile.rotation = (target.Center - Owner.MountedCenter).ToRotation();
            latchTimer++;

            Lighting.AddLight(Projectile.Center, PlanteraRenderHelper.GlowGreen.ToVector3() * 0.3f);
            //缠身荧光孢子滴落
            if (!VaultUtils.isServer && Main.rand.NextBool(9)) {
                PlanteraRenderHelper.SpawnAmbientMote(
                    target.Center + Main.rand.NextVector2Circular(target.width * 0.4f, target.height * 0.4f), false);
            }

            if (Projectile.owner == Main.myPlayer
                && (latchTimer >= LatchTime || Owner.Distance(Projectile.Center) > BreakRange)) {
                BeginRetract();
            }
        }

        private void UpdateRetract() {
            retractTimer++;
            Projectile.velocity *= 0.75f;
            if (Projectile.owner == Main.myPlayer && retractTimer >= RetractTime) {
                Projectile.Kill();
            }
        }

        private void BeginRetract() {
            Phase = 2;
            Projectile.netUpdate = true;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (Phase != 0) {
                return;
            }
            //缠住：锁定改成实际命中者，相位入包
            Projectile.ai[0] = target.whoAmI + 1;
            Phase = 1;
            latchTimer = 0;
            Projectile.Center = target.Center;
            Projectile.velocity = Vector2.Zero;
            Projectile.netUpdate = true;

            int duration = 240 + (int)(300 * Growth);
            target.AddBuff(ModContent.BuffType<BloomSnaredDebuff>(), duration);

            SoundEngine.PlaySound(SoundID.NPCHit1 with { Pitch = -0.5f, Volume = 0.7f, MaxInstances = 4 }, target.Center);
            SoundEngine.PlaySound(SoundID.Grass with { Pitch = -0.15f, Volume = 0.85f, MaxInstances = 4 }, target.Center);
            PlanteraRenderHelper.SpawnPetalBurst(target.Center, 7, 4f, false);
            PlanteraRenderHelper.SpawnSporePuff(target.Center, 0.8f);
        }

        public override bool PreDraw(ref Color lightColor) {
            if (!Owner.active) {
                return false;
            }

            float fade = Phase == 2 ? 1f - retractTimer / (float)RetractTime : 1f;
            if (fade <= 0.02f) {
                return false;
            }

            //生长前沿：飞行期从根啃向梢头，回收期倒吞回去
            float grow = Phase switch {
                0 => MathHelper.Clamp(age / 10f, 0.12f, 1f),
                1 => 1f,
                _ => fade,
            };

            Vector2 root = Owner.MountedCenter;
            bool latched = Phase == 1;
            VineParams vine = VineParams.Default;
            vine.RestLength = Vector2.Distance(root, Projectile.Center) + (latched ? 6f : 26f);
            vine.HalfWidth = 6.5f + 3f * Growth;
            vine.Taut = (latched ? 1f : 0.85f) * fade;
            vine.Pulse = latched ? 0.8f : 0.45f;
            //行波根→梢：疼痛灌进敌人身体
            vine.PulseDir = 1f;
            vine.Grow = grow;
            vine.Fade = fade;
            vine.Phase2 = false;
            vine.Seed = seed;

            PlanteraVineRenderer.DrawVine(Main.spriteBatch, root, Projectile.Center, vine);

            //梢头爪叶：飞行张开，缠中扣紧(粉瓣=玩家侧身份色)
            Texture2D petal = CWRAsset.Extra_98.Value;
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            float openAngle = latched ? 0.30f : 0.82f;
            Color leafColor = Color.Lerp(PlanteraRenderHelper.PetalPink, Color.White, 0.18f) * fade;

            for (int i = -1; i <= 1; i++) {
                float rot = Projectile.rotation + i * openAngle;
                Main.EntitySpriteDraw(petal, drawPos, null, leafColor, rot + MathHelper.PiOver2,
                    new Vector2(petal.Width / 2f, petal.Height * 0.9f),
                    new Vector2(0.15f, 0.30f), SpriteEffects.None, 0);
            }
            //梢心荧光：默认AlphaBlend批里A=0加色技法
            Main.EntitySpriteDraw(glow, drawPos, null,
                PlanteraRenderHelper.GlowGreen with { A = 0 } * (0.6f * fade),
                0f, glow.Size() / 2f, 0.35f, SpriteEffects.None, 0);

            return false;
        }
    }
}
