// 파일명: CombatSystem.cs
using System;
using System.Collections.Generic;
using GameCore;
using EntityGroup;

namespace CombatSystem
{
    public class Projectile { public Vector2 Position; public Vector2 Direction; public float Speed; public float Damage; public float Lifetime; public float Timer = 0f; public bool IsActive = true; }
    public class DamageText { public Vector2 Position; public float Damage; public float Lifetime = 0.5f; public float Timer = 0f; public void Update(float dt) { Timer += dt; Position.Y -= 30f * dt; } }

    public class Weapon
    {
        // 1. 기본 무기 (지팡이 투사체)
        public float Damage = 0f; public float FireCooldown = 0.5f; private float _fireTimer = 0f; public float Range = 300f;
        public List<Projectile> Projectiles = new List<Projectile>();
        
        // 2. 마늘 (광역 오라)
        public bool HasGarlic = true; // 우선 시작하자마자 적용되도록 true
        public float GarlicDamage = 5f; public float GarlicRadius = 70f; public float GarlicCooldown = 0.5f; private float _garlicTimer = 0f;

        // 3. 궤도 무기 (주위를 도는 구체)
        public bool HasOrbital = true; // 우선 시작하자마자 적용되도록 true
        public int OrbitalCount = 2; public float OrbitalRadius = 80f; public float OrbitalSpeed = 3f; public float OrbitalDamage = 15f;
        public float OrbitalAngle = 0f;

        public void Update(float dt, Player player, List<Enemy> enemies, List<DamageText> damageTexts)
        {
            // [1] 지팡이 연산
            _fireTimer += dt;
            if (_fireTimer >= FireCooldown)
            {
                Enemy nearest = null; float minDist = Range;
                foreach (var e in enemies) { if (e.IsDead) continue; float d = Vector2.Distance(player.Position, e.Position); if (d < minDist) { minDist = d; nearest = e; } }
                if (nearest != null)
                {
                    float dx = nearest.Position.X - player.Position.X; float dy = nearest.Position.Y - player.Position.Y;
                    float dist = (float)Math.Sqrt(dx * dx + dy * dy);
                    Projectiles.Add(new Projectile { Position = player.Position, Direction = new Vector2(dx / dist, dy / dist), Speed = 400f, Damage = Damage, Lifetime = 2f });
                    _fireTimer = 0f;
                }
            }
            foreach (var p in Projectiles)
            {
                p.Position.X += p.Direction.X * p.Speed * dt; p.Position.Y += p.Direction.Y * p.Speed * dt; p.Timer += dt;
                if (p.Timer >= p.Lifetime) p.IsActive = false;
                foreach (var e in enemies) { if (!e.IsDead && p.IsActive && Vector2.Distance(p.Position, e.Position) < 20f) { e.HP -= p.Damage; e.HitTimer = 0.1f; e.KnockbackDir = p.Direction; e.KnockbackSpeed = 150f; p.IsActive = false; damageTexts.Add(new DamageText { Position = e.Position, Damage = p.Damage }); } }
            }
            Projectiles.RemoveAll(p => !p.IsActive);

            // [2] 마늘 연산
            if (HasGarlic)
            {
                _garlicTimer += dt;
                if (_garlicTimer >= GarlicCooldown)
                {
                    bool hitAny = false;
                    foreach (var e in enemies)
                    {
                        if (!e.IsDead && Vector2.Distance(player.Position, e.Position) <= GarlicRadius)
                        {
                            e.HP -= GarlicDamage; e.HitTimer = 0.1f;
                            float dx = e.Position.X - player.Position.X; float dy = e.Position.Y - player.Position.Y;
                            float dist = (float)Math.Sqrt(dx * dx + dy * dy);
                            if (dist > 0) { e.KnockbackDir = new Vector2(dx / dist, dy / dist); e.KnockbackSpeed = 100f; }
                            damageTexts.Add(new DamageText { Position = e.Position, Damage = GarlicDamage });
                            hitAny = true;
                        }
                    }
                    if (hitAny) _garlicTimer = 0f; 
                }
            }

            // [3] 궤도 무기 연산
            if (HasOrbital)
            {
                OrbitalAngle += OrbitalSpeed * dt;
                if (OrbitalAngle > (float)Math.PI * 2) OrbitalAngle -= (float)Math.PI * 2;
                
                for (int i = 0; i < OrbitalCount; i++)
                {
                    float currentAngle = OrbitalAngle + (i * ((float)Math.PI * 2 / OrbitalCount));
                    Vector2 orbPos = new Vector2(player.Position.X + (float)Math.Cos(currentAngle) * OrbitalRadius, player.Position.Y + (float)Math.Sin(currentAngle) * OrbitalRadius);
                    
                    foreach (var e in enemies)
                    {
                        if (!e.IsDead && Vector2.Distance(orbPos, e.Position) < 25f)
                        {
                            if (e.HitTimer <= 0) // 다단히트 방지용 피격 무적시간 활용
                            {
                                e.HP -= OrbitalDamage; e.HitTimer = 0.2f; 
                                float dx = e.Position.X - player.Position.X; float dy = e.Position.Y - player.Position.Y;
                                float dist = (float)Math.Sqrt(dx * dx + dy * dy);
                                if (dist > 0) { e.KnockbackDir = new Vector2(dx / dist, dy / dist); e.KnockbackSpeed = 150f; }
                                damageTexts.Add(new DamageText { Position = e.Position, Damage = OrbitalDamage });
                            }
                        }
                    }
                }
            }
        }
    }
}