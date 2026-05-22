// 파일명: EntityGroup.cs
using System;
using Raylib_cs;
using GameCore;

namespace EntityGroup
{
    // 이름은 기존과 동일하게 유지하되, 내부 로직을 업그레이드합니다.
    public class ExpGem 
    { 
        public Vector2 Position; 
        public bool IsCollected = false; 
        public int GemTypeIndex = 0;
        
        public float AnimTimer = 0f; 
        public int CurrentFrame = 0; 
        
        // 0, 1, 2는 코인(Moneda), 나머지는 경험치 보석
        public int MaxFrames => (GemTypeIndex < 3) ? 5 : 4; 
        public bool IsCoin => GemTypeIndex < 3;

        // ★ 각 인덱스별로 획득하는 가치(Amount)를 반환하는 함수
        public int GetValue()
        {
            switch (GemTypeIndex)
            {
                case 0: return 1;   // MonedaP (1 골드)
                case 1: return 10;  // MonedaD (10 골드)
                case 2: return 50;  // MonedaR (50 골드)
                case 3: return 1;   // Gri (회색 1 EXP)
                case 4: return 5;   // Strip4 (초록 5 EXP)
                case 5: return 15;  // Azu (하늘 15 EXP)
                case 6: return 50;  // Ama (노랑 50 EXP)
                case 7: return 100; // Roj (빨강 100 EXP)
                default: return 1;
            }
        }

        public void Update(float deltaTime) 
        {
            AnimTimer += deltaTime; 
            if (AnimTimer >= 0.1f) 
            { 
                AnimTimer = 0f; 
                CurrentFrame = (CurrentFrame + 1) % MaxFrames; 
            }
        }
    }

    public class Player
    {
        public Vector2 Position; 
        public float Speed = 200f;
        public float MaxHP = 100f;
        public float CurrentHP = 100f;
        public bool IsDead => CurrentHP <= 0;

        // ★ 플레이어 보유 골드 속성 추가
        public int Gold = 0;

        public bool IsMoving = false; 
        public bool IsFacingLeft = false; 
        public float AnimTimer = 0f; 
        public int CurrentFrame = 0;

        public void Update(float deltaTime)
        {
            if (IsDead) return;

            bool keyPressed = false;
            if (Raylib.IsKeyDown(KeyboardKey.W) || Raylib.IsKeyDown(KeyboardKey.Up)) { Position.Y -= Speed * deltaTime; keyPressed = true; }
            if (Raylib.IsKeyDown(KeyboardKey.S) || Raylib.IsKeyDown(KeyboardKey.Down)) { Position.Y += Speed * deltaTime; keyPressed = true; }
            if (Raylib.IsKeyDown(KeyboardKey.A) || Raylib.IsKeyDown(KeyboardKey.Left)) { Position.X -= Speed * deltaTime; keyPressed = true; IsFacingLeft = true; }
            if (Raylib.IsKeyDown(KeyboardKey.D) || Raylib.IsKeyDown(KeyboardKey.Right)) { Position.X += Speed * deltaTime; keyPressed = true; IsFacingLeft = false; }
            
            if (IsMoving != keyPressed) { CurrentFrame = 0; AnimTimer = 0f; }
            IsMoving = keyPressed; 

            AnimTimer += deltaTime; 
            if (AnimTimer >= 0.08f) { AnimTimer = 0f; CurrentFrame++; }
        }
    }

    public class Enemy
    {
        public Vector2 Position; public float Speed = 90f; public int HP = 10; public bool IsDead => HP <= 0;
        public float Damage = 10f; public float HitTimer = 0f; public Vector2 KnockbackDir; public float KnockbackSpeed = 0f;
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