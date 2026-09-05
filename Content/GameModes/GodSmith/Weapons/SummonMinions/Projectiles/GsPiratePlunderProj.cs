using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.SummonMinions.Projectiles
{
    /// <summary>
    /// 分赃金币臼弹：海盗团在集结旗点抛洒的鎏金炮弹。抛物线翻飞，
    /// 落地或砸中敌人即附点金指，命中金币脆响
    /// </summary>
    internal class GsPiratePlunderProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.GoldCoin;

        public override string LocalizationCategory => "GodSmithSummonMinionsB";

        private ref float Life => ref Projectile.localAI[0];

        public override void SetDefaults() {
            Projectile.width = 14;
            Projectile.height = 14;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 240;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
        }

        public override void AI() {
            Life++;
            if (Life == 1f && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item1 with { Volume = 0.35f, Pitch = 0.5f },
                    Projectile.Center);
            }
            //抛物线：40 帧后开始下坠，横速微阻
            if (Life > 12f) {
                Projectile.velocity.Y += 0.32f;
                if (Projectile.velocity.Y > 14f) {
                    Projectile.velocity.Y = 14f;
                }
            }
            Projectile.velocity.X *= 0.995f;
            Projectile.rotation += 0.2f * Math.Sign(Projectile.velocity.X == 0f
                ? 1f : Projectile.velocity.X);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //点金指：被砸中的敌人掉更多钱（海盗的职业素养）
            target.AddBuff(BuffID.Midas, 240);
            Pop();
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            Pop();
            return true;
        }

        /// <summary>金币脆响（命中与落地共用的收尾反馈）</summary>
        private void Pop() {
            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Coins with { Volume = 0.5f }, Projectile.Center);
        }
    }
}
