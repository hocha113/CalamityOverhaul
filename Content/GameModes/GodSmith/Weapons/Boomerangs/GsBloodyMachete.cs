using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Boomerangs
{
    /// <summary>
    /// 血腥砍刀重铸。材质：锈口屠刀。签名行为：①命中附加流血并记刻，同一目标第三刻触发放血
    /// ②放血那一击加伤 40%，并为你回复 2 点生命 ③音色钝沉
    /// </summary>
    internal class GsBloodyMachete : GsBoomerScheme
    {
        public override int TargetItemID => ItemID.BloodyMachete;

        internal override int BoomerProjType => ModContent.ProjectileType<GsBloodyMacheteProj>();

        internal override float DamageMul => 1.10f;   //万圣节掉落的弱势镖，补一成底伤

        protected override string GsDescFallback =>
            "Hits inflict Bleeding and carve a tally; the third tally on one target lets the blood loose\nThe bloodletting strike deals 40% bonus damage and heals you for 2 life";
    }

    /// <summary>屠刀镖体：放血三刻</summary>
    internal class GsBloodyMacheteProj : GsBoomerProjBase
    {
        internal override int SourceItemID => ItemID.BloodyMachete;

        protected override SoundStyle HitSound => SoundID.Tink with { Volume = 0.45f, Pitch = -0.4f };

        /// <summary>目标 whoAmI → 刻数（owner 判定端本地量）</summary>
        private readonly Dictionary<int, int> tally = [];

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            tally.TryGetValue(target.whoAmI, out int cur);
            if (cur + 1 >= 3) {
                modifiers.FinalDamage *= 1.4f;
            }
        }

        protected override void OnHitEffects(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.Bleeding, 240);
            tally.TryGetValue(target.whoAmI, out int cur);
            cur++;
            if (cur >= 3) {
                tally[target.whoAmI] = 0;
                //放血：owner 端回 2 点血（自己客户端写自己的生命，逐帧差量自动同步）
                Player owner = Owner;
                if (owner.whoAmI == Main.myPlayer && owner.statLife < owner.statLifeMax2) {
                    owner.Heal(2);
                }
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.NPCHit18 with { Volume = 0.5f, Pitch = -0.3f }, target.Center);
                }
            }
            else {
                tally[target.whoAmI] = cur;
            }
        }
    }
}
