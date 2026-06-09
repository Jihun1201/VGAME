// 파일명: CombatSystem.cs
using System;
using System.Collections.Generic;
using GameCore;
using EntityGroup;
using WeaponData;

namespace CombatSystem
{
    public enum ProjectileSprite { None, StaffBullet, Fireball, Axe, AxeStorm, Shuriken, InfiniteShuriken }

    public class Projectile
    {
        public Vector2 Position; public Vector2 Velocity; public float Damage; public float Lifetime; public float Timer = 0f; public bool IsActive = true;
        public bool IsPiercing = false; public int PierceCount = 1;
        public bool IsAxeType = false; // 중력(포물선) 적용 여부 — 도끼 계열만 true
        // 부메랑: 절반 거리 후 돌아옴
        public bool IsBoomerang = false; public bool IsReturning = false; public Vector2 OwnerPos;
        // 버스트: 여러 발을 짧은 간격으로 순차 발사
        public bool IsBurst = false; public int BurstIndex = 0;
        public bool IsFireball = false;
        public ProjectileSprite Sprite = ProjectileSprite.None;
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
        // 버그3 추가: 투사체 속도 배율 (날개 Lv1 효과)
        public float AccProjectileSpeedMult = 1f;

        // 지팡이
        public bool HasStaff = false; public float StaffDamage = 10f; public float StaffCooldown = 0.8f; public int StaffCount = 1; private float _staffTimer = 0f;
        // 마늘
        public bool HasGarlic = false; public float GarlicDamage = 5f; public float GarlicRadius = 70f; public float GarlicCooldown = 0.5f; private float _garlicTimer = 0f;
        // 궤도
        public bool HasOrbital = false; public float OrbitalDamage = 15f; public int OrbitalCount = 2; public float OrbitalRadius = 80f; public float OrbitalSpeed = 3f; public float OrbitalAngle = 0f;
        // 도끼
        public bool HasAxe = false; public float AxeDamage = 25f; public int AxeCount = 1; public float AxeSpeed = 400f; private float _axeTimer = 0f;
        
        // 진화 무기
        public bool HasMagicCircle     = false; private float _mcTimer  = 0f; // 마법진 (영창+갑옷 진화 — 광역피흡)
        public bool HasHellFire        = false; private float _hwTimer  = 0f; // 헬파이어 (지팡이+신발 진화 — 샷건식 동시 발사)
        public bool HasBlackHole       = false; public float BlackHoleAngle = 0f;
        public bool HasAxeStorm        = false; private float _asTimer  = 0f;
        // 표창 / 무한표창
        public bool HasShuriken        = false; public float ShurikenDamage = 18f; public int ShurikenCount = 1; private float _shurikenTimer = 0f; public float ShurikenCooldown = 1.2f;
        // 표창 재장전: 공중에 있는 표창 수를 추적 → 0이 되어야만 다음 발사 가능
        private int _shurikenInFlight  = 0;
        public bool HasInfiniteShuriken= false; private float _isTimer  = 0f;

        public List<Projectile> Projectiles = new List<Projectile>();

        public void ApplyLevel(WeaponType type, int level)
        {
            var data = WeaponTable.GetWeapon(type, level);
            switch (type) {
                case WeaponType.Staff:    HasStaff = true; StaffDamage = data.StaffDamage; StaffCooldown = data.StaffCooldown; StaffCount = data.StaffProjectileCount; break;
                case WeaponType.Garlic:   HasGarlic = true; GarlicDamage = data.GarlicDamage; GarlicRadius = data.GarlicRadius; GarlicCooldown = data.GarlicCooldown; break;
                case WeaponType.Orbital:  HasOrbital = true; OrbitalDamage = data.OrbitalDamage; OrbitalCount = data.OrbitalCount; OrbitalRadius = data.OrbitalRadius; OrbitalSpeed = data.OrbitalSpeed; break;
                case WeaponType.Axe:      HasAxe = true; AxeDamage = data.AxeDamage; AxeCount = data.AxeCount; AxeSpeed = data.AxeSpeed; break;
                case WeaponType.Shuriken: HasShuriken = true; ShurikenDamage = data.AxeDamage; ShurikenCount = data.AxeCount; break;
            }
        }

