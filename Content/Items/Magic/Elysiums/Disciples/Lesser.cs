using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Magic.Elysiums.Disciples
{
    /// <summary>
    /// 小雅各·奉献(席位8)：奉献治愈。
    /// 主人带伤时献上治愈：回复生命，光尘自小雅各涌向主人再化作升腾的柔光
    /// </summary>
    internal class Lesser : BaseDisciple
    {
        public override int Seat => 8;

        private const int HealAmount = 45;

        protected override bool TryCast() => Owner.statLife < (int)(Owner.statLifeMax2 * 0.86f);

        protected override void ExecuteAbility() {
            SoundEngine.PlaySound(SoundID.Item4 with { Volume = 0.7f, Pitch = 0.4f }, Owner.Center);

            //治疗只在主人端结算(绿字广播全端可见)
            if (Projectile.IsOwnedByLocalPlayer()) {
                int heal = HealAmount;
                Owner.statLife += heal;
                if (Owner.statLife > Owner.statLifeMax2) {
                    Owner.statLife = Owner.statLifeMax2;
                }
                Owner.HealEffect(heal, true);
            }

            //奉献演出：光尘自小雅各涌向主人 + 主人身上升起柔光十字
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < 10; i++) {
                Vector2 pos = Projectile.Center + Main.rand.NextVector2Circular(14f, 18f);
                Vector2 vel = (Owner.Center - pos).SafeNormalize(Vector2.UnitY) * Main.rand.NextFloat(4f, 7f);
                PRTLoader.NewParticle<PRT_Light>(pos, vel, Def.AccentColor, Main.rand.NextFloat(0.22f, 0.36f))
                    ?.Configure(Main.rand.Next(18, 28), 0.9f, _entity: Owner, _followingRateRatio: 0.5f);
            }
            //柔光十字：五点定位摆出小十字后各自上飘
            for (int i = 0; i < 5; i++) {
                Vector2 offset = i switch {
                    0 => Vector2.Zero,
                    1 => new Vector2(0f, -14f),
                    2 => new Vector2(0f, 14f),
                    3 => new Vector2(-11f, -4f),
                    _ => new Vector2(11f, -4f),
                };
                PRTLoader.NewParticle<PRT_Light>(Owner.Center + offset, new Vector2(0f, -1.4f)
                    , new Color(214, 255, 220), 0.3f)?.Configure(30, 0.95f, _entity: Owner, _followingRateRatio: 0.85f);
            }
        }
    }
}
