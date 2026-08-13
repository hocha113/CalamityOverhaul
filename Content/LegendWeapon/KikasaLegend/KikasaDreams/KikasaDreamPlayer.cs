using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDomains;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDreams
{
    /// <summary>
    /// 鬼梦里的玩家：物品与建造全数封禁（镜像鬼切肢解的锁法——SetControls 窗口
    /// 每帧压 noItems，移动保留），左键成为唯一的语言：按住即不断唤出恶犬。
    /// 冷却与在场数走实例字段，不进 static——联机里每人一份
    /// </summary>
    public class KikasaDreamPlayer : ModPlayer
    {
        /// <summary>两次唤犬的间隔（帧）</summary>
        private const int CooldownFrames = 22;

        /// <summary>同时在场的犬数上限，超编时最老的那只先散</summary>
        private const int MaxHounds = 6;

        private int houndCooldown;

        /// <summary>HUD 冷却弧 0~1（1=刚唤出）</summary>
        public float HoundCooldown01 => Math.Clamp(houndCooldown / (float)CooldownFrames, 0f, 1f);

        /// <summary>本人是否身处鬼梦稳态</summary>
        public bool InDreamSteady
            => Player.GetModPlayer<KikasaDomainPlayer>().Phase == KikasaDomainPhase.Dreaming;

        /// <summary>梦里收走双手：物品与建造封禁；拉入/归返的过场里同样按住</summary>
        public override void SetControls() {
            if (!Player.GetModPlayer<KikasaDomainPlayer>().InDreamPhase) {
                return;
            }
            Player.noItems = true;
            Player.noBuilding = true;
        }

        public override void PostUpdate() {
            if (Main.dedServ || Player.whoAmI != Main.myPlayer || Player.dead) {
                return;
            }
            if (houndCooldown > 0) {
                houndCooldown--;
            }
            if (!InDreamSteady) {
                return;
            }
            //左键按住连唤；悬停 UI 让位界面点击
            if (Player.controlUseItem && !Player.mouseInterface && houndCooldown <= 0) {
                SummonHound();
                houndCooldown = CooldownFrames;
            }
        }

        /// <summary>唤出一只恶犬：自脚下黑水朝光标方向跃出。仅本机受理，弹幕走原版同步</summary>
        private void SummonHound() {
            //超编先散最老的：timeLeft 最小者
            int count = 0;
            KikasaDreamHound oldest = null;
            int oldestTime = int.MaxValue;
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile proj = Main.projectile[i];
                if (proj?.active != true || proj.owner != Player.whoAmI
                    || proj.ModProjectile is not KikasaDreamHound hound) {
                    continue;
                }
                count++;
                if (proj.timeLeft < oldestTime) {
                    oldestTime = proj.timeLeft;
                    oldest = hound;
                }
            }
            if (count >= MaxHounds) {
                oldest?.BeginDissolve();
            }

            int dir = Main.MouseWorld.X >= Player.Center.X ? 1 : -1;
            Vector2 spawnAt = Player.Bottom + new Vector2(0f, -12f);
            Vector2 vel = new(dir * Main.rand.NextFloat(6.2f, 8.2f), Main.rand.NextFloat(-7.6f, -5.8f));
            int damage = (int)Player.GetTotalDamage(DamageClass.Summon)
                .ApplyTo(KikasaDreamHound.BiteDamage);

            Projectile.NewProjectile(Player.GetSource_Misc("KikasaDreamHound"),
                spawnAt, vel, ModContent.ProjectileType<KikasaDreamHound>(),
                damage, 4f, Player.whoAmI);

            SoundEngine.PlaySound(SoundID.SplashWeak with { Pitch = -0.5f, Volume = 0.45f, MaxInstances = 3 }, spawnAt);
        }
    }
}
