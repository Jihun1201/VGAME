// 파일명: CombatSystem.cs
using System;
using System.Collections.Generic;
using GameCore;
using EntityGroup;
using WeaponData;

namespace CombatSystem
{
    public class Projectile
    {
        public Vector2 Position; public Vector2 Velocity; public float Damage; public float Lifetime; public float Timer = 0f; public bool IsActive = true;
        public bool IsPiercing = false; public int PierceCount = 1;
    }

    public class DamageText
    {
        public Vector2 Position; public float Damage; public float Lifetime = 0.5f; public float Timer = 0f;
        public void Update(float dt) { Timer += dt; Position.Y -= 30f * dt; }
    }

    public class Weapon
    {
        // 장신구 패시브 스탯
        public int AccProjectileBonus = 0; public float AccAreaMult = 1f; public float AccDamageMult = 1f;

        // 지팡이
        public bool HasStaff = false; public float StaffDamage = 10f; public float StaffCooldown = 0.8f; public int StaffCount = 1; private float _staffTimer = 0f;
        // 마늘
        public bool HasGarlic = false; public float GarlicDamage = 5f; public float GarlicRadius = 70f; public float GarlicCooldown = 0.5f; private float _garlicTimer = 0f;
        // 궤도
        public bool HasOrbital = false; public float OrbitalDamage = 15f; public int OrbitalCount = 2; public float OrbitalRadius = 80f; public float OrbitalSpeed = 3f; public float OrbitalAngle = 0f;
        // 도끼
        public bool HasAxe = false; public float AxeDamage = 25f; public int AxeCount = 1; public float AxeSpeed = 400f; private float _axeTimer = 0f;
        
        // 진화 무기
        public bool HasMagicCircle = false; private float _mcTimer = 0f; // 마법진 (관통 빔)
        public bool HasHolyWater = false; private float _hwTimer = 0f;   // 성수 (거대 폭발 펄스)
        public bool HasBlackHole = false; public float BlackHoleAngle = 0f; // 블랙홀 (적 흡입)
        public bool HasAxeStorm = false; private float _asTimer = 0f;    // 도끼폭풍 (8방향 투척)

        public List<Projectile> Projectiles = new List<Projectile>();

        public void ApplyLevel(WeaponType type, int level)
        {
            var data = WeaponTable.GetWeapon(type, level);
            switch (type) {
                case WeaponType.Staff: HasStaff = true; StaffDamage = data.StaffDamage; StaffCooldown = data.StaffCooldown; StaffCount = data.StaffProjectileCount; break;
                case WeaponType.Garlic: HasGarlic = true; GarlicDamage = data.GarlicDamage; GarlicRadius = data.GarlicRadius; GarlicCooldown = data.GarlicCooldown; break;
                case WeaponType.Orbital: HasOrbital = true; OrbitalDamage = data.OrbitalDamage; OrbitalCount = data.OrbitalCount; OrbitalRadius = data.OrbitalRadius; OrbitalSpeed = data.OrbitalSpeed; break;
                case WeaponType.Axe: HasAxe = true; AxeDamage = data.AxeDamage; AxeCount = data.AxeCount; AxeSpeed = data.AxeSpeed; break;
            }
        }

        public void ApplyAccessory(AccessoryType type, int level, Player player)
        {
            var data = WeaponTable.GetAcc(type, level);
            switch (type) {
                case AccessoryType.Wings: AccProjectileBonus = data.ValueInt; break;
                case AccessoryType.Armor: player.MaxHP += data.ValueFloat; player.HealHP(data.ValueFloat); break;
                case AccessoryType.Ring: AccAreaMult = data.ValueFloat; break;
                case AccessoryType.Glove: AccDamageMult = data.ValueFloat; break;
            }
        }

        public void ApplyEvolution(WeaponType from, WeaponType to)
        {
            if (from == WeaponType.Staff) HasStaff = false;
            if (from == WeaponType.Garlic) HasGarlic = false;
            if (from == WeaponType.Orbital) HasOrbital = false;
            if (from == WeaponType.Axe) HasAxe = false;

            if (to == WeaponType.MagicCircle) HasMagicCircle = true;
            if (to == WeaponType.HolyWater) HasHolyWater = true;
            if (to == WeaponType.BlackHole) HasBlackHole = true;
            if (to == WeaponType.AxeStorm) HasAxeStorm = true;
        }

