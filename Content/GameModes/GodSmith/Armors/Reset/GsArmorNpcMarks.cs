using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Armors.Reset
{
    /// <summary>
    /// 盔甲套挂在敌人身上的标记类减益公共层：都借原版增益图标，`buffNoSave`，
    /// 只负责在 <see cref="GsArmorMarkNPC"/> 上点亮旗标；结算集中在那一个 GlobalNPC 里。
    /// 用 ModBuff 而非裸旗标的原因：NPC.AddBuff 自带同步，减速与标记在各端一致
    /// </summary>
    internal abstract class GsArmorMarkBuff : ModBuff
    {
        public override string LocalizationCategory => "GodSmithArmorsReset";

        public override void SetStaticDefaults() {
            Main.debuff[Type] = true;
            Main.pvpBuff[Type] = true;
            Main.buffNoSave[Type] = true;
        }
    }

    /// <summary>铅毒（铅套）：每秒 2 点持续伤害并压掉再生，借中毒图标</summary>
    internal class GsLeadPoisonBuff : GsArmorMarkBuff
    {
        public override string Texture => "Terraria/Images/Buff_" + BuffID.Poisoned;

        public override void Update(NPC npc, ref int buffIndex) => npc.GetGlobalNPC<GsArmorMarkNPC>().LeadPoisoned = true;
    }

    /// <summary>琥珀凝滞（化石套）：移动速度 −30%，借减速图标</summary>
    internal class GsAmberSlowBuff : GsArmorMarkBuff
    {
        public override string Texture => "Terraria/Images/Buff_" + BuffID.Slow;

        public override void Update(NPC npc, ref int buffIndex) => npc.GetGlobalNPC<GsArmorMarkNPC>().AmberSlowed = true;
    }

    /// <summary>蛛丝缠缚（蜘蛛套）：移动速度 −30%，借蛛网图标</summary>
    internal class GsWebSlowBuff : GsArmorMarkBuff
    {
        public override string Texture => "Terraria/Images/Buff_" + BuffID.Webbed;

        public override void Update(NPC npc, ref int buffIndex) => npc.GetGlobalNPC<GsArmorMarkNPC>().WebSlowed = true;
    }

    /// <summary>曜岩印（黑曜石套）：被鞭子烙印，仆从命中额外造成固定伤害，借鞭子标记图标</summary>
    internal class GsObsidianMarkBuff : GsArmorMarkBuff
    {
        public override string Texture => "Terraria/Images/Buff_" + BuffID.BlandWhipEnemyDebuff;

        public override void Update(NPC npc, ref int buffIndex) => npc.GetGlobalNPC<GsArmorMarkNPC>().ObsidianMarked = true;
    }

    /// <summary>
    /// 盔甲标记的逐帧结算面：铅毒在 UpdateLifeRegen 扣血；两种减速在 PostAI 把本帧位移回退三成
    /// （不碰 velocity，敌怪 AI 照常）；Boss 免疫减速但吃标记。另存山铜花瓣的逐敌命中计数
    /// </summary>
    internal class GsArmorMarkNPC : GlobalNPC
    {
        public override bool InstancePerEntity => true;

        internal bool LeadPoisoned;
        internal bool AmberSlowed;
        internal bool WebSlowed;
        internal bool ObsidianMarked;

        /// <summary>山铜套：该敌人累计被花瓣命中的次数（每 5 次分裂一次后归零）</summary>
        internal int PetalHits;

        /// <summary>钯金套：该敌人被同一穿戴者连续命中的次数与最近一次命中帧（2 秒不打就断）</summary>
        internal int SiphonHits;
        internal uint SiphonStamp;

        /// <summary>钯金套计数：连续命中 +1，断拍归一；达到 full 时归零并返回 true</summary>
        internal bool SiphonUp(int full, int breakFrames) {
            if (Main.GameUpdateCount - SiphonStamp > (uint)breakFrames) {
                SiphonHits = 0;
            }
            SiphonStamp = Main.GameUpdateCount;
            SiphonHits++;
            if (SiphonHits < full) {
                return false;
            }
            SiphonHits = 0;
            return true;
        }

        /// <summary>减速比例：两种减速不叠加，取其一</summary>
        private const float SlowFraction = 0.3f;

        public override void ResetEffects(NPC npc) {
            LeadPoisoned = false;
            AmberSlowed = false;
            WebSlowed = false;
            ObsidianMarked = false;
        }

        public override void UpdateLifeRegen(NPC npc, ref int damage) {
            if (!LeadPoisoned) {
                return;
            }
            if (npc.lifeRegen > 0) {
                npc.lifeRegen = 0;
            }
            //lifeRegen 单位是每秒 1/2 点：−4 即每秒 2 点
            npc.lifeRegen -= 4;
            if (damage < 1) {
                damage = 1;
            }
        }

        public override void PostAI(NPC npc) {
            if (!(AmberSlowed || WebSlowed) || npc.boss || npc.velocity == Vector2.Zero) {
                return;
            }
            npc.position -= npc.velocity * SlowFraction;
            if (Main.dedServ || !Main.rand.NextBool(4)) {
                return;
            }
            //各自的材质丝缕：琥珀滴、蛛丝
            int dustType = WebSlowed ? DustID.Web : DustID.AmberBolt;
            Dust dust = Dust.NewDustDirect(npc.position, npc.width, npc.height, dustType, 0f, 0f, 120, default, 0.9f);
            dust.noGravity = true;
            dust.velocity *= 0.2f;
        }
    }
}
