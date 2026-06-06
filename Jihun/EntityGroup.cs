using System;
using Raylib_cs;
using GameCore;

namespace EntityGroup
{
    // ... (ExpGem 클래스는 기존과 동일) ...
    public class ExpGem { public Vector2 Position; public bool IsCollected = false; public int GemTypeIndex = 0; public float AnimTimer = 0f; public int CurrentFrame = 0; public int MaxFrames => (GemTypeIndex < 3) ? 5 : 4; public bool IsCoin => GemTypeIndex < 3; public int GetValue() { switch (GemTypeIndex) { case 0: return 1; case 1: return 10; case 2: return 50; case 3: return 1; case 4: return 5; case 5: return 15; case 6: return 50; case 7: return 100; default: return 1; } } public void Update(float deltaTime) { AnimTimer += deltaTime; if (AnimTimer >= 0.1f) { AnimTimer = 0f; CurrentFrame = (CurrentFrame + 1) % MaxFrames; } } }

    public class Player
    {
        public Vector2 Position; public float Speed = 200f;
        public float MaxHP = 100f; public float CurrentHP = 100f; public bool IsDead => CurrentHP <= 0;
        public int Gold = 0;
        
        // ★ 플레이어 전용 피격 타이머 추가
        public float HitTimer = 0f;

        public bool IsMoving = false; public bool IsFacingLeft = false; public float AnimTimer = 0f; public int CurrentFrame = 0;

        public void Update(float deltaTime)
        {
            if (IsDead) return;
            
            // ★ 피격 타이머 감소
            if (HitTimer > 0) HitTimer -= deltaTime;

            bool keyPressed = false;
            if (Raylib.IsKeyDown(KeyboardKey.W) || Raylib.IsKeyDown(KeyboardKey.Up)) { Position.Y -= Speed * deltaTime; keyPressed = true; }
            if (Raylib.IsKeyDown(KeyboardKey.S) || Raylib.IsKeyDown(KeyboardKey.Down)) { Position.Y += Speed * deltaTime; keyPressed = true; }
            if (Raylib.IsKeyDown(KeyboardKey.A) || Raylib.IsKeyDown(KeyboardKey.Left)) { Position.X -= Speed * deltaTime; keyPressed = true; IsFacingLeft = true; }
            if (Raylib.IsKeyDown(KeyboardKey.D) || Raylib.IsKeyDown(KeyboardKey.Right)) { Position.X += Speed * deltaTime; keyPressed = true; IsFacingLeft = false; }
            
            if (IsMoving != keyPressed) { CurrentFrame = 0; AnimTimer = 0f; }
            IsMoving = keyPressed; 
            AnimTimer += deltaTime; if (AnimTimer >= 0.08f) { AnimTimer = 0f; CurrentFrame++; }
        }
    }

    public class Enemy
    {
        public Vector2 Position; public float Speed = 90f; public float HP = 10; public bool IsDead => HP <= 0;
        public float Damage = 10f; public float HitTimer = 0f; public Vector2 KnockbackDir; public float KnockbackSpeed = 0f;

        // ★ 웨이브/보스 시스템을 위한 추가 속성
        public bool IsBoss = false;
        public float Scale = 3.0f; // 렌더링 크기 (보스는 6.0f 등으로 키움)
        public Color TintColor = Color.White; // 기본 색상 (보스는 보라색 등으로 변경)

        public void Update(float deltaTime, Vector2 playerPosition)
        {
            if (HitTimer > 0) HitTimer -= deltaTime;
            if (KnockbackSpeed > 0)
            {
                Position.X += KnockbackDir.X * KnockbackSpeed * deltaTime; Position.Y += KnockbackDir.Y * KnockbackSpeed * deltaTime;
                KnockbackSpeed -= 1500f * deltaTime; if (KnockbackSpeed < 0) KnockbackSpeed = 0;
            }
            else
            {
                float dirX = playerPosition.X - Position.X; float dirY = playerPosition.Y - Position.Y;
                float distance = (float)Math.Sqrt(dirX * dirX + dirY * dirY);
                if (distance > 0) { Position.X += (dirX / distance) * Speed * deltaTime; Position.Y += (dirY / distance) * Speed * deltaTime; }
            }
        }
    }
}