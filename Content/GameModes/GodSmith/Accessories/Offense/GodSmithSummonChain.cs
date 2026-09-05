using CalamityOverhaul.Content.GameModes.GodSmith.Core;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Accessories.Offense
{
    /// <summary>
    /// 【召唤链】四件驭军之物四种军略：侏儒项链=战舞叠层、亡灵卷轴=收魂反噬（击杀起魂焰）、
    /// 纸莎草圣甲虫=点金圣甲（镀金联动增伤）、赫拉克勒斯甲虫=角力蓄冲（满层放冲角）。<br/>
    /// 全部按召唤类过滤（支援弹为 DamageClass.Default，防自喂）；
    /// 每玩家状态在同文件私有 <see cref="MinionBondPlayer"/>
    /// </summary>
    internal class GodSmithPygmyNecklace : GodSmithAccEffect
    {
        /// <summary>战舞叠层上限</summary>
        internal const int MaxStacks = 5;

        /// <summary>战舞持续帧数（命中刷新）</summary>
        internal const int DanceDuration = 360;

        /// <summary>叠层内置冷却</summary>
        private const int StackICD = 20;

        public override int[] TargetItemIDs => [ItemID.PygmyNecklace];

        protected override string EffectDescFallback =>
            "War Dance: minion hits keep the dance alive, +1.2% summon damage per stack, up to 5 stacks (6s)\nAt full tempo minion strikes scatter tribal feather sparks";

        public override void UpdateAccessory(Item item, Player player, bool hideVisual, GodSmithPlayer state) {
            MinionBondPlayer bond = player.GetModPlayer<MinionBondPlayer>();
            if (bond.DanceStacks > 0) {
                player.GetDamage(DamageClass.Summon) += 0.012f * bond.DanceStacks;
            }
        }

        public override void OnHitNPC(Item item, Player player, GodSmithPlayer state, NPC target,
            in NPC.HitInfo hit, int damageDone, bool fromProjectile) {
            if (!hit.DamageType.CountsAsClass(DamageClass.Summon)) {
                return;
            }
            if (state.TryUseCooldown(item.type, StackICD)) {
                player.GetModPlayer<MinionBondPlayer>().AddDanceStack();
            }
        }
    }

    /// <summary>亡灵卷轴：仆从收割亡魂，尸骸处起缚魂魂焰反噬余敌，死灵经济学</summary>
    internal class GodSmithNecromanticScroll : GodSmithAccEffect
    {
        /// <summary>收魂冷却帧数</summary>
        private const int SoulCD = 90;

        public override int[] TargetItemIDs => [ItemID.NecromanticScroll];

        protected override string EffectDescFallback =>
            "Soul Harvest: when a minion kill lands, a bound soulflame rises from the corpse\nIt homes in on the nearest foe dealing 40% of the killing blow, once every 1.5s";

        public override void UpdateAccessory(Item item, Player player, bool hideVisual, GodSmithPlayer state) { }

        public override void OnHitNPC(Item item, Player player, GodSmithPlayer state, NPC target,
            in NPC.HitInfo hit, int damageDone, bool fromProjectile) {
            //只在召唤系击杀瞬间收魂
            if (!hit.DamageType.CountsAsClass(DamageClass.Summon) || target.life > 0
                || !state.TryUseCooldown(item.type, SoulCD)) {
                return;
            }
            SoundEngine.PlaySound(SoundID.NPCDeath6 with { Volume = 0.4f, Pitch = 0.5f }, target.Center);
            if (player.whoAmI == Main.myPlayer) {
                int soulDamage = Math.Clamp((int)(damageDone * 0.4f), 10, 150);
                Projectile.NewProjectile(player.GetSource_Accessory(item), target.Center,
                    new Vector2(0f, -3f), ModContent.ProjectileType<GodSmithNecromanticScrollSoulProj>(),
                    soulDamage, 1f, player.whoAmI);
            }
        }
    }

    /// <summary>纸莎草圣甲虫：仆从命中镀金目标（掉更多钱），场上有镀金者时全军增伤，宝藏军略</summary>
    internal class GodSmithPapyrusScarab : GodSmithAccEffect
    {
        /// <summary>镀金冷却帧数</summary>
        private const int GildCD = 60;

        public override int[] TargetItemIDs => [ItemID.PapyrusScarab];

        protected override string EffectDescFallback =>
            "Gilded Scarab: minion hits gild the target with Midas, once every 1s\nWhile any foe is gilded, your minions deal +5% damage";

        public override void UpdateAccessory(Item item, Player player, bool hideVisual, GodSmithPlayer state) {
            //镀金联动：场上任一敌人带点金债即全军增伤
            foreach (NPC npc in Main.ActiveNPCs) {
                if (!npc.friendly && npc.HasBuff(BuffID.Midas)) {
                    player.GetDamage(DamageClass.Summon) += 0.05f;
                    break;
                }
            }
        }

        public override void OnHitNPC(Item item, Player player, GodSmithPlayer state, NPC target,
            in NPC.HitInfo hit, int damageDone, bool fromProjectile) {
            if (!hit.DamageType.CountsAsClass(DamageClass.Summon) || !state.TryUseCooldown(item.type, GildCD)) {
                return;
            }
            target.AddBuff(BuffID.Midas, 360);
            SoundEngine.PlaySound(SoundID.CoinPickup with { Volume = 0.5f, Pitch = -0.2f }, target.Center);
        }
    }

    /// <summary>赫拉克勒斯甲虫：仆从命中蓄角力，满层放出赫拉克勒斯冲角横贯战线，巨力图腾</summary>
    internal class GodSmithHerculesBeetle : GodSmithAccEffect
    {
        /// <summary>角力蓄层上限</summary>
        internal const int ChargeMax = 8;

        /// <summary>蓄层内置冷却</summary>
        private const int ChargeICD = 8;

        /// <summary>冲角冷却帧数</summary>
        private const int RamCD = 60;

        /// <summary>副冷却键正键高位偏移（负键域归词缀神赋，约定 2026-08-27）</summary>
        private const int SecondaryCDKeyOffset = 10_000_000;

        public override int[] TargetItemIDs => [ItemID.HerculesBeetle];

        protected override string EffectDescFallback =>
            "Hercules Ram: minion hits build wrestling might; at 8 charges the next minion hit\nlooses a colossal beetle horn that rams through the line, dealing 90% of that hit with massive knockback";

        public override void UpdateAccessory(Item item, Player player, bool hideVisual, GodSmithPlayer state) { }

        public override void OnHitNPC(Item item, Player player, GodSmithPlayer state, NPC target,
            in NPC.HitInfo hit, int damageDone, bool fromProjectile) {
            if (!hit.DamageType.CountsAsClass(DamageClass.Summon)) {
                return;
            }
            MinionBondPlayer bond = player.GetModPlayer<MinionBondPlayer>();
            if (bond.RamCharge < ChargeMax) {
                if (state.TryUseCooldown(item.type, ChargeICD)) {
                    bond.RamCharge++;
                }
                return;
            }
            //满层结算：冲角冷却单独走高位副键，防与蓄层冷却互踩
            if (!state.TryUseCooldown(item.type + SecondaryCDKeyOffset, RamCD)) {
                return;
            }
            bond.RamCharge = 0;
            SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.5f, Pitch = 0.3f }, target.Center);
            if (player.whoAmI == Main.myPlayer) {
                int ramDamage = Math.Clamp((int)(damageDone * 0.9f), 15, 320);
                Vector2 vel = new Vector2(hit.HitDirection, 0f).SafeNormalize(Vector2.UnitX) * 12f;
                Projectile.NewProjectile(player.GetSource_Accessory(item),
                    target.Center - vel * 6f, vel,
                    ModContent.ProjectileType<GodSmithHerculesBeetleRamProj>(), ramDamage, 11f, player.whoAmI);
            }
        }
    }

    /// <summary>缚魂魂焰：一缕自尸骸拔起的死灵之火，先腾空定魂再俯咬最近之敌</summary>
    internal class GodSmithNecromanticScrollSoulProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.LostSoulFriendly;

        private ref float Life => ref Projectile.ai[0];

        /// <summary>定魂段帧数，之后俯咬</summary>
        private const int RiseFrames = 14;

        public override void SetDefaults() {
            Projectile.width = 14;
            Projectile.height = 14;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Default;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 90;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
        }

        public override void AI() {
            Life++;
            if (Life <= RiseFrames) {
                Projectile.velocity *= 0.92f;
            }
            else {
                NPC target = FindTarget();
                if (target != null) {
                    Vector2 want = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitX) * 10f;
                    float turn = MathHelper.Clamp((Life - RiseFrames) / 18f, 0.06f, 0.2f);
                    Projectile.velocity = Vector2.Lerp(Projectile.velocity, want, turn);
                }
                else {
                    Projectile.velocity *= 0.96f;
                }
            }
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
        }

        private NPC FindTarget() {
            NPC best = null;
            float bestDist = 550f;
            foreach (NPC npc in Main.ActiveNPCs) {
                if (!npc.CanBeChasedBy(Projectile)) {
                    continue;
                }
                float dist = Projectile.Center.Distance(npc.Center);
                if (dist < bestDist) {
                    bestDist = dist;
                    best = npc;
                }
            }
            return best;
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            SoundEngine.PlaySound(SoundID.NPCDeath6 with { Volume = 0.3f, Pitch = 0.8f }, Projectile.Center);
        }
    }

    /// <summary>赫拉克勒斯冲角：一记有吨位的甲虫巨角，减速前推、犁开战线；重击退是它的语言</summary>
    internal class GodSmithHerculesBeetleRamProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.Stinger;

        private const int LifeMax = 32;

        public override void SetDefaults() {
            Projectile.width = 46;
            Projectile.height = 34;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Default;
            Projectile.penetrate = -1;
            Projectile.timeLeft = LifeMax;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override void AI() {
            //冲角减速前推：先猛后滞，不匀速
            Projectile.velocity *= 0.965f;
            //原版毒刺贴图竖向朝上，旋转补四分之一圈
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (Main.dedServ) {
                return;
            }
            SoundEngine.PlaySound(SoundID.NPCHit4 with { Volume = 0.5f, Pitch = -0.2f }, target.Center);
        }
    }

    /// <summary>召唤链私有状态载体：战舞叠层与角力蓄层。攻击方端本地量，无需同步</summary>
    internal class MinionBondPlayer : ModPlayer
    {
        /// <summary>侏儒项链：战舞层数</summary>
        internal int DanceStacks { get; private set; }

        private int danceTimer;

        /// <summary>赫拉克勒斯甲虫：角力蓄层</summary>
        internal int RamCharge;

        internal void AddDanceStack() {
            DanceStacks = Math.Min(DanceStacks + 1, GodSmithPygmyNecklace.MaxStacks);
            danceTimer = GodSmithPygmyNecklace.DanceDuration;
        }

        public override void PostUpdateMiscEffects() {
            if (danceTimer > 0 && --danceTimer == 0) {
                DanceStacks = 0;
            }
        }

        public override void UpdateDead() {
            DanceStacks = 0;
            danceTimer = 0;
            RamCharge = 0;
        }
    }
}
