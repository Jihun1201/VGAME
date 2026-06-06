// 파일명: EntityGroup.cs
using System;
using Raylib_cs;
using GameCore;

namespace EntityGroup
{
    public class ExpGem
    {
        public Vector2 Position;
        public bool    IsCollected  = false;
        public int     GemTypeIndex = 0;
        public float   AnimTimer    = 0f;
        public int     CurrentFrame = 0;
        public int     MaxFrames    => (GemTypeIndex < 3) ? 5 : 4;
        public bool    IsCoin       => GemTypeIndex < 3;

        // ★ 자석 효과 플래그 및 가속도 속성 추가
        public bool    IsMagnetized = false;
        public float   MagnetSpeed  = 0f;

        public int GetValue()
        {
            switch (GemTypeIndex)
            {
                case 0: return 1;  case 1: return 10; case 2: return 50;
                case 3: return 1;  case 4: return 5;  case 5: return 15;
                case 6: return 50; case 7: return 100; default: return 1;
            }
        }

        // ★ 플레이어의 위치를 받아와서 자석 효과 적용
        public void Update(float deltaTime, Vector2 playerPos)
        {
            AnimTimer += deltaTime;
            if (AnimTimer >= 0.1f) { AnimTimer = 0f; CurrentFrame = (CurrentFrame + 1) % MaxFrames; }

            // 자석 효과: 플레이어 쪽으로 점점 빠르게 날아감
            if (IsMagnetized)
            {
                MagnetSpeed += 1200f * deltaTime; // 가속도가 붙어서 점점 빨라짐
                float dx = playerPos.X - Position.X;
                float dy = playerPos.Y - Position.Y;
                float dist = (float)Math.Sqrt(dx * dx + dy * dy);
                if (dist > 0)
                {
                    Position.X += (dx / dist) * MagnetSpeed * deltaTime;
                    Position.Y += (dy / dist) * MagnetSpeed * deltaTime;
                }
            }
        }
    }

    public class Player
    {
        public Vector2 Position;
        public float   Speed      = 200f;
        public float   MaxHP      = 100f;
        public float   CurrentHP  = 100f;
        public bool    IsDead     => CurrentHP <= 0;
        public int     Gold       = 0;

        public float HitTimer = 0f;
        public bool MagnetActive = false;
        public float ShieldTimer = 0f;
        public bool  IsShielded  => ShieldTimer > 0f;

        public bool IsMoving      = false;
        public bool IsFacingLeft  = false;
        public float AnimTimer    = 0f;
        public int   CurrentFrame = 0;

        public void HealHP(float amount)
        {
            CurrentHP = Math.Min(CurrentHP + amount, MaxHP);
        }

        public void Update(float deltaTime)
        {
            if (IsDead) return;

            if (HitTimer    > 0) HitTimer    -= deltaTime;
            if (ShieldTimer > 0) ShieldTimer -= deltaTime;

            bool keyPressed = false;
            if (Raylib.IsKeyDown(KeyboardKey.W) || Raylib.IsKeyDown(KeyboardKey.Up))
            { Position.Y -= Speed * deltaTime; keyPressed = true; }
            if (Raylib.IsKeyDown(KeyboardKey.S) || Raylib.IsKeyDown(KeyboardKey.Down))
            { Position.Y += Speed * deltaTime; keyPressed = true; }
            if (Raylib.IsKeyDown(KeyboardKey.A) || Raylib.IsKeyDown(KeyboardKey.Left))
            { Position.X -= Speed * deltaTime; keyPressed = true; IsFacingLeft = true; }
            if (Raylib.IsKeyDown(KeyboardKey.D) || Raylib.IsKeyDown(KeyboardKey.Right))
            { Position.X += Speed * deltaTime; keyPressed = true; IsFacingLeft = false; }

            if (IsMoving != keyPressed) { CurrentFrame = 0; AnimTimer = 0f; }
            IsMoving   = keyPressed;
            AnimTimer += deltaTime;
            if (AnimTimer >= 0.08f) { AnimTimer = 0f; CurrentFrame++; }
        }
    }

    public class Enemy
    {
        public Vector2 Position;
        public float   Speed       = 90f;
        public float   HP          = 10f;
        public bool    IsDead      => HP <= 0;
        public float   Damage      = 10f;
        public float   HitTimer    = 0f;
        public Vector2 KnockbackDir;
        public float   KnockbackSpeed = 0f;

        public bool  IsBoss     = false;
        public float Scale      = 3.0f;
        public Color TintColor  = Color.White;

        public void Update(float deltaTime, Vector2 playerPosition)
        {
            if (HitTimer > 0) HitTimer -= deltaTime;

            if (KnockbackSpeed > 0)
            {
                Position.X    += KnockbackDir.X * KnockbackSpeed * deltaTime;
                Position.Y    += KnockbackDir.Y * KnockbackSpeed * deltaTime;
                KnockbackSpeed -= 1500f * deltaTime;
                if (KnockbackSpeed < 0) KnockbackSpeed = 0;
            }
            else
            {
                float dirX = playerPosition.X - Position.X;
                float dirY = playerPosition.Y - Position.Y;
                float dist = (float)Math.Sqrt(dirX * dirX + dirY * dirY);
                if (dist > 0)
                {
                    Position.X += (dirX / dist) * Speed * deltaTime;
                    Position.Y += (dirY / dist) * Speed * deltaTime;
                }
            }
        }
    }
}