using CalamityOverhaul.Content.GameModes.UI;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.VolleyBows.Projectiles
{
    /// <summary>
    /// 标桩发射器「钉标」桩：钉在非 Boss 敌身上 0.4 秒的定身桩。
    /// ai[0] = 目标 NPC whoAmI。定身走真弹幕：AI 在所有端（含服务器）压制目标速度，
    /// 服务器压了才是权威定身，跨端一致，无需自定义包。
    /// 非 Boss 判定在生成端做，这里只留兜底
    /// </summary>
    internal class GsStakePinProj : ModProjectile
    {
        public override string Texture => $"Terraria/Images/Projectile_{ProjectileID.Stake}";

        private ref float TargetIndex => ref Projectile.ai[0];

        public override void SetDefaults() {
            Projectile.width = 8;
            Projectile.height = 8;
            Projectile.friendly = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 24;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
        }

        public override void AI() {
            int idx = (int)TargetIndex;
            NPC npc = idx >= 0 && idx < Main.maxNPCs ? Main.npc[idx] : null;
            if (npc == null || !npc.active || npc.boss) {
                Projectile.Kill();
                return;
            }
            //各端一致压速：服务器同样执行，定身权威且同步
            npc.velocity *= 0.05f;
            //钉点用 identity 定相，各端同一位置
            float seed = Projectile.identity * 0.777f;
            Vector2 offset = new(MathF.Sin(seed) * 7f, MathF.Cos(seed * 1.3f) * 6f);
            Projectile.Center = npc.Center + offset;
            Projectile.rotation = MathHelper.PiOver4 + MathF.Sin(seed) * 0.3f;
            Lighting.AddLight(Projectile.Center, GameModeTheme.GodSmithAccent.ToVector3() * 0.12f);
        }

        public override bool PreDraw(ref Color lightColor) {
            Main.instance.LoadProjectile(Projectile.type);
            var tex = Terraria.GameContent.TextureAssets.Projectile[Projectile.type].Value;
            Vector2 pos = Projectile.Center - Main.screenPosition;
            float fade = MathHelper.Clamp(Projectile.timeLeft / 6f, 0f, 1f);
            //金色钉标微光垫底 + 桩体
            Color glow = GameModeTheme.GodSmithEmber with { A = 0 };
            Main.EntitySpriteDraw(tex, pos, null, glow * (0.4f * fade), Projectile.rotation,
                tex.Size() * 0.5f, 1.2f, Microsoft.Xna.Framework.Graphics.SpriteEffects.None, 0);
            Main.EntitySpriteDraw(tex, pos, null, lightColor * fade, Projectile.rotation,
                tex.Size() * 0.5f, 1f, Microsoft.Xna.Framework.Graphics.SpriteEffects.None, 0);
            return false;
        }
    }
}