        public void ApplyAccessory(AccessoryType type, int level, Player player)
        {
            var data = WeaponTable.GetAcc(type, level);
            switch (type) {
                case AccessoryType.Shoes:
                    if (data.ValueInt == -1) { player.Speed += data.ValueFloat; }
                    else if (data.ValueFloat > 1.0f && data.ValueInt == 0) { AccProjectileSpeedMult = data.ValueFloat; }
                    else { AccProjectileBonus += data.ValueInt; }
                    break;
                case AccessoryType.Armor:    player.MaxHP += data.ValueFloat; player.HealHP(data.ValueFloat); break;
                case AccessoryType.Ring:     AccAreaMult = data.ValueFloat; break;
                case AccessoryType.Glove:    AccDamageMult = data.ValueFloat; break;
                case AccessoryType.Necklace: /* 경험치 배율은 LevelSystem.ExpMult로 처리 — GameCore에서 적용 */ break;
            }
        }

        public void ApplyEvolution(WeaponType from, WeaponType to)
        {
            if (from == WeaponType.Staff)    HasStaff    = false;
            if (from == WeaponType.Garlic)   HasGarlic   = false;
            if (from == WeaponType.Orbital)  HasOrbital  = false;
            if (from == WeaponType.Axe)      HasAxe      = false;
            if (from == WeaponType.Shuriken) HasShuriken = false;

            if (to == WeaponType.MagicCircle)      HasMagicCircle      = true;  // 마법진 (영창+갑옷)
            if (to == WeaponType.HellFire)          HasHellFire         = true;  // 헬파이어 (지팡이+날개)
            if (to == WeaponType.BlackHole)         HasBlackHole        = true;
            if (to == WeaponType.AxeStorm)          HasAxeStorm         = true;
            if (to == WeaponType.InfiniteShuriken)  HasInfiniteShuriken = true;
        }

