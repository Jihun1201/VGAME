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

        public void Update(float deltaTime, Vector2 playerPos)
        {
            AnimTimer += deltaTime;
            if (AnimTimer >= 0.1f) { AnimTimer = 0f; CurrentFrame = (CurrentFrame + 1) % MaxFrames; }

            if (IsMagnetized)
            {
                MagnetSpeed += 1200f * deltaTime;
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

    // ──────────────────────────────────────────────────────────────
    // 보스 장판 (경고→활성 2단계)
    // ──────────────────────────────────────────────────────────────
    public class BossZone
    {
        public Vector2 Position;
        public float   Radius;
        public float   Timer      = 0f;
        public float   WarnTime   = 1.2f;
        public float   ActiveTime = 1.8f;
        public bool    IsWarning  => Timer < WarnTime;
        public bool    IsActive   => Timer >= WarnTime && Timer < WarnTime + ActiveTime;
        public bool    IsDone     => Timer >= WarnTime + ActiveTime;
        public float   Damage     = 30f;
        public float   HitTimer   = 0f;
    }

    // ──────────────────────────────────────────────────────────────
    // 보스 추적 투사체 (최종보스 전용)
    // ──────────────────────────────────────────────────────────────
    public class BossProjectile
    {
        public Vector2 Position;
        public Vector2 Velocity;
        public float   Damage   = 35f;
        public float   Timer    = 0f;
        public float   Lifetime = 6f;
        public bool    IsActive = true;
        public float   HitTimer = 0f;
        public float   Radius   = 10f;
    }

    public class Player
    {
        public Vector2 Position;
        public float   Speed      = 200f;
        public float   MaxHP      = 100f;
        public float   CurrentHP  = 100f;
        public bool    IsDead     => CurrentHP <= 0;
        public int     Gold       = 0;

        public float HitTimer    = 0f;
        public bool  MagnetActive = false;
        public float ShieldTimer  = 0f;
        public bool  IsShielded   => ShieldTimer > 0f;
        public int   ReviveCount  = 0;

        public bool  IsMoving      = false;
        public bool  IsFacingLeft  = false;
        public float AnimTimer     = 0f;
        public int   CurrentFrame  = 0;

        public void HealHP(float amount) { CurrentHP = Math.Min(CurrentHP + amount, MaxHP); }

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

    public enum BossPatternState { None, DashWarn, Dashing }

    public class Enemy
    {
        public Vector2 Position;
        public float   Speed       = 90f;
        public float   HP          = 10f;
        public float   MaxHP       = 10f;
        public bool    IsDead      => HP <= 0;
        public float   Damage      = 10f;

        // 무기별 피격 쿨다운
        public float ProjectileHitTimer = 0f;
        public float MeleeHitTimer      = 0f;
        public float OrbitalHitTimer    = 0f;
        public float HitTimer           = 0f;

        public Vector2 KnockbackDir;
        public float   KnockbackSpeed = 0f;

        public bool  IsBoss      = false;
        public bool  IsFinalBoss = false;
        public float Scale       = 3.0f;
        public Color TintColor   = Color.White;

        // ── 보스 패턴 공통 ──
        public BossPatternState CurrentPattern  = BossPatternState.None;
        public float            PatternTimer    = 0f;
        public float            PatternInterval = 5f;
        public int              PatternPhase    = 0;   // 순환 인덱스

        // ── 돌진 패턴 ──
        public float   DashWarnRemain = 1.0f;
        public float   DashRemain     = 0f;
        public Vector2 DashDir;
        public Vector2 DashWarnStart;
        public Vector2 DashWarnEnd;
        public bool    IsShowingDashWarn => CurrentPattern == BossPatternState.DashWarn;

        // ── 장판 패턴 ──
        public bool SpawnZoneRequest = false;

        // ── 최종보스 투사체 발사 타이머 ──
        public float FinalBossShotTimer    = 0f;
        public float FinalBossShotInterval = 2.2f;
        public bool  FinalBossShotRequest  = false;

        public void InitBoss(float hp, float interval = 5f)
        {
            HP = hp; MaxHP = hp; PatternInterval = interval;
        }

        public void Update(float dt, Vector2 playerPos)
        {
            if (IsDead) return;

            if (HitTimer           > 0) HitTimer           -= dt;
            if (ProjectileHitTimer > 0) ProjectileHitTimer -= dt;
            if (MeleeHitTimer      > 0) MeleeHitTimer      -= dt;
            if (OrbitalHitTimer    > 0) OrbitalHitTimer    -= dt;

            if (IsBoss) UpdateBossPattern(dt, playerPos);

            // 돌진 중: 패턴이 이동 담당
            if (CurrentPattern == BossPatternState.Dashing)
            {
                Position.X += DashDir.X * 750f * dt;
                Position.Y += DashDir.Y * 750f * dt;
                return;
            }
            // 경고 중: 정지
            if (CurrentPattern == BossPatternState.DashWarn) return;

            // 일반 이동
            if (KnockbackSpeed > 0)
            {
                Position.X    += KnockbackDir.X * KnockbackSpeed * dt;
                Position.Y    += KnockbackDir.Y * KnockbackSpeed * dt;
                KnockbackSpeed -= 1500f * dt;
                if (KnockbackSpeed < 0) KnockbackSpeed = 0;
            }
            else
            {
                float dx = playerPos.X - Position.X;
                float dy = playerPos.Y - Position.Y;
                float dist = (float)Math.Sqrt(dx * dx + dy * dy);
                if (dist > 0)
                {
                    Position.X += (dx / dist) * Speed * dt;
                    Position.Y += (dy / dist) * Speed * dt;
                }
            }
        }

        private void UpdateBossPattern(float dt, Vector2 playerPos)
        {
            // ── 진행 중인 패턴 처리 ──
            if (CurrentPattern == BossPatternState.DashWarn)
            {
                DashWarnRemain -= dt;
                if (DashWarnRemain <= 0f)
                {
                    CurrentPattern = BossPatternState.Dashing;
                    DashRemain     = 0.55f;
                }
                return;
            }
            if (CurrentPattern == BossPatternState.Dashing)
            {
                DashRemain -= dt;
                if (DashRemain <= 0f) CurrentPattern = BossPatternState.None;
                return;
            }

            // ── 최종보스 자동 투사체 발사 ──
            if (IsFinalBoss)
            {
                FinalBossShotTimer += dt;
                if (FinalBossShotTimer >= FinalBossShotInterval)
                {
                    FinalBossShotTimer    = 0f;
                    FinalBossShotRequest  = true;
                }
            }

            // ── 패턴 주기 타이머 ──
            PatternTimer += dt;
            if (PatternTimer < PatternInterval) return;
            PatternTimer = 0f;

            // 패턴 순환: 0=돌진, 1=장판
            int chosen = PatternPhase % 2;
            PatternPhase++;

            if (chosen == 0)
            {
                // 돌진 패턴
                float dx = playerPos.X - Position.X;
                float dy = playerPos.Y - Position.Y;
                float d  = (float)Math.Sqrt(dx * dx + dy * dy);
                DashDir = (d > 0) ? new Vector2(dx / d, dy / d) : new Vector2(1, 0);

                DashWarnRemain = 1.0f;
                DashWarnStart  = Position;
                DashWarnEnd    = new Vector2(Position.X + DashDir.X * 700f,
                                              Position.Y + DashDir.Y * 700f);
                CurrentPattern = BossPatternState.DashWarn;
            }
            else
            {
                // 장판 패턴
                SpawnZoneRequest = true;
            }
        }
    }
}