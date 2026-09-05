using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicConduit;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicChant
{
    /// <summary>
    /// 荨麻刺重铸：生根荨麻。材质身份：丛林荨麻藤（带倒刺的翠绿藤蔓）。<br/>
    /// ①「生根」：正拍藤刺命中点生根滞留荨麻丛（多跳低伤驻场，场上至多三丛，超限枯最旧）；<br/>
    /// ②满层强化「藤龙横扫」：一条大藤龙蜿蜒横扫沿途撕咬；<br/>
    /// ③命中挂中毒加长；④施法有前刺推压体感（与晶邪碎片的推压参数不同：过冲更深、回挑收势）
    /// </summary>
    internal class GsNettleBurst : GsChantScheme
    {
        public override int TargetItemID => ItemID.NettleBurst;

        protected override string GsDescFallback =>
            "Reforged: on-beat vines take root where they bite, leaving stinging nettle thickets (up to three)\nAt full resonance the next cast unleashes a great vine dragon that sweeps through the field";
        protected override float BaseDamageMult => 1.08f;

        /// <summary>owner 端在场荨麻丛上限</summary>
        private const int MaxThickets = 3;

        /// <summary>本弹是否荨麻藤延展段（右延/左延/收尾）</summary>
        private static bool IsVineSegment(int projType)
            => projType is ProjectileID.NettleBurstRight or ProjectileID.NettleBurstLeft or ProjectileID.NettleBurstEnd;

        //==================== 动画法：前刺推压（深过冲变体） ====================

        public override void GsUseStyle(Item item, Player player, Rectangle heldItemFrame) {
            if (player.itemAnimationMax <= 0) {
                return;
            }
            //前刺推压：过冲曲线（progress 平方）比晶邪碎片压得更深，收势带回挑（绝对剖面 0.09·p²，差分施加防累积漂移）
            float n = player.itemAnimationMax;
            float progress = player.itemAnimation / n;
            float shove = progress * progress;
            float prev = (player.itemAnimation + 1) / n;
            player.itemLocation += new Vector2(player.direction, 0f) * (6f * shove);
            GsMagicKickMath.ApplyKickDiff(player, 0.09f * shove, 0.09f * prev * prev);
        }

        //==================== 强化咏唱：藤龙横扫 ====================

        protected override bool? ChantEmpowerShoot(Item item, Player player, GsChantPlayer chant,
            EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity,
            int type, int damage, float knockback) {
            SoundEngine.PlaySound(SoundID.Item17 with { Volume = 0.9f, Pitch = -0.35f }, position);
            int dragonDamage = Math.Max(1, (int)(damage * 1.5f));
            Projectile.NewProjectile(source, position, velocity.SafeNormalize(Vector2.UnitX) * 8.5f,
                ModContent.ProjectileType<GsNettleBurstDragonProj>(), dragonDamage, knockback, player.whoAmI);
            return false;
        }

        //==================== 命中：生根荨麻 ====================

        public override void GsProjOnHitNPC(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone, GodSmithProjRouter router) {
            if (!IsVineSegment(proj.type)) {
                return;
            }
            if (!proj.IsOwnedByLocalPlayer()) {
                return;
            }
            bool onBeat = router.MarkData is FormOnBeat or FormEmpower;
            //中毒加长：正拍咬得更毒
            target.AddBuff(BuffID.Poisoned, onBeat ? 240 : 120);
            if (!onBeat) {
                return;
            }
            //生根：命中点滞留荨麻丛（12t 节流防一串藤段瞬间铺满）
            GsChantPlayer chant = Chant(Main.player[proj.owner]);
            uint now = Main.GameUpdateCount;
            if (chant.TimerB != 0 && now - chant.TimerB < 12) {
                return;
            }
            chant.TimerB = now;
            RootThicket(proj, target.Center);
        }

        /// <summary>生根荨麻丛：owner 端在场上限三丛，超限枯最旧</summary>
        private static void RootThicket(Projectile proj, Vector2 pos) {
            int thicketType = ModContent.ProjectileType<GsNettleBurstThicketProj>();
            int count = 0, oldestIdx = -1, oldestLeft = int.MaxValue;
            foreach (Projectile other in Main.ActiveProjectiles) {
                if (other.type != thicketType || other.owner != proj.owner) {
                    continue;
                }
                count++;
                if (other.timeLeft < oldestLeft) {
                    oldestLeft = other.timeLeft;
                    oldestIdx = other.whoAmI;
                }
            }
            if (count >= MaxThickets && oldestIdx >= 0) {
                Main.projectile[oldestIdx].Kill();
            }
            int thicketDamage = Math.Max(1, (int)(proj.damage * 0.2f));
            Projectile.NewProjectile(proj.GetSource_FromThis(), pos, Vector2.Zero,
                thicketType, thicketDamage, 0f, proj.owner);
        }
    }

    /// <summary>
    /// 荨麻丛：生根滞留的带刺灌丛，多跳低伤（判定圆与可见尺寸同源）
    /// </summary>
    internal class GsNettleBurstThicketProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.NettleBurstEnd;

        public override string LocalizationCategory => "GodSmithMagicChant";

        private const int LifeTicks = 180;
        private const float Radius = 44f;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 56;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 20;
            Projectile.timeLeft = LifeTicks;
        }

        /// <summary>破土-繁茂-枯萎的半径生命周期</summary>
        private float RadiusNow {
            get {
                float grow = MathHelper.Clamp((LifeTicks - Projectile.timeLeft) / 12f, 0f, 1f);
                float fade = MathHelper.Clamp(Projectile.timeLeft / 26f, 0f, 1f);
                return Radius * VaultUtils.EaseOutQuad(grow) * fade;
            }
        }

        public override void AI() {
            Projectile.velocity = Vector2.Zero;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
            => target.AddBuff(BuffID.Poisoned, 120);

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            float r = RadiusNow;
            if (r < 6f) {
                return false;
            }
            float nx = MathHelper.Clamp(Projectile.Center.X, targetHitbox.Left, targetHitbox.Right);
            float ny = MathHelper.Clamp(Projectile.Center.Y, targetHitbox.Top, targetHitbox.Bottom);
            return new Vector2(nx - Projectile.Center.X, ny - Projectile.Center.Y).LengthSquared() <= r * r;
        }

        public override bool PreDraw(ref Color lightColor) {
            //原版藤尖贴图一笔按判定半径缩放（尺寸提示与判定同源）
            float r = RadiusNow;
            if (r < 4f) {
                return false;
            }
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            float scale = r * 2f / Math.Max(tex.Width, tex.Height);
            Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, null, lightColor,
                0f, tex.Size() / 2f, scale, SpriteEffects.None, 0);
            return false;
        }
    }

    /// <summary>
    /// 藤龙：强化咏唱放出的巨型荨麻藤龙。owner 端每 30t 锁定近敌写 ai[1] 过线，
    /// 各端向同一目标缓转横扫；本体沿用原版藤段贴图默认绘制
    /// </summary>
    internal class GsNettleBurstDragonProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.NettleBurstRight;

        public override string LocalizationCategory => "GodSmithMagicChant";

        private ref float SteerTimer => ref Projectile.localAI[0];
        private int TargetWho => (int)Projectile.ai[1] - 1;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 34;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 20;
            Projectile.timeLeft = 200;
        }

        public override void AI() {
            //蜿蜒横扫：基速上叠正弦摆身（identity 定相，各端确定性）；原版藤段贴图竖向朝上
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
            Projectile.velocity = Projectile.velocity.RotatedBy(
                MathF.Sin(Projectile.timeLeft * 0.18f + Projectile.identity * 1.1f) * 0.05f);

            //owner 端周期性锁定近敌，目标经 ai[1] 过线，各端向同一目标缓转
            if (Projectile.IsOwnedByLocalPlayer() && ++SteerTimer >= 30f) {
                SteerTimer = 0f;
                NPC next = Projectile.Center.FindClosestNPC(540f);
                int encoded = next != null ? next.whoAmI + 1 : 0;
                if ((int)Projectile.ai[1] != encoded) {
                    Projectile.ai[1] = encoded;
                    Projectile.netUpdate = true;
                }
            }
            if (TargetWho >= 0 && TargetWho < Main.maxNPCs) {
                NPC chase = Main.npc[TargetWho];
                if (chase.active && chase.CanBeChasedBy()) {
                    float current = Projectile.velocity.ToRotation();
                    float wanted = (chase.Center - Projectile.Center).ToRotation();
                    Projectile.velocity = Utils.AngleTowards(current, wanted, MathHelper.ToRadians(2.2f))
                        .ToRotationVector2() * Projectile.velocity.Length();
                }
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.Poisoned, 360);
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.NPCHit32 with { Volume = 0.5f, Pitch = 0.2f, MaxInstances = 3 }, target.Center);
            }
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Grass with { Volume = 0.8f, Pitch = -0.3f }, Projectile.Center);
        }
    }
}
