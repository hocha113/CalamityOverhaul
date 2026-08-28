using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Armors.Endgame
{
    /// <summary>
    /// 【神赋·星云套】「三相共鸣」：星云之华（缓旋绽放的玫瑰星云瓣团）。
    /// ①任意一种星云增益升至满级的一瞬绽放星云之华；②华体漂向敌群，三次脉冲
    /// 灼烧周围敌人，瓣径随脉冲呼吸；③谢幕时瓣屑化作星尘飘回佩戴者身边。
    /// 另有三重奏读数：三种增益俱在时三色微光绕身缓旋。<br/>
    /// 与原版套装技联动而非覆盖：原版增幅拾取与三档增益全数照常，神赋只监听
    /// 等级升满的上升沿；沿检测在各端各自观察（增益随玩家同步），华体 owner 侧生成
    /// </summary>
    internal class GsNebulaArmor : GodSmithArmorScheme
    {
        public override string GsFamily => "ArmorsC";

        public override int[] HeadIDs => [ItemID.NebulaHelmet];

        public override int BodyID => ItemID.NebulaBreastplate;

        public override int LegsID => ItemID.NebulaLeggings;

        protected override string EndowLineFallback =>
            "Triune Resonance: the moment any nebula booster reaches full level, a nebula bloom bursts forth, pulsing three searing waves before its petals drift back to you";

        //玫瑰星云色板
        internal static readonly Color NebulaBright = new(255, 214, 240);
        internal static readonly Color NebulaMain = new(218, 108, 222);
        internal static readonly Color NebulaDeep = new(72, 24, 84);
        //三重奏读数的三色（增伤粉红/回生翠绿/魔力蓝紫，对应原版三种增幅）
        private static readonly Color triadDamage = new(255, 120, 170);
        private static readonly Color triadLife = new(120, 235, 150);
        private static readonly Color triadMana = new(130, 140, 255);

        /// <summary>两次绽放之间的最短间隔帧数</summary>
        private const int BloomCooldown = 90;

        public override void UpdateEndowment(Player player, GodSmithArmorPlayer state) {
            var reg = player.GetModPlayer<GsNebulaArmorPlayer>();
            int dmg = player.nebulaLevelDamage;
            int life = player.nebulaLevelLife;
            int mana = player.nebulaLevelMana;

            //满级上升沿：任意一相攒满即共鸣绽放（带冷却防三相同帧连炸）
            bool maxedEdge = (dmg >= 3 && reg.PrevDamage < 3)
                || (life >= 3 && reg.PrevLife < 3)
                || (mana >= 3 && reg.PrevMana < 3);
            if (maxedEdge && Main.GameUpdateCount - reg.LastBloomTick > BloomCooldown) {
                reg.LastBloomTick = Main.GameUpdateCount;
                Bloom(player);
            }
            reg.PrevDamage = dmg;
            reg.PrevLife = life;
            reg.PrevMana = mana;

            //三重奏读数：三相俱在时三色微光绕身缓旋（确定性角度，不掷 rand）
            if (VaultUtils.isServer || dmg < 1 || life < 1 || mana < 1) {
                return;
            }
            Lighting.AddLight(player.Center, NebulaMain.ToVector3() * 0.14f);
            if (Main.GameUpdateCount % 5 == 0) {
                float baseAng = Main.GameUpdateCount * 0.035f;
                Span<Color> triad = [triadDamage, triadLife, triadMana];
                for (int i = 0; i < 3; i++) {
                    Vector2 at = player.Center + (baseAng + MathHelper.TwoPi * i / 3f).ToRotationVector2() * 28f;
                    PRTLoader.NewParticle<PRT_Light>(at, Vector2.Zero, triad[i], 0.075f)?.Configure(7, 0.75f);
                }
            }
        }

        private static void Bloom(Player player) {
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.8f, Pitch = -0.1f }, player.Center);
                for (int i = 0; i < 8; i++) {
                    float ang = MathHelper.TwoPi * i / 8f;
                    PRTLoader.NewParticle<PRT_Spark>(player.Center + ang.ToRotationVector2() * 12f,
                        ang.ToRotationVector2() * Main.rand.NextFloat(2f, 4f),
                        Main.rand.NextBool() ? NebulaBright : NebulaMain,
                        Main.rand.NextFloat(0.3f, 0.5f))?.Configure(false, Main.rand.Next(14, 22));
                }
            }
            if (player.whoAmI != Main.myPlayer) {
                return;
            }
            //华体逐跳伤害走魔法面板固定预算；触发要靠增幅拾取攒满一相，收益在神赋包络内
            int bloomDamage = (int)player.GetTotalDamage(DamageClass.Magic).ApplyTo(150f);
            Projectile.NewProjectile(player.GetSource_Misc("GodSmithNebulaEndow"),
                player.Center + new Vector2(0f, -24f), new Vector2(0f, -1.5f),
                ModContent.ProjectileType<GsNebulaBloomProj>(), bloomDamage, 2f, player.whoAmI);
        }
    }

    /// <summary>
    /// 三相共鸣的沿检测寄存器：每玩家一份的上一帧增益等级 + 绽放冷却时刻；
    /// 纯本地量，不进存档不联网
    /// </summary>
    internal class GsNebulaArmorPlayer : ModPlayer
    {
        internal int PrevDamage;
        internal int PrevLife;
        internal int PrevMana;
        internal uint LastBloomTick;
    }

    /// <summary>
    /// 星云之华：一团缓旋绽放的玫瑰星云，不是光球。漂向敌群，在第 20/45/70 帧
    /// 三次脉冲（判定域随瓣径开合），脉冲时瓣环外扩；谢幕时瓣屑化作星尘
    /// 飘回佩戴者身边，余尘比华体活得久
    /// </summary>
    internal class GsNebulaBloomProj : ModProjectile
    {
        public override string Texture => CWRConstant.Masking + "Extra_98";

        /// <summary>三次脉冲的时刻表</summary>
        private static readonly int[] pulseTicks = [20, 45, 70];

        private ref float Life => ref Projectile.ai[0];

        private float Seed => Projectile.identity * 0.7333f % 3.41f;

        private float VisualFade => MathHelper.Clamp(Life / 6f, 0f, 1f) * (1f - MathHelper.Clamp((Life - 82f) / 8f, 0f, 1f));

        /// <summary>距最近脉冲时刻的相位（0=正脉冲），驱动瓣径呼吸</summary>
        private float PulsePhase {
            get {
                float best = 99f;
                for (int i = 0; i < pulseTicks.Length; i++) {
                    best = MathF.Min(best, MathF.Abs(Life - pulseTicks[i]));
                }
                return MathHelper.Clamp(1f - best / 10f, 0f, 1f);
            }
        }

        public override void SetDefaults() {
            Projectile.width = 40;
            Projectile.height = 40;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Generic;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 90;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 24;
        }

        public override void AI() {
            Life++;
            Projectile.rotation += 0.045f;

            //漂移：缓缓飘向最近敌人，无敌则悬停轻晃（非匀速）
            NPC target = FindNearest(430f);
            if (target != null) {
                Vector2 want = (target.Center - Projectile.Center).SafeNormalize(Vector2.Zero) * 2.6f;
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, want, 0.045f);
            }
            else {
                Projectile.velocity *= 0.95f;
                Projectile.velocity.Y += MathF.Sin(Life * 0.12f + Seed) * 0.02f;
            }

            //脉冲时刻：判定域外扩一拍再收拢（Resize 由 Life 驱动，各端一致）
            bool pulsing = false;
            for (int i = 0; i < pulseTicks.Length; i++) {
                if (Life >= pulseTicks[i] && Life < pulseTicks[i] + 6) {
                    pulsing = true;
                    break;
                }
            }
            int wantSize = pulsing ? 170 : 40;
            if (Projectile.width != wantSize) {
                Projectile.Resize(wantSize, wantSize);
            }
            //脉冲一瞬的瓣环外扩演出
            if (!Main.dedServ && pulsing && Projectile.width == 170 && Life % 6 < 1) {
                SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.5f, Pitch = 0.3f, MaxInstances = 3 }, Projectile.Center);
                for (int i = 0; i < 10; i++) {
                    float ang = MathHelper.TwoPi * i / 10f + Seed;
                    PRTLoader.NewParticle<PRT_Spark>(Projectile.Center + ang.ToRotationVector2() * 20f,
                        ang.ToRotationVector2() * Main.rand.NextFloat(3f, 5f),
                        Main.rand.NextBool() ? GsNebulaArmor.NebulaBright : GsNebulaArmor.NebulaMain,
                        Main.rand.NextFloat(0.3f, 0.5f))?.Configure(false, Main.rand.Next(14, 22));
                }
            }

            //驻场相：瓣缘星尘缓释
            if (!Main.dedServ && Life % 4 == 0) {
                float ang = Main.rand.NextFloat(MathHelper.TwoPi);
                PRTLoader.NewParticle<PRT_Light>(Projectile.Center + ang.ToRotationVector2() * 24f,
                    ang.ToRotationVector2() * 0.4f, GsNebulaArmor.NebulaMain,
                    Main.rand.NextFloat(0.06f, 0.1f))?.Configure(12, 0.7f);
            }
            Lighting.AddLight(Projectile.Center, GsNebulaArmor.NebulaMain.ToVector3() * (0.38f * VisualFade));
        }

        private NPC FindNearest(float range) {
            NPC best = null;
            float bestDist = range;
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
            //谢幕：瓣屑化星尘飘回佩戴者身边（初速朝人，余生自然衰减）
            SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.4f, Pitch = 0.55f }, Projectile.Center);
            Player owner = Main.player[Projectile.owner];
            Vector2 toOwner = owner.active
                ? (owner.Center - Projectile.Center).SafeNormalize(Vector2.Zero)
                : Vector2.UnitY * -1f;
            for (int i = 0; i < 9; i++) {
                PRTLoader.NewParticle<PRT_Spark>(Projectile.Center + Main.rand.NextVector2Circular(18f, 18f),
                    toOwner * Main.rand.NextFloat(2f, 4.5f) + Main.rand.NextVector2Circular(0.8f, 0.8f),
                    Main.rand.NextBool() ? GsNebulaArmor.NebulaBright : GsNebulaArmor.NebulaMain,
                    Main.rand.NextFloat(0.24f, 0.42f))?.Configure(false, Main.rand.Next(20, 34));
            }
        }

        //==================== 绘制：三层星云瓣团 + 脉冲呼吸 + 慢旋涂抹 ====================

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = CWRAsset.Extra_98?.Value;
            if (tex == null) {
                return false;
            }
            float fade = VisualFade;
            //瓣径呼吸：脉冲相位驱动，速度再添一点拉伸
            float breath = 1f + PulsePhase * 0.55f;
            float stretch = MathHelper.Clamp(Projectile.velocity.Length() * 0.03f, 0f, 0.2f);
            Vector2 pos = Projectile.Center - Main.screenPosition;
            Vector2 origin = tex.Size() * 0.5f;
            float wob = MathF.Sin(Life * 0.4f + Seed * 5f) * 0.08f;

            //暗菫压边
            Main.EntitySpriteDraw(tex, pos, null, GsNebulaArmor.NebulaDeep * (0.85f * fade), Projectile.rotation, origin,
                new Vector2(0.5f + wob + stretch, 0.46f - wob) * breath, SpriteEffects.None, 0);
            //玫瑰星云主体：双层反向慢旋，转出云团层理
            Main.EntitySpriteDraw(tex, pos, null, GsNebulaArmor.NebulaMain * (0.85f * fade), Projectile.rotation * 1.3f, origin,
                new Vector2(0.40f, 0.34f + wob) * breath, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(tex, pos, null, GsNebulaArmor.NebulaMain * (0.5f * fade), -Projectile.rotation, origin,
                new Vector2(0.34f + wob, 0.28f) * breath, SpriteEffects.None, 0);
            //白粉亮芯：加色三瓣旋纹
            Color core = GsNebulaArmor.NebulaBright with { A = 0 };
            for (int i = 0; i < 3; i++) {
                float ang = Projectile.rotation * 1.6f + MathHelper.TwoPi * i / 3f;
                Main.EntitySpriteDraw(tex, pos, null, core * (0.5f * fade), ang, origin,
                    new Vector2(0.26f, 0.07f) * breath, SpriteEffects.None, 0);
            }
            return false;
        }
    }
}
