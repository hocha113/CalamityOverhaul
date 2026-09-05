using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Armors.Reset
{
    /// <summary>
    /// 肉前八套矿石甲的公共层：头盔与护腿无单件属性，胸甲按矿石档位给不同比例的挖掘速度；
    /// 头盔可镶嵌低一档头盔并在整套穿好后继承其套装奖励（镶嵌链数据见 GodSmithHelmetNestItem，配方见 GsHelmetNest）
    /// </summary>
    internal abstract class GsOreArmorScheme : GsResetArmorScheme
    {
        /// <summary>胸甲提供的挖掘速度提升比例（0.05 = 快 5%）</summary>
        protected abstract float MiningSpeed { get; }

        public override void UpdateBody(Player player, Item item) => player.pickSpeed -= MiningSpeed;
    }

    /// <summary>铜套：胸甲挖掘 +5%；套装奖励为攻击时 30% 概率追加 2 点固定伤害</summary>
    internal class GsCopperArmor : GsOreArmorScheme
    {
        public override int[] HeadIDs => [ItemID.CopperHelmet];
        public override int BodyID => ItemID.CopperChainmail;
        public override int LegsID => ItemID.CopperGreaves;
        protected override float MiningSpeed => 0.05f;
        protected override string BodyLineFallback => "5% faster mining speed";
        protected override string SetBonusLineFallback => "Attacks have a 30% chance to deal 2 extra fixed damage";

        public override void ModifyEndowHitNPC(Player player, GodSmithArmorPlayer state, NPC target,
            ref NPC.HitModifiers modifiers, Projectile sourceProj) {
            if (Main.rand.Next(100) < 30) {
                modifiers.FinalDamage.Flat += 2f;
            }
        }
    }

    /// <summary>锡套：胸甲挖掘 +6%；套装奖励为移速 +10% 与最大魔力 +50</summary>
    internal class GsTinArmor : GsOreArmorScheme
    {
        public override int[] HeadIDs => [ItemID.TinHelmet];
        public override int BodyID => ItemID.TinChainmail;
        public override int LegsID => ItemID.TinGreaves;
        protected override float MiningSpeed => 0.06f;
        protected override string BodyLineFallback => "6% faster mining speed";
        protected override string SetBonusLineFallback => "10% increased movement speed and 50 more maximum mana";

        public override void UpdateSetBonus(Player player, GodSmithArmorPlayer state) {
            player.moveSpeed += 0.10f;
            player.statManaMax2 += 50;
        }
    }

    /// <summary>铁套：胸甲挖掘 +8%；套装奖励为防御 +5、穿甲 +10，代价是移速 -20%</summary>
    internal class GsIronArmor : GsOreArmorScheme
    {
        public override int[] HeadIDs => [ItemID.IronHelmet, ItemID.AncientIronHelmet];
        public override int BodyID => ItemID.IronChainmail;
        public override int LegsID => ItemID.IronGreaves;
        protected override float MiningSpeed => 0.08f;
        protected override string BodyLineFallback => "8% faster mining speed";
        protected override string SetBonusLineFallback => "5 more defense and 10 armor penetration, but 20% reduced movement speed";

        public override void UpdateSetBonus(Player player, GodSmithArmorPlayer state) {
            player.statDefense += 5;
            player.GetArmorPenetration(DamageClass.Generic) += 10f;
            player.moveSpeed -= 0.20f;
        }
    }

    /// <summary>铅套：胸甲挖掘 +9%；套装奖励为攻击 50% 概率挂铅毒（每秒 4 点持续 6 秒），召唤栏 +1</summary>
    internal class GsLeadArmor : GsOreArmorScheme
    {
        public override int[] HeadIDs => [ItemID.LeadHelmet];
        public override int BodyID => ItemID.LeadChainmail;
        public override int LegsID => ItemID.LeadGreaves;
        protected override float MiningSpeed => 0.09f;
        protected override string BodyLineFallback => "9% faster mining speed";
        protected override string SetBonusLineFallback => "Attacks have a 50% chance to inflict Lead Poisoning (4 damage per second for 6 seconds); +1 minion slot";

        public override void UpdateSetBonus(Player player, GodSmithArmorPlayer state) => player.maxMinions += 1;

        public override void OnEndowHitNPC(Player player, GodSmithArmorPlayer state, NPC target,
            in NPC.HitInfo hit, int damageDone, Projectile sourceProj) {
            if (Main.rand.NextBool()) {
                target.AddBuff(ModContent.BuffType<GsLeadPoisonBuff>(), 360);
            }
        }
    }

    /// <summary>银套：胸甲挖掘 +11%；套装奖励沿用原版量级的防御 +3（用户未另行指定）</summary>
    internal class GsSilverArmor : GsOreArmorScheme
    {
        public override int[] HeadIDs => [ItemID.SilverHelmet];
        public override int BodyID => ItemID.SilverChainmail;
        public override int LegsID => ItemID.SilverGreaves;
        protected override float MiningSpeed => 0.11f;
        protected override string BodyLineFallback => "11% faster mining speed";
        protected override string SetBonusLineFallback => "3 more defense";

        public override void UpdateSetBonus(Player player, GodSmithArmorPlayer state) => player.statDefense += 3;
    }

    /// <summary>钨套：胸甲挖掘 +12%；套装奖励沿用原版量级的防御 +3（用户未另行指定）</summary>
    internal class GsTungstenArmor : GsOreArmorScheme
    {
        public override int[] HeadIDs => [ItemID.TungstenHelmet];
        public override int BodyID => ItemID.TungstenChainmail;
        public override int LegsID => ItemID.TungstenGreaves;
        protected override float MiningSpeed => 0.12f;
        protected override string BodyLineFallback => "12% faster mining speed";
        protected override string SetBonusLineFallback => "3 more defense";

        public override void UpdateSetBonus(Player player, GodSmithArmorPlayer state) => player.statDefense += 3;
    }

    /// <summary>金套：胸甲挖掘 +14%；套装奖励为伤害 +15%，代价是防御 -20</summary>
    internal class GsGoldArmor : GsOreArmorScheme
    {
        public override int[] HeadIDs => [ItemID.GoldHelmet, ItemID.AncientGoldHelmet];
        public override int BodyID => ItemID.GoldChainmail;
        public override int LegsID => ItemID.GoldGreaves;
        protected override float MiningSpeed => 0.14f;
        protected override string BodyLineFallback => "14% faster mining speed";
        protected override string SetBonusLineFallback => "15% increased damage, but 20 less defense";

        public override void UpdateSetBonus(Player player, GodSmithArmorPlayer state) {
            player.statDefense -= 20;
            player.GetDamage(DamageClass.Generic) += 0.15f;
        }
    }

    /// <summary>铂金套：胸甲挖掘 +15%；套装奖励为防御 +10、召唤栏 +1，代价是伤害 -15%</summary>
    internal class GsPlatinumArmor : GsOreArmorScheme
    {
        public override int[] HeadIDs => [ItemID.PlatinumHelmet];
        public override int BodyID => ItemID.PlatinumChainmail;
        public override int LegsID => ItemID.PlatinumGreaves;
        protected override float MiningSpeed => 0.15f;
        protected override string BodyLineFallback => "15% faster mining speed";
        protected override string SetBonusLineFallback => "10 more defense and +1 minion slot, but 15% reduced damage";

        public override void UpdateSetBonus(Player player, GodSmithArmorPlayer state) {
            player.statDefense += 10;
            player.maxMinions += 1;
            player.GetDamage(DamageClass.Generic) -= 0.15f;
        }
    }

    /// <summary>铅毒：铅套挂在敌人身上的持续伤害减益，借原版中毒图标；每秒 4 点</summary>
    internal class GsLeadPoisonBuff : ModBuff
    {
        public override string Texture => "Terraria/Images/Buff_" + BuffID.Poisoned;

        public override string LocalizationCategory => "GodSmithArmorsReset";

        public override void SetStaticDefaults() {
            Main.debuff[Type] = true;
            Main.pvpBuff[Type] = true;
            Main.buffNoSave[Type] = true;
        }

        public override void Update(NPC npc, ref int buffIndex) {
            npc.GetGlobalNPC<GsLeadPoisonNPC>().LeadPoisoned = true;
        }
    }

    /// <summary>铅毒的每帧结算面：在 UpdateLifeRegen 里压再生并扣血（每秒 4 点）</summary>
    internal class GsLeadPoisonNPC : GlobalNPC
    {
        public override bool InstancePerEntity => true;

        internal bool LeadPoisoned;

        public override void ResetEffects(NPC npc) => LeadPoisoned = false;

        public override void UpdateLifeRegen(NPC npc, ref int damage) {
            if (!LeadPoisoned) {
                return;
            }
            if (npc.lifeRegen > 0) {
                npc.lifeRegen = 0;
            }
            npc.lifeRegen -= 8;
            if (damage < 2) {
                damage = 2;
            }
        }
    }
}
