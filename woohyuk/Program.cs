/* // 파일명: Program.cs
using System;
using System.Collections.Generic;
using Raylib_cs;
using GameCore;
using EntityGroup;
using CombatSystem;
using UpgradeLogic;

namespace GameCore
{
    public struct Vector2
    {
        public float X;
        public float Y;
        public Vector2(float x, float y) { X = x; Y = y; }

        public static float Distance(Vector2 a, Vector2 b)
        {
            float dx = a.X - b.X;
            float dy = a.Y - b.Y;
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }
    }

    public enum GameState
    {
        Playing,
        LevelUp
    }

    public class Engine
    {
        private Player _player;
        private List<Enemy> _enemies;
        private Weapon _weapon;
        private List<ExpGem> _gems;
        private LevelSystem _levelSystem;
        private GameState _currentState = GameState.Playing;
        private float _spawnTimer = 0f;

        // ★ 카메라 변수 추가
        private Camera2D _camera;

        // 텍스처 관리 변수
        private Texture2D _texIdle;
        private Texture2D _texWalk;
        private Texture2D _texEnemy;
        private Texture2D _texFloor;
        
        // 8종류의 보석 텍스처 리스트
        private List<Texture2D> _gemTextures = new List<Texture2D>();
        private string[] _gemFileNames = {
            "MonedaD.png", "MonedaP.png", "MonedaR.png", "spr_coin_ama.png",
            "spr_coin_azu.png", "spr_coin_gri.png", "spr_coin_roj.png", "spr_coin_strip4.png"
        };

        public Engine()
        {
            _player = new Player { Position = new Vector2(400, 300) };
            _enemies = new List<Enemy>();
            _weapon = new Weapon();
            _gems = new List<ExpGem>();
            _levelSystem = new LevelSystem();

            // ★ 카메라 초기 셋팅 (화면 정중앙을 기준으로 잡음)
            _camera = new Camera2D();
            _camera.Offset = new System.Numerics.Vector2(800f / 2f, 600f / 2f);
            _camera.Rotation = 0f;
            _camera.Zoom = 1.0f;
        }

        public void Run()
        {
            Raylib.InitWindow(800, 600, "Vampire Survivor - Camera System");
            Raylib.SetTargetFPS(60);

            // 텍스처 로드
            _texIdle = Raylib.LoadTexture("idle.png");
            _texWalk = Raylib.LoadTexture("walk.png");
            _texEnemy = Raylib.LoadTexture("Basic 1x.png");
            _texFloor = Raylib.LoadTexture("floor.png"); 

            foreach (var fileName in _gemFileNames)
            {
                _gemTextures.Add(Raylib.LoadTexture(fileName));
            }

            // 메인 루프
            while (!Raylib.WindowShouldClose())
            {
                float deltaTime = Raylib.GetFrameTime();
                Update(deltaTime);
                Render(); 
            }

            // 메모리 해제
            Raylib.UnloadTexture(_texIdle);
            Raylib.UnloadTexture(_texWalk);
            Raylib.UnloadTexture(_texEnemy);
            Raylib.UnloadTexture(_texFloor);
            foreach (var tex in _gemTextures) Raylib.UnloadTexture(tex);
            Raylib.CloseWindow();
        }

        private void Update(float dt)
        {
            if (_currentState == GameState.LevelUp)
            {
                if (Raylib.IsKeyPressed(KeyboardKey.One)) { _player.Speed += 30f; ResumeGame(); }
                else if (Raylib.IsKeyPressed(KeyboardKey.Two)) { _weapon.Damage += 5; ResumeGame(); }
                else if (Raylib.IsKeyPressed(KeyboardKey.Three)) { _weapon.FireCooldown *= 0.8f; ResumeGame(); }
                return; 
            }

            _player.Update(dt);

            // ★ 카메라가 항상 플레이어의 현재 위치를 추적하도록 설정
            _camera.Target = new System.Numerics.Vector2(_player.Position.X, _player.Position.Y);

            _spawnTimer += dt;
            if (_spawnTimer >= 0.8f) 
            {
                _spawnTimer = 0f;
                Random rand = new Random();
                
                // ★ 몬스터가 화면 바깥쪽(플레이어 위치 기준 멀리서)에서 생성되도록 수정
                float spawnX = _player.Position.X + (rand.Next(0, 2) == 0 ? rand.Next(-500, -400) : rand.Next(400, 500));
                float spawnY = _player.Position.Y + (rand.Next(0, 2) == 0 ? rand.Next(-400, -300) : rand.Next(300, 400));
                
                _enemies.Add(new Enemy { Position = new Vector2(spawnX, spawnY) });
            }

            foreach (var enemy in _enemies) enemy.Update(dt, _player.Position);
            _weapon.Update(dt, _player, _enemies);

            for (int i = _enemies.Count - 1; i >= 0; i--)
            {
                if (_enemies[i].IsDead)
                {
                    Random rand = new Random();
                    int randomGemIndex = rand.Next(0, 8); 

                    _gems.Add(new ExpGem { 
                        Position = _enemies[i].Position,
                        GemTypeIndex = randomGemIndex
                    });
                    _enemies.RemoveAt(i);
                }
            }

            // 젬 애니메이션 업데이트 및 획득 처리
            foreach (var gem in _gems)
            {
                gem.Update(dt); 

                if (Vector2.Distance(_player.Position, gem.Position) < 30.0f)
                {
                    gem.IsCollected = true;
                    _levelSystem.AddExp(gem.ExpValue);
                }
            }
            _gems.RemoveAll(g => g.IsCollected);

            if (_levelSystem.IsLevelUpReady) _currentState = GameState.LevelUp;
        }

        private void ResumeGame()
        {
            _levelSystem.IsLevelUpReady = false;
            _currentState = GameState.Playing;
        }

        private void Render()
        {
            Raylib.BeginDrawing();
            Raylib.ClearBackground(Color.Black);

            // ==========================================
            // ★ 카메라 모드 시작 (이 안에서 그리는 것들은 월드 좌표 기준)
            // ==========================================
            Raylib.BeginMode2D(_camera);

            if (_texFloor.Width > 0 && _texFloor.Height > 0)
            {
                // ★ 무한 타일링 로직: 카메라 화면에 보이는 영역만 계산해서 바닥을 깜
                int tileW = _texFloor.Width;
                int tileH = _texFloor.Height;
                float startX = (float)Math.Floor((_camera.Target.X - 400) / tileW) * tileW;
                float startY = (float)Math.Floor((_camera.Target.Y - 300) / tileH) * tileH;

                for (float x = startX; x < _camera.Target.X + 400 + tileW; x += tileW)
                {
                    for (float y = startY; y < _camera.Target.Y + 300 + tileH; y += tileH)
                    {
                        Raylib.DrawTexture(_texFloor, (int)x, (int)y, Color.White);
                    }
                }
            }

            // 1. 보석 스프라이트 애니메이션 렌더링
            foreach (var gem in _gems) 
            {
                Texture2D gemTex = _gemTextures[gem.GemTypeIndex];
                if (gemTex.Width > 0)
                {
                    float frameWidth = (float)gemTex.Width / gem.MaxFrames;
                    float frameHeight = gemTex.Height;

                    Rectangle sourceRec = new Rectangle(gem.CurrentFrame * frameWidth, 0, frameWidth, frameHeight);
                    float scale = 1.0f; 
                    Rectangle destRec = new Rectangle(gem.Position.X, gem.Position.Y, frameWidth * scale, frameHeight * scale);
                    System.Numerics.Vector2 origin = new System.Numerics.Vector2((frameWidth * scale) / 2, (frameHeight * scale) / 2);

                    Raylib.DrawTexturePro(gemTex, sourceRec, destRec, origin, 0f, Color.White);
                }
                else 
                {
                    Raylib.DrawCircle((int)gem.Position.X, (int)gem.Position.Y, 5, Color.SkyBlue);
                }
            }

            // 2. 투사체 렌더링
            foreach (var p in _weapon.Projectiles) 
            {
                Raylib.DrawCircle((int)p.Position.X, (int)p.Position.Y, 5, Color.Yellow);
            }

            // 3. 몬스터 렌더링
            foreach (var e in _enemies) 
            {
                if (_texEnemy.Width > 0)
                {
                    int cols = 5;
                    int rows = 3;
                    float frameWidth = (float)_texEnemy.Width / cols;
                    float frameHeight = (float)_texEnemy.Height / rows;

                    Rectangle sourceRec = new Rectangle(0, 0, frameWidth, frameHeight);
                    
                    float scale = 3.0f; 
                    Rectangle destRec = new Rectangle(e.Position.X, e.Position.Y, frameWidth * scale, frameHeight * scale);
                    
                    System.Numerics.Vector2 origin = new System.Numerics.Vector2((frameWidth * scale) / 2, (frameHeight * scale) / 2);
                    Raylib.DrawTexturePro(_texEnemy, sourceRec, destRec, origin, 0f, Color.White);
                }
                else
                {
                    Raylib.DrawRectangle((int)e.Position.X - 10, (int)e.Position.Y - 10, 20, 20, Color.Red);
                }
            }

            // 4. 플레이어 렌더링
            if (_texIdle.Width > 0 && _texWalk.Width > 0)
            {
                Texture2D currentTex = _player.IsMoving ? _texWalk : _texIdle;
                int maxFrames = _player.IsMoving ? 24 : 10;
                int cols = _player.IsMoving ? 4 : 10;
                int rows = _player.IsMoving ? 6 : 1;

                int currentFrameNum = _player.CurrentFrame % maxFrames;
                float frameWidth = (float)currentTex.Width / cols;
                float frameHeight = (float)currentTex.Height / rows;

                float sourceX = (currentFrameNum % cols) * frameWidth;
                float sourceY = (currentFrameNum / cols) * frameHeight;

                float renderWidth = _player.IsFacingLeft ? frameWidth : -frameWidth;
                Rectangle sourceRec = new Rectangle(sourceX, sourceY, renderWidth, frameHeight);
                
                float scale = 1.5f;
                Rectangle destRec = new Rectangle(_player.Position.X, _player.Position.Y, frameWidth * scale, frameHeight * scale);
                System.Numerics.Vector2 origin = new System.Numerics.Vector2((frameWidth * scale) / 2, (frameHeight * scale) / 2);

                Raylib.DrawTexturePro(currentTex, sourceRec, destRec, origin, 0f, Color.White);
            }
            else
            {
                Raylib.DrawCircle((int)_player.Position.X, (int)_player.Position.Y, 15, Color.Blue);
            }

            // ★ 카메라 모드 종료
            Raylib.EndMode2D();
            // ==========================================


            // 5. UI 렌더링 (HUD는 화면에 고정되어야 하므로 카메라 바깥에서 그립니다)
            float expRatio = (float)_levelSystem.CurrentExp / _levelSystem.MaxExp;
            Raylib.DrawRectangle(0, 0, 800, 20, Color.Black);
            Raylib.DrawRectangle(0, 0, (int)(800 * expRatio), 20, Color.Blue);
            
            Raylib.DrawText($"LV: {_levelSystem.Level}", 10, 25, 20, Color.White);
            Raylib.DrawText($"ATK: {_weapon.Damage}", 10, 50, 15, Color.LightGray);

            if (_currentState == GameState.LevelUp)
            {
                Raylib.DrawRectangle(0, 0, 800, 600, new Color(0, 0, 0, 150));
                Raylib.DrawText("LEVEL UP! (1, 2, 3)", 300, 200, 30, Color.Gold);
                
                Raylib.DrawRectangle(100, 280, 180, 100, Color.DarkBlue);
                Raylib.DrawText("[1] Movement\n    Speed +", 115, 310, 20, Color.White);

                Raylib.DrawRectangle(310, 280, 180, 100, Color.DarkPurple);
                Raylib.DrawText("[2] Weapon\n    Damage +", 325, 310, 20, Color.White);

                Raylib.DrawRectangle(520, 280, 180, 100, Color.DarkGreen);
                Raylib.DrawText("[3] Attack\n    Speed +", 535, 310, 20, Color.White);
            }

            Raylib.EndDrawing();
        }
    }
}

namespace EntityGroup
{
    public class ExpGem 
    { 
        public Vector2 Position; 
        public int ExpValue = 5; 
        public bool IsCollected = false; 
        public int GemTypeIndex = 0;
        
        // 애니메이션 속성
        public float AnimTimer = 0f;
        public int CurrentFrame = 0;
        
        // 0~2번(Moneda)는 5프레임, 3~7번(spr_coin)은 4프레임
        public int MaxFrames => (GemTypeIndex < 3) ? 5 : 4; 

        public void Update(float deltaTime)
        {
            AnimTimer += deltaTime;
            if (AnimTimer >= 0.1f) // 0.1초마다 동전 프레임 넘김
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

        public bool IsMoving = false;
        public bool IsFacingLeft = false;
        public float AnimTimer = 0f;
        public int CurrentFrame = 0;

        public void Update(float deltaTime)
        {
            IsMoving = false; 

            if (Raylib.IsKeyDown(KeyboardKey.W)) { Position.Y -= Speed * deltaTime; IsMoving = true; }
            if (Raylib.IsKeyDown(KeyboardKey.S)) { Position.Y += Speed * deltaTime; IsMoving = true; }
            if (Raylib.IsKeyDown(KeyboardKey.A)) { Position.X -= Speed * deltaTime; IsMoving = true; IsFacingLeft = true; }
            if (Raylib.IsKeyDown(KeyboardKey.D)) { Position.X += Speed * deltaTime; IsMoving = true; IsFacingLeft = false; }

            AnimTimer += deltaTime;
            if (AnimTimer >= 0.08f) 
            {
                AnimTimer = 0f;
                CurrentFrame++;
            }
        }
    }

    public class Enemy
    {
        public Vector2 Position; 
        public float Speed = 90f; 
        public int HP = 10; 
        public bool IsDead => HP <= 0;

        public void Update(float deltaTime, Vector2 playerPosition)
        {
            float dirX = playerPosition.X - Position.X; 
            float dirY = playerPosition.Y - Position.Y;
            float distance = (float)Math.Sqrt(dirX * dirX + dirY * dirY);
            if (distance > 0) 
            { 
                Position.X += (dirX / distance) * Speed * deltaTime; 
                Position.Y += (dirY / distance) * Speed * deltaTime; 
            }
        }
    }
}

namespace CombatSystem
{
    public class Projectile 
    { 
        public Vector2 Position; 
        public Vector2 Direction; 
        public float Speed = 500f; 
        public int Damage = 5; 
        public bool IsActive = true;
        public void Update(float deltaTime) 
        { 
            Position.X += Direction.X * Speed * deltaTime; 
            Position.Y += Direction.Y * Speed * deltaTime; 
        }
    }

    public class Weapon 
    {  
        public List<Projectile> Projectiles = new List<Projectile>(); 
        public float FireCooldown = 0.5f; 
        public int Damage = 5; 
        private float _timer = 0f;

        public void Update(float dt, Player player, List<Enemy> enemies) 
        {
            _timer += dt; 
            if (_timer >= FireCooldown && enemies.Count > 0) 
            { 
                _timer = 0f; 
                FireAtNearest(player.Position, enemies); 
            }
            foreach (var p in Projectiles) 
            {
                if (!p.IsActive) continue; 
                p.Update(dt);
                foreach (var e in enemies) 
                {
                    if (e.IsDead) continue;
                    if (Vector2.Distance(p.Position, e.Position) < 15.0f) 
                    { 
                        e.HP -= p.Damage; 
                        p.IsActive = false; 
                        break; 
                    }
                }
            }
            Projectiles.RemoveAll(p => !p.IsActive);
        }

        private void FireAtNearest(Vector2 playerPos, List<Enemy> enemies) 
        {
            Enemy nearest = null; 
            float minDistance = float.MaxValue;
            foreach (var e in enemies) 
            {
                float dist = Vector2.Distance(playerPos, e.Position); 
                if (dist < minDistance) 
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
                Projectiles.Add(new Projectile 
                { 
                    Position = playerPos, 
                    Direction = new Vector2(dirX / dist, dirY / dist), 
                    Damage = this.Damage 
                });
            }
        }
    }
}

namespace UpgradeLogic
{
    public class LevelSystem 
    { 
        public int Level = 1; 
        public int CurrentExp = 0; 
        public int MaxExp = 20; 
        public bool IsLevelUpReady = false;

        public void AddExp(int amount) 
        { 
            CurrentExp += amount; 
            if (CurrentExp >= MaxExp) 
            { 
                CurrentExp -= MaxExp; 
                Level++; 
                MaxExp = (int)(MaxExp * 1.5f); 
                IsLevelUpReady = true; 
            } 
        }
    }
}

class Program 
{ 
    static void Main(string[] args) 
    { 
        Engine game = new Engine(); 
        game.Run(); 
    } 
} */

// 파일명: Program.cs
using System;
using GameCore; // Engine을 가져오기 위한 using

class Program 
{ 
    static void Main(string[] args) 
    { 
        Engine game = new Engine(); 
        game.Run(); 
    } 
}