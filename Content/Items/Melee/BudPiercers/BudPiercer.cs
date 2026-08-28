using CalamityOverhaul.Content.NPCs.BloomsandSerpents;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee.BudPiercers
{
    /// <summary>
    /// 蕾锋，投掷飞匕。命中敌人时种下花蕾，约一秒后绽放一圈花瓣；
    /// 未命中而插进地面时就地开花，向上喷出三根荒针。
    /// 与 <see cref="SandDagger"/> 的沙地地脉定位错开：这把讲开花
    /// </summary>
    internal class BudPiercer : BssModItem
    {
        public override string Texture => CWRConstant.Item_Melee + "BudPiercer";

        public override void SetDefaults() {
            Item.width = 20;
            Item.height = 48;
            Item.damage = 17;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.useAnimation = 17;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.useTime = 17;
            Item.knockBack = 3.5f;
            Item.UseSound = SoundID.Item1 with { Pitch = 0.1f };
            Item.autoReuse = true;
            Item.rare = ItemRarityID.Green;
            Item.value = Item.sellPrice(0, 1, 20);
            Item.shoot = ModContent.ProjectileType<BudPiercerThrow>();
            Item.shootSpeed = 16.5f;
            Item.DamageType = DamageClass.Melee;
        }
    }

    /// <summary>
    /// 蕾锋实体：翻滚贴图连续自旋，直飞一小段后下坠。
    /// 命中敌人种蕾（<see cref="BudSeedling"/>），插地喷针
    /// </summary>
    internal class BudPiercerThrow : BssModProjectile
    {
        public override string Texture => CWRConstant.Projectile_Melee + "BudPiercerThrow";

        /// <summary>飞行计时</summary>
        private ref float FlyTimer => ref Projectile.ai[0];

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 5;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 1;
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 12;
            Projectile.friendly = true;
            Projectile.tileCollide = true;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 240;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override void AI() {
            //翻滚：素材本身是带碎屑的中滚帧，连续自旋读作飞行
            Projectile.rotation += 0.34f * (Projectile.velocity.X >= 0f ? 1f : -1f);

            if (++FlyTimer > 18f) {
                Projectile.velocity.Y = MathHelper.Clamp(Projectile.velocity.Y + 0.3f, -20f, 14f);
                Projectile.velocity.X *= 0.99f;
            }

            if (!Main.dedServ && Main.rand.NextBool(8)) {
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.JunglePlants,
                    -Projectile.velocity * 0.04f, 150, default, 0.6f);
                d.noGravity = true;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //在敌人身上种蕾，延时绽放（伤害在生成时折算给花瓣圈）
            if (Projectile.IsOwnedByLocalPlayer()) {
                Projectile.NewProjectile(Projectile.GetSource_FromAI(), target.Center, Vector2.Zero,
                    ModContent.ProjectileType<BudSeedling>(), (int)(Projectile.damage * 0.55f),
                    0f, Projectile.owner, ai0: target.whoAmI);
            }
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            //插地就地开花：向上扇形喷三根荒针
            Collision.HitTiles(Projectile.position, Projectile.velocity, Projectile.width, Projectile.height);
            SoundEngine.PlaySound(SoundID.Grass with { Pitch = -0.15f }, Projectile.position);
            for (int i = -1; i <= 1; i++) {
                Vector2 vel = new Vector2(0f, -6.8f).RotatedBy(i * 0.42f) * Main.rand.NextFloat(0.9f, 1.1f);
                BloomArsenal.ShedNeedle(Projectile, Projectile.Center, vel,
                    (int)(Projectile.damage * 0.5f), 0f, gravity: true);
            }
            if (!Main.dedServ) {
                for (int i = 0; i < 5; i++) {
                    BssVfx.PetalDrift(Projectile.Center, Main.rand.NextVector2Circular(1.2f, 0.8f), 0.6f);
                }
            }
            return true;
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;
            Vector2 origin = texture.Size() / 2f;

            //飞行残影
            for (int k = 0; k < Projectile.oldPos.Length; k++) {
                Vector2 drawPos = Projectile.oldPos[k] - Main.screenPosition + Projectile.Size / 2f;
                Color color = lightColor * ((Projectile.oldPos.Length - k) / (float)Projectile.oldPos.Length / 2.2f);
                Main.EntitySpriteDraw(texture, drawPos, null, color, Projectile.rotation,
                    origin, Projectile.scale, SpriteEffects.None, 0);
            }

            Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, null, lightColor,
                Projectile.rotation, origin, Projectile.scale, SpriteEffects.None, 0);
            return false;
        }
    }

    /// <summary>
    /// 种下的花蕾：贴在敌人身上鼓动，到时绽放一圈花瓣；宿主消失则就地绽放。
    /// ai[0]=宿主 NPC 下标。本体不结算伤害，伤害由花瓣圈携带
    /// </summary>
    internal class BudSeedling : BssModProjectile
    {
        public override string Texture => CWRConstant.NPC + "BSS/BloomBud";

        private const int BloomDelay = 48;

        private ref float HostIndex => ref Projectile.ai[0];

        /// <summary>贴附点在宿主身上的随机偏移，各端由 identity 推出同一值</summary>
        private Vector2 HostOffset {
            get {
                int seed = Projectile.identity;
                float x = (seed * 73 % 17 - 8) * 1.6f;
                float y = (seed * 31 % 13 - 6) * 1.4f;
                return new Vector2(x, y);
            }
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 16;
            Projectile.friendly = true;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = BloomDelay;
            Projectile.DamageType = DamageClass.Melee;
        }

        public override bool? CanDamage() => false;

        public override void AI() {
            int idx = (int)HostIndex;
            if (idx >= 0 && idx < Main.maxNPCs) {
                NPC host = Main.npc[idx];
                if (host.active && !host.friendly) {
                    Projectile.Center = host.Center + HostOffset;
                }
                else {
                    //宿主没了就地绽放
                    Projectile.Kill();
                    return;
                }
            }

            //鼓动：越接近绽放鼓得越急
            float t = 1f - Projectile.timeLeft / (float)BloomDelay;
            Projectile.scale = 0.7f + 0.12f * MathF.Sin(t * t * 26f);

            if (!Main.dedServ && Main.rand.NextBool(9)) {
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.RedTorch, Vector2.Zero, 150, default, 0.7f);
                d.noGravity = true;
            }
        }

        public override void OnKill(int timeLeft) {
            SoundEngine.PlaySound(SoundID.Grass with { Pitch = 0.35f, Volume = 0.8f }, Projectile.Center);
            BloomArsenal.PetalRing(Projectile, Projectile.Center, 6, Projectile.damage, 0f);
            if (!Main.dedServ) {
                for (int i = 0; i < 4; i++) {
                    BssVfx.PetalDrift(Projectile.Center, Main.rand.NextVector2Circular(1f, 0.7f), 0.7f);
                }
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, null, lightColor,
                Projectile.rotation, tex.Size() / 2f, Projectile.scale, SpriteEffects.None, 0);
            return false;
        }
    }
}
