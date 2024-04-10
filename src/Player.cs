using System.Numerics;

namespace zomboi
{
    public class Player
    {
        public string Name { get; }
        public DateTime LastSeen { get; set; }
        public Vector2 Position { get; set; }

        public Player(string name, DateTime lastSeen, Vector2 position)
        {
            Name = name;
            LastSeen = lastSeen;
            Position = position;
        }
    }
}
