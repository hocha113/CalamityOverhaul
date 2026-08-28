using CalamityOverhaul.Content.GameModes.GodSmith.Core;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Prefixes.Magic
{
    /// <summary>
    /// 【魔力系·签名】星辉咏灵：只覆盖神话词缀的顶级签名神赋，与魔力回流同池并立。
    /// 施法唤来一枚金紫星灵绕身随行，定期朝附近敌人掷出星辉飞弹；
    /// 星灵在持有者收起这件武器后自行散去
    /// </summary>
    internal class GodSmithMythicEchoEndow : GodSmithEndow
    {
        /// <summary>星弹伤害占武器伤害比</summary>
        internal const float BaseDamageRatio = 0.65f;

        /// <summary>星灵开火间隔（帧）</summary>
        internal const int FireInterval = 150;

        //签名彩蛋：池内偏稀有
        public override float RollWeight => 0.6f;

        public override int[] CoveredPrefixes => [PrefixID.Mythical];

        protected override string EndowNameFallback => "Mythic Starcall";

        protected override string EndowDescFallback =>
            "Casting calls a star spirit to your side; it hurls starbolts at nearby foes for {0}% weapon damage";

        public override object[] DescFormatArgs(Item item)
            => [(BaseDamageRatio * 100f * TierScaleFor(item.prefix)).ToString("0.#")];

        public override void OnUseAnimation(Item item, Player player, float tierScale) {
            //星灵只在 owner 端点名生成，实体自然同步
            if (player.whoAmI != Main.myPlayer) {
                return;
            }
            if (player.ownedProjectileCounts[ModContent.ProjectileType<GodSmithMythicStarWisp>()] > 0) {
                return;
            }
            Projectile.NewProjectile(player.GetSource_Misc("GodSmithMythicEchoEndow"),
                player.Center - Vector2.UnitY * 40f, Vector2.Zero,
                ModContent.ProjectileType<GodSmithMythicStarWisp>(), 0, 0f, player.whoAmI);
        }
    }

    /// <summary>星灵：金紫四芒星绕身盘旋，脉动呼吸；持有者换下神话武器即散场。
    /// ai[0] = 轨道角，ai[1] = 开火计时。开火只在 owner 端结算，伤害按当时手持武器实算</summary>
    internal class GodSmithMythicStarWisp : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.FallingStar;

        /// <summary>索敌半径</summary>
        internal const float SeekRange = 600f;

        public override void SetDefaults() {
            Projectile.width = 20;
            Projectile.height = 20;
            Projectile.friendly = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 90;
            Projectile.tileCollide = false;
            Projectile.aiStyle = 0;
        }

        /// <summary>持有者手上是否仍是携带本神赋的武器</summary>
        private static bool StillValid(Player owner)
            => GameModeSystem.GodSmithActive && owner.active && !owner.dead
               && owner.HeldItem != null && !owner.HeldItem.IsAir
               && owner.HeldItem.TryGetGlobalItem(out GodSmithItem data)
               && data.EndowKey == nameof(GodSmithMythicEchoEndow);

        public override void AI() {
            Player owner = Main.player[Projectile.owner];
            if (StillValid(owner)) {
                Projectile.timeLeft = 90;
            }
            else if (Projectile.timeLeft > 20) {
                //收场：进入淡出倒计时
                Projectile.timeLeft = 20;
            }
            if (Projectile.timeLeft <= 20) {
                Projectile.alpha = Math.Min(255, Projectile.alpha + 13);
            }
            //绕身轨道：缓慢公转 + 纵向呼吸浮动
            Projectile.ai[0] += 0.045f;
            Vector2 anchor = owner.Center - Vector2.UnitY * 12f;
            Vector2 orbit = Projectile.ai[0].ToRotationVector2() * 46f;
            orbit.Y += (float)Math.Sin(Projectile.ai[0] * 2.3f) * 8f;
            Projectile.Center = Vector2.Lerp(Projectile.Center, anchor + orbit, 0.25f);
            Projectile.rotation += 0.08f;
            Lighting.AddLight(Projectile.Center, 0.4f, 0.3f, 0.5f);
            if (!VaultUtils.isServer && Main.rand.NextBool(5)) {
                Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.YellowStarDust,
                    Main.rand.NextVector2Circular(0.8f, 0.8f), 100, default, 0.8f);
                dust.noGravity = true;
            }
            //开火循环：只在 owner 端点名，伤害按当时手持武器与词缀档实算
            if (Projectile.owner != Main.myPlayer || Projectile.timeLeft <= 20) {
                return;
            }
            if (++Projectile.ai[1] < GodSmithMythicEchoEndow.FireInterval) {
                return;
            }
            NPC target = FindTarget();
            if (target == null) {
                return;
            }
            Projectile.ai[1] = 0f;
            Item held = owner.HeldItem;
            float tier = GodSmithEndow.TryGet(nameof(GodSmithMythicEchoEndow), out GodSmithEndow endow)
                ? endow.TierScaleFor(held.prefix) : 1f;
            int damage = Math.Max(1, (int)(owner.GetWeaponDamage(held)
                * GodSmithMythicEchoEndow.BaseDamageRatio * tier));
            Vector2 dir = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitX);
            Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, dir * 7f,
                ModContent.ProjectileType<GodSmithMythicStarShot>(), damage, 2f, Projectile.owner, target.whoAmI);
        }

        private NPC FindTarget() {
            NPC best = null;
            float bestDist = SeekRange;
            foreach (NPC npc in Main.ActiveNPCs) {
                if (!npc.CanBeChasedBy()) {
                    continue;
                }
                float dist = npc.Distance(Projectile.Center);
                if (dist < bestDist) {
                    bestDist = dist;
                    best = npc;
                }
            }
            return best;
        }

        public override Color? GetAlpha(Color lightColor) => new Color(255, 220, 160, 0) * Projectile.Opacity;

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Vector2 origin = tex.Size() * 0.5f;
            //呼吸脉动：确定性时间函数，不掷随机
            float pulse = 0.85f + 0.15f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 5f + Projectile.whoAmI);
            Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, null,
                new Color(140, 80, 200, 0) * (0.7f * Projectile.Opacity), Projectile.rotation, origin,
                pulse * 1.25f, 0);
            Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, null,
                new Color(255, 235, 180, 0) * Projectile.Opacity, Projectile.rotation, origin,
                pulse * 0.8f, 0);
            return false;
        }
    }

    /// <summary>星辉飞弹：金紫小星划着弧线咬向目标，尾迹缀满星屑</summary>
    internal class GodSmithMythicStarShot : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.FallingStar;

        public override void SetDefaults() {
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Generic;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 70;
            Projectile.tileCollide = false;
            Projectile.aiStyle = 0;
        }

        public override void AI() {
            if (Projectile.timeLeft == 69 && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item9 with { Volume = 0.5f, Pitch = 0.2f }, Projectile.Center);
            }
            NPC target = Main.npc[(int)Projectile.ai[0]];
            if (target.active && target.CanBeChasedBy()) {
                Vector2 want = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitX);
                float speed = MathHelper.Lerp(7f, 15f, 1f - Projectile.timeLeft / 70f);
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, want * speed, 0.12f);
            }
            Projectile.rotation += 0.25f;
            Lighting.AddLight(Projectile.Center, 0.35f, 0.28f, 0.45f);
            if (!VaultUtils.isServer && Main.rand.NextBool(2)) {
                Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.YellowStarDust,
                    -Projectile.velocity * 0.15f, 100, default, 0.9f);
                dust.noGravity = true;
            }
        }

        public override Color? GetAlpha(Color lightColor) => new Color(255, 225, 170, 0) * Projectile.Opacity;

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Vector2 origin = tex.Size() * 0.5f;
            Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, null,
                new Color(150, 90, 210, 0) * (0.7f * Projectile.Opacity), Projectile.rotation, origin, 0.9f, 0);
            Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, null,
                new Color(255, 240, 190, 0) * Projectile.Opacity, Projectile.rotation, origin, 0.6f, 0);
            return false;
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            for (int i = 0; i < 8; i++) {
                Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.YellowStarDust,
                    Main.rand.NextVector2Circular(3f, 3f), 100, default, 1.1f);
                dust.noGravity = true;
            }
        }
    }
}