        public void Update(float dt, Player player, List<Enemy> enemies, List<DamageText> damageTexts)
        {
            // ── 지팡이 (샷건: 한 번 발사 시 count발이 옆으로 동시에 퍼져 나감) ──
            if (HasStaff) {
                _staffTimer += dt;
                if (_staffTimer >= StaffCooldown) {
                    Enemy nearest = GetNearest(player.Position, enemies);
                    if (nearest != null) {
                        Vector2 dir = GetDir(player.Position, nearest.Position);
                        int count = StaffCount + AccProjectileBonus;
                        // spread: 발수가 많을수록 더 넓게 (샷건 느낌)
                        float totalSpread = count > 1 ? 0.28f * (count - 1) : 0f;
                        for (int i = 0; i < count; i++) {
                            float angle = count > 1 ? -totalSpread / 2f + (totalSpread / (count - 1)) * i : 0f;
                            Vector2 rotDir = Rotate(dir, angle);
                            Projectiles.Add(new Projectile {
                                Position = player.Position,
                                Velocity = new Vector2(rotDir.X * 400f * AccProjectileSpeedMult, rotDir.Y * 400f * AccProjectileSpeedMult),
                                Damage = StaffDamage * AccDamageMult,
                                Lifetime = 2f,
                                IsFireball = true,
                                Sprite = ProjectileSprite.StaffBullet
                            });
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
                        Projectiles.Add(new Projectile { Position = new Vector2(player.Position.X, player.Position.Y - 20), Velocity = new Vector2(vx, -AxeSpeed), Damage = AxeDamage * AccDamageMult, Lifetime = 3f, IsPiercing = true, PierceCount = 99, IsAxeType = true, Sprite = ProjectileSprite.Axe });
                    }
                    _axeTimer = 0f;
                }
            }

            // ── 표창 (부메랑: 날아가다가 돌아옴) ──
            // 공중에 표창이 없고, 쿨타임이 지난 경우에만 발사 (최대 3발)
            if (HasShuriken) {
                _shurikenTimer += dt;
                if (_shurikenInFlight <= 0 && _shurikenTimer >= ShurikenCooldown) {
                    Enemy nearest = GetNearest(player.Position, enemies);
                    if (nearest != null) {
                        Vector2 dir = GetDir(player.Position, nearest.Position);
                        int cnt = Math.Min(ShurikenCount + AccProjectileBonus, 3); // 최대 3발
                        for (int i = 0; i < cnt; i++) {
                            float ang = cnt > 1 ? -0.15f + (0.3f/(cnt-1))*i : 0f;
                            Vector2 rd = Rotate(dir, ang);
                            Projectiles.Add(new Projectile {
                                Position = player.Position,
                                Velocity = new Vector2(rd.X*500f*AccProjectileSpeedMult, rd.Y*500f*AccProjectileSpeedMult),
                                Damage = ShurikenDamage * AccDamageMult,
                                Lifetime = 2.0f, IsPiercing = true, PierceCount = 99,
                                IsBoomerang = true, OwnerPos = player.Position,
                                Sprite = ProjectileSprite.Shuriken
                            });
                        }
                        _shurikenInFlight = cnt;
                        _shurikenTimer = 0f;
                    }
                }
            }

            // ── 무한표창 (부메랑, 더 빠르고 더 많이) ──
            if (HasInfiniteShuriken) {
                _isTimer += dt;
                if (_isTimer >= 0.5f) {
                    Enemy nearest = GetNearest(player.Position, enemies);
                    if (nearest != null) {
                        Vector2 dir = GetDir(player.Position, nearest.Position);
                        int cnt = 3 + AccProjectileBonus;
                        for (int i = 0; i < cnt; i++) {
                            float ang = cnt > 1 ? -0.25f + (0.5f/(cnt-1))*i : 0f;
                            Vector2 rd = Rotate(dir, ang);
                            Projectiles.Add(new Projectile {
                                Position = player.Position,
                                Velocity = new Vector2(rd.X*650f, rd.Y*650f),
                                Damage = 90f * AccDamageMult,
                                Lifetime = 2.0f, IsPiercing = true, PierceCount = 999,
                                IsBoomerang = true, OwnerPos = player.Position,
                                Sprite = ProjectileSprite.InfiniteShuriken
                            });
                        }
                        _isTimer = 0f;
                    }
                }
            }
            // ── 진화: 헬파이어 (지팡이+신발 — 샷건식 관통 빔, 쿨타임마다 동시 발사) ──
            if (HasHellFire) {
                _hwTimer += dt;
                if (_hwTimer >= 1.0f) {
                    Enemy nearest = GetNearest(player.Position, enemies);
                    if (nearest != null) {
                        _hwTimer = 0f;
                        Vector2 dir = GetDir(player.Position, nearest.Position);
                        int count = 3 + AccProjectileBonus;
                        float totalSpread = count > 1 ? 0.32f * (count - 1) : 0f;
                        for (int i = 0; i < count; i++) {
                            float spreadAngle = count > 1 ? -totalSpread / 2f + (totalSpread / (count - 1)) * i : 0f;
                            Vector2 rd = Rotate(dir, spreadAngle);
                            Projectiles.Add(new Projectile {
                                Position = player.Position,
                                Velocity = new Vector2(rd.X * 700f, rd.Y * 700f),
                                Damage = 100f * AccDamageMult,
                                Lifetime = 2.2f, IsPiercing = true, PierceCount = 999,
                                IsFireball = true,
                                Sprite = ProjectileSprite.Fireball
                            });
                        }
                    }
                }
            }

            // ── 진화: 마법진 (영창+갑옷 — 광역 피흡, 싸울수록 체력회복) ──
            if (HasMagicCircle) {
                _mcTimer += dt;
                if (_mcTimer >= 0.8f) {
                    _mcTimer = 0f;
                    float rad = 120f * AccAreaMult;
                    float totalHeal = 0f;
                    int hitCount = 0;
                    foreach (var e in enemies) {
                        if (!e.IsDead && Vector2.Distance(player.Position, e.Position) <= rad) {
                            float dmg = 80f * AccDamageMult;
                            e.HP -= dmg; e.MeleeHitTimer = 0.15f;
                            Vector2 d = GetDir(player.Position, e.Position); e.KnockbackDir = d; e.KnockbackSpeed = 120f;
                            damageTexts.Add(new DamageText { Position = e.Position, Damage = dmg });
                            totalHeal += dmg * 0.12f; // 데미지의 12% 피흡
                            hitCount++;
                        }
                    }
                    if (totalHeal > 0) {
                        // 적을 많이 칠수록 더 많이 회복 (최대 MaxHP 20%)
                        float healed = Math.Min(totalHeal, player.MaxHP * 0.20f);
                        player.HealHP(healed);
                        damageTexts.Add(new DamageText { Position = player.Position, Damage = -healed });
                    }
                    // 싸울수록 체력회복: 적 3마리 이상 동시에 맞추면 추가 회복
                    if (hitCount >= 3) {
                        float bonusHeal = player.MaxHP * 0.02f * hitCount;
                        bonusHeal = Math.Min(bonusHeal, player.MaxHP * 0.10f);
                        player.HealHP(bonusHeal);
                    }
                }
            }

            // ── 진화: 도끼폭풍 (8방향 투척) ──
            if (HasAxeStorm) {
                _asTimer += dt;
                if (_asTimer >= 1.2f) {
                    int totalCount = 8 + AccProjectileBonus;
                    for (int i = 0; i < totalCount; i++) {
                        float angle = i * ((float)Math.PI * 2 / totalCount);
                        // 버그4 수정: 수평(cos)뿐 아니라 수직(sin)도 반영하여 진짜 8방향으로 퍼짐
                        // 중력(IsAxeType=true)이 붙으므로 위쪽 방향엔 보정 오프셋(-300f) 추가
                        float vx = (float)Math.Cos(angle) * 500f;
                        float vy = (float)Math.Sin(angle) * 500f - 300f;
                        Projectiles.Add(new Projectile { Position = player.Position, Velocity = new Vector2(vx, vy), Damage = 120f * AccDamageMult, Lifetime = 3f, IsPiercing = true, PierceCount = 99, IsAxeType = true, Sprite = ProjectileSprite.AxeStorm });
                    }
                    _asTimer = 0f;
                }
            }

            // 투사체 이동 및 충돌
            foreach (var p in Projectiles) {
                // 부메랑: 절반 수명이 지나면 플레이어 방향으로 돌아옴
                if (p.IsBoomerang) {
                    p.OwnerPos = player.Position; // 플레이어 위치 실시간 추적
                    if (!p.IsReturning && p.Timer >= p.Lifetime * 0.45f) {
                        p.IsReturning = true;
                        // 돌아오는 방향으로 속도 반전
                        float dx = player.Position.X - p.Position.X;
                        float dy = player.Position.Y - p.Position.Y;
                        float dist = (float)Math.Sqrt(dx*dx+dy*dy);
                        float spd = (float)Math.Sqrt(p.Velocity.X*p.Velocity.X+p.Velocity.Y*p.Velocity.Y);
                        if (spd < 1f) spd = 500f;
                        p.Velocity = dist > 0 ? new Vector2(dx/dist*spd, dy/dist*spd) : p.Velocity;
                    }
                    // 돌아오는 중이면 플레이어 방향으로 유도
                    if (p.IsReturning) {
                        float dx = player.Position.X - p.Position.X;
                        float dy = player.Position.Y - p.Position.Y;
                        float dist = (float)Math.Sqrt(dx*dx+dy*dy);
                        // 플레이어에 도달하면 사라짐
                        if (dist < 20f) { p.IsActive = false; if (HasShuriken) _shurikenInFlight = Math.Max(0, _shurikenInFlight - 1); continue; }
                        float spd = (float)Math.Sqrt(p.Velocity.X*p.Velocity.X+p.Velocity.Y*p.Velocity.Y);
                        if (spd < 400f) spd = 550f; // 돌아올 때는 빠르게
                        p.Velocity = dist > 0 ? new Vector2(dx/dist*spd, dy/dist*spd) : p.Velocity;
                        // 귀환 중엔 수명을 연장해 중간에 깜빡이며 사라지지 않도록
                        if (p.Timer >= p.Lifetime - 0.05f) p.Lifetime += 0.5f;
                    }
                }
                p.Position.X += p.Velocity.X * dt; p.Position.Y += p.Velocity.Y * dt;
                if (p.IsAxeType) p.Velocity.Y += 800f * dt; // 도끼 계열만 중력(포물선) 적용
                p.Timer += dt;
                if (p.Timer >= p.Lifetime) {
                    // 수명 만료 시: 표창이면 InFlight 카운트도 감소
                    if (p.IsBoomerang && HasShuriken) _shurikenInFlight = Math.Max(0, _shurikenInFlight - 1);
                    p.IsActive = false;
                }

                foreach (var e in enemies) {
                    if (!e.IsDead && p.IsActive && e.ProjectileHitTimer <= 0 && Vector2.Distance(p.Position, e.Position) < 25f) {
                        e.HP -= p.Damage; e.ProjectileHitTimer = 0.15f;
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
                    float rad = GarlicRadius; // 마늘은 범위 장신구(반지) 영향을 받지 않음
                    foreach (var e in enemies) {
                        if (!e.IsDead && Vector2.Distance(player.Position, e.Position) <= rad) {
                            e.HP -= GarlicDamage * AccDamageMult; e.MeleeHitTimer = 0.1f;
                            Vector2 d = GetDir(player.Position, e.Position); e.KnockbackDir = d; e.KnockbackSpeed = 100f;
                            damageTexts.Add(new DamageText { Position = e.Position, Damage = GarlicDamage * AccDamageMult });
                        }
                    }
                    // 버그1 수정: 적 유무와 무관하게 항상 타이머 리셋 (기존: hit==true 일 때만 리셋)
                    _garlicTimer = 0f;
                }
            }

            // ── (구 마법진 코드 제거 — 위의 HasMagicCircle 로직으로 통합됨) ──

            // ── 궤도 ──
            if (HasOrbital) {
                OrbitalAngle += OrbitalSpeed * dt;
                float rad = OrbitalRadius * AccAreaMult;
                for (int i = 0; i < OrbitalCount + AccProjectileBonus; i++) {
                    float ang = OrbitalAngle + (i * ((float)Math.PI * 2 / (OrbitalCount + AccProjectileBonus)));
                    Vector2 orb = new Vector2(player.Position.X + (float)Math.Cos(ang) * rad, player.Position.Y + (float)Math.Sin(ang) * rad);
                    foreach (var e in enemies) {
                        if (!e.IsDead && e.OrbitalHitTimer <= 0 && Vector2.Distance(orb, e.Position) < 30f) {
                            e.HP -= OrbitalDamage * AccDamageMult; e.OrbitalHitTimer = 0.2f;
                            damageTexts.Add(new DamageText { Position = e.Position, Damage = OrbitalDamage * AccDamageMult });
                        }
                    }
                }
            }

            // ── 진화: 블랙홀 (빈틈 없이 연속 회전하는 구체 링) ──
            if (HasBlackHole) {
                BlackHoleAngle += 3.5f * dt; // 빠르게 회전
                float rad = 120f * AccAreaMult;
                int orbCount = 12 + AccProjectileBonus * 2; // 12개 구체로 빈틈 없이 채움
                for (int i = 0; i < orbCount; i++) {
                    float ang = BlackHoleAngle + (i * ((float)Math.PI * 2 / orbCount));
                    Vector2 orb = new Vector2(
                        player.Position.X + (float)Math.Cos(ang) * rad,
                        player.Position.Y + (float)Math.Sin(ang) * rad);
                    foreach (var e in enemies) {
                        if (!e.IsDead && e.OrbitalHitTimer <= 0 && Vector2.Distance(orb, e.Position) < 22f) {
                            e.HP -= 60f * AccDamageMult; e.OrbitalHitTimer = 0.15f;
                            // 블랙홀 흡입: 플레이어 방향으로 끌어당김
                            Vector2 d = GetDir(player.Position, e.Position);
                            e.KnockbackDir = new Vector2(-d.X, -d.Y); e.KnockbackSpeed = 150f;
                            damageTexts.Add(new DamageText { Position = e.Position, Damage = 60f * AccDamageMult });
                        }
                    }
                }
            }
        }

        // 유틸 함수
        // 버그6 수정: 타겟팅 거리는 고정값(400f) 사용. AccAreaMult는 '공격 범위'지 '탐색 거리'가 아님
        private Enemy GetNearest(Vector2 pos, List<Enemy> enemies) {
            Enemy n = null; float min = 400f;
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