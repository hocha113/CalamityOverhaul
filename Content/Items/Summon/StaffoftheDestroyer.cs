using InnoVault.GameContent.BaseEntity;
using Microsoft.Xna.Framework.Graphics;
using System.IO;
using System.Linq;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Summon
{
    internal class StaffoftheDestroyer : ModItem
    {
        public override string Texture => CWRConstant.Item_Summon + "StaffoftheDestroyer";
        public override void SetDefaults() {
            Item.width = 60;
            Item.height = 60;
            Item.damage = 36;
            Item.mana = 10;
            Item.useTime = Item.useAnimation = 10;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.noMelee = true;
            Item.knockBack = 2f;
            Item.UseSound = SoundID.Item113;
            Item.autoReuse = true;
            Item.shoot = ModContent.ProjectileType<DestroyerHead>();
            Item.shootSpeed = 10f;
            Item.DamageType = DamageClass.Summon;
            Item.rare = ItemRarityID.Purple;
            Item.value = Item.buyPrice(0, 1, 60, 5);
            Item.CWR().DeathModeItem = true;
        }

        public override bool CanUseItem(Player player) {
            float neededSlots = 4;
            float foundSlotsCount = neededSlots;

            if (Main.projectile == null || Main.projectile.Length == 0) {
                return false;
            }

            foreach (Projectile p in Main.ActiveProjectiles) {
                if (p.owner != player.whoAmI) {
                    continue;
                }
                if (p.minion) {
                    foundSlotsCount += p.minionSlots;
                }
                if (p.type == ModContent.ProjectileType<DestroyerHead>()) {
                    return false;
                }
            }

            if (foundSlotsCount > player.maxMinions) {
                return false;
            }

            return true;
        }

        public override bool? UseItem(Player player) {
            if (!player.CWR().DestroyerOwner) {
                player.AddBuff(ModContent.BuffType<DestroyerSummonBuff>(), 10086);
            }
            return base.UseItem(player);
        }

        public static void SpawnBody(Projectile head, Player player) {
            if (Main.myPlayer != player.whoAmI) {
                return;
            }

            foreach (var proj in Main.ActiveProjectiles) {
                if (proj.owner != player.whoAmI) {
                    continue;
                }
                if (proj.type == ModContent.ProjectileType<DestroyerTail>()
                    || proj.type == ModContent.ProjectileType<DestroyerBody>()) {
                    proj.active = false;
                    proj.netUpdate = true;
                }
            }

            int index = head.whoAmI;
            for (int i = 0; i <= player.maxMinions; i++) {
                int bodyID = i == player.maxMinions ? ModContent.ProjectileType<DestroyerTail>() : ModContent.ProjectileType<DestroyerBody>();
                index = Projectile.NewProjectile(head.FromObjectGetParent(), head.Center, head.velocity, bodyID, head.damage, head.knockBack
                    , player.whoAmI, ai0: Main.projectile[index].identity);
            }
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            Projectile head = Projectile.NewProjectileDirect(source, position, velocity, type, damage, knockback, player.whoAmI);
            SpawnBody(head, player);
            return false;
        }
    }

    internal class DestroyerSummonBuff : ModBuff
    {
        public override string Texture => CWRConstant.Buff + "DestroyerSummonBuff";
        public override void SetStaticDefaults() {
            Main.buffNoTimeDisplay[Type] = true;
            Main.buffNoSave[Type] = true;
        }

        public override void Update(Player player, ref int buffIndex) {
            if (player.ownedProjectileCounts[ModContent.ProjectileType<DestroyerHead>()] > 0) {
                player.CWR().DestroyerOwner = true;
            }
            if (!player.CWR().DestroyerOwner) {
                player.DelBuff(buffIndex);
                buffIndex--;
            }
            else {
                player.buffTime[buffIndex] = 10086;
            }
        }
    }

    internal class DestroyerHead : BaseHeldProj
    {
        public override string Texture => CWRConstant.Item_Summon + "DestroyerHead";
        private Vector2 offsetByIdlePos;
        private Vector2 offsetByAttackPos;
        private NPC target;
        private int oldMinionSlots;
        public override void SetStaticDefaults() {
            ProjectileID.Sets.MinionSacrificable[Projectile.type] = true;
            ProjectileID.Sets.MinionTargettingFeature[Projectile.type] = true;
            ProjectileID.Sets.NeedsUUID[Projectile.type] = true;
        }
        public override void SetDefaults() {
            Projectile.minion = true;
            Projectile.width = 40;
            Projectile.height = 40;
            Projectile.minionSlots = 4;
            offsetByIdlePos = Vector2.Zero;
            Projectile.friendly = true;
            Projectile.ignoreWater = true;
            Projectile.netImportant = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 10086;
            Projectile.alpha = 255;
            Projectile.tileCollide = false;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 30;
            Projectile.DamageType = DamageClass.Summon;
        }

        public override void Initialize() => oldMinionSlots = Owner.maxMinions;

        public override void AI() {
            if (Projectile.IsOwnedByLocalPlayer()) {
                Owner.AddBuff(ModContent.BuffType<DestroyerSummonBuff>(), 10086);
                if (Owner.dead) {
                    Owner.CWR().DestroyerOwner = false;
                }
                if (Owner.CWR().DestroyerOwner) {
                    Projectile.timeLeft = 2;
                }
            }

            //每次更改召唤上限时都重写生成一次体节
            if (oldMinionSlots != Owner.maxMinions) {
                StaffoftheDestroyer.SpawnBody(Projectile, Owner);
            }
            oldMinionSlots = Owner.maxMinions;

            if (Projectile.Distance(Owner.Center) > 2800) {
                if (!Main.dedServ) {
                    SoundEngine.PlaySound(SoundID.Item113, Owner.Center);
                    Lighting.AddLight(Owner.Center, Color.DarkRed.ToVector3() * 1.45f);
                }
                Projectile.Center = Owner.Center;
                foreach (var proj in Main.ActiveProjectiles) {
                    if (proj.owner != Owner.whoAmI) {
                        continue;
                    }
                    if (proj.type == ModContent.ProjectileType<DestroyerBody>()
                        || proj.type == ModContent.ProjectileType<DestroyerTail>()) {
                        proj.Center = Owner.Center;
                    }
                }
            }

            Projectile.rotation = Projectile.velocity.ToRotation() - MathHelper.PiOver2;

            target = Projectile.Center.FindClosestNPC(1900);
            if (target != null) {
                AttackAI();
            }
            else {
                IdleAI();
            }

            Projectile.localAI[0]++;
        }

        public override void NetHeldSend(BinaryWriter writer) {
            writer.WriteVector2(offsetByIdlePos);
            writer.WriteVector2(offsetByAttackPos);
        }

        public override void NetHeldReceive(BinaryReader reader) {
            offsetByIdlePos = reader.ReadVector2();
            offsetByAttackPos = reader.ReadVector2();
        }

        private void AttackAI() {
            if (Projectile.ai[0] == 0) {
                if (Projectile.ai[2] == 0) {
                    Projectile.velocity *= 0.97f;
                }

                Projectile.SmoothHomingBehavior(target.Center + offsetByAttackPos, 1, 0.2f);
                if (Projectile.localAI[0] % 20 == 0) {
                    if (Projectile.IsOwnedByLocalPlayer()) {
                        offsetByAttackPos = CWRUtils.randVr(target.width * 2);
                        NetUpdate();
                    }

                    Projectile.ai[2]++;
                    NetUpdate();
                }

                if (Projectile.ai[2] > 3) {
                    Projectile.ai[2] = 0;
                    Projectile.ai[0] = 1;
                    if (Projectile.IsOwnedByLocalPlayer()) {
                        offsetByAttackPos = CWRUtils.randVr(520, 600 + target.width * 2);
                        NetUpdate();
                    }

                    Projectile.velocity = Projectile.velocity.UnitVector() * 13;
                }
            }
            else if (Projectile.ai[0] == 1) {
                Projectile.SmoothHomingBehavior(target.Center + offsetByAttackPos, 1, 0.1f);
                if (Projectile.ai[2] == 100) {
                    offsetByAttackPos *= 0.1f;
                }
                if (++Projectile.ai[2] > 120) {
                    Projectile.ai[2] = 0;
                    Projectile.ai[0] = 0;
                    offsetByAttackPos = Vector2.Zero;
                    Projectile.velocity = Projectile.Center.To(target.Center + offsetByAttackPos).UnitVector() * Projectile.velocity.Length() * 2.5f;

                    if (Projectile.IsOwnedByLocalPlayer()) {
                        for (int i = 0; i < 3; i++) {
                            int proj = Projectile.NewProjectile(Projectile.FromObjectGetParent()
                                , Projectile.Center, Projectile.velocity.RotatedBy((-1 + i) * 0.12f)
                                , ProjectileID.DeathLaser, Projectile.damage, Projectile.knockBack, Projectile.owner);
                            Main.projectile[proj].friendly = true;
                            Main.projectile[proj].hostile = false;
                            Main.projectile[proj].tileCollide = false;
                            Main.projectile[proj].netUpdate = true;
                        }
                    }

                    NetUpdate();
                }
            }
        }

        private void IdleAI() {
            Projectile.SmoothHomingBehavior(Owner.Center + offsetByIdlePos, 1, 0.1f);
            if (Projectile.IsOwnedByLocalPlayer() && Projectile.localAI[0] % 20 == 0) {
                offsetByIdlePos = CWRUtils.randVr(360);
                NetUpdate();
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D value = TextureAssets.Projectile[Type].Value;
            Main.EntitySpriteDraw(value, Projectile.Center - Main.screenPosition, null, lightColor
                , Projectile.rotation, value.Size() / 2, Projectile.scale, SpriteEffects.None, 0);
            value = CWRUtils.GetT2DValue(Texture + "Glow");
            Main.EntitySpriteDraw(value, Projectile.Center - Main.screenPosition, null, Color.White
                , Projectile.rotation, value.Size() / 2, Projectile.scale, SpriteEffects.None, 0);
            return false;
        }
    }

    internal class DestroyerBody : BaseHeldProj
    {
        public override string Texture => CWRConstant.Item_Summon + "DestroyerBody";
        public override void SetStaticDefaults() {
            ProjectileID.Sets.MinionSacrificable[Projectile.type] = true;
            ProjectileID.Sets.MinionTargettingFeature[Projectile.type] = true;
            ProjectileID.Sets.NeedsUUID[Projectile.type] = true;
        }
        public override void SetDefaults() {
            Projectile.minion = true;
            Projectile.width = 36;
            Projectile.height = 36;
            Projectile.minionSlots = 0;
            Projectile.friendly = true;
            Projectile.ignoreWater = true;
            Projectile.netImportant = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 10086;
            Projectile.alpha = 255;
            Projectile.tileCollide = false;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 30;
            Projectile.DamageType = DamageClass.Summon;
        }
        //我他妈也不知道为什么要这么写
        private bool FindaheadSegmentByIdentity(Projectile proj) => proj.owner == Projectile.owner && (proj.projUUID == (int)Projectile.ai[0] || proj.identity == (int)Projectile.ai[0]);
        public override void AI() {
            Projectile aheadSegment = Main.projectile.Take(Main.maxProjectiles).FirstOrDefault(FindaheadSegmentByIdentity);
            if (!aheadSegment.Alives() || (aheadSegment.type != Type && aheadSegment.type != ModContent.ProjectileType<DestroyerHead>())) {
                Projectile.Kill();
                return;
            }
            if (Owner.dead) {
                Owner.CWR().DestroyerOwner = false;
            }
            if (Owner.CWR().DestroyerOwner) {
                Projectile.timeLeft = 2;
            }
            Vector2 directionToNextSegment = aheadSegment.Center - Projectile.Center;
            directionToNextSegment = directionToNextSegment.RotatedBy(MathHelper.WrapAngle(aheadSegment.rotation - Projectile.rotation) * 0.08f);
            directionToNextSegment = directionToNextSegment.MoveTowards((aheadSegment.rotation - Projectile.rotation).ToRotationVector2(), 1f);
            Projectile.velocity = Vector2.Zero;
            Projectile.rotation = directionToNextSegment.ToRotation() - MathHelper.PiOver2;
            Projectile.Center = aheadSegment.Center - directionToNextSegment.UnitVector() * Projectile.scale * Projectile.width;
            Projectile.spriteDirection = (directionToNextSegment.X > 0).ToDirectionInt();
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D value = TextureAssets.Projectile[Type].Value;
            Main.EntitySpriteDraw(value, Projectile.Center - Main.screenPosition, null, lightColor
                , Projectile.rotation, value.Size() / 2, Projectile.scale, SpriteEffects.None, 0);
            value = CWRUtils.GetT2DValue(Texture + "Glow");
            Main.EntitySpriteDraw(value, Projectile.Center - Main.screenPosition, null, Color.White
                , Projectile.rotation, value.Size() / 2, Projectile.scale, SpriteEffects.None, 0);
            return false;
        }
    }

    internal class DestroyerTail : DestroyerBody
    {
        public override string Texture => CWRConstant.Item_Summon + "DestroyerTail";
    }
}
