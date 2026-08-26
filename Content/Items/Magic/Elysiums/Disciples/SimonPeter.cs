using CalamityOverhaul.Common;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Magic.Elysiums.Disciples
{
    /// <summary>
    /// 彼得·磐石(席位0)：磐石之盾。
    /// 圣盾就绪时替主人挡下来袭伤害的一部分(结算接线在 <see cref="ElysiumPlayer.ModifyHurt"/>)，
    /// 格挡瞬间彼得抢身到主人身前，冲击圣环荡开。
    /// ai[0]=格挡演出倒计时(主人端写入同步)
    /// </summary>
    internal class SimonPeter : BaseDisciple
    {
        public override int Seat => 0;

        private ref float BlockFx => ref Projectile.ai[0];
        private const float BlockFxTime = 26f;

        //格挡瞬间的抢身冲量
        private Vector2 blockDashTarget;
        private bool dashLatched;

        public override void AI() {
            base.AI();
            if (IsMartyring) {
                return;
            }

            //主人端侦测圣盾格挡事件：写入演出倒计时并同步
            if (Projectile.IsOwnedByLocalPlayer()
                && Owner.TryGetModPlayer(out ElysiumPlayer ep)
                && ep.PeterBlockAt != 0 && Main.GameUpdateCount - ep.PeterBlockAt <= 1
                && BlockFx <= 0f) {
                BlockFx = BlockFxTime;
                Projectile.netUpdate = true;
            }

            if (BlockFx > 0f) {
                BlockFx--;
                haloFlare = 1f;

                //抢身：荡到主人面前一挡
                if (!dashLatched) {
                    dashLatched = true;
                    blockDashTarget = Owner.Center + new Vector2(Owner.direction * 52f, -8f);
                }
                Projectile.Center = Vector2.Lerp(Projectile.Center, blockDashTarget, 0.35f);
            }
            else {
                dashLatched = false;
            }
        }

        /// <summary>格挡冲击圣环：以主人为心荡开</summary>
        protected override void ExtraDraw(SpriteBatch sb, Vector2 drawPos) {
            if (BlockFx <= 0f) {
                return;
            }
            float prog = 1f - BlockFx / BlockFxTime;
            float radius = MathHelper.Lerp(26f, 108f, VaultUtils.EaseOutCubic(prog));
            float alpha = (1f - prog) * 0.9f;
            ShockRingDraw.Draw(sb, Owner.Center, radius, 9f,
                new Color(240, 246, 255), new Color(181, 191, 224), new Color(90, 100, 140),
                alpha, innerGlow: 0.35f, timeSeed: Projectile.identity * 0.173f);
        }
    }
}
