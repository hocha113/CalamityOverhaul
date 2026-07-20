using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.Wraiths.Runtime
{
    /// <summary>
    /// 攻击吞没回执（鬼律第一条的防回归层）。<br/>
    /// 事实基础：<c>WraithActor</c> 走 InnoVault Actor，不在 NPC/弹幕伤害管线内，武器命中本就
    /// 零数值交互（弹幕直接穿过，无任何钩子被调用）——本层只补上"被吞没"的主题化演出，
    /// 让玩家读到"打不动"而不是"没打着"。<b>永远不得</b>在此写入伤害、击退、弹幕消耗等任何数值。<br/>
    /// 纯客户端视觉：各端本地检测本地可见的弹幕/挥击，无网络交互
    /// </summary>
    internal static class WraithSwallow
    {
        /// <summary>两次回执的最小间隔（帧），防粒子刷屏</summary>
        private const int ReceiptCooldown = 7;
        /// <summary>低于此显形强度不回执：虚影期武器穿过连"吞"都不值得演</summary>
        private const float MinStrength = 0.35f;

        /// <summary>每帧驱动（客户端，<c>WraithActor.AI</c> 调用）</summary>
        public static void Update(WraithActor wraith) {
            if (wraith.SwallowCooldown > 0) {
                wraith.SwallowCooldown--;
                return;
            }
            if (wraith.PresenceStrength < MinStrength || !wraith.InScreen) {
                return;
            }

            Rectangle hitbox = wraith.HitBox;

            //弹幕:任何玩家的伤害性友方弹幕(含召唤物、持握弹幕近战)
            foreach (Projectile projectile in Main.ActiveProjectiles) {
                if (!projectile.friendly || projectile.hostile || projectile.damage <= 0
                    || Main.projHook[projectile.type] || projectile.aiStyle == ProjAIStyleID.Bobber) {
                    continue;
                }
                if (hitbox.Intersects(projectile.Hitbox)) {
                    PlayReceipt(wraith, Intersection(hitbox, projectile.Hitbox));
                    return;
                }
            }

            //真近战:无弹幕的原版式挥击,按持物位置近似一个挥击盒(仅演出,无需精确)
            foreach (Player player in Main.ActivePlayers) {
                if (player.dead || player.itemAnimation <= 0) {
                    continue;
                }
                Item item = player.HeldItem;
                if (item == null || item.IsAir || item.damage <= 0 || item.noMelee
                    || item.useStyle != ItemUseStyleID.Swing) {
                    continue;
                }
                float reach = MathHelper.Clamp(MathHelper.Max(item.width, item.height) * player.GetAdjustedItemScale(item), 28f, 120f);
                Vector2 swingCenter = player.MountedCenter + new Vector2(player.direction * reach * 0.6f, -reach * 0.1f);
                Rectangle swing = new((int)(swingCenter.X - reach * 0.7f), (int)(swingCenter.Y - reach * 0.7f)
                    , (int)(reach * 1.4f), (int)(reach * 1.4f));
                if (hitbox.Intersects(swing)) {
                    PlayReceipt(wraith, Intersection(hitbox, swing));
                    return;
                }
            }
        }

        private static Vector2 Intersection(Rectangle a, Rectangle b) {
            Rectangle overlap = Rectangle.Intersect(a, b);
            return overlap.Width > 0 ? overlap.Center.ToVector2() : a.Center.ToVector2();
        }

        /// <summary>"被吞没"回执：雾体在命中处翻涌一口把劲道吃掉 + 一声闷响</summary>
        private static void PlayReceipt(WraithActor wraith, Vector2 contact) {
            wraith.SwallowCooldown = ReceiptCooldown;

            Color body = wraith.Definition?.BaseColor ?? new Color(150, 160, 185);
            Color eye = wraith.Definition?.EyeColor ?? new Color(120, 220, 200);
            for (int i = 0; i < 5; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(1.6f, 1.4f) - Vector2.UnitY * 0.4f;
                PRTLoader.NewParticle<PRT_Smoke>(contact + Main.rand.NextVector2Circular(8f, 10f), vel
                    , body * 0.55f, Main.rand.NextFloat(0.16f, 0.26f))
                    ?.Configure(Main.rand.Next(22, 34), Main.rand.NextFloat(0.30f, 0.44f), Main.rand.NextFloat(-0.02f, 0.02f));
            }
            //一点鬼火色芯,标记"它接住了"而非普通烟尘
            PRTLoader.NewParticle<PRT_Smoke>(contact, -Vector2.UnitY * 0.3f, eye * 0.5f, 0.12f)
                ?.Configure(18, 0.5f);

            SoundEngine.PlaySound(SoundID.NPCHit54 with { Volume = 0.28f, Pitch = -0.55f, MaxInstances = 3 }, contact);
        }
    }
}
