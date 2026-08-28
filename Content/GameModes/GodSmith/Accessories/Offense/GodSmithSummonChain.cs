using CalamityOverhaul.Content.GameModes.GodSmith.Core;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
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
            MinionBondPlayer bond = player.GetModPlayer<MinionBondPlayer>();
            if (state.TryUseCooldown(item.type, StackICD)) {
                bond.AddDanceStack();
            }
            //满拍战舞：图腾彩羽迸出（命中钩子只在攻击方端跑）
            if (bond.DanceStacks >= MaxStacks && Main.rand.NextBool(2)) {
                for (int i = 0; i < 2; i++) {
                    PRTLoader.NewParticle<PRT_Spark>(target.Center + Main.rand.NextVector2Circular(8f, 8f),
                        new Vector2(Main.rand.NextFloat(-1.5f, 1.5f), -Main.rand.NextFloat(1f, 3f)),
                        Main.rand.NextBool() ? new Color(255, 120, 80) : new Color(80, 200, 160),
                        Main.rand.NextFloat(0.26f, 0.42f))?.Configure(true, Main.rand.Next(14, 22));
                }
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
            //魂气自尸骸升腾
            for (int i = 0; i < 5; i++) {
                PRTLoader.NewParticle<PRT_Spark>(target.Center + Main.rand.NextVector2Circular(10f, 8f),
                    new Vector2(Main.rand.NextFloat(-0.8f, 0.8f), -Main.rand.NextFloat(1f, 2.5f)),
                    Main.rand.NextBool() ? new Color(120, 255, 160) : new Color(60, 180, 120),
                    Main.rand.NextFloat(0.26f, 0.44f))?.Configure(false, Main.rand.Next(16, 26));
            }
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
            //金鳞自甲壳迸落
            for (int i = 0; i < 6; i++) {
                Dust dust = Dust.NewDustPerfect(target.Center + Main.rand.NextVector2Circular(10f, 10f),
                    DustID.GoldCoin, new Vector2(Main.rand.NextFloat(-2f, 2f), Main.rand.NextFloat(-1f, 2f)));
                dust.noGravity = Main.rand.NextBool();
            }
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

        public override void UpdateAccessory(Item item, Player player, bool hideVisual, GodSmithPlayer state) {
            MinionBondPlayer bond = player.GetModPlayer<MinionBondPlayer>();
            //蓄满读数：琥珀微光绕身（个人读数）
            if (bond.RamCharge >= ChargeMax && !VaultUtils.isServer && Main.rand.NextBool(10)) {
                PRTLoader.NewParticle<PRT_Light>(player.Center + Main.rand.NextVector2Circular(14f, 18f),
                    new Vector2(0f, -Main.rand.NextFloat(0.3f, 0.8f)), new Color(220, 160, 60),
                    Main.rand.NextFloat(0.06f, 0.1f))?.Configure(12, 0.7f);
            }
        }

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

    /// <summary>
    /// 缚魂魂焰：一缕自尸骸拔起的死灵之火，先腾空定魂再俯咬最近之敌；
    /// 三层鬼绿焰体自绘 + 升腾余烬，亡处魂气散尽
    /// </summary>
    internal class GodSmithNecromanticScrollSoulProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private ref float Life => ref Projectile.ai[0];

        /// <summary>确定性绘制相位，绘制路径不掷 Main.rand</summary>
        private float Seed => Projectile.identity * 0.9127f % 2.63f;

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
            if (!Main.dedServ && Life % 3 == 0) {
                PRTLoader.NewParticle<PRT_Spark>(Projectile.Center + Main.rand.NextVector2Circular(4f, 4f),
                    new Vector2(0f, -Main.rand.NextFloat(0.5f, 1.2f)) - Projectile.velocity * 0.05f,
                    Main.rand.NextBool() ? new Color(120, 255, 160) : new Color(50, 160, 110),
                    Main.rand.NextFloat(0.18f, 0.3f))?.Configure(false, Main.rand.Next(10, 16));
            }
            Lighting.AddLight(Projectile.Center, new Vector3(0.12f, 0.4f, 0.22f));
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
            for (int i = 0; i < 6; i++) {
                PRTLoader.NewParticle<PRT_Spark>(Projectile.Center,
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(1f, 3.5f),
                    Main.rand.NextBool() ? new Color(120, 255, 160) : new Color(60, 180, 120),
                    Main.rand.NextFloat(0.22f, 0.38f))?.Configure(false, Main.rand.Next(12, 20));
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = CWRAsset.Extra_98?.Value;
            if (tex == null) {
                return false;
            }
            Vector2 pos = Projectile.Center - Main.screenPosition;
            Vector2 origin = tex.Size() * 0.5f;
            float stretch = MathHelper.Clamp(Projectile.velocity.Length() * 0.05f, 0.1f, 0.5f);
            //魂焰摇曳：宽窄以确定性相位反相呼吸
            float wob = MathF.Sin(Life * 0.5f + Seed * 6f) * 0.15f;
            Vector2 flicker = new(1f + wob, 1f - wob * 0.7f);
            //焰缘墨绿
            Main.EntitySpriteDraw(tex, pos, null, new Color(30, 90, 60) * 0.8f, Projectile.rotation,
                origin, new Vector2(0.3f, 0.38f + stretch) * flicker, SpriteEffects.None, 0);
            //焰体鬼绿
            Main.EntitySpriteDraw(tex, pos, null, new Color(80, 220, 140) with { A = 0 } * 0.8f,
                Projectile.rotation, origin, new Vector2(0.22f, 0.3f + stretch * 0.8f) * flicker, SpriteEffects.None, 0);
            //焰芯苍白
            Main.EntitySpriteDraw(tex, pos, null, new Color(210, 255, 225) with { A = 0 } * 0.6f,
                Projectile.rotation, origin, new Vector2(0.09f, 0.15f + stretch * 0.4f) * flicker, SpriteEffects.None, 0);
            return false;
        }
    }

    /// <summary>
    /// 赫拉克勒斯冲角：一记有吨位的甲虫巨角，减速前推、犁开战线；
    /// 双层琥珀角体自绘 + 犁地碎屑，重击退是它的语言
    /// </summary>
    internal class GodSmithHerculesBeetleRamProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private ref float Life => ref Projectile.ai[0];

        /// <summary>确定性绘制相位，绘制路径不掷 Main.rand</summary>
        private float Seed => Projectile.identity * 0.5711f % 3.07f;

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
            Life++;
            //冲角减速前推：先猛后滞，不匀速
            Projectile.velocity *= 0.965f;
            Projectile.rotation = Projectile.velocity.ToRotation();
            if (!Main.dedServ && Life % 2 == 0) {
                //犁开地面的碎屑向后抛
                PRTLoader.NewParticle<PRT_Spark>(
                    Projectile.Center - Projectile.velocity + Main.rand.NextVector2Circular(8f, 10f),
                    -Projectile.velocity * 0.15f + new Vector2(0f, -Main.rand.NextFloat(0.5f, 1.5f)),
                    Main.rand.NextBool() ? new Color(220, 160, 60) : new Color(140, 90, 40),
                    Main.rand.NextFloat(0.24f, 0.42f))?.Configure(true, Main.rand.Next(12, 20));
            }
            Lighting.AddLight(Projectile.Center, new Vector3(0.35f, 0.25f, 0.08f));
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (Main.dedServ) {
                return;
            }
            SoundEngine.PlaySound(SoundID.NPCHit4 with { Volume = 0.5f, Pitch = -0.2f }, target.Center);
            for (int i = 0; i < 5; i++) {
                PRTLoader.NewParticle<PRT_Spark>(target.Center,
                    Projectile.velocity.SafeNormalize(Vector2.UnitX).RotatedByRandom(0.8f)
                        * Main.rand.NextFloat(2f, 6f),
                    Main.rand.NextBool() ? new Color(255, 210, 120) : new Color(220, 160, 60),
                    Main.rand.NextFloat(0.3f, 0.5f))?.Configure(true, Main.rand.Next(14, 22));
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = CWRAsset.Extra_98?.Value;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (tex == null || glow == null) {
                return false;
            }
            float fade = 1f - MathHelper.Clamp((Life - 20f) / (LifeMax - 20f), 0f, 1f);
            Vector2 pos = Projectile.Center - Main.screenPosition;
            Vector2 origin = tex.Size() * 0.5f;
            float stretch = MathHelper.Clamp(Projectile.velocity.Length() * 0.06f, 0.15f, 0.7f);
            float wob = 1f + MathF.Sin(Life * 0.7f + Seed * 4f) * 0.08f;
            //角势气浪垫底
            Main.EntitySpriteDraw(glow, pos - Projectile.velocity * 0.8f, null,
                new Color(200, 140, 50) with { A = 0 } * (0.35f * fade), 0f, glow.Size() * 0.5f,
                1.5f * wob, SpriteEffects.None, 0);
            //甲壳深棕压边
            Main.EntitySpriteDraw(tex, pos, null, new Color(90, 55, 25) * (0.85f * fade),
                Projectile.rotation, origin, new Vector2(0.5f + stretch, 0.4f) * wob, SpriteEffects.None, 0);
            //角面琥珀
            Main.EntitySpriteDraw(tex, pos, null, new Color(220, 160, 60) with { A = 0 } * (0.85f * fade),
                Projectile.rotation, origin, new Vector2(0.4f + stretch * 0.8f, 0.3f) * wob, SpriteEffects.None, 0);
            //角尖亮金
            Main.EntitySpriteDraw(tex, pos + Projectile.velocity.SafeNormalize(Vector2.Zero) * 14f, null,
                new Color(255, 230, 150) with { A = 0 } * (0.7f * fade), Projectile.rotation, origin,
                new Vector2(0.2f + stretch * 0.5f, 0.14f) * wob, SpriteEffects.None, 0);
            return false;
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
