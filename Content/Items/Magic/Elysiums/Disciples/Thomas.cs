using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Magic.Elysiums.Disciples
{
    /// <summary>
    /// 多马·验证(席位6)：验证之目。
    /// 敌人在场时周期性为主人开启验证：一段时间内主人的攻击必然暴击
    /// (结算接线在 <see cref="ElysiumPlayer"/> 的命中钩子)
    /// </summary>
    internal class Thomas : BaseDisciple
    {
        public override int Seat => 6;

        private const int VerifyDuration = 300;

        protected override bool TryCast() {
            if (Owner.HasBuff<VerificationBuff>()) {
                return false;
            }
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (npc.active && npc.CanBeChasedBy(Projectile)
                    && Vector2.Distance(npc.Center, Owner.Center) < 640f) {
                    return true;
                }
            }
            return false;
        }

        protected override void ExecuteAbility() {
            SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.7f, Pitch = 0.55f }, Projectile.Center);
            SoundEngine.PlaySound(SoundID.MaxMana with { Volume = 0.6f, Pitch = -0.1f }, Owner.Center);

            if (Projectile.IsOwnedByLocalPlayer()) {
                Owner.AddBuff(ModContent.BuffType<VerificationBuff>(), VerifyDuration);
            }

            //验证之光：多马注视主人，一线洞察之光牵过去
            if (Main.dedServ) {
                return;
            }
            Vector2 dir = (Owner.Center - Projectile.Center).SafeNormalize(Vector2.UnitX);
            float dist = Vector2.Distance(Owner.Center, Projectile.Center);
            for (int i = 0; i < 9; i++) {
                Vector2 pos = Projectile.Center + dir * (dist * i / 9f);
                PRTLoader.NewParticle<PRT_Light>(pos, dir * 1.5f, Def.AccentColor, 0.24f)?.Configure(14, 0.85f);
            }
            for (int i = 0; i < 5; i++) {
                PRTLoader.NewParticle<PRT_HeavenfallStar>(Owner.Center, VaultUtils.RandVr(1.5f, 4f)
                    , Def.AccentColor, Main.rand.NextFloat(0.5f, 0.8f))?.Configure(false, Main.rand.Next(12, 18));
            }
        }
    }
}
