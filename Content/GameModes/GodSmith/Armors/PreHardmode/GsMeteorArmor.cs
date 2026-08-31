using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Armors.PreHardmode
{
    /// <summary>
    /// 【神赋·盔甲】陨石套「天外余烬」（A 档）：材质=烧穿大气的陨铁。<br/>
    /// ①命中积攒星火 ②满 7 层后下一击自高空左右各呼落一枚陨铁碎片，坠体逐帧增速砸向目标
    /// ③碎片头部白热压缩、尾部焦黑拖长，甩火星陨尘拖烟，命中点燃并小爆
    /// ④受击崩落 2 层星火，陨铁屑四溅。<br/>
    /// 原版套装奖励（太空枪免蓝）保留，神赋是叠加层；星火层数是攻击方端本地量，
    /// 满火余烬只对佩戴者自己可见（个人读数），跨端可见的部分是碎片实体
    /// </summary>
    internal class GsMeteorArmor : GodSmithArmorScheme
    {
        public override string GsFamily => "ArmorsA";

        public override int[] HeadIDs => [ItemID.MeteorHelmet];

        public override int BodyID => ItemID.MeteorSuit;

        public override int LegsID => ItemID.MeteorLeggings;

        protected override string EndowLineFallback =>
            "Stray Embers: strikes build starfire; at 7 stacks the next strike calls two burning meteor shards down on your foe";

        //陨铁色板
        internal static readonly Color CharShell = new(52, 40, 40);
        internal static readonly Color MoltenOrange = new(255, 120, 40);
        internal static readonly Color WhiteHot = new(255, 230, 180);

        /// <summary>呼落碎片所需星火层数</summary>
        private const int FullCharge = 7;

        public override void UpdateEndowment(Player player, GodSmithArmorPlayer state) {
            if (VaultUtils.isServer || state.EndowCharge < FullCharge) {
                return;
            }
            //满火态：余烬绕身升腾（个人读数，层数只在攻击方端存在）
            Lighting.AddLight(player.Center, MoltenOrange.ToVector3() * 0.22f);
            if (Main.rand.NextBool(8)) {
                PRTLoader.NewParticle<PRT_Spark>(player.Center + Main.rand.NextVector2CircularEdge(16f, 24f),
                    new Vector2(Main.rand.NextFloat(-0.3f, 0.3f), -Main.rand.NextFloat(0.5f, 1.2f)),
                    Main.rand.NextBool() ? MoltenOrange : WhiteHot, Main.rand.NextFloat(0.2f, 0.35f))
                    ?.Configure(false, Main.rand.Next(12, 20));
            }
        }

        public override void OnEndowHitNPC(Player player, GodSmithArmorPlayer state, NPC target,
            in NPC.HitInfo hit, int damageDone, Projectile sourceProj) {
            //陨铁碎片自身命中不喂层，防自循环
            if (sourceProj != null && sourceProj.type == ModContent.ProjectileType<GsMeteorShardProj>()) {
                return;
            }
            if (target.type == NPCID.TargetDummy) {
                return;
            }

            if (state.EndowCharge < FullCharge) {
                state.EndowCharge++;
                return;
            }

            //呼落：满火后这一击自高空左右各召一枚陨铁碎片
            state.EndowCharge = 0;
            if (!VaultUtils.isServer) {
                //目标处先起一点引火光，坠击的因果预告
                PRTLoader.NewParticle<PRT_Light>(target.Center, Vector2.Zero, MoltenOrange, 0.16f)?.Configure(12, 0.7f);
            }
            //proc 弹幕 owner 侧生成；每枚伤害按触发伤害 30% 折算并封顶
            if (player.whoAmI == Main.myPlayer) {
                int shardDamage = Math.Clamp((int)(damageDone * 0.30f), 10, 130);
                for (int side = -1; side <= 1; side += 2) {
                    //生成前探顶棚收缩高度，碎片带标的线（ai[1]）越线恢复碰撞，洞内不再穿岩
                    Vector2 spawn = GsArmorTerrainProbe.SkySpawnAbove(target.Center,
                        side * Main.rand.NextFloat(140f, 220f), 420f);
                    Vector2 aim = target.Center + Main.rand.NextVector2Circular(24f, 24f);
                    Projectile.NewProjectile(player.GetSource_Misc("GodSmithMeteorEndow"),
                        spawn, (aim - spawn).SafeNormalize(Vector2.UnitY) * 15f,
                        ModContent.ProjectileType<GsMeteorShardProj>(), shardDamage, 3f, player.whoAmI,
                        0f, target.Center.Y);
                }
            }
        }

        public override void OnEndowHurt(Player player, GodSmithArmorPlayer state, in Player.HurtInfo info) {
            //受击崩落两层星火，陨铁屑四溅
            if (state.EndowCharge <= 0) {
                return;
            }
            state.EndowCharge = Math.Max(0, state.EndowCharge - 2);
            if (!VaultUtils.isServer) {
                for (int i = 0; i < 4; i++) {
                    Dust d = Dust.NewDustPerfect(player.Center + Main.rand.NextVector2Circular(10f, 16f),
                        DustID.Meteorite, new Vector2(Main.rand.NextFloat(-2f, 2f), Main.rand.NextFloat(0.5f, 2.5f)));
                    d.noGravity = false;
                }
            }
        }
    }

    /// <summary>
    /// 陨铁碎片：一块正在烧穿大气的陨铁，不是匀速光弹。坠体每帧 ×1.03 增速（上限 22）；
    /// 出生免地形碰撞、越过标的线（ai[1]）才恢复（Stardust 式高度门，脱靶后砸地收场不穿岩）；
    /// 三层强速度拉伸叠色（焦黑壳尾部拖长/熔橙主体/白热芯头部压缩）+ 余烬残影渐暗，
    /// 沿途甩火星陨尘拖烟，命中点燃并小爆（脉冲环 + 火星扇 + 低音爆响）
    /// </summary>
    internal class GsMeteorShardProj : ModProjectile
    {
        public override string Texture => CWRConstant.Masking + "Extra_98";

        private ref float Life => ref Projectile.ai[0];

        /// <summary>标的高度线：低于此线才恢复地形碰撞</summary>
        private ref float TargetLineY => ref Projectile.ai[1];

        /// <summary>确定性抖动相位，绘制路径不掷 Main.rand</summary>
        private float Seed => Projectile.identity * 0.7391f % 3.71f;

        /// <summary>出生 4 帧淡入，防第一帧硬弹出</summary>
        private float VisualFade => MathHelper.Clamp(Life / 4f, 0f, 1f);

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 8;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Generic;
            Projectile.penetrate = 2;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
            Projectile.timeLeft = 70;
            //出生免碰撞，越过标的线由高度门恢复（见 AI），脱靶砸地收场
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
        }

        public override void AI() {
            Life++;
            //高度门：越过标的线才恢复地形碰撞（标的线为 0 的裸生成立即恢复，普通坠落）
            GsArmorTerrainProbe.UpdateFallGate(Projectile, TargetLineY);

            //坠体增速不匀速：每帧 ×1.03，上限 22
            if (Projectile.velocity.Length() < 22f) {
                Projectile.velocity *= 1.03f;
            }
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

            //飞行相：每 2 帧甩火星陨尘，偶发小烟团拖出燃烧尾迹
            if (!Main.dedServ) {
                if (Life % 2 == 0) {
                    Dust d = Dust.NewDustPerfect(Projectile.Center - Projectile.velocity * 0.4f,
                        Main.rand.NextBool() ? DustID.Torch : DustID.Meteorite,
                        -Projectile.velocity * 0.1f + Main.rand.NextVector2Circular(0.8f, 0.8f),
                        100, default, Main.rand.NextFloat(0.9f, 1.4f));
                    d.noGravity = true;
                }
                if (Main.rand.NextBool(9)) {
                    //PRT_Smoke 是加色批，烟团用暗橙余烬色才可见（纯焦黑加色等于不画）
                    PRTLoader.NewParticle<PRT_Smoke>(Projectile.Center - Projectile.velocity * 0.6f,
                        -Projectile.velocity * 0.05f, GsMeteorArmor.MoltenOrange, Main.rand.NextFloat(0.14f, 0.22f))
                        ?.Configure(Main.rand.Next(18, 28), 0.25f, 0.02f);
                }
            }
            Lighting.AddLight(Projectile.Center, GsMeteorArmor.MoltenOrange.ToVector3() * (0.4f * VisualFade));
        }

        /// <summary>命中与消亡共用的小爆：脉冲环 + 火星扇 + 低音爆响</summary>
        private void Blast() {
            SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.45f, Pitch = -0.3f, MaxInstances = 3 }, Projectile.Center);
            PRTLoader.NewParticle<PRT_StarPulseRing>(Projectile.Center, Vector2.Zero, GsMeteorArmor.MoltenOrange, 0.5f)
                ?.Configure(0.15f, 1.1f, 14);
            for (int i = 0; i < 7; i++) {
                PRTLoader.NewParticle<PRT_Spark>(Projectile.Center,
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(2f, 6f),
                    Main.rand.NextBool() ? GsMeteorArmor.WhiteHot : GsMeteorArmor.MoltenOrange,
                    Main.rand.NextFloat(0.3f, 0.55f))?.Configure(true, Main.rand.Next(16, 26));
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.OnFire, 180);
            //末次穿透由 OnKill 收尾，避免同帧双爆
            if (!Main.dedServ && Projectile.penetrate > 1) {
                Blast();
            }
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            Blast();
            //余痕：陨尘余烬回落，比碎片活得久
            for (int i = 0; i < 5; i++) {
                Dust d = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(8f, 8f),
                    DustID.Meteorite, Main.rand.NextVector2Unit() * Main.rand.NextFloat(1f, 3f));
                d.noGravity = false;
            }
        }

        //==================== 绘制：三层强速度拉伸 + 余烬残影渐暗 ====================

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = CWRAsset.Extra_98?.Value;
            if (tex == null) {
                return false;
            }
            float fade = VisualFade;
            Vector2 origin = tex.Size() * 0.5f;
            Vector2 velNorm = Projectile.velocity.SafeNormalize(Vector2.UnitY);
            float rotation = Projectile.rotation;
            //坠体越快拉得越长
            float stretch = MathHelper.Clamp(Projectile.velocity.Length() * 0.045f, 0.3f, 1.1f);
            //燃烧失稳的确定性抖动
            float wob = 1f + MathF.Sin(Life * 0.8f + Seed * 6f) * 0.07f;

            //余烬残影：旧位置的熔橙渐暗渐缩
            for (int i = Projectile.oldPos.Length - 1; i >= 1; i--) {
                if (Projectile.oldPos[i] == Vector2.Zero) {
                    continue;
                }
                float ghost = (1f - i / (float)Projectile.oldPos.Length) * 0.30f * fade;
                Vector2 gpos = Projectile.oldPos[i] + Projectile.Size * 0.5f - Main.screenPosition;
                Main.EntitySpriteDraw(tex, gpos, null, (GsMeteorArmor.MoltenOrange with { A = 0 }) * ghost,
                    Projectile.oldRot[i], origin, new Vector2(0.16f, 0.22f + stretch * 0.3f) * (1f - i * 0.06f),
                    SpriteEffects.None, 0);
            }

            Vector2 posDraw = Projectile.Center - Main.screenPosition;
            //焦黑壳：偏向尾部拖长（真 alpha 贴图正常叠色压暗）
            Main.EntitySpriteDraw(tex, posDraw - velNorm * 6f, null, GsMeteorArmor.CharShell * (0.9f * fade),
                rotation, origin, new Vector2(0.30f * wob, 0.34f + stretch * 0.9f), SpriteEffects.None, 0);
            //熔橙主体
            Main.EntitySpriteDraw(tex, posDraw, null, (GsMeteorArmor.MoltenOrange with { A = 0 }) * fade,
                rotation, origin, new Vector2(0.24f * wob, 0.28f + stretch * 0.6f), SpriteEffects.None, 0);
            //白热芯：偏向头部压缩
            Main.EntitySpriteDraw(tex, posDraw + velNorm * 5f, null, (GsMeteorArmor.WhiteHot with { A = 0 }) * (0.75f * fade),
                rotation, origin, new Vector2(0.14f * wob, 0.16f + stretch * 0.2f), SpriteEffects.None, 0);
            return false;
        }
    }
}
