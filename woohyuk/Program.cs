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