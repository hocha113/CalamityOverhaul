using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Melee;
using CalamityOverhaul.Content.Items.Placeable;
using Microsoft.Xna.Framework;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee.MurasamaProj
{
    internal class EndSkillEffectStart : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;

        public const int CanDamageTime = 50;
        public const int CanDamageInNPCCountNum = 30;

        public Player player => Main.player[Projectile.owner];

        private ref float Time => ref Projectile.ai[0];

        /// <summary>
        /// 在场的符合条件的NPC是否小于<see cref="CanDamageInNPCCountNum"/>，如果是，返回<see langword="true"/>
        /// </summary>
        /// <returns></returns>
        public static bool CanDealDamageToNPCs() {
            int count = 0;
            foreach (NPC n in Main.npc) {
                if (!n.active || n.friendly) {
                    continue;
                }
                count++;
            }
            return count <= CanDamageInNPCCountNum;
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 32;
            Projectile.timeLeft = 120;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = false;
        }

        public override bool? CanDamage() => false;

        private struct EntityStart
        {
            public Vector2 origPos;
            public float rot;
        }

        private Dictionary<Projectile, EntityStart> ProjDic = new Dictionary<Projectile, EntityStart>();
        private Dictionary<NPC, EntityStart> NPCDic = new Dictionary<NPC, EntityStart>();

        public Vector2 OrigPos {
            get => new Vector2(Projectile.ai[1], Projectile.ai[2]);
            set {
                Projectile.ai[1] = value.X;
                Projectile.ai[2] = value.Y;
            }
        }

        public override void AI() {
            Main.LocalPlayer.CWR().EndSkillEffectStartBool = true;
            Projectile.Center = Main.player[Projectile.owner].Center;

            foreach (Projectile p in Main.projectile) {
                if (!ProjDic.ContainsKey(p) && !p.friendly) {
                    ProjDic.Add(p, new EntityStart { origPos = p.position, rot = p.rotation });
                }
            }
            foreach (NPC n in Main.npc) {
                if (!NPCDic.ContainsKey(n) && !n.friendly) {
                    NPCDic.Add(n, new EntityStart { origPos = n.position, rot = n.rotation });
                }
            }

            foreach (Projectile p in ProjDic.Keys) {
                if (p.Alives()) {
                    p.rotation = ProjDic[p].rot;
                    p.position = ProjDic[p].origPos;
                }
                else {
                    ProjDic.Remove(p);
                }
            }
            foreach (NPC n in NPCDic.Keys) {
                if (n.Alives()) {
                    n.rotation = NPCDic[n].rot;
                    n.position = NPCDic[n].origPos;
                }
                else {
                    NPCDic.Remove(n);
                }
            }

            if (Time == CanDamageTime) {
                if (!CanDealDamageToNPCs() && Projectile.IsOwnedByLocalPlayer()) {
                    Projectile.NewProjectile(Projectile.parent(), OrigPos, Vector2.Zero
                        , ModContent.ProjectileType<EndSkillMakeDamage>(), Projectile.damage, 0, Projectile.owner);
                }
            }
            Time++;
        }

        public override void OnKill(int timeLeft) {
            if (Murasama.NameIsVergil(player)) {
                SoundStyle[] sounds = new SoundStyle[] { CWRSound.V_YouSouDiad , CWRSound.V_ThisThePwero , CWRSound.V_You_Wo_Namges_Is_The_Pwero };
                SoundEngine.PlaySound(sounds[Main.rand.Next(sounds.Length)]);
                if (Main.rand.NextBool(13)) {
                    player.QuickSpawnItem(player.parent(), ModContent.ItemType<FoodStallChair>());
                }
                Projectile.NewProjectile(Projectile.parent(), Projectile.Center, Vector2.Zero, ModContent.ProjectileType<PowerSoundEgg>(), 0, 0, Projectile.owner);
            }
        }
    }
}
