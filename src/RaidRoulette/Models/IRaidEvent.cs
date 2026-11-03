namespace RaidRoulette.Models
{
    public interface IRaidEvent
    {
        string Id { get; }

        string Name { get; }

        string Description { get; }

        int Weight { get; }

        bool Enabled { get; }

        void Initialize(RaidContext context);
        void Apply(RaidContext context);
        void Verify(RaidContext context);
        void Cleanup(RaidContext context);
    }
}
