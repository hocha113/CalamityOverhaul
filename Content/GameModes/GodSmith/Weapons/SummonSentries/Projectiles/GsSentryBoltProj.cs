using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.SummonSentries.Projectiles
{
    /// <summary>
    /// 哨兵族通用飞行弹体，五样式共用一类。<br/>
    /// ai[0]=样式（0 棱光射线 / 1 月门伴束 / 2 迫击火雨 / 3 上抛破片 / 4 极寒吐息）
    /// ai[1]=样式参数（预留）。<br/>
    /// 穿透/重力/寿命按样式在首帧配置（本类自有弹幕，各端由 ai[0] 推得一致）
    /// </summary>
    internal class GsSentryBoltProj : ModProjectile
    {
        internal const int StylePrismRay = 0;
        internal const int StyleLunarLance = 1;
        internal const int StyleMortar = 2;
        internal const int StyleShard = 3;
        internal const int StyleFrostBreath = 4;

        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.RainbowRodBullet;

        public override string LocalizationCategory => "GodSmithSummonSentries";

        private ref float Style => ref Projectile.ai[0];
        private ref float Age => ref Projectile.localAI[0];

        public override void SetDefaults() {
            Projectile.width = 14;
            Projectile.height = 14;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.penetrate = 1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 240;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        private void ConfigureByStyle() {
            switch ((int)Style) {
                case StylePrismRay:
                    Projectile.penetrate = 2;
                    Projectile.timeLeft = 40;
                    break;
                case StyleLunarLance:
                    Projectile.penetrate = 5;
                    Projectile.timeLeft = 50;
                    break;
                case StyleMortar:
                    Projectile.tileCollide = true;
                    Projectile.timeLeft = 240;
                    break;
                case StyleShard:
                    Projectile.tileCollide = true;
                    Projectile.timeLeft = 120;
                    break;
                case StyleFrostBreath:
                    Projectile.penetrate = 3;
                    Projectile.timeLeft = 26;
                    Projectile.Resize(26, 26);
                    break;
            }
        }

        public override void AI() {
            Age++;
            if (Age == 1f) {
                ConfigureByStyle();
            }
            int style = (int)Style;
            //样式运动学
            switch (style) {
                case StyleMortar:
                    Projectile.velocity.Y += 0.28f;
                    break;
                case StyleShard:
                    Projectile.velocity.Y += 0.32f;
                    Projectile.velocity.X *= 0.995f;
                    break;
                case StyleFrostBreath:
                    Projectile.velocity *= 0.975f;
                    break;
            }
            Projectile.rotation = Projectile.velocity.ToRotation();
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if ((int)Style == StyleFrostBreath) {
                //原版减益骑原版同步，跨端一致
                target.AddBuff(BuffID.Frostburn, 120);
            }
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer || (int)Style != StyleMortar) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.35f, Pitch = 0.3f, MaxInstances = 3 }, Projectile.Center);
        }
    }
}
