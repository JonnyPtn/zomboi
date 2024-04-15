namespace zomboi
{
    public class Perk
    {
        public string Name { get; set; }
        public int Level { get; set; }

        public Perk(string name, int level)
        {
            Name = name;
            Level = level;
        }
    }
}
