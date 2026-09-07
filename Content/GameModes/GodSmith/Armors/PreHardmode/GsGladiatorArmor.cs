using CalamityOverhaul.Content.GameModes.GodSmith.Armors.Hardmode;
using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Armors.PreHardmode
{
    /// <summary>
    /// 【角斗士套·凯旋标枪】（P10a 移交，键族归 ArmorsB）竞技场的喝彩化为投械：
    /// ①命中积攒喝彩，满六层后下一击自肩上依次掷出三支青铜标枪
    /// ②标枪走抛物弧线钉向目标。
    /// 原版套装奖励（免疫击退）保留，神赋叠加
    /// </summary>
    internal class GsGladiatorArmor : GsArmorsBChargeScheme
    {
        public override int[] HeadIDs => [ItemID.GladiatorHelmet];

        public override int BodyID => ItemID.GladiatorBreastplate;

        public override int LegsID => ItemID.GladiatorLeggings;

        protected override string EndowLineFallback =>
            "Triumph Volley: strikes build acclaim; at 6 stacks the next strike hurls three bronze javelins over your shoulder in arcing volleys at the foe";

        //青铜色板（基类抽象色板签名仍需实现）
        internal static readonly Color BronzeBright = new(255, 232, 172);
        internal static readonly Color BronzeGold = new(222, 172, 92);

        protected override int FullCharge => 6;

        protected override Color ThemeMain => BronzeGold;

        protected override Color ThemeBright => BronzeBright;

        protected override bool IsOwnProc(Projectile proj)
            => proj.type == ModContent.ProjectileType<GsGladiatorJavelinProj>();

        protected override void ReleaseEndow(Player player, GodSmithArmorPlayer state, NPC target,
            in NPC.HitInfo hit, int damageDone) {
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item1 with { Volume = 0.7f, Pitch = -0.25f }, player.Center);
            }
            if (player.whoAmI != Main.myPlayer) {
                return;
            }
            int javelinDamage = Math.Clamp((int)(damageDone * 0.35f), 7, 75);
            Vector2 shoulder = player.Center + new Vector2(-player.direction * 8f, -18f);
            for (int i = 0; i < 3; i++) {
                //抛物弧：抬高出手角，重力自然落向目标；三支错拍不同弧
                Vector2 flat = target.Center - shoulder;
                float dist = MathHelper.Clamp(flat.Length(), 60f, 700f);
                Vector2 dir = flat.SafeNormalize(Vector2.UnitX);
                float speed = 10f + dist * 0.008f + i * 0.8f;
                //上抬量随距离/序号变化，三弧分层
                Vector2 vel = dir * speed - Vector2.UnitY * (3.2f + i * 1.1f);
                Projectile.NewProjectile(player.GetSource_Misc("GodSmithGladiatorEndow"),
                    shoulder, vel, ModContent.ProjectileType<GsGladiatorJavelinProj>(),
                    javelinDamage, 3f, player.whoAmI, 0f, 0f, i * 6f);
            }
        }
    }

    /// <summary>
    /// 凯旋标枪：肩上掷出的标枪，错拍出手、抛物坠向目标；借原版骨标枪贴图默认绘制
    /// </summary>
    internal class GsGladiatorJavelinProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.BoneJavelin;

        private ref float Life => ref Projectile.ai[0];

        /// <summary>错拍延迟帧（随生成参数过线）</summary>
        private ref float HoldFrames => ref Projectile.ai[2];

        public override void SetDefaults() {
            Projectile.width = 14;
            Projectile.height = 14;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Generic;
            Projectile.penetrate = 2;
            Projectile.timeLeft = 90;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 20;
        }

        /// <summary>错拍持枪期不判定</summary>
        public override bool? CanDamage() => Life >= HoldFrames;

        public override void AI() {
            Life++;
            //错拍：悬持蓄势（竖向贴图，枪尖对齐速度方向）
            if (Life < HoldFrames) {
                Projectile.position -= Projectile.velocity;
                Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
                return;
            }
            if ((int)Life == (int)HoldFrames && !Main.dedServ) {
                SoundEngine.PlaySound(SoundID.Item1 with { Volume = 0.4f, Pitch = 0.4f, MaxInstances = 4 }, Projectile.Center);
            }
            //抛物坠落
            Projectile.velocity.Y += 0.24f;
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (Main.dedServ) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item37 with { Volume = 0.45f, Pitch = 0.35f, MaxInstances = 4 }, target.Center);
        }
    }
}
