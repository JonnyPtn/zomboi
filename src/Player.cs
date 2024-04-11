﻿using System.Numerics;

namespace zomboi
{
    public class Player
    {
        public string Name { get; }
        public DateTime LastSeen { get; set; }
        public Vector2 Position { get; set; }
        public List<Perk> Perks { get; set; }
        public bool Online  { get; set; }

        public Player(string name, Vector2 position, List<Perk> perks)
        {
            Name = name;
            LastSeen = DateTime.Now;
            Position = position;
            Perks = perks;
            Online = false;
        }
    }
}
