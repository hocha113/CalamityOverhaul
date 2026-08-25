using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDomains;
using System;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDreams
{
    /// <summary>
    /// 鬼梦里的玩家：物品与建造全数封禁（镜像鬼切肢解的锁法，SetControls 窗口
    /// 每帧压 noItems，移动保留），左键成为唯一的语言：按住即不断唤出恶犬。
    /// 冷却与在场数走实例字段，不进 static：联机里每人一份
    /// </summary>
    public class KikasaDreamPlayer : ModPlayer
    {
        /// <summary>两次唤犬的间隔（帧）</summary>
        private const int CooldownFrames = 22;

        /// <summary>
        /// 同时在场的犬数上限：随影位魇系涨（无魇基线 4 只、每枚 +2），
        /// 超编时最老的那只先散
        /// </summary>
        internal static int MaxHoundsFor(Player player)
            => KikasaServants.KikasaEffigyBoard.HoundCap(player);

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

        /// <summary>
        /// 唤出一只恶犬：玩家朝光标一侧的身旁撕开梦境裂缝，犬自缝中窜出
        /// （出生态时序与撕裂音效都在 <see cref="KikasaDreamHound"/> 里各端自播）。
        /// 仅本机受理，弹幕走原版同步
        /// </summary>
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
            if (count >= MaxHoundsFor(Player)) {
                oldest?.BeginDissolve();
            }

            int dir = Main.MouseWorld.X >= Player.Center.X ? 1 : -1;
            //身旁悬空撕缝：朝光标一侧偏出，带小错落让连唤的缝不叠死一处；
            //位置随 spawn 包同步，各端裂缝锚点一致
            Vector2 spawnAt = Player.Center + new Vector2(
                dir * (52f + Main.rand.NextFloat(-8f, 10f)),
                -18f + Main.rand.NextFloat(-12f, 8f));
            //横向为主的窜出初速，Emerge 蓄形期间由恶犬自己冻结、出穴帧释放
            Vector2 vel = new(dir * Main.rand.NextFloat(7.4f, 9f), Main.rand.NextFloat(-3.6f, -1.8f));
            int damage = KikasaDreamHound.ResolveBiteDamage(Player, applyNightmare: true);

            Projectile.NewProjectile(Player.GetSource_Misc("KikasaDreamHound"),
                spawnAt, vel, ModContent.ProjectileType<KikasaDreamHound>(),
                damage, 4f, Player.whoAmI, KikasaDreamHound.StateEmerge);
        }
    }
}
