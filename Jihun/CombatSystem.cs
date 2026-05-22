// 파일명: CombatSystem.cs
using System;
using System.Collections.Generic;
using GameCore;
using EntityGroup;

namespace CombatSystem
{
    public class DamageText
    {
        public Vector2 Position;
        public int Damage;
        public float Timer = 0f;
        public float Lifetime = 0.5f;

        public void Update(float dt)
        {
            Timer += dt;
            Position.Y -= 50f * dt;
        }
    }

    public class Projectile 
    { 
        public Vector2 Position; public Vector2 Direction; public float Speed = 500f; public int Damage = 5; public bool IsActive = true;
        public void Update(float deltaTime) 
        { 
            Position.X += Direction.X * Speed * deltaTime; Position.Y += Direction.Y * Speed * deltaTime; 
        }
    }

    public class Weapon 
    {  
        public List<Projectile> Projectiles = new List<Projectile>(); 
        public float FireCooldown = 0.5f; 
        public int Damage = 5; 
        
        // ★ 무기 사거리(시야) 추가 (화면 밖의 적은 무시함)
        public float Range = 400f; 
        
        private float _timer = 0f;

        public void Update(float dt, Player player, List<Enemy> enemies, List<DamageText> damageTexts) 
        {
            _timer += dt; 
            if (_timer >= FireCooldown && enemies.Count > 0) 
            { 
                // 발사 시도 (사거리 내에 적이 없으면 발사하지 않음)
                bool fired = FireAtNearest(player.Position, enemies);
                if (fired) _timer = 0f; // 진짜로 쐈을 때만 쿨타임 초기화
            }
            
            foreach (var p in Projectiles) 
            {
                if (!p.IsActive) continue; 
                p.Update(dt);
                
                // 투사체도 일정 거리 이상 날아가면 소멸되도록 처리 (메모리 최적화)
                if (Vector2.Distance(player.Position, p.Position) > 600f)
                {
                    p.IsActive = false;
                    continue;
                }

                foreach (var e in enemies) 
                {
                    if (e.IsDead) continue;
                    if (Vector2.Distance(p.Position, e.Position) < 15.0f) 
                    { 
                        e.HP -= p.Damage; 
                        p.IsActive = false; 

                        e.HitTimer = 0.15f; 
                        e.KnockbackDir = p.Direction; 
                        e.KnockbackSpeed = 300f; 

                        damageTexts.Add(new DamageText { Position = e.Position, Damage = p.Damage });
                        break; 
                    }
                }
            }
            Projectiles.RemoveAll(p => !p.IsActive);
        }

        // 반환형을 void에서 bool로 변경 (사격 성공 여부 반환)
        private bool FireAtNearest(Vector2 playerPos, List<Enemy> enemies) 
        {
            Enemy nearest = null; 
            float minDistance = float.MaxValue;
            
            foreach (var e in enemies) 
            {
                float dist = Vector2.Distance(playerPos, e.Position); 
                
                // ★ 거리가 가장 가깝고, 동시에 '사거리(Range)' 안쪽에 있는 적만 타겟팅
                if (dist < minDistance && dist <= Range) 
                { 
                    minDistance = dist; 
                    nearest = e; 
                }
            }
            
            if (nearest != null) 
            {
                float dirX = nearest.Position.X - playerPos.X; 
                float dirY = nearest.Position.Y - playerPos.Y;
                float dist = (float)Math.Sqrt(dirX * dirX + dirY * dirY);
                Projectiles.Add(new Projectile { Position = playerPos, Direction = new Vector2(dirX / dist, dirY / dist), Damage = this.Damage });
                return true; // 사격 성공
            }
            return false; // 사거리 내에 적이 없어서 실패
        }
    }
}