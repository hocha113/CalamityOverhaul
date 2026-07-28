using CalamityOverhaul.Content.PRTTypes;
using CalamityOverhaul.Content.Wraiths.Buffs;
using CalamityOverhaul.Content.Wraiths.Core;
using InnoVault.PRT;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Wraiths.Abilities
{
    /// <summary>
    /// 替死鬼借力：施放后挂保护印记，下次致死伤害强制转移到替死目标。<br/>
    /// 重复施放（贪开二重保险）为 Taboo。
    /// </summary>
    internal sealed class ScapeGhostAbility : WraithAbility
    {
        public override int CooldownTicks => 60 * 120;
        public override float ErosionCost => 0.04f;
        public override float MasteryWear => 0.008f;
        public override float TabooPenalty => 0.05f;

        public override WraithCastResult Cast(WraithAbilityContext ctx) {
            // 已有印记：贪开二重保险，犯戒
            if (ctx.Player.HasBuff(ModContent.BuffType<ScapeGhostMark>()))
                return WraithCastResult.Taboo;
            return WraithCastResult.Success;
        }

        public override void ExecuteWorld(Player caster, Vector2 aim, float mastery) {
            int buffType = ModContent.BuffType<ScapeGhostMark>();
            if (caster.HasBuff(buffType)) {
                return;
            }
            // 驾驭度 0→30s，1→90s
            int ticks = (int)MathHelper.Lerp(60 * 30, 60 * 90, mastery);
            caster.AddBuff(buffType, ticks);
        }

        public override void PlayWorldFx(Player caster, Vector2 aim) {
            if (Main.dedServ)
                return;

            // 激活时：血色烟雾从玩家身上缓慢上升，示意鬼魂附身驻守
            for (int i = 0; i < 8; i++) {
                Vector2 pos = caster.Center + new Vector2(Main.rand.NextFloat(-18f, 18f), Main.rand.NextFloat(-8f, 12f));
                Vector2 vel = new Vector2(Main.rand.NextFloat(-0.5f, 0.5f), Main.rand.NextFloat(-1.2f, -0.4f));
                Color c = Color.Lerp(new Color(96, 12, 18), new Color(156, 22, 28), Main.rand.NextFloat());
                PRTLoader.NewParticle<PRT_Smoke>(pos, vel, c, Main.rand.NextFloat(0.08f, 0.14f))
                    ?.Configure(Main.rand.Next(28, 48), Main.rand.NextFloat(0.45f, 0.7f)
                        , Main.rand.NextFloat(-0.02f, 0.02f));
            }

            // 两粒慢速血珠从玩家手背渗出
            for (int i = 0; i < 3; i++) {
                Vector2 pos = caster.Center + new Vector2(Main.rand.NextFloat(-10f, 10f), Main.rand.NextFloat(-4f, 8f));
                Vector2 vel = new Vector2(Main.rand.NextFloat(-1f, 1f), Main.rand.NextFloat(-0.8f, 0.2f));
                PRTLoader.NewParticle<PRT_HeartcarverDroplet>(pos, vel, new Color(120, 15, 20)
                    , Main.rand.NextFloat(0.6f, 0.9f))
                    ?.Configure(Main.rand.Next(20, 30), 0.22f);
            }

            SoundEngine.PlaySound(SoundID.NPCDeath6 with { Pitch = -0.9f, Volume = 0.35f }, caster.Center);
        }
    }
}