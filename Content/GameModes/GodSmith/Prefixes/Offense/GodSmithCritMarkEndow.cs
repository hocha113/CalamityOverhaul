using CalamityOverhaul.Content.GameModes.GodSmith.Core;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Prefixes.Offense
{
    /// <summary>
    /// 【暴击系·印记】会心印记：覆盖暴击词缀群（狂热/锋利之刃 Keen/瞄准/灵巧/杀戮），
    /// 命中在目标身上累刻金色印记，叠满三层引爆为一记必定暴击的会心爆裂。
    /// 印记是攻击方端本地量（私有 ModPlayer），爆裂弹实体跨端可见
    /// </summary>
    internal class GodSmithCritMarkEndow : GodSmithEndow
    {
        /// <summary>引爆所需层数</summary>
        internal const int FullStacks = 3;

        /// <summary>爆裂基础伤害占触发伤害比（顶级档，必定暴击后翻倍生效）</summary>
        internal const float BaseDamageRatio = 0.20f;

        public override int[] CoveredPrefixes => [
            PrefixID.Zealous, PrefixID.Keen, PrefixID.Sighted, PrefixID.Agile, PrefixID.Murderous,
        ];

        public override float TierScaleFor(int prefixId) => prefixId switch {
            PrefixID.Zealous => 1f,
            PrefixID.Keen => 0.7f,
            PrefixID.Sighted => 0.65f,
            _ => 0.6f,
        };

        protected override string EndowNameFallback => "Critical Sigil";

        protected override string EndowDescFallback =>
            "Hits engrave a sigil; at {0} stacks it detonates as a guaranteed critical dealing {1}% of that hit";

        public override object[] DescFormatArgs(Item item)
            => [FullStacks, (BaseDamageRatio * 100f * TierScaleFor(item.prefix)).ToString("0.#")];

        public override void OnHitNPC(Player player, Item sourceItem, Projectile sourceProj, NPC target,
            in NPC.HitInfo hit, int damageDone, float tierScale) {
            if (target.friendly || target.type == NPCID.TargetDummy || target.life <= 0) {
                return;
            }
            if (player.whoAmI != Main.myPlayer) {
                return;
            }
            //爆裂弹自身命中不再刻印，防自循环
            if (sourceProj != null && sourceProj.type == ModContent.ProjectileType<GodSmithCritMarkBurst>()) {
                return;
            }
            GodSmithCritMarkEndowPlayer marks = player.GetModPlayer<GodSmithCritMarkEndowPlayer>();
            if (marks.AddStack(target) < FullStacks) {
                return;
            }
            marks.ClearStack(target.whoAmI);
            int damage = Math.Clamp((int)(damageDone * BaseDamageRatio * tierScale), 6, 500);
            Projectile.NewProjectile(player.GetSource_Misc("GodSmithCritMarkEndow"), target.Center,
                Vector2.Zero, ModContent.ProjectileType<GodSmithCritMarkBurst>(), damage, 1f, player.whoAmI);
        }
    }

    /// <summary>会心印记的攻击方端记账：目标 → 层数，带时限与类型校验，跨帧保留</summary>
    internal class GodSmithCritMarkEndowPlayer : ModPlayer
    {
        /// <summary>层数窗口（帧）</summary>
        internal const int StackWindow = 240;

        private readonly Dictionary<int, (int npcType, int stacks, uint expire)> marks = [];

        /// <summary>为目标叠一层，返回当前层数；换代或超时的旧记录直接重置</summary>
        internal int AddStack(NPC target) {
            if (marks.TryGetValue(target.whoAmI, out (int npcType, int stacks, uint expire) mark)
                && mark.npcType == target.type && Main.GameUpdateCount < mark.expire) {
                marks[target.whoAmI] = (target.type, mark.stacks + 1, Main.GameUpdateCount + StackWindow);
                return mark.stacks + 1;
            }
            marks[target.whoAmI] = (target.type, 1, Main.GameUpdateCount + StackWindow);
            return 1;
        }

        internal void ClearStack(int whoAmI) => marks.Remove(whoAmI);
    }

    /// <summary>会心爆裂：在目标身上炸开的短命判定，命中必定暴击。misc 出生源，无级联</summary>
    internal class GodSmithCritMarkBurst : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.LightBeam;

        public override void SetDefaults() {
            Projectile.width = 60;
            Projectile.height = 60;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Generic;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 20;
            Projectile.tileCollide = false;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 20;
            Projectile.aiStyle = 0;
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) => modifiers.SetCrit();

        public override void AI() {
            if (Projectile.timeLeft == 19 && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item4 with { Volume = 0.6f, Pitch = 0.5f }, Projectile.Center);
            }
        }
    }
}
