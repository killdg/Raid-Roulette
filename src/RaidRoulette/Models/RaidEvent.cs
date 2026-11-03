namespace RaidRoulette.Models
{
    public abstract class RaidEvent : IRaidEvent
    {
        protected RaidEvent(string id, string name, string description, int weight, bool enabled = true)
        {
            Id = id;
            Name = name;
            Description = description;
            Weight = weight;
            Enabled = enabled;
        }

        public string Id { get; }
        public string Name { get; }
        public string Description { get; }
        public int Weight { get; }
        public bool Enabled { get; set; }

        public virtual void Initialize(RaidContext context)
        {
        }

        public virtual void Apply(RaidContext context)
        {
            context.Report.MarkApplied();
        }

        public virtual void Verify(RaidContext context)
        {
        }

        public virtual void Cleanup(RaidContext context)
        {
        }
    }
}
