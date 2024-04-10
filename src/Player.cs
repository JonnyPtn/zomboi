using System.Numerics;

namespace zomboi
{
    public class Player
    {
        public string Name { get; }
        public DateTime LastSeen { get; set; }
        public Vector2 Position { get; set; }
        public List<Perk> Perks { get; set; }

        public Player(string name, DateTime lastSeen, Vector2 position, List<Perk> perks)
        {
            Name = name;
            LastSeen = lastSeen;
            Position = position;
            Perks = perks;
        }
    }
}