        public void Update(float dt, Player player, List<Enemy> enemies, List<DamageText> damageTexts)
        {
            // ── 지팡이 ──
            if (HasStaff) {
                _staffTimer += dt;
                if (_staffTimer >= StaffCooldown) {
                    Enemy nearest = GetNearest(player.Position, enemies);
                    if (nearest != null) {
                        Vector2 dir = GetDir(player.Position, nearest.Position);
                        int count = StaffCount + AccProjectileBonus;
                        for (int i = 0; i < count; i++) {
                            float angle = count > 1 ? -0.2f + (0.4f / (count - 1)) * i : 0f;
                            Vector2 rotDir = Rotate(dir, angle);
                            Projectiles.Add(new Projectile { Position = player.Position, Velocity = new Vector2(rotDir.X * 400f, rotDir.Y * 400f), Damage = StaffDamage * AccDamageMult, Lifetime = 2f });
                        }
                        _staffTimer = 0f;
                    }
                }
            }

            // ── 도끼 ──
            if (HasAxe) {
                _axeTimer += dt;
                if (_axeTimer >= 1.5f) {
                    int count = AxeCount + AccProjectileBonus;
                    for (int i = 0; i < count; i++) {
                        float vx = (i % 2 == 0 ? 1 : -1) * (50f + i * 20f);
                        Projectiles.Add(new Projectile { Position = new Vector2(player.Position.X, player.Position.Y - 20), Velocity = new Vector2(vx, -AxeSpeed), Damage = AxeDamage * AccDamageMult, Lifetime = 3f, IsPiercing = true, PierceCount = 99 });
                    }
                    _axeTimer = 0f;
                }
            }

            // ── 진화: 마법진 (전방 3방향 무한 관통빔) ──
            if (HasMagicCircle) {
                _mcTimer += dt;
                if (_mcTimer >= 0.4f) {
                    Enemy nearest = GetNearest(player.Position, enemies);
                    if (nearest != null) {
                        Vector2 dir = GetDir(player.Position, nearest.Position);
                        int count = 3 + AccProjectileBonus;
                        for (int i = 0; i < count; i++) {
                            Vector2 rotDir = Rotate(dir, -0.3f + (0.6f / (count - 1)) * i);
                            Projectiles.Add(new Projectile { Position = player.Position, Velocity = new Vector2(rotDir.X * 600f, rotDir.Y * 600f), Damage = 80f * AccDamageMult, Lifetime = 2.5f, IsPiercing = true, PierceCount = 999 });
                        }
                        _mcTimer = 0f;
                    }
                }
            }

            // ── 진화: 도끼폭풍 (8방향 투척) ──
            if (HasAxeStorm) {
                _asTimer += dt;
                if (_asTimer >= 1.2f) {
                    for (int i = 0; i < 8 + AccProjectileBonus; i++) {
                        float angle = i * ((float)Math.PI * 2 / (8 + AccProjectileBonus));
                        Projectiles.Add(new Projectile { Position = player.Position, Velocity = new Vector2((float)Math.Cos(angle) * 300f, -600f), Damage = 120f * AccDamageMult, Lifetime = 3f, IsPiercing = true, PierceCount = 99 });
                    }
                    _asTimer = 0f;
                }
            }

            // 투사체 이동 및 충돌
            foreach (var p in Projectiles) {
                p.Position.X += p.Velocity.X * dt; p.Position.Y += p.Velocity.Y * dt;
                if (p.IsPiercing) p.Velocity.Y += 800f * dt; // 도끼용 중력
                p.Timer += dt; if (p.Timer >= p.Lifetime) p.IsActive = false;

                foreach (var e in enemies) {
                    if (!e.IsDead && p.IsActive && e.HitTimer <= 0 && Vector2.Distance(p.Position, e.Position) < 25f) {
                        e.HP -= p.Damage; e.HitTimer = 0.15f;
                        damageTexts.Add(new DamageText { Position = e.Position, Damage = p.Damage });
                        if (p.IsPiercing) { p.PierceCount--; if (p.PierceCount <= 0) p.IsActive = false; }
                        else { p.IsActive = false; break; }
                    }
                }
            }
            Projectiles.RemoveAll(p => !p.IsActive);

            // ── 마늘 ──
            if (HasGarlic) {
                _garlicTimer += dt;
                if (_garlicTimer >= GarlicCooldown) {
                    float rad = GarlicRadius * AccAreaMult; bool hit = false;
                    foreach (var e in enemies) {
                        if (!e.IsDead && Vector2.Distance(player.Position, e.Position) <= rad) {
                            e.HP -= GarlicDamage * AccDamageMult; e.HitTimer = 0.1f;
                            Vector2 d = GetDir(player.Position, e.Position); e.KnockbackDir = d; e.KnockbackSpeed = 100f;
                            damageTexts.Add(new DamageText { Position = e.Position, Damage = GarlicDamage * AccDamageMult }); hit = true;
                        }
                    }
                    if (hit) _garlicTimer = 0f;
                }
            }

            // ── 진화: 성수 (360도 거대 폭발 펄스) ──
            if (HasHolyWater) {
                _hwTimer += dt;
                if (_hwTimer >= 1.0f) {
                    float rad = 250f * AccAreaMult; // 압도적 범위
                    foreach (var e in enemies) {
                        if (!e.IsDead && Vector2.Distance(player.Position, e.Position) <= rad) {
                            e.HP -= 150f * AccDamageMult; e.HitTimer = 0.2f;
                            Vector2 d = GetDir(player.Position, e.Position); e.KnockbackDir = d; e.KnockbackSpeed = 300f;
                            damageTexts.Add(new DamageText { Position = e.Position, Damage = 150f * AccDamageMult });
                        }
                    }
                    _hwTimer = 0f;
                }
            }

            // ── 궤도 ──
            if (HasOrbital) {
                OrbitalAngle += OrbitalSpeed * dt;
                float rad = OrbitalRadius * AccAreaMult;
                for (int i = 0; i < OrbitalCount + AccProjectileBonus; i++) {
                    float ang = OrbitalAngle + (i * ((float)Math.PI * 2 / (OrbitalCount + AccProjectileBonus)));
                    Vector2 orb = new Vector2(player.Position.X + (float)Math.Cos(ang) * rad, player.Position.Y + (float)Math.Sin(ang) * rad);
                    foreach (var e in enemies) {
                        if (!e.IsDead && e.HitTimer <= 0 && Vector2.Distance(orb, e.Position) < 30f) {
                            e.HP -= OrbitalDamage * AccDamageMult; e.HitTimer = 0.2f;
                            damageTexts.Add(new DamageText { Position = e.Position, Damage = OrbitalDamage * AccDamageMult });
                        }
                    }
                }
            }

            // ── 진화: 블랙홀 (적 강제 흡입) ──
            if (HasBlackHole) {
                BlackHoleAngle += 2f * dt;
                float rad = 150f * AccAreaMult;
                foreach (var e in enemies) {
                    if (!e.IsDead && Vector2.Distance(player.Position, e.Position) < rad) {
                        Vector2 d = GetDir(player.Position, e.Position);
                        // 역방향 넉백으로 플레이어 쪽으로 강제 흡입
                        e.KnockbackDir = new Vector2(-d.X, -d.Y); e.KnockbackSpeed = 120f;
                        if (e.HitTimer <= 0) {
                            e.HP -= 50f * AccDamageMult; e.HitTimer = 0.2f;
                            damageTexts.Add(new DamageText { Position = e.Position, Damage = 50f * AccDamageMult });
                        }
                    }
                }
            }
        }

        // 유틸 함수
        private Enemy GetNearest(Vector2 pos, List<Enemy> enemies) {
            Enemy n = null; float min = 400f * AccAreaMult;
            foreach (var e in enemies) { if (e.IsDead) continue; float d = Vector2.Distance(pos, e.Position); if (d < min) { min = d; n = e; } }
            return n;
        }
        private Vector2 GetDir(Vector2 from, Vector2 to) {
            float dx = to.X - from.X; float dy = to.Y - from.Y; float dist = (float)Math.Sqrt(dx * dx + dy * dy);
            return dist > 0 ? new Vector2(dx / dist, dy / dist) : new Vector2(1, 0);
        }
        private Vector2 Rotate(Vector2 v, float rad) => new Vector2(v.X * (float)Math.Cos(rad) - v.Y * (float)Math.Sin(rad), v.X * (float)Math.Sin(rad) + v.Y * (float)Math.Cos(rad));
    }
}