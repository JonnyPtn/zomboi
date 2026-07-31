using System.Numerics;

namespace zomboi
{
    public class Player
    {
        public string Name { get; }
        public DateTime LastSeen { get; set; }
        public Vector2 Position { get; set; }
        public List<Perk> Perks { get; set; }
        public bool Online { get; set; }

        /// <summary>
        /// How long their current character has been alive in game, as of the last perk log line we
        /// saw for them, or null if we haven't seen one yet. It goes back to zero when they die and
        /// make a new character.
        /// </summary>
        public int? HoursSurvived { get; set; }

        /// <summary>
        /// Whether we've had a position for them at all - the logs are the only source of one, so a
        /// player we've only heard about from a line without one hasn't got a position yet.
        /// </summary>
        public bool HasPosition { get { return Position != Vector2.Zero; } }

        public Player(string name, DateTime seenTime, Vector2 position, List<Perk> perks)
        {
            Name = name;
            LastSeen = seenTime;
            Position = position;
            Perks = perks;
            Online = false;
        }
    }
}
